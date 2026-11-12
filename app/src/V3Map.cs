using System;
using System.Collections.Generic;

namespace PSTools
{
    static class V3Map
    {
        public static readonly string[][] Rows =
        {
            new[] { "copy-contents", "copy.contents", "Copy/Contents", "File contents" },
            new[] { "copy-csv", "copy.names.csv", "Copy/Names", "Names, comma-separated" },
            new[] { "copy-datauri", "copy.datauri", "Copy/Contents", "Data URI" },
            new[] { "copy-dirs-paths", "copy.list.subfolder-paths", "Copy/Lists", "All subfolder paths" },
            new[] { "copy-dirs-rel", "copy.list.subfolder-paths-rel", "Copy/Lists", "All subfolder paths (relative)" },
            new[] { "copy-dirs-tree", "copy.tree.folders", "Copy/Tree", "Tree, folders only" },
            new[] { "copy-json", "copy.path.json", "Copy/Paths", "Paths as JSON array" },
            new[] { "copy-md-link", "copy.path.markdown-link", "Copy/Paths", "Markdown link" },
            new[] { "copy-md5", "copy.hash.md5", "Copy/Hash", "MD5" },
            new[] { "copy-name", "copy.names.selected", "Copy/Names", "Names of selected" },
            new[] { "copy-name-noext", "copy.names.stem", "Copy/Names", "Names without extension" },
            new[] { "copy-names", "copy.list.names-inside", "Copy/Lists", "Names of items inside" },
            new[] { "copy-parent", "copy.path.parent", "Copy/Paths", "Parent folder path" },
            new[] { "copy-parent-name", "copy.path.parent-name", "Copy/Paths", "Parent folder name" },
            new[] { "copy-path-fwd", "copy.path.fwd", "Copy/Paths", "Path with /" },
            new[] { "copy-path-quoted", "copy.path.quoted", "Copy/Paths", "Path (quoted)" },
            new[] { "copy-pslocation", "copy.path.cd", "Copy/Paths", "cd command for this folder" },
            new[] { "copy-sha1", "copy.hash.sha1", "Copy/Hash", "SHA-1" },
            new[] { "copy-sha256", "copy.hash.sha256", "Copy/Hash", "SHA-256" },
            new[] { "copy-size", "copy.size", "Copy", "Size" },
            new[] { "copy-tree", "copy.tree.ascii", "Copy/Tree", "Tree (ASCII)" },
            new[] { "copy-unc", "copy.path.unc", "Copy/Paths", "UNC / network path" },
            new[] { "copy-url", "copy.path.uri", "Copy/Paths", "file:// URL" },
            new[] { "copy-wsl", "copy.path.wsl", "Copy/Paths", "WSL path" },
            new[] { "structure", "copy.tree.window", "Copy/Tree", "Folder structure…" },
            new[] { "verify-hash", "copy.hash.verify", "Copy/Hash", "Verify hash from clipboard" },
            new[] { "copy-sysinfo", "system.summary", "System", "Copy system summary" },
            new[] { "download-clip", "new.download-clip", "Paste & New", "Download link from clipboard" },
            new[] { "group-selected", "new.group-selected", "Paste & New", "Move selection into new folder" },
            new[] { "new-dated-folder", "new.dated-folder", "Paste & New", "Dated folder" },
            new[] { "new-folder-clip", "new.folder-from-clip", "Paste & New", "Folder named from clipboard" },
            new[] { "paste-image", "new.paste-image", "Paste & New", "Paste image as PNG" },
            new[] { "paste-text", "new.paste-text", "Paste & New", "Paste text as file" },
            new[] { "snapshot", "new.snapshot", "Paste & New", "Dated snapshot copy" },
            new[] { "unwrap", "new.unwrap", "Paste & New", "Unwrap folder" },
            new[] { "undo-history", "system.undo-history", "System", "Undo history\u2026" },
            new[] { "undo-last", "system.undo-last", "System", "Undo last action" },
            new[] { "unzip-here", "archive.extract", "Archive", "Extract here (smart)" },
            new[] { "zip-smart", "archive.zip-smart", "Archive", "Zip (skip build folders)" },
            new[] { "img-jpg", "image.jpg", "Image", "Convert to JPEG" },
            new[] { "img-png", "image.png", "Image", "Convert to PNG" },
            new[] { "img-resize-1600", "image.resize-1600", "Image", "Shrink to 1600 px" },
            new[] { "img-resize-800", "image.resize-800", "Image", "Shrink to 800 px" },
            new[] { "img-strip", "image.strip", "Image", "Strip metadata" },
            new[] { "flatten-copy", "organize.flatten", "Organize", "Flatten" },
            new[] { "flatten-move", "organize.flatten-move", "Organize", "Flatten tree (move here)" },
            new[] { "organize-ext", "organize.by-ext", "Organize", "Sort by extension" },
            new[] { "organize-month", "organize.by-month", "Organize", "Sort by month" },
            new[] { "organize-type", "organize.by-type", "Organize", "Sort into folders by type" },
            new[] { "rename", "rename.batch", "Rename", "Batch rename…" },
            new[] { "delete-empty", "clean.empty-folders-delete", "Clean up", "Remove empty folders" },
            new[] { "unblock", "fix.unblock-recursive", "Fix", "Unblock all files below" },
            new[] { "largest", "search.large", "Search", "Largest files" },
            new[] { "recent", "search.recent", "Search", "Recently changed files" },
            new[] { "duplicates", "search.duplicates", "Search", "Duplicate files" },
            new[] { "long-paths", "search.long-paths", "Search", "Long paths" },
            new[] { "search-here", "search.here", "Search", "Search here…" },
            new[] { "folder-sizes", "inspect.sizes", "Inspect", "Subfolder sizes" },
            new[] { "by-type", "inspect.types", "Inspect", "File types" },
            new[] { "space-map", "inspect.space-map", "Inspect", "Folder space map" },
            new[] { "inventory", "inspect.inventory", "Inspect", "Export inventory.csv" },
            new[] { "port-who", "inspect.port", "Inspect", "What is using port\u2026" },
            new[] { "attr-hide", "fix.attr.hide", "Fix/Attributes", "Hide" },
            new[] { "attr-unhide", "fix.attr.unhide", "Fix/Attributes", "Unhide" },
            new[] { "attr-ro-off", "fix.attr.readonly-off", "Fix/Attributes", "Clear read-only" },
            new[] { "attr-ro-on", "fix.attr.readonly-on", "Fix/Attributes", "Make read-only" },
            new[] { "bom-add", "fix.bom.add", "Fix/Encoding", "Add UTF-8 BOM" },
            new[] { "bom-remove", "fix.bom.remove", "Fix/Encoding", "Remove UTF-8 BOM" },
            new[] { "eol-crlf", "fix.eol.crlf", "Fix/Encoding", "Line endings to CRLF" },
            new[] { "eol-lf", "fix.eol.lf", "Fix/Encoding", "Line endings to LF" },
            new[] { "force-delete", "fix.force-delete", "Fix", "Force delete" },
            new[] { "take-ownership", "fix.take-ownership", "Fix", "Take ownership" },
            new[] { "touch", "fix.touch", "Fix", "Set dates to now" },
            new[] { "unblock-files", "fix.unblock", "Fix", "Unblock selected files" },
            new[] { "compact-on", "disk.compress", "Fix/Compression", "Compress folder (NTFS)" },
            new[] { "compact-off", "disk.uncompress", "Fix/Compression", "Uncompress folder" },
            new[] { "mark", "compare.mark", "Compare", "Mark for compare" },
            new[] { "compare-marked", "compare.with-marked", "Compare", "Compare with marked" },
            new[] { "link-hard", "links.hard", "Links", "Hard link to marked here" },
            new[] { "link-junction", "links.junction", "Links", "Junction to marked here" },
            new[] { "link-symlink", "links.symlink", "Links", "Symbolic link to marked here" },
            new[] { "clean-build", "dev.clean-build", "Dev", "Find removable build folders" },
            new[] { "serve-here", "dev.serve", "Dev", "Serve folder on localhost" },
            new[] { "git-pull", "git.pull.repos", "Git", "Pull all repos below" },
            new[] { "git-status", "git.status.repos", "Git", "Git status of all repos below" },
            new[] { "open-cmd", "open.cmd", "Open", "Command Prompt here" },
            new[] { "open-ps", "open.ps", "Open", "Windows PowerShell here" },
            new[] { "open-ps-admin", "open.ps-admin", "Open", "PowerShell here (admin)" },
            new[] { "open-notepad", "open.notepad", "Open", "Open in Notepad" },
            new[] { "open-vscode", "open.vscode", "Open", "Open in VS Code" },
            new[] { "open-wsl", "open.wsl", "Open", "WSL here" },
            new[] { "apps-export", "system.apps-export", "System", "Export installed apps list" },
            new[] { "apps-import", "system.apps-import", "System", "Install apps from a list" },
            new[] { "battery-report", "system.battery", "System", "Battery report" },
            new[] { "clear-icon-cache", "system.icon-cache", "System", "Rebuild icon cache" },
            new[] { "dism-cleanup", "system.dism-cleanup", "System", "Clean up component store" },
            new[] { "flush-dns", "system.dns-flush", "System", "Flush DNS cache" },
            new[] { "power-requests", "system.power-requests", "System", "What keeps the PC awake" },
            new[] { "reset-network", "system.network-reset", "System", "Reset network stack" },
            new[] { "restart-explorer", "system.restart-explorer", "System", "Restart Explorer" },
            new[] { "setup-guide", "system.setup-guide", "System", "Setup guide\u2026" },
            new[] { "sfc-scan", "system.sfc-scan", "System", "System file check" },
            new[] { "toggle-classic", "system.classic-menu", "System", "Toggle classic right-click menu" },
            new[] { "toggle-ext", "system.toggle-ext", "System", "Toggle file extensions" },
            new[] { "toggle-hidden", "system.toggle-hidden", "System", "Toggle hidden files" },
            new[] { "update-all", "system.update-all", "System", "Update everything\u2026" },
            new[] { "library", "library.browse", "Library", "Command library\u2026" },
            new[] { "recipes-manage", "recipes.manage", "Recipes", "Manage recipes\u2026" }
        };

        public static readonly HashSet<string> Destructive = new HashSet<string>
        {
            "force-delete", "delete-empty", "clean-build", "take-ownership", "reset-network"
        };

        public static readonly HashSet<string> Grid = new HashSet<string>
        {
            "by-type", "clean-build", "duplicates", "folder-sizes", "git-pull", "git-status", "largest", "long-paths", "recent", "compare-marked", "port-who"
        };

        public static readonly Dictionary<string, string> Requires = new Dictionary<string, string>
        {
            { "git-pull", "git" }, { "git-status", "git" }, { "open-vscode", "code" }, { "apps-import", "winget" }, { "update-all", "winget" }
        };

        static Dictionary<string, string[]> byV3;

        public static string[] Find(string v3Id)
        {
            if (byV3 == null)
            {
                var d = new Dictionary<string, string[]>();
                foreach (var r in Rows) d[r[0]] = r;
                byV3 = d;
            }
            string[] row;
            return byV3.TryGetValue(v3Id, out row) ? row : null;
        }
    }
}
