using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace PSTools
{
    static partial class Actions
    {
        static Func<string[], Ctx, Result> F(Func<string, Ctx, Result> fn)
        {
            return (p, c) => fn(p[0], c);
        }

        static IEnumerable<Action> Classic()
        {
            var d = Target.Dirs;
            yield return new Action { Id = "flatten-copy", Group = "Organize", Label = "Flatten tree (copy)", Tier = Tier.Confirm, Targets = d,
                Hint = "Copies every file from all subfolders into a new sibling folder \"<name>-flat\". Duplicate names become name_1.ext.",
                Confirm = "Copy every file under\n{0}\ninto\n{0}-flat ?", Run = F(FlattenCopy) };
            yield return new Action { Id = "flatten-move", Group = "Organize", Label = "Flatten tree (move here)", Tier = Tier.Confirm, Targets = d,
                Hint = "Moves every file from subfolders up into this folder, then removes the emptied subfolders. Undoable.",
                Confirm = "Move all files from the subfolders of\n{0}\ninto the folder itself and remove the empty subfolders?", Run = F(FlattenMove) };
            yield return new Action { Id = "organize-ext", Group = "Organize", Label = "Sort files by extension", Tier = Tier.Confirm, Targets = d,
                Hint = "Moves files in this folder into JPG, PDF, ... subfolders. Undoable.",
                Confirm = "Sort the files in\n{0}\ninto folders by extension?",
                Run = F((p, c) => SortInto(p, c, "Sort by extension", f => { var e = Path.GetExtension(f).TrimStart('.').ToUpperInvariant(); return e.Length == 0 ? "NOEXT" : e; })) };
            yield return new Action { Id = "organize-month", Group = "Organize", Label = "Sort files by month", Tier = Tier.Confirm, Targets = d,
                Hint = "Moves files in this folder into yyyy-MM subfolders by modified date. Undoable.",
                Confirm = "Sort the files in\n{0}\ninto yyyy-MM folders by modified date?",
                Run = F((p, c) => SortInto(p, c, "Sort by month", f => File.GetLastWriteTime(f).ToString("yyyy-MM"))) };
            yield return new Action { Id = "organize-type", Group = "Organize", Label = "Sort files by kind", Tier = Tier.Confirm, Targets = d,
                Hint = "Moves files into Images, Videos, Audio, Documents, Archives, Code, Other. Undoable.",
                Confirm = "Sort the files in\n{0}\ninto Images / Videos / Audio / Documents / Archives / Code / Other?",
                Run = F((p, c) => SortInto(p, c, "Sort by kind", f => Kind(Path.GetExtension(f))) ) };
            yield return new Action { Id = "rename", Group = "Organize", Label = "Batch rename...", Targets = d,
                Hint = "Find/replace, prefix/suffix, numbering, case, spaces - with a live preview. Undoable.", Window = p => new RenameForm(p[0]) };

            yield return new Action { Id = "delete-empty", Group = "Clean up", Label = "Remove empty folders", Tier = Tier.Confirm, Targets = d,
                Hint = "Deletes empty subfolders, deepest first, so parents that become empty go too. Undoable.",
                Confirm = "Remove all empty folders under\n{0} ?", Run = F(DeleteEmpty) };
            yield return new Action { Id = "duplicates", Group = "Clean up", Label = "Find duplicates", Targets = d,
                Hint = "Groups by size, then hashes candidates (SHA-256). Tick the copies to send to the Recycle Bin.", Run = F(Duplicates) };
            yield return new Action { Id = "unblock", Group = "Clean up", Label = "Unblock downloaded files", Tier = Tier.Confirm, Targets = d,
                Hint = "Removes the \"downloaded from the internet\" mark from every file below.",
                Confirm = "Unblock every downloaded file under\n{0} ?", Run = F(Unblock) };

            yield return new Action { Id = "folder-sizes", Group = "Inspect", Label = "Subfolder sizes", Targets = d, Hint = "Total size of each immediate subfolder.", Run = F(FolderSizes) };
            yield return new Action { Id = "by-type", Group = "Inspect", Label = "Disk usage by type", Targets = d, Hint = "File count and total size per extension.", Run = F(ByType) };
            yield return new Action { Id = "largest", Group = "Inspect", Label = "Largest files", Targets = d, Hint = "The 200 biggest files below this folder.", Run = F(Largest) };
            yield return new Action { Id = "recent", Group = "Inspect", Label = "Changed in last 7 days", Targets = d, Hint = "Files modified in the last week, newest first.", Run = F(Recent) };
            yield return new Action { Id = "long-paths", Group = "Inspect", Label = "Paths over 240 chars", Targets = d, Hint = "Paths that tend to break copy and zip tools.", Run = F(LongPaths) };

            yield return new Action { Id = "inventory", Group = "Export", Label = "Export inventory.csv", Tier = Tier.Confirm, Targets = d,
                Hint = "Path, size and dates of every file, saved into this folder.", Confirm = "Write inventory.csv into\n{0} ?", Run = F(Inventory) };
            yield return new Action { Id = "library", Group = "PowerShell", Label = "Command library...", Targets = d,
                Hint = "Browse the snippets in commands.json. Copy one, or open PowerShell in this folder with it pre-loaded for review.", Window = p => new LibraryForm(p[0]) };
        }

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
            var rows = order(list).Select(fi => new[] { "", fi.Name, Fs.Size(fi.Length), fi.LastWriteTime.ToString("yyyy-MM-dd HH:mm"), fi.FullName }).ToList();
            return new Result { Summary = rows.Count + " " + what, Columns = new[] { "", "Name", "Size", "Modified", "Path" }, Rows = rows, PathColumn = 4, Checkable = true };
        }

        static Result Largest(string root, Ctx c) { return FileList(root, c, f => true, l => l.OrderByDescending(f => f.Length).Take(200), "largest files"); }
        static Result Recent(string root, Ctx c) { var t = DateTime.Now.AddDays(-7); return FileList(root, c, f => f.LastWriteTime > t, l => l.OrderByDescending(f => f.LastWriteTime), "files changed in the last 7 days"); }
        static Result LongPaths(string root, Ctx c) { return FileList(root, c, f => f.FullName.Length > 240, l => l.OrderByDescending(f => f.FullName.Length), "files with paths over 240 characters"); }


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
    }
}
