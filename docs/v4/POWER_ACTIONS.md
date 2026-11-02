# PS Tools v4: Power Actions Catalog

Legend:
- **Impl**: `C#` = built-in in C# (speed, Win32 API, journaling, or tricky parsing); `PS` = declarative in `app/actions.json` (PowerShell); `PS7` = needs pwsh features (set `fallback` or `requires: pwsh`).
- **When**: F = file, D = folder, B = background (inside a folder), V = drive.
- **Out**: clip = clipboard + toast; grid; console; toast; window; none.
- **Risk**: s = safe, m = modifies (confirm once, journaled if C#), d = destructive.
- **[T]** = must have an automated test spec + golden (coverage gate, V4_PLAN §11.2).
- v3 alias: the old id if the action exists in v3. **Reuse the v3 implementation** and re-home it (look it up in `CopyActions.cs`, `CreateActions.cs`, `QuickActions.cs`, `Classic.cs`, `DevActions.cs`, `SystemActions.cs`, `MapActions.cs`). Use `--list-actions` on v3 (Phase 0) to get the exact v3 ids, and fill in the alias column in `actions.json`/C#. The ids written below in *italics* are guesses that you must verify.

General rules for all tree/list actions:
- Skip reparse points (junctions/symlinks), as v3 does. Sort folders first, then files, case-insensitive ordinal (`StringComparer.OrdinalIgnoreCase`).
- "Build folders" excluded by the `nobuild` variants: `.git`, `node_modules`, `.vs`, `.vscode`(no, keep it), `__pycache__`, `bin`, `obj`, `dist`, `build`, `.next`, `.venv`, `venv`, `target`, `.gradle`, `.idea`. Put this in one shared list `Fs.BuildDirs` (extend the v3 zip list).
- `.gitignore` respect: if `{dir}` is in a git repo and git is present, use `git -C <dir> ls-files -co --exclude-standard` (cached + others, not ignored), then build the tree from that list. Otherwise use the built-in BuildDirs list. The toast says which was used ("respected .gitignore").
- Caps: max 20,000 entries or 2 MB of text. Beyond that, truncate and append a line `… (truncated: 23,104 more entries)`.
- Output trees use `\n` internally and CRLF when written to the clipboard (Windows apps expect it).

---

## A. Copy › Tree (C#, extend v3 `Structure.cs`)
| id | Title | When | Out | Risk | Notes | T |
|---|---|---|---|---|---|---|
| `copy.tree.ascii` | Tree (ASCII) | D B V | clip | s | `├── └── │` box-drawing, folders end with `/`. Root line = folder name. Depth all. *v3: copy-tree* | [T] |
| `copy.tree.ascii-plain` | Tree (plain ASCII) | D B | clip | s | `+--`, `\--`, `|` for apps that mangle Unicode | [T] |
| `copy.tree.folders` | Tree, folders only | D B V | clip | s | | [T] |
| `copy.tree.depth` | Tree to depth… | D B | clip | s | input `depth` pick 1/2/3/4 | [T] (depth 2) |
| `copy.tree.sizes` | Tree with sizes | D B | clip | s | right-aligned human sizes, folder = recursive total | [T] |
| `copy.tree.markdown` | Tree as Markdown | D B | clip | s | wrapped in ```` ```text ```` fence | [T] |
| `copy.tree.nested-list` | Tree as nested list | D B | clip | s | `- name` with 2-space indents (paste into Notion/Obsidian) | [T] |
| `copy.tree.gitignore` | Tree (respect .gitignore) | D B | clip | s | as above | [T] (repo fixture) |
| `copy.tree.json` | Tree as JSON | D B | clip | s | `{name,type,size?,children[]}` | [T] |
| `copy.tree.window` | Folder structure… | D B V | window | s | v3 StructureForm, restyled (all the options together) | – |

Golden for `copy.tree.ascii` on `tree-basic` (exact, which defines the format). Rules: folders first, then files, each alphabetical (case-insensitive); folders end with `/`; hidden items (`.hidden/`) are excluded by default (the setting "Include hidden" applies to all tree actions); build folders are **included** in this variant (only `nobuild`/`gitignore` variants drop them):
```
tree-basic/
├── bin/
│   └── out.dll
├── docs/
│   └── readme.md
├── empty/
├── node_modules/
│   └── pkg/
│       └── index.js
└── src/
    ├── app/
    │   ├── main.py
    │   └── util.py
    └── lib/
```
(Generate goldens with `--update-goldens`, then **read them** and check them against these rules before committing.)

## B. Copy › Names & lists
| id | Title | When | Out | Risk | Notes | T |
|---|---|---|---|---|---|---|
| `copy.names.selected` | Names of selected | F D | clip | s | one per line, multi=join, Explorer order (sorted) | [T] |
| `copy.names.stem` | Names without extension | F | clip | s | *v3 alias* | [T] |
| `copy.list.subfolder-names` | All subfolder names | D B | clip | s | direct children only | [T] |
| `copy.list.subfolder-names-rec` | All subfolder names (recursive) | D B | clip | s | unique, sorted | [T] |
| `copy.list.subfolder-paths` | All subfolder paths | D B | clip | s | absolute; *v3 alias* | [T] |
| `copy.list.subfolder-paths-rel` | All subfolder paths (relative) | D B | clip | s | `src\app` | [T] |
| `copy.list.file-names-rec` | All file names (recursive) | D B | clip | s | | [T] |
| `copy.list.file-paths-rel` | All file paths (relative) | D B | clip | s | respects nobuild by default | [T] |
| `copy.list.file-paths-abs` | All file paths (absolute) | D B | clip | s | | [T] |
| `copy.list.by-ext` | File paths of type… | D B | clip | s | input ext (prefilled with the top 5 extensions found) | [T] (.py) |
| `copy.list.csv` | Listing as CSV | D B | clip | s | `RelativePath,Name,Ext,Size,Modified` (ISO) | [T] |
| `copy.list.json` | Listing as JSON | D B | clip | s | array of the same fields | [T] |
| `copy.list.ext-summary` | File types summary | D B | clip | s | `.py  2 files  1.2 KB` sorted by size | [T] |
| `copy.list.count` | Count files and folders | D B | toast | s | "214 files, 31 folders, 12.4 MB" (also copies) | [T] |

## C. Copy › Paths (C#, mostly v3)
| id | Title | When | Out | Notes | T |
|---|---|---|---|---|---|
| `copy.path` | Path | F D V | clip | | [T] |
| `copy.path.quoted` | Path (quoted) | F D V | clip | *v3: copy-path-quoted* | [T] |
| `copy.path.fwd` | Path with / | F D | clip | *v3: copy-path-fwd* | [T] |
| `copy.path.json` | Path escaped (\\\\) | F D | clip | `C:\\a\\b` | [T] |
| `copy.path.ps` | Path as PowerShell literal | F D | clip | `'C:\a\O''Brien'` | [T] |
| `copy.path.wsl` | WSL path | F D | clip | `/mnt/c/...` | [T] |
| `copy.path.uri` | file:// URL | F D | clip | percent-encode spaces/unicode | [T] |
| `copy.path.unc` | UNC / network path | F D | clip | mapped drive → `\\server\share\...` (WNetGetUniversalName), else unchanged + toast | – |
| `copy.path.short` | 8.3 short path | F D | clip | GetShortPathName, toast if 8.3 is disabled | – |
| `copy.path.rel-git` | Path relative to repo root | F D | clip | requires git | [T] |
| `copy.path.rel-marked` | Path relative to marked folder | F D | clip | uses v3 Links "mark" | [T] |
| `copy.path.parent` | Parent folder path | F D | clip | *v3: copy-parent* | [T] |
| `copy.path.cd` | `cd` command for this folder | D B | clip | `Set-Location -LiteralPath '...'` | [T] |

## D. Copy › For AI / contents (C#)
| id | Title | When | Out | Risk | Notes | T |
|---|---|---|---|---|---|---|
| `copy.ai.folder` | Copy folder for AI | D B | clip | s | Header: `# <folder>` + ASCII tree (gitignore-aware) + each text file as `` ### rel/path `` + fenced block with a language from the extension map. Skip binaries (NUL byte in the first 8 KB, or a known binary ext), files > 200 KB, lock files (`package-lock.json`, `pnpm-lock.yaml`, `yarn.lock`, `poetry.lock`, `Cargo.lock`). Total cap 400 KB (setting). Footer: `Skipped: 3 binary, 1 large (names…)`. Toast: "Copied 23 files · 41 KB · ~10k tokens" (tokens ≈ chars/4). | [T] |
| `copy.ai.files` | Copy selected files for AI | F (multi) | clip | s | same format for the selection | [T] |
| `copy.contents` | File contents | F | clip | s | *v3: copy-contents*; detect the encoding | [T] |
| `copy.contents.fenced` | Contents as code block | F | clip | s | ```` ```py ```` | [T] |
| `copy.file.clipboard` | Copy as file (for pasting into apps) | F D (multi) | toast | s | `Clipboard.SetFileDropList` (works in Explorer, Teams, Slack, Outlook) | – |
| `copy.image.clipboard` | Copy image to clipboard | F[img] | toast | s | `Clipboard.SetImage` + PNG format for transparency | – |
| `copy.datauri` | Data URI | F | clip | s | *v3* | [T] |
| `copy.hash.sha256` / `.sha1` / `.md5` | Hash | F | clip | s | *v3* | [T] |
| `copy.hash.verify` | Verify hash from clipboard | F | toast | s | *v3* green/red toast | [T] |
| `copy.size` | Size | F D | clip | s | *v3* | [T] |

## E. Copy › Git (requires git)
| id | Title | When | Out | Notes | T |
|---|---|---|---|---|---|
| `git.copy.permalink` | Permalink to this file | F | clip | remote → https (handle `git@github.com:o/r.git`, `https://…/o/r.git`, GitLab, Bitbucket, Azure DevOps `dev.azure.com/org/proj/_git/repo?path=`). URL = `<web>/blob/<HEAD sha>/<rel path with />`. | [T] |
| `git.copy.branch` | Branch name | D B F | clip | | [T] |
| `git.copy.last-commit` | Last commit (hash + message) | D B F | clip | for a file: the last commit touching it | [T] |
| `git.copy.remote` | Remote URL | D B | clip | | [T] |
| `git.open.web` | Open repo in browser | D B F | none | file → the blob page | – |
| `git.status.repos` | Git status of all repos below | D B V | grid | *v3* (Repo, Branch, Ahead, Behind, Changes) | [T] |
| `git.pull.repos` | Pull all repos below | D B | grid | m, *v3* | – |
| `git.fetch.repos` | Fetch all repos below | D B | grid | s | – |
| `git.log.file` | History of this file | F | console | `git log --follow --oneline -n 50` | – |
| `git.blame.file` | Who changed this file (blame summary) | F | grid | `git blame --line-porcelain` → author, lines, % | – |

## F. Paste & New (C#, journaled)
| id | Title | When | Out | Risk | Notes | T |
|---|---|---|---|---|---|---|
| `new.from-clip-tree` | Folders from clipboard tree | B D | toast | m | Parse: (1) box-drawing ASCII trees (`├──`, `└──`, `│`, `+--`, `\--`), (2) indented lists (spaces/tabs/`-`/`*` bullets, auto-detect the indent unit), (3) a path list (`a/b/c.txt`, `a\b\`). Entries with an extension or without a trailing `/` at a leaf → empty files; trailing `/` or has children → folders. Show a preview tree dialog (FTreeList, check items to skip) → create → journal → one Undo. Reject `..`, absolute paths, invalid chars (show the problems in the preview). | [T] (`clip-tree.txt` → expected files) |
| `new.paste-image` | Paste image as PNG | B D | toast | m | *v3: paste-image* | [T] |
| `new.paste-text` | Paste text as file | B D | toast | m | *v3: paste-text*; v4 auto extension: JSON (parses) → .json, starts with `#`/has md links → .md, CSV (consistent commas across ≥ 3 lines) → .csv, `<?xml`/`<html` → .xml/.html, shebang/`def `/`import ` → .py, `function`/`const ` → .js, else .txt. Name: `clip_<now>.<ext>` | [T] |
| `new.download-clip` | Download link from clipboard | B D | toast | m | *v3: download-clip* | – |
| `new.paste-shortcut` | Paste as shortcut | B D | toast | m | from the clipboard file drop list → `.lnk` via WScript.Shell COM (late-bound `Type.GetTypeFromProgID`) | [T] |
| `new.paste-hardlink` | Paste as hard link | B D | toast | m | files only, same volume check | [T] |
| `new.paste-symlink` | Paste as symbolic link | B D | toast | m | needs Developer Mode or admin → clear message otherwise | – |
| `new.paste-junction` | Paste as junction | B D | toast | m | folders | [T] |
| `new.folder-from-clip` | Folder named from clipboard | B D | toast | m | *v3* | [T] |
| `new.dated-folder` | Dated folder | B D | toast | m | *v3* | [T] |
| `new.file` | New file… | B D | toast | m | input name (with ext), creates and selects it in Explorer (`SHOpenFolderAndSelectItems`) | [T] |
| `new.group-selected` | Move selection into new folder | F D multi | toast | m | *v3* | [T] |
| `new.unwrap` | Unwrap folder | D | toast | m | *v3* | [T] |
| `new.snapshot` | Dated snapshot copy | D F | toast | m | *v3* | [T] |

## G. Search
| id | Title | When | Out | Notes | T |
|---|---|---|---|---|---|
| `search.here` | Search here… | D B V | window | *v3 SearchForm*, restyled; uses `rg --json` when present (much faster), else the built-in | – |
| `search.text` | Find text in files… | D B | grid | input pattern + regex toggle → File, Line, Text; row action: open in VS Code at the line (`code -g file:line`) or Notepad | [T] |
| `search.large` | Largest files | D B V | grid | *v3* | [T] |
| `search.recent` | Recently changed files | D B | grid | *v3* | [T] |
| `search.duplicates` | Duplicate files | D B | grid | *v3* | [T] |
| `search.empty` | Empty folders | D B | grid | *v3* | [T] |
| `search.long-paths` | Long paths | D B | grid | *v3* | [T] |
| `search.by-date` | Files changed between… | D B | grid | inputs from/to (date) | – |

## H. Open
| id | Title | When | Notes |
|---|---|---|---|
| `open.terminal` | Terminal here | D B V F | `wt.exe -d "<dir>"` (requires wt), else PowerShell |
| `open.pwsh` | PowerShell 7 here | D B V | requires pwsh |
| `open.ps` | Windows PowerShell here | D B V | *v3: open-ps* |
| `open.cmd` | Command Prompt here | D B V | *v3* |
| `open.admin` | Admin terminal here | D B V | admin, extended |
| `open.wsl` | WSL here | D B | requires wsl |
| `open.gitbash` | Git Bash here | D B | find `git-bash.exe` via the git install path |
| `open.vscode` | Open in VS Code | D B F | requires code; *v3* |
| `open.run-console` | Run console here | D B V F | `PSTools.exe run "<dir>"` |
| `open.notepad` | Open in Notepad | F | *v3* |
| `open.with-default-editor` | Edit with… | F | `rundll32 shell32,OpenAs_RunDLL` |

## I. Inspect (C# where it says so)
| id | Title | When | Out | Notes | T |
|---|---|---|---|---|---|
| `inspect.locking` | Who is locking this? | F D | window | **C#** Restart Manager (`RmStartSession`, `RmRegisterResources`, `RmGetList`). For folders, check every file below (cap 5,000). List: App, PID, Type, and a button **Close app** (CloseMainWindow → wait 5 s → offer Kill, d). Empty → toast "Nothing is using this". | [T] (open a FileStream in the test process on a fixture file → expect our own pid) |
| `inspect.origin` | Where did this come from? | F | window | **C#** read the `:Zone.Identifier` ADS (`File.ReadAllText(path + ":Zone.Identifier")` doesn't work in .NET Framework; use `CreateFile` P/Invoke on `path:Zone.Identifier`), show ZoneId, HostUrl, ReferrerUrl; buttons Copy URL, Open URL, Unblock (m) | [T] (fixture writes an ADS via PowerShell `Set-Content -Stream`) |
| `inspect.facts` | File facts | F | window | encoding + BOM, line endings, lines, size, created/modified, hashes (lazy), Authenticode status (PowerShell `Get-AuthenticodeSignature`, async), image WxH, media duration (Shell property `System.Media.Duration` via `Shell.Application` COM) | [T] (text fixture) |
| `inspect.sizes` | Subfolder sizes | D B V | grid | *v3: folder-sizes* | [T] |
| `inspect.space-map` | Folder space map | D B V | window | *v3: space-map* | – |
| `inspect.types` | File types | D B | grid | *v3* | [T] |
| `inspect.port` | What is using port… | B | grid | input port; *v3 System* | – |
| `inspect.env-path` | Is this folder on PATH? | D | toast | | [T] |

## J. Compare (C#)
| id | Title | When | Out | Notes | T |
|---|---|---|---|---|---|
| `compare.mark` | Mark for compare | F D | toast | *v3 Links mark* (shared mark) | – |
| `compare.with-marked` | Compare with marked | F D | window | folders: v3 compare (only in A / only in B / different by size+hash); files: if `code` → `code --diff a b`, else a built-in side-by-side line diff (simple LCS, cap 5,000 lines) | [T] (folder compare golden) |
| `compare.two-selected` | Compare the two selected | F D multi(min 2,max 2) | window | same | [T] |
| `compare.hash-selected` | Are these identical? | F multi | toast | SHA-256 of all → "All 4 identical" / "2 groups" | [T] |

## K. Rename / Organize (mostly v3)
| id | Title | When | Notes | T |
|---|---|---|---|---|
| `rename.batch` | Batch rename… | F D multi, B | *v3 RenameForm* restyled + live preview | – |
| `rename.lower` / `.kebab` / `.snake` / `.title` | Rename: lower-case / kebab-case / snake_case / Title Case | F D multi | m, journaled | [T] |
| `rename.date-prefix` | Prefix with date taken/modified | F multi | m; images: EXIF DateTimeOriginal via `Image.GetPropertyItem(0x9003)` | [T] |
| `rename.number` | Number sequentially | F multi | m; `name_001.ext` in Explorer sort order | [T] |
| `rename.ext-lower` | Lower-case extensions | F multi, D B (recursive) | m | [T] |
| `organize.flatten` | Flatten | D | *v3* | [T] |
| `organize.by-type` | Sort into folders by type | D B | *v3* | [T] |
| `organize.by-date` | Sort into folders by month | D B | `2026-01\` based on modified/taken | [T] |

## L. Archive / Image / Media
| id | Title | When | Notes | T |
|---|---|---|---|---|
| `archive.zip-smart` | Zip (skip build folders) | D F multi | *v3: zip-smart* | [T] |
| `archive.zip-gitignore` | Zip (respect .gitignore) | D | git ls-files based | [T] |
| `archive.extract` | Extract here (smart) | F[.zip] | *v3: unzip-here* | [T] |
| `archive.extract-tar` | Extract (tar/7z/gz) | F[.tar,.gz,.tgz,.7z,.rar] | uses `tar.exe` (Windows 10+ bsdtar handles zip/tar/gz/7z/rar read) | [T] (.tar.gz fixture made with tar) |
| `archive.list` | List archive contents | F[archives] | grid | [T] |
| `image.png` / `.jpg` / `.resize-1600` / `.resize-800` / `.strip` | (v3) | F[img] multi | *v3* | [T] |
| `image.webp` | Convert to WebP | F[img] | requires magick or ffmpeg | – |
| `image.dimensions` | Copy dimensions | F[img] multi | clip `1200x800` | [T] |
| `media.to-mp3` | Extract audio (MP3) | F[video] | requires ffmpeg | – |
| `media.compress-video` | Compress video (H.264, CRF 28) | F[video] | requires ffmpeg, console | – |
| `media.gif` | Video → GIF (first 10 s) | F[video] | requires ffmpeg | – |

## M. Clipboard (B context + app; operate on clipboard text and write back; toast summary)
`clip.sort`, `clip.unique`, `clip.trim`, `clip.remove-blank`, `clip.json-pretty`, `clip.json-minify`, `clip.base64-encode`, `clip.base64-decode`, `clip.url-encode`, `clip.url-decode`, `clip.lower`, `clip.upper`, `clip.kebab`, `clip.snake`, `clip.camel`, `clip.count` (toast only), `clip.paths-fwd` (replace `\` with `/`), `clip.quote-lines` (`'a', 'b'`), `clip.to-ps-array` (`@('a','b')`), `clip.guid` (new GUID → clipboard), `clip.timestamp` (ISO now). **C#**, all **[T]** (pure functions: unit-test them in selftest with input → output tables).

## N. Dev
| id | Title | When | Notes |
|---|---|---|---|
| `dev.serve` | Serve folder on localhost | D B | *v3* |
| `dev.clean-build` | Find removable build folders | D B V | *v3* (grid, d) |
| `dev.npm-install` | npm install here | D B | requires node; console; m; only when `package.json` exists (match via the `when` + a "has file" check: add `match.has: ["package.json"]` to the schema, checked against the dir) |
| `dev.pip-venv` | Create Python venv here | D B | requires python; `match.has: ["requirements.txt"]` optional |
| `dev.port-kill` | Free port… | B | input port → show the process → confirm kill (d) |
| `dev.env-path-add` | Add this folder to user PATH | D | m; read `HKCU\Environment\Path` raw (`RegistryValueOptions.DoNotExpandEnvironmentNames`), append, broadcast `WM_SETTINGCHANGE`; journal-able (store the old value) → Undo. Track it in the footprint list as a Tweak (`changes.txt`) so a full uninstall offers to revert it. |
| `dev.env-path-remove` | Remove this folder from user PATH | D | m, same |

## O. System (keep the v3 set, re-home it) + new
`system.ip-local` (clip), `system.ip-public` (clip; `https://api.ipify.org`, 5 s timeout, offline → toast), `system.dns-flush`, `system.restart-explorer` (extended), `system.toggle-hidden`, `system.toggle-ext`, `system.classic-menu`, `system.summary`, `system.battery`, `system.update-all` (winget), `system.uptime` (toast), and the rest of the v3 System module unchanged.

## P. Fix / Links / Disk / Clean up
Keep every v3 action, re-homed under these folders with v4 ids `fix.*`, `links.*`, `disk.*`, `clean.*`. Add `fix.unblock-recursive` (Unblock-File on all below, m) and `clean.empty-folders-delete` (d, Recycle Bin).

---

## Z. Manual demo script (record as gallery screenshots in Phase 4 via a UIA scenario `demo-tour`)
1. Popup on `tree-basic` → `ta` → Enter → paste the clipboard into a Run-console echo → screenshot.
2. Popup on `tree-basic` → `for ai` → Enter → toast "Copied N files …" → screenshot of the toast.
3. Put `clip-tree.txt` content on the clipboard → popup background of an empty temp dir → `folders from` → the preview dialog screenshot → Create → `copy.tree.ascii` of the result equals the input tree (assert).
4. Open a FileStream on `names\a&b.txt` from the test process → popup → `locking` → the window lists powershell (pid) → screenshot.
5. Popup on `repo\src\app\main.py` → `permalink` → the clipboard equals `https://github.com/example/repo/blob/<sha>/src/app/main.py`.
6. Run console: `Get-ChildItem -Recurse -File | Group-Object Extension | Select Name, Count` → Show as table → screenshot → Save as action "Count by extension" in Inspect → popup → `count by` → the table appears.
