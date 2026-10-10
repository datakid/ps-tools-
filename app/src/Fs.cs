using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace PSTools
{
    static class Fs
    {
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern bool DeleteFile(string path);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern uint GetCompressedFileSize(string name, out uint high);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern bool CreateSymbolicLink(string link, string target, int flags);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern bool CreateHardLink(string link, string existing, IntPtr reserved);

        [DllImport("mpr.dll", CharSet = CharSet.Unicode)]
        static extern int WNetGetConnection(string local, StringBuilder remote, ref int length);

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

        public static bool IsDir(string path) { return Directory.Exists(path); }

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

        public static string Full(string path)
        {
            string p = Path.GetFullPath(path);
            if (p.Length > 3) p = p.TrimEnd('\\');
            return p;
        }

        public static bool IsRoot(string path)
        {
            string p = Full(path);
            return p.Length <= 3;
        }

        public static bool IsProtected(string path)
        {
            string p = Full(path);
            if (p.Length <= 3) return true;
            string profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (string.Equals(p, profile, StringComparison.OrdinalIgnoreCase)) return true;
            string[] zones =
            {
                Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)
            };
            foreach (var z in zones)
                if (!string.IsNullOrEmpty(z) && IsInside(p, z)) return true;
            string up = Path.GetDirectoryName(profile);
            if (!string.IsNullOrEmpty(up) && string.Equals(p, up, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        public static long DiskSize(string file)
        {
            uint high;
            uint low = GetCompressedFileSize(file, out high);
            if (low == 0xFFFFFFFF && Marshal.GetLastWin32Error() != 0) return -1;
            return ((long)high << 32) | low;
        }

        public static long TreeBytes(string dir, bool onDisk, Ctx c)
        {
            long sum = 0;
            foreach (var f in Files(dir, true))
            {
                if (c != null) c.Check();
                try { sum += onDisk ? Math.Max(0, DiskSize(f)) : new FileInfo(f).Length; } catch { }
            }
            return sum;
        }

        public static string Sanitize(string name)
        {
            var bad = Path.GetInvalidFileNameChars();
            var sb = new StringBuilder();
            foreach (var ch in name) sb.Append(bad.Contains(ch) ? '_' : ch);
            string s = sb.ToString().Trim().TrimEnd('.', ' ');
            if (s.Length > 100) s = s.Substring(0, 100).TrimEnd('.', ' ');
            return s;
        }

        public static bool LooksText(string path)
        {
            try
            {
                using (var s = File.OpenRead(path))
                {
                    var buf = new byte[8192];
                    int n = s.Read(buf, 0, buf.Length);
                    for (int i = 0; i < n; i++) if (buf[i] == 0) return false;
                    return true;
                }
            }
            catch { return false; }
        }

        public static string Mime(string ext)
        {
            switch ((ext ?? "").ToLowerInvariant())
            {
                case ".html": case ".htm": return "text/html; charset=utf-8";
                case ".js": case ".mjs": return "text/javascript; charset=utf-8";
                case ".css": return "text/css; charset=utf-8";
                case ".json": return "application/json; charset=utf-8";
                case ".txt": case ".md": return "text/plain; charset=utf-8";
                case ".csv": return "text/csv; charset=utf-8";
                case ".xml": return "application/xml; charset=utf-8";
                case ".svg": return "image/svg+xml";
                case ".png": return "image/png";
                case ".jpg": case ".jpeg": return "image/jpeg";
                case ".gif": return "image/gif";
                case ".webp": return "image/webp";
                case ".ico": return "image/x-icon";
                case ".wasm": return "application/wasm";
                case ".pdf": return "application/pdf";
                case ".mp3": return "audio/mpeg";
                case ".mp4": return "video/mp4";
                case ".woff": return "font/woff";
                case ".woff2": return "font/woff2";
                case ".ttf": return "font/ttf";
                case ".zip": return "application/zip";
                default: return "application/octet-stream";
            }
        }

        public static string ToWsl(string path)
        {
            string p = Full(path);
            if (p.Length >= 2 && p[1] == ':')
                return "/mnt/" + char.ToLowerInvariant(p[0]) + p.Substring(2).Replace('\\', '/');
            return p.Replace('\\', '/');
        }

        public static string ToUrl(string path)
        {
            string p = Full(path);
            bool unc = p.StartsWith("\\\\");
            string[] seg = p.Replace('\\', '/').Split('/');
            for (int i = 0; i < seg.Length; i++)
                if (!(i == 0 && seg[i].EndsWith(":"))) seg[i] = Uri.EscapeDataString(seg[i]);
            string joined = string.Join("/", seg);
            return unc ? "file:" + joined : "file:///" + joined;
        }

        public static string ToUnc(string path)
        {
            string p = Full(path);
            if (p.Length >= 2 && p[1] == ':')
            {
                var sb = new StringBuilder(512);
                int len = sb.Capacity;
                if (WNetGetConnection(p.Substring(0, 2), sb, ref len) == 0)
                    return sb.ToString().TrimEnd('\\') + p.Substring(2);
            }
            return p;
        }

        public static string LastError()
        {
            return new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error()).Message;
        }

        public static bool MakeSymlink(string link, string target, bool dir)
        {
            return CreateSymbolicLink(link, target, (dir ? 1 : 0) | 2);
        }

        public static bool MakeHardLink(string link, string existing)
        {
            return CreateHardLink(link, existing, IntPtr.Zero);
        }

        static Encoding Oem()
        {
            try { return Encoding.GetEncoding(CultureInfo.CurrentCulture.TextInfo.OEMCodePage); }
            catch { return Encoding.Default; }
        }

        public static string Cmd(string args, int timeoutMs, out int exit)
        {
            var psi = new ProcessStartInfo("cmd.exe", "/c " + args)
            {
                UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true,
                StandardOutputEncoding = Oem(), StandardErrorEncoding = Oem()
            };
            using (var p = Process.Start(psi))
            {
                var err = p.StandardError.ReadToEndAsync();
                string o = p.StandardOutput.ReadToEnd();
                p.WaitForExit(timeoutMs);
                exit = p.HasExited ? p.ExitCode : -1;
                return (o + err.Result).Trim();
            }
        }

        public static string Which(string exe)
        {
            try
            {
                int exit;
                string o = Cmd("where " + exe, 6000, out exit);
                if (exit == 0)
                {
                    foreach (var line in o.Split('\n'))
                    {
                        string l = line.Trim();
                        if (l.Length > 0 && File.Exists(l)) return l;
                    }
                    var first = o.Split('\n').Select(x => x.Trim()).FirstOrDefault(x => x.Length > 0);
                    if (first != null) return first;
                }
            }
            catch { }
            return null;
        }
    }

    class Journal
    {
        public static string Dir { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PSTools"); } }
        static string FilePath { get { return Path.Combine(Dir, "undo.txt"); } }
        static string DataDir { get { return Path.Combine(Dir, "undo-data"); } }

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

        static void PurgeOld()
        {
            try
            {
                if (File.Exists(FilePath))
                    foreach (var l in File.ReadAllLines(FilePath, Encoding.UTF8))
                    {
                        var p = l.Split('\t');
                        if (p.Length == 3 && p[0] == "B") try { File.Delete(p[2]); } catch { }
                    }
            }
            catch { }
        }

        public void Save()
        {
            if (lines.Count == 0) return;
            Directory.CreateDirectory(Dir);
            PurgeOld();
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
            foreach (var l in all)
            {
                var p = l.Split('\t');
                if (p.Length == 3 && p[0] == "B") try { File.Delete(p[2]); } catch { }
            }
            File.Delete(FilePath);
            return "Undo finished: " + ok + " items restored" + (failed > 0 ? ", " + failed + " failed (file changed or missing)." : ".");
        }
    }
}
