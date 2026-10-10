using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;

namespace PSTools
{
    static class Launch
    {
        static string Enc(string script)
        {
            return Convert.ToBase64String(Encoding.Unicode.GetBytes(script));
        }

        public static void PowerShell(string dir, string tail, bool admin)
        {
            string cmd = "Set-Location -LiteralPath '" + dir.Replace("'", "''") + "'" + (string.IsNullOrEmpty(tail) ? "" : "; " + tail);
            var psi = new ProcessStartInfo("powershell.exe", "-NoExit -NoProfile -EncodedCommand " + Enc(cmd)) { WorkingDirectory = dir, UseShellExecute = true };
            if (admin) psi.Verb = "runas";
            Process.Start(psi);
        }

        public static void Console(string script, bool admin, bool keepOpen)
        {
            string full = script;
            if (!keepOpen) full += "\r\nWrite-Host ''\r\nWrite-Host 'Finished. Press Enter to close this window.' -ForegroundColor Green\r\n[void](Read-Host)";
            var psi = new ProcessStartInfo("powershell.exe", (keepOpen ? "-NoExit " : "") + "-NoProfile -EncodedCommand " + Enc(full)) { UseShellExecute = true };
            if (admin) psi.Verb = "runas";
            Process.Start(psi);
        }

        public static string Capture(string exe, string args, string workDir, Ctx c, int timeoutMs, bool noPrompt)
        {
            var psi = new ProcessStartInfo(exe, args)
            {
                UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8
            };
            if (!string.IsNullOrEmpty(workDir)) psi.WorkingDirectory = workDir;
            if (noPrompt) psi.EnvironmentVariables["GIT_TERMINAL_PROMPT"] = "0";
            var sb = new StringBuilder();
            using (var p = new Process { StartInfo = psi })
            {
                p.OutputDataReceived += (s, e) => { if (e.Data != null) lock (sb) sb.AppendLine(e.Data); };
                p.ErrorDataReceived += (s, e) => { if (e.Data != null) lock (sb) sb.AppendLine(e.Data); };
                p.Start();
                p.BeginOutputReadLine();
                p.BeginErrorReadLine();
                var started = DateTime.Now;
                while (!p.WaitForExit(200))
                {
                    if (c != null && c.Cancel) { try { p.Kill(); } catch { } throw new OperationCanceledException(); }
                    if ((DateTime.Now - started).TotalMilliseconds > timeoutMs) { try { p.Kill(); } catch { } lock (sb) sb.AppendLine("(timed out)"); break; }
                }
                p.WaitForExit();
            }
            lock (sb) return sb.ToString().Trim();
        }

        public static string TailLines(string text, int n)
        {
            var lines = new List<string>();
            foreach (var l in text.Replace("\r", "").Split('\n')) if (l.Trim().Length > 0) lines.Add(l.Trim());
            int skip = Math.Max(0, lines.Count - n);
            return string.Join("\n", lines.GetRange(skip, lines.Count - skip).ToArray());
        }
    }
}
