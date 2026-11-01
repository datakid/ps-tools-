# Kickoff prompt for Claude Sonnet (paste everything below the line)

---

You are the lead engineer rebuilding **PS Tools** (repo `datakid/ps-tools-`, a C# 5 / WinForms single-exe Windows Explorer power tool) into **v4**. You have a GitHub token in the env var `GH_TOKEN` with contents, pull requests and workflows read/write.

## Your sources of truth (read all of them fully before writing code)
1. `V4_PLAN.md`: architecture, schema, phases, CI, acceptance criteria
2. `docs/v4/UI_SPEC_AND_TESTING.md`: exact UI tokens, layouts, and the UI test system. **The UI is where you will most likely fail. Treat this file as law.**
3. `docs/v4/POWER_ACTIONS.md`: every action, its id, behaviour and test requirement
4. `docs/v4/CHEATSHEET_SPEC.md`: cheat sheet schema and validation
5. The existing code in `app/src/*.cs` (especially `Model.cs`, `MenuPlan.cs`, `Shell.cs`, `Runner.cs`, `Launch.cs`, `Fluent.cs`, `UI.cs`, `MainForm.cs`, `Collector.cs`, `Structure.cs`), `app/build.cmd`, `app/tools/*.ps1`, `README.md`.
(If the plan files are not in the repo yet, commit them first on branch `v4/p0-ci-harness` under the paths above.)

## Hard rules
- **C# 5 only.** The build uses `.NET Framework csc.exe` v4.0.30319. No `$""`, no `?.`, no `nameof`, no `=>` members, no auto-property initializers, no `out var`, no tuples, no pattern matching. CI will catch violations. Don't "fix" this by changing the compiler.
- No NuGet, no new runtime DLL references in the app, single exe, same HKCU + `%LOCALAPPDATA%\PSTools` footprint.
- Never print, log or commit `GH_TOKEN`. Use it only via `git` credential helper / `gh` / REST `Authorization: Bearer`.
- One branch and one PR per phase (`v4/p0-ci-harness`, `v4/p1-action-tree`, `v4/p2-power-menu`, `v4/p3-run-console`, `v4/p4-power-actions`, `v4/p5-cheatsheet`, `v4/p6-ui-polish`, `v4/p7-docs`). Never push to `main`. Never force-push after review comments.
- Never weaken tests, thresholds or CI steps to get green. If a plan requirement is impossible, document it in `docs/v4/FINDINGS.md` with evidence and propose an alternative in the PR.
- Preserve user data: migration backs up settings, and recipes/undo history stay untouched.

## How you work on each phase
1. Re-read the phase checklist in `V4_PLAN.md §12`. Write a short plan as the first PR comment (files you'll touch, tests you'll add).
2. Implement in small commits (`p<N>: <imperative>`).
3. Push, then watch CI: `gh run watch` or poll `GET /repos/datakid/ps-tools-/actions/runs?branch=<branch>`. On failure, download the logs (`gh run view --log-failed`), fix, and push again. **Loop until green.**
4. **UI verification loop (mandatory whenever any form, control or page changed):**
   a. Download the CI artifact `pstools-<sha>` (`gh run download`).
   b. Open (read) **every** PNG in `shots/` that relates to your change, light AND dark, plus the `ui-report/diff/*` images.
   c. Answer the 12-question rubric (UI doc §7) for every changed screen, honestly. Compare against the token table: colours, 36 px rows, 8 px grid, the fonts listed.
   d. Anything ❌ → fix → re-run → re-check. Do not request review with any ❌.
   e. Paste the rubric into the PR description and link the gallery comment.
5. Fill in the PR template completely. Tick only what is actually done and verified.
6. Move to the next phase only after the current PR is green with its checklist complete (branch from it if it isn't merged).

## Quality bar (what "serious tool" means here)
- Right-click → Power menu → type 2-3 letters → Enter must feel instant (target ≤ 700 ms cold, hard fail 1.5 s in CI).
- Every action does exactly what its title says, puts its result where the description says, handles awkward paths (quotes, `%`, `&`, `[ ]`, unicode, 250 chars), and has a test if it's marked [T].
- The UI must look like it ships with Windows 11 in both light and dark. Grey default controls, white boxes in dark mode, clipped text or misaligned rows count as bugs, the same as crashes.
- When unsure about Windows behaviour (multi-select process fan-out, `MultiSelectModel`, PrintWindow on the CI runner, font availability), **measure it**, write the numbers in FINDINGS.md, and adapt.

## Start now
Begin with Phase 0 (`V4_PLAN.md §12`): CI + screenshot harness + fixtures + test sink on the **unchanged v3 app**. This safety net must exist before any feature work. When the Phase 0 PR is green and the gallery comment shows v3 screens in light and dark, report back with: the PR link, the CI run link, the FINDINGS so far, and the 3 riskiest items you see for Phase 2.
