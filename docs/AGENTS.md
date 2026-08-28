# Documentation Agent Contract

These rules apply under `docs/` in addition to the root `AGENTS.md`.

## Canonical documents

Files under `docs/project/` describe current truth and remain concise. Rewrite them when facts change; do not append complete session histories, PR bodies, raw logs, or provider chronicles.

- Charter changes require an explicit owner decision.
- Current-state claims require source or evidence.
- Roadmap entries are candidates until activated in `planning/active/MILESTONE.md`.
- Risks close only with named evidence.
- Project decisions are append-only records with explicit status.

## ADRs

Use ADRs for technical decisions with lasting architectural consequences. State context, decision, alternatives, consequences, status, and supersession. Do not create an ADR for every implementation detail or use an ADR to activate roadmap work.

## History

Historical files remain unchanged except for clearly labeled correction notes. They may contain stale claims by design. Keep them out of current navigation except through history indexes.

## Links and evidence

Prefer relative repository links. Link to bounded evidence rather than copying it into canonical documents. Do not commit secrets, raw account/provider data, personal host paths, or unrestricted model responses.
