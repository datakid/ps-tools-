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
        public string Id, Label, Group, Hint, Confirm;
        public Tier Tier = Tier.Instant;
        public Target Targets = Target.Dirs;
        public bool Multi, Fast, DefaultFav;
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
        public bool DefaultOn;
        public Module(string name, bool on, string blurb) { Name = name; DefaultOn = on; Blurb = blurb; }
    }

    static class Modules
    {
        public static readonly List<Module> All = new List<Module>
        {
            new Module("Copy", true, "Copy names, paths, hashes, contents and folder structures to the clipboard"),
            new Module("Create", true, "Paste clipboard as a file, dated folders, group, unwrap, snapshot"),
            new Module("Organize", true, "Flatten, sort, batch rename"),
            new Module("Clean up", true, "Empty folders, duplicates, unblock"),
            new Module("Inspect", true, "Sizes, types, largest, recent, long paths"),
            new Module("Export", true, "Inventory and listings"),
            new Module("Fix", false, "Force delete, timestamps, attributes, line endings, ownership"),
            new Module("Links", false, "Mark, then junction, symlink, hard link or compare"),
            new Module("Dev", false, "Serve a folder, clear build folders, git across repos, open in tools"),
            new Module("Disk", false, "Compress folders with NTFS compression"),
            new Module("System", false, "Updates, app lists, Explorer fixes, network and repair tools"),
            new Module("PowerShell", true, "Browse and run the command library"),
            new Module("Library", true, "Snippets from commands.json that you pinned to the menu")
        };

        public static Module Find(string name) { return All.FirstOrDefault(m => m.Name == name); }
        public static int Order(string name) { int i = All.FindIndex(m => m.Name == name); return i < 0 ? 999 : i; }
    }
}
