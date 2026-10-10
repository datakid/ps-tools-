using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Forms;
using Microsoft.VisualBasic;

namespace PSTools
{
    static class Marks
    {
        static string FilePath { get { return Path.Combine(Journal.Dir, "marks.txt"); } }

        public static void Set(IEnumerable<string> paths)
        {
            Directory.CreateDirectory(Journal.Dir);
            File.WriteAllLines(FilePath, paths.Select(Fs.Full).ToArray(), Encoding.UTF8);
        }

        public static string[] Get()
        {
            try { return File.Exists(FilePath) ? File.ReadAllLines(FilePath, Encoding.UTF8).Where(l => l.Trim().Length > 0).ToArray() : new string[0]; }
            catch { return new string[0]; }
        }
    }

    static partial class Actions
    {
        static string Stamp() { return DateTime.Now.ToString("yyyyMMdd-HHmmss"); }

        static IEnumerable<Action> CreateModule()
        {
            yield return new Action
            {
                Id = "paste-image", Group = "Create", Label = "Paste clipboard image as PNG", Targets = Target.Dirs, Fast = true, DefaultFav = true,
                Hint = "Saves the image on the clipboard (a screenshot, a copied picture) as clip-<date>.png in this folder.",
                Run = (paths, c) =>
                {
                    Image img = Clipboard.ContainsImage() ? Clipboard.GetImage() : null;
                    if (img == null) return new Result { Summary = "The clipboard does not contain an image." };
                    string target = Fs.FreeName(paths[0], "clip-" + Stamp() + ".png");
                    using (img) img.Save(target, ImageFormat.Png);
                    var j = new Journal("Paste image into " + paths[0]);
                    j.Link(target);
                    j.Save();
                    return new Result { Summary = "Saved " + Path.GetFileName(target) };
                }
            };

            yield return new Action
            {
                Id = "paste-text", Group = "Create", Label = "Paste clipboard text as file", Targets = Target.Dirs, Fast = true,
                Hint = "Saves the text on the clipboard as clip-<date>.txt (.json or .html when it clearly is one).",
                Run = (paths, c) =>
                {
                    string t = Clipboard.ContainsText() ? Clipboard.GetText() : "";
                    if (t.Trim().Length == 0) return new Result { Summary = "The clipboard does not contain text." };
                    string s = t.TrimStart();
                    string ext = ".txt";
                    if ((s.StartsWith("{") || s.StartsWith("[")) && (s.TrimEnd().EndsWith("}") || s.TrimEnd().EndsWith("]"))) ext = ".json";
                    else if (s.StartsWith("<!DOCTYPE html", StringComparison.OrdinalIgnoreCase) || s.StartsWith("<html", StringComparison.OrdinalIgnoreCase)) ext = ".html";
                    string target = Fs.FreeName(paths[0], "clip-" + Stamp() + ext);
                    File.WriteAllText(target, t, new UTF8Encoding(false));
                    var j = new Journal("Paste text into " + paths[0]);
                    j.Link(target);
                    j.Save();
                    return new Result { Summary = "Saved " + Path.GetFileName(target) };
                }
            };

            yield return new Action
            {
                Id = "new-folder-clip", Group = "Create", Label = "New folder named from clipboard", Targets = Target.Dirs, Fast = true,
                Hint = "Creates a folder named after the first line of the clipboard text (invalid characters become _).",
                Run = (paths, c) =>
                {
                    string t = Clipboard.ContainsText() ? Clipboard.GetText() : "";
                    string first = t.Replace("\r", "").Split('\n').FirstOrDefault(l => l.Trim().Length > 0) ?? "";
                    string name = Fs.Sanitize(first);
                    if (name.Length == 0) return new Result { Summary = "The clipboard has no usable text for a folder name." };
                    string target = Fs.FreeName(paths[0], name);
                    var j = new Journal("New folder " + target);
                    j.EnsureDir(target);
                    j.Save();
                    return new Result { Summary = "Created " + Path.GetFileName(target) };
                }
            };

            yield return new Action
            {
                Id = "new-dated-folder", Group = "Create", Label = "New dated folder", Targets = Target.Dirs, Fast = true,
                Hint = "Creates a folder named with today's date, yyyy-MM-dd.",
                Run = (paths, c) =>
                {
                    string target = Fs.FreeName(paths[0], DateTime.Now.ToString("yyyy-MM-dd"));
                    var j = new Journal("New folder " + target);
                    j.EnsureDir(target);
                    j.Save();
                    return new Result { Summary = "Created " + Path.GetFileName(target) };
                }
            };

            yield return new Action
            {
                Id = "group-selected", Group = "Create", Label = "Move selection into new folder...", Targets = Target.File | Target.Folder, Multi = true, Fast = true,
                Hint = "Asks for a folder name, creates it next to the selected items and moves them in. Undoable.",
                Run = (paths, c) =>
                {
                    string parent = ParentOf(paths[0]);
                    if (paths.Any(p => !string.Equals(ParentOf(p), parent, StringComparison.OrdinalIgnoreCase)))
                        return new Result { Summary = "The selected items are not all in the same folder." };
                    string first = Path.GetFileNameWithoutExtension(NameOf(paths[0]));
                    string name = Interaction.InputBox("Folder name for " + paths.Length + (paths.Length == 1 ? " item:" : " items:"), "Move into new folder", first.Length > 0 ? first : "New folder");
                    name = Fs.Sanitize(name ?? "");
                    if (name.Length == 0) return null;
                    string target = Fs.FreeName(parent, name);
                    var j = new Journal("Group into " + target);
                    try
                    {
                        j.EnsureDir(target);
                        int n = 0;
                        foreach (var p in paths)
                        {
                            if (Directory.Exists(p) && Fs.IsInside(target, p)) continue;
                            j.Move(p, Fs.FreeName(target, NameOf(p)));
                            n++;
                        }
                        return new Result { Summary = "Moved " + n + " items into " + Path.GetFileName(target) };
                    }
                    finally { j.Save(); }
                }
            };

            yield return new Action
            {
                Id = "unwrap", Group = "Create", Label = "Unwrap folder (move contents up)", Targets = Target.Folder, Tier = Tier.Confirm,
                Hint = "Moves everything inside the folder up into its parent and removes the empty folder. Fixes folder-in-folder after unzipping. Undoable.",
                Confirm = "Move everything inside\n{0}\nup one level and remove the empty folder?",
                Run = (paths, c) =>
                {
                    string dir = Fs.Full(paths[0]);
                    if (Fs.IsRoot(dir)) throw new Exception("Pick a folder, not a drive.");
                    string parent = Path.GetDirectoryName(dir);
                    string tmp = Path.Combine(parent, "~pst" + Guid.NewGuid().ToString("N").Substring(0, 8));
                    var j = new Journal("Unwrap " + dir);
                    int n = 0;
                    try
                    {
                        j.Move(dir, tmp);
                        foreach (var e in Directory.GetFileSystemEntries(tmp))
                        {
                            c.Check();
                            j.Move(e, Fs.FreeName(parent, Path.GetFileName(e)));
                            n++;
                        }
                        if (Fs.IsEmptyDir(tmp)) { Directory.Delete(tmp); j.RemovedDir(tmp); }
                    }
                    finally { j.Save(); }
                    return new Result { Summary = "Moved " + n + " items up and removed the folder." };
                }
            };

            yield return new Action
            {
                Id = "snapshot", Group = "Create", Label = "Snapshot copy with date", Targets = Target.Dirs, Tier = Tier.Confirm,
                Hint = "Copies the whole folder next to itself as <name>_yyyy-MM-dd. Nothing is overwritten or deleted.",
                Confirm = "Copy all of\n{0}\ninto a new dated folder next to it?",
                Run = (paths, c) =>
                {
                    string src = Fs.Full(paths[0]);
                    if (Fs.IsRoot(src)) throw new Exception("Pick a folder, not a whole drive.");
                    string dest = Fs.FreeName(Path.GetDirectoryName(src), Path.GetFileName(src) + "_" + DateTime.Now.ToString("yyyy-MM-dd"));
                    if (Fs.IsInside(dest, src)) throw new Exception("The destination would be inside the source.");
                    Directory.CreateDirectory(dest);
                    int n = 0; long bytes = 0;
                    foreach (var f in Fs.Files(src, true).ToList())
                    {
                        c.Check();
                        string rel = f.Substring(src.Length).TrimStart('\\');
                        string to = Path.Combine(dest, rel);
                        Directory.CreateDirectory(Path.GetDirectoryName(to));
                        try { File.Copy(f, to); n++; bytes += new FileInfo(to).Length; } catch { }
                        if (n % 50 == 0) c.Report("Copied " + n + " files");
                    }
                    foreach (var d in Fs.DirsDeepestFirst(src))
                    {
                        string to = Path.Combine(dest, d.Substring(src.Length).TrimStart('\\'));
                        if (!Directory.Exists(to)) try { Directory.CreateDirectory(to); } catch { }
                    }
                    return new Result { Summary = "Copied " + n + " files (" + Fs.Size(bytes) + ") to " + dest };
                }
            };
        }

        static Action AttrAction(string id, string label, string hint, FileAttributes flag, bool set)
        {
            return new Action
            {
                Id = id, Group = "Fix", Label = label, Hint = hint, Targets = Target.File | Target.Folder, Multi = true, Fast = true,
                Run = (paths, c) =>
                {
                    var j = new Journal(label + " (" + Actions.Describe(paths) + ")");
                    int n = 0;
                    try
                    {
                        foreach (var p in paths)
                        {
                            var a = File.GetAttributes(p);
                            var b = set ? a | flag : a & ~flag;
                            if (a == b) continue;
                            j.Attrs(p);
                            File.SetAttributes(p, b);
                            n++;
                        }
                    }
                    finally { j.Save(); }
                    return new Result { Summary = label + ": " + n + " changed" };
                }
            };
        }

        static int[] FindBom(byte[] b) { return b.Length >= 3 && b[0] == 0xEF && b[1] == 0xBB && b[2] == 0xBF ? new[] { 3 } : new int[0]; }

        static byte[] ToLf(byte[] src)
        {
            var o = new List<byte>(src.Length);
            for (int i = 0; i < src.Length; i++)
                if (!(src[i] == 13 && i + 1 < src.Length && src[i + 1] == 10)) o.Add(src[i]);
            return o.ToArray();
        }

        static byte[] ToCrlf(byte[] src)
        {
            var o = new List<byte>(src.Length + src.Length / 20);
            foreach (var v in ToLf(src)) { if (v == 10) o.Add(13); o.Add(v); }
            return o.ToArray();
        }

        static Action TextFix(string id, string label, string hint, Func<byte[], byte[]> fix, bool needUtf8)
        {
            return new Action
            {
                Id = id, Group = "Fix", Label = label, Hint = hint, Targets = Target.File, Multi = true, Tier = Tier.Confirm,
                Confirm = label + " for {0}?\nBinary files are skipped. Undoable.",
                Run = (paths, c) =>
                {
                    var j = new Journal(label + " (" + Actions.Describe(paths) + ")");
                    int changed = 0, same = 0, skipped = 0;
                    try
                    {
                        foreach (var p in paths)
                        {
                            c.Check();
                            var fi = new FileInfo(p);
                            if (fi.Length > 50 * 1024 * 1024 || !Fs.LooksText(p)) { skipped++; continue; }
                            byte[] data = File.ReadAllBytes(p);
                            if (needUtf8)
                            {
                                try { new UTF8Encoding(false, true).GetString(data); } catch { skipped++; continue; }
                            }
                            byte[] res = fix(data);
                            if (res.SequenceEqual(data)) { same++; continue; }
                            j.Backup(p);
                            File.WriteAllBytes(p, res);
                            changed++;
                        }
                    }
                    finally { j.Save(); }
                    return new Result { Summary = changed + " changed, " + same + " already fine" + (skipped > 0 ? ", " + skipped + " skipped (binary or not UTF-8)" : "") + "." };
                }
            };
        }

        static IEnumerable<Action> FixModule()
        {
            yield return new Action
            {
                Id = "force-delete", Group = "Fix", Label = "Force delete (stubborn items)", Targets = Target.File | Target.Folder, Multi = true, Tier = Tier.Confirm,
                Hint = "Permanently deletes items Explorer refuses to remove: reserved names (nul, aux, con), trailing dots or spaces, over-long paths. Skips the Recycle Bin.",
                Confirm = "PERMANENTLY delete {0}?\nThis bypasses the Recycle Bin and cannot be undone.",
                Run = (paths, c) =>
                {
                    int ok = 0; var bad = new List<string>();
                    foreach (var raw in paths)
                    {
                        c.Check();
                        string p = Fs.Full(raw);
                        if (Fs.IsProtected(p)) { bad.Add(NameOf(p) + " (protected location)"); continue; }
                        string unc = "\\\\?\\" + p;
                        int exit;
                        Fs.Cmd(Directory.Exists(p) ? "rd /s /q \"" + unc + "\"" : "del /f /q \"" + unc + "\"", 120000, out exit);
                        if (!File.Exists(p) && !Directory.Exists(p)) ok++; else bad.Add(NameOf(p));
                    }
                    return new Result { Summary = "Deleted " + ok + (bad.Count > 0 ? ". Could not delete: " + string.Join(", ", bad) : ".") };
                }
            };

            yield return new Action
            {
                Id = "touch", Group = "Fix", Label = "Set dates to now", Targets = Target.File | Target.Folder, Multi = true, Tier = Tier.Confirm,
                Hint = "Sets created, modified and accessed time of the selected items to the current time. Undoable.",
                Confirm = "Set the dates of {0} to now?",
                Run = (paths, c) =>
                {
                    var j = new Journal("Set dates (" + Actions.Describe(paths) + ")");
                    var now = DateTime.Now;
                    try
                    {
                        foreach (var p in paths)
                        {
                            j.Times(p);
                            if (Directory.Exists(p)) { Directory.SetCreationTime(p, now); Directory.SetLastWriteTime(p, now); Directory.SetLastAccessTime(p, now); }
                            else { File.SetCreationTime(p, now); File.SetLastWriteTime(p, now); File.SetLastAccessTime(p, now); }
                        }
                    }
                    finally { j.Save(); }
                    return new Result { Summary = "Updated " + paths.Length + " items." };
                }
            };

            yield return AttrAction("attr-hide", "Hide", "Sets the Hidden attribute. Undoable.", FileAttributes.Hidden, true);
            yield return AttrAction("attr-unhide", "Unhide", "Clears the Hidden attribute. Undoable.", FileAttributes.Hidden, false);
            yield return AttrAction("attr-ro-on", "Make read-only", "Sets the Read-only attribute. Undoable.", FileAttributes.ReadOnly, true);
            yield return AttrAction("attr-ro-off", "Clear read-only", "Clears the Read-only attribute. Undoable.", FileAttributes.ReadOnly, false);

            yield return new Action
            {
                Id = "unblock-files", Group = "Fix", Label = "Unblock selected files", Targets = Target.File, Multi = true, Fast = true,
                Hint = "Removes the \"downloaded from the internet\" mark from the selected files.",
                Run = (paths, c) => new Result { Summary = "Unblocked " + paths.Count(Fs.Unblock) + " of " + paths.Length + " files." }
            };

            yield return TextFix("eol-lf", "Convert line endings to LF", "Windows CRLF to Unix LF. Text files only.", ToLf, false);
            yield return TextFix("eol-crlf", "Convert line endings to CRLF", "Unix LF to Windows CRLF. Text files only.", ToCrlf, false);
            yield return TextFix("bom-add", "Add UTF-8 BOM", "Adds the UTF-8 byte order mark. Only valid UTF-8 files are touched.",
                d => d.Length >= 3 && d[0] == 0xEF && d[1] == 0xBB && d[2] == 0xBF ? d : new byte[] { 0xEF, 0xBB, 0xBF }.Concat(d).ToArray(), true);
            yield return TextFix("bom-remove", "Remove UTF-8 BOM", "Strips the UTF-8 byte order mark if present.",
                d => FindBom(d).Length > 0 ? d.Skip(3).ToArray() : d, false);

            yield return new Action
            {
                Id = "take-ownership", Group = "Fix", Label = "Take ownership and grant full control", Targets = Target.File | Target.Folder, Multi = true, Tier = Tier.Elevated,
                Hint = "For \"Access denied\" on your own old folders. Runs takeown and icacls in an elevated PowerShell. Refuses drives, Windows, Program Files and your profile root.",
                Script = paths =>
                {
                    var sb = new StringBuilder();
                    foreach (var raw in paths)
                    {
                        string p = Fs.Full(raw);
                        if (Fs.IsProtected(p)) throw new Exception("Refusing to change permissions on " + p);
                        string q = "'" + p.Replace("'", "''") + "'";
                        bool dir = Directory.Exists(p);
                        sb.AppendLine("takeown /f " + q + (dir ? " /r /d y" : ""));
                        sb.AppendLine("icacls " + q + " /grant \"${env:USERNAME}:" + (dir ? "(OI)(CI)F" : "F") + "\"" + (dir ? " /t /c" : ""));
                    }
                    return sb.ToString();
                }
            };
        }

        static IEnumerable<Action> LinksModule()
        {
            yield return new Action
            {
                Id = "mark", Group = "Links", Label = "Mark for link or compare", Targets = Target.File | Target.Folder, Multi = true, Fast = true,
                Hint = "Remembers the selected items. Then right-click a destination folder and choose a link or compare action.",
                Run = (paths, c) =>
                {
                    Marks.Set(paths);
                    return new Result { Summary = "Marked " + paths.Length + (paths.Length == 1 ? " item" : " items") + ". Now right-click the destination folder." };
                }
            };
            yield return LinkAction("link-junction", "Paste marked as junction here", "Creates a junction (folder link, no admin needed) for each marked folder. Undoable.", 0);
            yield return LinkAction("link-symlink", "Paste marked as symbolic link here", "Needs Developer Mode or an elevated session. Undoable.", 1);
            yield return LinkAction("link-hard", "Paste marked as hard link here", "Hard link for marked files. Works only on the same drive. Undoable.", 2);

            yield return new Action
            {
                Id = "compare-marked", Group = "Links", Label = "Compare with marked folder", Targets = Target.Dirs,
                Hint = "Lists files that exist only on one side or differ in content between the marked folder and this one.",
                Run = (paths, c) =>
                {
                    var marks = Marks.Get().Where(Directory.Exists).ToArray();
                    if (marks.Length == 0) return new Result { Summary = "Mark a folder first (right-click it, Mark for link or compare)." };
                    string a = marks[0], b = Fs.Full(paths[0]);
                    var A = Index(a, c); var B = Index(b, c);
                    var rows = new List<string[]>();
                    foreach (var k in A.Keys.Union(B.Keys).OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
                    {
                        c.Check();
                        FileInfo fa, fb;
                        bool ha = A.TryGetValue(k, out fa), hb = B.TryGetValue(k, out fb);
                        if (ha && !hb) rows.Add(new[] { "Only in marked", k, Fs.Size(fa.Length), "", fa.FullName });
                        else if (!ha && hb) rows.Add(new[] { "Only here", k, "", Fs.Size(fb.Length), fb.FullName });
                        else if (fa.Length != fb.Length) rows.Add(new[] { "Different size", k, Fs.Size(fa.Length), Fs.Size(fb.Length), fb.FullName });
                        else if (Math.Abs((fa.LastWriteTimeUtc - fb.LastWriteTimeUtc).TotalSeconds) > 2 && HashFile(fa.FullName, () => SHA256.Create(), c) != HashFile(fb.FullName, () => SHA256.Create(), c))
                            rows.Add(new[] { "Different content", k, Fs.Size(fa.Length), Fs.Size(fb.Length), fb.FullName });
                    }
                    return new Result
                    {
                        Summary = rows.Count == 0 ? "The folders are identical." : rows.Count + " differences.  Marked: " + a + "   Here: " + b,
                        Columns = new[] { "Status", "Relative path", "Marked", "Here", "Path" }, Rows = rows, PathColumn = 4
                    };
                }
            };
        }

        static Dictionary<string, FileInfo> Index(string root, Ctx c)
        {
            var d = new Dictionary<string, FileInfo>(StringComparer.OrdinalIgnoreCase);
            foreach (var f in Fs.Files(root, true))
            {
                c.Check();
                try { d[f.Substring(root.Length).TrimStart('\\')] = new FileInfo(f); } catch { }
            }
            return d;
        }

        static Action LinkAction(string id, string label, string hint, int kind)
        {
            return new Action
            {
                Id = id, Group = "Links", Label = label, Hint = hint, Targets = Target.Dirs, Tier = Tier.Confirm,
                Confirm = "Create links to the marked items inside\n{0} ?",
                Run = (paths, c) =>
                {
                    var marks = Marks.Get().Where(m => File.Exists(m) || Directory.Exists(m)).ToArray();
                    if (marks.Length == 0) return new Result { Summary = "Nothing is marked. Right-click the source and choose Mark for link or compare." };
                    string dest = Fs.Full(paths[0]);
                    var j = new Journal("Links in " + dest);
                    var errors = new List<string>();
                    int n = 0;
                    try
                    {
                        foreach (var m in marks)
                        {
                            bool dir = Directory.Exists(m);
                            if (kind == 2 && dir) { errors.Add(NameOf(m) + ": hard links work for files only"); continue; }
                            if (Fs.IsInside(dest, m) && dir) { errors.Add(NameOf(m) + ": cannot link a folder into itself"); continue; }
                            string link = Fs.FreeName(dest, NameOf(m));
                            bool ok; string err = "";
                            if (kind == 0)
                            {
                                if (!dir) { errors.Add(NameOf(m) + ": junctions are for folders (use a symbolic or hard link)"); continue; }
                                int exit;
                                string o = Fs.Cmd("mklink /J \"" + link + "\" \"" + m + "\"", 20000, out exit);
                                ok = exit == 0; err = o;
                            }
                            else if (kind == 1) { ok = Fs.MakeSymlink(link, m, dir); if (!ok) err = Fs.LastError(); }
                            else { ok = Fs.MakeHardLink(link, m); if (!ok) err = Fs.LastError(); }
                            if (ok) { j.Link(link); n++; } else errors.Add(NameOf(m) + ": " + err);
                        }
                    }
                    finally { j.Save(); }
                    return new Result { Summary = "Created " + n + " links." + (errors.Count > 0 ? "\n" + string.Join("\n", errors) : "") };
                }
            };
        }
    }
}
