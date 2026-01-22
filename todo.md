# Testing Pipeline TODO

## P0 — Reliability / Running Tests
- [ ] Expand test discovery to include `tests/` root and nested subdirectories (recursive scan).【F:tests/test_runner.gd†L8-L148】
- [ ] Move test output file from `res://` to `user://` (or allow CLI override) to avoid read-only failures in headless/CI.【F:tests/test_runner.gd†L22-L27】
- [ ] Add `--path` guidance to docs to ensure `res://` resolves correctly in CLI runs.【F:README.md†L60-L69】
- [ ] Add a preflight check for required config files (`res://config/*.json`) and fail fast with a clear error message.【F:tests/test_fixtures.gd†L11-L30】
- [ ] Ensure runner returns a distinct error when zero tests are found (currently fails ambiguously).【F:tests/test_runner.gd†L124-L129】

## P0 — Hung Tests / Timeouts
- [x] Add per-test timeout handling (watchdog or cooperative yield/timeout pattern) to prevent indefinite hangs.【F:tests/test_runner.gd†L90-L317】
- [ ] Emit a timeout error that includes the test file and subtest label when applicable.【F:tests/test_runner.gd†L126-L145】

## P1 — Noise Reduction & Actionable Output
- [x] Emit machine-readable test results (JSON) alongside human log output for CI parsing.【F:tests/test_runner.gd†L136-L224】
- [x] Summarize failures with test file + failure count at end of run for quick triage.【F:tests/test_runner.gd†L136-L145】
- [x] Add optional `--filter` or `--only` flag to run a subset of tests quickly.【F:tests/test_runner.gd†L168-L191】

## P1 — Fixture Consistency
- [ ] Consolidate test fixtures (`SimFixture` vs `TestFixtures`) to a single, documented API to reduce drift.【F:tests/sim_fixture.gd†L1-L52】【F:tests/test_fixtures.gd†L1-L139】
- [ ] Document the expected minimal tuning defaults used by tests to prevent slowdowns when tuning changes.【F:sim/sim.gd†L25-L120】

## P1 — Slow/Flaky Integration Tests
- [ ] Add a “fast/slow” split (e.g., `tests/slow/`) with an opt-in flag to run long-running integration tests.【F:tests/integration/test_save_load.gd†L24-L126】
- [ ] Reduce simulation workload in tests by using a minimal tuning config (smaller world, fewer NPCs) to improve runtime stability.【F:config/tuning.json†L1-L60】

## P2 — Environment-Specific Test Cleanup
- [ ] Remove or rewrite `test_baseline_determinism.gd` to avoid hard-coded Windows Godot path; use in-process logic or PATH-based `godot` invocation.【F:tests/test_baseline_determinism.gd†L34-L60】
- [ ] Move debug scripts out of `tests/` or expand exclusions to avoid accidental execution when discovery is broadened.【F:tests/test_runner.gd†L12-L19】

## P2 — De-duplication
- [ ] Merge or remove duplicate config validation tests (`tests/test_config_schema.gd` vs `tests/integration/test_config.gd`) to prevent redundant failures and slow runs.【F:tests/test_config_schema.gd†L1-L118】【F:tests/integration/test_config.gd†L1-L118】

## P3 — Developer Experience
- [ ] Add a `make test` or script wrapper to standardize the command and project path usage.
- [ ] Add a short troubleshooting section for common CLI errors (missing `res://` path, permissions, missing Godot binary).

# V2 Settlement Simulation TODO

## P0 — Planning + Task System Foundations
- [x] Add a Task/Project system phase before Agents tick without reordering existing pipeline. (docs/sim_update_order.md)
- [x] Expand JobBoard activity types (haul, deliver-to-project, build site, craft at station, farm tasks) and serialization coverage. (sim/job_board.gd)
- [x] Extend DefaultBrain to claim new task types and stack lightweight intents for project work. (sim/brains/default_brain.gd)
- [x] Add task generation hooks for build sites, hauling, and farming while preserving deterministic ordering. (sim/job_board.gd, sim/sim.gd)
- [x] Post deliver-to-project activities for communal project resource needs. (sim/systems/job_board_system.gd, sim/job_board.gd)
- [x] Post build-site activities for communal project build phases. (sim/systems/job_board_system.gd, sim/job_board.gd)

## P0 — Communal Projects as Build Sites
- [x] Add build site state (required inputs, delivered inputs, build progress, assigned workers). (sim/communal_projects_system.gd)
- [x] Add phases: COLLECTING → BUILDING → COMPLETED with tick-based progress. (sim/communal_projects_system.gd)
- [x] Create BUILD_SITE tasks from active build sites and finalize on progress completion. (sim/communal_projects_system.gd, sim/job_board.gd)
- [x] Preserve project type API and resource requirement definition. (sim/communal_projects_system.gd)

## P0 — Shared Storage + Logistics
- [x] Add Stockpile structure state with capacity, ownership, and reserved items. (sim/structures.gd, sim/structure_state.gd)
- [x] Add deposit/withdraw/haul actions and task types for stockpile logistics. (sim/actions.gd, sim/job_board.gd)
- [x] Add reservation/escrow to prevent double-spending across projects. (sim/communal_projects_system.gd)

## P1 — Organization Planner
- [x] Create Organization entity with members, stockpile access, and treasury. (sim/organizations.gd)
- [x] Add daily planner that spawns stockpile/workshop/shelter projects based on thresholds. (sim/organizations.gd, sim/sim.gd)
- [x] Implement contiguous claim expansion from a town center and zoning tags. (sim/claims_system.gd)

## P1 — Workshops + Production Chains
- [x] Add station types and require them for recipes (carpenter, kiln, smithy). (sim/recipes.gd, sim/workshop_system.gd)
- [x] Add craft-at-station tasks and station build projects. (sim/job_board.gd, sim/communal_projects_system.gd)
- [x] Planner spawns stations in sequence based on needs. (sim/organizations.gd)

## P1 — Roads + Logistics Optimization
- [x] Spawn road projects connecting resource clusters to stockpiles and town center. (sim/communal_projects_system.gd, sim/organizations.gd)
- [x] Prefer roads for hauling tasks and pathing when available. (sim/agent_navigation.gd)

## P2 — Farming Pipeline
- [x] Add farm plot tile state: tilled, seeded, growth, harvest-ready. (sim/world_tile.gd)
- [x] Integrate daily growth into EnvironmentSystem tick. (sim/environment_system.gd)
- [x] Add TILL/PLANT/HARVEST/DELIVER tasks and actions. (sim/job_board.gd, sim/actions.gd)
- [x] Transition from foraging to farming via planner thresholds. (sim/organizations.gd)

## P2 — Economy + Contracts
- [x] Post procurement contracts when stockpiles fall below thresholds. (sim/organizations.gd, sim/contract_system.gd)
- [x] Auto-post surplus to market. (sim/market_system.gd)
- [x] Allow agents to choose between org tasks and paid contracts. (sim/brains/default_brain.gd)

## P3 — Metrics + Debugging
- [ ] Add build-site progress telemetry and stockpile throughput metrics. (sim/metrics_system.gd)
- [ ] Add debug overlays for projects, tasks, and stockpile reservations. (viz/)
