using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;

namespace PSTools
{
    class Suggestion
    {
        public string ActionId, Reason;
        public Action Action;
    }

    static class Suggest
    {
        public const int MaxItems = 5;
        public const int MaxEntries = 3000;
        public const int MaxMillis = 400;

        public static readonly string[] RuleIds =
        {
            "paste-image", "download-clip", "paste-text", "img-resize-1600", "unzip-here", "git-status",
            "clean-build", "serve-here", "open-vscode", "space-map", "organize-type", "duplicates"
        };

        static readonly string[] PictureExts = { ".png", ".jpg", ".jpeg", ".bmp", ".gif", ".tif", ".tiff" };
        static readonly string[] BuildNames = { "node_modules", "bin", "obj" };

        public class Scan
        {
            public int Entries, Pictures, Zips, TopItems, GitRepos;
            public bool BuildFolders, ProjectFiles, Truncated;
            public string ProjectFile = "";
            public Dictionary<long, int> Sizes = new Dictionary<long, int>();
            public int DuplicateCandidates
            {
                get { int n = 0; foreach (var kv in Sizes) if (kv.Value > 1) n += kv.Value; return n; }
            }
        }

        public enum ClipKind { None, Image, Link, Text }

        public static List<Suggestion> For(string folder)
        {
            return Combine(ReadClipboard(), Directory.Exists(folder) ? ScanFolder(folder) : new Scan());
        }

        public static Scan ScanFolder(string folder)
        {
            var scan = new Scan();
            var clock = Stopwatch.StartNew();
            string[] top;
            try { top = Directory.GetFileSystemEntries(folder); }
            catch { return scan; }
            scan.TopItems = top.Length;
            var subdirs = new List<string>();
            foreach (var path in top)
            {
                if (Over(scan, clock)) break;
                scan.Entries++;
                FileAttributes attr;
                try { attr = File.GetAttributes(path); } catch { continue; }
                string name = Path.GetFileName(path);
                bool link = (attr & FileAttributes.ReparsePoint) != 0;
                if ((attr & FileAttributes.Directory) != 0)
                {
                    if (link) continue;
                    if (BuildNames.Contains(name, StringComparer.OrdinalIgnoreCase)) scan.BuildFolders = true;
                    subdirs.Add(path);
                }
                else Count(scan, path, name, true);
            }
            foreach (var dir in subdirs)
            {
                if (Over(scan, clock)) break;
                string[] inner;
                try { inner = Directory.GetFileSystemEntries(dir); } catch { continue; }
                bool git = false;
                foreach (var path in inner)
                {
                    if (Over(scan, clock)) break;
                    scan.Entries++;
                    FileAttributes attr;
                    try { attr = File.GetAttributes(path); } catch { continue; }
                    string name = Path.GetFileName(path);
                    if ((attr & FileAttributes.ReparsePoint) != 0 && (attr & FileAttributes.Directory) != 0) continue;
                    if ((attr & FileAttributes.Directory) != 0)
                    {
                        if (name.Equals(".git", StringComparison.OrdinalIgnoreCase)) git = true;
                        if (BuildNames.Contains(name, StringComparer.OrdinalIgnoreCase)) scan.BuildFolders = true;
                    }
                    else
                    {
                        if (name.Equals(".git", StringComparison.OrdinalIgnoreCase)) git = true;
                        Count(scan, path, name, false);
                    }
                }
                if (git) scan.GitRepos++;
            }
            return scan;
        }

        static bool Over(Scan scan, Stopwatch clock)
        {
            if (scan.Entries >= MaxEntries || clock.ElapsedMilliseconds >= MaxMillis) { scan.Truncated = true; return true; }
            return false;
        }

        static void Count(Scan scan, string path, string name, bool topLevel)
        {
            string ext = Path.GetExtension(name).ToLowerInvariant();
            if (PictureExts.Contains(ext)) scan.Pictures++;
            if (ext == ".zip") scan.Zips++;
            if (topLevel)
            {
                string lower = name.ToLowerInvariant();
                if (lower == "package.json" || lower == "index.html" || ext == ".sln" || ext == ".csproj")
                {
                    if (!scan.ProjectFiles) scan.ProjectFile = name;
                    scan.ProjectFiles = true;
                }
            }
            try
            {
                long len = new FileInfo(path).Length;
                if (len > 0)
                {
                    int n;
                    scan.Sizes.TryGetValue(len, out n);
                    scan.Sizes[len] = n + 1;
                }
            }
            catch { }
        }

        public static ClipKind ReadClipboard()
        {
            ClipKind result = ClipKind.None;
            var t = new Thread(() =>
            {
                try
                {
                    if (Clipboard.ContainsImage()) { result = ClipKind.Image; return; }
                    if (!Clipboard.ContainsText()) return;
                    string text = (Clipboard.GetText() ?? "").Trim();
                    if (text.Length == 0) return;
                    Uri uri;
                    bool link = text.IndexOfAny(new[] { ' ', '\t', '\r', '\n' }) < 0 && Uri.TryCreate(text, UriKind.Absolute, out uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
                    result = link ? ClipKind.Link : ClipKind.Text;
                }
                catch { }
            });
            t.SetApartmentState(ApartmentState.STA);
            t.IsBackground = true;
            t.Start();
            if (!t.Join(250)) return ClipKind.None;
            return result;
        }

        public static List<Suggestion> Combine(ClipKind clip, Scan scan)
        {
            var raw = new List<string[]>();
            if (clip == ClipKind.Image) raw.Add(new[] { "paste-image", "Image on the clipboard" });
            else if (clip == ClipKind.Link) raw.Add(new[] { "download-clip", "Link on the clipboard" });
            else if (clip == ClipKind.Text) raw.Add(new[] { "paste-text", "Text on the clipboard" });
            if (scan.Pictures >= 5) raw.Add(new[] { "img-resize-1600", scan.Pictures + " pictures here" });
            if (scan.Zips > 0) raw.Add(new[] { "unzip-here", scan.Zips + (scan.Zips == 1 ? " zip file here" : " zip files here") });
            if (scan.GitRepos >= 2) raw.Add(new[] { "git-status", scan.GitRepos + " git repositories here" });
            if (scan.BuildFolders) raw.Add(new[] { "clean-build", "Build folders here" });
            if (scan.ProjectFiles)
            {
                raw.Add(new[] { "serve-here", scan.ProjectFile + " here" });
                raw.Add(new[] { "open-vscode", scan.ProjectFile + " here" });
            }
            if (scan.TopItems > 300)
            {
                raw.Add(new[] { "space-map", scan.TopItems.ToString("N0") + " items here" });
                raw.Add(new[] { "organize-type", scan.TopItems.ToString("N0") + " items here" });
            }
            if (scan.DuplicateCandidates >= 10) raw.Add(new[] { "duplicates", scan.DuplicateCandidates + " files share a size" });

            var list = new List<Suggestion>();
            foreach (var r in raw)
            {
                var a = Actions.Find(r[0]);
                if (a == null || !a.Usable || list.Any(x => x.ActionId == r[0])) continue;
                list.Add(new Suggestion { ActionId = r[0], Reason = r[1], Action = a });
                if (list.Count >= MaxItems) break;
            }
            return list;
        }
    }
}
