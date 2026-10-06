# PS Tools – ultra-lean file management app for Windows 10 / 11

A single small native `PSTools.exe` (C# / WinForms on the .NET Framework 4.8 that Windows already has). It has a real GUI and its own Explorer right-click submenu. Nothing to install, no runtime download, no background process, no admin rights.

## Build (on Windows, no SDK needed)
```
cd app
build.cmd          -> app\out\PSTools.exe + commands.json
```
`build.cmd` uses `%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe`, which ships with Windows. The first build also runs `make-icon.ps1` to create `app.ico`.

## Use
- **GUI:** run `PSTools.exe`. Type, browse to or drag in a folder, pick an action, then **Run**. You can also double-click an action or press Enter.
- **Explorer menu:** click **Add to Explorer menu** in the app. Right-click a folder, folder background or drive and choose **PS Tools ▸ …**. On Windows 11 it's under *Show more options* / Shift+F10.
- **CLI:** `PSTools.exe <action-id> "<folder>"`, `PSTools.exe open "<folder>"`, `PSTools.exe --install`, `PSTools.exe --uninstall`.

## Actions (ids)
| Group | Actions |
|---|---|
| Organize | `flatten-copy`, `flatten-move`*, `organize-ext`*, `organize-month`*, `organize-type`*, `rename`* (batch rename with live preview: find/replace, regex, prefix/suffix with `{n}` / `{date}`, numbering, case, spaces→_, include folders, name filter) |
| Clean up | `delete-empty`*, `duplicates` (size → SHA-256; copies pre-ticked → Recycle Bin), `unblock` |
| Inspect | `folder-sizes`, `by-type`, `largest`, `recent`, `long-paths`: sortable/filterable table with open, show in folder, copy path, recycle ticked, export CSV |
| Export | `inventory` (inventory.csv), `copy-names`, `copy-tree` |
| PowerShell | `library`: browse/search `commands.json`, edit the snippet, copy it, or open PowerShell in the folder (elevated if `admin: true`). Never auto-runs. |

`*` = recorded in an undo journal. **Undo last** puts moved and renamed files back and recreates removed empty folders.

Safety:
- Same-name conflicts become `name_1.ext`, so nothing is overwritten.
- Deletions always go to the Recycle Bin.
- Actions that change files ask for confirmation first.
- Long jobs show progress and can be cancelled.
- Junctions/symlinks are not followed.
- Flatten refuses to run on a whole drive.

## Footprint / clean uninstall
Writes only:
- `HKCU\Software\Classes\Directory\shell\PSTools`
- `HKCU\Software\Classes\Directory\Background\shell\PSTools`
- `HKCU\Software\Classes\Drive\shell\PSTools`
- `%LOCALAPPDATA%\PSTools\undo.txt`

**Remove Explorer menu** (or `PSTools.exe --uninstall`) deletes all of it. Then delete the exe folder and nothing is left. The app is portable: if you move the exe, click "Add to Explorer menu" again so the menu points to the new location.

## Extending
- **New snippet:** edit `commands.json` (same schema: id, category, title, description, command, risk, admin, tags, note, requires).
- **New built-in action:** add an `Action` entry in `app/src/Actions.cs` with a `Run` function returning a `Result` (summary, or table rows). It appears in the GUI and, after re-adding, in the Explorer menu.

## Project structure
```
app/build.cmd, app.manifest (DPI-aware, common controls v6), make-icon.ps1, commands.json
app/src/Program.cs      entry + CLI
app/src/Actions.cs      all actions
app/src/Fs.cs           safe walking, free names, recycle, unblock, undo journal
app/src/Runner.cs       background run + progress/cancel
app/src/MainForm.cs     main window
app/src/ResultsForm.cs  result tables
app/src/RenameForm.cs   batch rename
app/src/LibraryForm.cs  commands.json browser
app/src/Shell.cs        context menu install/uninstall
```

## Not done yet / next steps
- Not compiled or tested in this environment. Build on Windows and try it on a test folder first.
- Single-file context menu (`*\shell`): hash, data URI, unblock.
- Windows 11 compact (top-level) menu needs a signed MSIX + IExplorerCommand, which is skipped to stay lean.
- Dark mode, and a settings file (for example a default flatten target).
