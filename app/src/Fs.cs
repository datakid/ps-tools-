// File-system helpers: safe recursive walking, free names, recycle bin, unblock, undo journal.
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace PSTools
{
    static class Fs
    {
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern bool DeleteFile(string path);

        // Recursive file enumeration that skips folders it cannot open and does not follow
        // junctions/symlinks (avoids loops such as "Application Data").
        public static IEnumerable<string> Files(string root, bool recurse)
        {
            var stack = new Stack<string>();
            stack.Push(root);
            while (stack.Count > 0)
            {
                string dir = stack.Pop();
                string[] files = null, subs = null;
                try { files = Directory.GetFiles(dir); } catch { }
                if (files != null) foreach (var f in files) yield return f;
                if (!recurse) continue;
                try { subs = Directory.GetDirectories(dir); } catch { }
                if (subs == null) continue;
                foreach (var s in subs)
                    if (!IsLink(s)) stack.Push(s);
            }
        }

        // All subdirectories (not the root), deepest first.
        public static List<string> DirsDeepestFirst(string root)
        {
            var list = new List<string>();
            var stack = new Stack<string>();
            stack.Push(root);
            while (stack.Count > 0)
            {
                string[] subs = null;
                try { subs = Directory.GetDirectories(stack.Pop()); } catch { }
                if (subs == null) continue;
                foreach (var s in subs) if (!IsLink(s)) { list.Add(s); stack.Push(s); }
            }
            list.Sort((a, b) => b.Length.CompareTo(a.Length));
            return list;
        }

        public static bool IsLink(string path)
        {
            try { return (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0; }
            catch { return true; }
        }

        public static bool IsEmptyDir(string dir)
        {
            try
            {
                using (var e = Directory.EnumerateFileSystemEntries(dir).GetEnumerator()) return !e.MoveNext();
            }
            catch { return false; }
        }

        // name.ext -> name_1.ext, name_2.ext ... until it does not exist in dir.
        public static string FreeName(string dir, string fileName)
        {
            string target = Path.Combine(dir, fileName);
            if (!File.Exists(target) && !Directory.Exists(target)) return target;
            string bas = Path.GetFileNameWithoutExtension(fileName), ext = Path.GetExtension(fileName);
            for (int i = 1; ; i++)
            {
                target = Path.Combine(dir, bas + "_" + i + ext);
                if (!File.Exists(target) && !Directory.Exists(target)) return target;
            }
        }

        public static void Recycle(string path)
        {
            if (Directory.Exists(path))
                Microsoft.VisualBasic.FileIO.FileSystem.DeleteDirectory(path,
                    Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs, Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin);
            else
                Microsoft.VisualBasic.FileIO.FileSystem.DeleteFile(path,
                    Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs, Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin);
        }

        // Removes the "downloaded from the internet" mark (Zone.Identifier stream).
        public static bool Unblock(string path)
        {
            return DeleteFile(path + ":Zone.Identifier");
        }

        public static string Size(long bytes)
        {
            string[] u = { "B", "KB", "MB", "GB", "TB" };
            double v = bytes; int i = 0;
            while (v >= 1024 && i < u.Length - 1) { v /= 1024; i++; }
            return (i == 0 ? v.ToString("0") : v.ToString("0.0")) + " " + u[i];
        }

        public static bool IsInside(string child, string parent)
        {
            string c = Path.GetFullPath(child).TrimEnd('\\') + "\\";
            string p = Path.GetFullPath(parent).TrimEnd('\\') + "\\";
            return c.StartsWith(p, StringComparison.OrdinalIgnoreCase);
        }
    }

    // Records moves/renames/created folders so the last operation can be undone.
    // Stored in %LOCALAPPDATA%\PSTools\undo.txt (removed by --uninstall).
    class Journal
    {
        public static string Dir { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PSTools"); } }
        static string FilePath { get { return Path.Combine(Dir, "undo.txt"); } }

        readonly List<string> lines = new List<string>();
        readonly string title;
        public Journal(string title) { this.title = title; }

        public void Move(string from, string to)
        {
            if (Directory.Exists(from)) Directory.Move(from, to); else File.Move(from, to);
            lines.Add("M\t" + from + "\t" + to);
        }

        public void CreatedDir(string dir) { lines.Add("D\t" + dir); }

        public void EnsureDir(string dir)
        {
            if (Directory.Exists(dir)) return;
            Directory.CreateDirectory(dir);
            CreatedDir(dir);
        }

        public void RemovedDir(string dir) { lines.Add("R\t" + dir); }

        public void Save()
        {
            if (lines.Count == 0) return;
            Directory.CreateDirectory(Dir);
            var sb = new StringBuilder();
            sb.AppendLine("#" + title + "\t" + DateTime.Now.ToString("yyyy-MM-dd HH:mm"));
            foreach (var l in lines) sb.AppendLine(l);
            File.WriteAllText(FilePath, sb.ToString(), Encoding.UTF8);
        }

        public static string LastTitle()
        {
            try
            {
                if (!File.Exists(FilePath)) return null;
                string first = File.ReadAllLines(FilePath)[0];
                return first.TrimStart('#').Replace("\t", "  -  ");
            }
            catch { return null; }
        }

        // Replays the journal backwards. Returns a summary.
        public static string Undo()
        {
            if (!File.Exists(FilePath)) return "Nothing to undo.";
            string[] all = File.ReadAllLines(FilePath, Encoding.UTF8);
            int ok = 0, failed = 0;
            for (int i = all.Length - 1; i >= 1; i--)
            {
                string[] p = all[i].Split('\t');
                try
                {
                    if (p[0] == "M")
                    {
                        string back = Path.GetDirectoryName(p[1]);
                        if (!Directory.Exists(back)) Directory.CreateDirectory(back);
                        if (Directory.Exists(p[2])) Directory.Move(p[2], p[1]); else File.Move(p[2], p[1]);
                        ok++;
                    }
                    else if (p[0] == "D") { if (Fs.IsEmptyDir(p[1])) Directory.Delete(p[1]); }
                    else if (p[0] == "R") { Directory.CreateDirectory(p[1]); }
                }
                catch { failed++; }
            }
            File.Delete(FilePath);
            return "Undo finished: " + ok + " items restored" + (failed > 0 ? ", " + failed + " failed (file changed or missing)." : ".");
        }
    }
}
