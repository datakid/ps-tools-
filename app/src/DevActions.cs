using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace PSTools
{
    static partial class Actions
    {
        static string DirOf(string p) { return Directory.Exists(p) ? Fs.Full(p) : ParentOf(p); }

        static string CodePath()
        {
            string[] c =
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Programs\Microsoft VS Code\Code.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), @"Microsoft VS Code\Code.exe")
            };
            return c.FirstOrDefault(File.Exists);
        }

        static bool? gitFound;
        static bool HasGit { get { if (gitFound == null) gitFound = Fs.Which("git") != null; return gitFound.Value; } }

        static IEnumerable<Action> DevModule()
        {
            yield return new Action
            {
                Id = "open-ps", Group = "Dev", Label = "Open PowerShell here", Targets = Target.All, Fast = true,
                Hint = "A PowerShell window in this folder (or in the folder of the selected file).",
                Run = (paths, c) => { Launch.PowerShell(DirOf(paths[0]), null, false); return new Result { Summary = "Opened PowerShell" }; }
            };
            yield return new Action
            {
                Id = "open-cmd", Group = "Dev", Label = "Open Command Prompt here", Targets = Target.All, Fast = true,
                Hint = "A cmd window in this folder.",
                Run = (paths, c) =>
                {
                    Process.Start(new ProcessStartInfo("cmd.exe") { WorkingDirectory = DirOf(paths[0]), UseShellExecute = true });
                    return new Result { Summary = "Opened Command Prompt" };
                }
            };
            yield return new Action
            {
                Id = "open-ps-admin", Group = "Dev", Label = "Open PowerShell here as administrator", Targets = Target.All, Fast = true, Tier = Tier.Elevated,
                Hint = "Elevated PowerShell in this folder. Windows asks for confirmation (UAC).",
                Run = (paths, c) => { Launch.PowerShell(DirOf(paths[0]), null, true); return new Result { Summary = "Opened elevated PowerShell" }; }
            };
            yield return new Action
            {
                Id = "open-wsl", Group = "Dev", Label = "Open WSL here", Targets = Target.Dirs, Fast = true,
                Available = () => File.Exists(Path.Combine(Environment.SystemDirectory, "wsl.exe")),
                Hint = "A WSL shell starting in this folder.",
                Run = (paths, c) =>
                {
                    Process.Start(new ProcessStartInfo("wsl.exe", "--cd \"" + DirOf(paths[0]) + "\"") { UseShellExecute = true });
                    return new Result { Summary = "Opened WSL" };
                }
            };
            yield return new Action
            {
                Id = "open-vscode", Group = "Dev", Label = "Open in VS Code", Targets = Target.All, Fast = true, Multi = true,
                Available = () => CodePath() != null,
                Hint = "Opens the folder or files in Visual Studio Code (found at install time).",
                Run = (paths, c) =>
                {
                    string exe = CodePath();
                    if (exe == null) return new Result { Summary = "VS Code was not found." };
                    Process.Start(new ProcessStartInfo(exe, string.Join(" ", paths.Select(p => "\"" + Fs.Full(p) + "\""))) { UseShellExecute = false });
                    return new Result { Summary = "Opened in VS Code" };
                }
            };
            yield return new Action
            {
                Id = "open-notepad", Group = "Dev", Label = "Open as text in Notepad", Targets = Target.File, Fast = true, Multi = true,
                Hint = "Opens any file in Notepad, whatever its extension.",
                Run = (paths, c) =>
                {
                    foreach (var p in paths.Take(10)) Process.Start("notepad.exe", "\"" + p + "\"");
                    return new Result { Summary = "Opened in Notepad" };
                }
            };

            yield return new Action
            {
                Id = "serve-here", Group = "Dev", Label = "Serve this folder on localhost...", Targets = Target.Dirs,
                Hint = "Starts a small web server on http://localhost for this folder (HTML apps, WASM and modules need http, not file://). Stops when you close its window.",
                Window = p => new ServeForm(p[0])
            };

            yield return new Action
            {
                Id = "clean-build", Group = "Dev", Label = "Find removable build folders", Targets = Target.Dirs,
                Hint = "node_modules, bin and obj next to a project file, __pycache__, .vs and Rust target folders. Everything is pre-ticked; review, then delete permanently or recycle.",
                Run = (paths, c) => BuildJunk(paths[0], c)
            };

            yield return new Action
            {
                Id = "git-status", Group = "Dev", Label = "Git status of all repos below", Targets = Target.Dirs, Available = () => HasGit,
                Hint = "Finds every git repository below this folder and shows branch, uncommitted changes, ahead and behind.",
                Run = (paths, c) => GitScan(paths[0], c, false)
            };
            yield return new Action
            {
                Id = "git-pull", Group = "Dev", Label = "Git pull (fast-forward) all repos below", Targets = Target.Dirs, Tier = Tier.Confirm, Available = () => HasGit,
                Hint = "Runs git pull --ff-only in every repository below this folder. Never merges or rewrites anything; repos that cannot fast-forward are reported.",
                Confirm = "Run git pull --ff-only in every repository below\n{0} ?",
                Run = (paths, c) => GitScan(paths[0], c, true)
            };
        }

        static Result BuildJunk(string root, Ctx c)
        {
            var found = new List<string[]>();
            long total = 0;
            var stack = new Stack<string>();
            stack.Push(Fs.Full(root));
            while (stack.Count > 0)
            {
                string dir = stack.Pop();
                c.Check();
                c.Report(dir);
                string[] subs;
                try { subs = Directory.GetDirectories(dir); } catch { continue; }
                foreach (var s in subs)
                {
                    if (Fs.IsLink(s)) continue;
                    string name = Path.GetFileName(s);
                    string why = JunkReason(dir, name);
                    if (why != null)
                    {
                        long size = Fs.TreeBytes(s, false, c);
                        total += size;
                        found.Add(new[] { "x", name, Fs.Size(size), why, s });
                    }
                    else if (!name.Equals(".git", StringComparison.OrdinalIgnoreCase)) stack.Push(s);
                }
            }
            var rows = found.OrderByDescending(r => ParseSize(r[2])).ToList();
            return new Result
            {
                Summary = rows.Count == 0 ? "No removable build folders found." : rows.Count + " folders, " + Fs.Size(total) + ". They are recreated by npm install / a rebuild.",
                Columns = new[] { "", "Folder", "Size", "Reason", "Path" }, Rows = rows, PathColumn = 4, Checkable = true, Regenerable = true
            };
        }

        static double ParseSize(string s)
        {
            string[] u = { " B", " KB", " MB", " GB", " TB" };
            for (int i = u.Length - 1; i >= 0; i--)
                if (s.EndsWith(u[i])) { double d; if (double.TryParse(s.Substring(0, s.Length - u[i].Length), out d)) return d * Math.Pow(1024, i); }
            return 0;
        }

        static bool Has(string dir, params string[] patterns)
        {
            foreach (var p in patterns)
                try { if (Directory.GetFiles(dir, p).Length > 0) return true; } catch { }
            return false;
        }

        static string JunkReason(string parent, string name)
        {
            switch (name.ToLowerInvariant())
            {
                case "node_modules": return Has(parent, "package.json") ? "npm packages (package.json beside it)" : null;
                case "__pycache__": return "Python bytecode cache";
                case "bin":
                case "obj": return Has(parent, "*.csproj", "*.vbproj", "*.fsproj") ? ".NET build output" : null;
                case ".vs": return Has(parent, "*.sln") ? "Visual Studio cache" : null;
                case "target": return Has(parent, "Cargo.toml") ? "Rust build output" : null;
            }
            return null;
        }

        static IEnumerable<string> FindRepos(string root, Ctx c)
        {
            var stack = new Stack<string>();
            stack.Push(Fs.Full(root));
            int n = 0;
            while (stack.Count > 0 && n < 500)
            {
                string dir = stack.Pop();
                c.Check();
                if (Directory.Exists(Path.Combine(dir, ".git")) || File.Exists(Path.Combine(dir, ".git"))) { n++; yield return dir; continue; }
                string[] subs;
                try { subs = Directory.GetDirectories(dir); } catch { continue; }
                foreach (var s in subs)
                {
                    string name = Path.GetFileName(s).ToLowerInvariant();
                    if (name == "node_modules" || Fs.IsLink(s)) continue;
                    stack.Push(s);
                }
            }
        }

        static Result GitScan(string root, Ctx c, bool pull)
        {
            var rows = new List<string[]>();
            var repos = FindRepos(root, c).OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList();
            if (repos.Count == 0) return new Result { Summary = "No git repositories found below this folder." };
            int i = 0, dirty = 0, failed = 0;
            foreach (var r in repos)
            {
                c.Check();
                c.Report((++i) + " / " + repos.Count + "  " + Path.GetFileName(r));
                string name = Path.GetFileName(r);
                if (pull)
                {
                    string o = Launch.Capture("git", "pull --ff-only", r, c, 120000, true);
                    string last = Launch.TailLines(o, 1);
                    bool bad = o.IndexOf("fatal", StringComparison.OrdinalIgnoreCase) >= 0 || o.IndexOf("error", StringComparison.OrdinalIgnoreCase) >= 0 || o.IndexOf("(timed out)") >= 0;
                    if (bad) failed++;
                    rows.Add(new[] { name, bad ? "FAILED" : (o.IndexOf("Already up to date", StringComparison.OrdinalIgnoreCase) >= 0 ? "up to date" : "updated"), last, r });
                }
                else
                {
                    string o = Launch.Capture("git", "status --porcelain=v1 -b", r, c, 30000, true);
                    var lines = o.Replace("\r", "").Split('\n').Where(l => l.Length > 0).ToArray();
                    string branch = "", track = "";
                    int changes = 0;
                    if (lines.Length > 0 && lines[0].StartsWith("## "))
                    {
                        string h = lines[0].Substring(3);
                        int br = h.IndexOf(" [");
                        if (br >= 0) { track = h.Substring(br + 2).TrimEnd(']'); h = h.Substring(0, br); }
                        int dots = h.IndexOf("...");
                        branch = dots >= 0 ? h.Substring(0, dots) : h;
                        changes = lines.Length - 1;
                    }
                    else { branch = "?"; track = Launch.TailLines(o, 1); }
                    if (changes > 0) dirty++;
                    rows.Add(new[] { name, branch, changes == 0 ? "clean" : changes + " changed", track, r });
                }
            }
            if (pull)
                return new Result { Summary = repos.Count + " repos, " + failed + " failed", Columns = new[] { "Repo", "Result", "Last line", "Path" }, Rows = rows, PathColumn = 3 };
            return new Result { Summary = repos.Count + " repos, " + dirty + " with uncommitted changes", Columns = new[] { "Repo", "Branch", "Changes", "Ahead / behind", "Path" }, Rows = rows, PathColumn = 4 };
        }

        static IEnumerable<Action> DiskModule()
        {
            yield return new Action
            {
                Id = "compact-on", Group = "Disk", Label = "Compress folder (NTFS, LZX)", Targets = Target.Folder | Target.Background, Tier = Tier.Confirm,
                Hint = "Compresses every file below with Windows' compact.exe using the LZX algorithm. Files stay fully usable; the saving is on disk only. Best for rarely-written data such as old projects and game folders.",
                Confirm = "Compress everything under\n{0}\nwith NTFS compression (LZX)?\nThis can take a while. You can reverse it with \"Uncompress folder\".",
                Run = (paths, c) =>
                {
                    string dir = Fs.Full(paths[0]);
                    if (Fs.IsProtected(dir)) throw new Exception("Refusing to compress this location.");
                    c.Report("Compressing...");
                    string o = Launch.Capture("compact.exe", "/c /s:\"" + dir + "\" /a /i /exe:lzx", dir, c, 6 * 3600 * 1000, false);
                    return new Result { Summary = Launch.TailLines(o, 4) };
                }
            };
            yield return new Action
            {
                Id = "compact-off", Group = "Disk", Label = "Uncompress folder", Targets = Target.Folder | Target.Background, Tier = Tier.Confirm,
                Hint = "Reverses \"Compress folder\" for everything below.",
                Confirm = "Uncompress everything under\n{0} ?",
                Run = (paths, c) =>
                {
                    string dir = Fs.Full(paths[0]);
                    if (Fs.IsProtected(dir)) throw new Exception("Refusing to change this location.");
                    c.Report("Uncompressing...");
                    string o = Launch.Capture("compact.exe", "/u /s:\"" + dir + "\" /a /i /exe", dir, c, 6 * 3600 * 1000, false);
                    return new Result { Summary = Launch.TailLines(o, 4) };
                }
            };
        }
    }
}
