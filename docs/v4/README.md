# PS Tools v4: Rebuild Plan Package

Planning documents for rebuilding [datakid/ps-tools-](https://github.com/datakid/ps-tools-) (C# 5 / WinForms Explorer power tool) into v4. They are written to be handed to Claude Sonnet, which implements them with a GitHub token.

| File | What it is |
|---|---|
| `SONNET_KICKOFF.md` | The prompt to paste into Sonnet. It points to everything else. |
| `V4_PLAN.md` | Master plan: v3 analysis, architecture, action schema, menu tree, Power Menu popup, Run console, Save as action, migration, CI workflows, test strategy, 8 phases with acceptance criteria, definition of done |
| `docs/v4/UI_SPEC_AND_TESTING.md` | Design tokens, component and screen specs, a deterministic screenshot harness, visual diff, pixel lint, UI Automation scenarios, a PR screenshot gallery, and the 12-point UI review rubric |
| `docs/v4/POWER_ACTIONS.md` | ~150 actions with ids, contexts, outputs, risk, and test requirements (tree/list copy, copy for AI, folders from a clipboard tree, who-is-locking, permalinks, clipboard transforms…) |
| `docs/v4/CHEATSHEET_SPEC.md` | `cheatsheet.json` schema, content requirements (≥300 entries plus bash/cmd → PowerShell tables), and CI parse/run validation |

Usage: copy `V4_PLAN.md`, `SONNET_KICKOFF.md` and `docs/v4/` into the repo root of `ps-tools-` (or let Sonnet commit them in Phase 0), then paste `SONNET_KICKOFF.md` into Sonnet.

Not a website. There's nothing to deploy here. These are Markdown planning files only.
