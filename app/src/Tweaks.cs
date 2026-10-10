using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32;

namespace PSTools
{
    static class Tweaks
    {
        [DllImport("shell32.dll")]
        static extern void SHChangeNotify(int evt, int flags, IntPtr a, IntPtr b);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        static extern IntPtr SendMessageTimeout(IntPtr h, int msg, IntPtr w, string l, int flags, int timeout, out IntPtr result);

        const string Adv = @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced";
        const string ClassicKey = @"Software\Classes\CLSID\{86ca1aa0-34aa-4e8b-a509-50c905bae2a2}";

        static string FilePath { get { return Path.Combine(Journal.Dir, "changes.txt"); } }

        static Dictionary<string, string> Read()
        {
            var d = new Dictionary<string, string>();
            try
            {
                if (File.Exists(FilePath))
                    foreach (var l in File.ReadAllLines(FilePath, Encoding.UTF8))
                    {
                        int t = l.IndexOf('\t');
                        if (t > 0) d[l.Substring(0, t)] = l.Substring(t + 1);
                    }
            }
            catch { }
            return d;
        }

        static void Write(Dictionary<string, string> d)
        {
            if (d.Count == 0) { try { File.Delete(FilePath); } catch { } return; }
            Directory.CreateDirectory(Journal.Dir);
            File.WriteAllLines(FilePath, d.Select(kv => kv.Key + "\t" + kv.Value).ToArray(), Encoding.UTF8);
        }

        static void Remember(string id, string previous)
        {
            var d = Read();
            if (!d.ContainsKey(id)) { d[id] = previous; Write(d); }
        }

        public static string Pending()
        {
            var d = Read();
            return d.Count == 0 ? "" : string.Join(", ", d.Keys.Select(Name));
        }

        static string Name(string id)
        {
            switch (id)
            {
                case "hidden": return "show hidden files";
                case "ext": return "show file extensions";
                case "classic": return "classic right-click menu";
            }
            return id;
        }

        static int GetAdv(string name, int def)
        {
            using (var k = Registry.CurrentUser.OpenSubKey(Adv))
            {
                object v = k == null ? null : k.GetValue(name);
                return v is int ? (int)v : def;
            }
        }

        static void SetAdv(string name, int value)
        {
            using (var k = Registry.CurrentUser.CreateSubKey(Adv)) k.SetValue(name, value, RegistryValueKind.DWord);
        }

        public static bool HiddenShown { get { return GetAdv("Hidden", 2) == 1; } }
        public static bool ExtShown { get { return GetAdv("HideFileExt", 1) == 0; } }
        public static bool Classic { get { using (var k = Registry.CurrentUser.OpenSubKey(ClassicKey + @"\InprocServer32")) return k != null; } }

        public static string ToggleHidden()
        {
            Remember("hidden", GetAdv("Hidden", 2).ToString());
            SetAdv("Hidden", HiddenShown ? 2 : 1);
            Refresh();
            return HiddenShown ? "Hidden files are now shown. Press F5 in open Explorer windows." : "Hidden files are now hidden. Press F5 in open Explorer windows.";
        }

        public static string ToggleExt()
        {
            Remember("ext", GetAdv("HideFileExt", 1).ToString());
            SetAdv("HideFileExt", ExtShown ? 1 : 0);
            Refresh();
            return ExtShown ? "File extensions are now shown. Press F5 in open Explorer windows." : "File extensions are now hidden. Press F5 in open Explorer windows.";
        }

        public static string ToggleClassic()
        {
            if (Classic)
            {
                Registry.CurrentUser.DeleteSubKeyTree(ClassicKey, false);
                return "Windows 11 menu restored. Use Restart Explorer to apply.";
            }
            Remember("classic", "absent");
            using (var k = Registry.CurrentUser.CreateSubKey(ClassicKey + @"\InprocServer32")) k.SetValue("", "");
            return "Classic menu on: every right-click shows the full menu. Use Restart Explorer to apply.";
        }

        static void Refresh()
        {
            try
            {
                IntPtr r;
                SendMessageTimeout((IntPtr)0xFFFF, 0x1A, IntPtr.Zero, "ShellState", 2, 1000, out r);
                SHChangeNotify(0x08000000, 0, IntPtr.Zero, IntPtr.Zero);
            }
            catch { }
        }

        public static void RevertAll()
        {
            var d = Read();
            foreach (var kv in d)
            {
                try
                {
                    if (kv.Key == "hidden") SetAdv("Hidden", int.Parse(kv.Value));
                    else if (kv.Key == "ext") SetAdv("HideFileExt", int.Parse(kv.Value));
                    else if (kv.Key == "classic") Registry.CurrentUser.DeleteSubKeyTree(ClassicKey, false);
                }
                catch { }
            }
            if (d.Count > 0) Refresh();
            try { File.Delete(FilePath); } catch { }
        }
    }
}
