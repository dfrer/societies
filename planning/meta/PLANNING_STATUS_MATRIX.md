# Planning Status Matrix

> Authoritative as of: 2026-04-06
> Purpose: Map planning documents against current implementation reality to prevent future agents from drifting into stale planning fiction.

## How to Read This Matrix

- **Implemented** — code exists and is exercised by tests or live gameplay
- **Partial/Spike** — experimental or incomplete code exists
- **Planned** — described in planning docs but zero implementation code
- **Obsolete** — planning document references systems that no longer exist or were fundamentally redesigned

## Session-by-Session Status

### Session 1: Technical Architecture

| Doc | Status | Notes |
|-----|--------|-------|
| `01-architecture-overview.md` | Partial | Describes multiplayer architecture; current build is single-player local only |
| `02-client-server-architecture.md` | Planned | No networking code beyond `NetworkManager.StartLocalSession()` |
| `03-data-persistence.md` | Partial | Local JSON snapshots implemented; no PostgreSQL, event sourcing, or backend persistence |
| `06-risk-management.md` | Partial | Some risk items addressed (bounded frontier, uncapped comparison) |
| `08-network-monitoring.md` | Planned | No network monitoring — no networking yet |
| `09-rpc-protocol.md` | Planned | No RPC |
| `10-event-sourcing.md` | Planned | Event log is a simple append list, not event sourcing |
| `12-security-spec.md` | Planned | No security concerns in local prototype |
| `13-voxel-world-system.md` | Partial/Spike | Experimental voxel spike exists; not the runtime world |
| `14-terrain-generation.md` | Implemented | Heightfield terrain with biome/buildability/movement cost is the current runtime |
| `15-terrain-modification.md` | Partial | Voxel spike supports edits; heightfield does not |
| `16-world-persistence.md` | Partial | Local JSON only |
| `17-rendering-meshing.md` | Partial/Spike | Voxel spike includes chunk meshing; heightfield uses Godot terrain |
| `18-physics-collision.md` | Partial | Basic Godot physics for player; agents do not use physics |

### Session 2: AI System Design
| Doc | Status | Notes |
|-----|--------|-------|
| GOAP/Utility AI docs | Planned | Current AI is a simple role-based work order system |

### Session 3: Core Gameplay Loops
| Doc | Status | Notes |
|-----|--------|-------|
| Core loops (harvesting, building, crafting) | Implemented | All present and tested |
| UI/UX paths | Implemented | HUD presenter + text builder with overlays |

### Session 4: Progression and Balance
| Doc | Status | Notes |
|-----|--------|-------|
| Progression systems | Planned | No progression/rank/skill systems |
| World resources | Implemented | Resource clusters, caches, hauling |

### Session 5: Governance Mechanics
| Doc | Status | Notes |
|-----|--------|-------|
| All governance docs | Planned | Zero governance implementation |

### Session 6: Prototyping Roadmap
| Doc | Status | Notes |
|-----|--------|-------|
| `day6-prototyping-roadmap.md` | **STALE** | References systems from Sessions 1-2 that are not implemented. Treat as aspirational only. |
| Voxel-first roadmap | **STALE** | Voxel is an experimental spike; heightfield is the runtime authority |

### Session 7: Integration Master Plan
| Doc | Status | Notes |
|-----|--------|-------|
| `day7-master-development-plan.md` | **STALE** | Describes multiplayer integration, ENet, backend persistence; none implemented |

## What IS Implemented (V2 M3)

- Deterministic local session bootstrap
- Heightfield terrain with biome/buildability/movement-cost overlays
- First-person movement and harvesting
- Observer camera and runtime overlay cycling
- Seeded scenario world generation
- Citizen-based settlement simulation with food, fatigue, beds, hearth, build queue
- Terrain-aware route planning, path corridors, remote depots, logistics metrics
- Local JSON snapshot + event-log + run-summary output
- V2 artifact exports including world summary and metrics CSV
- Headless .NET + Godot validation coverage
- Experimental voxel spike (chunking, edits, meshing, persistence, walkability)
- Bounded work-order frontier (max(50, workers * 5))
- Uncapped comparison mode for testing
- Settlement workload diagnostics counters
- Settlement classification (Stable/Strained/Collapsed)
- Extended characterization tests separated from PR gate
- Runtime frame/tick performance metrics (this branch)

## What IS NOT Implemented

- Multiplayer networking (ENet deferred)
- Authoritative server architecture
- GOAP/Utility AI
- Governance/law systems
- Market/economy systems
- Trade systems
- Combat
- Social simulation
- Voxel as gameplay world (spike only)
- Production backend persistence (PostgreSQL, event sourcing)
- Pathfinding-based collision avoidance / crowd simulation
