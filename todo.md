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
