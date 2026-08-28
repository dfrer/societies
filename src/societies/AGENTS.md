# Product Runtime Agent Contract

These rules apply to `src/societies/` in addition to the root `AGENTS.md`.

## Role

This tree is the authoritative Godot player-facing product. It owns the lived world and the deterministic runtime used by the player. Laboratory provider, recording, routing, and billing machinery must not leak into this dependency graph or normal UI.

## State ownership

- `PrototypeRuntimeSession` and domain systems own canonical state.
- Scenes, nodes, HUDs, presenters, capture runners, and debug overlays consume projections and send intents.
- No scene field or UI state may become an unpersisted second source of inventory, structure, citizen, ecology, commitment, policy, or outcome truth.
- Every accepted mutation must have explicit validation, ordering, command result, event, persistence/replay behavior, and failure semantics.

## Product rules

- Build vertical product slices, not disconnected mechanics.
- Preserve the embodied resident-founder and citizen-autonomy charter.
- Normal gameplay must not expose provider identity, routing, raw model output, billing, credentials, or debug-only authority.
- Do not add a new panel when spatial interaction, animation, sound, or a concise contextual surface can communicate the result.
- A screenshot or headless test cannot establish feel, visual quality, citizen credibility, or human acceptance.
- Do not expand worldcraft breadth while the active product risk is citizen participation unless the milestone explicitly couples them.

## Integration with Snow Globe

Adopt only a small provider-neutral cognition interface. Product requests contain bounded citizen-known observations and closed proposal vocabularies. Product receipts separate communication from action proposals. Runtime validation remains authoritative. Recorded/deterministic adapters are the first production path; live adapters require explicit authorization.

The product project must not reference Snow Globe CLI assemblies, provider-specific request/response models, credentials, account state, payment journals, or storage roots.

## Refactors

Refactor a hotspot only when the active task names the risk and the seam can be protected by characterization and integration tests. Keep behavior changes and broad structural cleanup separate. New abstractions require demonstrated repeated use or an explicit milestone need.

## Required checks

Use the task packet and test manifest. At minimum for runtime changes:

```powershell
python scripts/check-project-governance.py
dotnet build src/societies/Societies.csproj --configuration Release
dotnet test tests/Societies.Core.Tests/Societies.Core.Tests.csproj --configuration Release
godot --headless --path src/societies --build-solutions --quit
godot --headless --path src/societies res://tests/HeadlessTestRunner.tscn
```

Run persistence, replay, migration, performance, visual, accessibility, and human gates when the task changes those claims. Report exact failures and do not infer acceptance.
