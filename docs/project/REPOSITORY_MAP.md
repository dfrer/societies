# Repository Map and Placement Rules

## Product source

### `src/societies/`

The authoritative Godot 4 + C# product. This is the only player-facing executable authority.

- `scenes/` — composed Godot scenes.
- `scripts/core/` — session, commands, persistence, catalogs, and application orchestration.
- `scripts/simulation/` — settlement and citizen simulation.
- `scripts/world/` — terrain, voxel, and world systems.
- `scripts/presentation/` — view construction and presentation adapters.
- `scripts/ui/` — HUD and UI.
- `data/` — versioned authored catalogs and scenarios.
- `tests/` — Godot-hosted test runners and diagnostics.

Use the scoped `src/societies/AGENTS.md` for product changes.

## Snow Globe laboratory

### `labs/`

Provider-neutral experiments and operator CLIs. The core lab is `labs/Societies.SnowGlobe/`; benchmark, recording, and OpenRouter CLIs are sibling projects. The lab may prove mechanisms but cannot become product authority or activate provider work.

Use `labs/AGENTS.md` and the concise lab indexes before detailed contract files.

## Tests

### `tests/`

Managed product tests, extended characterization, Snow Globe core tests, and CLI tests. `tests/test-manifest.json` declares product tiers and must match actual discovery before test-count claims are made. Use `tests/AGENTS.md`.

## Planning

### `planning/active/`

Contains only:

- `README.md`;
- `MILESTONE.md`, the one authorized plan;
- `evidence/`, retained temporarily for compatibility.

### `planning/archives/`

Completed, stopped, superseded, or pre-consolidation plans. Archive documents are immutable historical evidence unless a correction note is added. They do not authorize work.

### `planning/concept/`, `sessions/`, `research/`, `meta/`, `spreadsheets/`

Source material and long-range thinking. The active milestone must cite a document before it constrains implementation. Research findings are not adopted architecture until a decision or ADR says so.

## Canonical documentation

### `docs/project/`

Small maintained documents for charter, current state, architecture, process, roadmap, decisions, risks, and repository layout. Do not store raw logs or per-PR completion narratives here.

### `docs/adr/`

Technical architecture decisions. One concern per ADR. Status must be explicit: proposed, accepted, superseded, or rejected.

### `docs/history/`

Development history and preserved former authority documents. The dated pre-consolidation snapshot allows forensic reading without polluting current navigation.

## Evidence and artifacts

- `artifacts/` — bounded tracked evidence with provenance.
- generated performance/runtime output — follow `.gitignore`, scripts, and evidence contracts.
- secrets, credentials, raw account data, unrestricted provider responses, and personal host paths must never be committed.

## Automation

- `.github/workflows/` — required CI.
- `.github/pull_request_template.md` — delivery contract.
- `scripts/check-project-governance.py` — authority and layout guard.
- `scripts/` — launch, validation, capture, diagnostic, and performance tools.

## Placement decision rule

Ask what authority and lifecycle a new file has:

- current product truth -> `docs/project/`;
- one active implementation outcome -> `planning/active/MILESTONE.md` or a cited bounded task packet;
- accepted technical decision -> `docs/adr/`;
- research input -> `planning/research/`;
- mechanical evidence -> governed evidence/artifact location;
- completed plan or report -> archive;
- session transcript or agent narrative -> do not make it canonical; preserve only when it contains unique evidence.

A file should not remain at repository root merely because an earlier prompt expected it. Root compatibility files must stay concise and point to canonical material.
