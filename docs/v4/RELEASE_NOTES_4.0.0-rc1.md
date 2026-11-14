# PS Tools 4.0.0-rc1 (release candidate)

This is a release candidate for v4. Phases 0 to 5 of the rebuild plan are done and tested in CI. Phases 6 (main window redesign) and 7 (README and docs rewrite) are not done yet, so the main window still looks like the v3 window with new pages added.

## What is new since v3.1.0
- **One action model.** All 108 v3 actions were converted into a catalog with ids, menu paths, contexts and risk levels. User actions live in a folder and can be overridden or hidden. A Problems list shows broken user actions.
- **Power menu.** Right-click, open the Power menu, type two or three letters and press Enter. It has Recent, Suggested and Favorites sections, ranked search, keys 1 to 9, details on `?`, inline inputs and a Shift view for rare or risky actions. Ctrl+K in the app opens the same popup.
- **Run console.** `PSTools.exe run "<folder>"` or from the Power menu. Windows PowerShell 5.1, PowerShell 7 or cmd, autocomplete, history with Ctrl+R (PSReadLine history can be imported), stop that kills the whole process tree, show as table, copy command plus output, open in terminal. Commands that change files are flagged by a risk scan and ask first.
- **Save as action.** Turn any command you ran into a menu action, with placeholders such as `{dir}` and `{input:...}`.
- **313 actions.** 66 new declarative PowerShell actions plus native clipboard, rename, organize and paste-and-new actions. 127 golden and undo tests cover them.
- **Cheat sheet.** 399 task-style PowerShell entries in 25 categories, a cmd to PowerShell table (61 rows) and a bash to PowerShell table (106 rows). Open it from the rail or with `PSTools.exe cheatsheet <folder>`. Search finds commands by name, alias or keyword, so typing `grep` finds Select-String. Try here opens the example in the Run console and never runs it. Save as action keeps placeholders.

## What is not in this release
- The Phase 6 main window redesign (home page, settings page, keyboard tour, new baselines).
- The Phase 7 README rewrite and `docs/USER_ACTIONS.md`.
- Testing on real hardware. Everything was verified on GitHub Actions `windows-latest` runners (build with the real .NET Framework csc, self-test, 127 action tests, parse of every declarative command and every cheat sheet entry on Windows PowerShell 5.1, 224 cheat sheet entries executed, 30 UI Automation scenarios, light and dark screenshots at 100% and 150%).
- 175 cheat sheet entries (network, administrator, file-changing and a few slow ones) are parsed and risk-checked but never executed in CI.

## Install
Unzip all files into one folder and run `PSTools.exe`. `actions.json`, `cheatsheet.json` and `commands.json` must stay next to the exe. There is no installer and no background process. Settings and history are kept under `%LOCALAPPDATA%\PSTools` and in HKCU, as in v3. Migration backs up your settings and leaves recipes and undo history untouched.

Check the download with `SHA256.txt`.

## Known gaps
- The exe is not code-signed, so SmartScreen may warn.
- Rare layouts below the minimum window size were only checked at the sizes in the screenshot gallery.
