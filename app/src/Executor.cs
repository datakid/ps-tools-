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
            return ContextFor(paths, null);
        }

        public static PlaceholderContext ContextFor(string[] paths, Dictionary<string, string> inputs)
        {
            var c = new PlaceholderContext { Paths = paths, Clip = Clip.Text(), GitRoot = FindGitRoot };
            if (inputs != null) c.Inputs = inputs;
            return c;
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
            Run(s, paths, null);
        }

        public static void Run(ActionSpec s, string[] paths, Dictionary<string, string> inputs)
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
            try { firstScript = Placeholders.Expand(s.Command, s.Shell, ContextFor(batches[0], inputs)); }
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
                try { script = Placeholders.Expand(s.Command, s.Shell, ContextFor(batch, inputs)); }
                catch (PlaceholderException ex) { Program.Fail(ex.Message); return; }
                int code;
                string text = Execute(s.Shell, script, WorkDir(batch), s.Output == ActionOutput.Grid && s.Shell != ActionShell.Cmd, out code);
                if (output.Length > 0) output.AppendLine();
                output.Append(text.TrimEnd());
                if (code != 0) output.Append("\n(exit code " + code + ")");
            }
            if (s.Output == ActionOutput.Grid && s.Shell != ActionShell.Cmd)
            {
                string plain;
                var table = RunEngine.ParseTable(output.ToString(), out plain);
                if (table != null) { ShowForm(new ResultsForm(s.Title, table)); return; }
                Program.Msg(plain ?? "");
                return;
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

        public static void ShowForm(System.Windows.Forms.Form f)
        {
            if (System.Windows.Forms.Application.OpenForms.Count > 0) f.Show();
            else System.Windows.Forms.Application.Run(f);
        }

        public static string RunText(string script, string dir)
        {
            int code;
            string text = RunEngine.RunSync(new RunRequest { Shell = ActionShell.PowerShell, Script = script, Dir = dir }, out code, TimeoutSeconds);
            return code == 0 ? text.TrimEnd() : text.TrimEnd() + "\n(exit code " + code + ")";
        }

        static string Execute(ActionShell shell, string script, string dir, bool asTable, out int exitCode)
        {
            return RunEngine.RunSync(new RunRequest { Shell = shell, Script = script, Dir = dir, AsTable = asTable }, out exitCode, TimeoutSeconds);
        }
    }
}
