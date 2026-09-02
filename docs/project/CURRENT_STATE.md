# Societies Current State

- **Status date:** 2026-09-02 (Packet 02A branch-local deterministic substrate implementation)
- **Accepted runtime source:** `847c86b1c379e6a1dd8d4b7b641c3c89646e28c9`
- **Preservation branch:** `archive/pre-consolidation-2026-08-27`
- **Consolidation integration record:** PR #183 at `54a4e5c0ea1297438b06e4b40ea14391db343657`
- **Governance workflow integration:** PR #186 at `420738bfc1b51cffacd94845b4e10cb9c72db081`
- **Branch-protection documentation closeout:** PR #189 at master commit `1eaa1ab6b0c79550a99c9cad68c4ea04e9fdea75`
- **Planning authority:** PR #191 merged at master/base `31ea1d6012d6fd932d0bfe0dbc621e668fd58c80`
- **Packet 01 local checkpoint:** `936771285fd1fd2ebb92054dc3630a656a78b941` on `feature/social-kernel-01-baseline`; verified tree `2bd4a709a54ae72fde84fc3e24968e5fec49e49b`
- **Packet 01 delivery:** [PR #192](https://github.com/dfrer/societies/pull/192), exact head `ea538a506318f99946f858c7c4b50508b65fb44c`, merged as `f471efe5fe5f5b62aef37e48f27bc1fbffd0104e`
- **Visual-recovery amendment:** [PR #194](https://github.com/dfrer/societies/pull/194), planning head `52557d0a517b841421aa60a7b7b295375908c35f`, merged as `ccb651c67689c25eb0f3adec78abbfcf870699f5` at `2026-09-02T03:28:09Z`
- **Active milestone:** [`SNOW-GLOBE-SOCIAL-KERNEL-V1 — Causeway Before Nightfall`](../../planning/active/MILESTONE.md)
- **Current packet:** Packet 02A is implemented locally on `feature/social-kernel-02a-causeway-substrate`, based on `master` `1745896535124bd39ca6321fe6430d93de81bf43`, awaiting technical review and hosted delivery. Packet 02V follows only after that delivery; Packet 03 is **NOT STARTED** and unauthorized until the 02V owner gate passes.

This document reports what is proven, not everything planned or implemented somewhere in repository history.

## Packet 02A branch-local status

Packet 02A retains deterministic causeway state/catalog/events/session, schema-v12 persistence/replay/artifact/summary, v5 route/profile, `ExecuteCausewayIntent`, and zero-tick HUD refresh suppression/coalescing. It explicitly drops the rejected presenter/`Label3D`, keyboard review controls, source-string UI scaffold/tests, and historical evidence/status. The implementation is not committed, PR-open, hosted-green, merge-ready, playable, visual-complete, or release-ready. Historical checkpoint `7aad77dc10f2edf84e44f5f43e0082568a1ab8e9` and rejection head `269c80e16766094378afd4809bca40e045ab686b` remain unpublished/read-only history.

Local evidence: governance passed; Causeway authority 44/44; authority/artifact/baseline 85/85; broader persistence/voxel 162/162; executable profile/tooling and accepted-scene contracts 21/21, including 8/8 fail-closed tooling cases; full managed 582/582 and fast 462/462 with zero failures or skips; integration 11 and soak 109 exactly declared; direct Release and Debug builds 0 warnings/errors; Godot headless 29/29 with observed exit 0. Schema-v12 freezes and binds the actual Causeway definition schema/version/SHA-256 digest and fails closed on catalog mismatch; exact-field, duplicate, order, and valid-anchor checks are strict. The accepted deterministic route transition is `ContributeCommunityTimber`, revision `0->1`, with Causeway equality across edit/reload/fixed replay. Earlier ad-hoc reuse-derived timing is invalid and is not Packet 02A evidence. A corrected fresh export with explicit `--quit` exited 0 and finalized a fail-closed v2 attestation, but post-export full-worktree drift stopped the wrapper before trials. All six current performance trials are unrun, so Packet 02A has no current performance classification; the 16.67 ms target, 33.33 ms hard-safety result, and GPU/display timing remain unresolved. The known `--build-solutions` signal-11 attempt remains red and was not repeated.

## Executive assessment

Societies has a substantial deterministic foundation and a user-accepted bounded founder-worldcraft experience. Snow Globe is making materially better, more unified progress than the fragmented pre-V3 period. The project has crossed from speculative architecture into a credible product base.

It has **not** yet proven its central thesis. The accepted player-facing scene still has **zero participating citizens**. The first Packet 02 human visual gate was rejected as not near visual completeness (“basically none of this is near actual visual completleness, needs massive work”). The owner preserves the broad block-based substrate, selects a weathered civic-folk wetland production direction, and requires the representative district to pass the Packet 02V in-engine gate before introducing Mara, Ivo, or Sena. Decision [D-016](DECISION_LOG.md#d-016--recover-causeway-visual-production-before-social-breadth) supersedes D-014 only on sequencing and visual detail; mechanics remain salvageable evidence.

Packet 01 establishes a clean, reproducible baseline for the authoritative empty Snow Globe scene and its four-leg edit/reload/replay route. The realtime route is mechanically validated and deterministic; three fixed-60 executions provide identity evidence only. The product performance target is not met: worst physics p95 is **23.109 ms** against **16.67 ms**. Both process and physics p95 metrics pass the **33.33 ms hard-safety** line. Required hosted checks passed and PR #192 merged through the protected branch path. The visual-recovery amendment then merged as PR #194 with required `build-test-smoke` and Snow Globe laboratory evidence passing. No human product gate is claimed. This remains characterization, not performance acceptance, deployment, accessibility acceptance, or release readiness.

## Product runtime: proven

The Godot 4 + C# product under `src/societies/` provides an authoritative deterministic settlement runtime with command/event ownership, persistence, replay, diagnostics, world generation, resource and work systems, and a player-facing scene.

The accepted EB-01R bounded slice adds or proves:

- an editable finite voxel world candidate;
- deterministic player grounding and bounded world edits;
- embodied gathering and a bounded inventory;
- modular floor, wall, and post construction;
- valid/invalid placement projection with authoritative reasons;
- dismantling and material return behavior;
- save/load, migration, and replay support for the worldcraft state;
- a centered field-kit/tool-belt interaction surface;
- user-accepted world readability, HUD hierarchy, inventory usability, construction clarity, and interaction feel.

The acceptance is recorded in the [EB-01R repository attestation](../../planning/active/evidence/v3-sg-eb-01-validation.json). Numeric per-axis scores were not supplied and are not inferred.

The accepted voxel scenario itself remains socially empty: it declares `initialCitizens: 0`, and current catalog validation forbids citizens, resources, stock, and crisis state in voxel scenarios. Legacy heightfield worker, civic, wetland, and crisis systems remain reusable infrastructure and regression harnesses, not accepted social UX or an alternate current product route.

## Previously merged hosted product evidence

The integrated founder-worldcraft baseline was validated on clean GitHub-hosted runners before merge:

- authoritative Godot project Release build passed with zero warnings and zero errors;
- the complete managed suite passed **507/507** with zero failures;
- the permanent pull-request fast tier passed **387/387** and verified the manifest count;
- the Godot headless suite passed **28/28** and verified the manifest count;
- the Godot C# solution build passed;
- project-governance and patch-whitespace validation passed.

Those **507 managed / 387 fast / 28 Godot** counts remain earlier hosted evidence. Packet 01's distinct local capture passed the complete **519/519** managed suite and **399/399** fast tier. Packet 01's hosted PR gate ran the manifest-owned **399/399** fast tier and **28/28** Godot suite; the complete 519-test managed run was local evidence and is not relabeled as hosted. The pull-request and weekly-full workflows fail when discovered counts drift from manifest authority instead of silently accepting missing tests.

## Snow Globe laboratory: proven

The isolated .NET 8 laboratory under `labs/` contains real, reusable evidence for:

- persistent agents and immutable observations;
- deterministic sequential and controlled-parallel scheduling;
- proposal validation and ordered commits;
- deterministic fallback and failure handling;
- checkpoint/resume, recorded responses, replay, and metrics;
- cognition-quality corpora and provider-neutral comparison;
- bounded Ollama and OpenRouter recording/proposal paths;
- provider readiness, routing, durable attempt-state, and recovery experiments;
- separate benchmark, recording, and OpenRouter CLIs with dedicated tests.

The permanent Windows-hosted laboratory gate passed on the final consolidation head:

- Snow Globe core: **1,186 passed, 5 skipped, 0 failed, 1,191 total**;
- benchmark CLI: **56/56**;
- recording CLI: **94/94**;
- OpenRouter CLI: **104/104**;
- Release build: zero warnings and zero errors.

The five skips are exact byte-pinning checks for operator-retained v4, v5, and v6 evidence files that are not committed to the repository. They do not replace or relax the synthetic schema, tamper, routing, durability, recovery, and Windows filesystem-contract tests, which remain mandatory and passed.

This work is technically meaningful. It remains laboratory infrastructure until a product milestone uses the smallest necessary interface inside the embodied world.

### Cognition integration boundary

The existing `PrototypeCognitionModule` is a narrow strict precursor, not the selected gameplay interface. Its current apply path records an informational event only; it does not validate or execute a citizen proposal, create a commitment, or cause authoritative world change.

No product request/receipt seam yet binds interaction, citizen, tick, state version, state/observation digests, allowed actions, and citizen-known facts. No validated receipt/result persistence or replay-no-call path exists for the accepted scene. The active milestone requires that small product-owned boundary through deterministic and recorded adapters only; provider, CLI, billing, raw-response, and operator-store concerns remain outside product scope.

## Canonical Packet 01 baseline

Evidence: [`snow-globe-social-kernel-packet-01-validation.json`](../../planning/active/evidence/snow-globe-social-kernel-packet-01-validation.json). The ignored raw bundle is `artifacts/performance/accepted-scene-baseline-packet01-canonical-20260830-01/accepted-scene-baseline-v4.json`, SHA-256 `ccbf7c499a84b68e5d9b6a50ac5decee5d808480116cd59b7c2c055387ab5c39`; schema `societies_accepted_scene_baseline_bundle/v4`; status `characterized`; six executions; exit code 0. ExportRelease assembly SHA-256 is `0778ed08acbd729656543291ce6be616cffb29ad2cca3643f2b6ec54acbc1b0d`, on official Godot `4.6.2 stable mono`.

Route: `res://scenes/snow_globe_voxel_foundation.tscn`, `snow-globe-voxel-four-leg-edit-reload-replay/v4`, seed `260827`, 120 warm frames and 300 measured physics intervals in each realtime trial. Three trials ran uncapped realtime for timing evidence; three fixed-60 executions ran the same route for identity evidence only. The route starts at `(0.5,13.550155639648438,0.5)`, visits right at 10, forward at 20, left at 30, and backward at 40 to return exactly to start; minimum leg is `1.0833334922790527m`, followed by 260 neutral physics intervals. Primary route checkpoints are exact across all six executions. Starting, post-route, edited, reload, and replay state identities are exact across the three fixed-60 identity executions.

Metrics are scheduling-inclusive callback-start wall-clock cadence in headless ExportRelease: `process_frame_start_interval_ms` and `physics_frame_start_interval_ms`. They are not CPU, GPU, render-thread, or whole-engine durations.

| Metric | Bundle median p95 | Bundle worst p95 | Assessment |
| --- | ---: | ---: | --- |
| Process cadence | 8.3635 ms | 8.8501 ms | hard-safety pass |
| Physics cadence | 22.7914 ms | 23.109 ms | target miss; hard-safety pass |

Realtime per-trial p95 values are process `8.3635, 8.8501, 8.2454` ms and physics `22.7914, 23.109, 22.6063` ms. Full p50/p95/p99/max and active-p95 values for those three timing trials, plus the three fixed-60 identity-only executions with timing fields explicitly not applicable, are preserved in the evidence JSON. Maxima above 33.33 ms are reported; no max threshold is defined.

All six executions recorded zero citizens/resources/stock/structures/build queue/stress/crisis counts, 64 collision bodies, 12,777 initial collision shapes, and 12,781 after the fixed edit at `(-2,12,-2)`. The three realtime timing trials recorded zero backlog. The +4 shape change is expected; no unexplained collision drift occurred. Instrumentation was excluded.

## Validation and review

- Local focused Packet tests: **12/12**.
- Local manifest fast gate: **399/399**.
- Local complete managed suite: **519/519**, 0 failures in 5m40s; one known generated test-project `CS0436 Main` conflict warning; production assembly clean.
- Local production Release build: 0 warnings, 0 errors.
- Local Godot 4.6.2 build-solutions, wrapper parse, and git diff checks passed.
- Independent deep review: **PASS**, 0 P0/P1/P2 findings.

The final checkpoint was clean, including post-export/trial status and content identity. Earlier shutdown deferred-free and cadence-dependent queued-input route-divergence findings were fixed before this checkpoint; stale manifest executable counts were corrected before final review.

### Packet 01 hosted delivery evidence

- Required [`build-test-smoke`](https://github.com/dfrer/societies/actions/runs/33358362870/job/99384744879) was **SUCCESS**: run `33358362870`, job `99384744879`, started `2026-08-31T04:48:31Z`, completed `2026-08-31T04:52:24Z`, displayed duration `3m53s`.
- That hosted job built the production Release assembly with 0 warnings and 0 errors, passed the fast managed gate **399/399** with manifest count 399, passed Godot headless **28/28** with manifest count 28, and passed project governance. The known generated test-project `CS0436 Main` warning appeared only during the fast test build, consistent with local evidence.
- Required [`lab-tests`](https://github.com/dfrer/societies/actions/runs/33358362846/job/99385290379) was **SUCCESS**: run `33358362846`, final job `99385290379`, started `2026-08-31T04:52:06Z`, completed `2026-08-31T04:52:10Z`, displayed duration `4s`. Its classifier was **SUCCESS** in job `99384744780` (`6s`), and the full relevant Windows lab suite was **SUCCESS** in job `99384766079` (displayed `3m23s`).
- The relevant lab job built core with 0 warnings and 0 errors and passed Snow Globe core **1,186 passed, 5 documented skips, 0 failed, 1,191 total**; benchmark **56/56**; recording **94/94**; OpenRouter **104/104**; and project governance.

## Repository authority: proven

- PR #183 is merged into `master` at `54a4e5c0ea1297438b06e4b40ea14391db343657`.
- Branch-protection repair branch `chore/complete-master-protection-v1` was merged as PR #186 at `420738bfc1b51cffacd94845b4e10cb9c72db081`.
- Branch-protection documentation closeout PR #189 merged at master commit `1eaa1ab6b0c79550a99c9cad68c4ea04e9fdea75`.
- Planning PR #191 merged at master/base `31ea1d6012d6fd932d0bfe0dbc621e668fd58c80` and activated the bounded three-citizen proof in `planning/active/MILESTONE.md`.
- Visual-recovery planning PR #194 merged planning head `52557d0a517b841421aa60a7b7b295375908c35f` as `ccb651c67689c25eb0f3adec78abbfcf870699f5` at `2026-09-02T03:28:09Z`; required `build-test-smoke` job `99934428113` and Snow Globe laboratory detector `99934427058`, suite `99934496859`, and `lab-tests` `99935427335` jobs passed.
- Packet 01 [PR #192](https://github.com/dfrer/societies/pull/192) merged at `2026-08-31T04:52:40Z` as `f471efe5fe5f5b62aef37e48f27bc1fbffd0104e`. Its parents are the exact base `31ea1d6012d6fd932d0bfe0dbc621e668fd58c80` and exact PR head `ea538a506318f99946f858c7c4b50508b65fb44c`.
- The exact accepted pre-consolidation source remains preserved on `archive/pre-consolidation-2026-08-27` at `847c86b1c379e6a1dd8d4b7b641c3c89646e28c9`.
- PR #178 is recognized as merged ancestry; #177 and #179–#182 are closed as superseded with links to #183.
- No pull requests remained open at the consolidation closeout boundary; that historical fact does not imply Packet 01 delivery.
- Historical plans and branches remain recoverable but carry no execution authority.
- Packet 01 implementation `936771285fd1fd2ebb92054dc3630a656a78b941` (tree `2bd4a709a54ae72fde84fc3e24968e5fec49e49b`) is retained as the local evidence identity; PR head `ea538a506318f99946f858c7c4b50508b65fb44c` and merge `f471efe5fe5f5b62aef37e48f27bc1fbffd0104e` are the delivery identities.

### Master governance policy: proven

`master` uses classic branch protection; the repository has no active rulesets. Pull requests are required, with zero required approvals. The required GitHub Actions contexts are exactly `build-test-smoke` and `lab-tests`, both provided by the GitHub Actions app (id `15368`), with strict/up-to-date enforcement enabled. Stale-approval dismissal, code-owner review, and last-push approval are disabled. Admin enforcement is enabled, with no bypass actors or restrictions. Conversation resolution is required. Force pushes and branch deletion are disabled; linear history, signed commits, and lock-branch settings are not enabled.

The repair was evidenced by PR #186 and its merge commit above. Follow-up PR #187 (unrelated `.github/pull_request_template.md`) and PR #188 (harmless `labs/README.md`) both became CLEAN after the required checks, then closed unmerged with their branches deleted; neither changed product scope. #187 recorded `build-test-smoke` pass (6s), Windows skipped, and final `lab-tests` pass (3s; relevant=false, detection succeeded, lab skipped). #188 recorded `build-test-smoke` pass (8s), classifier pass, Windows pass (6m4s; 1,186 passed, 5 documented evidence-only skips, 56 benchmark, 94 recording, 104 offline OpenRouter), and final `lab-tests` pass with relevant=true, detection success, and lab success.

## Not yet proven

- a participating citizen in the accepted player-facing scene;
- a gameplay-facing Snow Globe observation/proposal/communication interface;
- persistent citizen-specific interests, trust, commitments, refusal, and memory experienced in play;
- the wetland causeway negotiation and physical shared consequence;
- meaningful social/ecological variation that invites replay;
- whether live inference improves the experience over recorded and deterministic paths;
- accessibility acceptance;
- representative performance acceptance or release readiness;
- larger economy, governance, multiplayer, long-term memory, or civilization-scale behavior.

No Mara, Ivo, or Sena identity, authoritative causeway situation, player-shelter/dry-timber scenario custody, citizen trust, or commitment domain currently exists. Exactly three citizens are the selected product pass gate; a one-citizen route will be an internal integration checkpoint only.

The selected contract requires the player's own shelter to be exposed and their reserved dry timber to be a keep-or-sacrifice choice observed by citizens. Citizens must observe and respond causally to one another; zero player input must advance through nightfall to the following morning; staged compromise must consume immediate material, player labor, and citizen cooperation plus create a dated restoration/repair obligation. These are planned acceptance requirements, not implemented facts.

## Known red or open gates

- The [EB-01R repository attestation](../../planning/active/evidence/v3-sg-eb-01-validation.json) characterized **64 collision bodies**, **12,777 initial collision shapes**, and **12,781 shapes after an edit**; Packet 01 reproduced those counts and the expected +4 edit delta, while scaling remains unresolved.
- The historical [`51.9392 ms` safety miss](../history/pre-consolidation-2026-08-27/root/CURRENT_BUILD.md) remains context. Packet 01 now supplies a representative local accepted-scene cadence baseline, but its worst physics p95 of **23.109 ms** remains a **16.67 ms target miss** even though it passes the **33.33 ms hard-safety line**. This is characterization, not acceptance.
- The accepted worldcraft scene has no citizens; product progress can stall if more worldcraft or provider infrastructure is added first.
- Packet 02 visual production is an open human gate. Headless evidence cannot accept it. Packet 02A's corrected fresh export completed with a fail-closed v2 attestation, but full-worktree drift stopped execution before all six performance trials; earlier ad-hoc reuse-derived timing is invalid. Packet 02A therefore has no current process/physics classification or GPU/display evidence. Packet 02V must prove one near-final district, provenance, interaction feedback, and displayed frame/physics/GPU baselines against the bounded budgets in the active milestone.
- A narrow scenario can become a scripted quest or player-puppet sequence if citizen knowledge, independent action, refusal, commitment, and no-player routes are not derived from authoritative state.
- A tally, privileged player choice, or magical veto could counterfeit social causality. The milestone permits influence only from role authority, bounded knowledge, material custody, labor availability, obligations, trust, dependency, and current world facts, with perturbation tests for those causes.
- `GameManager`, HUD, diagnostic, voxel, run-store, and provider components have grown large enough to require targeted decomposition when a product slice touches them.
- The repository has extensive stale branches and historical planning. The authority system prevents their use but does not erase Git history.
- Branch protection is proven under the exact policy recorded above; no product or release gate is implied by this administrative closeout.
- The active milestone requires a performance baseline before citizen breadth, a 16.67 ms p95 target, a 33.33 ms p95 hard-safety line, a 10% same-route regression budget, and no unexplained terrain-collision growth. Target misses may not be relabeled; hard-safety breach requires explicit owner disposition.

## Current authorization

The owner approved `SNOW-GLOBE-SOCIAL-KERNEL-V1 — Causeway Before Nightfall` in [D-014](DECISION_LOG.md#d-014--select-the-three-citizen-causeway-before-nightfall-proof); D-016 accepts/selects the visual recovery sequence. PR #194 delivered the amendment and authorizes the revised sequence beginning Packet 02A; each packet is one PR from then-current `master` after its predecessor merges and required gates are reconciled.

Packet 01 implementation, canonical characterization, required hosted checks, protected merge, and delivery reconciliation are complete. Packet 02A is implemented locally on the bounded feature branch and awaits technical review and hosted delivery; Packet 02V follows, while Packet 03 remains unstarted pending the 02V human gate. No historical milestone label is current authority. No live-provider, network, credential, paid, billing, raw-response, operator-store, deployment, publishing, release, or adjacent feature work is authorized.

## Packet 01 delivery status

Packet 01's required `build-test-smoke` and `lab-tests` checks passed under the exact protected-branch policy: those two contexts only, strict/up-to-date enforcement, GitHub Actions app id `15368`. PR #192 then merged with the exact base and head identified above. No deployment, release, accessibility acceptance, or human product acceptance is claimed. Packet 01 required no human product-acceptance gate; the absence of that gate is not an acceptance claim. The 16.67 ms target remains open for a later measured Packet 08 repair; Packet 01's hard-safety pass does not convert it into a target pass. Packet 02A is branch-local and not yet hosted-delivered; Packet 02V remains gated behind that delivery and its own human visual gate.

## Status vocabulary

- **Accepted:** explicitly passed its named human gate.
- **Validated:** passed named mechanical or runtime evidence.
- **Implemented:** code exists; no broader claim.
- **Characterized:** measured, but target not necessarily passed.
- **Historical:** preserved evidence with no execution authority.
- **Deferred:** intentionally outside the current product proof.
