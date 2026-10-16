using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace PSTools
{
    enum Tier { Instant, Confirm, Elevated }

    [Flags]
    enum Target { None = 0, Folder = 1, Background = 2, File = 4, Drive = 8, Dirs = 11, All = 15 }

    class Result
    {
        public string Summary;
        public string[] Columns;
        public List<string[]> Rows;
        public int PathColumn = -1;
        public bool Checkable;
        public bool Regenerable;
        public string ClipText;
        public string RowActionText;
        public Func<string[], string> RowAction;
    }

    class Ctx
    {
        public volatile bool Cancel;
        public Action<string> Report = delegate { };
        public string ClipboardText = "";
        public void Check() { if (Cancel) throw new OperationCanceledException(); }
    }

    class Action
    {
        public string Id, Label, Group, Hint, Confirm, AppliesTo;
        public string Ps, PsNone;
        public Tier Tier = Tier.Instant;
        public Target Targets = Target.Dirs;
        public bool Multi, Fast, DefaultFav, Extended, Undoable;
        public Func<bool> Available;
        public Func<string[], Ctx, Result> Run;
        public Func<string[], Form> Window;
        public Func<string[], string> Script;

        public bool Usable { get { return Available == null || Available(); } }
        public bool InMenuCapable { get { return Targets != Target.None && Usable; } }
    }

    class Module
    {
        public string Name, Blurb;
        public Module(string name, string blurb) { Name = name; Blurb = blurb; }
    }

    static class Modules
    {
        public const string ImageQuery = "System.FileExtension:=\".png\" OR System.FileExtension:=\".jpg\" OR System.FileExtension:=\".jpeg\" OR System.FileExtension:=\".bmp\" OR System.FileExtension:=\".gif\" OR System.FileExtension:=\".tif\" OR System.FileExtension:=\".tiff\"";
        public const string ZipQuery = "System.FileExtension:=\".zip\"";

        public static readonly List<Module> All = new List<Module>
        {
            new Module("Copy", "Copy names, paths, hashes, contents and folder structures to the clipboard"),
            new Module("Create", "Paste clipboard as a file, download a link, dated folders, group, unwrap, snapshot, undo"),
            new Module("Archive", "Zip folders without build clutter, extract archives safely"),
            new Module("Image", "Convert, shrink and strip metadata from pictures"),
            new Module("Organize", "Flatten, sort, batch rename"),
            new Module("Clean up", "Empty folders, duplicates, unblock"),
            new Module("Inspect", "Search here, sizes, types, largest, recent, long paths"),
            new Module("Export", "Inventory and listings"),
            new Module("Fix", "Force delete, timestamps, attributes, line endings, ownership"),
            new Module("Links", "Mark, then junction, symlink, hard link or compare"),
            new Module("Dev", "Serve a folder, clear build folders, git across repos, open in tools"),
            new Module("Disk", "Compress folders with NTFS compression"),
            new Module("System", "Updates, app lists, Explorer fixes, network and repair tools"),
            new Module("Recipes", "Your own chains of actions, run in order on the same selection"),
            new Module("PowerShell", "Browse and run the command library"),
            new Module("Library", "Snippets from commands.json that you pinned to the menu")
        };

        public static Module Find(string name) { return All.FirstOrDefault(m => m.Name == name); }
        public static int Order(string name) { int i = All.FindIndex(m => m.Name == name); return i < 0 ? 999 : i; }
    }

    static class DefaultPins
    {
        public static readonly Target[] Contexts = { Target.Folder, Target.Background, Target.File, Target.Drive };

        public static string Key(Target t)
        {
            switch (t)
            {
                case Target.Folder: return "folder";
                case Target.Background: return "background";
                case Target.File: return "file";
                case Target.Drive: return "drive";
            }
            return null;
        }

        public static Target FromKey(string key)
        {
            foreach (var t in Contexts) if (Key(t) == key) return t;
            return Target.None;
        }

        public static string Title(Target t)
        {
            switch (t)
            {
                case Target.Folder: return "Folder";
                case Target.Background: return "Inside a folder";
                case Target.File: return "File";
                case Target.Drive: return "Drive";
            }
            return "";
        }

        public static List<string> For(Target t)
        {
            switch (t)
            {
                case Target.Folder: return new List<string> { "copy-path-quoted", "zip-smart", "space-map", "search-here" };
                case Target.Background: return new List<string> { "paste-image", "paste-text", "download-clip", "search-here", "open-ps", "undo-last" };
                case Target.File: return new List<string> { "copy-path-quoted", "copy-name", "copy-sha256", "img-png", "unzip-here" };
                case Target.Drive: return new List<string> { "space-map", "folder-sizes", "open-ps" };
            }
            return new List<string>();
        }
    }

    static class Presets
    {
        public const string Custom = "Custom";

        public static readonly string[] Names = { "Minimal", "Everyday", "Developer", "Everything" };

        static readonly string[] Minimal =
        {
            "copy-name", "copy-path-fwd", "copy-sha256", "paste-image", "paste-text",
            "download-clip", "search-here", "open-ps", "undo-last"
        };

        static readonly string[] Everyday =
        {
            "copy-path-quoted", "copy-parent", "verify-hash", "copy-contents", "structure",
            "new-dated-folder", "group-selected", "zip-smart", "unzip-here",
            "img-png", "img-jpg", "img-resize-1600", "img-strip",
            "folder-sizes", "space-map", "duplicates", "rename", "undo-history"
        };

        public static IEnumerable<string> ReferencedIds()
        {
            return Minimal.Concat(Everyday);
        }

        public static string Describe(string name)
        {
            switch (name)
            {
                case "Minimal": return "9 entries: copy path and name, hash, paste image or text, download a link, search, open PowerShell, undo.";
                case "Everyday": return "About 25 entries: Minimal plus zip, extract, image tools, folder structure, duplicates, batch rename.";
                case "Developer": return "Everyday plus the Dev tools (terminals, VS Code, local server, git across repos) and Links.";
                case "Everything": return "Every action that does not need administrator rights. A long menu.";
            }
            return "";
        }

        public static HashSet<string> Ids(string name)
        {
            var set = new HashSet<string>(Minimal);
            if (name == "Minimal") return Keep(set);
            set.UnionWith(Everyday);
            if (name == "Everyday") return Keep(set);
            if (name == "Developer")
            {
                foreach (var a in Actions.All)
                    if (a.Group == "Dev" || a.Group == "Links") set.Add(a.Id);
                return Keep(set);
            }
            foreach (var a in Actions.All) set.Add(a.Id);
            return Keep(set);
        }

        static HashSet<string> Keep(HashSet<string> set)
        {
            var ok = new HashSet<string>();
            foreach (var a in Actions.All)
                if (set.Contains(a.Id) && a.Targets != Target.None && a.Tier != Tier.Elevated && Modules.Find(a.Group) != null && a.Group != "Library" && a.Group != "Recipes" && a.Usable) ok.Add(a.Id);
            return ok;
        }

        public static string Match(HashSet<string> enabled)
        {
            foreach (var n in Names) if (Ids(n).SetEquals(enabled)) return n;
            return Custom;
        }
    }
}
