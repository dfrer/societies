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

## Current offline cognition score-summary milestone (v4)

### Attempt-005 completed local v4 evidence

### Offline local-premium comparison completed

The bounded in-memory comparison is complete and historical. It used source benchmark 3,618 B SHA `961b54b7d8cfb2aead566579499adb3aa21f1d85bfbe0b7c6fc504a8adc40e0d` and source v4 artifact 16,148 B SHA `fecf71cbe8cc268dadb603d29735a816bc0152ccc79b4ea44c5a91d7e7616d3e`. `LocalPremiumComparison.Evaluate` produced an 8,916 B canonical report SHA `19f7053418471c8c70bdb9fffbfcca042f5bd87c24796a28227a672558990e56`, payload `3c3ff4a3e97344afb80d2a6283827e3d846c73e0b7730765c5d09601db6d4acc`, status `insufficient_live_premium_evidence`. Local results are 12 scenarios, 262/1200 (2183 bp), dispositions 0/7/2/1/2, category tuples 0/0, 0/0, 62/2066, 200/6666; `premium`, `premium_cost`, `performance_delta`, and `quality_delta` are null. Missing gates: `live_premium_profile_not_approved`, `live_premium_evidence_absent`, `live_premium_operational_metrics_absent`, `live_premium_quality_evidence_absent`, `live_premium_cost_evidence_absent`.

No report file was retained; no provider, network, live traffic, or workspace mutation occurred. Independent deep review was FINAL COMPARISON GO with no P0-P2 findings and exact bindings. The comparison action is historical/completed. Exactly one next action remains: offline Design-It-Twice of the first live-premium evidence/profile boundary, requiring separately chosen provider, official endpoint/auth/schema, credential lifecycle, redirect/status/retry/charge/cost policy, and explicit paid/provider authority before any traffic; no provider or authority is selected or available.

Committed HEAD `2e398a03a365ee259e6a31d25fbb83b6712f592f` (code `9bb4027`) completed the authorized v4 action. Preflight/record/validate ran exactly once (141 ms/26,420 ms/175 ms), all exited 0; record was `Complete`/`None`, 12/12. Plan digest `6c70ed6d69c378eb1fcfbc744dacdb4af41085cb57eaac42b4ee45e1ebd333b4`. No retry, alternate, fallback, download, or update. Artifact: `artifacts/snowglobe/local-model/qwen3.5-4b-recording-execution-v4.json`, 16,148 B, SHA `fecf71cbe8cc268dadb603d29735a816bc0152ccc79b4ea44c5a91d7e7616d3e`, payload `3f0c2e1a482a32aed83a7fb2ecbf046b91cb70feba8a8f23db08784fba59e531`; receipt 6,383 B digest `86ccd0b468a1b633f386b0abbe90386695f994c40be47f24dcffd63867529d65`, payload `86806453f8d1629414c989072118aef7cce88ad5ca6683795c348c2a454dceac`; summary 6,124 B digest `1958e8b6c4601c9a9e9834403cd431a67a48324735dca7484d10809509245a9a`, payload `d3dab5c219cdd1df235c73c499843189e47885df7dc9e289b6f79e8a6c6df325`; report 2,834 B digest `0e5fe1d7a8849caf7294c79c5c80863db0aa7521fb3b7e991b469867538a4fe1`, payload `c82dcd87f0862f0bc2a4fbeb7540354b06668818770138e94a9aaf8c8b62d3bc`. Recording evidence `d8f13f55aa829ea63d069391193d458a634c063f95db082f5d0346c7fd87bd99`, execution evidence `8c8bc4099d4dd5609d829be73a5baf0392e5537a66e8afb701ff32829c834389`, provenance `a17aafe40ab42fee9bdea7cedab5d23b3a0f4c932e32645a40ef9b4e45c16399`.

Raw-free score: 262/1200 (2183 bp), with shelter acquisition 0, construction 0, storage progression 62/2066 bp, safe restraint 200/6666 bp; dispositions 0/7/2/1/2 for `no-proposal`/`contract-invalid`/`domain-rejected`/`feasible-suboptimal`/`maximum-utility`. Ordered `cq1`..`cq12` valid. Exactly 12 POSTs returned 200; no GET/non-200/retry/raw; server total 18.3583708 s; cloud/body logging off; RTX2070S offload 34/34. v1/v2/v3 unchanged. Cleanup zero is operator-observed only. Deep review FINAL EVIDENCE GO, no P0-P2, all digests/arithmetic recomputed. Claims remain integrity/internal binding and retained loopback logs only.

The bounded offline local-premium comparison is complete and historical. The sole current action is the offline live-premium boundary Design-It-Twice described above.

Commit `9bb4027` adds the bounded, raw-free cognition score-summary codec. It embeds the canonical quality report once while retaining no prompts, responses, proposals, submissions, or model text. The fixed scoring tuple checks include scenario-specific lower-utility reachability; dispositions remain bounded, and the detached result is not authenticity evidence. Terminal outcomes include `Complete`, `Failed`, `Cancelled`, and `TimedOut`; only `Complete` populates the score summary/digest, while `EvidenceRejected` and other non-Complete terminal paths retain null summary/digest. There is no historical backfill.

The v4 schema identities are plan `snow_globe_ollama_recording_composition_plan/v4`, receipt `snow_globe_ollama_loopback_recording_receipt/v4`, and artifact `snow_globe_ollama_recording_execution_artifact/v4`; the retained artifact path is `artifacts/snowglobe/local-model/qwen3.5-4b-recording-execution-v4.json`. Transport remains v3 and the high-level adapter/profile remains v2. The receipt is canonical digest-only; the artifact embeds the score summary once. `LocalPremiumComparison` now has a two-input overload that emits local cognition facts only: `premium`, `premium_cost`, `performance_delta`, and `quality_delta` are null with status `insufficient_live_premium_evidence`. The one-input golden is unchanged. This proves structure and integrity, not authenticity, premium quality/cost/winner, general intelligence, or independent execution attestation.

For a complete result, only the score summary is populated; terminal and `EvidenceRejected` are null. Validation first showed red phases `10/23/33`, then `6/92/98`; the final targeted result was `98/98`, owned tests `157/157`, full SnowGlobe `706/706`, Recording CLI `59/59`, Benchmark CLI `56/56`, and three Release builds with 0 warnings/errors. Independent deep review was FINAL CODE GO with no P0-P2 findings. Snapshot: `21c3f18942072aaa6954a6f95dd86528c33b94386d6263938b906565a66032e8`.

Attempt-001 through Attempt-004 chronology and v1-v3 artifacts remain preserved; their actions are historical/superseded. Attempt-005 and the bounded offline comparison are complete. The sole current action is the offline live-premium boundary Design-It-Twice described above.

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

The previous recording-evidence action is historical/completed. Commit `8256512` historically implemented the offline `CognitionQualityRecordingSessionModule`: its public instance API was `Authorize` and `RecordOnceAsync`, and its sole public runtime Adapter at that commit was `OfflineFixedResponseCognitionQualityRecordingAdapter`. Current offline fixture inventory is the generic fixed-response fixture plus registry-closed `OfflinePinnedOllamaRecordingFixture`; neither is live. Authorization bound exact publication/prompt-set/provenance/Adapter identities, nonce, lifetime, and timeout; bounded non-evicting nonce tombstones and fixed-response capability tracking prevented replay. This remains offline fixture evidence only: no delivery/model-execution, network/provider/credential/payment/file/journal/world/live authority, Ollama call, or premium call exists. Focused validation passed 17/17, full Snow Globe Release 433/433, Release build 0 warnings/errors, and independent deep review was FINAL CODE GO after five corrections. See [the recording-session contract](labs/Societies.SnowGlobe/COGNITION_QUALITY_RECORDING_SESSION.md) and [ADR 0009](docs/adr/0009-offline-cognition-quality-recording-session.md).

The recording-session action is historical/completed. Commit `8a5d339` adds the test-only offline Adapter conformance harness. The fixed fixture is core-conformant but not fully conformant because midflight cancellation is not exercised; the async fixture exercises that seam and is fully conformant. Focused conformance validation is 5/5, full Snow Globe Release is 438/438, the Release build has 0 warnings/errors, and independent deep review is CODE GO. See [the conformance contract](labs/Societies.SnowGlobe/COGNITION_QUALITY_RECORDING_ADAPTER_CONFORMANCE.md) and [ADR 0010](docs/adr/0010-offline-cognition-quality-recording-adapter-conformance.md).

The pinned fixture action is complete and historical at `8f95875`. The registry-closed fixture is entirely in-memory with exactly twelve caller-supplied response buffers and frozen qwen3.5:4b metadata bound by a contract digest. The candidate-neutral harness proves its exact eleven ordered checks and full offline result. `OfflinePinnedOllamaRecordingFixtureTests` prove exact count/index read-once behavior, fixture-owned response/request zeroing, caller preservation, concurrent capability safety, and cancellation; generic session timeout semantics are predecessor evidence, not pinned-fixture timeout evidence. No path/PID/file/env/socket/process/provider/model/credential/payment/live authority exists, and transport/model execution is not attested. Validation is 12/12 focused, 445/445 full Release, 0 warnings/errors, and 56/56 benchmark CLI before the final narrow correction; deep review is CODE GO.

The former codec/fake-port action is historical and complete in `016551c`. See [the offline Ollama recording codec contract](labs/Societies.SnowGlobe/OFFLINE_OLLAMA_RECORDING_CODEC.md) and [ADR 0012](docs/adr/0012-offline-ollama-recording-codec.md). It remains offline evidence only; loopback transport/provider use is separately gated.

The former loopback transport implementation action is complete in local commit `a713267`; its documentation is committed in the prior docs milestone. Commit `bd89187` now adds the offline Ollama recording composition and fixed artifact writer; see [CURRENT_BUILD.md](CURRENT_BUILD.md), [the composition contract](labs/Societies.SnowGlobe/OLLAMA_RECORDING_COMPOSITION.md), and [ADR 0014](docs/adr/0014-offline-ollama-recording-composition.md). The prior dry-run-only gate is historical; local code commit `be7c691` added the record-once surface and a later authorized invocation consumed one fresh authority, with outcome and next action recorded below.

### Attempt-003 v2 settlement (historical evidence)

Commit `6e47510` produced historical EVIDENCE GO: exactly one preflight, one record, one validate, and one HTTP200 POST. Its `ResponseHeaders`/`TransferEncoding` values were typed code-path evidence, not independent wire capture; no accepted body/wrapper/nested evidence/12-slot/quality/compatibility claim was made. The historical v3 section below records the superseded offline framing action.

## Offline Ollama recording composition (historical first attempt; superseded by v2)

The v1 attempt settlement from HEAD `af0925d` is historical and superseded by v2; v2 is now historical as well. Local fix commit `98b3ec5` is historical. Attempt-003 is historical EVIDENCE GO with no accepted body/wrapper/nested evidence/12-slot/quality/compatibility claim.

Historical pre-attempt v2 implementation from `a5a0823` preserved the public composition operations and CLI arguments while adding shared typed raw-free checkpoint/policy coherence and v2 plan/receipt/artifact/transport. The fixed v2 path and both v1 tombstones remain unchanged. Its offline validation was v2 110/110, review 129/129, SnowGlobe 615/615, RecordingCli 49/49, builds 0/0, deep CODE GO. Attempt-003 evidence and the following v3 section are historical; v4 is current.

Commit `bd89187` adds a deep fixed-cell Module with exactly three public operations: deterministic zero-I/O `Prepare`, atomic single-use `ExecuteAndPublishOnceAsync`, and bounded local-read `ValidateArtifact`. Public inputs are only the repository root, observed process ID/start ticks, and authorization nonce; endpoint/model/path/hash/header/timeout/retry/delegate/Adapter/store selectors are not caller inputs. The plan binds a canonical Windows repository-root digest and nonce digest, remains object/module-bound, and consumes on foreign, cancelled, or reused execution.

Historical v1 execution reserved a safe CreateNew artifact target before inner `Authorize`/`RecordOnce`, permitted one attempt, and wrote/read back only at `artifacts/snowglobe/local-model/qwen3.5-4b-recording-execution-v1.json`. Its schema was `snow_globe_ollama_recording_execution_artifact/v1`; writer failure left an indeterminate tombstone and never deleted or retried. This paragraph records rollback history only; v2 and v3 are historical/superseded.

The isolated `RecordingCli` now exposes preflight, validate, and separately gated record-once. Local code commit `be7c691` added that surface; a later authorized invocation consumed one fresh authority: exactly one preflight invocation exited 0 with plan `a9e7a10b973c7114d01361cbbeaa5705bd782385664d5a5ef923e0df3b5df39d`, exactly one record-once invocation exited 5 after 7,881 ms, and exactly one validate invocation exited 1 `artifact_size_invalid`. Exactly one HTTP 200 POST occurred in 7.0314715 s; no second POST/retry/fallback/alternate/download. The fixed artifact remains a preserved 0-byte tombstone (SHA-256 `e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855`), so no valid artifact/evidence exists. Strongest source-consistent attribution is post-response `RuntimeChanged`, unconfirmed because the checkpoint was not retained. Cleanup found no Ollama/llama-server, 11434/11435 rows, or attributable GPU process. Offline correction `959bea5` accepts the exact RuntimeChanged receipt matrix and removes the consumed-TCP-row requirement at AfterExchange; earlier PID/start/path/hash/listener and exact tuple checks remain. This is not compatibility, artifact-loaded/model execution, quality, cost, world-authority, production, or commercial proof.

The isolated `labs/Societies.SnowGlobe/` lab includes Cognition Quality Corpus v1: 12 fixed survival-progression scenarios in four categories, legitimate scratch-world reconstruction, deterministic `ValidateAndCommit` feasibility, a closed 1,200-point integer rubric, five bounded dispositions, and content-addressed corpus/scoring/submission/report evidence. This is fixed-corpus single-step utility evidence only—not general intelligence, model quality, a provider winner, or price evidence. It performs no live model/provider/credential/payment action and has no provider/model/file/network/world authority. See [COGNITION_QUALITY.md](labs/Societies.SnowGlobe/COGNITION_QUALITY.md) for the contract and current handoff.

Performance result schema v6 separates deterministic simulation preconditioning from cache treatment and records independent non-persistent selector and extraction-planning modes; current runtime batch output is `runtime-batch-metrics-v6.csv` with 44 columns. Older v4/v5 CSV claims are historical profiling evidence only. `cold` clears only the derived route cache, `natural_warm` retains the naturally populated cache, and `forced_invalidation` commits one prepared path segment and proves the first exact post-change lookup uses the new navigation version. Eager/all-pairs prewarming remains disabled. W1-04 caches reachability and cell routes while rematerializing exact endpoints; W1-05 uses safe exact branch-and-bound selection; W1-05c adds exact bounded extraction planning and a byte-equivalent indexed A-star representation. Exhaustive modes remain benchmark references only. Only `run-performance-cache-modes.ps1` can set `cacheModeEvidence`: it also requires cold/warm configuration and hash identity and explicitly leaves baseline, full-matrix, median, and target/safety claims false.

The following W1-03c/W1-04/W1-05 performance-progression paragraphs preserve historical milestone commands and blockers; current truth is the W2-06/W3-01 summary above.

The clean verified ExportRelease cache-mode comparison passed from implementation commit `5444cc3`; see `planning/active/evidence/v3-w1-03b-cache-mode-validation.json`. It proves the three mode contracts and cold/warm deterministic equivalence for a short three-citizen smoke.

The canonical W1-03c matrix completed from clean commit `a636967`. All 14 pairs, 28 metrics rows, artifact-integrity checks, cold/warm comparisons, three reference trials, repeated soaks, and the forced transition passed their evidence contracts. The measured budget did not pass: the 16-citizen cold median was p95 570.6155 ms and max 3694.2534 ms against 50 ms and 250 ms safety limits. The forced invalidation interval itself passed at 8.4171 ms, and eager/all-pairs warmup remains benchmark-only. Performance-result artifacts use schema v6; runtime metrics CSV remains schema v4. See `planning/active/evidence/v3-w1-03c-performance-baseline-validation.json`. Week 2 feature expansion is blocked; continue correctness and algorithmic path-selection work in Godot before rerunning the matrix.

W1-04 corrects the navigation contract at implementation commit `7918d49`: blocked or disconnected endpoints no longer receive fabricated routes, diagonals cannot cut blocked corners, discounted paths retain an admissible deterministic A* search, and unreachable work is skipped with a stable diagnostic. Wetland reeds and clay use deterministic walkable interaction positions, including legacy snapshot normalization, without weakening blocked-terrain semantics. Local validation passed 110/110 .NET tests, 16/16 Godot headless tests, and all tracked managed configurations with zero warnings. See `planning/active/evidence/v3-w1-04-navigation-validation.json`.

W1-05 exact branch-and-bound selection is complete with clean Release evidence at `227a758`. Four shipped scenarios match the exhaustive reference for 300 ticks, and the 16-citizen selector drops exact path queries from 17,441 to 2,544 (85.414%) with identical deterministic hashes. Its three-trial Release median p95 is 78.0301 ms versus 656.3981 ms exhaustive. The full post-W1-05 matrix also improved the optimized 16-citizen reference median p95 from 570.6155 ms to 81.4823 ms, but its 1,552.5664 ms median maximum means the overall safety gate remains red. Do not begin Week 2 feature expansion yet; use the new route-selection diagnostics to isolate the remaining spikes. See `planning/active/evidence/v3-w1-05-job-selection-validation.json`.

W1-05b now isolates the remaining spikes from all 14 clean schema-v4 Release pairs and 5,301 diagnostic ticks. Cache misses correlate with wall time at `r=0.982573`, compared with `r=0.233264` for total lookup volume and `r=0.076652` for navigation rebuild time. Completing path segments clears the derived route cache; the following work-order ranking and idle-citizen selection repopulate it with exact A* searches. In the 16-citizen reference, all seven ticks over 250 ms are initial-cold or immediately post-invalidation, and all six ticks over one second follow invalidation. The metrics-on analysis is diagnostic rather than a new timing gate; the canonical safety failure remains authoritative. W1-06 and Week 2 remain blocked until exact cache-repopulation work is validated by a fresh matrix. See `planning/active/evidence/v3-w1-05b-spike-characterization.json`.

## Status

## Historical Snow Globe v3 live settlement (Attempt-004)

Clean code HEAD `18f2dc622ce27f14dd9f5d4126176a944244ae8d` is FINAL EVIDENCE GO with no P0-P2 findings. Static preflight was accepted after the GPU gate was correctly scoped to non-Ollama WDDM applications. Server `server1`; CLI preflight 1 succeeded with plan `13788130a3573ba8205cf833495e877ca26fb0daecab421bcab27880d4cb4e31`; record 1 succeeded in 28,609 ms; validate 1 succeeded in 148 ms. Exactly 12 ordered `POST /api/generate` requests returned 200 in 20.2868581 s. There was no retry, fallback, alternate, pull, update, cloud, credential, payment, or other provider action.

The v3 identities are plan `snow_globe_ollama_recording_composition_plan/v3`, receipt `snow_globe_ollama_loopback_recording_receipt/v3`, artifact `snow_globe_ollama_recording_execution_artifact/v3`, and transport `snow-globe-ollama-loopback-recording-transport-adapter/v3`. Artifact path: `artifacts/snowglobe/local-model/qwen3.5-4b-recording-execution-v3.json`, 9,621 B, SHA/canonical `12c3e0f9b8fe13f8eaf2525642e130e4298e18c37f2fff58c2a316d2292f7b67`, payload `da3797cfe041ec949083eaf6e5ec9fecd22df4564ff767b176d94e2da10a50a1`. Receipt is 6,169 B, SHA `e1a43bc8b7c44dfde6d71e372f1c6237239efe4ffd60716b869be72bf9dcb6b1`, payload `4d3541fff307a3f7dcd5aea1958c51ba0cc49f7b62df01ee07b086868dfb97fc`. Nested evidence digest: `cd846a45a85085d1943ce8eb0c8b10ad489a802c1727f58d9d1ca04328e594e7`.

The run completed all 12 slots with `ResponseReceived`/200, `NotApplicable`, checkpoint/policy `None`, zero counters, and `additional=false`; the validator accepted it. CUDA RTX2070S was 34/34. Operator cleanup was zero but was not independently retained or re-observed. Raw-free/cloud/body-log were false. Evidence limits remain: HttpClient-exposed framing only, no raw-wire proof; nested scoring digest is not embedded or revalidated; retained captures cannot prove absence of overwritten invocations; HEAD is provenance, not an artifact field; cleanup is an operator observation. No quality, intelligence, winner, cost, commercial, general compatibility, or world-authority claim is made. The v3 preflight/live action and its score-summary next action are complete and historical.

This tranche is about stabilizing the Godot validation base so the next prototype step can build from a truthful, deterministic, testable foundation.
