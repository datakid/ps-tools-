using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Web.Script.Serialization;

namespace PSTools
{
    static partial class Actions
    {
        static string NameOf(string p)
        {
            string f = Fs.Full(p);
            string n = Path.GetFileName(f);
            return n.Length == 0 ? f : n;
        }

        static string NameNoExt(string p)
        {
            return Directory.Exists(p) ? NameOf(p) : Path.GetFileNameWithoutExtension(NameOf(p));
        }

        static string ParentOf(string p)
        {
            string f = Fs.Full(p);
            return Path.GetDirectoryName(f) ?? f;
        }

        static Action Cp(string id, string label, string hint, Func<string, string> map, bool fav)
        {
            return new Action
            {
                Id = id, Group = "Copy", Label = label, Hint = hint, Targets = Target.All, Multi = true, Fast = true, DefaultFav = fav,
                Run = (paths, c) =>
                {
                    string text = string.Join("\r\n", paths.Select(map));
                    return new Result { ClipText = text, Summary = paths.Length == 1 ? "Copied: " + Short(text) : "Copied " + paths.Length + " items" };
                }
            };
        }

        static string Short(string s)
        {
            s = s.Replace("\r", " ").Replace("\n", " ");
            return s.Length > 90 ? s.Substring(0, 87) + "..." : s;
        }

        static IEnumerable<Action> CopyModule()
        {
            yield return Cp("copy-name", "Copy name", "The file or folder name, one per line for several items.", NameOf, true);
            yield return Cp("copy-name-noext", "Copy name without extension", "The name without its extension (folders keep their full name).", NameNoExt, false);
            yield return Cp("copy-path-fwd", "Copy path with / slashes", "Full path using forward slashes, handy for code and config files.", p => Fs.Full(p).Replace('\\', '/'), false);
            yield return Cp("copy-path-quoted", "Copy path in quotes", "Full path wrapped in double quotes.", p => "\"" + Fs.Full(p) + "\"", false);
            yield return Cp("copy-parent", "Copy parent folder path", "The folder that contains the item.", ParentOf, false);
            yield return Cp("copy-parent-name", "Copy parent folder name", "Only the name of the containing folder.", p => NameOf(ParentOf(p)), false);
            yield return Cp("copy-url", "Copy as file:// URL", "A file:/// address for browsers and links.", Fs.ToUrl, false);
            yield return Cp("copy-wsl", "Copy WSL path", "The same location as seen from WSL: /mnt/c/...", Fs.ToWsl, false);
            yield return Cp("copy-unc", "Copy network (UNC) path", "Resolves a mapped drive letter to its \\\\server\\share path. Local drives stay unchanged.", Fs.ToUnc, false);
            yield return Cp("copy-md-link", "Copy as Markdown link", "[name](file:///path) for notes and docs.", p => "[" + NameOf(p) + "](" + Fs.ToUrl(p) + ")", false);
            yield return Cp("copy-pslocation", "Copy Set-Location command", "A PowerShell line that jumps to the folder (or to the file's folder).",
                p => "Set-Location -LiteralPath '" + (Directory.Exists(p) ? Fs.Full(p) : ParentOf(p)).Replace("'", "''") + "'", false);

            yield return new Action
            {
                Id = "copy-json", Group = "Copy", Label = "Copy paths as JSON array", Targets = Target.All, Multi = true, Fast = true,
                Hint = "All selected full paths as a JSON array.",
                Run = (paths, c) => new Result { ClipText = new JavaScriptSerializer().Serialize(paths.Select(Fs.Full).ToArray()), Summary = "Copied " + paths.Length + " paths as JSON" }
            };
            yield return new Action
            {
                Id = "copy-csv", Group = "Copy", Label = "Copy names comma-separated", Targets = Target.All, Multi = true, Fast = true,
                Hint = "Selected names on one line separated by commas.",
                Run = (paths, c) => new Result { ClipText = string.Join(", ", paths.Select(NameOf)), Summary = "Copied " + paths.Length + " names" }
            };

            yield return new Action
            {
                Id = "copy-size", Group = "Copy", Label = "Copy size", Targets = Target.All, Multi = true,
                Hint = "Size and file count (folders are measured recursively).",
                Run = (paths, c) =>
                {
                    var lines = new List<string>();
                    long all = 0;
                    foreach (var p in paths)
                    {
                        c.Report(NameOf(p));
                        long bytes = 0; int count = 0;
                        if (Directory.Exists(p))
                            foreach (var f in Fs.Files(p, true)) { c.Check(); try { bytes += new FileInfo(f).Length; count++; } catch { } }
                        else { try { bytes = new FileInfo(p).Length; count = 1; } catch { } }
                        all += bytes;
                        lines.Add(NameOf(p) + ": " + Fs.Size(bytes) + " (" + bytes.ToString("N0") + " bytes)" + (Directory.Exists(p) ? ", " + count.ToString("N0") + " files" : ""));
                    }
                    return new Result { ClipText = string.Join("\r\n", lines), Summary = paths.Length == 1 ? lines[0] : paths.Length + " items, " + Fs.Size(all) };
                }
            };

            yield return Hash("copy-sha256", "Copy SHA-256", "SHA-256 of the file.", () => SHA256.Create());
            yield return Hash("copy-sha1", "Copy SHA-1", "SHA-1 of the file.", () => SHA1.Create());
            yield return Hash("copy-md5", "Copy MD5", "MD5 of the file.", () => MD5.Create());

            yield return new Action
            {
                Id = "verify-hash", Group = "Copy", Label = "Verify hash from clipboard", Targets = Target.File,
                Hint = "Copy a published checksum first (MD5, SHA-1, SHA-256 or SHA-512), then run this on the downloaded file.",
                Run = (paths, c) =>
                {
                    string want = new string((c.ClipboardText ?? "").Where(Uri.IsHexDigit).ToArray()).ToLowerInvariant();
                    string name;
                    Func<HashAlgorithm> make;
                    switch (want.Length)
                    {
                        case 32: name = "MD5"; make = () => MD5.Create(); break;
                        case 40: name = "SHA-1"; make = () => SHA1.Create(); break;
                        case 64: name = "SHA-256"; make = () => SHA256.Create(); break;
                        case 128: name = "SHA-512"; make = () => SHA512.Create(); break;
                        default: return new Result { Summary = "The clipboard does not contain a checksum (expected 32, 40, 64 or 128 hex characters)." };
                    }
                    string got = HashFile(paths[0], make, c);
                    return new Result { Summary = got == want ? "MATCH (" + name + ")\n\n" + NameOf(paths[0]) + " is identical to the published checksum." : "DIFFERENT (" + name + ")\n\nExpected: " + want + "\nActual:   " + got };
                }
            };

            yield return new Action
            {
                Id = "copy-contents", Group = "Copy", Label = "Copy file contents", Targets = Target.File, Multi = true, Fast = true,
                Hint = "Text files only, up to 1 MB each. Several files are separated by a header line.",
                Run = (paths, c) =>
                {
                    var sb = new StringBuilder();
                    foreach (var p in paths)
                    {
                        var fi = new FileInfo(p);
                        if (fi.Length > 1024 * 1024) return new Result { Summary = NameOf(p) + " is larger than 1 MB." };
                        if (!Fs.LooksText(p)) return new Result { Summary = NameOf(p) + " is not a text file." };
                        if (paths.Length > 1) sb.Append("==== ").Append(NameOf(p)).Append(" ====\r\n");
                        sb.Append(File.ReadAllText(p)).Append(paths.Length > 1 ? "\r\n" : "");
                    }
                    return new Result { ClipText = sb.ToString(), Summary = "Copied contents of " + paths.Length + (paths.Length == 1 ? " file" : " files") };
                }
            };

            yield return new Action
            {
                Id = "copy-datauri", Group = "Copy", Label = "Copy as data URI", Targets = Target.File, Fast = true,
                Hint = "The file as a base64 data: URI for embedding in HTML or CSS (up to 2 MB).",
                Run = (paths, c) =>
                {
                    var fi = new FileInfo(paths[0]);
                    if (fi.Length > 2 * 1024 * 1024) return new Result { Summary = "Files over 2 MB are too large for a data URI." };
                    string mime = Fs.Mime(fi.Extension).Split(';')[0];
                    return new Result { ClipText = "data:" + mime + ";base64," + Convert.ToBase64String(File.ReadAllBytes(paths[0])), Summary = "Copied data URI (" + Fs.Size(fi.Length) + ")" };
                }
            };

            yield return new Action
            {
                Id = "copy-names", Group = "Copy", Label = "Copy names of items inside", Targets = Target.Dirs, Fast = true,
                Hint = "Names of everything directly inside this folder, one per line.",
                Run = (paths, c) =>
                {
                    string[] names = Directory.GetFileSystemEntries(paths[0]).Select(Path.GetFileName).OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray();
                    return names.Length == 0 ? new Result { Summary = "Folder is empty." } : new Result { ClipText = string.Join("\r\n", names), Summary = "Copied " + names.Length + " names" };
                }
            };

            yield return StructAction("copy-dirs-tree", "Copy folder tree (folders only)", "An organized tree of every subfolder.", new StructureOptions { Format = "Tree" }, true);
            yield return StructAction("copy-dirs-paths", "Copy all subfolder paths", "The full path of every subfolder, one per line.", new StructureOptions { Format = "Full paths" }, false);
            yield return StructAction("copy-dirs-rel", "Copy all subfolder paths (relative)", "Every subfolder relative to this folder, one per line.", new StructureOptions { Format = "Relative paths" }, false);
            yield return StructAction("copy-tree", "Copy tree with files", "Folders and files as an indented tree.", new StructureOptions { Format = "Tree", Files = true }, false);

            yield return new Action
            {
                Id = "structure", Group = "Copy", Label = "Folder structure...", Targets = Target.Dirs,
                Hint = "Pick the format (tree, markdown, indented, full or relative paths), depth, files, counts and hidden folders, preview it, then copy or save.",
                Window = p => new StructureForm(p[0])
            };
        }

        static Action StructAction(string id, string label, string hint, StructureOptions o, bool fav)
        {
            return new Action
            {
                Id = id, Group = "Copy", Label = label, Hint = hint, Targets = Target.Dirs, DefaultFav = fav,
                Run = (paths, c) =>
                {
                    c.Report("Reading folders...");
                    string text = Structure.Build(paths[0], o, c);
                    int lines = text.Split('\n').Length - 1;
                    return new Result { ClipText = text, Summary = "Copied " + lines.ToString("N0") + " lines" };
                }
            };
        }

        static string HashFile(string path, Func<HashAlgorithm> make, Ctx c)
        {
            using (var alg = make())
            using (var s = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 1 << 16))
            {
                var buf = new byte[1 << 16];
                int n;
                long done = 0, total = Math.Max(1, s.Length);
                while ((n = s.Read(buf, 0, buf.Length)) > 0)
                {
                    c.Check();
                    alg.TransformBlock(buf, 0, n, null, 0);
                    done += n;
                    c.Report(NameOf(path) + "  " + (done * 100 / total) + "%");
                }
                alg.TransformFinalBlock(buf, 0, 0);
                return BitConverter.ToString(alg.Hash).Replace("-", "").ToLowerInvariant();
            }
        }

        static Action Hash(string id, string label, string hint, Func<HashAlgorithm> make)
        {
            return new Action
            {
                Id = id, Group = "Copy", Label = label, Hint = hint, Targets = Target.File, Multi = true,
                Run = (paths, c) =>
                {
                    var lines = new List<string>();
                    foreach (var p in paths) lines.Add(paths.Length == 1 ? HashFile(p, make, c) : HashFile(p, make, c) + "  " + NameOf(p));
                    return new Result { ClipText = string.Join("\r\n", lines), Summary = paths.Length == 1 ? "Copied: " + lines[0] : "Copied " + lines.Count + " hashes" };
                }
            };
        }
    }
}
