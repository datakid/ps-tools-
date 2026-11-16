using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace PSTools
{
    static class Program
    {
        [DllImport("kernel32.dll")]
        static extern bool AttachConsole(int processId);

        public static bool Quiet;
        public static string StartupNote;

        [STAThread]
        static int Main(string[] argv)
        {
            var args = argv.Where(a => a != "--quiet").ToArray();
            if (args.Length >= 1 && (args[0] == "--selftest" || args[0] == "--dump-snippets"))
            {
                Quiet = true;
                try { AttachConsole(-1); } catch { }
                return args[0] == "--selftest" ? SelfTest.Run() : SelfTest.DumpSnippets();
            }
            string dataDir = TestTools.Option(args, "--data-dir");
            if (dataDir != null) Journal.DirOverride = dataDir;
            MenuCli.ApplyGlobalOptions(args);
            if (args.Length >= 1 && args[0] == "--dump-menu")
            {
                Quiet = true;
                try { AttachConsole(-1); } catch { }
                return MenuCli.DumpMenu(args);
            }
            if (args.Length >= 1 && args[0] == "--dump-tree")
            {
                Quiet = true;
                try { AttachConsole(-1); } catch { }
                return MenuCli.DumpTree(args);
            }
            if (args.Length >= 1 && args[0] == "--list-actions")
            {
                Quiet = true;
                try { AttachConsole(-1); } catch { }
                return TestTools.ListActions(args);
            }
            if (args.Length >= 1 && args[0] == "--risk-classify-json")
            {
                Quiet = true;
                try { AttachConsole(-1); } catch { }
                return TestTools.RiskClassifyJson(args);
            }
            if (args.Length >= 1 && args[0] == "--test-actions")
            {
                Quiet = true;
                try { AttachConsole(-1); } catch { }
                return TestTools.RunActionTests(args);
            }
            if (args.Length >= 1 && args[0] == "--screenshot")
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                return UiHarness.Run(args);
            }
            if (args.Length != argv.Length)
            {
                Quiet = true;
                try { AttachConsole(-1); } catch { }
            }
            if (!Quiet)
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                UI.Init();
            }

            if (args.Length >= 1 && args[0] == "--install") { Settings.MarkConfigured(); Shell.Install(); Msg("Explorer menu installed.\n\nWindows 11: right-click > Show more options (or turn on the classic menu in the System module)."); return 0; }
            if (args.Length >= 1 && args[0] == "--uninstall") { Msg(Shell.UninstallAll()); return 0; }
            if (args.Length >= 1 && args[0] == "--status") { Msg(Shell.Status()); return 0; }
            if (args.Length >= 1 && args[0] == "cheatsheet")
            {
                string startDir = args.Length >= 2 && !args[1].StartsWith("--") ? Clean(args[1]) : Environment.CurrentDirectory;
                var sheetForm = new CheatSheetForm(startDir, null);
                string startQuery = TestTools.Option(args, "--query");
                if (startQuery != null) sheetForm.SetQuery(startQuery);
                Application.Run(sheetForm);
                return 0;
            }
            if (args.Length >= 1 && args[0] == "--repair")
            {
                if (!Shell.IsInstalled()) { Msg("The Explorer menu is not installed."); return 1; }
                Shell.Install();
                Msg("Explorer menu re-created for " + Application.ExecutablePath);
                return 0;
            }

            if (args.Length >= 2)
            {
                string path = Clean(args[1]);
                if (args[0] == "open") { Application.Run(new MainForm(path)); return 0; }
                if (args[0] == "menu" || args[0] == "menu-bg") return PowerMenuLauncher.Run(args[0] == "menu-bg", path);
                if (args[0] == "run")
                {
                    string shellOpt = TestTools.Option(args, "--shell");
                    if (shellOpt != null) RunHistory.LastShell = shellOpt;
                    Application.Run(new RunForm(path, TestTools.Option(args, "--command") ?? "", "background"));
                    return 0;
                }
                var spec = ActionCatalog.Current.Find(args[0]);
                if (spec == null) { Msg("Unknown action: " + args[0]); return 1; }
                Action a = spec.Legacy;
                if (a == null)
                {
                    string[] scriptPaths = spec.Multi == MultiMode.Single ? new[] { path } : Collector.Gather(spec.Id, path);
                    if (scriptPaths == null) return 0;
                    Executor.Run(spec, scriptPaths);
                    return 0;
                }
                string[] paths = a.Multi ? Collector.Gather(a.Id, path) : new[] { path };
                if (paths == null) return 0;
                Runner.Execute(a, paths, null);
                return 0;
            }

            if (Quiet) { Msg("Nothing to do. Use an action id and a path, or --install, --uninstall, --status, --repair, --selftest, --dump-snippets."); return 1; }

            if (!Settings.Configured) new FirstRunForm().ShowDialog();
            else if (Shell.HealIfMoved()) StartupNote = "The Explorer menu pointed at an old location and was re-created for this one.";

            Application.Run(new MainForm(args.Length == 1 ? Clean(args[0]) : null));
            return 0;
        }

        public static string Clean(string p)
        {
            p = p.Trim().Trim('"');
            if (p.Length > 3) p = p.TrimEnd('\\');
            if (p.Length == 2 && p[1] == ':') p += "\\";
            return p;
        }

        public static void Msg(string text)
        {
            if (TestSink.Active) { TestSink.Dialogs.Add(text); return; }
            if (Quiet) { Console.WriteLine(text); return; }
            FDialog.Show(text, "PS Tools", FDialog.Kind.Info, false);
        }

        public static void Fail(string text)
        {
            if (TestSink.Active) { TestSink.Dialogs.Add("ERROR: " + text); return; }
            if (Quiet) { Console.WriteLine(text); return; }
            FDialog.Show(text, "PS Tools - error", FDialog.Kind.Error, false);
        }

        public static bool Ask(string text)
        {
            if (TestSink.Active) { TestSink.Dialogs.Add("ASK: " + text); return true; }
            if (Quiet) { Console.WriteLine(text + "\n(confirmation needed, so nothing was run in --quiet mode)"); return false; }
            return FDialog.Show(text, "PS Tools", FDialog.Kind.Question, true);
        }

        public static string ExeDir { get { return Path.GetDirectoryName(Application.ExecutablePath); } }
    }
}
