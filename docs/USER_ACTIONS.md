# Your own actions

Put a script in the actions folder and it appears in the Power menu, the Actions page and (if you star it) the Explorer right-click menu.

**Where:** `%LOCALAPPDATA%\PSTools\actions\`. Settings, Actions folder, Open folder takes you there. Subfolders become menu folders: `actions\Copy\Git\Branch.ps1` shows up under Copy, then Git. After adding or changing a file, press Reload in Settings (or restart PS Tools). Mistakes never crash anything: a bad file is skipped and listed under Settings, Actions folder, Problems with the file and the reason.

## The header

A PowerShell script starts with a comment block of `@key value` lines. A `.cmd` or `.bat` script uses `:: @key value` lines instead. Every key is optional.

| Key | Meaning | Default |
|---|---|---|
| `@id` | Stable name, lowercase with dots | `user.` plus the file path |
| `@title` | Menu label, short | the file name |
| `@menu` | Folder path in the menu, `Copy/Git` | the folder the file is in |
| `@when` | `file`, `folder`, `background` (empty space), `drive`, `desktop` | file, folder, background |
| `@output` | `clipboard`, `toast`, `grid`, `console`, `none` | `console` |
| `@risk` | `safe` runs at once, `modifies` asks once, `destructive` always asks and shows the script | `modifies` |
| `@multi` | `join` one run for all selected items, `each` one run per item, `single` hide when several are selected | `join` |
| `@requires` | Tools that must be installed, for example `git`, `wt`, `code` | none |
| `@ext` | Only show for these extensions, `png, jpg` | any |
| `@keywords` | Extra words the search matches | none |
| `@description` | One sentence shown in the details | a generic line |
| `@admin` | `true` runs elevated (always asks) | false |
| `@shell` | `pwsh` to use PowerShell 7 | Windows PowerShell 5.1 |
| `@extended` | `true` shows it only while Shift is held | false |

## Placeholders

| Placeholder | Becomes |
|---|---|
| `{dir}` | the folder you are in (or the right-clicked folder), quoted |
| `{path}` | the selected item (the first one if several), quoted. Use `@multi each` to run the script once per selected item |
| `{paths}` | all selected items as a PowerShell array, `@('a','b')` (space separated and quoted in cmd scripts) |
| `{name}`, `{stem}`, `{ext}`, `{parent}` | parts of the selected item |
| `{clip}` | the clipboard text (up to 100 KB) |
| `{date}`, `{time}`, `{now}` | the current date `2026-11-15`, time `14-30-05` or both `2026-11-15_14-30-05`, safe to use in file names |
| `{gitroot}` | the git root of the folder |
| `{input:Label}` | asks you for a value first and uses what you type |

Write placeholders without quotes of your own, for example `Get-ChildItem -LiteralPath {dir}`. PS Tools quotes the values correctly, even for paths with spaces, quotes or `%`.

## Ten examples

Each example is a file in `docs/examples/actions/` of this repository. CI loads all of them with the real parser, so they work as shown. Copy a block into a new file with the name in the heading.

### 1. Copy the current git branch

Copies the branch name of the folder you are in. Shows only when git is installed.

File: `Copy\Git-branch.ps1`

```powershell
<#
@id        user.copy.git-branch
@title     Git branch name
@menu      Copy/Git
@when      folder, background
@output    clipboard
@risk      safe
@requires  git
@keywords  branch, git
@description Copies the current git branch of this folder.
#>
git -C {dir} rev-parse --abbrev-ref HEAD
```

### 2. Copy the SHA-256 of a file

Works on one selected file. `multi: single` hides it when you select several.

File: `Copy\File-hash.ps1`

```powershell
<#
@id        user.copy.sha256
@title     SHA-256 of the file
@menu      Copy
@when      file
@output    clipboard
@risk      safe
@multi     single
@keywords  hash, checksum
@description Copies the SHA-256 hash of the selected file.
#>
(Get-FileHash -LiteralPath {path} -Algorithm SHA256).Hash
```

### 3. Copy the last 10 commits

A one-line summary, handy for a chat message.

File: `Copy\Git-last-commits.ps1`

```powershell
<#
@id        user.copy.git-last-commits
@title     Last 10 commits
@menu      Copy/Git
@when      folder, background
@output    clipboard
@risk      safe
@requires  git
@keywords  log, history, commits
@description Copies a one-line summary of the last 10 commits in this folder.
#>
git -C {dir} log --oneline -10
```

### 4. Open Windows Terminal in this folder

`output: none` means nothing is shown afterwards.

File: `Dev\Open-in-terminal.ps1`

```powershell
<#
@id        user.dev.windows-terminal
@title     Open in Windows Terminal
@menu      Dev
@when      folder, background
@output    none
@risk      safe
@requires  wt
@keywords  terminal, shell
@description Opens a Windows Terminal tab in this folder.
#>
wt -d {dir}
```

### 5. Count lines of code

Shows a table, one row per file type.

File: `Dev\Count-lines-of-code.ps1`

```powershell
<#
@id        user.dev.lines-of-code
@title     Count lines of code
@menu      Dev
@when      folder, background
@output    grid
@risk      safe
@keywords  loc, lines, code size
@description Counts files and lines per file type in this folder, ignoring node_modules and .git.
#>
Get-ChildItem -LiteralPath {dir} -Recurse -File -Include *.cs,*.js,*.ts,*.py,*.ps1 |
    Where-Object { $_.FullName -notmatch '\\(node_modules|\.git)\\' } |
    Group-Object Extension |
    ForEach-Object { [pscustomobject]@{ Type = $_.Name; Files = $_.Count; Lines = ($_.Group | Get-Content | Measure-Object -Line).Lines } }
```

### 6. Find files bigger than a size you type

`{input:Minimum size in MB}` asks you for a value before it runs.

File: `Inspect\Big-files.ps1`

```powershell
<#
@id        user.inspect.big-files
@title     Files bigger than...
@menu      Inspect
@when      folder, background
@output    grid
@risk      safe
@keywords  large, size, space
@description Lists the files in this folder tree that are bigger than the size you type, in megabytes.
#>
Get-ChildItem -LiteralPath {dir} -Recurse -File |
    Where-Object { $_.Length -gt ([int]'{input:Minimum size in MB}' * 1MB) } |
    Sort-Object Length -Descending |
    Select-Object FullName, @{ n = 'MB'; e = { [math]::Round($_.Length / 1MB, 1) } }
```

### 7. Files changed in the last N days

Another action that asks for a number.

File: `Inspect\Changed-recently.ps1`

```powershell
<#
@id        user.inspect.changed-recently
@title     Changed in the last N days
@menu      Inspect
@when      folder, background
@output    grid
@risk      safe
@keywords  modified, recent, new files
@description Lists files changed in the last number of days that you type.
#>
Get-ChildItem -LiteralPath {dir} -Recurse -File |
    Where-Object { $_.LastWriteTime -gt (Get-Date).AddDays(-[int]'{input:Days}') } |
    Sort-Object LastWriteTime -Descending |
    Select-Object FullName, LastWriteTime
```

### 8. A cmd script

`.cmd` and `.bat` files use `:: @key value` lines at the top.

File: `Inspect\Listening-ports.cmd`

```bat
:: @id        user.inspect.listening-ports
:: @title     Listening ports
:: @menu      Inspect
:: @when      folder, background
:: @output    console
:: @risk      safe
:: @keywords  netstat, port, server
:: @description Shows which programs are listening on a port.
netstat -ano | findstr LISTENING
```

### 9. Make a dated copy of a folder

`risk: modifies` makes PS Tools ask once before it runs. `multi: each` runs it once per selected folder.

File: `Clean\Dated-backup.ps1`

```powershell
<#
@id        user.clean.dated-backup
@title     Dated backup copy
@menu      Clean up
@when      folder
@output    toast
@risk      modifies
@multi     each
@keywords  backup, snapshot, copy
@description Copies the folder next to itself with today's date in the name. The original is not changed.
#>
Copy-Item -LiteralPath {path} -Destination ({path} + ' ' + (Get-Date -Format 'yyyy-MM-dd')) -Recurse
'Backup created'
```

### 10. Delete empty folders

`risk: destructive` always asks, and shows the exact script. Undo does not cover scripts.

File: `Clean\Delete-empty-folders.ps1`

```powershell
<#
@id        user.clean.delete-empty-folders
@title     Delete empty folders
@menu      Clean up
@when      folder, background
@output    toast
@risk      destructive
@keywords  empty, cleanup
@description Deletes every empty folder below this one. Undo does not cover this.
#>
$gone = 0
Get-ChildItem -LiteralPath {dir} -Recurse -Directory |
    Sort-Object { $_.FullName.Length } -Descending |
    Where-Object { -not (Get-ChildItem -LiteralPath $_.FullName -Force) } |
    ForEach-Object { Remove-Item -LiteralPath $_.FullName; $gone++ }
"Deleted $gone empty folders"
```

## Tips

- To turn a command you already ran into an action, use Save as action in the Run console or the Cheat sheet. It writes a file like these for you.
- To change an action that PS Tools ships, press Duplicate on the Actions page. Your copy goes into this folder and you can edit it.
- To hide a built-in action, press Hide on the Actions page. Settings, Actions folder, Show hidden actions brings them back.
- Put test scripts in a folder of their own and use `@risk safe` only for scripts that read and never change anything.
