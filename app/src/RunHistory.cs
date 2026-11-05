using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Web.Script.Serialization;

namespace PSTools
{
    class RunEntry
    {
        public string Command = "", Shell = "ps", Dir = "", Time = "";
        public int Exit;
    }

    static class RunHistory
    {
        public const int Max = 500;
        public static string FileOverride;

        static string FilePath { get { return FileOverride ?? Path.Combine(Journal.Dir, "run-history.json"); } }

        public static string LastShell = "ps";
        public static HashSet<string> Approved = new HashSet<string>(StringComparer.Ordinal);
        static List<RunEntry> entries;

        static void Ensure()
        {
            if (entries != null) return;
            entries = new List<RunEntry>();
            try
            {
                if (!File.Exists(FilePath)) return;
                var root = new JavaScriptSerializer { MaxJsonLength = int.MaxValue }.DeserializeObject(File.ReadAllText(FilePath, Encoding.UTF8)) as Dictionary<string, object>;
                if (root == null) return;
                object v;
                if (root.TryGetValue("shell", out v) && v != null) LastShell = v.ToString();
                if (root.TryGetValue("approved", out v) && v is IEnumerable) foreach (object x in (IEnumerable)v) if (x != null) Approved.Add(x.ToString());
                if (root.TryGetValue("entries", out v) && v is IEnumerable)
                    foreach (object x in (IEnumerable)v)
                    {
                        var d = x as Dictionary<string, object>;
                        if (d == null) continue;
                        var e = new RunEntry();
                        object t;
                        if (d.TryGetValue("c", out t) && t != null) e.Command = t.ToString();
                        if (d.TryGetValue("s", out t) && t != null) e.Shell = t.ToString();
                        if (d.TryGetValue("d", out t) && t != null) e.Dir = t.ToString();
                        if (d.TryGetValue("t", out t) && t != null) e.Time = t.ToString();
                        if (d.TryGetValue("x", out t) && t != null) { int n; int.TryParse(t.ToString(), out n); e.Exit = n; }
                        if (e.Command.Length > 0) entries.Add(e);
                    }
            }
            catch { entries = new List<RunEntry>(); }
        }

        public static void Reset() { entries = null; }

        public static List<RunEntry> All { get { Ensure(); return entries; } }

        public static void Save()
        {
            Ensure();
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
                var list = entries.Select(e => new Dictionary<string, object> { { "c", e.Command }, { "s", e.Shell }, { "d", e.Dir }, { "x", e.Exit }, { "t", e.Time } }).ToList();
                var json = new JavaScriptSerializer { MaxJsonLength = int.MaxValue }.Serialize(new Dictionary<string, object>
                {
                    { "shell", LastShell }, { "approved", Approved.ToArray() }, { "entries", list }
                });
                File.WriteAllText(FilePath, json, new UTF8Encoding(false));
            }
            catch { }
        }

        public static void Add(string command, string shell, string dir, int exit)
        {
            Ensure();
            if (string.IsNullOrWhiteSpace(command)) return;
            entries.RemoveAll(e => e.Command == command && e.Shell == shell);
            entries.Insert(0, new RunEntry { Command = command, Shell = shell, Dir = dir ?? "", Exit = exit, Time = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") });
            if (entries.Count > Max) entries.RemoveRange(Max, entries.Count - Max);
            Save();
        }

        public static void Clear()
        {
            Ensure();
            entries.Clear();
            Save();
        }

        public static List<string> ImportPsReadLine(string folder)
        {
            var result = new List<string>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            try
            {
                string dir = folder ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Microsoft", "Windows", "PowerShell", "PSReadLine");
                if (!Directory.Exists(dir)) return result;
                var files = new DirectoryInfo(dir).GetFiles("*_history.txt").OrderByDescending(f => f.LastWriteTimeUtc).ToList();
                foreach (var f in files)
                {
                    var lines = File.ReadAllLines(f.FullName, Encoding.UTF8);
                    var logical = new List<string>();
                    var current = new StringBuilder();
                    foreach (string raw in lines)
                    {
                        if (raw.EndsWith("`")) { current.Append(raw.Substring(0, raw.Length - 1)).Append("\n"); continue; }
                        current.Append(raw);
                        string cmd = current.ToString().Trim();
                        current.Length = 0;
                        if (cmd.Length > 0) logical.Add(cmd);
                    }
                    for (int i = logical.Count - 1; i >= 0; i--)
                        if (seen.Add(logical[i])) result.Add(logical[i]);
                }
            }
            catch { }
            return result;
        }
    }

    static class RunComplete
    {
        static readonly string[] Commands =
        {
            "Add-Content", "Clear-Host", "Compare-Object", "Compress-Archive", "ConvertFrom-Json", "ConvertTo-Csv", "ConvertTo-Json", "ConvertTo-Html",
            "Copy-Item", "Expand-Archive", "Export-Csv", "ForEach-Object", "Format-List", "Format-Table", "Get-ChildItem", "Get-Clipboard", "Get-Command",
            "Get-Content", "Get-Date", "Get-FileHash", "Get-History", "Get-Help", "Get-Item", "Get-ItemProperty", "Get-Location", "Get-Member", "Get-NetIPAddress",
            "Get-NetTCPConnection", "Get-Process", "Get-Service", "Get-WinEvent", "Group-Object", "Import-Csv", "Invoke-RestMethod", "Invoke-WebRequest",
            "Join-Path", "Measure-Command", "Measure-Object", "Move-Item", "New-Item", "Out-File", "Out-GridView", "Remove-Item", "Rename-Item", "Resolve-Path",
            "Select-Object", "Select-String", "Set-Clipboard", "Set-Content", "Set-Location", "Sort-Object", "Split-Path", "Start-Process", "Start-Sleep",
            "Stop-Process", "Test-Connection", "Test-NetConnection", "Test-Path", "Where-Object", "Write-Host", "Write-Output", "git", "winget", "robocopy",
            "ipconfig", "netstat", "tasklist", "taskkill", "findstr", "where", "tree", "xcopy", "attrib", "icacls", "systeminfo"
        };

        public static int TokenStart(string text, int caret)
        {
            int i = Math.Min(caret, text.Length);
            while (i > 0 && !char.IsWhiteSpace(text[i - 1]) && text[i - 1] != '|' && text[i - 1] != ';' && text[i - 1] != '(' && text[i - 1] != '{') i--;
            return i;
        }

        public static List<string> Suggest(string text, int caret, string dir)
        {
            int start = TokenStart(text, caret);
            string token = text.Substring(start, Math.Min(caret, text.Length) - start);
            var result = new List<string>();
            if (token.Length < 2) return result;
            bool pathy = token.Contains("\\") || token.Contains(".");
            if (pathy && !string.IsNullOrEmpty(dir) && Directory.Exists(dir))
            {
                try
                {
                    string prefix = token.TrimStart('.', '\\');
                    foreach (var n in Directory.GetFileSystemEntries(dir).Select(Path.GetFileName))
                        if (n.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && result.Count < 8) result.Add((token.StartsWith(".\\") ? ".\\" : "") + n);
                }
                catch { }
            }
            bool firstWord = text.Substring(0, start).Trim().Length == 0 || text.Substring(0, start).TrimEnd().EndsWith("|");
            if (firstWord)
            {
                foreach (var c in Commands)
                    if (c.StartsWith(token, StringComparison.OrdinalIgnoreCase) && !result.Contains(c) && result.Count < 8) result.Add(c);
                foreach (var h in RunHistory.All)
                {
                    string first = h.Command.Split(new[] { ' ', '\n' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
                    if (first != null && first.StartsWith(token, StringComparison.OrdinalIgnoreCase) && !result.Contains(first) && result.Count < 8) result.Add(first);
                }
            }
            else
                foreach (var c in Commands)
                    if (c.StartsWith(token, StringComparison.OrdinalIgnoreCase) && !result.Contains(c) && result.Count < 8) result.Add(c);
            return result;
        }
    }
}
