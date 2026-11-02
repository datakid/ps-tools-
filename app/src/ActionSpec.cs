using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace PSTools
{
    [Flags]
    enum ContextKind { None = 0, File = 1, Folder = 2, Background = 4, Drive = 8, Desktop = 16, Any = 31 }

    enum ActionShell { Builtin, PowerShell, Pwsh, Cmd }

    enum ActionOutput { Clipboard, Toast, Grid, Console, Window, None }

    enum Risk { Safe, Modifies, Destructive }

    enum MultiMode { Join, Each, Single }

    enum UndoMode { None, Journal }

    enum SpecSource { Builtin, Shipped, Library, Recipe, User }

    enum MatchKind { Any, File, Folder }

    class InputSpec
    {
        public string Name = "", Label = "", Type = "text", Default = "";
        public List<string> Options = new List<string>();
    }

    class ActionSpec
    {
        public string Id = "";
        public List<string> Aliases = new List<string>();
        public string Title = "", Menu = "";
        public int Order = 500;
        public ContextKind When = ContextKind.None;
        public List<string> MatchExt = new List<string>();
        public MatchKind MatchKind = MatchKind.Any;
        public int MatchMin, MatchMax;
        public List<string> Requires = new List<string>();
        public ActionShell Shell = ActionShell.Builtin;
        public bool Fallback;
        public string Builtin = "", Command = "";
        public List<InputSpec> Inputs = new List<InputSpec>();
        public ActionOutput Output = ActionOutput.Toast;
        public Risk Risk = Risk.Safe;
        public bool Admin;
        public MultiMode Multi = MultiMode.Join;
        public UndoMode Undo = UndoMode.None;
        public bool Extended;
        public string Icon = "";
        public List<string> Keywords = new List<string>();
        public string Description = "", Ps = "";
        public bool PopupHidden;

        public SpecSource Source = SpecSource.Builtin;
        public string SourceFile = "";
        public string Overrides = "";
        public bool AppOnly, UserHidden;
        public string PsNote = "";
        public Action Legacy;
        public ActionTest Test;

        public string[] MenuParts
        {
            get { return Menu.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries); }
        }

        public bool IsScript { get { return Shell != ActionShell.Builtin; } }

        public bool AppliesTo(ContextKind ctx)
        {
            return (When & ctx) != 0;
        }
    }

    class ActionTest
    {
        public string Fixture = "", Target = "", Expect = "golden";
    }

    class CatalogProblem
    {
        public string File, Reason;
        public int Line;

        public override string ToString()
        {
            return File + (Line > 0 ? ":" + Line : "") + " - " + Reason;
        }
    }

    static class SpecRules
    {
        public static readonly Regex IdPattern = new Regex(@"^[a-z0-9]+(\.[a-z0-9-]+)+$", RegexOptions.Compiled);

        public static readonly string[] KnownTools = { "git", "code", "wt", "pwsh", "ffmpeg", "rg", "magick", "node", "python", "winget", "tar" };

        public const int MaxTitleStrict = 32;
        public const int MaxTitleLoose = 64;
        public const int MaxMenuDepth = 3;

        public static string ContextName(ContextKind k)
        {
            switch (k)
            {
                case ContextKind.File: return "file";
                case ContextKind.Folder: return "folder";
                case ContextKind.Background: return "background";
                case ContextKind.Drive: return "drive";
                case ContextKind.Desktop: return "desktop";
            }
            return "";
        }

        public static ContextKind ParseContext(string s)
        {
            switch ((s ?? "").Trim().ToLowerInvariant())
            {
                case "file": return ContextKind.File;
                case "folder": return ContextKind.Folder;
                case "background": return ContextKind.Background;
                case "drive": return ContextKind.Drive;
                case "desktop": return ContextKind.Desktop;
            }
            return ContextKind.None;
        }

        public static ContextKind FromTarget(Target t)
        {
            var k = ContextKind.None;
            if ((t & Target.File) != 0) k |= ContextKind.File;
            if ((t & Target.Folder) != 0) k |= ContextKind.Folder;
            if ((t & Target.Background) != 0) k |= ContextKind.Background;
            if ((t & Target.Drive) != 0) k |= ContextKind.Drive;
            return k;
        }

        public static List<string> ContextNames(ContextKind k)
        {
            var l = new List<string>();
            foreach (ContextKind c in new[] { ContextKind.File, ContextKind.Folder, ContextKind.Background, ContextKind.Drive, ContextKind.Desktop })
                if ((k & c) != 0) l.Add(ContextName(c));
            return l;
        }

        public static string Validate(ActionSpec s)
        {
            if (!IdPattern.IsMatch(s.Id ?? "")) return "id '" + s.Id + "' must look like copy.tree.ascii (lowercase letters, digits, dots, hyphens)";
            if (string.IsNullOrWhiteSpace(s.Title)) return "title is missing";
            int max = s.Source == SpecSource.Builtin || s.Source == SpecSource.Shipped ? MaxTitleStrict : MaxTitleLoose;
            if (s.Title.Length > max) return "title is longer than " + max + " characters";
            if (s.Menu.Contains("//") || s.Menu.StartsWith("/") || s.Menu.EndsWith("/")) return "menu '" + s.Menu + "' has an empty level";
            if (s.MenuParts.Length > MaxMenuDepth) return "menu '" + s.Menu + "' is deeper than " + MaxMenuDepth + " levels";
            if (s.When == ContextKind.None) return "when is empty (use file, folder, background, drive or desktop)";
            if (string.IsNullOrWhiteSpace(s.Description)) return "description is missing";
            if (s.Shell == ActionShell.Builtin && s.Builtin.Length == 0 && s.Legacy == null) return "builtin key is missing";
            if (s.Shell != ActionShell.Builtin && string.IsNullOrWhiteSpace(s.Command)) return "command is missing";
            foreach (var r in s.Requires)
                if (Array.IndexOf(KnownTools, r) < 0) return "requires '" + r + "' is not a known tool (" + string.Join(", ", KnownTools) + ")";
            if (s.MatchMax < 0 || s.MatchMin < 0 || (s.MatchMax > 0 && s.MatchMin > s.MatchMax)) return "match.min/match.max are inconsistent";
            foreach (var i in s.Inputs)
            {
                if (i.Name.Length == 0) return "an input has no name";
                if (Array.IndexOf(new[] { "text", "pick", "number", "bool", "folder", "file" }, i.Type) < 0) return "input '" + i.Name + "' has unknown type '" + i.Type + "'";
                if (i.Type == "pick" && i.Options.Count == 0) return "input '" + i.Name + "' is a pick without options";
            }
            return null;
        }
    }
}
