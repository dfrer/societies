# Workflow

The canonical process is [`docs/project/DEVELOPMENT_PROCESS.md`](docs/project/DEVELOPMENT_PROCESS.md). The root file remains only because older prompts and scripts expect it.

## Latest delivery boundary

Packet 01's canonical empty-scene baseline remains identified by implementation `936771285fd1fd2ebb92054dc3630a656a78b941` and tree `2bd4a709a54ae72fde84fc3e24968e5fec49e49b`. Its six-execution ExportRelease bundle is deterministic and mechanically validated: three realtime trials supply timing evidence, while three fixed-60 executions supply identity evidence only. Realtime physics p95 remains a characterized **16.67 ms target miss** (worst **23.109 ms**) while both realtime p95 metrics pass the **33.33 ms hard-safety line**.

Delivery is complete through [PR #192](https://github.com/dfrer/societies/pull/192): exact head `ea538a506318f99946f858c7c4b50508b65fb44c` merged at `2026-08-31T04:52:40Z` as `f471efe5fe5f5b62aef37e48f27bc1fbffd0104e`, whose parents are exact base `31ea1d6012d6fd932d0bfe0dbc621e668fd58c80` and that PR head. Required `build-test-smoke` was **SUCCESS** (run `33358362870`, job `99384744879`, `3m53s`) and required `lab-tests` was **SUCCESS** (run `33358362846`, job `99385290379`, `4s`); the classifier and full relevant lab suite also succeeded. Branch protection required exactly those two strict/up-to-date contexts from GitHub Actions app id `15368`.

Hosted product evidence is 0-warning/0-error production Release, **399/399** fast managed, **28/28** Godot, and governance pass. Hosted relevant-lab evidence is 0-warning/0-error core build, Snow Globe core **1,186 passed, 5 documented skips, 0 failed**, benchmark **56/56**, recording **94/94**, OpenRouter **104/104**, and governance pass. The local full managed **519/519** result remains local evidence and is not presented as hosted.

The visual-recovery amendment was delivered through [PR #194](https://github.com/dfrer/societies/pull/194): planning head `52557d0a517b841421aa60a7b7b295375908c35f` merged as `ccb651c67689c25eb0f3adec78abbfcf870699f5` at `2026-09-02T03:28:09Z`. Required hosted CI validation ran and succeeded: `build-test-smoke` in run `33531175853`, job `99934428113`; Snow Globe lab detector, suite, and required `lab-tests` in jobs `99934427058`, `99934496859`, and `99935427335`. No local runtime/build/test suite was rerun for this documentation-only reconciliation. D-016 is accepted/delivered, so Packet 02A is next authorized implementation; Packet 02V still requires the interactive owner visual gate, and Packet 03 remains unstarted/unauthorized.

The visual-production recovery amendment changes only planning/prose authority in `planning/active/MILESTONE.md`, `docs/project/DECISION_LOG.md`, `docs/project/CURRENT_STATE.md`, `docs/project/RISKS_AND_DEBT.md`, `docs/project/ROADMAP.md`, `CURRENT_BUILD.md`, and this file. No local runtime/build/test suite was rerun for this documentation-only boundary. The first Packet 02 visual gate remains failed; its unpublished branch is historical evidence and is not rewritten. Recovery is Packet 02A substrate -> Packet 02V visual-production target -> Packet 03, with Packet 03 **NOT STARTED** and unauthorized until the 02V owner gate passes. No deployment, accessibility, provider, paid, or release gate is claimed.

Before work:

1. read `project-governance.json`;
2. read `docs/project/CURRENT_STATE.md`;
3. read `planning/active/MILESTONE.md`;
4. read the nearest scoped `AGENTS.md`;
5. inspect the actual branch, source, tests, and evidence.

Every task must be bounded by an observable outcome, non-goals, acceptance criteria, validation, human gate, and stop conditions. Execution agents implement the active plan; they do not silently choose or expand the roadmap. A passing automated suite cannot override a failed or missing human product gate.

Run `python scripts/check-project-governance.py` before handoff. Historical workflow records were preserved under `docs/history/pre-consolidation-2026-08-27/`.
