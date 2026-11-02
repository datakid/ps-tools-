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
