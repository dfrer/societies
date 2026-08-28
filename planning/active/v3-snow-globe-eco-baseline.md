# Snow Globe Eco-Like Development Baseline

## Status

**Active recovery program; no baseline acceptance is claimed.** The existing SG-VX-01 scene is a validated terrain/collision test, but the user has rejected it as a development baseline because the world is visually flat and repetitive, the HUD/UI is unacceptable, and player inventory, construction, and interaction are too rudimentary and restrictive.

### 2026-08-27 EB-01R replacement result

EB-01R is a technically validated replacement candidate and EB-02 remains blocked pending user acceptance. The authoritative gather, bounded inventory, modular floor/wall/post construction, dismantle, save/load, schema-v10 migration, and schema-v11 replay are unchanged. The replacement adds an eight-slot tactile tool belt and pack, three build cards, reliable focused-GUI Tab/Escape handling, separate gather/build reach, explicit gather/place/dismantle feedback, and strong green-valid/red-invalid placement projection. Independent review is GO with no remaining P0-P3 findings; the authoritative wrapper passes 507/507 managed and 28/28 Godot tests; Release and ExportRelease builds have zero warnings/errors.

Private-desktop r6 diagnostics pass at 1280x720 and 1920x1080 with 13 hash-bound captures each. Inspected frames show a centered non-overlapping pack, readable belt/build hierarchy, distinct valid/invalid previews with reasons, and committed floor/wall/post silhouettes on intact terrain support. These captures supersede the rejected 960x540 presentation evidence for this replacement candidate, but they do not substitute for human representative play. See [EB-01 validation evidence](evidence/v3-sg-eb-01-validation.json).

The next action is user-led play through the established launcher and explicit five-axis scoring. Do not add EB-02 tools/storage/workstations until every EB-01 score is at least 4/5.

This program replaces “add another prototype feature” with a coherent miniature-society foundation. The Snow Globe remains a bounded version of Societies—not a separate simplified game—and must eventually exercise the same deterministic human/citizen action model and provider-neutral cognition seams at smaller scale.

## Baseline definition

The baseline is good enough to begin wider Societies development only when a player can spend 15–20 minutes in one contained world and naturally:

1. orient in a visually readable landscape with shadows, atmosphere, differentiated materials, and authored points of interest;
2. move, look, target, harvest, and receive tactile success/rejection feedback;
3. collect deterministic resources into an authoritative slotted inventory with a persistent hotbar;
4. enter a deliberate building mode, select a modular piece, see cost and placement validity, rotate it, place it, and remove or recover it;
5. use a restrained HUD, a legible inventory screen, and a build catalog without exposing debug state;
6. save, load, and replay world, inventory, and construction state exactly; and
7. understand what to do next without a scripted rail or wall of instructions.

This is an extremely basic Eco-like foundation, not final art, a complete economy, or a complete settlement simulation.

## Product principles

- **Embodied resident-founder:** the player acts through the same bounded world capabilities future citizens use. No admin painting or UI-only state mutation.
- **World first:** graphics, material identity, lighting, placement, and interaction feedback must make the space understandable before social complexity expands.
- **Deep worldcraft Module:** a small session interface hides harvest rewards, inventory rules, build catalog, placement validation, costs, placed-piece state, events, persistence, and replay.
- **Presentation adapters:** Godot meshes, ghost previews, hotbar selection visuals, animation, sound, and HUD layout present authoritative results but never duplicate simulation state.
- **Broad enough to build on:** the baseline must support later storage, crafting stations, property, citizen work, commitments, and LLM-proposed actions without replacing the human path.
- **Human gate:** screenshots, headless checks, and automated input cannot accept visual quality, interaction feel, HUD hierarchy, or the desire to continue.

## Baseline milestones

### EB-01 — Founder Worldcraft Vertical Slice

Deliver one complete gather-to-build loop on the finite voxel world:

- visually lit terrain with directional shadows, ambient depth, fog/sky treatment, and clearly different soil, grass-top, stone, and wood surfaces;
- a composed spawn clearing with surrounding resource silhouettes and terrain variation;
- voxel harvesting that atomically changes the world and grants the corresponding authoritative inventory item;
- fixed inventory slots, stack limits/capacity, item display metadata, and an always-visible hotbar;
- build mode with at least floor, wall, and post pieces; 90-degree rotation; valid/invalid ghost; cost preview; overlap/support/range validation; placement and dismantle results;
- a new voxel-mode HUD hierarchy: crosshair/context, concise feedback, hotbar, build strip, optional inventory, optional diagnostics;
- schema-versioned save/load/replay for inventory and placed construction while preserving schema-v10 historical compatibility;
- keyboard and pointer parity sufficient for manual play.

**Exit gate:** the user can gather enough material, construct a recognizable small shelter/platform, save/load it, and rates world readability, HUD hierarchy, inventory usability, construction clarity, and interaction feel at least 4/5 individually.

### EB-02 — Tools, Storage, and Workstations

After EB-01 human acceptance:

- tool roles and durability/energy only where they create a meaningful choice;
- placeable storage with bounded transfer UI;
- one workbench and a small recipe catalog;
- material recovery and clear ownership of carried versus stored resources;
- construction repair/upgrade rather than an unbounded crafting tree.

**Exit gate:** the player can establish a functional work area and understands every resource transition without diagnostics.

### EB-03 — Living Settlement Foundation

After the player worldcraft interface is stable:

- reintroduce a tiny settlement through the same voxel standability, inventory, storage, workstation, and construction commands;
- three autonomous citizens with deterministic survival/work fallback, seed-derived circumstances, and no fixed personality/goal-template enum;
- visible work, needs, disagreement, commitments, and refusal;
- provider-neutral cognition observations/proposals plus recorded-response and offline adapters; no live-provider dependency for simulation continuation.

**Exit gate:** citizens visibly use the same world rather than appearing as scripted markers, and the user can influence but not directly command every outcome.

### EB-04 — Baseline Hardening and Acceptance

- resolve collision-shape scaling and the predecessor 51.9392 ms performance failure;
- add bounded audio, interaction animation, accessibility, rebinding, and resolution coverage;
- run exact replay/resume, warning-free production builds, authoritative validation, representative performance evidence, and user-led play acceptance;
- record a go, narrow-rework, or stop decision.

**Exit gate:** the user explicitly accepts the build as the foundation from which normal feature development can begin. Until then, “baseline complete,” “playable,” “polished,” and “ready” remain false.

## EB-01 architecture contract

### Authoritative interface

`PrototypeRuntimeSession` remains the external seam. It exposes no Godot types beyond the already persisted player position and offers three worldcraft capabilities:

1. harvest a targeted exposed voxel and return an authoritative result including world change and inventory delta;
2. evaluate a prospective modular piece placement without mutation; and
3. place or dismantle a modular piece atomically and return authoritative cost/state changes.

A private in-process worldcraft Module hides item/build catalogs, slot/stack constraints, geometry footprints, support/overlap/range rules, sequencing, event formation, persistence, and replay. Tests exercise the session interface, not its internal implementation.

### Presentation interface

`GameManager` translates player intent into session commands. The voxel world, building presenter, player, inventory/build UI, lighting, audio, and diagnostic runner are adapters. A ghost preview may cache the current non-authoritative intent/result for a frame, but must never invent inventory, placement, or cost state.

### Persistence policy

- New EB-01 captures use a new voxel runtime schema.
- Strict schema-v10 artifacts remain readable as historical terrain-only worlds and migrate to an empty construction state without rewriting their voxel identity or event history.
- Inventory and placed pieces are canonical, bounded, validated before session mutation, and replay-equivalent.
- Build selection, open panels, hover state, animation, and camera pose are presentation state unless already part of the established player-position contract.

## EB-01 acceptance route

1. Spawn above the authored clearing; inspect shadows, material differences, horizon, and nearby resources.
2. Harvest wood and stone; confirm world change, feedback, hotbar count, and inventory slots.
3. Open inventory and select a building piece from the build catalog.
4. Preview a floor, rotate and place a wall, observe invalid overlap/unsupported/range feedback, then place a post.
5. Dismantle one piece and confirm authoritative material recovery.
6. Save, make additional changes, load, and confirm exact inventory/build/world restoration.
7. Play freely for several minutes and score the five EB-01 product gates.

## Non-goals until the baseline passes

No live provider calls, credentials, paid traffic, broad LLM UI, markets, governance expansion, multiplayer, combat, infinite streaming, fluids, caves, large biome breadth, generalized quest framework, full technology tree, or final asset production.

## Delivery boundary and next action

EB-01R work is isolated on `codex/snow-globe-eb01-ui-replacement`, based on exact PR #181 head `29021b221abc99ff0e5d35b0df067eec1d2422cc`. Implementation commit `00024cd67c8d889fd2e448cd95b45fc580253198` is pushed in stacked PR #182 against `codex/snow-globe-eco-baseline`. EB-01 remains the only active implementation milestone. Merge, hosted CI, performance acceptance, and all five human product scores remain open; do not begin EB-02 before acceptance.
