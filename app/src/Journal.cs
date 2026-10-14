using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace PSTools
{
    class JournalEntry
    {
        public string File = "", Title = "", When = "";
        public string[] Lines = new string[0];
        public int Items;
        public long BackupBytes;
    }

    class Journal
    {
        public const int MaxEntries = 10;
        public static long MaxBackupBytes = 1024L * 1024 * 1024;

        public static string Dir { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PSTools"); } }
        static string HistoryDir { get { return Path.Combine(Dir, "undo"); } }
        static string LegacyFile { get { return Path.Combine(Dir, "undo.txt"); } }
        static string DataDir { get { return Path.Combine(Dir, "undo-data"); } }

        public static bool Wrote;
        static List<string> batch;
        static string batchTitle;

        public static void BeginBatch(string title)
        {
            batch = new List<string>();
            batchTitle = title;
        }

        public static void EndBatch()
        {
            var collected = batch;
            string t = batchTitle;
            batch = null; batchTitle = null;
            if (collected == null || collected.Count == 0) return;
            WriteEntry(t, collected);
        }

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

        public void Backup(string path)
        {
            Directory.CreateDirectory(DataDir);
            string b = Path.Combine(DataDir, Guid.NewGuid().ToString("N"));
            File.Copy(path, b);
            lines.Add("B\t" + path + "\t" + b);
        }

        public void Times(string path)
        {
            bool d = Directory.Exists(path);
            DateTime c = d ? Directory.GetCreationTime(path) : File.GetCreationTime(path);
            DateTime w = d ? Directory.GetLastWriteTime(path) : File.GetLastWriteTime(path);
            lines.Add("T\t" + path + "\t" + c.Ticks + "\t" + w.Ticks);
        }

        public void Attrs(string path)
        {
            lines.Add("A\t" + path + "\t" + (int)File.GetAttributes(path));
        }

        public void Link(string path) { lines.Add("L\t" + path); }

        public void Save()
        {
            if (lines.Count == 0) return;
            if (batch != null) { batch.AddRange(lines); return; }
            WriteEntry(title, lines);
        }

        static void WriteEntry(string title, IList<string> lines)
        {
            Directory.CreateDirectory(HistoryDir);
            Migrate();
            var sb = new StringBuilder();
            sb.AppendLine("#" + title + "\t" + DateTime.Now.ToString("yyyy-MM-dd HH:mm"));
            foreach (var l in lines) sb.AppendLine(l);
            string name = DateTime.Now.ToString("yyyyMMddHHmmssfff") + "-" + Guid.NewGuid().ToString("N").Substring(0, 6) + ".txt";
            File.WriteAllText(Path.Combine(HistoryDir, name), sb.ToString(), Encoding.UTF8);
            Wrote = true;
            Prune();
        }

        static void Migrate()
        {
            try
            {
                if (!File.Exists(LegacyFile)) return;
                Directory.CreateDirectory(HistoryDir);
                string dest = Path.Combine(HistoryDir, File.GetLastWriteTime(LegacyFile).ToString("yyyyMMddHHmmssfff") + "-legacy.txt");
                File.Move(LegacyFile, dest);
            }
            catch { }
        }

        static JournalEntry Load(string file)
        {
            string[] all = File.ReadAllLines(file, Encoding.UTF8);
            var e = new JournalEntry { File = file };
            string[] head = (all.Length > 0 ? all[0].TrimStart('#') : "").Split('\t');
            e.Title = head[0];
            e.When = head.Length > 1 ? head[1] : "";
            e.Lines = all.Skip(1).ToArray();
            foreach (var l in e.Lines)
            {
                string[] p = l.Split('\t');
                if (p[0] == "M" || p[0] == "B" || p[0] == "T" || p[0] == "A" || p[0] == "L") e.Items++;
                if (p.Length == 3 && p[0] == "B")
                {
                    try { var fi = new FileInfo(p[2]); if (fi.Exists) e.BackupBytes += fi.Length; } catch { }
                }
            }
            return e;
        }

        static IEnumerable<string> EntryFiles()
        {
            Migrate();
            if (!Directory.Exists(HistoryDir)) return new string[0];
            return Directory.GetFiles(HistoryDir, "*.txt").OrderByDescending(f => Path.GetFileName(f));
        }

        public static List<JournalEntry> History()
        {
            var list = new List<JournalEntry>();
            foreach (string f in EntryFiles())
            {
                try { list.Add(Load(f)); } catch { }
            }
            return list;
        }

        static JournalEntry Latest()
        {
            foreach (string f in EntryFiles())
            {
                try { return Load(f); } catch { }
            }
            return null;
        }

        public static long HistoryBytes()
        {
            long n = 0;
            foreach (var e in History()) n += e.BackupBytes;
            return n;
        }

        static void Discard(JournalEntry e)
        {
            foreach (var l in e.Lines)
            {
                string[] p = l.Split('\t');
                if (p.Length == 3 && p[0] == "B") try { File.Delete(p[2]); } catch { }
            }
            try { File.Delete(e.File); } catch { }
        }

        static void Prune()
        {
            try
            {
                var all = History();
                long bytes = 0;
                for (int i = 0; i < all.Count; i++)
                {
                    bytes += all[i].BackupBytes;
                    if (i > 0 && (i >= MaxEntries || bytes > MaxBackupBytes)) Discard(all[i]);
                }
            }
            catch { }
        }

        public static void ClearHistory()
        {
            foreach (var e in History()) Discard(e);
            try { if (Directory.Exists(DataDir) && Directory.GetFileSystemEntries(DataDir).Length == 0) Directory.Delete(DataDir); } catch { }
        }

        public static string LastTitle()
        {
            try
            {
                var e = Latest();
                return e == null ? null : e.Title + "  -  " + e.When;
            }
            catch { return null; }
        }

        public static List<string> Steps(JournalEntry e)
        {
            var list = new List<string>();
            for (int i = e.Lines.Length - 1; i >= 0; i--)
            {
                string[] p = e.Lines[i].Split('\t');
                switch (p[0])
                {
                    case "M": if (p.Length > 2) list.Add("Move back:  " + p[2] + "  to  " + p[1]); break;
                    case "B": if (p.Length > 1) list.Add("Restore the previous version of  " + p[1]); break;
                    case "T": if (p.Length > 1) list.Add("Restore the dates of  " + p[1]); break;
                    case "A": if (p.Length > 1) list.Add("Restore the attributes of  " + p[1]); break;
                    case "L": if (p.Length > 1) list.Add("Remove the item that was created:  " + p[1]); break;
                    case "D": if (p.Length > 1) list.Add("Remove the folder that was created (only if empty):  " + p[1]); break;
                    case "R": if (p.Length > 1) list.Add("Recreate the removed folder:  " + p[1]); break;
                }
            }
            return list;
        }

        static void Apply(JournalEntry e, out int ok, out int failed)
        {
            ok = 0; failed = 0;
            for (int i = e.Lines.Length - 1; i >= 0; i--)
            {
                string[] p = e.Lines[i].Split('\t');
                try
                {
                    switch (p[0])
                    {
                        case "M":
                            string back = Path.GetDirectoryName(p[1]);
                            if (!Directory.Exists(back)) Directory.CreateDirectory(back);
                            if (Directory.Exists(p[2])) Directory.Move(p[2], p[1]); else File.Move(p[2], p[1]);
                            ok++;
                            break;
                        case "D":
                            if (Fs.IsEmptyDir(p[1])) Directory.Delete(p[1]);
                            break;
                        case "R":
                            Directory.CreateDirectory(p[1]);
                            break;
                        case "B":
                            File.Copy(p[2], p[1], true);
                            ok++;
                            break;
                        case "T":
                            DateTime c = new DateTime(long.Parse(p[2])), w = new DateTime(long.Parse(p[3]));
                            if (Directory.Exists(p[1])) { Directory.SetCreationTime(p[1], c); Directory.SetLastWriteTime(p[1], w); }
                            else { File.SetCreationTime(p[1], c); File.SetLastWriteTime(p[1], w); }
                            ok++;
                            break;
                        case "A":
                            File.SetAttributes(p[1], (FileAttributes)int.Parse(p[2]));
                            ok++;
                            break;
                        case "L":
                            if (Directory.Exists(p[1])) Directory.Delete(p[1], false); else File.Delete(p[1]);
                            ok++;
                            break;
                    }
                }
                catch { failed++; }
            }
            Discard(e);
        }

        static string Describe(int ok, int failed)
        {
            return ok + " items restored" + (failed > 0 ? ", " + failed + " failed (file changed or missing)." : ".");
        }

        public static string Undo()
        {
            var e = Latest();
            if (e == null) return "Nothing to undo.";
            int ok, failed;
            Apply(e, out ok, out failed);
            return "Undo finished: " + Describe(ok, failed);
        }

        public static string UndoThrough(JournalEntry target)
        {
            int ok = 0, failed = 0, n = 0;
            foreach (var e in History())
            {
                int o, f;
                Apply(e, out o, out f);
                ok += o; failed += f; n++;
                if (e.File == target.File) break;
            }
            if (n == 0) return "Nothing to undo.";
            return "Undid " + n + (n == 1 ? " operation: " : " operations: ") + Describe(ok, failed);
        }
    }
}
