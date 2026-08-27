# Snow Globe Voxel Foundation (SG-VX-01)

## Status

**Technical grounding recovery validated; human retest remains open.** Two user-led attempts failed because the player appeared in or below the terrain and could fall out of the world. Those failures override earlier automated confidence. The repaired candidate is not a claim of human visual, play, performance, or release acceptance.

## Bounded outcome

- One town-sized editable world now; citizen, settlement, and navigation simulation remain outside this foundation.
- The selected world is `voxel_v1`, never a mirrored edit layer over `WorldMapState`.
- World bounds are `[-64,64) x [0,32) x [-64,64)`: 64 eager `16x32x16` chunks.
- Material ids are closed and persisted: Air 0, Soil 1, Stone 2, Wood 3, Bedrock 4.
- `PrototypeRuntimeSession.ExecuteVoxelEdit` is the mutation seam. `GameManager`, player, HUD, presenter, and scene remain intent or presentation adapters.
- Snapshot schema v10 is voxel-only and preserves complete chunk identity, hashes, ordered edit events, and replay. Legacy v5-v9 remain heightfield snapshots and are not converted.

## Grounding and interaction recovery

The dedicated `snow_globe_voxel_foundation.tscn` scene now presents authoritative indexed chunk geometry plus immutable vertical occupied-run projections. `VoxelWorldPresenter` coalesces those runs into exact solid box collision, including legacy gaps and overhangs, instead of treating a ray-hittable hollow mesh or top-only heightmap as sufficient character ground. A baseline world has 64 collision bodies and 12,777 shapes; dirty chunk replacement is bounded and leak-tested.

Generation v2 creates a deterministic `9x9` spawn clearing and selects a strict safe spawn from its central `5x5`. Saved positions are validated from the actual capsule foot, not the body center: a grounded valid save is restored exactly, while a below-surface or unsafe save recovers to the authoritative spawn. Historical generator-v1 identity and event replay remain intact, including a truthful minimum-relief fallback for old worlds.

New edits are surface-bound: removal rejects interior/non-top blocks and placement rejects unsupported floating blocks. Left click and right click still cross `GameManager -> PrototypeRuntimeSession.ExecuteVoxelEdit`; the Godot layer never creates a second voxel state. The dedicated scene no longer renders the unrelated legacy heightfield settlement, resources, or panels. Its compact HUD reports the voxel interaction contract and preserves transient authoritative feedback across scenario-mode transitions.

## Failure evidence and diagnostic isolation

The first user play fell through the map. The second user report found the player apparently under or in the terrain and the settlement apparently below the map. A rendered scene diagnostic confirmed mismatched spawn/collision presentation and legacy-scene leakage. It also exposed a testing defect: an ordinary Godot window could take focus and consume the user's live mouse even when launched offscreen or marked no-focus.

Rendered diagnostics now fail closed unless launched through `run-sg-vx-scene-diagnostic.ps1`. The launcher creates a private Windows desktop, starts Godot on that desktop, places the complete descendant process tree in a kill-on-close Job Object before resume, verifies the active input desktop is different, and records exact source, assembly, log, camera, and encoded-PNG hashes. The runner disables live player and manager input and drives the real `PlayerCharacter` controller with synthetic actions. True headless Godot remains the route for non-rendered tests; it cannot supply renderer pixels.

The final isolated run captured launch, spawn, wide, cutaway, and post-controller/edit views. It proves the tested build placed the player above the clearing, removed legacy settlement presentation, matched rendered/collision surfaces, and completed grounded controller traversal. Screenshot inspection does **not** establish visual quality or human play acceptance; the present terrain and lighting remain a crude technical foundation.

## Validation result

- Focused managed voxel and persistence coverage passes 17/17.
- The authoritative wrapper passes 498/498 managed tests in 6m06s and 28/28 Godot tests with exit 0.
- Release and ExportRelease builds pass with 0 warnings and 0 errors.
- Godot coverage includes strict and legacy-fallback spawn contracts, exact grounded-save restoration, v1 replay/resave identity, vertical-run collision, scenario-scoped HUD/culling, a 16-cycle dirty-edit leak soak, finite-edge inward traversal, and landing that requires `IsOnFloor`, settled velocity, and foot-to-surface agreement.
- Independent deep review of the final provenance-bound diagnostic and implementation returns GO with no P0-P3 findings.

## Performance safety

This is characterization, not representative performance acceptance. The authoritative run measured startup at 946.293 ms and the 16-cycle dirty-edit soak at 720.162 ms, with 64 bodies, 12,777 baseline shapes, and 12,779 maximum shapes. The isolated rendered diagnostic took about 13.2 seconds. The approximately 12.8k collision-shape baseline remains a scaling risk, and the predecessor 51.9392 ms safety failure is still unresolved and unattributed.

## Citizen boundary and non-goals

Do not introduce fixed citizen personality or goal-template enums. Future citizens may use seed-derived identity and circumstances plus continuous situational state; an LLM may propose bounded deterministic capabilities, but replay never recalls a model and deterministic fallback remains mandatory.

No citizen/settlement integration, live providers, fluids, caves, infinite streaming, LOD, networking, multiplayer, ecology expansion, markets, broad governance, final art, or generalized voxel framework is included here.

## Remaining gate and one next action

Implementation commit `9a0edaa1d8066431c7869f011c5fc7aaab12fc0c` is the repaired technical candidate on PR #180. The user should retest the dedicated scene and explicitly confirm or reject: spawn above terrain, no ordinary fall-through, collision/render agreement, absence of the legacy settlement, stable camera/input behavior, and basic remove/place readability. Do not merge or begin citizen/world participation work until that result is recorded.
