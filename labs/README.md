# Snow Globe Laboratories

The projects in this directory develop and test mechanisms needed by persistent model-assisted citizens. They are part of the Societies repository but remain isolated from the player-facing Godot dependency graph until a product milestone adopts a reviewed interface.

## Projects

- `Societies.SnowGlobe/` — core domain, agents, observations, scheduling, inference contracts, fallback, persistence, replay, cognition quality, provider-neutral execution, run stores, routing, and recovery experiments.
- `Societies.SnowGlobe.BenchmarkCli/` — bounded benchmark and comparison operator entry points.
- `Societies.SnowGlobe.RecordingCli/` — governed response-recording workflows.
- `Societies.SnowGlobe.OpenRouterCli/` — governed OpenRouter execution and evidence workflows.

Dedicated test projects live under `tests/`.

## Current relationship to the product

The laboratory proves substantial deterministic and operational machinery. The accepted Godot world does not yet use a gameplay-facing Snow Globe citizen interface. That integration is a future bounded product milestone, not an automatic next step.

Provider, credential, network, paid, and live-readiness operations require explicit current authorization. Archived standing authority and successful historical runs do not grant new execution.

Read `labs/AGENTS.md`, the root project charter/current state, and the active milestone before changing laboratory code.
