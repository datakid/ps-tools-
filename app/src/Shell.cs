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

        public static string RegisteredExe()
        {
            foreach (var r in Roots)
                using (var k = Registry.CurrentUser.OpenSubKey(r))
                {
                    if (k == null) continue;
                    string icon = k.GetValue("Icon") as string;
                    if (string.IsNullOrEmpty(icon)) continue;
                    icon = icon.Trim();
                    if (icon.StartsWith("\"")) { int end = icon.IndexOf('"', 1); return end > 1 ? icon.Substring(1, end - 1) : null; }
                    int comma = icon.LastIndexOf(',');
                    return comma > 0 ? icon.Substring(0, comma) : icon;
                }
            return null;
        }

        public static bool PointsElsewhere()
        {
            string reg = RegisteredExe();
            return reg != null && !string.Equals(reg, System.Windows.Forms.Application.ExecutablePath, StringComparison.OrdinalIgnoreCase);
        }

        public static bool HealIfMoved()
        {
            if (!IsInstalled() || !PointsElsewhere()) return false;
            try { Install(); return true; }
            catch { return false; }
        }

        public static void Install()
        {
            try { Write(); }
            catch { RemoveMenu(); throw; }
        }

        static void Write()
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
                if (dirs) Add(shell, n++, "Open in PS Tools", "open", exe, arg, null, false);
                var favs = acts.Where(a => Settings.IsFav(a.Id)).ToList();
                foreach (var a in favs) Add(shell, n++, a.Label, a.Id, exe, arg, a.AppliesTo, a.Extended);
                bool first = true;
                foreach (var g in acts.Where(a => !Settings.IsFav(a.Id)).GroupBy(a => a.Group).OrderBy(g => Settings.IsGroupPinned(g.Key) ? 0 : 1).ThenBy(g => Modules.Order(g.Key)))
                {
                    var items = g.ToList();
                    string mk = shell + @"\" + (n++).ToString("D2") + "m";
                    using (var k = Registry.CurrentUser.CreateSubKey(mk))
                    {
                        k.SetValue("MUIVerb", g.Key);
                        k.SetValue("SubCommands", "");
                        if (first && (dirs || favs.Count > 0)) k.SetValue("CommandFlags", 0x20, RegistryValueKind.DWord);
                        string common = items[0].AppliesTo;
                        if (!string.IsNullOrEmpty(common) && items.All(a => a.AppliesTo == common)) k.SetValue("AppliesTo", common);
                        if (items.All(a => a.Extended)) k.SetValue("Extended", "");
                    }
                    first = false;
                    int j = 0;
                    foreach (var a in items) Add(mk + @"\shell", j++, a.Label, a.Id, exe, arg, a.AppliesTo, a.Extended);
                }
            }
        }

        static void Add(string parent, int index, string label, string action, string exe, string arg, string appliesTo, bool extended)
        {
            using (var k = Registry.CurrentUser.CreateSubKey(parent + @"\" + index.ToString("D2") + action))
            {
                k.SetValue("MUIVerb", label);
                if (!string.IsNullOrEmpty(appliesTo)) k.SetValue("AppliesTo", appliesTo);
                if (extended) k.SetValue("Extended", "");
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

        static long FolderBytes(string dir)
        {
            long sum = 0;
            try { foreach (var f in Directory.GetFiles(dir, "*", SearchOption.AllDirectories)) try { sum += new FileInfo(f).Length; } catch { } }
            catch { }
            return sum;
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
            string reg = RegisteredExe();
            if (reg != null) sb.AppendLine("Menu points to: " + reg + (PointsElsewhere() ? "  (differs from this exe: run PSTools.exe --repair)" : "  (this exe)"));
            sb.AppendLine("Data folder: " + Journal.Dir + (Directory.Exists(Journal.Dir) ? "  (" + Fs.Size(FolderBytes(Journal.Dir)) + ")" : "  (not created)"));
            if (Settings.Configured) sb.AppendLine("Menu selection: " + Settings.CurrentPreset);
            string pending = Tweaks.Pending();
            sb.AppendLine("Windows settings changed by PS Tools: " + (pending.Length == 0 ? "none" : pending));
            sb.AppendLine("Executable: " + System.Windows.Forms.Application.ExecutablePath);
            return sb.ToString();
        }
    }
}
