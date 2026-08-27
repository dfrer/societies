# Snow Globe Voxel Foundation (SG-VX-01)

## Status

**Implemented local candidate; delivery and human acceptance remain open.** This authorized reset replaces the old exploratory voxel spike with one finite, deterministic, editable world authority. It is not a claim of human visual, play, performance, or release acceptance.

## Bounded outcome

- One town-sized editable world now; citizen, settlement, and navigation simulation are intentionally absent from this foundation. The architecture may host up to three town sites later.
- The selected world is `voxel_v1`, never a mirrored edit layer over `WorldMapState`.
- World bounds are `[-64,64) x [0,32) x [-64,64)`: 64 eager `16x32x16` chunks.
- Material ids are closed and persisted: Air 0, Soil 1, Stone 2, Wood 3, Bedrock 4.
- `PrototypeRuntimeSession.ExecuteVoxelEdit` is the mutation seam. A command has actor, tick, expected revision, expected material, edit kind, coordinate, and after material. Failure is inert; success emits one typed change and increments revision once.
- Snapshot schema v10 is only for voxel scenarios. It retains the complete chunk payload, identities, bounds, revisions, per-chunk hashes, and root hash. Legacy v5-v9 remain heightfield snapshots and are not converted.

## Player evidence implemented

The dedicated `snow_globe_voxel_foundation.tscn` scene presents engine-neutral indexed chunk geometry and trimesh collision from immutable projections. Left-click raycast intent removes a hit block and right-click places Wood on the hit face. Both paths cross `GameManager` into `PrototypeRuntimeSession.ExecuteVoxelEdit`; Godot never owns voxel state. The Godot smoke covers scene startup, outside collision, remove/place, Wood material, mesh normals/material, and voxel/heightfield lifecycle. Managed tests cover chunk-edge dirtiness, save/restore/replay identity, malformed-state rejection, exact 10,000-edit persistence capacity, and inert rejection of edit 10,001.

## Citizen boundary

Do not introduce `PersonalityArchetype` or fixed `GoalTemplate` enums. Future citizens use seed-derived identity/circumstances and continuous situational state. An LLM may create an open-ended persisted goal receipt; it only proposes a bounded deterministic capability. Session validation accepts or rejects it, records the normalized receipt/event, and replay never recalls a model. Deterministic survival/work fallback continues when no model is available.

## Deliberate non-goals

No fluids, caves, infinite streaming, LOD, networking, multiplayer, live providers, ecology expansion, market/governance expansion, legacy save conversion, or final art are included.

## Design references

- Microsoft’s [world-generation overview](https://learn.microsoft.com/en-us/minecraft/creator/documents/world-generation?view=minecraft-bedrock-stable) informs explicit seed-bound staged passes.
- Eco documents finite configured seeded worlds and simulation feedback: [WorldGenerator](https://wiki.play.eco/en/Server_Configuration/WorldGenerator.eco), [EcoSim](https://wiki.play.eco/en/Server_Configuration/EcoSim.eco), and [Pollution](https://wiki.play.eco/en/Pollution).
- Godot’s [SurfaceTool](https://docs.godotengine.org/en/stable/tutorials/3d/procedural_geometry/surfacetool.html) and [ArrayMesh](https://docs.godotengine.org/en/stable/classes/class_arraymesh.html) describe the planned CPU indexed mesh path.

## Architecture result

The registered `snow_globe_voxel` scenario selects one immutable world model at session construction. Voxel initialization does not generate a heightfield or create the legacy resource ledger, settlement simulation, or navigation simulation. A small read-only terrain-query adapter supplies surface height, spawn, and immutable world identity to existing presentation call sites without becoming a second authority. Legacy scenarios continue through the unchanged heightfield adapter.

Schema v10 is voxel-only. It binds scenario/outer/nested seed and world identity, canonical culture-invariant hashes, ordered authoritative edit ticks, exact revision/event counts, the zero-heightfield shell, chunk payload segments, and the full typed edit journal. Public deserialize, artifact preflight, and session apply share the same validation before any mutation or write. V5-v9 remain the frozen heightfield compatibility path and are not converted.

## Remaining gates and next slice

- Final authoritative wrapper, Release, and ExportRelease validation plus Git/PR delivery remain to be recorded in evidence.
- The scene is a technical interaction proof, not an accepted visual direction or a complete game loop. Human visual/play acceptance is open.
- Representative frame-time, edit latency, chunk rebuild, memory, and save-size performance were not measured. The predecessor `51.9392 ms` safety failure remains unresolved and cannot be attributed to this slice.
- Citizen, settlement, resource, ecology, and navigation participation are absent. The next slice should design the deterministic citizen/world participation contract over voxel standability before adding open-ended LLM goals. Do not treat the current top-solid terrain query as that navigation contract.
