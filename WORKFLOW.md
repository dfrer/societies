# Workflow

The canonical process is [`docs/project/DEVELOPMENT_PROCESS.md`](docs/project/DEVELOPMENT_PROCESS.md). The root file remains only because older prompts and scripts expect it.

## Latest delivery boundary

Packet 01's local canonical empty-scene baseline is recorded at implementation checkpoint `936771285fd1fd2ebb92054dc3630a656a78b941` (tree `2bd4a709a54ae72fde84fc3e24968e5fec49e49b`). Its six-execution ExportRelease bundle is deterministic and mechanically validated: three realtime trials supply timing evidence, while three fixed-60 executions supply identity evidence only. Realtime physics p95 remains a characterized **16.67 ms target miss** (worst **23.109 ms**) while both realtime p95 metrics pass the **33.33 ms hard-safety line**. The accepted scene still has zero citizens, and no human, hosted, PR, merge, deployment, accessibility, or release gate is claimed.

Changed handoff files: `CURRENT_BUILD.md`, `docs/project/CURRENT_STATE.md`, `WORKFLOW.md`, and `planning/active/evidence/snow-globe-social-kernel-packet-01-validation.json`. The ignored raw performance bundle is unchanged. Next action is bounded remote/hosted Packet 01 checks only after explicit authorization; do not begin Packet 02 before Packet 01 merge and reconciliation.

Before work:

1. read `project-governance.json`;
2. read `docs/project/CURRENT_STATE.md`;
3. read `planning/active/MILESTONE.md`;
4. read the nearest scoped `AGENTS.md`;
5. inspect the actual branch, source, tests, and evidence.

Every task must be bounded by an observable outcome, non-goals, acceptance criteria, validation, human gate, and stop conditions. Execution agents implement the active plan; they do not silently choose or expand the roadmap. A passing automated suite cannot override a failed or missing human product gate.

Run `python scripts/check-project-governance.py` before handoff. Historical workflow records were preserved under `docs/history/pre-consolidation-2026-08-27/`.
