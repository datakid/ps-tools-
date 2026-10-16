# PS Tools

A single small native `PSTools.exe` (C# / WinForms on the .NET Framework that Windows already has). Real GUI, a curated Explorer right-click menu, no installer, no background process, no admin rights for the app itself.

## Build (Windows, no SDK)
```
cd app
build.cmd          -> app\out\PSTools.exe + commands.json
```
Uses `%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe` (C# 5). The first build creates `app.ico`.

## First run
Run `PSTools.exe` with no arguments. One dialog asks how much to put in the right-click menu:

| Preset | What you get |
|---|---|
| Minimal | 9 entries: copy name and path, SHA-256, paste image or text as a file, download a link, search, open PowerShell, undo |
| Everyday (default) | Minimal plus zip, extract, image tools, folder structure, dated folders, group into folder, duplicates, batch rename, subfolder sizes |
| Developer | Everyday plus all Dev tools (terminals, VS Code, local server, git across repos) and Links |
| Everything | Every action that does not need administrator rights |

On Windows 11 the dialog also offers the classic right-click menu, because Windows 11 only shows registry-based menus under "Show more options". The switch is tracked and reverted by a full uninstall. Choosing Not now installs nothing and the dialog does not return.

## Use
- **Right-click:** the flat entries at the top are the daily ones (paste image or text as a file, download link, search, undo, zip). Everything else is grouped in submenus. Image entries appear for pictures, Extract for zip files. Rare or risky entries (force delete, take ownership, restart Explorer) show only with Shift+right-click.
- **GUI:** the left rail switches between **Actions** and **Menu and settings** and opens **Recipes**, **Undo history**, **Space map** and the **Setup guide**. Pick a folder (type, Browse, or drop one), pick an action, Run. **Ctrl+K** opens the command palette (type to find any action, recently used ones first), **Ctrl+F** searches the list, Ctrl+1 and Ctrl+2 (or Ctrl+Tab) switch pages. The window remembers its size and position, and scales with Windows display scaling.
- **Menu page:** pick where you right-click (**Folder**, **Inside a folder**, **File**, **Drive**). The left card is the top of the menu for that context, an ordered list of up to 12 pinned actions: **Add...** opens a menu of modules and their actions (or restores the suggested pins), **Up** and **Down** reorder, **Unpin** removes. The middle card lists every action that applies there, grouped by module (click a group header to fold it); the checkbox shows it in the menu, the star pins it for that context (pinning also enables it), double-click or Enter pins the selected action, and a group header's checkbox and star show or pin the whole submenu. The right card previews exactly what Explorer will show (the preview and the real menu are built from the same plan), with a **Holding Shift** switch. Every change is saved at once and the installed Explorer menu is updated about a second later; there is no Apply button. If the menu is not installed yet, **Install Explorer menu** appears. **Setup guide** reopens the first-run dialog any time. Choosing a preset asks **Keep your pinned actions?** so pins are never lost silently.
- **Actions page:** the list starts with **Suggested here**: actions that fit the folder you picked (pictures to shrink, zip files, build folders, git repositories, project files, large folders, likely duplicates) and what is on the clipboard (image, link, text). Suggestions are scanned in the background (top level plus one level down, at most 3000 entries and 400 ms, links and junctions skipped) and only show in the window and in the Ctrl+K palette; the Explorer menu itself is static. Selecting an action shows what it does, whether it changes files, whether Undo covers it, where it appears in the right-click menu, and the **PowerShell equivalent**. **Copy command** copies it with the folder from the box filled in; **Open PowerShell with command** opens PowerShell in that folder with the command on the clipboard (paste with a right-click, nothing runs by itself). **Pin...** pins the selected action to any context it applies to.
- **Undo:** actions that change files show a toast with an **Undo** link for 5 seconds. **Undo last PS Tools action** is also in the folder background menu, and **Undo last** and **History...** are in the window.
- **CLI:** `PSTools.exe <action-id> "<path>"`, `open "<folder>"`, `--install`, `--status`, `--repair`, `--uninstall`, `--selftest` (internal checks, exit code 0 when all pass) and `--dump-snippets` (every PowerShell equivalent as JSON). Add `--quiet` to print to the console instead of showing windows; actions that need a confirmation are not run in quiet mode.

## Modules
| Module | What it does |
|---|---|
| Copy | name, name without extension, paths (/ slashes, quoted, parent, file://, WSL, UNC, JSON), size, SHA-256/SHA-1/MD5, verify hash from clipboard, file contents, data URI, folder tree / all subfolder paths, Folder structure window |
| Create | paste clipboard image as PNG, paste text as file, download link from clipboard, folder named from clipboard, dated folder, move selection into new folder, unwrap folder, dated snapshot copy, undo last |
| Archive | zip that leaves out `.git`, `node_modules`, `.vs`, `__pycache__`, `bin`, `obj` and says so; extract a zip into its own folder (single top folder is not nested twice, unsafe entries are skipped) |
| Image | convert to PNG or JPEG, shrink to 1600 or 800 px, strip metadata (EXIF, location). Copies are written next to the originals; nothing is overwritten |
| Organize / Clean up / Inspect / Export | flatten, sort, batch rename, empty folders, duplicates, unblock, search here (names and text inside files), sizes, **folder space map**, types, largest, recent, long paths, inventory |
| Fix | force delete stubborn items, set dates to now, hide/read-only, unblock files, LF/CRLF, add/remove BOM, take ownership (admin) |
| Links | mark, then junction / symlink / hard link here, compare with marked folder |
| Dev | serve folder on localhost, find removable build folders, git status / pull across repos, open PowerShell / cmd / admin / WSL / VS Code / Notepad |
| Disk | NTFS (LZX) compress and uncompress |
| System | update everything, export/import app list, restart Explorer, rebuild icon cache, toggle hidden files / extensions / classic menu, system summary, what is using a port, battery report, DNS flush, network reset, power requests, DISM cleanup, sfc |
| Recipes | your own chains of actions, run in order with one confirmation and one Undo (see below) |
| PowerShell / Library | browse `commands.json`; run or pin any snippet |

Multi-select works: several Explorer processes are merged into one run.

## Look and feel
PS Tools follows Windows 11 design: light or dark from your system theme (override it on the Menu page), your Windows accent color, Segoe UI Variable and the Fluent icon font when installed (Segoe UI and Segoe MDL2 Assets on Windows 10), rounded cards and controls (including checkboxes, radio buttons, drop-downs and dialogs, in dark mode too), a navigation rail on the left, and a title bar that matches the window. Everything is drawn in code: no images, fonts or runtime files ship with it, it only reads your theme and accent color from the registry, and it writes nothing there. Keyboard: Tab into the rail, Up and Down to move, Enter to open; Ctrl+1 and Ctrl+2 switch pages; every button shows a focus ring.

## Undo history
**History...** (in the window, in the Ctrl+K palette, and in the folder background menu as **Undo history...**) lists the last 10 operations PS Tools made, newest first. Select one to see exactly what undoing it will do. Undo goes in order, so **Undo this and the N above it** also undoes everything newer. A recipe counts as one operation. The history is capped at 10 operations and 1 GB of backups; when a new operation pushes it over, the oldest entries and their backups are deleted. **Clear history** deletes all of it. The old single `undo.txt` from earlier versions is migrated into the history automatically.

## Folder space map
**Folder space map...** (Inspect module, also in the right-click menu) scans a folder and draws it as a map of colored tiles, each sized by how much space it takes. Click a folder to open it, Backspace goes up. Right-click a tile to open it, show it in Explorer, copy its path or move it to the Recycle Bin (after asking; you can restore it from the Recycle Bin). Colors show the kind of file: images, video, audio, archives, documents, code, programs. Each folder keeps its 80 largest files as tiles and groups the rest. Links and junctions are skipped. Cancel during a scan shows what was found so far. Nothing is changed unless you choose the Recycle Bin.

## Recipes
A recipe is a named list of actions that run one after another on the same selection. Open **Manage recipes...** (in the action list, the Ctrl+K palette, or the Recipes module) to create, edit, copy, import and export them.

- **One OK, one Undo:** if any step asks for confirmation, you see all the steps and answer once. **Undo last** reverts the whole recipe, not just the last step.
- **Same selection:** every step runs on the items you picked. A step does not receive files an earlier step created, but it sees the changes earlier steps made to your items (sort, then remove empty folders; clean, then zip).
- **Right-click menu:** tick *Show in the right-click menu* and save; the Explorer menu is updated automatically if it is installed, and the recipe appears in a **Recipes** submenu. Recipes work for whatever kinds of item all their steps support.
- **Which actions can be steps:** the ones that do their work without opening a window or asking a question while running. Result lists (find duplicates, folder sizes, git status), the clipboard-paste actions, the administrator actions and the System module are not offered. If several steps copy to the clipboard, the last one wins.
- **Where they live:** `recipes.json` next to `PSTools.exe`, in the same spirit as `commands.json`. It is plain JSON you can edit or share, and Full uninstall does not delete it. If the exe is in a protected folder such as Program Files, saving fails with a message; move the exe somewhere you can write to.

## Command library
`commands.json` holds PowerShell snippets. `{path}` in a snippet becomes the folder you are in (or right-clicked), quoted for a single-quoted PowerShell string, and PowerShell starts in that folder.
- **Run here** (library window) and pinned snippets run in a PowerShell window that stays open.
- Snippets marked `safe` start immediately. Snippets that `modifies` or are `destructive`, and admin snippets, first show the exact script and wait for **Run in PowerShell**.
- **Copy** and **Open PowerShell here** put the filled-in command on the clipboard without running it.

## Safety tiers
Read-only actions run directly; actions that change files ask first and are journaled where possible (**Undo** covers moves, renames, new folders, pasted and downloaded files, zips, extractions, converted images, links, timestamps, attributes, text rewrites); admin actions show the exact script and need UAC. Same-name conflicts become `name_1.ext`, deletions use the Recycle Bin (except force delete and build-folder delete, which say so), junctions and symlinks are not followed. Undo keeps a history of the last 10 operations (see below).

## Footprint / clean uninstall
Writes only:
- `HKCU\Software\Classes\Directory\shell\PSTools`
- `HKCU\Software\Classes\Directory\Background\shell\PSTools`
- `HKCU\Software\Classes\Drive\shell\PSTools`
- `HKCU\Software\Classes\*\shell\PSTools`
- `%LOCALAPPDATA%\PSTools\` (settings, undo history in `undo\` and its backups in `undo-data\`, marks, temporary files)
- only if you use them: the Explorer hidden-files / extensions values and the classic-menu key, tracked in `changes.txt`

`PSTools.exe --status` lists what exists, the active preset, and whether the menu still points at this exe. **Remove Explorer menu** removes only the menu. **Full uninstall** (or `--uninstall`) also restores the Windows settings it changed and deletes the data folder; then delete the exe folder.

If you move or rename the exe folder, the menu entries would point at the old path. Starting the exe from its new place re-creates them automatically; `--repair` does the same on demand.

`app\tools\layout-check.ps1` opens the main window (both pages, at the minimum and default sizes), the menu preview and the first-run dialog off the live exe and fails if any control is clipped or overflowing. It only reads: it installs nothing and writes no settings.

`app\tools\footprint-check.ps1` proves the contract on your machine: it snapshots the registry classes, the data folder and the Explorer values, installs, runs an action, uninstalls, and fails if anything differs. Run it on an account where PS Tools is not installed. `-IncludeTweaks` also toggles the hidden-files and extensions settings to check they are restored.

## Project structure
```
app/src/Program.cs       entry and CLI          app/src/Shell.cs        menu install/repair/uninstall/status
app/src/Model.cs         actions, modules,      app/src/Settings.cs     enabled set, pins per context, theme
                         presets                app/src/Collector.cs    multi-select merge
app/src/Actions.cs       registry of actions    app/src/Structure.cs    folder structure builder
app/src/CopyActions.cs   Copy module            app/src/Tweaks.cs       reversible Windows toggles
app/src/CreateActions.cs Create, Fix, Links     app/src/Launch.cs       PowerShell / process helpers
app/src/QuickActions.cs  undo, download, zip,   app/src/Fs.cs           file helpers
                         extract, images        app/src/Journal.cs      undo history
                         extract, images        app/src/Runner.cs       run, progress, results
app/src/DevActions.cs    Dev, Disk
app/src/SystemActions.cs System module
app/src/Classic.cs       Organize/Clean/...
app/src/MainForm.cs, FirstRunForm.cs, SearchForm.cs, ResultsForm.cs, RenameForm.cs, LibraryForm.cs, StructureForm.cs, ToolForms.cs, UI.cs
app/src/PaletteForm.cs  Ctrl+K command palette       app/src/PreviewForm.cs  right-click menu preview
app/src/MenuPlan.cs     planned menu (shared)        app/src/Suggest.cs      suggestions for the folder
app/src/PsCatalog.cs    PowerShell equivalents       app/src/SelfTest.cs     --selftest and --dump-snippets
app/src/Recipes.cs      recipe data and runner       app/src/RecipesForm.cs  recipe editor and step picker
app/src/UndoHistoryForm.cs history window         app/src/Space.cs        space map scanner and treemap
app/src/SpaceForm.cs    space map window             app/src/MapActions.cs   history and space map actions
app/src/Fluent.cs       Windows 11 style controls (buttons, cards, rail, list rows, glyph helper)
app/tools/footprint-check.ps1
app/tools/layout-check.ps1
app/tools/snippet-check.ps1
```

## Extending
- **New action:** add a `new Action { Id, Group, Label, Hint, Targets, Run }` to a module list. It appears in the GUI and on the Menu page. Set `Tier.Confirm` plus `Confirm` text for anything that changes files; set `Multi` to receive every selected item; set `Fast` for instant clipboard-style actions; set `AppliesTo` (an Explorer query such as `Modules.ZipQuery`) to show it only for matching files; set `Extended` to show it only with Shift+right-click; add its id to `DefaultPins.For` in `Model.cs` to pin it at the top of a context by default; add its PowerShell equivalent (or the reason there is none) to `PsCatalog.cs`, which `--selftest` and `app\tools\snippet-check.ps1` verify. Journal anything that creates or changes files so Undo works. To include it in a preset, add its id to the lists in `Presets` in `Model.cs`.
- **New recipe:** use Manage recipes, or edit `recipes.json` (`{ "recipes": [ { "id", "name", "description", "menu", "steps": [ "<action-id>", ... ] } ] }`). Action ids are the `Id` values in the module lists.
- **New snippet:** edit `commands.json` (use `{path}` for the folder), then run or pin it from the library window.
