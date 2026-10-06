// PS Tools - ultra-lean file management app for Windows 10/11.
// Built with the C# 5 compiler that ships with .NET Framework 4.x (already part of Windows).
// Usage:
//   PSTools.exe                         open the GUI
//   PSTools.exe <action> "<folder>"     run one action (used by the context menu)
//   PSTools.exe --install | --uninstall add/remove the per-user context menu (HKCU only)
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

            if (args.Length >= 1 && args[0] == "--install") { Shell.Install(); Msg("Context menu installed.\n\nWindows 11: right-click > Show more options."); return 0; }
            if (args.Length >= 1 && args[0] == "--uninstall") { Shell.Uninstall(); Msg("Context menu removed. You can now delete PSTools.exe."); return 0; }

            if (args.Length >= 2)
            {
                string path = Clean(args[1]);
                if (args[0] == "open") { Application.Run(new MainForm(path)); return 0; }
                Action a = Actions.Find(args[0]);
                if (a == null) { Msg("Unknown action: " + args[0]); return 1; }
                Runner.RunWithUI(a, path, null);
                return 0;
            }

            Application.Run(new MainForm(args.Length == 1 ? Clean(args[0]) : null));
            return 0;
        }

        // Explorer can pass "C:\" as C:" or with trailing quotes/backslashes.
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
