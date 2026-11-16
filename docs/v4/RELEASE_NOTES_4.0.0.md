# PS Tools 4.0.0

PS Tools v4 is a rebuild of the v3 tool around one action model, a keyboard-first Power menu, a Run console, a cheat sheet and a folder of your own scripts.

## What is new since v3.1.0
- **New main window.** Home (working folder, suggestions, favorites, recent runs, get started), Actions (the full tree with search and a details card), Run, Cheat sheet, History and Settings. Ctrl+1 to Ctrl+6 switch pages, Ctrl+K opens the Power menu.
- **One action model.** The v3 actions were converted into a catalog with ids, menu paths, contexts and risk levels, and more than 300 actions ship in total. Your own actions live in a folder and can override or hide built-in ones. A Problems list shows broken user actions.
- **Power menu.** Right-click, open the Power menu, type two or three letters and press Enter. Recent, Suggested and Favorites sections, ranked search, keys 1 to 9, details on `?`, inline inputs, and a Shift view for rare or risky actions. Starring an action puts it in the Explorer menu right away.
- **Run console.** Windows PowerShell 5.1, PowerShell 7 or cmd, autocomplete, history with Ctrl+R (PSReadLine history can be imported), Stop kills the whole process tree, show as table, copy command plus output, open in terminal. Commands that change files are flagged and ask first (you can turn that off in Settings).
- **Save as action.** Turn any command you ran into a menu action, with placeholders such as `{dir}` and `{input:...}`.
- **Cheat sheet.** 399 task-style PowerShell entries in 25 categories, a cmd to PowerShell table (61 rows) and a bash to PowerShell table (106 rows). Searching `grep` finds Select-String. Try here opens the example in the Run console and never runs it. Save as action keeps placeholders.
- **Your own actions.** Drop a `.ps1`, `.cmd` or `.bat` with a comment header into `%LOCALAPPDATA%\PSTools\actions\`. `docs/USER_ACTIONS.md` lists every key and placeholder and has ten examples that CI loads with the real parser.
- **One-screen first run** and a Settings page with the Explorer menu (favorites, order, preview), the actions folder and its problems, Run console options, data and privacy, and About. The old presets and the separate menu preview window are gone.

## Verification
Everything was verified on GitHub Actions `windows-latest` runners: build with the real .NET Framework csc, self-test, action tests with golden files and undo checks, a parse of every declarative command and every cheat sheet entry on Windows PowerShell 5.1, execution of 224 cheat sheet entries, UI Automation scenarios including a keyboard-only tour, and light and dark screenshots at 100% and 150% with a pixel diff against reviewed baselines and a pixel lint.

## Known gaps
- Not tested on real hardware. In particular, how Windows 11 shows the entries in its modern right-click menu on a real PC (the classic-menu switch in the first-run screen avoids "Show more options").
- The exe is not code-signed, so SmartScreen may warn the first time.
- 175 cheat sheet entries (network, administrator, file-changing and a few slow ones) are parsed and risk-checked but never executed in CI.
- The v3 dialogs that v4 does not redesign (Recipes, Rename, Search, Structure, Library, Results grid, Space map) keep their v3 look and still have pixel lint warnings.
- Cold start to a visible Power menu was measured at a median of about 470 ms on CI (first launch of a fresh exe about 700 ms to 1 s), above the 700 ms target for a first launch only.

## Install
Unzip all files into one folder and run `PSTools.exe`. `actions.json`, `cheatsheet.json` and `commands.json` must stay next to the exe. No installer and no background process. Settings and history are kept under `%LOCALAPPDATA%\PSTools` and in HKCU, as in v3. Migration backs up your settings and leaves recipes and undo history untouched. Check the download with `SHA256.txt`.
