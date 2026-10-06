// Per-user Explorer context menu (HKCU only, no admin).
// Keys written (and the only ones removed on uninstall):
//   HKCU\Software\Classes\Directory\shell\PSTools
//   HKCU\Software\Classes\Directory\Background\shell\PSTools
//   HKCU\Software\Classes\Drive\shell\PSTools
using System;
using System.IO;
using System.Linq;
using Microsoft.Win32;

namespace PSTools
{
    static class Shell
    {
        static readonly string[] Roots = { @"Software\Classes\Directory\shell\PSTools", @"Software\Classes\Directory\Background\shell\PSTools", @"Software\Classes\Drive\shell\PSTools" };

        public static bool IsInstalled()
        {
            using (var k = Registry.CurrentUser.OpenSubKey(Roots[0])) return k != null;
        }

        public static void Install()
        {
            string exe = System.Windows.Forms.Application.ExecutablePath;
            foreach (var root in Roots)
            {
                string arg = root.Contains("Background") ? "%V" : "%1";
                Registry.CurrentUser.DeleteSubKeyTree(root, false);
                using (var k = Registry.CurrentUser.CreateSubKey(root))
                {
                    k.SetValue("MUIVerb", "PS Tools");
                    k.SetValue("Icon", "\"" + exe + "\",0");
                    k.SetValue("SubCommands", "");   // cascading submenu
                }
                int i = 0;
                // "Open in PS Tools" first, then every action flagged InMenu, separator between groups.
                Add(root, i++, "Open in PS Tools", "open", exe, arg, false);
                string lastGroup = null;
                foreach (var a in Actions.All.Where(x => x.InMenu))
                {
                    Add(root, i++, a.Label, a.Id, exe, arg, a.Group != lastGroup);
                    lastGroup = a.Group;
                }
            }
        }

        static void Add(string root, int index, string label, string action, string exe, string arg, bool separatorBefore)
        {
            // Keys are sorted alphabetically by Explorer, so prefix with an index.
            using (var k = Registry.CurrentUser.CreateSubKey(root + @"\shell\" + index.ToString("D2") + action))
            {
                k.SetValue("MUIVerb", label);
                if (separatorBefore) k.SetValue("CommandFlags", 0x20, RegistryValueKind.DWord);
                using (var c = k.CreateSubKey("command"))
                    c.SetValue("", "\"" + exe + "\" " + action + " \"" + arg + "\"");
            }
        }

        public static void Uninstall()
        {
            foreach (var root in Roots) Registry.CurrentUser.DeleteSubKeyTree(root, false);
            try { if (Directory.Exists(Journal.Dir)) Directory.Delete(Journal.Dir, true); } catch { }
        }
    }
}
