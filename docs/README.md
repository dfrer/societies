# Societies Documentation

The documentation tree is organized by **authority and lifecycle**, not by which agent created a file.

## Canonical project documents

- [`project/CHARTER.md`](project/CHARTER.md) — product identity and protected invariants.
- [`project/CURRENT_STATE.md`](project/CURRENT_STATE.md) — implementation and evidence truth.
- [`project/ARCHITECTURE.md`](project/ARCHITECTURE.md) — module, authority, and dependency boundaries.
- [`project/DEVELOPMENT_PROCESS.md`](project/DEVELOPMENT_PROCESS.md) — planning-to-delivery operating system.
- [`project/ROADMAP.md`](project/ROADMAP.md) — candidate product progression and activation rules.
- [`project/DECISION_LOG.md`](project/DECISION_LOG.md) — accepted project-level decisions.
- [`project/RISKS_AND_DEBT.md`](project/RISKS_AND_DEBT.md) — unresolved product, technical, and process reality.
- [`project/REPOSITORY_MAP.md`](project/REPOSITORY_MAP.md) — where material belongs.

## Other documentation

- `adr/` — accepted architecture decisions with specific technical scope.
- `history/` — development history and preserved former authority documents.
- `research/` — research indexes and adoption rules.
- `planning/` at repository root — owner-facing planning, the single active milestone, historical plans, concept work, and source research.
- Markdown inside `labs/` — bounded laboratory contracts and execution evidence; these are not product roadmaps.

## Authority rule

Documentation never outranks verified behavior. When prose conflicts, use the authority order in the root `AGENTS.md` and `project-governance.json`. A document under `history/`, `planning/archives/`, or an old branch remains evidence of past reasoning and work, but cannot activate implementation.

## Maintenance rule

Canonical documents summarize current truth and should remain concise. Do not append session logs, complete PR narratives, raw test output, or provider-generation chronicles to them. Put bounded evidence in its evidence location, decisions in the decision log or an ADR, and completed plans in the archive.
