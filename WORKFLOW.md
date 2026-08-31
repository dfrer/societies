# Workflow

The canonical process is [`docs/project/DEVELOPMENT_PROCESS.md`](docs/project/DEVELOPMENT_PROCESS.md). The root file remains only because older prompts and scripts expect it.

## Latest delivery boundary

Packet 01's canonical empty-scene baseline remains identified by implementation `936771285fd1fd2ebb92054dc3630a656a78b941` and tree `2bd4a709a54ae72fde84fc3e24968e5fec49e49b`. Its six-execution ExportRelease bundle is deterministic and mechanically validated: three realtime trials supply timing evidence, while three fixed-60 executions supply identity evidence only. Realtime physics p95 remains a characterized **16.67 ms target miss** (worst **23.109 ms**) while both realtime p95 metrics pass the **33.33 ms hard-safety line**.

Delivery is complete through [PR #192](https://github.com/dfrer/societies/pull/192): exact head `ea538a506318f99946f858c7c4b50508b65fb44c` merged at `2026-08-31T04:52:40Z` as `f471efe5fe5f5b62aef37e48f27bc1fbffd0104e`, whose parents are exact base `31ea1d6012d6fd932d0bfe0dbc621e668fd58c80` and that PR head. Required `build-test-smoke` was **SUCCESS** (run `33358362870`, job `99384744879`, `3m53s`) and required `lab-tests` was **SUCCESS** (run `33358362846`, job `99385290379`, `4s`); the classifier and full relevant lab suite also succeeded. Branch protection required exactly those two strict/up-to-date contexts from GitHub Actions app id `15368`.

Hosted product evidence is 0-warning/0-error production Release, **399/399** fast managed, **28/28** Godot, and governance pass. Hosted relevant-lab evidence is 0-warning/0-error core build, Snow Globe core **1,186 passed, 5 documented skips, 0 failed**, benchmark **56/56**, recording **94/94**, OpenRouter **104/104**, and governance pass. The local full managed **519/519** result remains local evidence and is not presented as hosted.

This reconciliation changes only `CURRENT_BUILD.md`, `docs/project/CURRENT_STATE.md`, `WORKFLOW.md`, and `planning/active/evidence/snow-globe-social-kernel-packet-01-validation.json`; no gameplay suite was rerun for the documentation/evidence-only boundary, and the ignored raw performance bundle is unchanged. The accepted scene still has zero citizens. Packet 01 required no human product gate, and none is claimed; no deployment, accessibility, provider, paid, or release gate is claimed. Packet 02 is the next ordered packet in principle but is **NOT STARTED** and requires its own bounded implementation branch from then-current `master`.

Before work:

1. read `project-governance.json`;
2. read `docs/project/CURRENT_STATE.md`;
3. read `planning/active/MILESTONE.md`;
4. read the nearest scoped `AGENTS.md`;
5. inspect the actual branch, source, tests, and evidence.

Every task must be bounded by an observable outcome, non-goals, acceptance criteria, validation, human gate, and stop conditions. Execution agents implement the active plan; they do not silently choose or expand the roadmap. A passing automated suite cannot override a failed or missing human product gate.

Run `python scripts/check-project-governance.py` before handoff. Historical workflow records were preserved under `docs/history/pre-consolidation-2026-08-27/`.
