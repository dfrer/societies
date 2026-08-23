# Snow Globe v5 durable session-control status

## Outcome

An offline caller can determine whether a valid v5 persisted session will reopen `Running` or `Paused` without treating the deliberately inert `SnowGlobePersistedRunInspector.Inspect(...).Snapshot.IsPaused` flag as historical pause evidence.

## Scope

`SnowGlobePersistedRunInspector.InspectDurableControlStatus(directory, expectedIdentity)` is an additive read-only API. It repeats the existing exact-identity, two-read evidence-stability gate and, for v5 only, returns a detached raw-free `snow_globe_persisted_session_control_status_receipt/v1` receipt. The receipt binds the state to the canonical run-identity checksum, stable evidence checksum, and committed tick/event-count/state-digest/event-digest tuple.

The state is reconstructed from the existing validated ledger, including the existing rule that a valid uncommitted v5 pause frame is abandoned at the prior durable state. V2, v3, and v4 remain accepted inputs with a null receipt: this API never manufactures a `Running` result for them.

## Non-goals

- No RunStore schema, frame, writer, mutable-session, recovery-provenance, or v4 ephemeral-pause change.
- No change to `Inspect` or the inert meaning of `Snapshot.IsPaused`.
- No path/ABA hardening, writer lease, repair, continuation, artifact mutation, adapter invocation, provider, network, credential, paid, live-state, or `src/societies/` behavior.

## Acceptance and validation

Focused tests cover v5 empty/running, committed pause, resume, v2/v3/v4 suppression, exact identity mismatch, between-read drift, valid uncommitted pause evidence at the prior durable state, partial pause evidence failing closed, ordinary inert inspection, and read-only/no-lease behavior.

Final local validation:

```powershell
dotnet test tests/Societies.SnowGlobe.Tests/Societies.SnowGlobe.Tests.csproj --configuration Release --filter FullyQualifiedName~PersistedRunInspectorTests
dotnet test tests/Societies.SnowGlobe.Tests/Societies.SnowGlobe.Tests.csproj --configuration Release --filter "FullyQualifiedName~PersistedSessionV5PauseTests|FullyQualifiedName~RunStoreV5PauseTests"
dotnet test tests/Societies.SnowGlobe.Tests/Societies.SnowGlobe.Tests.csproj --configuration Release
dotnet build labs/Societies.SnowGlobe/Societies.SnowGlobe.csproj --configuration Release
```

Results: 30/30 focused inspector tests, 25/25 v5 pause/session tests, and 976/976 full Snow Globe tests pass in Release; the Release library build has 0 warnings / 0 errors; `git diff --check` is clean. Independent determinism/persistence/public-interface review is FINAL GO with no P0-P3 findings.

This is ordinary read/reconstruction evidence only. It is not a claim of power-loss or hardware durability, cross-host coordination, exactly-once behavior, deployment, or release readiness.
