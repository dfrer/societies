# Societies — Repository Reorganization & Improvement Plan

> **Scope**: Planning-only output. No files are modified by this document.
> **Evidence standard**: Every finding cites a specific file and line/content observed.

---

## Phase 2: Structured Audit

---

### 2A — Directory & File Structure

**Is the folder hierarchy logical?**
Mostly yes at the top level (`src/`, `tests/`, `planning/`, `scripts/`), but there are significant structural problems inside `planning/`.

**Problem: `planning/week1-deep-planning/` still exists.**
`planning/week1-deep-planning/day4-progression-and-balance.md` (13,770 bytes) remains as a live file. This directory was supposed to be archived per `planning/archives/REORGANIZATION-REPORT.md` (line 43: "Phase 3: Week1 Archived"). The session-4 folder already has its own `day4-progression-and-balance.md` (31,693 bytes). There are now **two files with the same name and overlapping content**. The week1 copy (line 576: "TEMPLATE - Ready for Day 4 Planning") predates all session work.

**Problem: `(deepprojectplanning)/` at root with parentheses in name.**
`(deepprojectplanning)/Societies_Comprehensive_Breakdown.pdf` uses parentheses and PascalCase with underscores, violating the kebab-case convention established in `planning/[DIRECTORY-REORGANIZATION]-REPORT.md` (line 31). No file references this directory — it is entirely orphaned.

**Problem: `planning/archives/` contains two separate reorganization reports.**
`planning/archives/REORGANIZATION-REPORT.md` (10,014 bytes) and `planning/[DIRECTORY-REORGANIZATION]-REPORT.md` (9,501 bytes) describe different reorganization passes but are in different locations. `planning/meta/[META-INDEX].md` (line 20) links to only one of them.

**Problem: `.opencode/` directory in repo root.**
`.opencode/` contains `.gitignore`, `bun.lock`, `package.json`, and `plans/MASTER-IMPLEMENTATION-PLAN.md`. The planning document inside belongs in the planning tree. The Node tooling (`bun.lock`, `package.json`) is unrelated to the Godot/C# project and unexplained. This directory is invisible from all planning indices.

**Problem: `.claude/settings.local.json` is committed.**
`.claude/settings.local.json` (152 bytes) is a local IDE configuration file. The `.gitignore` does not exclude `.claude/`.

**Problem: Session master index references renamed files.**
`planning/sessions/[AGENTS-READ-FIRST]-index.md` (lines 271, 288) still lists `weight-carrying-system.md` and `comprehensive-entity-catalog.md` by their old names. Per `[DIRECTORY-REORGANIZATION]-REPORT.md` (lines 57-58), these were renamed to `03-weight-carrying-system.md` and `04-entity-catalog.md`. The actual files match the new names; the index is stale.

**Problem: `nul` file at repo root.**
A file named `nul` (0 bytes, no extension) exists at the repo root. This is a Windows null-device artifact from an accidentally redirected command. It is garbage.

**Problem: `tests/Societies.Core.Tests/Multiplayer/` is empty.**
No test files exist in this directory despite `NetworkManager.cs` and `PlayerSession.cs` existing in `src/`. An empty test directory with no explanation is misleading.

**Naming inconsistencies:**
- `[DIRECTORY-REORGANIZATION]-REPORT.md` — brackets signal navigation hubs by convention, but this is a report, not an index
- `RESEARCH-INDEX.md` (in each session) vs `[MASTER-RESEARCH-INDEX].md` (in research/) — inconsistent capitalization for the same concept
- `SESSION-3-HANDOFF.md` in session-2 uses SCREAMING_SNAKE; all other session documents use kebab-case
- `08-network-monitoring.md` described as "empty placeholder" in the reorg report but has 568 bytes — misleading status

---

### 2B — Documentation Quality & Completeness

**Stale/superseded files:**

1. **`planning/week1-deep-planning/day4-progression-and-balance.md`** — Marked "TEMPLATE - Ready for Day 4 Planning" (line 576). Should be in archives per the reorganization report but remains live.

2. **`planning/archives/week1-templates/day2-ai-system-design.md`** (32,060 bytes) vs. `planning/archives/session-2-ai-backups/day2-ai-system-design.legacy.md` (409,875 bytes) — Two different files, one an early template and one a full legacy backup, both in different archive directories with no cross-reference. The 409KB legacy backup contains content not present anywhere in the active planning tree.

3. **`planning/sessions/[AGENTS-READ-FIRST]-index.md`** (lines 254-258) lists `VERIFICATION-PROMPT.md` and `VERIFICATION-REPORT.md` under Session 2. These files do not exist anywhere in the repo. These are **broken references**.

4. **`planning/sessions/[AGENTS-READ-FIRST]-index.md`** (lines 270-292) still describes `day3-core-gameplay-loops.md` as a file in session-3. That file was renamed to `00-day3-legacy.md` and moved to `planning/archives/` per the reorg report (line 62).

5. **`SESSION-3-HANDOFF.md` (line 167)** references `day2-ai-system-design.md` as "Primary Document" but that file no longer exists in session-2 — it's at `planning/archives/session-2-ai-backups/day2-ai-system-design.legacy.md`. **Broken reference.**

6. **`.opencode/plans/MASTER-IMPLEMENTATION-PLAN.md`** — describes Sessions 1-3 gap-filling tasks (lines 578-609 list 16 new documents) that have already been completed. The plan says "Status: Ready for Execution" (line 615) but describes work already done. It's a passed milestone, not labeled as such.

**Planning docs mixed with reference docs in `planning/meta/`:**
`planning/meta/` serves as reference documentation (`technical-constants.md`, `system-integration-map.md`) but also contains process/audit docs (`consistency-audit-report.md`, `documentation-completion-report.md`, `societies-meta-planning.md`). These serve different audiences and should be separated.

**`CURRENT_BUILD.md` vs `README.md` duplication:**
Both files describe the current prototype scope with nearly identical bullet lists (README.md lines 14-28; CURRENT_BUILD.md lines 19-36). Validation commands appear in both (README.md lines 48-52; CURRENT_BUILD.md lines 48-52). This is duplication, not complementary documentation.

---

### 2C — Technical Constants & Single Sources of Truth

**`technical-constants.md` vs. actual code — critical divergences:**

1. **`TickIntervalSeconds = 1.0 / 20.0`** is hardcoded in `GameManager.cs` (line 17). `technical-constants.md` defines the same value as `TICK_INTERVAL_SECONDS = 0.05`. The planning constants are docs-only — there is no shared C# constants class that code imports.

2. **Structure costs in `PrototypeSettlementSimulation.cs`** (lines 25-53) hardcode all structure costs as local `static readonly` dictionaries: `HutCost`, `StorehouseCost`, `DryingRackCost`, `KilnCost`, `RemoteDepotCost`. None appear in `technical-constants.md` or any data file.

3. **Citizen tick constants in `PrototypeSettlementSimulation.cs`** (lines 16-23) hardcode: `CitizenTravelUnitsPerTick = 0.78f`, `MinimumTravelTicks = 4`, `HarvestTicks = 10`, `DepositTicks = 4`, `EatTicks = 10`, `SleepTicks = 28`, `HearthBurnIntervalTicks = 80`, `PathBuildTicks = 6`. None of these values appear in `technical-constants.md` or any planning document. They are implementation-only constants with zero planning documentation.

4. **`technical-constants.md`** (lines 312-313) says `INVENTORY_WEIGHT_MAX_KG = 100.0f`. The current prototype has no weight system — `InventoryComponent.cs` and `PlayerCharacter.cs` contain the real inventory implementation with zero weight tracking. The planning document describes aspirational constants for an unimplemented system.

5. **`project.godot`** (line 114) has a malformed comment: `#Serverconfigurationforheadlessmodetick_rate=20` — all whitespace stripped, unreadable, does nothing. The `max_players=100` and `max_agents=200` values at lines 115-116 differ from `technical-constants.md` which says `PLAYERS_LARGE = 100` and `AGENTS_ABSOLUTE_MAX = 100` (not 200).

6. **`technical-constants.md` (line 298)** says `STARTING_CREDITS_PLAYER = 100.0f` but `STARTING_CREDITS_MIN = 50.0f` (line 300). The `.opencode/plans/MASTER-IMPLEMENTATION-PLAN.md` (line 51) says `STARTING_CREDITS_PLAYER: 50`. The opencode plan and the constants doc disagree on the starting credits floor.

---

### 2D — Code Architecture & Organization

**Files doing too much:**

1. **`PrototypeSettlementSimulation.cs`** — 2,817 lines, 132,096 bytes. The largest file by a factor of ~6x. Contains: citizen AI, building queue, structure management, path corridor planning, remote depot placement, navigation grid building, resource store management, citizen lifecycle, environmental upkeep, serialization, classification logic, and metrics aggregation. By any standard this is at least 5 files.

2. **`PrototypeWorldGeneration.cs`** — 48,493 bytes, second largest. Contains: heightfield generation, biome classification, resource cluster placement, settlement spawn selection, path planner, terrain cell utilities, and buildability scoring.

3. **`GameManager.cs`** — 995 lines, 39,874 bytes. Acts as scene orchestrator AND holds hardcoded defaults that belong in data: `_initialTrees = 36`, `_initialRocks = 24`, `_initialBerryBushes = 14`, `_initialWorkers = 3` (lines 25-28). These defaults duplicate `prototype-scenarios.json`.

**Files doing too little / candidates for merging:**

1. **`DayNightCycle.cs`** (2,011 bytes) and **`WeatherController.cs`** (978 bytes) — Thin wrappers over Godot visual nodes. The simulation handles weather/time state in `PrototypeWeatherSimulation.cs` (2,518 bytes). Three files share weather/time responsibility.

2. **`PrototypeClockService.cs`** (1,745 bytes) — A static utility with a single public method (`FormatTime`) used in exactly one caller chain. Does not justify a separate file.

**Naming inconsistencies between docs and code:**

- Planning refers to "AI agents" and "citizens" interchangeably. Code has `PrototypeWorkerAgent.cs`, `PrototypeWorkerState`, and `_citizens` in the main simulation. Three names for one concept.
- Design docs describe "GOAP/Utility AI" architecture. Nothing in the actual code implements GOAP or Utility AI — worker agent behavior is a simple state machine. The code and planning describe fundamentally different architectures.

**`Prototype` prefix overuse:**
26 of 42 source files in `core/` and `simulation/` begin with `Prototype`. Once every file has the prefix, it conveys nothing. It also forces the file list to sort entirely under `P`.

**Script folder misclassification:**
- `ObserverCameraRig.cs` lives in `scripts/core/`. This is a UI/presentation concern.
- `TerrainGenerator.cs` (10,908 bytes) lives in `scripts/core/`. It generates the heightfield, assigns biomes, and computes movement costs — a world-generation concern.
- `PrototypeVoxelSpike.cs` (17,478 bytes) lives in `scripts/core/`. Per `CURRENT_BUILD.md` (line 59), it is "experimental only."

**Dead code candidates:**
- `NetworkManager.cs` (2,466 bytes) — referenced from `main.tscn` (lines 4, 17-18) and `GameManager.cs` (lines 30, 370-375) but `IsLocalSession` is the only property used; the multiplayer stack it was designed for does not exist.
- `PlayerSession.cs` (4,079 bytes) — no test coverage, not referenced from any scene or `GameManager.cs`. Likely dead code.

---

### 2E — Cross-System Coherence

**Planning describes systems that do not exist in code:**
Sessions 1-7 describe a fully networked multiplayer server with ENet, PostgreSQL/SQLite, event sourcing, GOAP/Utility AI, land claims, governance, markets, skills, a voxel world with greedy meshing, and 25-100 AI agents. The current code is a local deterministic simulation with heightfield terrain, simple state-machine worker agents (3-18 workers), JSON persistence, and no networking. `CURRENT_BUILD.md` (lines 37-44) acknowledges this, but the planning documents contain no corresponding "not yet" markers except in some (not all) session indices.

**`project.godot` server constants vs. planning:**
`project.godot` (lines 112-117) defines `max_agents=200`. `technical-constants.md` says `AGENTS_ABSOLUTE_MAX = 100`. Inconsistent.

**Voxel spike vs. voxel planning:**
`PrototypeVoxelSpike.cs` (17,478 bytes) in `scripts/core/` is described as "experimental only" in `CURRENT_BUILD.md`. Session 1 Docs 13-18 describe the voxel world in extreme detail across 6 documents. The spike is the only code representation of those 6 documents, and it is explicitly not authoritative. The planning treats voxel as foundational; the code treats it as an experiment.

---

### 2F — Agent & Developer Experience

**Three overlapping root-level documents:**
`AGENTS.md`, `CURRENT_BUILD.md`, and `README.md` all cover prototype scope and validation commands. A new agent reads all three and encounters duplicate information.

**`AGENTS.md`** (line 75) directs to `planning/sessions/[AGENTS-READ-FIRST]-index.md` as the planning entry point, but that file:
- References files that don't exist (`VERIFICATION-PROMPT.md`, `VERIFICATION-REPORT.md`)
- Lists old file names for two session-3 files

**Tribal knowledge issues:**
1. The `Prototype` prefix on all class names — no comment explains it signals provisional status without reading `CURRENT_BUILD.md`.
2. `(deepprojectplanning)/` — no file references it; its parenthesized name appears accidental.
3. Week1 `day4` file remaining after the reorganization report claims it was archived — an agent checking the report would believe it was already moved.
4. `ObserverCameraRig.cs` in `core/` — no comment explains why a camera rig lives in core simulation.

---

## Phase 3: Reorganization and Improvement Plan

---

### 3A — Proposed Directory Structure

```
societies/
├── AGENTS.md                           ← single AI entry point (consolidated)
├── README.md                           ← human overview (shorter, links out)
├── CURRENT_BUILD.md                    ← authoritative build state (unchanged)
├── .gitignore                          ← add .claude/ and .opencode/
├── .github/workflows/tests.yml
├── scripts/run-prototype-validation.ps1
├── src/
│   └── societies/
│       ├── project.godot
│       ├── Societies.csproj
│       ├── PROTOTYPE-CONSTANTS.md      ← NEW: prototype magic numbers doc
│       ├── data/
│       ├── scenes/
│       ├── scripts/
│       │   ├── core/                   ← pure domain logic and entity types
│       │   ├── simulation/             ← settlement, weather, logistics
│       │   │   └── README.md           ← NEW: which files are authoritative vs. experimental
│       │   ├── world/                  ← NEW: terrain, world gen, voxel spike
│       │   ├── presentation/           ← NEW: scene presenters, camera rig
│       │   ├── multiplayer/
│       │   └── ui/
│       └── tests/
├── tests/Societies.Core.Tests/
│   ├── Core/
│   ├── Simulation/
│   ├── Multiplayer/                    ← either add tests or delete
│   └── UI/
└── planning/
    ├── README.md                       ← NEW: planning vs. code explanation
    ├── meta/
    │   ├── technical-constants.md      ← SSOT for numerical constants
    │   ├── system-integration-map.md
    │   ├── societies-comprehensive-breakdown.md
    │   └── navigation/                 ← NEW: docs about the planning system itself
    │       ├── [META-INDEX].md         ← moved from planning/meta/
    │       ├── consistency-audit-report.md
    │       ├── documentation-completion-report.md
    │       ├── societies-meta-planning.md
    │       └── reorg-history/          ← NEW: reorganization audit trail
    │           ├── 2026-01-31-week1-to-sessions.md
    │           └── 2026-02-01-naming-standardization.md
    ├── sessions/
    │   ├── [AGENTS-READ-FIRST]-index.md  ← fixed (broken refs removed)
    │   ├── session-1-technical-architecture/
    │   ├── session-2-ai-system-design/
    │   ├── session-3-core-gameplay-loops/
    │   ├── session-4-progression-and-balance/
    │   ├── session-5-governance-mechanics/
    │   ├── session-6-prototyping-roadmap/
    │   └── session-7-integration-master-plan/
    ├── research/
    │   ├── [MASTER-RESEARCH-INDEX].md
    │   ├── guides/                     ← NEW: move 3 guide docs here
    │   ├── completed/
    │   └── reference-materials/
    ├── spreadsheets/
    └── archives/
        ├── week1-templates/            ← stays
        ├── session-2-backups/          ← stays
        └── legacy-planning/            ← NEW: expired planning artifacts
            └── master-implementation-plan.md
```

---

### 3B — Files to Create

| File | Purpose | Required Sections |
|------|---------|-------------------|
| `planning/README.md` | Entry point explaining what `planning/` is, what is aspirational vs. implemented, how to navigate | Purpose statement; planning vs. code distinction; navigation order; staleness notice |
| `src/societies/PROTOTYPE-CONSTANTS.md` | Lists every magic number hardcoded in `PrototypeSettlementSimulation.cs` (lines 16-23, 25-53) and `GameManager.cs` (lines 25-28) that has no planning equivalent | Table: constant name, value, where in code, closest planning equivalent (if any) |
| `src/societies/scripts/simulation/README.md` | Explains which simulation files are authoritative vs. experimental | Status table per file; explicit "experimental only" label for voxel spike |
| `planning/meta/navigation/reorg-history/2026-01-31-week1-to-sessions.md` | Moved copy of `planning/archives/REORGANIZATION-REPORT.md` | No new content; relocation only |
| `planning/meta/navigation/reorg-history/2026-02-01-naming-standardization.md` | Moved copy of `planning/[DIRECTORY-REORGANIZATION]-REPORT.md` | No new content; relocation only |

---

### 3C — Files to Delete or Archive

| File | Reason | Action |
|------|--------|--------|
| `nul` (repo root) | Windows null-device artifact, 0 bytes | Delete outright |
| `planning/week1-deep-planning/day4-progression-and-balance.md` | Template-era draft (line 576: "TEMPLATE"); superseded by session-4 copy; should already be archived per REORGANIZATION-REPORT.md | Move to `planning/archives/week1-templates/day4-progression-and-balance.md` |
| `planning/week1-deep-planning/` (directory) | Will be empty after file is moved | Delete |
| `(deepprojectplanning)/Societies_Comprehensive_Breakdown.pdf` | Orphaned; no file references it; naming convention violation | Move to `planning/research/reference-materials/r0-societies-source-breakdown.pdf`; delete `(deepprojectplanning)/` directory |
| `.opencode/bun.lock`, `.opencode/package.json`, `.opencode/.gitignore` | Node-ecosystem tooling files with no connection to Godot/C# project; unexplained | Delete; add `.opencode/` to `.gitignore` |
| `.claude/settings.local.json` | Local IDE config not excluded by `.gitignore` | Add `.claude/` to `.gitignore`; untrack file |
| `.opencode/plans/MASTER-IMPLEMENTATION-PLAN.md` | Expired planning artifact; describes work already completed; claims "Ready for Execution" for done tasks | Move to `planning/archives/legacy-planning/master-implementation-plan.md`; add deprecation notice |
| Rows for `VERIFICATION-PROMPT.md` and `VERIFICATION-REPORT.md` in `planning/sessions/[AGENTS-READ-FIRST]-index.md` (lines 255-257) | These files do not exist | Remove the table rows (edit, not deletion of the parent file) |

---

### 3D — Files to Merge

| Source Files | Result | Rationale |
|---|---|---|
| `README.md` + `CURRENT_BUILD.md` | Both kept, but de-duplicated | Remove prototype scope bullets from `README.md` (replace with one sentence + link to CURRENT_BUILD.md); remove validation commands from `README.md` (link instead). Each file then serves one distinct audience. |
| `planning/archives/REORGANIZATION-REPORT.md` + `planning/[DIRECTORY-REORGANIZATION]-REPORT.md` | Both moved (not merged) to `planning/meta/navigation/reorg-history/` | They document different reorganization passes; merge would destroy that history. Co-location is enough. |
| `DayNightCycle.cs` + `WeatherController.cs` | `EnvironmentController.cs` in `scripts/simulation/` | Both are thin wrappers; combining them reduces file count without losing clarity |
| `PrototypeClockService.cs` | Inline into primary caller (`PrototypeHudTextBuilder.cs` or `GameManager.cs`) | Single public method (`FormatTime`) with exactly one caller chain; does not justify a separate file |

---

### 3E — Files to Split

| Source File | Proposed Splits | Content Allocation |
|---|---|---|
| `PrototypeSettlementSimulation.cs` (2,817 lines) | `SettlementCitizen.cs` (~600 lines) | `AdvanceCitizenNeeds`, `AdvanceCitizen`, `InitializeCitizens`, `CaptureCitizen`, `RestoreCitizen` |
| | `SettlementInfrastructure.cs` (~600 lines) | `EnsureRemoteDepotPlans`, `EnsurePriorityPathPlans`, `EnsurePathCorridor`, `RebuildNavigation`, `FindPathPlan` |
| | `SettlementBuilding.cs` (~400 lines) | `InitializeStructures`, `CreateStructure`, `UpdateStructureStates`, `EnsureDynamicInfrastructurePlans` |
| | `SettlementEconomy.cs` (~500 lines) | `BuildWorkOrders`, `ApplyEnvironmentalUpkeep`, `CopyStockpileTo`, all store management methods |
| | `SettlementSimulation.cs` (core orchestrator, ~300 lines) | `Advance` tick loop, public properties, `CaptureSnapshot`, `LoadState` |
| `PrototypeWorldGeneration.cs` (48,493 bytes) | `WorldGenerator.cs` | Main heightfield + biome + resource cluster pipeline |
| | `WorldPathPlanner.cs` | A*/path-planning logic (used by both world gen and settlement) |
| `GameManager.cs` (995 lines) | `GameManager.cs` (~400 lines) | Scene orchestration only; remove hardcoded defaults (lines 25-28) which duplicate `prototype-scenarios.json` |
| `scripts/core/` reorganization | Move `ObserverCameraRig.cs` → `scripts/presentation/` | Camera rig is presentation, not core simulation |
| | Move `TerrainGenerator.cs` → `scripts/world/` | World-generation concern, not core logic |
| | Move `PrototypeVoxelSpike.cs` → `scripts/world/` | World system experiment, not core logic |

---

### 3F — Documentation Rewrites

| File | What is Wrong | What a Rewrite Must Accomplish |
|------|--------------|-------------------------------|
| `AGENTS.md` | Duplicates content from README.md (prototype scope) and CURRENT_BUILD.md (validation commands). "Getting Started" (lines 95-105) is generic. Planning structure section (lines 58-68) describes sessions but gives no explicit reading order. Directs to an index with broken references. | (1) Zero duplication of README/CURRENT_BUILD. (2) Explicit ordered reading list for first contact. (3) Clear statement of which code is authoritative. (4) All executable commands an agent might need. |
| `planning/sessions/[AGENTS-READ-FIRST]-index.md` | Lines 254-258: broken refs to nonexistent files. Lines 271, 288: old file names for renamed session-3 files. Lines 468-491: unchecked "Next Steps" boxes from early development, no signal about what happened. | Remove broken references. Fix renamed file paths. Annotate Next Steps: "These are pre-prototype planning checkboxes, not current tasks." |
| `planning/sessions/session-2-ai-system-design/SESSION-3-HANDOFF.md` | Line 167 references `day2-ai-system-design.md` as "Primary Document" but that file doesn't exist in session-2 — it's at `planning/archives/session-2-ai-backups/`. | Correct the reference. Add a banner: "This handoff is complete — Session 3 is done." |
| `planning/meta/technical-constants.md` | Claims "SINGLE SOURCE OF TRUTH" (line 3) but: (a) has no code equivalent, (b) sections 4-13 describe systems not in the current prototype, (c) prototype constants in `PrototypeSettlementSimulation.cs` (lines 16-23, 25-53) are not listed here. | Add a "Prototype Reality" banner at the top classifying each section: (a) implemented in current code, (b) planned but not yet implemented, (c) in code but missing from this doc. |
| `project.godot` | Line 114: malformed comment `#Serverconfigurationforheadlessmodetick_rate=20`. Lines 115-116: `max_agents=200` contradicts `technical-constants.md`'s `AGENTS_ABSOLUTE_MAX = 100`. | Fix the malformed comment (split into two readable lines). Resolve or document the `max_agents` discrepancy. |

---

### 3G — Naming Conventions

**Directories:** All names shall be `kebab-case` with no uppercase, underscores, parentheses, or spaces. Exception: `Societies.Core.Tests` follows .NET project naming convention and is exempt.

**Markdown files (planning):**
- Session documents: `##-kebab-case-name.md` (zero-padded number prefix)
- Navigation/index files: `[SCREAMING-KEBAB]-purpose.md` — brackets reserved for "read this first" files only
- Archive/reorg trail: `YYYY-MM-DD-kebab-description.md`
- Superseded content: `##-kebab-name.legacy.md`
- Research files: `r##-kebab-description.md`
- Report/reference files: `kebab-case-name.md` (no prefix)
- SCREAMING_SNAKE at repo root only (`AGENTS.md`, `README.md`, `CURRENT_BUILD.md`)

**C# source files:** `PascalCase.cs` matching the primary class name. No `Prototype` prefix on new files going forward; existing files keep the prefix to maintain consistency within the prototype scope.

**Godot scene files:** `kebab-case.tscn` (existing `main.tscn` is correct).

**Constants in `technical-constants.md`:** `SCREAMING_SNAKE_CASE` with category prefix. New constants must cite source session/document via a `// Source:` comment.

**Constants in C# code:** `PascalCase` per Godot C# convention (`TickIntervalSeconds`, `HarvestTicks`). No magic numbers inline.

---

### 3H — Navigation System

**Cold-start reading order for a new agent or developer:**

1. `AGENTS.md` — project identity, tech stack, repo layout, validation commands (no duplication)
2. `CURRENT_BUILD.md` — what is actually implemented right now
3. `src/societies/scenes/main.tscn` — scene tree orientation
4. `src/societies/scripts/core/GameManager.cs` — how the game bootstraps
5. `planning/README.md` (NEW) — what the planning tree is and isn't
6. `planning/sessions/[AGENTS-READ-FIRST]-index.md` — full planning navigation hub

**For implementation work on an existing system:** Read the session index, then the relevant planning doc, then the corresponding source files.

**Cross-reference format:** All markdown links use relative paths from the file's location. Absolute paths shall not appear in planning markdown. After any file move, all inbound links to that file must be updated before the move is considered complete.

**Code-to-docs bridge:** `src/societies/PROTOTYPE-CONSTANTS.md` (new file) lists current prototype constants with planning equivalents. `planning/meta/technical-constants.md` is the aspirational SSOT. Neither is complete alone — both must be read together until a shared C# constants class is implemented.

---

### 3I — Priority Order for Implementation

#### Tier 1 — Do First (structural changes; other changes depend on these)

1. **Delete `nul`** — zero-effort artifact removal; standalone commit
2. **Move `planning/week1-deep-planning/day4-progression-and-balance.md`** to `planning/archives/week1-templates/`; delete now-empty `week1-deep-planning/` directory
3. **Move `(deepprojectplanning)/Societies_Comprehensive_Breakdown.pdf`** to `planning/research/reference-materials/r0-societies-source-breakdown.pdf`; delete `(deepprojectplanning)/` directory
4. **Add `.claude/` to `.gitignore`**; untrack `.claude/settings.local.json`
5. **Add `.opencode/` to `.gitignore`** (or delete non-plan files); move `.opencode/plans/MASTER-IMPLEMENTATION-PLAN.md` to `planning/archives/legacy-planning/master-implementation-plan.md` with a deprecation notice at the top
6. **Fix `project.godot` line 114** malformed comment (split into two readable lines)

#### Tier 2 — Do Second (content fixes; depend on Tier 1 structure)

7. **Fix broken references in `planning/sessions/[AGENTS-READ-FIRST]-index.md`** — remove rows for `VERIFICATION-PROMPT.md` and `VERIFICATION-REPORT.md` (lines 255-257); update renamed session-3 file paths (lines 271, 288); add note to Next Steps section
8. **Fix `SESSION-3-HANDOFF.md` line 167** — update the broken `day2-ai-system-design.md` reference to its actual archive location
9. **Create `planning/README.md`** — the critical missing entry point for the planning tree
10. **De-duplicate `README.md` vs. `CURRENT_BUILD.md`** — remove prototype scope bullets from README.md; remove validation commands from README.md; replace both with links
11. **Create `src/societies/PROTOTYPE-CONSTANTS.md`** — document all magic numbers in `PrototypeSettlementSimulation.cs` (lines 16-23, 25-53) and `GameManager.cs` (lines 25-28) with no planning equivalent
12. **Create `planning/meta/navigation/reorg-history/`**; move both reorganization reports there
13. **Split `PrototypeSettlementSimulation.cs`** into 5 files (per 3E) — highest-value code improvement; the 2,817-line file blocks comprehension of the entire simulation subsystem

#### Tier 3 — Do Last (polish, consistency, optional improvements)

14. **Add `scripts/world/`** subfolder; move `TerrainGenerator.cs`, `PrototypeVoxelSpike.cs`, `PrototypeWorldGeneration.cs` there; update `main.tscn` script references and `using` statements
15. **Add `scripts/presentation/`** subfolder; move `ObserverCameraRig.cs` and `PrototypeSettlementScenePresenter.cs` there
16. **Merge `DayNightCycle.cs` + `WeatherController.cs`** into `EnvironmentController.cs`; inline `PrototypeClockService.cs` into its sole caller
17. **Add prototype reality markers** to `planning/meta/technical-constants.md` — classify each constant section: implemented / planned / code-only
18. **Reconcile `project.godot` `max_agents=200`** with `technical-constants.md` `AGENTS_ABSOLUTE_MAX=100`
19. **Create `planning/research/guides/`**; move three guide documents there
20. **Resolve `tests/Multiplayer/`** — either add `NetworkManagerTests.cs` and `PlayerSessionTests.cs`, or delete the directory and document the deferral in `CURRENT_BUILD.md`

---

*Plan produced: 2026-04-04. All findings are grounded in files read during the audit phase. No files were modified during the production of this plan.*
