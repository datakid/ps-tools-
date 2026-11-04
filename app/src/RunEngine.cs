using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Web.Script.Serialization;

namespace PSTools
{
    class RunRequest
    {
        public ActionShell Shell = ActionShell.PowerShell;
        public string Script = "";
        public string Dir;
        public string[] Paths = new string[0];
        public bool AsTable;
    }

    class RunSession
    {
        public Process Process;
        public Stopwatch Watch = Stopwatch.StartNew();
        public volatile bool Stopped;
        public volatile bool Finished;
        public DateTime LastOutput = DateTime.UtcNow;
        public string TempFile;
        TimeSpan lastCpu = TimeSpan.Zero;

        public void Stop()
        {
            Stopped = true;
            try
            {
                if (Process != null && !Process.HasExited)
                {
                    var psi = new ProcessStartInfo("taskkill.exe", "/T /F /PID " + Process.Id) { CreateNoWindow = true, UseShellExecute = false };
                    using (var k = Process.Start(psi)) { if (k != null) k.WaitForExit(5000); }
                }
            }
            catch { }
        }

        public bool LooksIdle(int seconds)
        {
            try
            {
                if (Finished || Process == null || Process.HasExited) return false;
                if ((DateTime.UtcNow - LastOutput).TotalSeconds < seconds) return false;
                var cpu = Process.TotalProcessorTime;
                bool idle = (cpu - lastCpu).TotalMilliseconds < 30;
                lastCpu = cpu;
                return idle;
            }
            catch { return false; }
        }
    }

    static class RunEngine
    {
        static readonly Regex Ansi = new Regex("\\x1B\\[[0-9;?]*[ -/]*[@-~]", RegexOptions.Compiled);

        public static string StripAnsi(string s) { return Ansi.Replace(s ?? "", ""); }

        public static string FindPwsh()
        {
            try
            {
                string onPath = Fs.Which("pwsh");
                if (onPath != null) return onPath;
                string pf = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "PowerShell", "7", "pwsh.exe");
                if (File.Exists(pf)) return pf;
            }
            catch { }
            return null;
        }

        static string Lit(string s) { return "'" + Placeholders.EscapePs(s ?? "") + "'"; }

        public static string BuildScript(RunRequest r)
        {
            var sb = new StringBuilder();
            sb.AppendLine("$ErrorActionPreference = 'Continue'");
            sb.AppendLine("[Console]::OutputEncoding = [Text.Encoding]::UTF8");
            sb.AppendLine("$OutputEncoding = [Text.Encoding]::UTF8");
            sb.AppendLine("$ProgressPreference = 'SilentlyContinue'");
            sb.AppendLine("if ($PSStyle) { $PSStyle.OutputRendering = 'PlainText' }");
            if (!string.IsNullOrEmpty(r.Dir)) sb.AppendLine("Set-Location -LiteralPath " + Lit(r.Dir));
            sb.AppendLine("$Paths = @(" + string.Join(",", (r.Paths ?? new string[0]).Select(Lit).ToArray()) + ")");
            sb.AppendLine("$Dir = " + Lit(r.Dir ?? ""));
            if (!r.AsTable) sb.AppendLine(r.Script);
            else
            {
                sb.AppendLine("$__r = @( & {");
                sb.AppendLine(r.Script);
                sb.AppendLine("} )");
                sb.AppendLine("if ($__r.Count -eq 0) { '##PST-EMPTY##'; return }");
                sb.AppendLine("if (@($__r | Where-Object { $_ -isnot [string] -and $_ -isnot [ValueType] }).Count -eq 0) {");
                sb.AppendLine("  '##PST-TEXT##'; $__r | ForEach-Object { \"$_\" }; return }");
                sb.AppendLine("'##PST-JSON##'");
                sb.AppendLine("$__r | Select-Object -First 5000 | ForEach-Object {");
                sb.AppendLine("  $o = [ordered]@{}");
                sb.AppendLine("  foreach ($p in $_.PSObject.Properties) { if ($p.MemberType -match 'Property') { $v = $p.Value; $o[$p.Name] = if ($null -eq $v) { '' } elseif ($v -is [datetime]) { $v.ToString('yyyy-MM-dd HH:mm:ss') } else { \"$v\" } } }");
                sb.AppendLine("  [pscustomobject]$o } | ConvertTo-Json -Compress -Depth 2");
            }
            return sb.ToString();
        }

        public static ProcessStartInfo BuildStart(RunRequest r, out string tempFile)
        {
            tempFile = null;
            var psi = new ProcessStartInfo
            {
                UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true, RedirectStandardInput = true,
                StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8
            };
            if (!string.IsNullOrEmpty(r.Dir) && Directory.Exists(r.Dir)) psi.WorkingDirectory = r.Dir;
            psi.EnvironmentVariables["NO_COLOR"] = "1";
            if (r.Shell == ActionShell.Cmd)
            {
                string tmp = Path.Combine(Journal.Dir, "tmp");
                Directory.CreateDirectory(tmp);
                tempFile = Path.Combine(tmp, "run-" + Guid.NewGuid().ToString("N") + ".cmd");
                var body = new StringBuilder();
                body.Append("@echo off\r\nchcp 65001 >nul\r\n");
                if (!string.IsNullOrEmpty(r.Dir)) body.Append("cd /d \"" + r.Dir + "\"\r\n");
                body.Append(r.Script.Replace("\r\n", "\n").Replace("\n", "\r\n")).Append("\r\n");
                File.WriteAllText(tempFile, body.ToString(), new UTF8Encoding(false));
                psi.FileName = "cmd.exe";
                psi.Arguments = "/d /c \"" + tempFile + "\"";
                return psi;
            }
            string exe = r.Shell == ActionShell.Pwsh ? (FindPwsh() ?? "pwsh.exe") : "powershell.exe";
            psi.FileName = exe;
            psi.Arguments = "-NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass -InputFormat None -OutputFormat Text -EncodedCommand " + Convert.ToBase64String(Encoding.Unicode.GetBytes(BuildScript(r)));
            return psi;
        }

        public static RunSession Start(RunRequest r, Action<string, bool> onLine, Action<int, long, bool> onExit)
        {
            string temp;
            var session = new RunSession();
            ProcessStartInfo psi;
            try { psi = BuildStart(r, out temp); }
            catch (Exception ex) { onLine("Could not prepare the command: " + ex.Message, true); onExit(-1, 0, false); session.Finished = true; return session; }
            session.TempFile = temp;
            try
            {
                var p = new Process { StartInfo = psi, EnableRaisingEvents = true };
                p.OutputDataReceived += (o, e) => { if (e.Data != null) { session.LastOutput = DateTime.UtcNow; onLine(StripAnsi(e.Data), false); } };
                p.ErrorDataReceived += (o, e) => { if (e.Data != null) { session.LastOutput = DateTime.UtcNow; onLine(StripAnsi(e.Data), true); } };
                p.Start();
                session.Process = p;
                try { p.StandardInput.Close(); } catch { }
                p.BeginOutputReadLine();
                p.BeginErrorReadLine();
                var waiter = new Thread(() =>
                {
                    try { p.WaitForExit(); } catch { }
                    int code = -1;
                    try { code = p.ExitCode; } catch { }
                    session.Finished = true;
                    if (temp != null) { try { File.Delete(temp); } catch { } }
                    onExit(code, session.Watch.ElapsedMilliseconds, session.Stopped);
                }) { IsBackground = true };
                waiter.Start();
            }
            catch (Exception ex)
            {
                session.Finished = true;
                if (temp != null) { try { File.Delete(temp); } catch { } }
                onLine("Could not start " + psi.FileName + ": " + ex.Message, true);
                onExit(-1, 0, false);
            }
            return session;
        }

        public static string RunSync(RunRequest r, out int exitCode, int timeoutSeconds)
        {
            var sb = new StringBuilder();
            var done = new ManualResetEvent(false);
            int code = -1;
            object gate = new object();
            var s = Start(r, (line, err) => { lock (gate) sb.AppendLine(line); }, (c, ms, stopped) => { code = c; done.Set(); });
            if (!done.WaitOne(timeoutSeconds * 1000))
            {
                s.Stop();
                done.WaitOne(3000);
                lock (gate) sb.AppendLine("(stopped after " + timeoutSeconds + " s)");
                exitCode = -1;
                return sb.ToString();
            }
            Thread.Sleep(50);
            exitCode = code;
            lock (gate) return sb.ToString();
        }

        public static Result ParseTable(string output, out string text)
        {
            text = null;
            string normalized = (output ?? "").Replace("\r\n", "\n");
            int m = normalized.IndexOf("##PST-", StringComparison.Ordinal);
            if (m < 0) { text = normalized.Trim(); return null; }
            int eol = normalized.IndexOf('\n', m);
            string marker = eol < 0 ? normalized.Substring(m) : normalized.Substring(m, eol - m);
            string rest = eol < 0 ? "" : normalized.Substring(eol + 1);
            if (marker.StartsWith("##PST-EMPTY##")) { text = "The command returned nothing."; return null; }
            if (marker.StartsWith("##PST-TEXT##")) { text = rest.Trim(); return null; }
            object parsed;
            try { parsed = new JavaScriptSerializer { MaxJsonLength = int.MaxValue }.DeserializeObject(rest.Trim()); }
            catch { text = rest.Trim(); return null; }
            var items = new List<Dictionary<string, object>>();
            var single = parsed as Dictionary<string, object>;
            if (single != null) items.Add(single);
            else
            {
                var seq = parsed as IEnumerable;
                if (seq != null) foreach (object o in seq) { var d = o as Dictionary<string, object>; if (d != null) items.Add(d); }
            }
            var columns = new List<string>();
            foreach (var d in items) foreach (var k in d.Keys) if (!columns.Contains(k) && columns.Count < 40) columns.Add(k);
            var rows = new List<string[]>();
            foreach (var d in items)
            {
                var row = new string[columns.Count];
                for (int i = 0; i < columns.Count; i++) { object v; row[i] = d.TryGetValue(columns[i], out v) && v != null ? Convert.ToString(v) : ""; }
                rows.Add(row);
            }
            int pathCol = columns.IndexOf("FullName");
            return new Result { Summary = rows.Count + " row" + (rows.Count == 1 ? "" : "s"), Columns = columns.ToArray(), Rows = rows, PathColumn = pathCol };
        }
    }
}
