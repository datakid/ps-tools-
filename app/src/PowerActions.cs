using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;

namespace PSTools
{
    static class PowerActions
    {
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern bool CreateHardLink(string newFile, string existingFile, IntPtr security);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern bool CreateSymbolicLink(string link, string target, int flags);

        static Action Make(string id, string menu, string title, string hint, Target targets, bool multi, Tier tier, bool undoable, Func<string[], Ctx, Result> run)
        {
            return new Action
            {
                Id = id, V4Menu = menu, V4Title = title, Label = title, Group = menu.StartsWith("Paste") ? "Create" : "Fix", Hint = hint,
                Targets = targets, Multi = multi, Tier = tier, Undoable = undoable, Fast = tier == Tier.Instant, Run = run,
                PsNone = "This runs inside PS Tools, so there is no single PowerShell command to copy."
            };
        }

        public static string Ask(string prompt, string def)
        {
            if (TestSink.Active) return TestSink.InputIn.Length > 0 ? TestSink.InputIn : def;
            return Microsoft.VisualBasic.Interaction.InputBox(prompt, "PS Tools", def);
        }

        static string SafeName(string s)
        {
            var sb = new StringBuilder();
            foreach (char c in s) sb.Append(Array.IndexOf(Path.GetInvalidFileNameChars(), c) >= 0 ? '_' : c);
            return sb.ToString().Trim().TrimEnd('.');
        }

        public static IEnumerable<Action> All()
        {
            var titles = new Dictionary<string, string[]>
            {
                { "sort", new[] { "Sort lines", "Sorts the lines of the clipboard text A to Z, ignoring case." } },
                { "unique", new[] { "Remove duplicate lines", "Keeps the first copy of every line on the clipboard." } },
                { "trim", new[] { "Trim each line", "Removes spaces and tabs at the start and end of every clipboard line." } },
                { "remove-blank", new[] { "Remove blank lines", "Deletes empty lines from the clipboard text." } },
                { "json-pretty", new[] { "Format JSON", "Rewrites JSON on the clipboard with indentation." } },
                { "json-minify", new[] { "Minify JSON", "Rewrites JSON on the clipboard on a single line." } },
                { "base64-encode", new[] { "Base64 encode", "Replaces the clipboard text with its Base64 form." } },
                { "base64-decode", new[] { "Base64 decode", "Replaces Base64 text on the clipboard with the text it hides." } },
                { "url-encode", new[] { "URL encode", "Makes the clipboard text safe to put in a web address." } },
                { "url-decode", new[] { "URL decode", "Turns %20 style codes on the clipboard back into characters." } },
                { "lower", new[] { "lower case", "Makes the clipboard text lower case." } },
                { "upper", new[] { "UPPER CASE", "Makes the clipboard text upper case." } },
                { "kebab", new[] { "kebab-case", "Turns each clipboard line into kebab-case." } },
                { "snake", new[] { "snake_case", "Turns each clipboard line into snake_case." } },
                { "camel", new[] { "camelCase", "Turns each clipboard line into camelCase." } },
                { "paths-fwd", new[] { "Slashes: \\ to /", "Replaces every backslash on the clipboard with a forward slash." } },
                { "quote-lines", new[] { "Quote lines: 'a', 'b'", "Wraps every clipboard line in quotes and joins them with commas." } },
                { "to-ps-array", new[] { "Lines to PowerShell array", "Turns the clipboard lines into @('a','b')." } },
                { "guid", new[] { "New GUID", "Puts a new random GUID on the clipboard." } },
                { "timestamp", new[] { "Timestamp (ISO)", "Puts the current date and time on the clipboard, like 2026-01-15T10:30:00." } }
            };
            foreach (string key in ClipOps.Keys)
            {
                string k = key;
                var t = titles[k];
                yield return Make("clip." + k, "Clipboard", t[0], t[1], Target.Background, false, Tier.Instant, false, (paths, c) =>
                {
                    string text = Clip.Text();
                    if (text.Trim().Length == 0 && k != "guid" && k != "timestamp") return new Result { Summary = "The clipboard does not contain text." };
                    try
                    {
                        string summary;
                        string result = ClipOps.Apply(k, text, out summary);
                        return new Result { ClipText = result, Summary = summary };
                    }
                    catch (FormatException) { return new Result { Summary = "The clipboard text is not valid for this change." }; }
                    catch (ArgumentException ex) { return new Result { Summary = ex.Message }; }
                    catch (InvalidOperationException ex) { return new Result { Summary = "The clipboard text is not valid: " + ex.Message }; }
                });
            }
            yield return Make("clip.count", "Clipboard", "Count lines, words, characters", "Shows how many lines, words and characters are on the clipboard.", Target.Background, false, Tier.Instant, false,
                (paths, c) => new Result { Summary = ClipOps.Count(Clip.Text()) });

            yield return Make("copy.file.clipboard", "Copy", "Copy as file for pasting", "Puts the selected files on the clipboard as files, so you can paste them into Teams, Slack, Outlook or Explorer.",
                Target.File | Target.Folder, true, Tier.Instant, false, (paths, c) =>
                {
                    Clip.SetFiles(paths);
                    return new Result { Summary = "Copied " + paths.Length + " item" + (paths.Length == 1 ? "" : "s") + " as files" };
                });
            yield return new Action
            {
                Id = "copy.image.clipboard", V4Menu = "Copy", V4Title = "Copy image to clipboard", Label = "Copy image to clipboard", Group = "Copy",
                Hint = "Puts the picture itself on the clipboard, with transparency kept, so you can paste it into documents and chats.",
                Targets = Target.File, Multi = false, Tier = Tier.Instant, Fast = true, AppliesTo = Modules.ImageQuery,
                PsNone = "This runs inside PS Tools, so there is no single PowerShell command to copy.",
                Run = (paths, c) =>
                {
                    int w, h;
                    Clip.SetImageFile(paths[0], out w, out h);
                    return new Result { Summary = "Copied image " + w + "x" + h };
                }
            };

            foreach (var r in RenameActions()) yield return r;
            foreach (var r in NewActions()) yield return r;

            yield return Make("organize.by-date", "Organize", "Sort into folders by month", "Moves the files in this folder into yyyy-MM folders by the date the picture was taken, or the last change date.",
                Target.Dirs, false, Tier.Confirm, true, (paths, c) =>
                {
                    var j = new Journal("Sort files by month in " + paths[0]);
                    int n = 0;
                    foreach (string f in Directory.GetFiles(paths[0]).OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
                    {
                        string month = TakenDate(f).ToString("yyyy-MM");
                        string dir = Path.Combine(paths[0], month);
                        j.EnsureDir(dir);
                        j.Move(f, Fs.FreeName(dir, Path.GetFileName(f)));
                        n++;
                    }
                    j.Save();
                    return new Result { Summary = n == 0 ? "No files to sort." : "Moved " + n + " file" + (n == 1 ? "" : "s") + " into month folders." };
                });
        }

        public static DateTime TakenDate(string file)
        {
            try
            {
                string ext = Path.GetExtension(file).ToLowerInvariant();
                if (ext == ".jpg" || ext == ".jpeg" || ext == ".tif" || ext == ".tiff")
                    using (var img = Image.FromFile(file))
                    {
                        foreach (var p in img.PropertyItems)
                            if (p.Id == 0x9003 && p.Value != null)
                            {
                                string s = Encoding.ASCII.GetString(p.Value).TrimEnd('\0').Trim();
                                DateTime d;
                                if (DateTime.TryParseExact(s, "yyyy:MM:dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out d)) return d;
                            }
                    }
            }
            catch { }
            return File.GetLastWriteTime(file);
        }

        static Result RenameWith(string[] paths, string title, Func<string, bool, string> newName)
        {
            var j = new Journal(title);
            int n = 0;
            foreach (string p in paths)
            {
                bool dir = Directory.Exists(p);
                string parent = Path.GetDirectoryName(p.TrimEnd('\\', '/'));
                string name = Path.GetFileName(p.TrimEnd('\\', '/'));
                string next = newName(name, dir);
                if (string.IsNullOrEmpty(next) || next == name) continue;
                string target = Path.Combine(parent, next);
                if (string.Equals(name, next, StringComparison.OrdinalIgnoreCase))
                {
                    string temp = Path.Combine(parent, Guid.NewGuid().ToString("N") + ".tmp");
                    j.Move(p, temp);
                    j.Move(temp, target);
                }
                else
                {
                    if (File.Exists(target) || Directory.Exists(target)) target = Fs.FreeName(parent, next);
                    j.Move(p, target);
                }
                n++;
            }
            j.Save();
            return new Result { Summary = n == 0 ? "Nothing needed renaming." : "Renamed " + n + " item" + (n == 1 ? "" : "s") };
        }

        static string StemExt(string name, bool dir, Func<string, string> f)
        {
            if (dir) return f(name);
            string ext = Path.GetExtension(name);
            return f(Path.GetFileNameWithoutExtension(name)) + ext;
        }

        static int NaturalCompare(string a, string b)
        {
            var ra = Regex.Split(a.ToLowerInvariant(), "(\\d+)");
            var rb = Regex.Split(b.ToLowerInvariant(), "(\\d+)");
            for (int i = 0; i < Math.Min(ra.Length, rb.Length); i++)
            {
                if (ra[i] == rb[i]) continue;
                long x, y;
                if (long.TryParse(ra[i], out x) && long.TryParse(rb[i], out y)) return x.CompareTo(y);
                return string.CompareOrdinal(ra[i], rb[i]);
            }
            return ra.Length.CompareTo(rb.Length);
        }

        static IEnumerable<Action> RenameActions()
        {
            var defs = new[]
            {
                new { Key = "lower", Title = "lower case names", Hint = "Renames the selected items to lower case.", F = (Func<string, string>)(s => s.ToLowerInvariant()) },
                new { Key = "kebab", Title = "kebab-case names", Hint = "Renames the selected items to kebab-case, for example my-file-name.", F = (Func<string, string>)ClipOps.Kebab },
                new { Key = "snake", Title = "snake_case names", Hint = "Renames the selected items to snake_case, for example my_file_name.", F = (Func<string, string>)ClipOps.Snake },
                new { Key = "title", Title = "Title Case Names", Hint = "Renames the selected items so every word starts with a capital letter.", F = (Func<string, string>)ClipOps.TitleCase }
            };
            foreach (var d in defs)
            {
                var def = d;
                yield return Make("rename." + def.Key, "Rename", def.Title, def.Hint, Target.File | Target.Folder, true, Tier.Confirm, true,
                    (paths, c) => RenameWith(paths, "Rename to " + def.Title, (name, dir) => StemExt(name, dir, def.F)));
            }
            yield return Make("rename.date-prefix", "Rename", "Prefix with date taken/modified", "Puts the date in front of each file name, like 2026-01-15 name.jpg. Pictures use the date they were taken.", Target.File, true, Tier.Confirm, true,
                (paths, c) => RenameWith(paths, "Add date prefix", (name, dir) =>
                {
                    if (Regex.IsMatch(name, "^\\d{4}-\\d{2}-\\d{2}[ _-]")) return name;
                    string full = paths.FirstOrDefault(x => Path.GetFileName(x) == name) ?? name;
                    return TakenDate(full).ToString("yyyy-MM-dd") + " " + name;
                }));
            yield return Make("rename.number", "Rename", "Number sequentially", "Renames the selected files name_001.ext, name_002.ext... in the order Explorer sorts them. The name comes from the first file.", Target.File, true, Tier.Confirm, true,
                (paths, c) =>
                {
                    var ordered = paths.OrderBy(Path.GetFileName, Comparer<string>.Create(NaturalCompare)).ToList();
                    string stem = Regex.Replace(Path.GetFileNameWithoutExtension(ordered[0]), "[\\s_\\-\\.]*\\d+$", "");
                    if (stem.Length == 0) stem = "file";
                    int width = Math.Max(3, ordered.Count.ToString().Length);
                    var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    for (int i = 0; i < ordered.Count; i++) map[ordered[i]] = stem + "_" + (i + 1).ToString().PadLeft(width, '0') + Path.GetExtension(ordered[i]);
                    return RenameWith(ordered.ToArray(), "Number files", (name, dir) => map.First(kv => Path.GetFileName(kv.Key) == name).Value);
                });
            yield return Make("rename.ext-lower", "Rename", "Lower-case extensions", "Makes the file extensions lower case, so photo.JPG becomes photo.jpg. Works on the selected files or on every file in a selected folder.", Target.File | Target.Folder, true, Tier.Confirm, true,
                (paths, c) =>
                {
                    var files = new List<string>();
                    foreach (string p in paths)
                    {
                        if (Directory.Exists(p)) files.AddRange(Directory.GetFiles(p, "*", SearchOption.AllDirectories));
                        else files.Add(p);
                    }
                    return RenameWith(files.Where(f => Path.GetExtension(f) != Path.GetExtension(f).ToLowerInvariant()).ToArray(), "Lower-case extensions",
                        (name, dir) => Path.GetFileNameWithoutExtension(name) + Path.GetExtension(name).ToLowerInvariant());
                });
        }

        static IEnumerable<Action> NewActions()
        {
            yield return Make("new.file", "Paste & New", "New file…", "Creates an empty file with the name you type. Without an extension it becomes .txt.", Target.Dirs, false, Tier.Instant, true, (paths, c) =>
            {
                string name = Ask("Name of the new file (for example notes.md)", "New file.txt").Trim();
                if (name.Length == 0) return new Result { Summary = "No name was given." };
                name = SafeName(name);
                if (Path.GetExtension(name).Length == 0) name += ".txt";
                string target = Fs.FreeName(paths[0], name);
                File.WriteAllBytes(target, new byte[0]);
                var j = new Journal("New file in " + paths[0]);
                j.Link(target);
                j.Save();
                return new Result { Summary = "Created " + Path.GetFileName(target) };
            });
            yield return Make("new.paste-shortcut", "Paste & New", "Paste as shortcut", "Creates a shortcut here to each file or folder you copied in Explorer.", Target.Dirs, false, Tier.Instant, true, (paths, c) =>
                PasteLinks(paths[0], "shortcut", (target, link) => CreateShortcut(target, link)));
            yield return Make("new.paste-hardlink", "Paste & New", "Paste as hard link", "Creates a hard link here to each file you copied in Explorer. Both must be on the same drive.", Target.Dirs, false, Tier.Instant, true, (paths, c) =>
                PasteLinks(paths[0], "hard link", (target, link) =>
                {
                    if (Directory.Exists(target)) throw new InvalidOperationException("Hard links only work for files.");
                    if (!string.Equals(Path.GetPathRoot(target), Path.GetPathRoot(link), StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("A hard link must be on the same drive as the file.");
                    if (!CreateHardLink(link, target, IntPtr.Zero)) throw new InvalidOperationException("Windows refused: error " + Marshal.GetLastWin32Error());
                }));
            yield return Make("new.paste-symlink", "Paste & New", "Paste as symbolic link", "Creates a symbolic link here to each file or folder you copied. Needs Developer Mode or administrator rights.", Target.Dirs, false, Tier.Instant, true, (paths, c) =>
                PasteLinks(paths[0], "symbolic link", (target, link) =>
                {
                    int flags = (Directory.Exists(target) ? 1 : 0) | 2;
                    if (!CreateSymbolicLink(link, target, flags))
                    {
                        int err = Marshal.GetLastWin32Error();
                        throw new InvalidOperationException(err == 1314 ? "Windows needs Developer Mode (Settings, For developers) or administrator rights to create symbolic links." : "Windows refused: error " + err);
                    }
                }));
            yield return Make("new.paste-junction", "Paste & New", "Paste as junction", "Creates a junction here to each folder you copied. No special rights are needed.", Target.Dirs, false, Tier.Instant, true, (paths, c) =>
                PasteLinks(paths[0], "junction", (target, link) =>
                {
                    if (!Directory.Exists(target)) throw new InvalidOperationException("Junctions only work for folders.");
                    var psi = new ProcessStartInfo("cmd.exe", "/d /c mklink /J \"" + link + "\" \"" + target + "\"") { CreateNoWindow = true, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
                    using (var p = Process.Start(psi))
                    {
                        string err = p.StandardError.ReadToEnd();
                        p.WaitForExit();
                        if (p.ExitCode != 0) throw new InvalidOperationException(err.Trim());
                    }
                }));
            yield return Make("new.from-clip-tree", "Paste & New", "Folders from clipboard tree", "Turns a folder tree copied as text (box-drawing, plain ASCII or an indented list) into real folders and empty files here. You see the list first.", Target.Dirs, false, Tier.Instant, true, (paths, c) =>
                FromTree(paths[0], Clip.Text()));
        }

        static void CreateShortcut(string target, string link)
        {
            Type t = Type.GetTypeFromProgID("WScript.Shell");
            if (t == null) throw new InvalidOperationException("Windows Script Host is not available.");
            object sh = Activator.CreateInstance(t);
            object lnk = t.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, sh, new object[] { link });
            lnk.GetType().InvokeMember("TargetPath", BindingFlags.SetProperty, null, lnk, new object[] { target });
            lnk.GetType().InvokeMember("WorkingDirectory", BindingFlags.SetProperty, null, lnk, new object[] { Directory.Exists(target) ? target : Path.GetDirectoryName(target) });
            lnk.GetType().InvokeMember("Save", BindingFlags.InvokeMethod, null, lnk, null);
        }

        static Result PasteLinks(string dir, string kind, System.Action<string, string> create)
        {
            var files = Clip.Files();
            if (files.Count == 0) return new Result { Summary = "Copy a file or folder in Explorer first." };
            var j = new Journal("Paste " + kind + " into " + dir);
            int n = 0;
            var problems = new List<string>();
            foreach (string f in files)
            {
                string name = Path.GetFileName(f.TrimEnd('\\', '/'));
                string link = Fs.FreeName(dir, name + (kind == "shortcut" ? ".lnk" : ""));
                try { create(f, link); j.Link(link); n++; }
                catch (Exception ex) { problems.Add(name + ": " + ex.Message); }
            }
            j.Save();
            if (n == 0) return new Result { Summary = problems.Count > 0 ? problems[0] : "Nothing was created." };
            return new Result { Summary = "Created " + n + " " + kind + (n == 1 ? "" : "s") + (problems.Count > 0 ? ", " + problems.Count + " failed: " + problems[0] : "") };
        }

        public class TreeItem
        {
            public string Name;
            public int Depth;
            public bool Folder;
            public string Path;
        }

        public static List<TreeItem> ParseTree(string text)
        {
            var raw = new List<KeyValuePair<int, string>>();
            const string glyphs = "\u2502\u251C\u2514\u2500\u252C\u250C\u2510\u2518\u2524\u2534\u253C+\\|-*\u2022 \t`'";
            var lines = ClipOps.Lines(text).Where(l => l.Trim().Length > 0).ToList();
            var cols = new List<int>();
            foreach (string l in lines)
            {
                int i = 0;
                while (i < l.Length && glyphs.IndexOf(l[i]) >= 0) i++;
                raw.Add(new KeyValuePair<int, string>(i, l.Substring(i)));
                if (i > 0) cols.Add(i);
            }
            int unit = cols.Count == 0 ? 4 : cols.Min();
            if (unit < 2) unit = 2;
            var items = new List<TreeItem>();
            foreach (var kv in raw)
            {
                string name = kv.Value;
                int hash = name.IndexOf("  #", StringComparison.Ordinal);
                if (hash > 0) name = name.Substring(0, hash);
                name = name.Trim();
                bool folder = name.EndsWith("/") || name.EndsWith("\\");
                name = name.TrimEnd('/', '\\').Trim();
                if (name.Length == 0) continue;
                items.Add(new TreeItem { Name = SafeName(name), Depth = (kv.Key + unit - 1) / unit, Folder = folder });
            }
            for (int i = 0; i < items.Count; i++)
                if (i + 1 < items.Count && items[i + 1].Depth > items[i].Depth) items[i].Folder = true;
            if (items.Count > 1 && items[0].Depth == 0 && items.Skip(1).All(x => x.Depth >= 1)) items[0].Folder = true;
            var stack = new List<string>();
            foreach (var it in items)
            {
                while (stack.Count > it.Depth) stack.RemoveAt(stack.Count - 1);
                it.Path = string.Join("\\", stack.Concat(new[] { it.Name }).ToArray());
                if (it.Folder) { stack.Add(it.Name); }
            }
            return items;
        }

        static Result FromTree(string dir, string text)
        {
            var items = ParseTree(text);
            if (items.Count == 0) return new Result { Summary = "The clipboard does not contain a tree." };
            int folders = items.Count(x => x.Folder), files = items.Count - folders;
            var preview = string.Join("\n", items.Take(25).Select(x => new string(' ', x.Depth * 2) + x.Name + (x.Folder ? "/" : "")).ToArray());
            if (!Program.Ask("Create " + folders + " folder" + (folders == 1 ? "" : "s") + " and " + files + " empty file" + (files == 1 ? "" : "s") + " in\n" + dir + " ?\n\n" + preview + (items.Count > 25 ? "\n..." : ""))) return new Result { Summary = "Cancelled." };
            var j = new Journal("Create tree in " + dir);
            foreach (var it in items)
            {
                string full = Path.Combine(dir, it.Path);
                if (it.Folder) j.EnsureDir(full);
                else
                {
                    string parent = Path.GetDirectoryName(full);
                    if (!Directory.Exists(parent)) j.EnsureDir(parent);
                    if (!File.Exists(full)) { File.WriteAllBytes(full, new byte[0]); j.Link(full); }
                }
            }
            j.Save();
            return new Result { Summary = "Created " + folders + " folders and " + files + " files." };
        }
    }
}
