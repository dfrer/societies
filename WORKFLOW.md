# Workflow

The canonical process is [`docs/project/DEVELOPMENT_PROCESS.md`](docs/project/DEVELOPMENT_PROCESS.md). The root file remains only because older prompts and scripts expect it.

Before work:

1. read `project-governance.json`;
2. read `docs/project/CURRENT_STATE.md`;
3. read `planning/active/MILESTONE.md`;
4. read the nearest scoped `AGENTS.md`;
5. inspect the actual branch, source, tests, and evidence.

Every task must be bounded by an observable outcome, non-goals, acceptance criteria, validation, human gate, and stop conditions. Execution agents implement the active plan; they do not silently choose or expand the roadmap. A passing automated suite cannot override a failed or missing human product gate.

Run `python scripts/check-project-governance.py` before handoff. Historical workflow records were preserved under `docs/history/pre-consolidation-2026-08-27/`.
