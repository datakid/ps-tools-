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
                Action a = Actions.Find(args[0]);
                if (a == null) { Msg("Unknown action: " + args[0]); return 1; }
                string[] paths = a.Multi ? Collector.Gather(a.Id, path) : new[] { path };
                if (paths == null) return 0;
                Runner.Execute(a, paths, null);
                return 0;
            }

            if (Quiet) { Msg("Nothing to do. Use an action id and a path, or --install, --uninstall, --status, --repair."); return 1; }

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
            if (Quiet) { Console.WriteLine(text); return; }
            FDialog.Show(text, "PS Tools", FDialog.Kind.Info, false);
        }

        public static void Fail(string text)
        {
            if (Quiet) { Console.WriteLine(text); return; }
            FDialog.Show(text, "PS Tools - error", FDialog.Kind.Error, false);
        }

        public static bool Ask(string text)
        {
            if (Quiet) { Console.WriteLine(text + "\n(confirmation needed, so nothing was run in --quiet mode)"); return false; }
            return FDialog.Show(text, "PS Tools", FDialog.Kind.Question, true);
        }

        public static string ExeDir { get { return Path.GetDirectoryName(Application.ExecutablePath); } }
    }
}
