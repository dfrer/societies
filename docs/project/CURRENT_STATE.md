# Societies Current State

- **Status date:** 2026-08-28
- **Accepted runtime source:** `847c86b1c379e6a1dd8d4b7b641c3c89646e28c9`
- **Preservation branch:** `archive/pre-consolidation-2026-08-27`
- **Consolidation integration record:** PR #183 at `54a4e5c0ea1297438b06e4b40ea14391db343657`
- **Governance workflow integration:** PR #186 at `420738bfc1b51cffacd94845b4e10cb9c72db081`
- **Branch-protection documentation closeout:** PR #189 at master commit `1eaa1ab6b0c79550a99c9cad68c4ea04e9fdea75`
- **Active milestone:** `planning/active/MILESTONE.md`

This document reports what is proven, not everything planned or implemented somewhere in repository history.

## Executive assessment

Societies has a substantial deterministic foundation and a user-accepted bounded founder-worldcraft experience. Snow Globe is making materially better, more unified progress than the fragmented pre-V3 period. The project has crossed from speculative architecture into a credible product base.

It has **not** yet proven its central thesis. The accepted player-facing scene still contains zero participating citizens, and the isolated cognition laboratory has not yet become an embodied social interaction inside the authoritative Godot world. The next product milestone must close that gap without allowing performance risk or infrastructure expansion to take over the roadmap.

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

## Repository authority: proven

- PR #183 is merged into `master` at `54a4e5c0ea1297438b06e4b40ea14391db343657`.
- Branch-protection repair branch `chore/complete-master-protection-v1` was merged as PR #186 at `420738bfc1b51cffacd94845b4e10cb9c72db081`.
- Branch-protection documentation closeout PR #189 merged at master commit `1eaa1ab6b0c79550a99c9cad68c4ea04e9fdea75`.
- The exact accepted pre-consolidation source remains preserved on `archive/pre-consolidation-2026-08-27` at `847c86b1c379e6a1dd8d4b7b641c3c89646e28c9`.
- PR #178 is recognized as merged ancestry; #177 and #179–#182 are closed as superseded with links to #183.
- No pull requests remain open after consolidation closeout.
- Historical plans and branches remain recoverable but carry no execution authority.
- No next product feature is authorized until the owner-led planning discussion selects a bounded proof.

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

## Known red or open gates

- The voxel presentation begins with roughly 12.8k collision shapes; scaling is unresolved.
- A historical canonical run recorded median p95 `51.9392 ms` against a `50 ms` safety line. Later performance evidence is mixed and must be reconciled for the accepted scene.
- The accepted worldcraft scene has no citizens; product progress can stall if more worldcraft or provider infrastructure is added first.
- `GameManager`, HUD, diagnostic, voxel, run-store, and provider components have grown large enough to require targeted decomposition when a product slice touches them.
- The repository has extensive stale branches and historical planning. The authority system prevents their use but does not erase Git history.
- Branch protection is now proven under the exact policy recorded above; no product or release gate is implied by this administrative closeout.
- The active milestone remains in feature-freeze closeout until the owner selects the next product proof.

## Current authorization

The consolidation record does **not** authorize another feature automatically. The owner and planning agent must choose the next smallest integrated product proof from current state, risks, and the charter. No label from the former F/EB/W sequences reactivates itself.

## Status vocabulary

- **Accepted:** explicitly passed its named human gate.
- **Validated:** passed named mechanical or runtime evidence.
- **Implemented:** code exists; no broader claim.
- **Characterized:** measured, but target not necessarily passed.
- **Historical:** preserved evidence with no execution authority.
- **Deferred:** intentionally outside the current product proof.
