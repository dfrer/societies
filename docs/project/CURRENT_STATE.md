# Societies Current State

- **Status date:** 2026-08-28
- **Accepted runtime source:** `847c86b1c379e6a1dd8d4b7b641c3c89646e28c9`
- **Preservation branch:** `archive/pre-consolidation-2026-08-27`
- **Consolidation integration record:** PR #183 at `54a4e5c0ea1297438b06e4b40ea14391db343657`
- **Governance workflow integration:** PR #186 at `420738bfc1b51cffacd94845b4e10cb9c72db081`
- **Branch-protection documentation closeout:** PR #189 at master commit `1eaa1ab6b0c79550a99c9cad68c4ea04e9fdea75`
- **Active milestone:** [`SNOW-GLOBE-SOCIAL-KERNEL-V1 — Causeway Before Nightfall`](../../planning/active/MILESTONE.md), effective when its planning PR merges

This document reports what is proven, not everything planned or implemented somewhere in repository history.

## Executive assessment

Societies has a substantial deterministic foundation and a user-accepted bounded founder-worldcraft experience. Snow Globe is making materially better, more unified progress than the fragmented pre-V3 period. The project has crossed from speculative architecture into a credible product base.

It has **not** yet proven its central thesis. The accepted player-facing scene still contains zero participating citizens, and the isolated cognition laboratory has not yet become an embodied social interaction inside the authoritative Godot world. The owner has selected **Causeway Before Nightfall** as the next bounded proof: exactly Mara, Ivo, and Sena must participate in one deterministic material, ecological, and social consequence. This selection authorizes no implementation until the planning PR merges.

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

The acceptance was explicitly recorded on 2026-08-27. Numeric per-axis scores were not supplied and are not inferred.

The accepted voxel scenario itself remains socially empty: it declares `initialCitizens: 0`, and current catalog validation forbids citizens, resources, stock, and crisis state in voxel scenarios. Legacy heightfield worker, civic, wetland, and crisis systems remain reusable infrastructure and regression harnesses, not accepted social UX or an alternate current product route.

## Hosted product evidence

The integrated baseline was validated on clean GitHub-hosted runners before merge:

- authoritative Godot project Release build passed with zero warnings and zero errors;
- the complete managed suite passed **507/507** with zero failures;
- the permanent pull-request fast tier passed **387/387** and verified the manifest count;
- the Godot headless suite passed **28/28** and verified the manifest count;
- the Godot C# solution build passed;
- project-governance and patch-whitespace validation passed.

`tests/test-manifest.json` declares the discovered **507 managed / 387 fast / 28 Godot** counts. The pull-request and weekly-full workflows fail when discovered counts drift from that authority instead of silently accepting missing tests.

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

## Repository authority: proven

- PR #183 is merged into `master` at `54a4e5c0ea1297438b06e4b40ea14391db343657`.
- Branch-protection repair branch `chore/complete-master-protection-v1` was merged as PR #186 at `420738bfc1b51cffacd94845b4e10cb9c72db081`.
- Branch-protection documentation closeout PR #189 merged at master commit `1eaa1ab6b0c79550a99c9cad68c4ea04e9fdea75`.
- The exact accepted pre-consolidation source remains preserved on `archive/pre-consolidation-2026-08-27` at `847c86b1c379e6a1dd8d4b7b641c3c89646e28c9`.
- PR #178 is recognized as merged ancestry; #177 and #179–#182 are closed as superseded with links to #183.
- No pull requests remained open at the consolidation closeout boundary; the new planning delivery is tracked separately from that historical fact.
- Historical plans and branches remain recoverable but carry no execution authority.
- The owner has selected the bounded three-citizen proof in `planning/active/MILESTONE.md`; implementation authorization begins only when the planning PR merges and then proceeds one ordered packet/PR at a time.

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

- Accepted-scene characterization records **64 collision bodies**, **12,777 initial collision shapes**, and **12,781 shapes after an edit**; scaling is unresolved.
- A historical `51.9392 ms` safety miss remains unresolved. There is no representative accepted-scene frame/physics timing baseline for the fixed Causeway route, so existing figures are characterization only.
- The accepted worldcraft scene has no citizens; product progress can stall if more worldcraft or provider infrastructure is added first.
- A narrow scenario can become a scripted quest or player-puppet sequence if citizen knowledge, independent action, refusal, commitment, and no-player routes are not derived from authoritative state.
- A tally, privileged player choice, or magical veto could counterfeit social causality. The milestone permits influence only from role authority, bounded knowledge, material custody, labor availability, obligations, trust, dependency, and current world facts, with perturbation tests for those causes.
- `GameManager`, HUD, diagnostic, voxel, run-store, and provider components have grown large enough to require targeted decomposition when a product slice touches them.
- The repository has extensive stale branches and historical planning. The authority system prevents their use but does not erase Git history.
- Branch protection is now proven under the exact policy recorded above; no product or release gate is implied by this administrative closeout.
- The active milestone requires a performance baseline before citizen breadth, a 16.67 ms p95 target, a 33.33 ms p95 hard-safety line, a 10% same-route regression budget, and no unexplained terrain-collision growth. Target misses may not be relabeled; hard-safety breach requires explicit owner disposition.

## Current authorization

The owner has approved `SNOW-GLOBE-SOCIAL-KERNEL-V1 — Causeway Before Nightfall`. The planning branch remains documentation-only and authorizes no implementation while unmerged. Once its planning PR merges, authorization is limited to the milestone's nine ordered packets, beginning with packet 01; each packet is one PR from the then-current `master` after its predecessor merges.

No historical milestone label is current authority. No live-provider, network, credential, paid, billing, raw-response, operator-store, deployment, publishing, release, or adjacent feature work is authorized.

## Status vocabulary

- **Accepted:** explicitly passed its named human gate.
- **Validated:** passed named mechanical or runtime evidence.
- **Implemented:** code exists; no broader claim.
- **Characterized:** measured, but target not necessarily passed.
- **Historical:** preserved evidence with no execution authority.
- **Deferred:** intentionally outside the current product proof.
