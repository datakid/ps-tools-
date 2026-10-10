# PS Tools

A single small native `PSTools.exe` (C# / WinForms on the .NET Framework that Windows already has). Real GUI, modular Explorer right-click menu, no installer, no background process, no admin rights for the app itself.

## Build (Windows, no SDK)
```
cd app
build.cmd          -> app\out\PSTools.exe + commands.json
```
Uses `%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe` (C# 5). The first build creates `app.ico`.

## Use
- **GUI:** run `PSTools.exe`. Pick a folder (type, Browse, or drop one), search with Ctrl+K, pick an action, Run.
- **Menu page:** tick modules or single actions, pin favorites to the top level, press **Apply to Explorer menu**.
- **CLI:** `PSTools.exe <action-id> "<path>"`, `open "<folder>"`, `--install`, `--status`, `--uninstall`.

## Modules (tick on or off on the Menu page)
| Module | Default | What it does |
|---|---|---|
| Copy | on | name, name without extension, paths (/ slashes, quoted, parent, file://, WSL, UNC, JSON), size, SHA-256/SHA-1/MD5, verify hash from clipboard, file contents, data URI, folder tree / all subfolder paths (absolute or relative), Folder structure window (tree, ASCII, markdown, indented, full or relative paths; depth, files, counts, hidden) |
| Create | on | paste clipboard image as PNG, paste text as file, folder named from clipboard, dated folder, move selection into new folder, unwrap folder, dated snapshot copy |
| Organize / Clean up / Inspect / Export | on | flatten, sort, batch rename (with Tidy), empty folders, duplicates, unblock, sizes, types, largest, recent, long paths, inventory |
| Fix | off | force delete stubborn items, set dates to now, hide/read-only, unblock files, LF/CRLF, add/remove BOM, take ownership (admin) |
| Links | off | mark, then junction / symlink / hard link here, compare with marked folder |
| Dev | off | serve folder on localhost, find removable build folders, git status / pull across repos, open PowerShell / cmd / admin / WSL / VS Code / Notepad |
| Disk | off | NTFS (LZX) compress and uncompress |
| System | off | update everything, export/import app list, restart Explorer, rebuild icon cache, toggle hidden files / extensions / classic menu, system summary, what is using a port, battery report, DNS flush, network reset, power requests, DISM cleanup, sfc |
| PowerShell / Library | on | browse `commands.json`; pin any snippet to the menu |

Multi-select works: several Explorer processes are merged into one run.

Safety tiers: read-only actions run directly; actions that change files ask first and are journaled where possible (**Undo last** covers moves, renames, new folders, pasted files, links, timestamps, attributes, text rewrites); admin actions show the exact script and need UAC. Same-name conflicts become `name_1.ext`, deletions use the Recycle Bin (except force delete and build-folder delete, which say so), junctions and symlinks are not followed.

## Footprint / clean uninstall
Writes only:
- `HKCU\Software\Classes\Directory\shell\PSTools`
- `HKCU\Software\Classes\Directory\Background\shell\PSTools`
- `HKCU\Software\Classes\Drive\shell\PSTools`
- `HKCU\Software\Classes\*\shell\PSTools`
- `%LOCALAPPDATA%\PSTools\` (settings, undo journal, marks, temporary files)
- only if you use them: the Explorer hidden-files / extensions values and the classic-menu key, tracked in `changes.txt`

`PSTools.exe --status` lists what exists. **Remove Explorer menu** removes only the menu. **Full uninstall** (or `--uninstall`) also restores the Windows settings it changed and deletes the data folder; then delete the exe folder.

## Project structure
```
src/Program.cs       entry and CLI          src/Shell.cs        menu install/uninstall/status
src/Model.cs         actions, modules       src/Settings.cs     enabled set, favorites, pins, theme
src/Actions.cs       registry of actions    src/Collector.cs    multi-select merge
src/CopyActions.cs   Copy module            src/Structure.cs    folder structure builder
src/CreateActions.cs Create, Fix, Links     src/Tweaks.cs       reversible Windows toggles
src/DevActions.cs    Dev, Disk              src/Launch.cs       PowerShell / process helpers
src/SystemActions.cs System module          src/Fs.cs           file helpers and undo journal
src/Classic.cs       Organize/Clean/...     src/Runner.cs       run, progress, results
src/MainForm.cs, ResultsForm.cs, RenameForm.cs, LibraryForm.cs, StructureForm.cs, ToolForms.cs, UI.cs
```

## Extending
- **New action:** add a `new Action { Id, Group, Label, Hint, Targets, Run }` to a module list. It appears in the GUI and on the Menu page. Set `Tier.Confirm` plus `Confirm` text for anything that changes files; set `Multi` to receive every selected item; set `Fast` for instant clipboard-style actions.
- **New snippet:** edit `commands.json`, then pin it from the library window.
