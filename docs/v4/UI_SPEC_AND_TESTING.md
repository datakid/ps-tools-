# PS Tools v4: UI Spec and UI Testing (mandatory reading)

> Why this document exists: agents usually ship UI that compiles but looks wrong (clipped text, misaligned rows, dark-mode white boxes, default WinForms grey, inconsistent spacing, DPI blur). In v4, **"it builds" is never evidence that the UI is right.** The only evidence is: (1) automated layout lint, (2) automated pixel lint, (3) visual diff, (4) UI Automation scenarios, and (5) **you looking at every screenshot and filling in the rubric in §7**. All five are required in every PR that touches UI.

---

## 1. Design principles
1. **Windows 11 native feel.** Same visual language as Settings / Terminal / PowerToys Run: Mica-like flat surfaces, 4/8 px rounded corners, subtle 1 px strokes, the accent colour used sparingly (selection, primary button, focus ring).
2. **Keyboard first.** Everything reachable without the mouse. A visible focus ring always.
3. **Calm density.** 8 px spacing grid. No element touches another. No text closer than 8 px to a card edge.
4. **One control library.** Only `Fluent.cs` controls (+ new ones added *to* `Fluent.cs`). No raw `Button`, `CheckBox`, `ComboBox`, `ListView`, `TreeView`, `DataGridView` without owner-draw/theming, because they render as grey Win95 boxes in dark mode.
5. **Explain before acting.** The risk is visible on every action row (dot/shield). Destructive actions always show the exact script.

## 2. Design tokens (put them in `Fluent.cs` as `static class Tok`, and never hard-code colours or sizes elsewhere)

### 2.1 Spacing / shape
| Token | Value (at 96 DPI; multiply by `UI.Scale`) |
|---|---|
| `S1..S6` | 4, 8, 12, 16, 24, 32 |
| `RadiusSmall` | 4 (buttons, inputs, list row highlight) |
| `RadiusLarge` | 8 (cards, popup window, dialogs) |
| `Stroke` | 1 |
| `RowHeight` | 36 (lists, popup rows) |
| `RowHeightCompact` | 32 (grids) |
| `InputHeight` | 32 |
| `ButtonHeight` | 32, min width 96, horizontal padding 12 |
| `RailWidth` | 64 collapsed (icons + tooltips) / 220 expanded (≥ 1100 px window width) |
| `IconSize` | 16 in lists, 20 in the rail |
| `FocusRing` | 2 px accent, 2 px offset, radius +2 |

### 2.2 Typography
| Role | Font | Size | Weight |
|---|---|---|---|
| Body | Segoe UI Variable Text → Segoe UI | 10pt (≈ 14 px) | Regular |
| Caption / secondary | same | 9pt | Regular, `TextSecondary` |
| Subtitle (card title) | Segoe UI Variable Display → Segoe UI Semibold | 12pt | Semibold |
| Title (page title) | same | 20pt | Semibold |
| Mono (commands, output) | Cascadia Mono → Consolas | 10pt | Regular |
| Icons | Segoe Fluent Icons → Segoe MDL2 Assets | 12pt (16 px) | – |

Text rendering: always `TextRenderer.DrawText` with `TextFormatFlags.NoPrefix | EndEllipsis | VerticalCenter` (+ `WordBreak` only for multi-line labels). Never use `Graphics.DrawString` for UI text (it renders differently and blurs). Never let text clip without an ellipsis, and show a tooltip with the full text when an ellipsis was applied.

### 2.3 Colours (light / dark)
| Token | Light | Dark |
|---|---|---|
| `Window` (app background) | `#F3F3F3` | `#202020` |
| `Surface` (card) | `#FFFFFF` | `#2B2B2B` |
| `SurfaceAlt` (popup, hover base) | `#F9F9F9` | `#2C2C2C` |
| `Stroke` | `#E5E5E5` | `#3A3A3A` |
| `StrokeStrong` (input border) | `#8A8A8A` (bottom 1 px) / `#D1D1D1` others | `#9A9A9A` / `#454545` |
| `Text` | `#1B1B1B` | `#FFFFFF` |
| `TextSecondary` | `#5D5D5D` | `#C5C5C5` |
| `TextDisabled` | `#A0A0A0` | `#787878` |
| `Hover` | `#0000000A` overlay (≈ `#EAEAEA` on white) | `#FFFFFF0F` (≈ `#353535`) |
| `Pressed` | `#00000006` | `#FFFFFF0A` |
| `Selected` row | accent at 12% over Surface + 3 px accent pill on the left (height 16, radius 2) | same with accent at 18% |
| `Accent` | from registry `HKCU\Software\Microsoft\Windows\DWM\AccentColor` (v3 already reads it), with test override `--accent` | same, lightened 1 step for text on dark |
| `OnAccent` | `#FFFFFF` (or `#000000` if the accent luminance > 0.5) | same rule |
| `Risk.Modifies` | `#9D5D00` | `#FCE100` |
| `Risk.Destructive` | `#C42B1C` | `#FF99A4` |
| `Success` | `#0F7B0F` | `#6CCB5F` |
| `ConsoleBg` | `#FFFFFF` | `#0C0C0C` |
| `ConsoleErr` | `#C42B1C` | `#FF99A4` |

Contrast gate: `Text` and `TextSecondary` on `Surface` and `Window` must be ≥ 4.5:1. Accent text on Surface must be ≥ 3:1, otherwise use `Text`. `--selftest` computes these ratios for both themes and for these accents: `0067C0`, `E81123`, `FFB900`, `00CC6A`, `744DA9`, `FFFFFF`, `000000`.

### 2.4 Dark mode completeness
- Title bar: `DwmSetWindowAttribute(hwnd, 20 /*DWMWA_USE_IMMERSIVE_DARK_MODE*/, 1)`, falling back to attribute 19 on older Windows 10 builds.
- Scrollbars: call `SetWindowTheme(hwnd, "DarkMode_Explorer", null)` on every scrollable control in dark mode. Pixel lint checks that no light scrollbar track exists in dark screenshots (§6.3).
- Tooltips, context menus (`ContextMenuStrip` with a custom `ToolStripProfessionalRenderer` using the tokens), dialogs, combo dropdowns: all themed. **A white box in a dark screenshot is a failed PR.**

## 3. Shared components (define them in `Fluent.cs`)
1. `FTreeList`: the core list used by the popup, the Actions page, history and the cheat sheet. Virtualized (draw only the visible rows). Owner-drawn rows (§3.1). Supports a flat or drill-in mode, sections with headers, keyboard behaviour, and `AccessibleObject` per row.
2. `FSearchBox`: 32 px, search glyph left (E721), clear button right (E711) when non-empty, placeholder in `TextSecondary`, accent bottom border 2 px when focused.
3. `FSegmented`: segmented control (shell picker, theme picker).
4. `FBreadcrumb`: `Copy › Tree`, each segment clickable, chevron E76C at 8pt.
5. `FInfoBar`: inline banner (info/warning/error/success) with an icon, a message, and up to 2 action links plus a close button.
6. `FConsole`: output view (virtualized lines, mono font, selection, copy, find).
7. `FGrid`: themed owner-drawn grid (or a themed `DataGridView`, provided every part is themed, including headers, the corner, scrollbars and the selection).
8. `FKbd`: small keycap chip (`Ctrl` `K`), 1 px stroke, radius 4, caption font.
9. `FToast`: v3 toast, restyled with tokens.

### 3.1 List row anatomy (36 px, used everywhere)
```
| 12 | icon 16 | 12 | Title (Body, Text)            path (Caption, TextSecondary) | flex | risk dot 8 | 8 | kbd "1" | 8 | chevron › | 12 |
```
- Selected: pill + selected fill, radius 4, inset 4 px left/right from the list edges.
- Hover: hover fill, radius 4.
- Section header: 28 px, Caption Semibold, `TextSecondary`, uppercase NOT allowed (sentence case), 12 px left padding, 8 px top margin. Not selectable.
- Folder row: chevron E76C right-aligned, `TextSecondary`.
- Favorite: a small filled star E735 in accent before the risk dot.
- Disabled (`requires` missing, shown only on the Actions page): `TextDisabled`, tooltip "Needs git on PATH".

### 3.2 Icons (Segoe Fluent / MDL2 code points)
Copy E8C8, Paste & New E77F, Search E721, Open E8A7, Rename E8AC, Organize E8B7 (folder), Archive E7B8, Image EB9F, Media E8B2, Inspect E9D9, Compare E8EF (or E1CA), Dev E943, Git E8D4 (or use the branch glyph if available), Links E71B, Clean up E74D, Fix E90F, Clipboard E77F → use F0E3 if it exists, else E8C8, System E770, Recipes E8FD, My actions E77B, Library E8F1, Run console E756, Favorite E734/E735, Shield (admin) EA18, Back E72B, Chevron E76C, Settings E713, Home E80F.
If a glyph renders as a box (font missing), fall back to a safe set in MDL2. `--selftest` checks every code point with `GetGlyphIndices` and falls back automatically.

### 3.3 Buttons
Primary: accent fill, `OnAccent` text. Secondary: Surface fill, 1 px Stroke. Subtle: transparent, hover fill. All radius 4, height 32. Icon+text buttons: icon 16, gap 8. Disabled: 40% opacity. **Never two primary buttons in one view.**

## 4. Screens (exact layouts)

### 4.1 Main window
- Default size 1100×720, min 880×560. Remembers size/position (v3). Title bar themed.
- Left rail (§2.1 widths). Items: Home, Actions, Run, Cheat sheet, (spacer), History, Settings. Selected item = pill + accent icon.
- Content area: padding 24 (S5). The page title (Title font) at top-left, page actions right-aligned on the same baseline.

#### Home
- Working folder card (path box + Browse + drop zone hint) at the top.
- "Suggested here" card (up to 6 chips/rows from Suggest.cs).
- "Favorites" card (rows, drag to reorder, star toggles).
- "Recent" card (last 8 runs, including Run-console commands, re-runnable).
- "Get started" card, shown until dismissed, with 3 steps: Try the Power menu (button opens the popup on the working folder), Save a command as an action, Install Explorer menu (only if not installed).
- Two-column grid ≥ 1000 px, one column below.

#### Actions
- Left: `FTreeList` in drill-in mode with a search box (the same experience as the popup, larger). Right: details card (title, path, description, risk badge, "Shows for" chips, PowerShell equivalent in an `FConsole` read-only block with a Copy button, source, buttons: **Run** (primary), Run with options…, Star, Open in Run console, Duplicate as my action / Edit file, Hide).
- Splitter at 45% / 55%, min 320 px each.

#### Run (§4.4)

#### Cheat sheet
- Three panes: categories (200 px), entries list (flex, rows of 2 lines: title + mono command chip), detail (360 px: description, syntax, parameters table, example + example output in `FConsole`, related links). Buttons: **Try here** (primary), Copy, Save as action. A tab strip above the list: `PowerShell` | `cmd → PS` | `bash → PS`.

#### Settings
Sections as cards: Appearance (theme System/Light/Dark segmented, accent follows Windows), Explorer menu (Install/Remove, "Favorites in menu: 6 of 8" with a list, toggles: full tree as native submenus, favorites at top level, a preview tree built from `--dump-menu` logic), Actions folder (path, Open folder, Reload, "Show problems (2)"), Run console (default shell, confirm modifies toggle, history size, Import PowerShell history), Data & privacy (data folder, Clear history, Full uninstall), About (version, build hash, links).

### 4.2 Power Menu popup
- Width 420. Height = header 44 + search 48 + list (rows × 36, min 4 rows, max 12 rows = 432) + footer 32. With details open: width 420 + 280.
- Outer: radius 8, 1 px Stroke, drop shadow, background `SurfaceAlt`.
- Header (44): 12 px padding, folder/file icon 16, selection summary Body Semibold (ellipsis in the middle for long paths: `C:\Users\…\src`), right side: a pin icon "Open app" (E8A7, subtle).
- Search (48): `FSearchBox` with 8 px margin.
- Breadcrumb (28, only when drilled in), between the search and the list.
- List: `FTreeList`.
- Footer (32): `FKbd` hints in Caption: `↵ Run` `→ Open` `← Back` `Ctrl D ★` `? Details`. Hide hints that don't fit, right to left.
- Empty search result: centered "No actions match "xyz"" plus a subtle link button **Run "xyz" in console** (opens the Run console with that text). **This is a key power feature.**

### 4.3 Save as action dialog: 640×600, form layout with labels on the left (140 px), inputs on the right, sections separated by 24 px. The placeholder suggestion uses `FInfoBar` (info). The preview uses an `FConsole` (3 lines). Buttons bottom-right: Test run (secondary), Cancel (secondary), Save (primary).

### 4.4 Run page / Run window (standalone 900×620, min 700×480)
```
┌ Working dir [C:\proj\src                         ▾][Browse]   [PowerShell 5.1|PowerShell 7|cmd] ┐
├ Editor (mono, 3-10 lines, rounded input)                                                       ┤
│ Get-ChildItem -Recurse -Filter *.py                                                            │
├ [▶ Run  Ctrl+↵](primary) [■ Stop](secondary, only while running) [Save as action…] [History Ctrl+R] … [Open in terminal] ┤
├ FInfoBar (risk warning / waiting for input) – only when needed                                  ┤
├ Output toolbar: Copy all · Copy cmd+output · Show as table · Wrap · Find · Clear   status: ● Exit 0 · 1.24 s ┤
│ FConsole output (fills the rest)                                                               │
└────────────────────────────────────────────────────────────────────────────────────────────────┘
```

### 4.5 First run (one screen, 640×480)
Title "Welcome to PS Tools", 3 short bullets (Power menu, Run anything, Save commands as actions), a toggle "Add PS Tools to the Explorer right-click menu" (on), a toggle "Use the classic right-click menu on Windows 11" (off, Windows 11 only, with a one-line explanation), and one primary button "Get started". No presets.

## 5. Behaviour details agents usually get wrong (checklist when coding)
- [ ] DPI: the manifest has `PerMonitorV2` (check `app.manifest`; if v3 uses `system`, upgrade to `<dpiAwareness>PerMonitorV2, PerMonitor</dpiAwareness>` + `<dpiAware>true/pm</dpiAware>`). All sizes go through `UI.S(int)`. Handle `WM_DPICHANGED` (v3 `UI.cs` may already, so check and extend). No bitmap scaling.
- [ ] `AutoScaleMode` is consistent (Dpi or None + manual scaling, follow what v3 does; do not mix).
- [ ] Flicker: `DoubleBuffered = true` / `SetStyle(OptimizedDoubleBuffer | AllPaintingInWmPaint | UserPaint)` on every owner-drawn control.
- [ ] Long paths/titles end with an ellipsis + tooltip. Never wrap a title into a 36 px row.
- [ ] Tab order is logical on every page (set `TabIndex` explicitly). The focus ring is visible on every focusable control.
- [ ] `Esc` closes dialogs/popup. `Enter` triggers the primary button. `Alt` mnemonics aren't needed.
- [ ] No layout jump when a banner appears: banners push content down with no overlap.
- [ ] Resizing to min size: nothing overlaps, and scroll appears where content can't fit.
- [ ] Empty states for every list ("No favorites yet. Press Ctrl+D on any action to add it.").
- [ ] Loading states: a spinner/`FProgress` in the content area, never a frozen UI (all IO goes on a background thread, then BeginInvoke).

## 6. UI testing system (build it in Phase 0, extend in every phase)

### 6.1 Deterministic screenshot mode (`UiHarness.cs`)
CLI: `PSTools.exe --screenshot <outDir> [--theme light|dark] [--accent RRGGBB] [--scale 1|1.25|1.5] [--only <name,...>] [--fixtures <dir>] [--quiet]`
- Forces the theme and accent (ignores the registry), fixed fonts (if Segoe UI Variable is missing on the runner, use Segoe UI. Record the font actually used in the PNG metadata/filename suffix), fixed clock (`2026-01-15 10:30`), fixed username in paths (`C:\Users\demo`), a temporary settings dir (`--settings` isolated in `%TEMP%\pstools-shot-*`), animations off, caret off.
- Renders each **named scenario** off-screen-safe: create the form, set the state (via an internal `IScreenshotState` method per form), `Show()`, pump messages 3× with `Application.DoEvents()` + `Thread.Sleep(50)`, then capture.
- Capture: `PrintWindow(hwnd, hdc, 2 /*PW_RENDERFULLCONTENT*/)` into a 32bpp bitmap. If it returns a black/empty image (detect: > 98% identical pixels), fall back to `Control.DrawToBitmap` and mark `-dtb` in the manifest. Window position: on the primary screen at (40,40), not minimized (runners have an interactive desktop session on `windows-latest`. If capture is black, record this in FINDINGS and use the fallback).
- Output: `<outDir>/<scenario>.<theme>.<scale>.png` + `manifest.json` (`[{name, file, width, height, theme, scale, font, method, controls:[{name,type,bounds,text,visible}]}]`). The control map lets compare.ps1 lint exact element boxes.

**Required scenarios (grow by phase):**
| Name | State |
|---|---|
| `main-home` | Home with fixtures/tree-basic as the working folder, 4 favorites, 5 recent, get-started visible |
| `main-home-empty` | no favorites, no recent |
| `main-actions-root` | Actions page, root, first row selected |
| `main-actions-drilled` | inside Copy › Tree, 2nd row selected, details showing |
| `main-actions-search` | search "hash" |
| `main-run-idle` | Run page, empty editor |
| `main-run-output` | after running `Get-ChildItem <fx>\tree-basic -Recurse \| Select -First 30` (real run), status Exit 0 |
| `main-run-error` | after `Get-Item C:\nope`, red status, stderr coloured |
| `main-run-table` | Show as table result window |
| `main-run-risk` | editor contains `Remove-Item .\x -Recurse`, the risk confirm visible |
| `main-cheatsheet` | Cheat sheet, category Files, an entry selected |
| `main-cheatsheet-bash` | bash → PS tab |
| `main-settings` | Settings scrolled to the top |
| `main-settings-menu` | Explorer menu card with preview |
| `popup-root-folder` | popup for a folder, Recent + Suggested + Favorites + folders |
| `popup-root-file-image` | popup for a .png (Image section suggested) |
| `popup-drilled` | Copy › Tree |
| `popup-search` | "ta" |
| `popup-search-empty` | "xyzzy" with the "Run in console" link |
| `popup-details` | details open |
| `popup-multi` | 200 items selected (header "200 items · 200 files") |
| `popup-inputs` | an action with inputs showing its inline form |
| `save-action` | dialog with the placeholder suggestion bar visible |
| `results-grid` | v3 ResultsForm restyled with 50 rows |
| `history` | UndoHistoryForm |
| `first-run` | FirstRun screen |
| `toast` | toast with an Undo link |
| `dialog-destructive` | FDialog destructive confirm |
Each scenario is rendered in light+dark at scale 1.0, and the main + popup ones also at 1.5. **Minimum ~70 PNGs by Phase 6.**

### 6.2 Visual diff (`app/tests/ui/compare.ps1`)
- Per PNG vs `app/tests/ui/baseline/<same name>`: per-pixel diff with a tolerance of 16/255 per channel (anti-aliasing). Fail if the changed-pixel ratio > 0.5% **or** the image size differs. Write a diff PNG (changed pixels magenta over a 30% faded original) + `report.html` with a side-by-side view and `report.json`.
- No baseline yet → status `new` (not a failure, but the PR must say so). Baselines get committed only after the owner approves the gallery (Phase 6). Before Phase 6, the visual diff runs as a **warning** (it reports, doesn't fail) and the pixel lint plus layout lint are the hard gates.
- Implement it in PowerShell with System.Drawing + `LockBits` (C# via `Add-Type` inline is OK in test tools), never `GetPixel` loops (too slow).

### 6.3 Pixel lint (hard gate from Phase 0 on new v4 screens)
Using the manifest control map + pixels:
1. **Text clipping**: for every Label/text control in the manifest, re-measure the text with `TextRenderer.MeasureText` at the recorded font and bounds. Fail if it needs more space and the control doesn't use an ellipsis (re-use the layout-check logic).
2. **Overlap**: fail if two visible sibling controls' bounds intersect by > 1 px (except intentional overlays tagged `Tag="overlay"`).
3. **Out of bounds**: a control extends past its parent's client rect (non-scrolling parent).
4. **Dark-mode white boxes**: in `*.dark.*` images, fail if any connected region > 400 px² has luminance > 0.85 and isn't inside a control tagged `allow-light` (e.g. image thumbnails).
5. **Light-mode dark boxes**: the inverse, a luminance < 0.12 region > 400 px² in light images (except `ConsoleBg` in dark-console mode, tagged).
6. **Default WinForms grey**: fail if any region > 400 px² equals exactly `#F0F0F0` (SystemColors.Control) or `#E1E1E1` (default button). That means an unthemed control.
7. **Edge padding**: text-bearing controls inside cards must have ≥ 8 px from the card edge (cards tagged `card`).
8. **Alignment**: siblings in the same `FlowLayout`/column whose left edges differ by 1-3 px → fail (that's misalignment; 0 or ≥ 4 px is intentional).
9. **Glyph boxes**: detect the "tofu" box by rendering each used glyph alone and checking it isn't identical to the `.notdef` glyph render (done once in selftest, §3.2).
10. **Contrast**: sample the text colour vs the background in each Label bounds (the mode colours in the region). Fail < 4.5:1 for body text.

### 6.4 UI Automation scenarios (`app/tests/ui/scenarios.ps1`, Windows PowerShell 5.1)
Use `UIAutomationClient` (`[System.Windows.Automation.AutomationElement]`) and `SendKeys`/`SendInput` for keys. Each scenario launches the exe with `--settings <temp>` and `--fixtures`, waits for the window (`AutomationElement.RootElement.FindFirst` with name "PS Tools…" / popup name "PS Tools Power menu", timeout 10 s), drives it, asserts, closes, and takes a screenshot at each assertion point into `out/shots/scenario-<name>-<step>.png` (they also appear in the gallery).
Scenarios:
- `popup-open`: `PSTools.exe menu "<fx>\tree-basic"`. Assert: the window appears ≤ 1500 ms, the search box has keyboard focus, the first row is selected, and the row count matches `--dump-tree --json` for that context.
- `popup-search`: type `ta`. Assert: the first result's name == "Tree (ASCII)" (or whatever the canonical id `copy.tree.ascii` title is); Enter → the process exits, and the clipboard text equals golden `copy.tree.ascii.txt` (after normalization).
- `popup-drill`: Right on "Copy" → the breadcrumb shows "Copy", Left → back to root, Esc → the process exits with code 0.
- `popup-favorite`: Ctrl+D on a row → reopen the popup → the item is in the Favorites section; `--dump-menu` contains it.
- `popup-perf`: 5 cold launches, record the median time to the first `WindowOpened` event. Fail > 1500 ms, warn > 700 ms. Write it to the report.
- `popup-empty-run`: type `Get-Date -Format yyyy` → the empty state shows → Enter on the "Run in console" link → the Run window opens with the editor containing the text.
- `run-echo`: Run window, type `'hello'; 1+1`, Ctrl+Enter → the output contains `hello` and `2`, status Exit 0.
- `run-cmd`: select cmd, `echo %CD%` → the output equals the working dir.
- `run-stop`: `Start-Sleep 30` → Stop within 2 s → status `Stopped`, no orphaned powershell child (check with `Get-CimInstance Win32_Process` by parent pid).
- `run-table`: `Get-ChildItem <fx>\tree-basic\src -Recurse -File | Select Name, Length` + Show as table → the grid has the columns Name, Length and 2 rows.
- `save-action-roundtrip`: Run `Get-ChildItem -Name -Filter *.py -Recurse` in `<fx>\tree-basic` → Save as action → accept the `{dir}` replacement → title "Python files" in Search → Save. Assert the file exists in the user actions dir with the header `@menu Search`. Open the popup on `<fx>\tree-basic\src` → search "python" → Enter (output clipboard) → the clipboard has `app\main.py` and `app\util.py`.
- `keyboard-tour` (Phase 6): Tab through the main window from start to end. Assert every focused element has a non-empty `Name`, focus never gets stuck, and the order matches the spec list in `app/tests/ui/tab-order.txt`.
- `theme-switch`: Settings → Dark → assert the title bar dark attribute (screenshot sample: the title bar region luminance < 0.2).

### 6.5 Layout lint (extend v3 `tools/layout-check.ps1`)
Add every new form/page: Home, Actions (root/drilled), Run (idle/output), Cheat sheet, Settings, PowerMenu (root + details), SaveActionForm, FirstRun. Sizes: min, default, 1600×1000. Run at scale 1.0 and, via `--scale 1.5` (harness forces `UI.Scale`), 1.5.

### 6.6 Gallery in the PR (see V4_PLAN §10.2)
The PR comment table must show light/dark/diff for every scenario changed in the PR. The agent must **open (read) every new/changed PNG itself** before requesting review.

## 7. UI review rubric (copy into every UI PR and answer each with ✅ / ❌ + note)
For **each** changed screen, light and dark:
1. Does every text fit, or end with an ellipsis, with no clipped descenders (g, y, p)?
2. Is everything on the 8 px grid: equal gaps between cards, same left edge for stacked elements?
3. Is there any default grey WinForms control, white box in dark mode, or light scrollbar in dark mode?
4. Is the accent used only for selection, the primary button, focus and links, with exactly one primary button per view?
5. Are row heights consistent (36), icons vertically centred, chevrons aligned on the right edge?
6. Are the focus ring and the selected row clearly visible in both themes?
7. Is the risk visible on modifying/destructive/admin rows?
8. Empty, loading and error states exist and look intentional?
9. At 150% scale, is it sharp (no blurry bitmaps), with no overlap?
10. At min window size, is nothing overlapping, with scroll where needed?
11. Does it look like it belongs next to Windows 11 Settings? (If it looks like WinForms from 2005, it fails.)
12. Compared with the baseline / previous screenshot: is every visible difference intended? List them.
Write the answers in the PR. Any ❌ blocks the PR until it's fixed.

## 8. Reference look
To calibrate taste: PowerToys Run / Command Palette (popup), Windows Terminal settings (Settings page), Windows 11 Settings app (cards, rail), VS Code command palette (search ranking + path hints). You can't copy assets, only the visual language.
