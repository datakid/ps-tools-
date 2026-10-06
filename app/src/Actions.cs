// All built-in actions. Each action is small, cancellable and reports progress.
// Moves/renames are journaled so "Undo last" can revert them.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace PSTools
{
    // What an action returns: a summary line and optionally a table for the results window.
    class Result
    {
        public string Summary;
        public string[] Columns;
        public List<string[]> Rows;
        public int PathColumn = -1;     // column holding a path (enables Open / Recycle in results)
        public bool Checkable;          // results window shows checkboxes + "Recycle checked"
    }

    class Ctx
    {
        public volatile bool Cancel;
        public Action<string> Report = delegate { };
        public void Check() { if (Cancel) throw new OperationCanceledException(); }
    }

    class Action
    {
        public string Id, Label, Group, Hint;
        public string Confirm;          // null = no confirmation (read-only action)
        public bool InMenu = true;      // show in the Explorer context menu
        public Func<string, Ctx, Result> Run;
    }

    static class Actions
    {
        public static readonly List<Action> All = new List<Action>
        {
            // ---------- Organize ----------
            new Action { Id = "flatten-copy", Group = "Organize", Label = "Flatten tree (copy)",
                Hint = "Copies every file from all subfolders into a new sibling folder \"<name>-flat\". Duplicate names become name_1.ext.",
                Confirm = "Copy every file under\n{0}\ninto\n{0}-flat ?", Run = FlattenCopy },
            new Action { Id = "flatten-move", Group = "Organize", Label = "Flatten tree (move here)",
                Hint = "Moves every file from subfolders up into this folder, then removes the emptied subfolders. Undoable.",
                Confirm = "Move all files from the subfolders of\n{0}\ninto the folder itself and remove the empty subfolders?", Run = FlattenMove },
            new Action { Id = "organize-ext", Group = "Organize", Label = "Sort files by extension",
                Hint = "Moves files in this folder into JPG, PDF, ... subfolders. Undoable.",
                Confirm = "Sort the files in\n{0}\ninto folders by extension?", Run = (p, c) => SortInto(p, c, "Sort by extension", f => { var e = Path.GetExtension(f).TrimStart('.').ToUpperInvariant(); return e.Length == 0 ? "NOEXT" : e; }) },
            new Action { Id = "organize-month", Group = "Organize", Label = "Sort files by month",
                Hint = "Moves files in this folder into yyyy-MM subfolders by modified date. Undoable.",
                Confirm = "Sort the files in\n{0}\ninto yyyy-MM folders by modified date?", Run = (p, c) => SortInto(p, c, "Sort by month", f => File.GetLastWriteTime(f).ToString("yyyy-MM")) },
            new Action { Id = "organize-type", Group = "Organize", Label = "Sort files by kind",
                Hint = "Moves files into Images, Videos, Audio, Documents, Archives, Code, Other. Undoable.",
                Confirm = "Sort the files in\n{0}\ninto Images / Videos / Audio / Documents / Archives / Code / Other?", Run = (p, c) => SortInto(p, c, "Sort by kind", f => Kind(Path.GetExtension(f))) },
            new Action { Id = "rename", Group = "Organize", Label = "Batch rename...",
                Hint = "Find/replace, prefix/suffix, numbering, case, spaces - with a live preview. Undoable.", Run = null },

            // ---------- Clean up ----------
            new Action { Id = "delete-empty", Group = "Clean up", Label = "Remove empty folders",
                Hint = "Deletes empty subfolders, deepest first, so parents that become empty go too.",
                Confirm = "Remove all empty folders under\n{0} ?", Run = DeleteEmpty },
            new Action { Id = "duplicates", Group = "Clean up", Label = "Find duplicates",
                Hint = "Groups by size, then hashes candidates (SHA-256). Tick the copies to send to the Recycle Bin.", Run = Duplicates },
            new Action { Id = "unblock", Group = "Clean up", Label = "Unblock downloaded files",
                Hint = "Removes the \"downloaded from the internet\" mark from every file below.",
                Confirm = "Unblock every downloaded file under\n{0} ?", Run = Unblock },

            // ---------- Inspect ----------
            new Action { Id = "folder-sizes", Group = "Inspect", Label = "Subfolder sizes", Hint = "Total size of each immediate subfolder.", Run = FolderSizes },
            new Action { Id = "by-type", Group = "Inspect", Label = "Disk usage by type", Hint = "File count and total size per extension.", Run = ByType },
            new Action { Id = "largest", Group = "Inspect", Label = "Largest files", Hint = "The 200 biggest files below this folder.", Run = Largest },
            new Action { Id = "recent", Group = "Inspect", Label = "Changed in last 7 days", Hint = "Files modified in the last week, newest first.", Run = Recent },
            new Action { Id = "long-paths", Group = "Inspect", Label = "Paths over 240 chars", Hint = "Paths that tend to break copy and zip tools.", Run = LongPaths },

            // ---------- Export ----------
            new Action { Id = "inventory", Group = "Export", Label = "Export inventory.csv", Hint = "Path, size and dates of every file, saved into this folder.", Run = Inventory },
            new Action { Id = "copy-names", Group = "Export", Label = "Copy file names", Hint = "Names in this folder to the clipboard, one per line.", Run = null },
            new Action { Id = "copy-tree", Group = "Export", Label = "Copy folder tree as text", Hint = "An indented tree of folders and files to the clipboard.", Run = null },

            // ---------- PowerShell library (commands.json) ----------
            new Action { Id = "library", Group = "PowerShell", Label = "Command library...",
                Hint = "Browse the snippets in commands.json. Copy one, or open PowerShell in this folder with it pre-loaded for review.", Run = null },
        };

        public static Action Find(string id) { return All.FirstOrDefault(a => a.Id == id); }

        static readonly Dictionary<string, string> Kinds = BuildKinds();
        static Dictionary<string, string> BuildKinds()
        {
            var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            string[][] map = {
                new[] { "Images", ".jpg .jpeg .png .gif .webp .bmp .tif .tiff .heic .svg .ico .raw .psd" },
                new[] { "Videos", ".mp4 .mkv .mov .avi .wmv .webm .m4v" },
                new[] { "Audio", ".mp3 .wav .flac .m4a .ogg .aac .wma .opus" },
                new[] { "Documents", ".pdf .doc .docx .xls .xlsx .ppt .pptx .txt .rtf .odt .ods .csv .md .epub" },
                new[] { "Archives", ".zip .rar .7z .tar .gz .bz2 .xz .iso" },
                new[] { "Code", ".js .ts .html .htm .css .json .py .cs .ps1 .bat .cmd .c .cpp .h .java .go .rs .xml .yml .yaml .sh .sql" },
                new[] { "Installers", ".exe .msi .msix .appx" } };
            foreach (var m in map) foreach (var e in m[1].Split(' ')) d[e] = m[0];
            return d;
        }
        static string Kind(string ext) { string k; return Kinds.TryGetValue(ext, out k) ? k : "Other"; }

        // ================= Organize =================

        static Result FlattenCopy(string root, Ctx c)
        {
            string dest = root.TrimEnd('\\') + "-flat";
            if (Path.GetPathRoot(root) == root) throw new Exception("Pick a folder, not a whole drive.");
            Directory.CreateDirectory(dest);
            int n = 0;
            foreach (var f in Fs.Files(root, true).ToList())
            {
                c.Check();
                File.Copy(f, Fs.FreeName(dest, Path.GetFileName(f)));
                if (++n % 50 == 0) c.Report("Copied " + n);
            }
            return new Result { Summary = "Copied " + n + " files to " + dest };
        }

        static Result FlattenMove(string root, Ctx c)
        {
            if (Path.GetPathRoot(root) == root) throw new Exception("Pick a folder, not a whole drive.");
            var j = new Journal("Flatten " + root);
            int n = 0;
            try
            {
                foreach (var f in Fs.Files(root, true).Where(f => !string.Equals(Path.GetDirectoryName(f), root.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase)).ToList())
                {
                    c.Check();
                    j.Move(f, Fs.FreeName(root, Path.GetFileName(f)));
                    if (++n % 50 == 0) c.Report("Moved " + n);
                }
                int d = RemoveEmpty(root, j, c);
                return new Result { Summary = "Moved " + n + " files, removed " + d + " empty folders." };
            }
            finally { j.Save(); }
        }

        static Result SortInto(string root, Ctx c, string title, Func<string, string> folderOf)
        {
            var j = new Journal(title + " " + root);
            int n = 0;
            try
            {
                foreach (var f in Fs.Files(root, false).ToList())
                {
                    c.Check();
                    string target = Path.Combine(root, folderOf(f));
                    j.EnsureDir(target);
                    j.Move(f, Fs.FreeName(target, Path.GetFileName(f)));
                    n++;
                }
            }
            finally { j.Save(); }
            return new Result { Summary = "Moved " + n + " files." };
        }

        // ================= Clean up =================

        static int RemoveEmpty(string root, Journal j, Ctx c)
        {
            int n = 0;
            foreach (var d in Fs.DirsDeepestFirst(root))
            {
                c.Check();
                if (Fs.IsEmptyDir(d)) { try { Directory.Delete(d); if (j != null) j.RemovedDir(d); n++; } catch { } }
            }
            return n;
        }

        static Result DeleteEmpty(string root, Ctx c)
        {
            var j = new Journal("Remove empty folders " + root);
            try { return new Result { Summary = "Removed " + RemoveEmpty(root, j, c) + " empty folders." }; }
            finally { j.Save(); }
        }

        static Result Duplicates(string root, Ctx c)
        {
            c.Report("Listing files...");
            var bySize = new Dictionary<long, List<string>>();
            foreach (var f in Fs.Files(root, true))
            {
                c.Check();
                long len; try { len = new FileInfo(f).Length; } catch { continue; }
                if (len == 0) continue;
                List<string> l; if (!bySize.TryGetValue(len, out l)) bySize[len] = l = new List<string>();
                l.Add(f);
            }
            var rows = new List<string[]>();
            int sets = 0; long wasted = 0, done = 0;
            var candidates = bySize.Where(kv => kv.Value.Count > 1).ToList();
            long total = candidates.Sum(kv => kv.Value.Count);
            using (var sha = SHA256.Create())
            {
                foreach (var kv in candidates)
                {
                    var byHash = new Dictionary<string, List<string>>();
                    foreach (var f in kv.Value)
                    {
                        c.Check();
                        if (++done % 10 == 0) c.Report("Hashing " + done + " / " + total);
                        string h;
                        try { using (var s = File.OpenRead(f)) h = BitConverter.ToString(sha.ComputeHash(s)).Replace("-", ""); } catch { continue; }
                        List<string> l; if (!byHash.TryGetValue(h, out l)) byHash[h] = l = new List<string>();
                        l.Add(f);
                    }
                    foreach (var g in byHash.Where(x => x.Value.Count > 1))
                    {
                        sets++; wasted += kv.Key * (g.Value.Count - 1);
                        // Keep the shortest path unticked as the "original" suggestion.
                        var ordered = g.Value.OrderBy(p => p.Length).ThenBy(p => p).ToList();
                        for (int i = 0; i < ordered.Count; i++)
                            rows.Add(new[] { i == 0 ? "" : "x", "#" + sets, Fs.Size(kv.Key), ordered[i] });
                    }
                }
            }
            return new Result
            {
                Summary = sets == 0 ? "No duplicates found." : sets + " duplicate sets, " + Fs.Size(wasted) + " reclaimable. Copies are pre-ticked; the shortest path in each set is kept.",
                Columns = new[] { "", "Set", "Size", "Path" }, Rows = rows, PathColumn = 3, Checkable = true
            };
        }

        static Result Unblock(string root, Ctx c)
        {
            int n = 0;
            foreach (var f in Fs.Files(root, true)) { c.Check(); if (Fs.Unblock(f)) n++; }
            return new Result { Summary = "Unblocked " + n + " files." };
        }

        // ================= Inspect =================

        static Result FolderSizes(string root, Ctx c)
        {
            var rows = new List<string[]>();
            var raw = new List<KeyValuePair<long, string[]>>();
            string[] dirs; try { dirs = Directory.GetDirectories(root); } catch { dirs = new string[0]; }
            foreach (var d in dirs)
            {
                c.Report(Path.GetFileName(d));
                long sum = 0; int count = 0;
                foreach (var f in Fs.Files(d, true)) { c.Check(); try { sum += new FileInfo(f).Length; count++; } catch { } }
                raw.Add(new KeyValuePair<long, string[]>(sum, new[] { Path.GetFileName(d), Fs.Size(sum), count.ToString("N0"), d }));
            }
            long fileSum = 0; foreach (var f in Fs.Files(root, false)) try { fileSum += new FileInfo(f).Length; } catch { }
            raw.Add(new KeyValuePair<long, string[]>(fileSum, new[] { "(files here)", Fs.Size(fileSum), "", root }));
            foreach (var kv in raw.OrderByDescending(x => x.Key)) rows.Add(kv.Value);
            return new Result { Summary = "Total " + Fs.Size(raw.Sum(x => x.Key)), Columns = new[] { "Folder", "Size", "Files", "Path" }, Rows = rows, PathColumn = 3 };
        }

        static Result ByType(string root, Ctx c)
        {
            var map = new Dictionary<string, long[]>(StringComparer.OrdinalIgnoreCase);
            foreach (var f in Fs.Files(root, true))
            {
                c.Check();
                string e = Path.GetExtension(f); if (e.Length == 0) e = "(none)";
                long[] v; if (!map.TryGetValue(e, out v)) map[e] = v = new long[2];
                v[0]++; try { v[1] += new FileInfo(f).Length; } catch { }
            }
            var rows = map.OrderByDescending(kv => kv.Value[1]).Select(kv => new[] { kv.Key, kv.Value[0].ToString("N0"), Fs.Size(kv.Value[1]) }).ToList();
            return new Result { Summary = map.Count + " file types", Columns = new[] { "Extension", "Files", "Size" }, Rows = rows };
        }

        static Result FileList(string root, Ctx c, Func<FileInfo, bool> keep, Func<IEnumerable<FileInfo>, IEnumerable<FileInfo>> order, string what)
        {
            var list = new List<FileInfo>();
            int n = 0;
            foreach (var f in Fs.Files(root, true))
            {
                c.Check();
                if (++n % 500 == 0) c.Report("Scanned " + n);
                try { var fi = new FileInfo(f); if (keep(fi)) list.Add(fi); } catch { }
            }
            // Leading "" column = tick state (unticked).
            var rows = order(list).Select(fi => new[] { "", fi.Name, Fs.Size(fi.Length), fi.LastWriteTime.ToString("yyyy-MM-dd HH:mm"), fi.FullName }).ToList();
            return new Result { Summary = rows.Count + " " + what, Columns = new[] { "", "Name", "Size", "Modified", "Path" }, Rows = rows, PathColumn = 4, Checkable = true };
        }

        static Result Largest(string root, Ctx c) { return FileList(root, c, f => true, l => l.OrderByDescending(f => f.Length).Take(200), "largest files"); }
        static Result Recent(string root, Ctx c) { var t = DateTime.Now.AddDays(-7); return FileList(root, c, f => f.LastWriteTime > t, l => l.OrderByDescending(f => f.LastWriteTime), "files changed in the last 7 days"); }
        static Result LongPaths(string root, Ctx c) { return FileList(root, c, f => f.FullName.Length > 240, l => l.OrderByDescending(f => f.FullName.Length), "files with paths over 240 characters"); }

        // ================= Export =================

        static Result Inventory(string root, Ctx c)
        {
            string outFile = Path.Combine(root, "inventory.csv");
            var sb = new StringBuilder("FullName,Length,CreationTime,LastWriteTime\r\n");
            int n = 0;
            foreach (var f in Fs.Files(root, true))
            {
                c.Check();
                if (string.Equals(f, outFile, StringComparison.OrdinalIgnoreCase)) continue;
                try
                {
                    var fi = new FileInfo(f);
                    sb.Append('"').Append(fi.FullName.Replace("\"", "\"\"")).Append("\",").Append(fi.Length).Append(',')
                      .Append(fi.CreationTime.ToString("yyyy-MM-dd HH:mm:ss")).Append(',').Append(fi.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss")).Append("\r\n");
                    n++;
                }
                catch { }
            }
            File.WriteAllText(outFile, sb.ToString(), new UTF8Encoding(true));
            return new Result { Summary = "Saved " + n + " rows to " + outFile };
        }

        public static string NamesText(string root)
        {
            var names = Directory.GetFileSystemEntries(root).Select(Path.GetFileName).OrderBy(x => x);
            return string.Join("\r\n", names);
        }

        public static string TreeText(string root)
        {
            var sb = new StringBuilder(Path.GetFileName(root.TrimEnd('\\')) + "\r\n");
            Tree(root, "", sb, 0);
            return sb.ToString();
        }

        static void Tree(string dir, string indent, StringBuilder sb, int depth)
        {
            if (depth > 30) return;
            string[] dirs, files;
            try { dirs = Directory.GetDirectories(dir).OrderBy(x => x).ToArray(); files = Directory.GetFiles(dir).OrderBy(x => x).ToArray(); } catch { return; }
            var all = dirs.Concat(files).ToArray();
            for (int i = 0; i < all.Length; i++)
            {
                bool last = i == all.Length - 1;
                sb.Append(indent).Append(last ? "\\-- " : "+-- ").Append(Path.GetFileName(all[i])).Append("\r\n");
                if (i < dirs.Length && !Fs.IsLink(all[i])) Tree(all[i], indent + (last ? "    " : "|   "), sb, depth + 1);
            }
        }
    }
}
