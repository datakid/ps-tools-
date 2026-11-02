using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace PSTools
{
    static class Executor
    {
        public const int TimeoutSeconds = 120;

        public static string FindGitRoot(string dir)
        {
            try
            {
                string d = dir;
                while (!string.IsNullOrEmpty(d))
                {
                    if (Directory.Exists(Path.Combine(d, ".git")) || File.Exists(Path.Combine(d, ".git"))) return d;
                    var parent = Directory.GetParent(d);
                    d = parent == null ? null : parent.FullName;
                }
            }
            catch { }
            return "";
        }

        public static PlaceholderContext ContextFor(string[] paths)
        {
            return new PlaceholderContext { Paths = paths, Clip = Clip.Text(), GitRoot = FindGitRoot };
        }

        static string WorkDir(string[] paths)
        {
            if (paths == null || paths.Length == 0) return null;
            try
            {
                if (Directory.Exists(paths[0])) return paths[0];
                string d = Path.GetDirectoryName(paths[0]);
                return d != null && Directory.Exists(d) ? d : null;
            }
            catch { return null; }
        }

        public static void Run(ActionSpec s, string[] paths)
        {
            if (s.Legacy != null)
            {
                Runner.Execute(s.Legacy, s.Legacy.Multi ? paths : new[] { paths[0] }, null);
                return;
            }
            if (s.Admin) { Program.Msg(s.Title + " needs administrator rights. Elevated script actions arrive with the Run console."); return; }

            var batches = new List<string[]>();
            if (s.Multi == MultiMode.Each && paths.Length > 1) foreach (var p in paths) batches.Add(new[] { p });
            else if (s.Multi == MultiMode.Single) batches.Add(new[] { paths[0] });
            else batches.Add(paths);

            string firstScript;
            try { firstScript = Placeholders.Expand(s.Command, s.Shell, ContextFor(batches[0])); }
            catch (PlaceholderException ex) { Program.Fail(ex.Message); return; }

            if (s.Risk != Risk.Safe)
            {
                string note = s.Risk == Risk.Destructive ? "This cannot be undone.\n\n" + firstScript : "It may change files.";
                if (!Program.Ask("Run \"" + s.Title + "\"?\n\n" + note)) return;
            }

            var output = new StringBuilder();
            foreach (var batch in batches)
            {
                string script;
                try { script = Placeholders.Expand(s.Command, s.Shell, ContextFor(batch)); }
                catch (PlaceholderException ex) { Program.Fail(ex.Message); return; }
                int code;
                string text = Execute(s.Shell, script, WorkDir(batch), out code);
                if (output.Length > 0) output.AppendLine();
                output.Append(text.TrimEnd());
                if (code != 0) output.Append("\n(exit code " + code + ")");
            }
            Deliver(s, output.ToString().Trim());
        }

        static void Deliver(ActionSpec s, string text)
        {
            switch (s.Output)
            {
                case ActionOutput.Clipboard:
                    if (text.Length == 0) { Program.Msg("Nothing to copy: " + s.Title + " returned no output."); return; }
                    Clip.Set(text);
                    Runner.Notify(null, "Copied " + text.Split('\n').Length + " line" + (text.Contains("\n") ? "s" : "") + " from " + s.Title, false);
                    break;
                case ActionOutput.Toast:
                    Runner.Notify(null, text.Length == 0 ? s.Title + " done" : text.Split('\n')[0], false);
                    break;
                case ActionOutput.None:
                    break;
                default:
                    Program.Msg(text.Length == 0 ? s.Title + " finished with no output." : (text.Length > 1800 ? text.Substring(0, 1800) + "\n..." : text));
                    break;
            }
        }

        public static string RunText(string script, string dir)
        {
            int code;
            string text = Execute(ActionShell.PowerShell, script, dir, out code);
            return code == 0 ? text.TrimEnd() : text.TrimEnd() + "\n(exit code " + code + ")";
        }

        static string Execute(ActionShell shell, string script, string dir, out int exitCode)
        {
            exitCode = 0;
            string tempFile = null;
            var psi = new ProcessStartInfo
            {
                UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8
            };
            if (!string.IsNullOrEmpty(dir)) psi.WorkingDirectory = dir;
            if (shell == ActionShell.Cmd)
            {
                tempFile = Path.Combine(Path.GetTempPath(), "pstools-" + Guid.NewGuid().ToString("N") + ".cmd");
                File.WriteAllText(tempFile, "@echo off\r\n" + script.Replace("\r\n", "\n").Replace("\n", "\r\n") + "\r\n", Encoding.Default);
                psi.FileName = "cmd.exe";
                psi.Arguments = "/d /c \"" + tempFile + "\"";
                psi.StandardOutputEncoding = Encoding.Default;
                psi.StandardErrorEncoding = Encoding.Default;
            }
            else
            {
                string full = "$ProgressPreference='SilentlyContinue'; [Console]::OutputEncoding=[System.Text.Encoding]::UTF8; " + script;
                psi.FileName = shell == ActionShell.Pwsh ? "pwsh.exe" : "powershell.exe";
                psi.Arguments = "-NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass -EncodedCommand " + Convert.ToBase64String(Encoding.Unicode.GetBytes(full));
            }
            try
            {
                using (var p = Process.Start(psi))
                {
                    var err = new StringBuilder();
                    p.ErrorDataReceived += (o, e) => { if (e.Data != null) err.AppendLine(e.Data); };
                    p.BeginErrorReadLine();
                    string stdout = p.StandardOutput.ReadToEnd();
                    if (!p.WaitForExit(TimeoutSeconds * 1000)) { try { p.Kill(); } catch { } return stdout + "\n(stopped after " + TimeoutSeconds + " s)"; }
                    p.WaitForExit();
                    exitCode = p.ExitCode;
                    string e2 = err.ToString().Trim();
                    return e2.Length > 0 ? (stdout.Length > 0 ? stdout.TrimEnd() + "\n" : "") + e2 : stdout;
                }
            }
            catch (Exception ex)
            {
                exitCode = -1;
                return "Could not start " + psi.FileName + ": " + ex.Message;
            }
            finally
            {
                if (tempFile != null) { try { File.Delete(tempFile); } catch { } }
            }
        }
    }
}
