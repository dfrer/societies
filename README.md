# Societies

Societies is currently a Godot 4 + C# prototype for validating a low-friction society-sim foundation.

The authoritative executable target is the Godot project under `src/societies/`.
## Product North Star

> "A deterministic civilization/ecology simulation where humans and AI citizens work, trade, negotiate, govern, and experience shared consequences."

This is future product intent, not a claim that all of those systems are implemented. The deterministic simulation owns facts and every world-changing outcome; future LLM capabilities interpret structured state, deliberate, communicate, summarize memory, and propose actions through validated deterministic commands/events. See [the product thesis](planning/PRODUCT-THESIS.md).


- Project: `src/societies/project.godot`
- Main scene: `src/societies/scenes/main.tscn`
- C# project: `src/societies/Societies.csproj`
- C# solution: `src/societies/Societies.sln`
- Current default branch: `master`

Use [CURRENT_BUILD.md](CURRENT_BUILD.md) as the short repo-truth reference.

## Current Prototype

See [CURRENT_BUILD.md](CURRENT_BUILD.md) for the up-to-date prototype scope, validation commands, and implementation details.
Current implemented reality includes W2-04/W2-05, W2-06, W3-01 merged through PR #122 at master `7b747af`, W3-02 merged through PR #123 at origin/master `d9e297f`, and W3-03 locally committed at `a513636` on `feature/v3-w3-03-wetland-consequences`. W3-03 docs/evidence publication and GitHub delivery remain pending; W3-04+ remain inactive until another explicit **Continue V3** decision.

W2-04/W2-05 remain historical merged milestones; see [CURRENT_BUILD.md](CURRENT_BUILD.md), [W3-02 validation evidence](planning/active/evidence/v3-w3-02-validation.json), and [W3-03 validation evidence](planning/active/evidence/v3-w3-03-validation.json). W3-03 adds deterministic Protect/DrawDown wetland quotas and health consequences, pre-ledger player+AI enforcement, canonical events, schema-v9 persistence with v5-v8 migration/legacy continuation, and a minimal HUD. It does not add W3-04 LLM/fallback, restoration jobs, general law, markets, or broad UI. Git/GitHub remains authoritative for delivery/merge state.


## Planning vs Code

The `planning/` tree contains long-term design material, including older and more ambitious directions than the current implementation.

Treat planning documents as aspirational unless they are confirmed by the current Godot code under `src/societies/`.

## Runtime Controls

- `1`: craft Stone Axe
- `2`: select Food & Fuel directive
- `3`: select Shelter directive
- `F3`: inspect citizen
- `F4`: inspect structure
- `F5`: toggle weather
- `F6`: save snapshot, event log, and run summary
- `F7`: reset the current deterministic run
- `F8`: toggle observer camera
- `F9`: load the latest snapshot set
- `F10`: toggle overlay
- `F11`: advance to next build
- `F12`: pause/unpause
- `Tab`: toggle inventory panel
- `E`: interact

## Repository Layout

- `src/societies/` - authoritative Godot project
- `tests/Societies.Core.Tests/` - fast .NET unit tests
- `planning/` - long-term design and research material
- `scripts/` - local workflow scripts

## Optional Performance Runs

Run a matching metrics-off/metrics-on Debug characterization pair from clean committed source:

```powershell
./scripts/run-performance-pair.ps1 -Scenario balanced_basin -Seed 1337 -Citizens 3 -Ticks 3 -CacheMode cold
```

On Windows, run the same short pair through the tracked Godot Release export route after installing the Godot 4.6.2 .NET export templates:

```powershell
./scripts/run-performance-pair.ps1 -ReleaseExport -Scenario balanced_basin -Seed 1337 -Citizens 3 -Ticks 3 -CacheMode cold
```

Run the complete cache-mode contract with identical cold/warm preconditioning and a one-tick forced invalidation case:

```powershell
./scripts/run-performance-cache-modes.ps1 -Scenario balanced_basin -Seed 1337 -Citizens 3 -PreconditioningTicks 2 -Ticks 2

# Verified Release route on Windows.
./scripts/run-performance-cache-modes.ps1 -ReleaseExport -Scenario balanced_basin -Seed 1337 -Citizens 3 -PreconditioningTicks 2 -Ticks 2
```

Run the canonical W1-03c Release matrix on Windows from clean committed source:

```powershell
./scripts/run-performance-baseline-matrix.ps1
```

Compare the exhaustive selector against exact branch-and-bound using one hash-pinned Release bundle and three counterbalanced trials:

```powershell
./scripts/run-job-selection-comparison.ps1 -ReleaseExport -Scenario balanced_basin -Seed 1337 -Citizens 16 -Ticks 300 -Trials 3
```

Reproduce the current residual spike attribution from the ignored W1-05c matrix artifacts:

```powershell
./scripts/analyze-performance-spikes.ps1 -InputPath artifacts/performance/w105c-baseline-a772d15 -OutputPath artifacts/performance/w105c-baseline-a772d15/spike-analysis.json
```

The matrix authority runs 14 metrics-off/on pairs: cold and natural-warm 300-tick cases for 3, 6, 12, and 16 citizens; three comparable cold 16-citizen reference trials; two 1,000-tick deterministic soaks; a 24-citizen stress case; and one forced invalidation case. `-PlanOnly` validates the inventory without making evidence claims, and `-CaseId` produces non-baseline partial characterization.

The Release route exports the `Windows Performance Release` preset and hard-fails unless the generated runner reports a managed `ExportRelease` assembly running in a non-debug Godot release template. The tracked solution maps Godot's `Debug`, `ExportDebug`, and `ExportRelease` configurations one-to-one. The base editor project still opens `scenes/main.tscn`; the preset's custom `performance_runner` feature selects `tests/PerfRunner.tscn` only in that export. Catalog JSON is explicitly packed and exported runs read it through `res://data`, so results do not depend on the process working directory.

These runs are not part of the pull-request gate. The runner writes ignored artifacts under `artifacts/performance/` and rejects content-dirty source by default so results identify reproducible code; stat-only touches whose Git blobs still match are not misclassified. It discovers Godot from `-GodotPath`, `GODOT_BIN`, `PATH`, or the standard WinGet package location. The editor/headless route remains Debug characterization. The Release execution route was first validated from clean commit `acf634f`; see `planning/active/evidence/v3-w1-03a-release-route-validation.json`.

## Offline cognition-quality corpus

The lab also emits `snow_globe_cognition_quality_execution_evidence/v1`: a pure synchronous, content-addressed envelope for one exact ordered twelve-proposal batch. It binds caller-attested local or premium provenance, the frozen corpus/scoring/submission/report digests, and the canonical payload/final evidence digest while embedding the recorded submission and report for standalone recomputation. Premium provenance derives the execution-policy digest plus model, prompt, and schema identities from one validated `ModelPolicySnapshot`; it does not retain the snapshot object or emit raw host, route, or cost fields. The envelope is capped at 64 KiB and makes no execution-attestation, provider, network, credential, payment, journal, live-action, world-authority, general-intelligence, model-quality, winner, or cost claim. See [the execution-evidence contract](labs/Societies.SnowGlobe/COGNITION_QUALITY_EXECUTION_EVIDENCE.md).

Commit `c7926d3` adds the pure synchronous `snow_globe_cognition_quality_recorded_response_run/v1` runner for exactly 12 ordered, already-recorded response fixtures. It enforces 1..1,024 raw response bytes per response, 12,288 bytes aggregate, and a 96 KiB canonical-output limit; invalid UTF-8 maps to `response_utf8_invalid`. Corrupt envelopes abort; correctly bound malformed content becomes `no_proposal` with a closed outcome; representable invalid proposals remain scorer input. The detached, raw-free artifact binds distinct runner/parser/proposal identities, the caller-supplied prompt revision, caller-attested provenance, response digests, and nested execution evidence. It is not execution attestation or v3 normalized replay and has no network, model, provider, credential, payment, journal, file, world, or live-call authority. Validation passed 11/11 focused tests, 404/404 full Snow Globe Release tests, a 0-warning/0-error Release build, and independent deep review. See [the recorded-response runner contract](labs/Societies.SnowGlobe/COGNITION_QUALITY_RECORDED_RESPONSE_RUNNER.md) and [ADR 0006](docs/adr/0006-offline-cognition-quality-recorded-response-runner.md).

Commit `bcba42a` adds the offline `snow_globe_cognition_quality_prompt_envelope_publication/v1` boundary. Its pure builder publishes exactly 12 corpus-ordered compact UTF-8 prompts under `snow_globe_cognition_quality_prompt/v1`, with a caller-supplied canonical prompt revision embedded in each prompt. Prompt bytes are bounded to 1..2,048 each, 24,576 aggregate, and 64 KiB publication; slots bind scenario/observation digests and publish empty response fields. Prompts contain survival rules, costs, observations, and response grammar, while excluding scenario/category, score, preferred answer, setup/state/event, model/provider, credential, and financial data. The canonical artifact binds corpus/scoring/validator, runner/parser/proposal identities, prompt bytes/digests, prompt-set/payload/final digests, and claim limitations. Validation passed 6/6 focused, 410/410 full Release, and a 0-warning/0-error Release build; independent deep review returned FINAL CODE GO. See [the prompt-envelope contract](labs/Societies.SnowGlobe/COGNITION_QUALITY_PROMPT_ENVELOPE.md) and [ADR 0007](docs/adr/0007-offline-cognition-quality-prompt-envelope.md).

Commit `bf756ed` completes that recording-evidence action with `snow_globe_cognition_quality_recording_evidence/v1`. Its one pure synchronous `Create(publication, provenance, exact ordered responses)` operation validates and embeds the exact prompt publication and exact recorded-response run, including nested execution evidence, and adds an ordered response-set digest. It requires exactly 12 responses of 1..1,024 bytes, caps aggregate response input at 12,288 bytes and the canonical artifact at 192 KiB, and is all-or-error only in memory. The raw-free result retains no response bytes; Module-owned temporary snapshots and detached fixtures are cleared, while caller-owned inputs are not. Response identity is caller-attested: the envelope proves neither prompt delivery nor model execution, records no provider status/retry/charge evidence, and grants no network, provider, credential, payment, journal, file, authoritative-world, or live-action authority. It makes no quality, intelligence, winner, or cost claim, and `src/societies/` is unchanged. Goldens are response-set `0c9ce26bf5f078e3cdcb85a2115f59f9a3e8d191736e8ab8e87c0c113b67e80c`, payload `069aa258c0a6870aa6d8c60f14aed800cbb46923564d3b62f36a41ba3159a7fd`, and final `61d0f7150b4b1cde5fba3f693e1a60eec6410deb83b6a371b62189f59a2115a4`. Validation passed 30/30 focused new-plus-predecessor tests, 416/416 full Snow Globe Release tests, and a 0-warning/0-error Release build; independent deep review returned FINAL CODE GO after four adversarial identity/digest fixes. See [the recording-evidence contract](labs/Societies.SnowGlobe/COGNITION_QUALITY_RECORDING_EVIDENCE.md) and [ADR 0008](docs/adr/0008-offline-cognition-quality-recording-evidence.md).

The previous recording-evidence action is historical/completed. Commit `8256512` implements the offline `CognitionQualityRecordingSessionModule`: its public instance API is `Authorize` and `RecordOnceAsync`, and its sole public runtime Adapter is `OfflineFixedResponseCognitionQualityRecordingAdapter`. Authorization binds exact publication/prompt-set/provenance/Adapter identities, nonce, lifetime, and timeout; bounded non-evicting nonce tombstones and `OfflineFixedResponseCognitionQualityRecordingAdapter` capability tracking prevent replay, capped at 4,096. One atomic capability use drives exactly twelve sequential one-attempt slots, with bounded response bytes and exact binding echoes. Evidence appears only after all twelve; failures are raw-free and evidence-free. Fixtures and prompt copies are explicitly disposed and zeroed, while caller inputs remain intact. This remains offline fixture evidence only: no delivery/model-execution, network/provider/credential/payment/file/journal/world/live authority, Ollama call, or premium call exists. Focused validation passed 17/17, full Snow Globe Release 433/433, Release build 0 warnings/errors, and independent deep review was FINAL CODE GO after five corrections. See [the recording-session contract](labs/Societies.SnowGlobe/COGNITION_QUALITY_RECORDING_SESSION.md) and [ADR 0009](docs/adr/0009-offline-cognition-quality-recording-session.md).

The recording-session action is historical/completed. Commit `8a5d339` adds the test-only offline Adapter conformance harness. The fixed fixture is core-conformant but not fully conformant because midflight cancellation is not exercised; the async fixture exercises that seam and is fully conformant. Focused conformance validation is 5/5, full Snow Globe Release is 438/438, the Release build has 0 warnings/errors, and independent deep review is CODE GO. See [the conformance contract](labs/Societies.SnowGlobe/COGNITION_QUALITY_RECORDING_ADAPTER_CONFORMANCE.md) and [ADR 0010](docs/adr/0010-offline-cognition-quality-recording-adapter-conformance.md).

Exactly one current next action for this lane: design and implement an entirely **OFFLINE** pinned local Ollama recording Adapter fixture against this harness, without starting Ollama, making model calls, using network, or changing production live/provider authority.

The isolated `labs/Societies.SnowGlobe/` lab includes Cognition Quality Corpus v1: 12 fixed survival-progression scenarios in four categories, legitimate scratch-world reconstruction, deterministic `ValidateAndCommit` feasibility, a closed 1,200-point integer rubric, five bounded dispositions, and content-addressed corpus/scoring/submission/report evidence. This is fixed-corpus single-step utility evidence only—not general intelligence, model quality, a provider winner, or price evidence. It performs no live model/provider/credential/payment action and has no provider/model/file/network/world authority. See [COGNITION_QUALITY.md](labs/Societies.SnowGlobe/COGNITION_QUALITY.md) for the contract and current handoff.

Performance result schema v6 separates deterministic simulation preconditioning from cache treatment and records independent non-persistent selector and extraction-planning modes; current runtime batch output is `runtime-batch-metrics-v6.csv` with 44 columns. Older v4/v5 CSV claims are historical profiling evidence only. `cold` clears only the derived route cache, `natural_warm` retains the naturally populated cache, and `forced_invalidation` commits one prepared path segment and proves the first exact post-change lookup uses the new navigation version. Eager/all-pairs prewarming remains disabled. W1-04 caches reachability and cell routes while rematerializing exact endpoints; W1-05 uses safe exact branch-and-bound selection; W1-05c adds exact bounded extraction planning and a byte-equivalent indexed A-star representation. Exhaustive modes remain benchmark references only. Only `run-performance-cache-modes.ps1` can set `cacheModeEvidence`: it also requires cold/warm configuration and hash identity and explicitly leaves baseline, full-matrix, median, and target/safety claims false.

The following W1-03c/W1-04/W1-05 performance-progression paragraphs preserve historical milestone commands and blockers; current truth is the W2-06/W3-01 summary above.

The clean verified ExportRelease cache-mode comparison passed from implementation commit `5444cc3`; see `planning/active/evidence/v3-w1-03b-cache-mode-validation.json`. It proves the three mode contracts and cold/warm deterministic equivalence for a short three-citizen smoke.

The canonical W1-03c matrix completed from clean commit `a636967`. All 14 pairs, 28 metrics rows, artifact-integrity checks, cold/warm comparisons, three reference trials, repeated soaks, and the forced transition passed their evidence contracts. The measured budget did not pass: the 16-citizen cold median was p95 570.6155 ms and max 3694.2534 ms against 50 ms and 250 ms safety limits. The forced invalidation interval itself passed at 8.4171 ms, and eager/all-pairs warmup remains benchmark-only. Performance-result artifacts use schema v6; runtime metrics CSV remains schema v4. See `planning/active/evidence/v3-w1-03c-performance-baseline-validation.json`. Week 2 feature expansion is blocked; continue correctness and algorithmic path-selection work in Godot before rerunning the matrix.

W1-04 corrects the navigation contract at implementation commit `7918d49`: blocked or disconnected endpoints no longer receive fabricated routes, diagonals cannot cut blocked corners, discounted paths retain an admissible deterministic A* search, and unreachable work is skipped with a stable diagnostic. Wetland reeds and clay use deterministic walkable interaction positions, including legacy snapshot normalization, without weakening blocked-terrain semantics. Local validation passed 110/110 .NET tests, 16/16 Godot headless tests, and all tracked managed configurations with zero warnings. See `planning/active/evidence/v3-w1-04-navigation-validation.json`.

W1-05 exact branch-and-bound selection is complete with clean Release evidence at `227a758`. Four shipped scenarios match the exhaustive reference for 300 ticks, and the 16-citizen selector drops exact path queries from 17,441 to 2,544 (85.414%) with identical deterministic hashes. Its three-trial Release median p95 is 78.0301 ms versus 656.3981 ms exhaustive. The full post-W1-05 matrix also improved the optimized 16-citizen reference median p95 from 570.6155 ms to 81.4823 ms, but its 1,552.5664 ms median maximum means the overall safety gate remains red. Do not begin Week 2 feature expansion yet; use the new route-selection diagnostics to isolate the remaining spikes. See `planning/active/evidence/v3-w1-05-job-selection-validation.json`.

W1-05b now isolates the remaining spikes from all 14 clean schema-v4 Release pairs and 5,301 diagnostic ticks. Cache misses correlate with wall time at `r=0.982573`, compared with `r=0.233264` for total lookup volume and `r=0.076652` for navigation rebuild time. Completing path segments clears the derived route cache; the following work-order ranking and idle-citizen selection repopulate it with exact A* searches. In the 16-citizen reference, all seven ticks over 250 ms are initial-cold or immediately post-invalidation, and all six ticks over one second follow invalidation. The metrics-on analysis is diagnostic rather than a new timing gate; the canonical safety failure remains authoritative. W1-06 and Week 2 remain blocked until exact cache-repopulation work is validated by a fresh matrix. See `planning/active/evidence/v3-w1-05b-spike-characterization.json`.

## Status

This tranche is about stabilizing the Godot validation base so the next prototype step can build from a truthful, deterministic, testable foundation.
