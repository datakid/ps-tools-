# v4 findings

Measured facts and deviations from the plan, with evidence. Newest phase last.

## Phase 0

### CI environment (windows-latest, measured)
- Primary screen 1024x768, work area 1024x720 (`shots/harness.*.log`). Windows clamps the 1040x720 main window to 1024x720, and at scale 1.5 the main pages overflow. Screenshots at 1.5 are therefore layout stress tests, not what a user with a large display sees.
- `PrintWindow` with `PW_RENDERFULLCONTENT` worked for all 38 captures; the `DrawToBitmap` fallback was never needed.
- Font on the runner is `Segoe UI Variable Text`, so metrics match Windows 11.
- Windows PowerShell 5.1 is used for fixtures, capture, compare and layout lint (same CLR as the app, `Add-Type` compiles the lint with the C# 5 compiler). pwsh is used for the rest.
- A full run (build, tests, 38 screenshots, lint) takes about 4 minutes.
- Determinism: two separate CI runs of the same code produced 38 of 38 pixel-identical screenshots (diff tolerance 16 per channel, 0.5% changed-pixel budget, measured result 0 changed pixels). The committed `baseline-v3` is therefore a valid regression reference.

### Logs and artifacts are not readable by the implementer
`GET /actions/jobs/{id}/logs` and artifact downloads redirect to `*.blob.core.windows.net`, which is outside the implementer sandbox's network allow-list. Consequences and workaround:
- `ui-gallery.yml` as a `workflow_run` workflow cannot run from a feature branch (GitHub only runs `workflow_run` workflows defined on the default branch). The gallery is a second job inside `ci.yml` instead (same-repo PRs only, `contents: write` and `pull-requests: write` scoped to that job). Behaviour is as planned: orphan branch `ui-gallery`, `pr-<n>/<sha7>/`, newest three runs kept, sticky comment with a light/dark table.
- The gallery job also publishes `logs/*.txt` (every CI step is run through `app/tests/ci/run-logged.ps1`, which writes UTF-8 logs) and `goldens-generated/`, so failures can be diagnosed from `raw.githubusercontent.com`.

### v3 problems the new safety net found (unchanged v3 code, except where noted)
1. `app/tools/layout-check.ps1` was stale: it built `PreviewForm` with the old two-set constructor and crashed. Fixed to use `Settings.DefaultState()`.
2. With that fixed, layout-check reports 7 real problems on a clean runner: Menu page summary label clipped at 860x520, 980x640 and 1200x760; "Pinned to menu..." button overflows at 860x520; Command library at minimum size (Copy and Pin to menu buttons overflow); Space map at minimum size (note clipped). The step is `continue-on-error` until Phase 6, which rewrites these screens and must bring it to zero. This is the only gate relaxed, and only because the failures are in v3 UI that Phase 1-6 replace.
3. `footprint-check.ps1` failed on a clean profile: uninstall removed the four `PSTools` keys but left empty `Software\Classes\Directory`, `Directory\shell`, `Directory\Background`, `Directory\Background\shell`, `Drive`, `Drive\shell`, `*`, `*\shell`. Fixed in `Shell.RemoveMenu` by pruning parents that are empty (no subkeys, no values), never touching `Software\Classes` itself.
4. Dark mode, Recipes window: the steps list renders as a light #F0F0F0 box (pixel lint rule `dark-white-box` and `default-grey`).
5. Pixel lint baseline on v3 (warn-only): contrast 7, dark-white-box 1, default-grey 18, light-dark-box 17, out-of-bounds 44, overlap 8, text-clip 16. Several `light-dark-box` hits are intentional dark surfaces (toast, code preview); v4 screens tag these `allow-dark` / `allow-light` in `Control.Tag`. Lint becomes blocking for v4 screens from Phase 1; `-LintWarnOnly` is only passed while v3 screens are in the set.

### Test tooling decisions
- `--list-actions`, `--test-actions`, `--screenshot`, `--data-dir` are new CLI flags. `TestSink` replaces clipboard read/write, toasts and dialogs when active. Text clipboard reads in actions go through `Clip.Text()`.
- Action tests run on a fresh copy of the fixture per test; copy skips paths over 250 characters so the long-path fixture does not break the copy. Tests that need the long-path fixture will run in place (Phase 4).
- Toast text is truncated by the app relative to the absolute path length, which differs between machines, so truncated toasts are normalised to `<truncated>` in goldens.
- Goldens are bootstrapped from the `goldens-generated` step, reviewed by eye, then committed. The gating step never writes goldens.
- The harness does not fix the clock; v3 screens show no clock values except Undo history timestamps, which are empty in the harness.
- `app/tests/v3-ids.txt` has 108 ids (what `--list-actions` returns on v3 with no recipes or library pins). Phase 1 asserts every id still resolves.

### Not done in Phase 0 (by design, belongs to later phases)
UI Automation scenarios, menu snapshot, cheat sheet checks, startup timing probe, and the glyph (tofu) check, which needs the new icon set.

## Phase 1

- **Settings format:** v3 settings are line-based `settings.txt`, not `settings.json` as V4_PLAN §4.2 and §9 assume. `CatalogOverrides.Parse` takes the plan's JSON shape (`hidden`, `overrides`) as input; where it is stored is decided with the schema-4 migration in Phase 2.
- **v4 ids:** where POWER_ACTIONS names an id for the same behaviour it is used (`copy.tree.ascii`, `copy.path.quoted`, `new.folder-from-clip`, `search.large`, `inspect.sizes`, `git.status.repos`, ...); otherwise the id follows the same naming scheme. The whole table is `V3Map.cs`; selftest fails if any id in `tests/v3-ids.txt` has no row or resolves to a `legacy.*` fallback.
- **Library vs built-ins:** 8 library ids equal v3 built-in ids (`flatten-copy`, `folder-sizes`, `long-paths`, ...). They live in their own namespace (`lib.<id>`, alias `lib-<id>`), so there is no clash.
- **Title limits:** the plan's 32-character limit holds for built-in and shipped entries. 21 of the 108 library titles are 33-47 characters, so library, recipe and user entries get a 64-character limit. Shipped content stays strict.
- **Admin with risk safe:** allowed (`mft-info` in the library is a read-only admin command). Admin always confirms, so the combination is not contradictory.
- **App-only v3 actions** (`Targets = None`, e.g. `dism-cleanup`, `setup-guide`): `when` is `background` and `AppOnly` is set, so Phase 2's MenuTree can keep them out of the popup.
- **Risk, output and multi for built-ins** are derived: Elevated means admin; Confirm or Undoable means `modifies`; `force-delete`, `delete-empty`, `clean-build`, `take-ownership` and `reset-network` are `destructive`; grid/clipboard/window outputs come from a small table. They are approximations until each action is re-homed in Phase 4.
- **Placeholders:**
  - PowerShell treats the curly quotes U+2018, U+2019, U+201A and U+201B as single quotes, so a path like `it's.txt` (typographic apostrophe) would end the literal early. They are doubled exactly like `'`. This is covered by the quoting matrix.
  - The v3 `'{path}'` form keeps the template's own quotes and only escapes the inside. The first implementation swallowed the quotes; the selftest caught it before CI.
  - `{ext}` includes the dot so `{stem}{ext}` equals `{name}`.
  - cmd values with a trailing backslash get it doubled so `"C:\"` cannot escape the closing quote.
- **Declarative actions cannot run yet.** The two shipped entries in `actions.json` (`system.ip-local`, `git.copy.branch`) load and validate, and the CLI says plainly that script actions arrive with the Executor in Phase 3.
- **Local gate:** C# 5 is also checked locally with Mono `mcs -langversion:5` before each push; CI with the real compiler remains the authority.

## Phase 2

### Measured on windows-latest (UI Automation scenarios, `logs/scenarios.txt`)
- **Cold start to visible popup** (process start to the window being found by UI Automation, 5 runs): median 386-463 ms across CI runs. The very first launch of a fresh exe is slower (800-1050 ms, disk cache and Defender scan); it stays under the 1.5 s hard limit but above the 700 ms target. No ReadyToRun/NGEN is available with plain `csc.exe`, so the remaining lever is less work at startup.
- **Multi-select fan-out** (N `menu <file>` processes started back to back, one popup expected):
  | selected | processes started in | popup visible after | items in popup |
  |---|---|---|---|
  | 1 | 15-17 ms | 390-560 ms | 1 |
  | 20 | 32-45 ms | 840-1005 ms | 20 |
  | 200 | 450-890 ms | 4.9-6.5 s | 200 |
  With the first implementation (leader waits a fixed quiet time) only 101 of 200 were collected, because the runner was still starting processes. The collector now waits until the file count has been stable for 120 ms **and** no peer process started in the last 8 s is still without a window (max 10 s). Real Explorer's spawn rate was not measured (CI cannot drive Explorer's own context menu).
- **Search cost:** 0.5-1.0 ms per keystroke over 218 actions (selftest limit 30 ms); menu build 0.2 ms (limit 50 ms). Repaint time was not measured separately.
- **UI Automation works** on the runner (SendKeys, row enumeration, screen captures). `LegacyIAccessiblePattern` is not available from PowerShell, so scenarios navigate with the keyboard and read row names. Rows are exposed through a custom `AccessibleObject` (role MenuItem, name = title, value = id).

### Not verifiable in CI
- Whether Explorer honours `MultiSelectModel=Player` on the new layout, and how the Windows 11 modern menu shows the entries (classic verbs sit under "Show more options" unless the classic menu toggle is on). This needs one check on a real Windows 11 machine. Registry layout is covered by `--dump-menu` goldens and the footprint check.
- Foreground focus when launched by Explorer: `SetForegroundWindow` with an `AttachThreadInput` fallback is implemented; on CI the popup received typed keys in every scenario.

### Deviations and decisions
- **Search ranking:** for 2-3 character queries an initials match (`ta` = Tree ASCII) outranks a plain title prefix (`ta` = Take ownership), because the plan's own scenario needs `ta` to find Tree (ASCII) first. Library entries are demoted by 25 points so built-ins win ties.
- **Favorites:** stored in settings schema 4 as `favorite=`, `hidden=`, `override=`, `used.<ctx>=`. Until the v3 Menu page is removed in Phase 6, pinning there is mirrored into favorites; its preview (still `MenuPlan`) no longer matches the real registry layout.
- **Controls** live in `FluentTree.cs` and `Tokens.cs`, not `Fluent.cs`, to keep the diff reviewable. `UI.Style` leaves a `TextBox` with `Tag = "raw"` alone (the default wraps it in a card, which collapsed the search input to 10 px; found through the control map in the manifest).
- **Interim executor:** declarative actions run through `Executor.cs` (PowerShell/pwsh/cmd with placeholders, confirm for risky ones, output to clipboard/toast/dialog). "Run ... in console" in the empty state uses it too. Phase 3 replaces both with the real Run console.
- **Hidden-from-popup:** library entries only show in search or when favorited. App-only v3 actions never show.
- **Popup size:** fixed 420 px wide, list 4-12 rows, capped to the work area; at narrow work areas the details pane opens below the list instead of beside it.
- **Row menu:** Run, Run with options, Copy PowerShell equivalent, Add/Remove from Explorer menu, Hide from menu, Edit (user scripts) or Duplicate as my action. "Open in Run console" is added in Phase 3.
- **Menu verbs:** `menu "%1"` for file/folder/drive, `menu-bg "%V"` for folder background, `open "%1"` for the app.
- **Local check:** menu/tree goldens are generated with Mono on Linux and match the Windows output byte for byte, so they can be regenerated without a Windows machine.

## Phase 3

### Measured on windows-latest
- **Real process runs** (selftest `Run console: real PowerShell and cmd runs`, ~5 s total): `Write-Output` round trip, a failing command (plain-text error, exit 1), `exit 3` honoured, cmd `exit /b 4`, a table run returning 3 rows x 2 columns, and a no-output table run reporting "nothing". Typical PowerShell 5.1 start to exit for a one-line command is 0.35-0.5 s in the scenarios (`Exit 0 · 0.4 s`); cmd 0.04 s.
- **Idle-input banner:** appears about 12 s after the last output of a silent, CPU-idle process (10 s of silence plus the 1 s check tick). `Start-Sleep -Seconds 40` triggers it; `Esc` stops the run.
- **Stop:** `taskkill /T /F` on the process tree, status "Stopped" within the scenario's 10 s window (observed under 2 s).

### Bugs found by looking at the screens
- With redirected output, `powershell.exe -EncodedCommand` serialises the error stream as CLIXML (`#< CLIXML <Objs ...>`), even with `-OutputFormat Text`. The script now merges the error stream inside a wrapper and writes each error record as plain text to stderr, remembering that something failed so the exit code stays 1.
- A multi-line `TextBox` inside `FSearchBox` was laid out as a one-line box, which collapsed the command editor to a single visible character; multi-line mode now fills the box.

### Decisions and deviations
- **Names:** the plan's `Launch.Stream`/`KillTree` live in `RunEngine` (start, stream, stop, sync run, table parser), `RiskScan` is its own class, and the standalone window is `RunForm`. The Run console is a standalone window opened from the Power menu (empty-search link, row menu "Open in Run console") and `PSTools.exe run "<dir>"`; embedding it as a main-window page happens with the Phase 6 shell rework.
- **Objects wrapper:** `Show as table` runs the command again with `AsTable` (JSON via `ConvertTo-Json -Depth 2`, first 5000 objects, values stringified, dates as `yyyy-MM-dd HH:mm:ss`). Only the first run of a command is recorded in history.
- **Confirm UX:** `modifies` shows an inline bar with Run / Run once / Cancel (Run remembers that exact command and shell); `destructive` always shows a modal with the command, the reasons and the working folder. `-WhatIf` downgrades to safe. The scanner is a heuristic (55 commands in the selftest); quoted text is ignored, aliases (`rm`, `del`, `ri`) are covered.
- **Autocomplete** is a small built-in command list plus history first words plus folder entries for path-like tokens; the generated `cheatsheet.json` command list replaces the built-in list in Phase 5.
- **Save as action:** defaults to Folder + Inside a folder (background) so an action saved from the console shows up when right-clicking a folder, not only its empty space. A literal copy of the working folder in the command is offered as `{dir}`. Ids come from menu and title (`user.search-python-files`), duplicates get a numeric suffix. The file is a `.ps1`/`.cmd` with the header block from the plan, written to `%LOCALAPPDATA%\PSTools\actions\<menu>\`.
- **History import** reads `ConsoleHost_history.txt` files from the PSReadLine folder (newest first, backtick continuation lines joined, duplicates dropped).
- **Lint calibration:** the pixel lint no longer reports the black DWM frame ring around a captured window as a dark region (a region touching all four image edges and under 10 percent of the image). This removed 16 false positives from the v3 baseline numbers (`light-dark-box` 17 to 1); no other rule changed. Run console and Save as action screens add 0 findings.
- **UI Automation notes:** owned modal dialogs appear under their owner window in the UIA tree, not as top-level windows; a label and its ComboBox can share a name, so lookups filter by control type; combos are driven through `ValuePattern` and `ExpandCollapsePattern`.
- **Selftest isolation:** tests that start real processes redirect the data folder to a temp folder (the footprint check caught an earlier leak into `%LOCALAPPDATA%\PSTools`).

## Phase 4

### What was built
- **125 action ids in POWER_ACTIONS.md plus the prose lists (clip, rename, system, fix) are all in the catalog: 313 actions in total** (the other 108 are the v3 library). 66 are declarative PowerShell actions in `actions.json` (tree, lists, paths, AI, git, search, open, inspect, compare, archive, media, dev, system), the rest are native C# (`ClipOps`, `PowerActions`) or v3 built-ins re-homed under the plan's ids and titles.
- **Gates (all in CI):** `actions-check.ps1` parses every declarative command with the Windows PowerShell 5.1 parser; `coverage-check.ps1` fails if any id in POWER_ACTIONS.md has no action or any `[T]` action has no test; `--test-actions` runs 127 golden/undo tests on a fresh copy of the fixtures; the UI scenarios include the section Z demo tour.
- **Demo tour (scenarios 11-16, all pass):** `ta` copies the tree and the Run console shows it back; "for ai" copies the folder for AI (toast captured); "folders from" previews and creates the pasted tree and `copy.tree.ascii` reproduces it exactly; "locking" lists the PowerShell process that holds a file open (Restart Manager); "permalink" copies a GitHub blob link with the 40-character commit; "Show as table" then "Save as action" under Inspect, then the saved action runs from the popup.

### Decisions and deviations
- **No Windows machine for the author:** declarative scripts were checked locally with PowerShell 7 on Linux (logic, quoting, Unicode) and then on windows-latest with PowerShell 5.1; goldens were produced by CI, read line by line, and committed. Menu and tree goldens were produced with Mono and match Windows.
- **Re-homed v3 actions keep working.** `copy-tree`, `copy-dirs-tree`, `copy-dirs-rel` and `copy-json` now resolve to the new declarative implementations (hidden items excluded, ordinal folders-first sort, `copy.tree.ascii` matches the spec's example exactly). Their golden output changed on purpose. Other v3 ids still use the original C#.
- **Four "window" actions are grids:** `inspect.facts`, `inspect.locking`, `inspect.origin` and `compare.two-selected` show their result in the table window instead of bespoke dialogs. `inspect.locking` uses the Restart Manager API through `Add-Type` (compile on first use, about 1 s).
- **Toast override:** a script can end its output with a `##PST-TOAST## text` line; the executor uses it as the toast and keeps it out of the clipboard (used by `copy.ai.*` for "Copied 23 files (41 KB, about 10k tokens)" and by the offline case of `system.ip-public`). Scripts also get `PSTOOLS_DATA` (data folder) and `PSTOOLS_EXE` in their environment.
- **Journaled C# actions:** rename (lower, kebab, snake, title, date prefix, numbering, lower-case extensions), `organize.by-date`, `new.file`, shortcut/hard link/symlink/junction paste, `new.from-clip-tree` (confirm with a preview) all go through `Journal` and have undo tests. Declarative `modifies` actions (zip, extract-tar, PATH edits) are not journaled; `dev.env-path-*` saves the previous PATH in `changes.txt` in the data folder.
- **Interpretations of the spec** where it was silent: `rename.number` uses the first file in Explorer order for the base name; `copy.path.rel-git` uses forward slashes; `copy.tree.sizes` right-aligns human sizes in a 10-character column; the clipboard file/image actions are native and covered by the same coverage gate but have no golden because the clipboard formats cannot be asserted through the test sink.
- **Not verified in CI:** `new.paste-symlink` (needs Developer Mode or admin; the test list only covers shortcut, hard link and junction), `open.*` launching real terminals, `dev.port-kill`, `system.update-all`, and any action that needs ffmpeg, ImageMagick, Node or Python (they hide when the tool is missing).
- **Titles** follow the doc; four are slightly shorter than the doc because of the 32-character limit ("Who changed this file?", "Remove folder from user PATH", "Copy as file for pasting", "Copy folder for AI"). Ellipses use the real character (U+2026).
- **Test harness additions:** folder copies keep creation/modified times and attributes (the first goldens showed a hidden folder and a "now" timestamp), WSL-style paths are normalised, `PID` values are normalised, and specs can set `inputs`, `input`, `clipboardFile`, `clipboardFiles`, `clipboardImage`, `marks`, `lock`, wildcard `expectFiles` and `skipUnless`.
