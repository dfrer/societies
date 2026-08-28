# Societies Snow Globe Core Laboratory

## Purpose

This .NET 8 project is the isolated laboratory for the smallest persistent-agent and cognition mechanisms required by Societies. It proves contracts without giving models, providers, storage adapters, or operator tools authority over the Godot world.

The pre-consolidation chronological README—containing detailed provider generations, hashes, ledgers, and historical status—is preserved at `docs/history/pre-consolidation-2026-08-27/labs/Societies.SnowGlobe-README.md`. Current orientation belongs here; detailed bounded contracts remain in their named Markdown files and ADRs.

## Capability map

### Agent and world loop

- persistent agent identities;
- immutable bounded observations;
- closed proposals and deterministic validation;
- sequential and controlled-parallel scheduling;
- ordered commits, deterministic fallback, replay, and metrics;
- gathering, storage, shelter, and maintenance test vocabulary.

### Persistence and recovery

- checkpoint/resume and recorded responses;
- persisted session inspection;
- run-store crash and recovery experiments;
- stable reads, generation identities, append-only evidence, and durable pause/control contracts.

### Cognition quality

- frozen scenario corpus and prompt envelope;
- normalized proposal codecs;
- deterministic scoring and provider-neutral comparison;
- recorded Ollama and OpenRouter proposal evidence.

### Provider and operator boundary

- offline preflight and readiness observations;
- provider-neutral routing policy and orchestration experiments;
- durable attempt-state and ambiguity handling;
- separate recording, OpenRouter, and benchmark CLIs;
- strict bounded evidence and security tests.

## What this project does not prove

- a citizen experienced inside the accepted Godot world;
- a complete social simulation;
- general model quality or consciousness;
- player-facing dialogue, autonomy, or fun;
- continuous provider readiness;
- release, deployment, commercial, or cost effectiveness;
- permission for another provider call.

## Product integration rule

The first adopted seam must be a small versioned experience-cognition interface with bounded citizen-known observations, a closed proposal vocabulary, a separate communication act, stale/cancellation binding, deterministic validation, and exact recorded replay. Deterministic and recorded adapters must work before live execution is relevant. Product code cannot depend on provider-specific or CLI assemblies.

## Tests

```powershell
dotnet test tests/Societies.SnowGlobe.Tests/Societies.SnowGlobe.Tests.csproj --configuration Release
dotnet test tests/Societies.SnowGlobe.BenchmarkCli.Tests/Societies.SnowGlobe.BenchmarkCli.Tests.csproj --configuration Release
dotnet test tests/Societies.SnowGlobe.RecordingCli.Tests/Societies.SnowGlobe.RecordingCli.Tests.csproj --configuration Release
dotnet test tests/Societies.SnowGlobe.OpenRouterCli.Tests/Societies.SnowGlobe.OpenRouterCli.Tests.csproj --configuration Release
```

Read `../AGENTS.md`, the relevant contract document, and the active milestone before work. Do not continue from the last historical “next action.”
