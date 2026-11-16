# PS Tools v4: Cheat Sheet Spec

## 1. File: `app/cheatsheet.json`
```json
{
  "version": 1,
  "categories": [ { "id": "files", "label": "Files & folders", "icon": "E8B7" } ],
  "entries": [
    {
      "id": "files.list-recursive",
      "category": "files",
      "title": "List everything below a folder",
      "command": "Get-ChildItem -Recurse",
      "aliases": ["gci -r", "ls -R", "dir /s"],
      "explain": "Lists files and folders in this folder and all subfolders.",
      "params": [ { "name": "-Recurse", "text": "Go into subfolders" }, { "name": "-File / -Directory", "text": "Only files / only folders" }, { "name": "-Depth 2", "text": "Limit how deep" }, { "name": "-Force", "text": "Include hidden and system items" } ],
      "example": "Get-ChildItem -Recurse -File -Filter *.log | Select-Object FullName, Length",
      "exampleOutput": "FullName                      Length\n--------                      ------\nC:\\proj\\logs\\a.log            1024",
      "safeToRun": true,
      "expectError": false,
      "risk": "safe",
      "minVersion": "5.1",
      "related": ["files.largest", "search.text"],
      "keywords": ["dir", "ls", "list", "tree"],
      "actionTemplate": { "when": ["folder","background"], "output": "grid", "command": "Get-ChildItem -LiteralPath {dir} -Recurse | Select-Object FullName, Length, LastWriteTime" }
    }
  ],
  "translations": {
    "cmd":  [ { "from": "dir /s /b", "to": "Get-ChildItem -Recurse -Name", "note": "-Name returns relative paths" } ],
    "bash": [ { "from": "grep -rn \"TODO\" .", "to": "Select-String -Path (Get-ChildItem -Recurse -File).FullName -Pattern 'TODO'", "note": "Or: rg -n TODO when ripgrep is installed" } ]
  },
  "commands": ["Get-ChildItem", "Set-Location", "..."]
}
```
- `commands` = the cmdlet name list used by Run console autocomplete. Generate it once from `Get-Command -CommandType Cmdlet,Function -Module Microsoft.PowerShell.*` on PowerShell 5.1 and commit it (a script in `app/tools/gen-commands-list.ps1`).
- `actionTemplate` is optional. When present, **Save as action** uses it (with placeholders already set) instead of the raw `example`.
- `{dir}`-style placeholders are **not** allowed in `command`/`example` (those are run as-is in the fixture dir). They're allowed only in `actionTemplate.command`.

## 2. Content requirements
- ≥ 300 entries total, and ≥ 8 per category. Categories (ids): `nav`, `files`, `search`, `pipeline`, `format`, `clipboard`, `process`, `services`, `network`, `sysinfo`, `disks`, `registry`, `env`, `tasks`, `archive`, `perm`, `events`, `security`, `apps`, `git`, `strings`, `dates`, `math`, `remoting`, `profile`.
- Every entry is written for a human who is **not** a PowerShell expert: the title is a task ("Find the 20 largest files"), not a cmdlet name.
- Must-include examples (not exhaustive): the 20 largest files; folder sizes; files changed today; find text in files; replace text in many files (modifies); rename extensions in bulk (modifies); count lines of code by extension; duplicate files by hash; who is using port 3000 (`Get-NetTCPConnection -LocalPort 3000 | % { Get-Process -Id $_.OwningProcess }`); kill a process by name; services that are set to automatic but stopped; installed apps (registry uninstall keys, not Win32_Product); last boot time; disk free space; battery report; Wi-Fi passwords (`netsh wlan show profile name=X key=clear`, admin note); test a TCP port (`Test-NetConnection host -Port 443`); download a file (`Invoke-WebRequest -OutFile`); call a JSON API (`Invoke-RestMethod`); CSV import/filter/export; JSON parse/convert; clipboard get/set (`Get-Clipboard`, `Set-Clipboard`); measure command time; run as admin; environment variables and PATH (user vs machine); scheduled task create/list; event log errors in the last 24 h (`Get-WinEvent -FilterHashtable @{LogName='System';Level=2;StartTime=(Get-Date).AddDays(-1)}`); file hash; zip/unzip; ACL view/grant; take ownership (admin, destructive note); unblock downloaded files; winget search/install/upgrade; execution policy (explain, don't recommend Unrestricted); profile path and editing; modules install (`Install-Module -Scope CurrentUser`); history search (`Get-History`, PSReadLine `Ctrl+R`, history file path); string split/join/format/regex replace; dates add/format/diff; base64 / URL encode; GUID; random password (`-join ((48..57)+(65..90)+(97..122) | Get-Random -Count 16 | % {[char]$_})`); remoting `Enter-PSSession` basics; splatting; `ForEach-Object -Parallel` (minVersion 7.0).
- The bash table must have ≥ 60 rows and the cmd table ≥ 40 rows (the list is in V4_PLAN §7).

## 3. CI validation: `app/tests/cheatsheet-check.ps1 -Fixtures <dir>`
1. JSON schema check (required fields, unique ids, `related` ids exist, category exists, `minVersion` in {5.1, 7.0}).
2. **Parse**: for every `command`, `example`, translation `to` and `actionTemplate.command` (with placeholders replaced by `'C:\x'`), run `[System.Management.Automation.Language.Parser]::ParseInput($text, [ref]$null, [ref]$errors)`. Any parse error → fail with id + message. Run under Windows PowerShell 5.1 for 5.1 entries, and pwsh for 7.0 entries.
3. **Run**: for every entry with `safeToRun: true`, run `example` (or `command` when there's no example) in a child `powershell.exe -NoProfile` with cwd = `<fixtures>\tree-basic`, a 10 s timeout, `$ErrorActionPreference='Stop'`. Fail on a non-zero exit/timeout unless `expectError: true`. Network-dependent entries must set `safeToRun: false`.
4. **Safety lint**: any entry whose `command`/`example` classifies as `destructive` by RiskScan (reuse the rules: export the RiskScan table via `PSTools.exe --risk-classify-json <file>`) must have `risk: "destructive"` and `safeToRun: false`. Admin-only cmdlets must say so in `explain`.
5. Count gates: ≥ 300 entries, the per-category minimum, and the translation table sizes.
6. Output a summary table: parsed N/N, ran N, skipped N, failed N.

## 4. UI behaviour (see UI doc §4.1 Cheat sheet)
- Search matches title, command, aliases, keywords and translation `from` (so typing `grep` finds the bash row and the Select-String entries).
- **Try here** opens Run with `example` (or `command`), shell = PowerShell 5.1 (pwsh if `minVersion` 7.0 and pwsh exists, else a warning infobar), cwd = the working folder. It never auto-runs.
- **Save as action** opens SaveActionForm with `actionTemplate` (if any), title = entry title, menu = a category-mapped folder (files→`Inspect`, search→`Search`, …; map in code).
- Copy puts `command` on the clipboard. Ctrl+C on the list row does the same.
