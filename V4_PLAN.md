# PS Tools v4: Master Rebuild Plan

> Audience: the implementing agent (Claude Sonnet) working directly on `github.com/datakid/ps-tools-` with a GitHub token.
> This plan builds on the real v3 code (`app/src/*.cs`, C# 5, WinForms, built by `app/build.cmd` with the .NET Framework `csc.exe`).
> Read this whole file, then `docs/v4/UI_SPEC_AND_TESTING.md`, `docs/v4/POWER_ACTIONS.md`, `docs/v4/CHEATSHEET_SPEC.md`, before writing any code.

---

## 0. The point of v4 (one paragraph)

v3 can do a lot, but you have to manage it: four contexts, presets, pins per context, module checkboxes, a preview, and a 12-pin limit. Every new action means more menu upkeep. **v4 removes menu upkeep completely.** The right-click menu is generated from one **action tree**. The folder structure of the tree *is* the menu. You never edit the menu itself. v4 also adds three things that make it a real power tool:
1. a **Run console**, where you type any PowerShell / pwsh / cmd command and run it in the folder you right-clicked;
2. **Save as action**, which turns any command (typed, from history, or from the cheat sheet) into a right-click action in one dialog;
3. a full **cheat sheet** of PowerShell commands you can search, try and turn into actions.

On top of that comes a set of **power actions** (tree to clipboard, copy for AI, who is locking this file, paste a tree to make folders, and more). These are the things terminal users do easily and Explorer users can't.

**North-star test:** a user right-clicks a folder, presses `T`, `A`, Enter, and has an ASCII tree of the folder on the clipboard in under a second. They never opened a settings page to make that possible.

---

## 1. Ground rules for the implementing agent (non-negotiable)

### 1.1 Language/toolchain constraints (v3 contract, keep it)
- The build is `app/build.cmd`, which uses `%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe`. That compiler is **C# 5**. CI must build with this exact compiler, so any newer syntax fails the build.
- **Forbidden syntax** (C# 6+). Never write these:
  - string interpolation `$"..."` → use `string.Format` or `+`
  - null-conditional `a?.b`, `a?[i]` → explicit null checks
  - `nameof(...)`
  - expression-bodied members `int X => 1;` / `void F() => ...;`
  - auto-property initializers `public int X { get; set; } = 1;`
  - `using static`
  - exception filters `catch (E) when (...)`
  - `out var`, tuples `(a, b)`, pattern matching `is T t`, `switch` expressions, local functions, `default` literal, `?? throw`
  - `await` in catch/finally
- Allowed: LINQ, lambdas, `var`, generics, `async/await` (C# 5 has it, but prefer `Thread` + `BeginInvoke` as v3 does).
- Allowed references only (already in build.cmd): System, System.Core, System.Drawing, System.Windows.Forms, Microsoft.VisualBasic, System.Web.Extensions (JSON via `JavaScriptSerializer`), System.IO.Compression(.FileSystem). You **may add** `UIAutomationClient.dll` and `UIAutomationTypes.dll` **only in test tools**, never in the app.
- No NuGet, no third-party DLLs, no installer. Output stays a single `PSTools.exe` plus JSON data files.
- Keep the v3 footprint contract (HKCU keys + `%LOCALAPPDATA%\PSTools\`). `footprint-check.ps1` must still pass.

### 1.2 Git / token discipline
- The token lives **only** in an environment variable (`GH_TOKEN`). Never print it, never write it to a file, never put it in a commit, a workflow or a PR body. If a command echoes it, stop and redact.
- Never push to `main` directly. One branch per phase: `v4/p0-ci-harness`, `v4/p1-action-tree`, … Open one PR per phase, with a description that follows the PR template in §12.
- Never force-push a branch that has review comments. Never delete branches you didn't create.
- Every PR must show a green CI run (§10) **and** a screenshot gallery (§ UI doc). If CI is red, fix it. Don't disable checks, skip tests, or lower thresholds to make it pass. Raising a visual-diff threshold needs a written reason in the PR.
- Commit messages: `p<phase>: <imperative summary>`. Keep commits small and logical.

### 1.3 Working method
1. Read the v3 file you are about to change in full before editing (especially `MainForm.cs` at 60 KB, `Fluent.cs` at 53 KB, `Settings.cs`, `Collector.cs`, `UI.cs`). Reuse `Fluent.cs` controls (`FButton`, `FProgress`, cards, rail, list rows, glyph helper). **Do not invent a second control library.**
2. Write or extend the test **before or with** the feature. A feature with no automated check is not done.
3. After every UI change, generate screenshots, **look at every one yourself** (you can read images), fill in the review rubric, and fix what you see before opening the PR.
4. If a Windows behavior in this plan turns out to be wrong when you test it (for example how Explorer passes multi-selection), write that down in `docs/v4/FINDINGS.md`, adapt, and say so in the PR. Never silently diverge.

---

## 2. What exists in v3 (so you don't rebuild what works)

| Area | v3 file(s) | Keep / change |
|---|---|---|
| Action model `class Action { Id, Label, Group, Hint, Tier, Targets, Multi, Fast, Extended, AppliesTo, Run, Window, Script, Ps }` | `Model.cs` | **Extend** into `ActionSpec` (§4). Keep `Run/Window/Script` delegates for built-ins. |
| Action registry, modules in fixed order | `Actions.cs`, `*Actions.cs`, `Classic.cs` | Keep the implementations. Replace the flat `Group` with a **tree path** (`Menu = "Copy/Tree"`). |
| Menu plan (pins + groups per context) | `MenuPlan.cs` | **Replace** with `MenuTree` (§5). |
| Registry writer `Shell.Install()`, 4 roots, `SubCommands`, `AppliesTo`, `Extended` | `Shell.cs` | **Rewrite** `Write()` to emit the minimal launcher plus favorites (§5.3). Keep `RemoveMenu`, `Status`, `HealIfMoved`, `UninstallAll`. |
| Presets, DefaultPins, per-context pins | `Model.cs`, `Settings.cs`, `FirstRunForm.cs`, `PreviewForm.cs` | **Remove presets.** Migrate pins → favorites (§9). FirstRun becomes one screen (§ UI doc). |
| Runner (confirm, progress thread, result → clipboard/toast/grid) | `Runner.cs`, `ResultsForm.cs` | Keep. Add output modes `console` and the script-action path (§6). |
| PowerShell launch helpers (`-EncodedCommand`, Capture with cancel/timeout) | `Launch.cs` | Keep. Add `Stream()` for live console output and `KillTree()` (§6). |
| Multi-select merge | `Collector.cs` | Keep. The popup uses it (§5.2). |
| Undo journal, history | `Journal.cs`, `UndoHistoryForm.cs` | Keep as is. |
| Recipes | `Recipes.cs`, `RecipesForm.cs` | Keep. Recipes become tree nodes under `Recipes/`. |
| Command library `commands.json` (`id, category, title, description, command, risk, admin, tags, note`, `{path}` placeholder) | `LibraryForm.cs`, `commands.json` | **Fold into** the action tree as `Library/<category>/…` and into the cheat sheet. Keep the file format readable (§4.4). |
| Ctrl+K palette | `PaletteForm.cs` | **Generalize** into the Power Menu popup control (§5.2). One control is used in both places. |
| Suggestions | `Suggest.cs` | Keep. Feeds the "Suggested" section of the popup. |
| Self-test, snippet dump | `SelfTest.cs`, `tools/snippet-check.ps1` | Keep and extend. |
| Layout check via reflection | `tools/layout-check.ps1` | Keep and extend into the UI test suite (UI doc §6). |

---

## 3. Target architecture (v4)

```
                ┌──────────────────────────── action sources ────────────────────────────┐
                │ Built-ins (C#)    actions.json (shipped)   commands.json (library)        │
                │ user folder %LOCALAPPDATA%\PSTools\actions\  (*.json, *.ps1, *.cmd, *.bat)│
                │ recipes.json                                                              │
                └───────────────┬───────────────────────────────────────────────────────────┘
                                ▼
                      ActionCatalog.Load()      (merge, precedence, validation, aliases)
                                ▼
                      MenuTree.Build(context, selection, shiftHeld)
                     ┌──────────┼─────────────────────────┬─────────────────────┐
                     ▼          ▼                         ▼                     ▼
             PowerMenu popup   Explorer registry       Main window pages      CLI
             (right-click)     (launcher + favorites)  Home/Actions/Run/      PSTools.exe <id> "<path>"
                                                       Cheat sheet/Settings
                                ▼
                      Executor.Run(spec, paths, args)  →  builtin | powershell | pwsh | cmd
                                ▼
                      Output: clipboard | toast | grid | console | window | none   (+ Journal/Undo)
```

New files (suggested names, keep one class per concern):

| File | Purpose |
|---|---|
| `src/ActionSpec.cs` | v4 action model + enums (`Shell`, `Output`, `Risk`, `MultiMode`, `ContextKind`) |
| `src/ActionCatalog.cs` | load/merge/validate every source; aliases; `Find(id)`; reload on file change |
| `src/ScriptHeader.cs` | parse `<# @name … #>` / `:: @name …` headers of user script files |
| `src/Placeholders.cs` | expand `{path}` etc. with correct quoting per shell (§6.3) |
| `src/Executor.cs` | run script actions (prelude, wrapper, streaming, objects→grid) |
| `src/MenuTree.cs` | build the filtered tree for a context and selection; Recent/Suggested sections |
| `src/PowerMenu.cs` | borderless popup form + reusable `TreeListControl` (search, drill-in, keyboard) |
| `src/RunConsole.cs` | Run page control (editor, shell picker, output, history, Save as action) |
| `src/SaveActionForm.cs` | "Save as action" dialog |
| `src/CheatSheet.cs`, `src/CheatSheetPage.cs` | load `cheatsheet.json`, page UI |
| `src/HistoryImport.cs` | read PSReadLine history files |
| `src/RiskScan.cs` | heuristic danger detection for typed commands |
| `src/UiHarness.cs` | `--screenshot`, `--ui-scenario`, deterministic theme (UI doc) |
| `src/TestSink.cs` | clipboard/toast/dialog capture for `--test-actions` |
| `app/actions.json` | shipped declarative actions (the JSON power actions) |
| `app/cheatsheet.json` | cheat sheet content |
| `app/tests/**` | fixtures, goldens, UI baselines, scripts |
| `.github/workflows/ci.yml`, `release.yml`, `ui-gallery.yml` | CI (§10) |

---

## 4. The unified action model

### 4.1 `ActionSpec` fields (exact)

| Field | Type | Required | Meaning |
|---|---|---|---|
| `id` | string | yes | Dotted, lowercase, stable: `copy.tree.ascii`. Regex `^[a-z0-9]+(\.[a-z0-9-]+)+$` |
| `aliases` | string[] | no | Old v3 ids (`"copy-tree"`). The CLI and old registry entries resolve through these. |
| `title` | string | yes | Menu label, ≤ 32 chars, sentence case, no trailing "…" unless it opens a window/asks input |
| `menu` | string | yes | Tree path, `/`-separated, ≤ 3 levels: `"Copy/Tree"`. Root items use `""`. |
| `order` | int | no | Sort within its folder (default 500). Ties → title. |
| `when` | string[] | yes | Any of `file`, `folder`, `background`, `drive`, `desktop` (desktop = background of Desktop) |
| `match.ext` | string[] | no | Only show when **every** selected file has one of these extensions |
| `match.kind` | `file`/`folder`/`any` | no | Narrower than `when` for mixed selections |
| `match.min`/`match.max` | int | no | Selection count bounds (max 0 = unlimited) |
| `requires` | string[] | no | Tools that must be on PATH: `git`, `code`, `wt`, `pwsh`, `ffmpeg`, `rg`, `magick`, `node`, `python`, `winget`, `tar`. Hidden when missing (cached lookup, refreshed at most every 10 min). |
| `shell` | `builtin`/`powershell`/`pwsh`/`cmd` | yes | `pwsh` falls back to `powershell` if pwsh missing **only** if `fallback: true` |
| `builtin` | string | if builtin | Key into the C# built-in table |
| `command` | string | if script | Script text with placeholders (§6.3) |
| `inputs` | object | no | Named prompts: `{"depth": {"label":"Depth","type":"pick","options":["1","2","3","all"],"default":"all"}}` types: `text`, `pick`, `number`, `bool`, `folder`, `file` |
| `output` | enum | yes | `clipboard` (stdout → clipboard + toast), `toast` (first line), `grid` (objects → table), `console` (live output window that stays), `window` (builtin opens a form), `none` |
| `risk` | `safe`/`modifies`/`destructive` | yes | safe = run immediately; modifies = confirm once (unless user turned off for that id); destructive = always confirm and show the exact script |
| `admin` | bool | no | Runs elevated (UAC); always confirm with the script shown |
| `multi` | `join`/`each`/`single` | no | join = one run with all paths; each = run per item; single = hide when >1 selected. Default `join` |
| `undo` | `none`/`journal` | no | Built-ins only (scripts are `none`, and the confirm dialog says "Undo does not cover this") |
| `extended` | bool | no | Only visible with Shift held |
| `icon` | string | no | Segoe Fluent/MDL2 code point hex (`"E8C8"`). Default per top folder (UI doc §3.5) |
| `keywords` | string[] | no | Extra search terms (`"tree"`, `"structure"`, `"ls -R"`) |
| `description` | string | yes | One sentence, what happens + where the result goes |
| `ps` | string | no | PowerShell equivalent shown in details (for builtins) |
| `source` | (set by loader) | – | `builtin`/`shipped`/`library`/`user`/`recipe` + file path |
| `test` | object | no | `{ "fixture": "tree-basic", "target": "src", "expect": "golden" }`, see §11 |

### 4.2 Sources and precedence
Load in this order. A later source with the **same id** overrides an earlier one, and the details pane shows "Overridden by your file …":
1. Built-ins (C#)
2. `actions.json` next to the exe (shipped)
3. `commands.json` next to the exe → converted (§4.4)
4. `recipes.json` → `Recipes/<name>` nodes
5. User folder `%LOCALAPPDATA%\PSTools\actions\` (recursive). **The folder structure is the menu path**: `actions\Copy\Tree\Folders only.ps1` → `menu: "Copy/Tree"`, `title: "Folders only"`, unless the header says otherwise.

Settings can hide any id (`hidden: [...]`) and rename/move (`overrides: { id: { title, menu, order } }`). The Menu page edits only these overrides. **No source file is ever rewritten by the app, except files the app itself created in the user folder.**

Validation: invalid entries are skipped, never crash. They show in **Settings → Problems** (`file:line — reason`). `--selftest` fails if any shipped entry is invalid.

### 4.3 User script header format
PowerShell (`.ps1`):
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
cmd (`.cmd`/`.bat`): same keys on lines starting with `:: @key value`, before the first non-comment line.
A file with no header still works: `title` = file name without extension, `menu` = folder path, `when` = folder+background+file, `output` = console, `risk` = modifies (the safe default: it asks once).
`id` defaults to `user.` + slug of the relative path.

### 4.4 `commands.json` (library) conversion
Each library entry becomes `id: "lib." + id`, `menu: "Library/" + categoryLabel`, `shell: powershell`, `command` with `{path}` kept, `output: console`, `risk` mapped from `risk`, `admin` from `admin`, `keywords` from `tags`, `description` + `note`. Library items are **hidden from the right-click popup by default** (they are visible in the app and in search). The user stars them to surface them. That keeps the popup clean.

---

## 5. The menu: tree instead of maintenance

### 5.1 `MenuTree.Build(ContextKind ctx, string[] paths, bool shift)` returns:
```
Root
 ├─ Section "Recent"     (last 5 actions used in this context kind; hidden if none)
 ├─ Section "Suggested"  (from Suggest.cs + selection: images → Image/*, .zip → Extract, git repo → Dev/Git; max 4)
 ├─ Section "Favorites"  (starred ids applicable here, in user order)
 └─ Folders in canonical order (§5.4), each with child folders and actions, filtered by:
      when ∋ ctx, match.*, requires satisfied, not hidden, extended only if shift
```
Rules:
- Empty folders are removed. A folder with exactly 1 action after filtering is **inlined** into its parent with title `"<Folder>: <Action>"`.
- An action appears **once** in the folder part. It may also appear in Recent/Suggested/Favorites.
- Build time budget: ≤ 15 ms for 400 actions (unit-tested with a stopwatch in `--selftest`; fail > 50 ms).

### 5.2 Power Menu popup (the new right-click experience)
Invocation: `PSTools.exe menu "<path>"` (registry, §5.3) or inside the app (Ctrl+K and the Actions page reuse the same `TreeListControl`).

Behavior spec (all of it is required):
1. Multi-select: Explorer starts one process per selected item. Reuse `Collector.Gather` (see `Collector.cs` for its mutex/handoff logic) so **one** popup opens with all items. Measure and record in FINDINGS.md: the time from the first process start to the popup being visible, for 1, 20 and 200 selected files.
2. Position: top-left at `Cursor.Position` + (2,2). Clamp inside `Screen.FromPoint(cursor).WorkingArea`. If it doesn't fit below, open upwards.
3. Window: borderless, `TopMost`, `ShowInTaskbar = false`, `CS_DROPSHADOW`. On Windows 11 set rounded corners with `DwmSetWindowAttribute(hwnd, 33 /*DWMWA_WINDOW_CORNER_PREFERENCE*/, 2 /*ROUND*/)` and ignore failure on Windows 10. Steal focus correctly: `Activate()` then `SetForegroundWindow`. If foreground is refused, fall back to `AttachThreadInput`.
4. Layout: header (selection summary, for example `📁 src` or `12 items · 3 folders, 9 files`), search box (focused, placeholder `Type to search actions…`), list, footer hint bar (`↵ run   → open   ← back   ★ Ctrl+D   ? details   Esc close`). Exact sizes are in UI doc §4.2.
5. Navigation model: a **drill-in** list (not cascading flyouts). Right/Enter on a folder opens it and shows a breadcrumb `Copy › Tree`. Left/Backspace (with an empty search box) goes up. Mouse: click a folder to drill in, click the breadcrumb to go up. Hovering a folder for 400 ms **does not** auto-open (that's what makes nested native menus annoying).
6. Search: typing filters across the **whole tree** (flattened). Ranking: exact title prefix > word-prefix (`ta` matches **T**ree **A**SCII) > substring in title > keywords > description; recent usage boost +; stable tie-break by tree order. Result rows show the path in grey (`Copy › Tree`). The first result is always selected; Enter runs it.
7. Accelerators: first letter jumps between rows starting with that letter when the search box is empty… **conflict:** the search box is focused, so letters type into the search instead. Resolution: there are **no** single-letter accelerators. Search *is* the accelerator (`ta↵`). Number keys `1`-`9` run the Nth visible row **only** while the search box is empty. Document this in the footer.
8. Row: 16px glyph, title, right side shows `›` for folders, a coloured risk dot for modifies (amber) / destructive (red) / admin (shield glyph), and a keyboard digit for rows 1-9.
9. `Ctrl+D` or right-click on a row toggles Favorite (star). Right-click menu on a row: Run, Run with options… (if inputs), Copy PowerShell equivalent, Open in Run console, Add to Explorer menu (= favorite), Hide from menu, Edit (opens the source file for user actions, or "Duplicate as my action" for built-ins).
10. `?` or `F1` toggles a details panel (240 px, to the right, or below if there's no room). It shows the description, risk, the PowerShell equivalent and the source.
11. Running: the popup closes **first**, then the action runs (clipboard actions show the v3 toast). Inputs: the popup turns into a small inline form (same window, back with Esc) before closing.
12. Esc, or losing focus (`Deactivate`), closes the popup. Exit code 0.
13. Shift held at invocation (`Control.ModifierKeys`) → include `extended` actions, shown with a subtle `Shift` tag.
14. Performance targets, measured by `--ui-scenario popup-perf`: cold start to first paint ≤ 700 ms on the CI runner (warn above, **fail above 1500 ms**); keystroke to filtered repaint ≤ 30 ms for 400 actions.
15. Accessibility: each row exposes a name via `AccessibleName`/`AccessibleRole.MenuItem` so UI Automation tests can find them (UI doc §6.4).

### 5.3 What goes into the Explorer registry (zero maintenance)
Under each of the 4 v3 roots (`Directory`, `Directory\Background`, `Drive`, `*`):
```
PS Tools ▸
   ├─ Power menu…            (command: "PSTools.exe" menu "%1"/"%V")   ← always first, default
   ├─ ── separator ──
   ├─ <Favorite 1..8 that apply to this context>   (command: "PSTools.exe" <id> "%1")
   └─ Open in PS Tools        (folders/background/drive only)
```
- Favorites are the only thing written per item, and they are written **automatically** when you star something (debounced 1 s, same as v3's auto-apply). The user never sees a "menu maintenance" page.
- Favorites with `match.ext` get `AppliesTo` (reuse v3 query builder, `Modules.ImageQuery` style). `extended` favorites get `Extended`.
- Set `MultiSelectModel` = `Player` on each verb so the entries still show when more than 15 items are selected. **Verify** this on Windows 10 and 11 and record the result in FINDINGS.md.
- Optional setting **"Also show the full tree as native submenus"** (off by default): generates the folder part of the tree for each context as cascading `SubCommands` keys, ≤ 3 levels, ≤ 16 entries per level (overflow → `More ▸`), regenerated automatically when sources change (a FileSystemWatcher on the user folder while the app is open, plus a check at every app start by comparing a hash of the tree with `menu-hash.txt` in the data folder).
- Optional top-level shortcut entries (outside the `PS Tools ▸` submenu): setting "Put favorites at top level" (off). Max 3.
- Windows 11 modern (non-"Show more options") menu needs `IExplorerCommand` plus package identity (a sparse MSIX package and signing). **Out of scope for v4.** Keep v3's classic-menu toggle and say so in README.
- `--dump-menu` CLI prints the exact registry tree that `Write()` would produce (for tests, §11).

### 5.4 Canonical top-level folders (order fixed, icons in UI doc)
`Copy`, `Paste & New`, `Search`, `Open`, `Rename`, `Organize`, `Archive`, `Image`, `Media`, `Inspect`, `Compare`, `Dev`, `Git`, `Links`, `Clean up`, `Fix`, `Clipboard`, `System`, `Recipes`, `My actions`, `Library`.
User actions with an unknown top folder go **after** `Recipes` in alphabetical order, before `My actions` (`My actions` = user actions with `menu: ""`).

---

## 6. Run console and "make any command an action"

### 6.1 Run page (also openable as a standalone window: `PSTools.exe run "<dir>"`, and from the popup: *Open in Run console*)
Layout and exact sizes: UI doc §4.4. Functions:
1. **Working directory bar**: path box (drop target, Browse, recent dirs dropdown). It defaults to the right-clicked folder.
2. **Shell picker** (segmented): `PowerShell 5.1` | `PowerShell 7` (disabled with tooltip "pwsh.exe not found" if missing) | `cmd`. Remember the last choice.
3. **Editor**: multi-line (min 3 lines, grows to 10), monospace `Cascadia Mono` → `Consolas` fallback, 10pt. `Ctrl+Enter` runs, `Enter` inserts a newline, `Up`/`Down` on the first/last line browses history, `Ctrl+R` opens history search (reuse TreeListControl in flat mode).
4. **Autocomplete** (lightweight, no runspace): after 2+ characters of the current token, show up to 8 suggestions from (a) cheat sheet command names and examples, (b) PowerShell verbs-nouns list shipped in `cheatsheet.json` (`"commands": [...]`), (c) history, (d) file names in the working dir when the token contains `\` or `.`. Tab accepts.
5. **Output pane**: streaming, monospace, read-only, ANSI stripped (regex `\x1B\[[0-9;?]*[ -/]*[@-~]`), stderr lines in error colour, max 20,000 lines (oldest trimmed, with a banner "older output trimmed"). Toolbar: Copy all, Copy last command + output, Clear, Wrap toggle, Find (Ctrl+F), **Show as table** (re-runs through the objects wrapper, §6.4; PowerShell only).
6. **Status strip**: running spinner / `Exit 0 · 1.24 s` (green) / `Exit 1 · 0.31 s` (red) / `Stopped`.
7. **Stop**: `Esc` or the button runs `taskkill /T /F /PID <pid>`. .NET Framework has no kill-tree, so use taskkill.
8. **Danger check** before running (§6.5).
9. **Save as action…** button (enabled when the editor is non-empty), §6.6.
10. **History**: stored in `%LOCALAPPDATA%\PSTools\run-history.json` (last 500 entries: command, shell, dir, exit code, timestamp). **Import from PowerShell history**: reads every `*_history.txt` in `%APPDATA%\Microsoft\Windows\PowerShell\PSReadLine\`, de-duplicates, newest first, shows them in a picker with a search box, and the picked entries go into the editor (never run automatically).

### 6.2 Process execution (`Launch.Stream`)
- PowerShell 5.1: `powershell.exe -NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass -EncodedCommand <b64>`, `UseShellExecute=false`, redirect stdout/stderr, `CreateNoWindow=true`, `WorkingDirectory=dir`, env `NO_COLOR=1`.
  Prelude, prepended to every script:
  ```powershell
  $ErrorActionPreference = 'Continue'
  [Console]::OutputEncoding = [Text.Encoding]::UTF8
  $OutputEncoding = [Text.Encoding]::UTF8
  $ProgressPreference = 'SilentlyContinue'
  if ($PSStyle) { $PSStyle.OutputRendering = 'PlainText' }
  Set-Location -LiteralPath '<dir with '' doubled>'
  $Paths = @(<each path as single-quoted literal>)
  $Dir = '<dir>'
  ```
  Output decoding: `StandardOutputEncoding = UTF8`.
- pwsh: same, with `pwsh.exe`. Locate it via PATH, then `%ProgramFiles%\PowerShell\7\pwsh.exe`.
- cmd: write the script to `%LOCALAPPDATA%\PSTools\tmp\run-<guid>.cmd` (UTF-8 **without BOM**) with first line `@echo off` and second line `chcp 65001 >nul`, then `cd /d "<dir>"`. Run `cmd.exe /d /c "<file>"`. Delete the file afterwards.
- Interactive commands (that read stdin) can't work in the embedded console. If there's no output for 10 s and the process is idle, show the banner "This command may be waiting for input. Open in a real terminal?" with a button that relaunches it in `wt.exe -d <dir>` (or `powershell -NoExit`), with the command pre-filled on the clipboard.
- "Open in terminal" button (always present): launches Windows Terminal if `wt.exe` exists, otherwise the console host, `-NoExit`, in the dir. The command goes on the clipboard (v3 behavior: nothing runs by itself).

### 6.3 Placeholders (`Placeholders.Expand(text, shell, ctx)`)
| Token | Value |
|---|---|
| `{path}` | first selected item (full path) |
| `{paths}` | all selected items |
| `{dir}` | folder context: the right-clicked folder, or the parent of the selected file |
| `{name}` `{stem}` `{ext}` `{parent}` | of `{path}` |
| `{clip}` | clipboard text (max 100 KB) |
| `{date}` `{time}` `{now}` | `yyyy-MM-dd`, `HH-mm-ss`, `yyyy-MM-dd_HH-mm-ss` |
| `{input:Name}` | value of input `Name` (prompted) |
| `{gitroot}` | `git rev-parse --show-toplevel` of `{dir}` (empty if not a repo) |

Quoting (**the placeholder emits the quotes**, so users never write `'{path}'`):
- PowerShell/pwsh: a single-quoted literal with `'` doubled: `C:\a\O'Brien` → `'C:\a\O''Brien'`. `{paths}` → `@('a','b')`.
  **Back-compat:** if the template already has `'{path}'` (v3 `commands.json` style), emit the inner escaped text only. Detect the exact sequences `'{token}'` before expanding.
- cmd: double-quoted `"C:\a b"`. `%` in values is doubled (`%%`) because they go into a `.cmd` file. A value containing `"` → refuse with the error "This path contains a quote character, which cmd cannot pass safely. Use PowerShell." `{paths}` → space-separated quoted list.
- Unit tests in `--selftest` for each token × shell × these values: plain, spaces, `'`, `%`, `&`, `^`, `!`, `$`, backtick, unicode (`Ünïcødé 日本`), trailing dot, `[brackets]` (use `-LiteralPath` in every shipped PowerShell action because of wildcard brackets), 250-char path.

### 6.4 Objects → grid wrapper (PowerShell, `output: grid` or "Show as table")
```powershell
$__r = @( & { <USER SCRIPT> } )
if ($__r.Count -eq 0) { '##PST-EMPTY##'; return }
if (@($__r | Where-Object { $_ -isnot [string] -and $_ -isnot [ValueType] }).Count -eq 0) {
  '##PST-TEXT##'; $__r | ForEach-Object { "$_" }; return }
'##PST-JSON##'
$__r | Select-Object -First 5000 | ForEach-Object {
  $o = [ordered]@{}
  foreach ($p in $_.PSObject.Properties) { if ($p.MemberType -match 'Property') { $v = $p.Value; $o[$p.Name] = if ($null -eq $v) { '' } elseif ($v -is [datetime]) { $v.ToString('yyyy-MM-dd HH:mm:ss') } else { "$v" } } }
  [pscustomobject]$o } | ConvertTo-Json -Compress -Depth 2
```
C# side: read everything after the marker. `JSON` → `JavaScriptSerializer.DeserializeObject` (a single object is not an array, so wrap it). Columns are the union of keys in first-seen order (cap 40 columns). Show it in the v3 `ResultsForm` (sortable, filter box, Copy as CSV / TSV / Markdown table / JSON). For `FileInfo`/`DirectoryInfo` results, set `PathColumn` to `FullName` so row actions (open, reveal) work.

### 6.5 Danger check (`RiskScan.Classify(command, shell)` → safe/modifies/destructive + reasons)
Tokenize roughly (PowerShell: split on whitespace/pipes/semicolons, case-insensitive; cmd likewise).
- **destructive**: `Remove-Item`, `rm`, `del`, `erase`, `rd`, `rmdir`, `ri`, `Clear-Content`, `Format-Volume`, `format`, `diskpart`, `Clear-Disk`, `Initialize-Disk`, `Stop-Computer`, `Restart-Computer`, `shutdown`, `reg delete`, `Remove-ItemProperty`, `bcdedit`, `cipher /w`, `vssadmin delete`, `takeown`+`icacls … /grant` on `C:\Windows`, `Set-ExecutionPolicy`, `git clean -fdx`, `git reset --hard`, `git push --force`, `-Force` together with any modifying verb, any `-Recurse` together with a removing verb.
- **modifies**: verbs `New-`, `Set-`, `Move-`, `Rename-`, `Copy-`, `Add-`, `Out-File`, `>`/`>>` redirection, `Expand-Archive`, `Compress-Archive`, `mkdir`/`md`, `move`, `ren`, `copy`, `xcopy`, `robocopy`, `git commit|pull|merge|checkout|switch|stash`, `npm|pnpm|yarn install`, `winget install|upgrade|uninstall`, `Start-Process … -Verb RunAs`.
- otherwise **safe**.
Run behavior: safe → runs. modifies → a single inline confirmation bar above the output ("This command changes files: `Move-Item`. Run / Cancel", with "don't ask again for this exact command"). destructive → a modal `FDialog` that shows the command, the reasons, and the working dir in bold, with the default button = Cancel.
The heuristic must never block, only warn. Unit tests: ≥ 40 commands with expected classes in `--selftest`.

### 6.6 Save as action (`SaveActionForm`)
Opened from the Run console, from a history entry, from a cheat-sheet entry, or from the popup ("Duplicate as my action").
Fields, prefilled smartly:
1. **Title** (default: the cheat-sheet title, or the first 32 chars of the command)
2. **Menu location**: a tree picker of existing folders plus "New folder…" (default `My actions`)
3. **Shows for**: checkboxes File / Folder / Inside a folder / Drive. Default: whatever context the console was opened from + background
4. **Only for extensions** (optional, comma-separated)
5. **Result**: radio Show output (console) / Copy output to clipboard / Show as table / Just run (toast)
6. **Risk**: prefilled from `RiskScan`, editable
7. **Command** with **placeholder generalization**: if the command contains the current working dir literally (any quoting), offer a highlighted suggestion bar: "Replace `C:\Users\me\proj` with `{dir}` so it works anywhere?" [Replace] (default on). Same for the selected file path → `{path}` and its name → `{name}`. Show a live preview: "On `D:\photos` this runs: …".
8. **Test run** button: runs it on the current dir and shows the output inline.
9. Save → writes `%LOCALAPPDATA%\PSTools\actions\<menu path>\<Title>.ps1` (or `.cmd`) with a header (§4.3), and with `@id user.<slug>`. Catalog reloads. The toast says "Saved. Right-click → PS Tools → Power menu → <path>". If "Add to Explorer menu" is ticked, also star it.
Name collisions: append ` (2)`. Invalid filename characters are replaced with `-`.

---

## 7. Cheat sheet (summary; full spec in `docs/v4/CHEATSHEET_SPEC.md`)
- `app/cheatsheet.json`, **≥ 300 entries**, in categories (navigation, files, search/text, pipeline & objects, formatting & export, clipboard, processes, services, network, system info, disks, registry, environment & PATH, scheduled tasks, archives & hashes, permissions & ownership, event logs, users & security, winget/apps, git, strings & dates, math & conversions, remoting basics, profile & modules), **plus** two translation tables: **cmd → PowerShell** (≥ 40 rows) and **Linux/bash → PowerShell** (≥ 60 rows: `ls -la`, `grep -rn`, `find -name`, `du -sh *`, `tail -f`, `wc -l`, `which`, `touch`, `xargs`, `head`, `sort | uniq -c`, `sed s///`, `awk '{print $1}'`, `curl -O`, `ps aux`, `kill -9`, `df -h`, `chmod` (→ icacls), `ln -s`, `export`, `history | grep`, `!!`, `cat a b > c`, `diff`, `tee`, `watch`, `env`, `alias`, `tar -czf`, …).
- The page: category list on the left, entries in the middle (title, command in a monospace chip, one-line explanation), and a detail panel on the right (full explanation, example output, parameters explained, related entries). Buttons: **Try here** (opens the Run console with the command filled in, working dir = current), **Copy**, **Save as action**.
- Every entry has `safeToRun` (bool). CI parses **every** command with the PowerShell parser (`[System.Management.Automation.Language.Parser]::ParseInput`) and fails on parse errors. CI also **executes** every `safeToRun` entry against the fixture folder with a 10 s timeout and fails on any non-zero exit / terminating error (allow-list `expectError: true` for documented cases).

---

## 8. Power actions (summary; full catalog with ids, behavior, tests in `docs/v4/POWER_ACTIONS.md`)
Headline features every user should notice within a minute:
- **Copy › Tree**: ASCII / Markdown / folders-only / with sizes / depth 1-2-3-all / **respect .gitignore** (via `git ls-files` when it's a repo, otherwise built-in ignore list) / exclude build folders.
- **Copy › List**: all subfolder names, all subfolder paths, all file names recursive, relative paths, names of selected items, as CSV/JSON listing.
- **Copy › For AI**: all text files of a folder (or the selection) concatenated with ```` ```lang path ```` fences. Respects .gitignore, skips binaries, caps at 400 KB with a summary of what was skipped. Includes a tree at the top.
- **Copy › Path variants**: quoted, `/`, `\\` escaped (JSON), PowerShell literal, WSL, `file://`, relative to git root, relative to the marked folder, UNC, 8.3 short.
- **Copy › Git**: GitHub/GitLab permalink to this file (at the current commit), branch name, last commit hash/message, remote URL.
- **Copy file to clipboard as file / as image** (paste into chat apps).
- **Paste & New › Folders from clipboard tree**: paste an ASCII tree, an indented list, or a list of relative paths → creates the structure (`mkdir -p`, journaled, preview first).
- **Paste & New**: clipboard image → PNG, text → file with auto extension (json/md/csv/py/js detection), URL → download, **paste as shortcut / hard link / symlink** of the copied files.
- **Inspect › Who is locking this?** (Restart Manager API) with "Close that app" (graceful `CloseMainWindow`, then an offer to kill).
- **Inspect › Where did this file come from?** (reads `Zone.Identifier` ADS: `HostUrl`/`ReferrerUrl`).
- **Inspect › File facts**: encoding, BOM, line endings, line count, size, hashes, signature (`Get-AuthenticodeSignature`), image dimensions, media duration.
- **Search**: text in files (ripgrep when present, otherwise Select-String) with a results grid. Find large / recent / duplicate / empty / long-path.
- **Open**: Terminal here (wt), PowerShell 7, cmd, admin, VS Code, WSL, Git Bash, Run console here.
- **Clipboard** (background context and app): sort lines, unique lines, trim, JSON pretty/minify, base64 encode/decode, URL encode/decode, to lower/UPPER/kebab/snake, count lines/words/chars, remove blank lines.
- **Dev/Git**: git status/pull/fetch across repos, open repo in browser, clean build folders, what's on port X, serve folder, npm/pnpm/pip install here (requires).
- **System**: add this folder to the user PATH (with an undo entry), copy local/public IP, flush DNS, restart Explorer.

---

## 9. Migration from v3 (no user data loss)
On the first v4 start (detect `settings.json` without `"schema": 4`):
1. Back up `%LOCALAPPDATA%\PSTools\settings.json` → `settings.v3.bak.json` (never overwrite an existing backup).
2. Pins: every v3 `ContextPins[ctx]` id becomes a favorite (by alias → new id), keeping order, de-duplicated across contexts. Cap at 8 per context in the registry; the rest stay favorites in the popup.
3. v3 enabled set / presets → ignored (documented). Disabled modules aren't hidden in v4, because the popup is searchable and cheap.
4. `GroupPins` → favorites folder order (if easy) or dropped (log in Problems).
5. `recipes.json`, undo history, marks: untouched.
6. Explorer menu: if installed, rewrite it with the v4 layout immediately (same roots, so `RemoveMenu` cleans it up).
7. The CLI keeps accepting v3 ids via `aliases` (`PSTools.exe copy-tree "C:\x"` still works). `--selftest` asserts that **every v3 action id** resolves. Generate the list from v3 before refactoring and commit it as `app/tests/v3-ids.txt`.
8. Show a one-time "What's new in v4" card on Home (dismissible).

---

## 10. CI / GitHub Actions (create in Phase 0)

### 10.1 `.github/workflows/ci.yml`
```yaml
name: ci
on:
  pull_request:
  push:
    branches: [main]
permissions:
  contents: read
jobs:
  build-test:
    runs-on: windows-latest
    timeout-minutes: 30
    steps:
      - uses: actions/checkout@v4
      - name: Build with .NET Framework csc (C# 5 contract)
        shell: cmd
        run: cd app && build.cmd
      - name: Self-test
        shell: pwsh
        run: |
          $p = Start-Process app\out\PSTools.exe -ArgumentList '--selftest','--quiet' -Wait -PassThru -NoNewWindow
          if ($p.ExitCode -ne 0) { exit 1 }
      - name: Fixtures
        shell: pwsh
        run: app\tests\fixtures\make-fixtures.ps1 -Out $env:RUNNER_TEMP\fx
      - name: Action tests (goldens)
        shell: pwsh
        run: app\tests\run-action-tests.ps1 -Exe app\out\PSTools.exe -Fixtures $env:RUNNER_TEMP\fx -Report app\out\action-report.json
      - name: Snippet + cheat sheet checks
        shell: pwsh
        run: |
          app\tools\snippet-check.ps1
          app\tests\cheatsheet-check.ps1 -Fixtures $env:RUNNER_TEMP\fx
      - name: Menu snapshot
        shell: pwsh
        run: app\tests\menu-snapshot.ps1 -Exe app\out\PSTools.exe
      - name: Layout lint
        shell: powershell   # Windows PowerShell 5.1: same CLR as the app
        run: app\tools\layout-check.ps1
      - name: Screenshots
        shell: pwsh
        run: app\tests\ui\capture.ps1 -Exe app\out\PSTools.exe -Out app\out\shots
      - name: Visual diff + pixel lint
        shell: pwsh
        run: app\tests\ui\compare.ps1 -Shots app\out\shots -Baseline app\tests\ui\baseline -Out app\out\ui-report
      - name: UI automation scenarios
        shell: powershell
        run: app\tests\ui\scenarios.ps1 -Exe app\out\PSTools.exe -Fixtures $env:RUNNER_TEMP\fx
      - name: Footprint check
        shell: pwsh
        run: app\tools\footprint-check.ps1 -Exe app\out\PSTools.exe
      - uses: actions/upload-artifact@v4
        if: always()
        with:
          name: pstools-${{ github.sha }}
          path: |
            app/out/PSTools.exe
            app/out/*.json
            app/out/shots/**
            app/out/ui-report/**
            app/out/action-report.json
```
Note: the step order matters. Screenshots run **before** anything that installs into HKCU, and footprint-check runs last. If `footprint-check.ps1` currently needs a param other than `-Exe`, adapt the call, not the script semantics.

### 10.2 `.github/workflows/ui-gallery.yml` (PR screenshot gallery, so you and the owner can SEE the UI in the PR)
Trigger: `workflow_run` of `ci` completed on a PR (safe for tokens). Permissions: `contents: write`, `pull-requests: write`, `actions: read`. Steps: download the artifact, then push `shots/*.png` + `ui-report/diff/*.png` to an orphan branch `ui-gallery` under `pr-<number>/<sha7>/`. Post or update **one** sticky PR comment (find it by the marker `<!-- ui-gallery -->`) with a markdown table: rows = screen, columns = Light / Dark / Diff vs baseline. Images link to `https://raw.githubusercontent.com/datakid/ps-tools-/ui-gallery/pr-<n>/<sha7>/<file>.png`. If the repo is private, raw URLs need auth. In that case link the artifact instead and state it in the comment.

### 10.3 `.github/workflows/release.yml`
On tag `v4.*`: build, selftest, zip `PSTools.exe`, `actions.json`, `commands.json`, `cheatsheet.json`, `README.md`, `LICENSE` (if present) → `PSTools-<tag>.zip`, plus a SHA-256 file. Create a GitHub Release with generated notes. **Do not tag releases yourself unless the owner asks.**

---

## 11. Test strategy (functional)

### 11.1 Fixtures: `app/tests/fixtures/make-fixtures.ps1 -Out <dir>` (deterministic, idempotent)
Creates:
- `tree-basic/` nested 3 levels: `src/app/main.py`, `src/app/util.py`, `src/lib/`, `docs/readme.md`, `empty/`, `.hidden/`(hidden attr), `node_modules/pkg/index.js`, `bin/out.dll`(random bytes, fixed seed)
- `names/` with `O'Brien.txt`, `100% done.txt`, `a&b.txt`, `[draft] v1.txt`, `Ünïcødé 日本.txt`, `space name/`, `trailing.dot.`(skip if FS refuses), a path of ~250 chars
- `images/` 3 PNG + 1 JPG drawn via System.Drawing (fixed sizes 1200x800, 640x480, …) with EXIF on the JPG
- `dups/` with 2 identical files and 1 different one of the same size
- `repo/`: `git init`, `.gitignore` (`node_modules/`, `*.log`), committed files, a remote `https://github.com/example/repo.git`, branch `main`; skipped if git is missing (record "skipped")
- `zip/` with `one-top.zip` (single top folder) and `flat.zip`
- `text/` with LF, CRLF, BOM, UTF-16 files
- `clip-tree.txt` (an ASCII tree for the Folders-from-clipboard test)

### 11.2 `--test-actions <fixturesDir> --report <file>` (in-app test mode)
- Replaces `Clip`, `Toast`, `FDialog` and `Program.Ask` with `TestSink` (records instead of showing; Ask → yes). Runs each action that has a `test` spec in quiet mode, on `fixturesDir/<fixture>/<target>`.
- `expect: "golden"` compares clipboard/stdout text with `app/tests/golden/<id>.txt`. Before comparing, normalize: CRLF→LF, replace the fixture root with `<FX>`, strip trailing spaces.
- `expect: {"files": ["<rel path>", …]}` asserts that files exist afterwards. Then **Undo** runs and asserts they're gone (for `undo: journal`).
- `--update-goldens` rewrites goldens. The PR must show the golden diff, and you must read it.
- The report JSON is `[{id, status: pass|fail|skip, ms, detail}]`. The script fails CI on any fail.
- Coverage gate: every action in `POWER_ACTIONS.md` marked **[T]** must have a test spec; `--selftest` checks it.

### 11.3 `--selftest` additions
Catalog validity, alias coverage (`v3-ids.txt`), placeholder quoting matrix, RiskScan table, MenuTree perf, tree filtering rules (inline single-child folders, `requires` hiding), header parser cases, JSON round-trip of settings migration (use a v3 settings sample committed in `app/tests/v3-settings-sample.json`).

### 11.4 `menu-snapshot.ps1`
Runs `PSTools.exe --dump-menu --quiet` with a test settings file (`--settings <file>`, add this flag) holding fixed favorites. Compares with `app/tests/golden/menu-dump.txt`. This guards the registry layout without touching the registry.

---

## 12. Phases, deliverables and acceptance criteria
Each phase = 1 PR. Don't start phase N+1 until PR N is green and its checklist is complete (the owner may merge later. Branch from the previous phase branch if it isn't merged yet, and say so).

### Phase 0: Safety net first (CI + UI harness on v3, no feature changes)
- [ ] `ci.yml` builds v3 with framework csc and runs `--selftest`, `snippet-check`, `layout-check`
- [ ] `UiHarness`: `--screenshot <outDir>` + `--theme light|dark` + `--accent 0067C0` + `--scale 1.0|1.5` (UI doc §6)
- [ ] `capture.ps1`, `compare.ps1` (with pixel lint), `ui-gallery.yml`
- [ ] Baseline screenshots of **v3** committed to `app/tests/ui/baseline-v3/` (reference only, not compared later)
- [ ] `make-fixtures.ps1`, `TestSink`, `--test-actions`, goldens for 10 existing v3 actions
- [ ] `app/tests/v3-ids.txt`
- **Accept:** CI green, gallery comment visible in the PR with v3 screens in light and dark, and FINDINGS.md lists any harness limitations (fonts on the runner, etc.)

### Phase 1: Action model + catalog (no visible UI change yet)
- [ ] `ActionSpec`, `ActionCatalog`, `ScriptHeader`, `Placeholders`, conversion of built-ins (each v3 action gets a v4 id + alias + `menu` path + `when` from `Targets` + `risk` from `Tier`/`Confirm`)
- [ ] `commands.json` conversion, `recipes.json` nodes, user folder loading, overrides/hidden, Problems list
- [ ] `--list-actions --json` CLI (for tests and docs)
- [ ] selftest: catalog, aliases, placeholders matrix
- **Accept:** all v3 goldens still pass via aliases. `--list-actions` shows ≥ v3 count. Zero invalid shipped entries.

### Phase 2: MenuTree + Power Menu popup + new registry layout
- [ ] `MenuTree` with sections, filtering and inlining. Perf test.
- [ ] `PowerMenu` popup with the full §5.2 behavior. Ctrl+K palette replaced by the same control.
- [ ] `Shell.Write()` v4 layout, favorites auto-written, `MultiSelectModel`, `--dump-menu`, `menu-snapshot.ps1`
- [ ] Migration §9 (pins → favorites, backup)
- [ ] UI scenarios: `popup-open`, `popup-search`, `popup-drill`, `popup-favorite`, `popup-perf` (UI doc §6.4)
- **Accept:** CI green incl. perf. Gallery shows the popup in light/dark, at root, drilled into Copy › Tree, searching `ta`, with details open, and with 200 items selected. Rubric filled.

### Phase 3: Run console + Save as action + history import
- [ ] `Launch.Stream`, `KillTree`, preludes, cmd temp-file path, ANSI strip, idle-input banner
- [ ] `RunConsole` page + standalone `run` CLI, autocomplete, history, Show as table (objects wrapper)
- [ ] `RiskScan` (+ 40 test cases), confirm UX
- [ ] `SaveActionForm` with placeholder generalization + test run. Writes header files. Catalog reload.
- [ ] Scenarios: `run-echo`, `run-cmd`, `run-stop`, `run-table`, `save-action-roundtrip` (save → appears in the popup → run → expected clipboard)
- **Accept:** a user can type `Get-ChildItem -Recurse -Filter *.py | Select Name, Length`, see a table, save it as "Python files" under `Search`, and run it from the popup on another folder. This is automated in `save-action-roundtrip`.

### Phase 4: Power actions pack
- [ ] Implement every action in `POWER_ACTIONS.md` (builtins in C# where marked **C#**, otherwise in `actions.json`)
- [ ] Goldens for every **[T]** action. Undo tests for journaled ones.
- **Accept:** coverage gate passes. The manual demo script in POWER_ACTIONS.md §Z is recorded as screenshots in the gallery.

### Phase 5: Cheat sheet
- [ ] `cheatsheet.json` ≥ 300 entries + both translation tables, schema per CHEATSHEET_SPEC
- [ ] `CheatSheetPage`, Try here, Save as action, search
- [ ] `cheatsheet-check.ps1` (parse all, run safe ones)
- **Accept:** CI parses 100%, runs all `safeToRun` entries green, and the page screenshots pass the rubric.

### Phase 6: Main window redesign + Menu page simplification + polish
- [ ] Pages per UI doc §4: Home, Actions (tree), Run, Cheat sheet, Settings (incl. Menu & favorites, Problems, About). FirstRun → one screen.
- [ ] Remove presets UI, PreviewForm (replaced by "Explorer menu" preview inside Settings, generated from `--dump-menu` logic)
- [ ] New baselines `app/tests/ui/baseline/` generated **by CI** (download the artifact, commit) after the owner approves the gallery
- [ ] Keyboard-only walkthrough scenario `keyboard-tour`
- **Accept:** rubric 100%. Pixel lint is zero. Layout lint is zero at min/default/large sizes and scale 1.0/1.5.

### Phase 7: Docs + release prep
- [ ] README rewrite (what v4 is, the 30-second tour with screenshots from the gallery, the user action folder format, CLI, footprint, migration)
- [ ] `docs/USER_ACTIONS.md` with 10 copy-paste examples
- [ ] `release.yml`. A draft release only if the owner asks.

### PR template (put it in `.github/pull_request_template.md` in Phase 0)
```
## What & why
## Phase checklist (copy from V4_PLAN.md §12, tick honestly)
## Tests added / changed
## Screenshots (gallery comment link) + UI rubric (docs/v4/UI_SPEC_AND_TESTING.md §7) – every item answered
## Findings / deviations from plan
## Risk & rollback
```

---

## 13. Things that are explicitly out of scope for v4
Windows 11 top-level modern menu (IExplorerCommand + MSIX), a resident tray process (the popup cold start must be fast enough without one; revisit only if `popup-perf` > 700 ms on real hardware), in-process PowerShell runspace hosting, plugins in other languages, auto-update, telemetry.

## 14. Definition of done for v4 (owner's checklist)
1. Right-click any folder → `PS Tools ▸ Power menu…` → type `tree` → Enter → the tree is on the clipboard. Under 1 s warm.
2. Starring an action puts it into the native Explorer submenu without visiting any settings page.
3. Dropping a `.ps1` file with a header into `%LOCALAPPDATA%\PSTools\actions\Git\` makes it appear under `Git` the next time the menu opens.
4. Typing a command in Run, then **Save as action**, makes it appear in the right-click menu with `{dir}` generalized.
5. The cheat sheet has ≥ 300 searchable entries. Every entry can be tried and saved.
6. Every PR has a green CI run, a screenshot gallery, and a completed UI rubric.
7. v3 users keep their pins (as favorites), recipes and undo history.
