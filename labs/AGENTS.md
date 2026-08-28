# Snow Globe Laboratory Agent Contract

These rules apply to all projects under `labs/` in addition to the root `AGENTS.md`.

## Role

The laboratory proves bounded mechanisms for persistent citizens, scheduling, cognition, recorded responses, persistence, provider adapters, recovery, and evidence. It is not world authority, not the player-facing product, and not an independently self-expanding platform roadmap.

## Scope discipline

- Laboratory work requires an active milestone or bounded task packet that names the product or architecture risk being reduced.
- Do not continue provider, routing, ledger, recovery, codec, or evidence depth because the previous task exposed another adjacent edge case.
- No network call, credential access, account inspection, paid request, provider recording, retry, fallback, or live readiness action occurs without explicit current authorization naming limits and delivery boundaries.
- Historical standing authority in an archived document is not current authorization.
- Keep provider-specific code behind provider-neutral contracts.
- Never grant lab output world-state authority or describe fixed-corpus/provider evidence as general intelligence, gameplay quality, or release readiness.

## Product adoption

A product milestone may adopt only the smallest interface needed for an accepted citizen interaction. The Godot project must remain playable with deterministic and recorded adapters. Live provider execution is optional and separately gated. Product code may not depend on laboratory CLIs, raw response objects, billing journals, or operator storage roots.

## Evidence honesty

Separate implementation, offline contract tests, recorded-response evidence, live provider evidence, cost/latency characterization, security review, and product value. A large passing test suite proves only its declared contracts.

## Refactors

The core lab is flat and contains large files. Do not perform cosmetic mass movement. Decompose a subsystem when a touched product-facing seam, repeated conflict, testability problem, or authority leak justifies it. Preserve public contracts and fixtures with targeted tests.

## Typical checks

```powershell
dotnet test tests/Societies.SnowGlobe.Tests/Societies.SnowGlobe.Tests.csproj --configuration Release
dotnet test tests/Societies.SnowGlobe.BenchmarkCli.Tests/Societies.SnowGlobe.BenchmarkCli.Tests.csproj --configuration Release
dotnet test tests/Societies.SnowGlobe.RecordingCli.Tests/Societies.SnowGlobe.RecordingCli.Tests.csproj --configuration Release
dotnet test tests/Societies.SnowGlobe.OpenRouterCli.Tests/Societies.SnowGlobe.OpenRouterCli.Tests.csproj --configuration Release
```

Run only authorized CLIs and state whether execution was offline, recorded, loopback, or live.
