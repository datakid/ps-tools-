using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.Win32;

namespace PSTools
{
    static class Shell
    {
        const string Base = @"Software\Classes\";

        static readonly string[] Roots =
        {
            Base + @"Directory\shell\PSTools",
            Base + @"Directory\Background\shell\PSTools",
            Base + @"Drive\shell\PSTools",
            Base + @"*\shell\PSTools"
        };

        static readonly Target[] Kinds = { Target.Folder, Target.Background, Target.Drive, Target.File };

        public static bool IsInstalled()
        {
            foreach (var r in Roots)
                using (var k = Registry.CurrentUser.OpenSubKey(r)) if (k != null) return true;
            return false;
        }

        public static List<Action> MenuActions(Target t)
        {
            Settings.EnsureLoaded();
            return Actions.All.Where(a => (a.Targets & t) != 0 && a.Usable && Settings.IsEnabled(a.Id)).ToList();
        }

        public static void Install()
        {
            RemoveMenu();
            string exe = System.Windows.Forms.Application.ExecutablePath;
            for (int i = 0; i < Roots.Length; i++)
            {
                var t = Kinds[i];
                var acts = MenuActions(t);
                bool dirs = t != Target.File;
                if (acts.Count == 0 && !dirs) continue;
                string root = Roots[i];
                string arg = t == Target.Background ? "%V" : "%1";
                using (var k = Registry.CurrentUser.CreateSubKey(root))
                {
                    k.SetValue("MUIVerb", "PS Tools");
                    k.SetValue("Icon", "\"" + exe + "\",0");
                    k.SetValue("SubCommands", "");
                }
                string shell = root + @"\shell";
                int n = 0;
                if (dirs) Add(shell, n++, "Open in PS Tools", "open", exe, arg);
                var favs = acts.Where(a => Settings.IsFav(a.Id)).ToList();
                foreach (var a in favs) Add(shell, n++, a.Label, a.Id, exe, arg);
                bool first = true;
                foreach (var g in acts.Where(a => !Settings.IsFav(a.Id)).GroupBy(a => a.Group).OrderBy(g => Modules.Order(g.Key)))
                {
                    string mk = shell + @"\" + (n++).ToString("D2") + "m";
                    using (var k = Registry.CurrentUser.CreateSubKey(mk))
                    {
                        k.SetValue("MUIVerb", g.Key);
                        k.SetValue("SubCommands", "");
                        if (first && (dirs || favs.Count > 0)) k.SetValue("CommandFlags", 0x20, RegistryValueKind.DWord);
                    }
                    first = false;
                    int j = 0;
                    foreach (var a in g) Add(mk + @"\shell", j++, a.Label, a.Id, exe, arg);
                }
            }
        }

        static void Add(string parent, int index, string label, string action, string exe, string arg)
        {
            using (var k = Registry.CurrentUser.CreateSubKey(parent + @"\" + index.ToString("D2") + action))
            {
                k.SetValue("MUIVerb", label);
                using (var c = k.CreateSubKey("command"))
                    c.SetValue("", "\"" + exe + "\" " + action + " \"" + arg + "\"");
            }
        }

        public static void RemoveMenu()
        {
            foreach (var r in Roots) Registry.CurrentUser.DeleteSubKeyTree(r, false);
        }

        public static string UninstallAll()
        {
            var sb = new StringBuilder();
            RemoveMenu();
            sb.AppendLine("Explorer menu removed.");
            string pending = Tweaks.Pending();
            Tweaks.RevertAll();
            if (pending.Length > 0) sb.AppendLine("Windows settings restored: " + pending + ".");
            try { if (Directory.Exists(Journal.Dir)) { Directory.Delete(Journal.Dir, true); sb.AppendLine("Settings, undo data and temporary files deleted."); } } catch (Exception ex) { sb.AppendLine("Could not delete " + Journal.Dir + ": " + ex.Message); }
            sb.AppendLine();
            sb.Append("You can now delete PSTools.exe and commands.json.");
            return sb.ToString();
        }

        public static string Status()
        {
            var sb = new StringBuilder();
            sb.AppendLine("Explorer menu keys (HKCU):");
            bool any = false;
            foreach (var r in Roots)
                using (var k = Registry.CurrentUser.OpenSubKey(r)) if (k != null) { sb.AppendLine("  " + r); any = true; }
            if (!any) sb.AppendLine("  none");
            sb.AppendLine();
            sb.AppendLine("Data folder: " + Journal.Dir + (Directory.Exists(Journal.Dir) ? "" : "  (not created)"));
            string pending = Tweaks.Pending();
            sb.AppendLine("Windows settings changed by PS Tools: " + (pending.Length == 0 ? "none" : pending));
            sb.AppendLine("Executable: " + System.Windows.Forms.Application.ExecutablePath);
            return sb.ToString();
        }
    }
}
