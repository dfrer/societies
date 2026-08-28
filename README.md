# Societies

Societies is a Godot 4 + C# civilization and ecology simulation project. **Snow Globe is the first bounded realization of the full idea**, not a disconnected side project: it combines a deterministic world, embodied human participation, persistent citizens, constrained cognition, and shared consequences at a scale that can actually be understood and tested.

## Current position

The accepted product baseline is the EB-01R founder-worldcraft slice recorded at commit `847c86b1c379e6a1dd8d4b7b641c3c89646e28c9` and preserved on `archive/pre-consolidation-2026-08-27`. The user accepted its world readability, HUD hierarchy, inventory usability, construction clarity, and interaction feel on 2026-08-27. The recorded validation reports 507 managed tests, 28 Godot tests, and warning-free Release and ExportRelease builds.

That is meaningful progress, but it is not yet a miniature society. The accepted scene still has no participating citizens, no gameplay-facing Snow Globe cognition interface, and unresolved performance and collision-shape risk. Feature development is paused while the repository is consolidated and the next product proof is selected explicitly.

## Repository map

- `src/societies/` — authoritative Godot product runtime and player-facing experience.
- `labs/` — isolated Snow Globe cognition, persistence, provider, recording, and benchmarking experiments.
- `tests/` — product, lab, CLI, integration, soak, and diagnostic tests.
- `docs/project/` — canonical project charter, current state, architecture, process, roadmap, decisions, and risks.
- `planning/active/` — exactly one active milestone plus compatibility evidence.
- `planning/archives/` and `docs/history/` — preserved historical plans, reports, and prior authority documents.
- `artifacts/` — checked-in bounded evidence; generated runtime/performance output remains governed separately.
- `scripts/` — validation, diagnostics, capture, performance, and launch tooling.

## Read in this order

1. [`docs/project/CHARTER.md`](docs/project/CHARTER.md) — what Societies is and what may never drift.
2. [`docs/project/CURRENT_STATE.md`](docs/project/CURRENT_STATE.md) — what is actually proven now.
3. [`planning/active/MILESTONE.md`](planning/active/MILESTONE.md) — the only currently authorized work.
4. [`docs/project/ARCHITECTURE.md`](docs/project/ARCHITECTURE.md) — authority and module boundaries.
5. [`docs/project/DEVELOPMENT_PROCESS.md`](docs/project/DEVELOPMENT_PROCESS.md) — how planning becomes bounded implementation.
6. [`docs/project/RISKS_AND_DEBT.md`](docs/project/RISKS_AND_DEBT.md) — unresolved reality, not hidden optimism.

## Core commands

```powershell
# Product build and managed tests
dotnet build src/societies/Societies.csproj --configuration Release
dotnet test tests/Societies.Core.Tests/Societies.Core.Tests.csproj --configuration Release

# Godot headless validation
godot --headless --path src/societies res://tests/HeadlessTestRunner.tscn

# Repository governance
python scripts/check-project-governance.py

# Full authoritative local wrapper
./scripts/run-prototype-validation.ps1

# Accepted bounded founder-worldcraft route
./scripts/play-snow-globe-eco-baseline.ps1
```

Verified source, tests, runtime evidence, and human product acceptance outrank prose. Historical documents remain evidence of how the project developed; they do not silently reactivate old roadmaps.
