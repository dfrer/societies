# Planning

Planning is the owner's primary interface to Societies development. It converts product judgment into bounded technical work; it is not an append-only record of everything an agent has ever done.

## Authority

- [`active/MILESTONE.md`](active/MILESTONE.md) is the **only executable plan**.
- [`active/README.md`](active/README.md) explains the active namespace.
- [`../docs/project/CHARTER.md`](../docs/project/CHARTER.md) holds the product invariants.
- [`../docs/project/CURRENT_STATE.md`](../docs/project/CURRENT_STATE.md) holds implementation truth.
- [`../docs/project/ROADMAP.md`](../docs/project/ROADMAP.md) holds candidate sequencing, not automatic authorization.
- `archives/`, `sessions/`, `research/`, `concept/`, `meta/`, and `spreadsheets/` are reference or history unless the active milestone cites them.

## Lifecycle

1. Discuss the product problem and decision with the owner.
2. Record an accepted decision or clearly marked open question.
3. Activate exactly one milestone with outcome, scope, evidence, human gate, and stop rules.
4. Deliver bounded tasks through isolated branches and reviewed PRs.
5. Record the result, then archive the milestone before activating another.

Historical plans formerly mixed into `planning/active/` were preserved intact under `planning/archives/2026-08-27-pre-consolidation/`. The evidence directory remains at `planning/active/evidence/` temporarily for path compatibility with manifests and historical tooling; it is evidence, not an active roadmap.
