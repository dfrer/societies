# Societies Current State

**Status date:** 2026-08-27  
**Accepted runtime source:** `847c86b1c379e6a1dd8d4b7b641c3c89646e28c9`  
**Preservation branch:** `archive/pre-consolidation-2026-08-27`  
**Current integration path:** `chore/project-consolidation-v1` to `master`  
**Active milestone:** `planning/active/MILESTONE.md`

This document reports what is proven, not everything planned or implemented somewhere in repository history.

## Executive assessment

Societies now has a substantial deterministic foundation and a user-accepted bounded founder-worldcraft experience. Snow Globe is making materially better, more unified progress than the fragmented pre-V3 period. The project has crossed from speculative architecture into a credible product base.

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

## Recorded technical evidence

The final agent handoff for the accepted source reports:

- 507/507 managed tests passing;
- 28/28 Godot tests passing;
- Release and ExportRelease builds with zero warnings and zero errors;
- private-desktop diagnostic capture at 1280×720 and 1920×1080;
- independent review with no P0–P3 findings.

These are recorded source-branch facts. The consolidation PR must run hosted checks again before integration. The test manifest still declares older expected counts and therefore requires reconciliation rather than silent trust.

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

This work is technically meaningful. It remains laboratory infrastructure until a product milestone uses the smallest necessary interface inside the embodied world.

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
- `master` does not contain the accepted stack until the consolidation PR is integrated.
- The old stacked PRs remain historical delivery paths until closed as superseded.
- The repository has extensive stale branches and historical planning; the new authority system prevents their use but does not erase Git history.
- No repository ruleset is enabled. Issue [#184](https://github.com/dfrer/societies/issues/184) records the manual branch-protection configuration that the connected integration cannot perform.

## Current authorization

Only repository consolidation and explicit next-milestone selection are authorized. No feature label from the old F/EB/W sequence activates itself. After consolidation, the owner and planning agent must choose the next smallest product proof, with a recommendation grounded in current risks and the charter.

## Status vocabulary

- **Accepted:** explicitly passed its named human gate.
- **Validated:** passed named mechanical or runtime evidence.
- **Implemented:** code exists; no broader claim.
- **Characterized:** measured, but target not necessarily passed.
- **Historical:** preserved evidence with no execution authority.
- **Deferred:** intentionally outside the current product proof.
