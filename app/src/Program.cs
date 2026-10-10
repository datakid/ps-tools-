using System;
using System.IO;
using System.Windows.Forms;

namespace PSTools
{
    static class Program
    {
        [STAThread]
        static int Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            UI.Init();

            if (args.Length >= 1 && args[0] == "--install") { Shell.Install(); Msg("Explorer menu installed.\n\nWindows 11: right-click > Show more options (or turn on the classic menu in the System module)."); return 0; }
            if (args.Length >= 1 && args[0] == "--uninstall") { Msg(Shell.UninstallAll()); return 0; }
            if (args.Length >= 1 && args[0] == "--status") { Msg(Shell.Status()); return 0; }

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
            MessageBox.Show(text, "PS Tools", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        public static bool Ask(string text)
        {
            return MessageBox.Show(text, "PS Tools", MessageBoxButtons.OKCancel, MessageBoxIcon.Question) == DialogResult.OK;
        }

        public static string ExeDir { get { return Path.GetDirectoryName(Application.ExecutablePath); } }
    }
}
