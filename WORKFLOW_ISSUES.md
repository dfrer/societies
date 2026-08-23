# Workflow Issues — Societies

Project-specific workflow friction for `E:\AIExperiments\games\societies`. The global registry is `C:\Users\hunte\Documents\Codex\WORKFLOW_ISSUES.md`.

Statuses: **Open**, **Monitoring**, **Resolved**, **Deferred**, **Rejected**.

## Open entries

### WI-SOCIETIES-2026-009 - Performance worker replaced the shared delivery worktree

- **Tier:** T1
- **Status:** Monitoring
- **Scope/project:** `E:\AIExperiments\games\societies`
- **Category:** process and tooling
- **First seen:** 2026-08-03 (America/Vancouver)
- **Last seen:** 2026-08-03 (America/Vancouver)
- **Occurrences:** 1 W3-03 performance preflight
- **Reporter:** main orchestrator and `performance_worker`
- **Symptom:** The performance worker treated the shared delivery worktree `C:\tmp\societies-v3-w2-06-perf` as a disposable clone target, moved it to a timestamped backup, and created a detached clean clone at the original path.
- **Impact:** The active feature branch and untracked validation evidence disappeared from the expected path, risking work loss and invalidating later delivery commands. The matrix was stopped before measurements began.
- **Evidence:** The replacement path reported detached `a513636` with no validation evidence, while `C:\tmp\societies-v3-w2-06-perf-preexisting-20260803` retained branch `feature/v3-w3-03-wetland-consequences`, exact commit `a5136360963e7e9951d4f46fc282ac8fd0f204de`, and validation evidence SHA-256 `cf15169ed49cf309153adefdb95cec6b85ae59e94e542a6ebd17b9f878262abc`.
- **Workaround:** Interrupt the worker, move the replacement clone aside without deleting it, restore the verified timestamped worktree to its exact path, and reverify branch, commit, evidence hash, and status before continuing.
- **Root cause:** The assignment required a disposable exact-commit clone but did not name a distinct clone path strongly enough; the worker reused the shared worktree path despite the preserve-unrelated-changes instruction.
- **Proposed fix:** Performance assignments must name an explicit disposable path that differs from every registered/shared worktree and must fail rather than move or replace an existing destination. Add a read-only source/destination identity preflight before any clone or move.
- **Auto-fix eligibility/rationale:** No - T1 workspace mutation and recovery behavior require reviewed orchestration/tooling changes.
- **Resolution/validation:** Worktree restored exactly and W3-03 delivery continued; monitoring for recurrence. The replacement clone remains recoverably isolated at `C:\tmp\societies-v3-w3-03-perf-disposable-a513636`.
- **Linked entry:** WI-GLOBAL-2026-078 in `C:\Users\hunte\Documents\Codex\WORKFLOW_ISSUES.md`.

### WI-SOCIETIES-2026-008 - Godot import scan dirties clean performance source via line endings

- **Tier:** T2
- **Status:** Monitoring
- **Scope/project:** `E:\AIExperiments\games\societies`
- **Category:** validation tooling
- **First seen:** 2026-08-01 (America/Vancouver)
- **Last seen:** 2026-08-02 (America/Vancouver)
- **Occurrences:** 2 clean performance-matrix runs
- **Reporter:** main orchestrator and `performance_worker`
- **Symptom:** A Godot headless import scan rewrites `src/societies/icon.svg.import` line endings even though its Git object remains identical to `HEAD`. The strict clean-source matrix then stops before the next pair.
- **Impact:** W2-06 required explicit generated-file restoration; W3-01 lost four completed pairs and 90.314 seconds before rerunning from a prewarmed clean clone.
- **Evidence:** In both milestones, `git hash-object` matched the `HEAD` blob `3e357237b7c316d6e9044c24b95a02ca1e010b1f`, content diff/numstat were empty, and restoring only the proven generated file returned tracked status to clean.
- **Workaround:** Prewarm the exact clean clone with one Godot import scan, prove the icon import object matches `HEAD`, restore only that file, verify empty status, then start the canonical matrix.
- **Root cause:** Godot normalizes the generated import metadata working-tree line endings after Git's clean-state check, while the matrix correctly treats any tracked stat change as unsafe.
- **Proposed fix:** Add a bounded preflight import warmup and exact-object refresh before the matrix freezes source cleanliness; do not weaken content-dirty rejection.
- **Auto-fix eligibility/rationale:** No - canonical evidence cleanliness and generated-file restoration require a separately reviewed tooling change.
- **Resolution/validation:** Monitoring; the W3-01 clean-clone rerun passed 14/14 after the bounded workaround.
- **Linked entry:** WI-GLOBAL-2026-077 in `C:\Users\hunte\Documents\Codex\WORKFLOW_ISSUES.md`.

### WI-SOCIETIES-2026-007 - Sandboxed Git cannot read the user-global excludes file

- **Tier:** T2
- **Status:** Monitoring
- **Scope/project:** `E:\AIExperiments\games\societies`
- **Category:** tooling and permissions
- **First seen:** 2026-08-01 (America/Vancouver)
- **Last seen:** 2026-08-01 (America/Vancouver)
- **Occurrences:** 1 W2-06 optimization and delivery run
- **Reporter:** main orchestrator and `performance_worker`
- **Symptom:** Read-only Git status, diff, and commit-content commands repeatedly emitted `unable to access 'C:\Users\hunte/.config/git/ignore': Permission denied` for the configured user-global excludes file.
- **Impact:** Repository evidence remained usable, but the warning obscured otherwise clean output throughout the milestone gate.
- **Evidence:** The warning recurred during W2-06 implementation review, clean-matrix preflight, and final repository-state checks while each Git command otherwise completed successfully.
- **Workaround:** Judge commands by exit code and explicit repository output; do not change user-global Git configuration or ACLs during project delivery.
- **Root cause:** The workspace sandbox can read the repository but cannot read the configured user-global excludes path.
- **Proposed fix:** If consequential, evaluate a narrowly scoped readable excludes path or an approved user-level permission correction outside a release or gameplay slice.
- **Auto-fix eligibility/rationale:** No - user-global configuration and permissions are excluded from automatic repair.
- **Resolution/validation:** Monitoring; the warning did not affect W2-06 source, validation, matrix, or commit-content checks.
- **Linked entry:** WI-GLOBAL-2026-058 in `C:\Users\hunte\Documents\Codex\WORKFLOW_ISSUES.md`.

### WI-SOCIETIES-2026-006 — PowerShell diagnostic assertion depended on rendered Linux error view

- **Tier:** T1
- **Status:** Monitoring
- **Scope/project:** `E:\AIExperiments\games\societies`
- **Category:** validation
- **First seen:** 2026-08-01 (America/Vancouver)
- **Last seen:** 2026-08-01 (America/Vancouver)
- **Occurrences:** 3 PR #120 GitHub Actions failures
- **Reporter:** main orchestrator
- **Symptom:** The run-ID regression compared an expected PowerShell diagnostic against redirected presentation output. Linux `pwsh` applied ANSI styling, ConciseView wrapping/indentation, and truncation, so the semantic message was not reliably recoverable even though the script returned nonzero and rejected the invalid contract invocation.
- **Impact:** PR #120's required `build-test-smoke` check stopped at 228/229 fast tests, preventing the reviewed W2-06 packet from reaching its delivery gate.
- **Evidence:** GitHub Actions runs `30650118932`, `30710305255`, and `30710621976` failed `PerformancePairGenerator_WritesCompatibleBoundedRunIdentityAndValidatorRejectsUnsafeIds`; ANSI and whitespace normalization could not reconstruct text truncated by the rendered error view. Windows local validation passed the same test.
- **Workaround:** None. Do not waive the required check or remove the diagnostic assertion.
- **Root cause:** The test harness sourced semantic diagnostic evidence from PowerShell's rendered error view instead of the thrown exception message.
- **Proposed fix:** Completed locally: a test-owned temporary PowerShell wrapper receives injection-safe JSON named parameters, catches the target exception, and emits only `Exception.Message`; exact message, nonzero-exit, empty-stdout, no-output-directory, success-output, case-insensitive switch, duplicate-name, and temp-cleanup checks remain enforced.
- **Auto-fix eligibility/rationale:** No — T1 required-check failure needs a reviewed cross-platform regression fix.
- **Resolution/validation:** Structured-capture focused 1/1 and manifest-fast 229/229 pass locally; final independent rereview returned GO with no P0-P3 findings. Monitoring through the next PR #120 Linux rerun and for recurrence.
- **Linked entry:** WI-GLOBAL-2026-076 in `C:\Users\hunte\Documents\Codex\WORKFLOW_ISSUES.md`.

### WI-SOCIETIES-2026-005 — Restricted Windows validation cannot read installed tool configuration or optional temp artifacts

- **Tier:** T2
- **Status:** Monitoring
- **Scope/project:** `E:\AIExperiments\games\societies`
- **Category:** environment
- **First seen:** 2026-07-31 (America/Vancouver)
- **Last seen:** 2026-07-31 (America/Vancouver)
- **Occurrences:** 1 final W2-06 validation run
- **Reporter:** main orchestrator
- **Symptom:** The authoritative validation script failed in the restricted shell while probing the WinGet Godot directory, and direct `dotnet build` could not read the user NuGet configuration. The approved elevated rerun passed, but the Godot smoke still reported that its optional runtime-metrics artifact under the user temp directory could not be updated.
- **Impact:** Normal validation requires a permission escalation, and one optional test artifact is not written even though the owning test and all 23 Godot tests pass.
- **Evidence:** Restricted `./scripts/run-prototype-validation.ps1` failed at `Test-Path` for the WinGet package root; restricted Release build failed reading `NuGet.Config`; approved reruns completed 334/334 .NET, 23/23 Godot, and zero-warning Release/ExportRelease builds while preserving the non-fatal optional-artifact warning.
- **Workaround:** Run the authoritative validation and managed builds with approved access to the installed Godot/NuGet locations; treat the optional metrics warning as explicit residual evidence rather than suppressing it.
- **Root cause:** The restricted Windows token cannot read required per-user tool configuration, and the Godot test process cannot write its selected user-temp artifact path.
- **Proposed fix:** Route validation temp output to a pre-created writable project-scoped directory and document the required installed-tool access without weakening the sandbox boundary.
- **Auto-fix eligibility/rationale:** No — environment permissions and test-temp routing should be repaired as a separately reviewed tooling slice.
- **Resolution/validation:** Monitoring. The approved final validation is green, but the restricted route and optional artifact write remain unresolved.
- **Linked entry:** WI-GLOBAL-2026-074 in `C:\Users\hunte\Documents\Codex\WORKFLOW_ISSUES.md`.

### WI-SOCIETIES-2026-001 — Stale checked-out branch masks merged master behind a massive dirty tree

- **Tier:** T1
- **Status:** Monitoring
- **Scope/project:** `E:\AIExperiments\games\societies`
- **Category:** process
- **First seen:** 2026-07-30 (America/Vancouver)
- **Last seen:** 2026-07-31 (America/Vancouver)
- **Occurrences:** 2 continuation preflights
- **Reporter:** main orchestrator
- **Symptom:** The original checkout remains on `feature/v3-w2-vis-baseline` at pre-merge `d519d4d`, while remote `master` advanced through PR #119 to `f0e88f0`; the checkout still contains extensive intentional modified and untracked work, including `.tmp-loom-import-*`.
- **Impact:** A continuation task could redo merged work, treat tracked master evidence as deleted, overwrite uncommitted planning/concept work, or accidentally include temporary assets.
- **Evidence:** Original-checkout `git status --short --branch` and `git rev-parse HEAD` on 2026-07-31 showed `feature/v3-w2-vis-baseline`, `d519d4d`, and 58 porcelain entries; the earlier expanded untracked inventory contained 7,284 paths. Clean W2-05 and W2-06 worktrees were established separately from current remote master.
- **Workaround:** Leave the dirty checkout untouched. Continue from separate clean worktrees and feature branches based on verified `origin/master`, importing only explicitly reviewed documentation.
- **Root cause:** Remote feature delivery advanced `master` without reconciling the original checkout, while later planning work and temporary asset imports accumulated there.
- **Proposed fix:** Inventory and preserve intentional uncommitted documentation, remove or archive temporary paths only with explicit approval, then retire or reconcile the stale checkout.
- **Auto-fix eligibility/rationale:** No — T1; cleanup risks deleting user work and changing Git/worktree state.
- **Resolution/validation:** Monitoring. The isolated-worktree route safely delivered W2-05 and supports W2-06, but the original checkout remains intentionally unreconciled.
- **Linked entry:** WI-GLOBAL-2026-070 in `C:\Users\hunte\Documents\Codex\WORKFLOW_ISSUES.md`.

### WI-SOCIETIES-2026-002 — Required development workflow document was referenced but unpublished

- **Tier:** T1
- **Status:** Monitoring
- **Scope/project:** `E:\AIExperiments\games\societies`
- **Category:** process
- **First seen:** 2026-07-31 (America/Vancouver)
- **Last seen:** 2026-07-31 (America/Vancouver)
- **Occurrences:** 1 W2-06 preflight
- **Reporter:** main orchestrator
- **Symptom:** `AGENTS.md` requires substantial work to follow `planning/DEVELOPMENT_WORKFLOW.md`, but that file was absent from merged `master`; a candidate existed only as an untracked file in the preserved dirty checkout.
- **Impact:** The authoritative delivery workflow, validation tiers, outcome contract, and continuation format were unavailable from a clean checkout. Work could continue from `AGENTS.md` and the active plan, but process drift was materially more likely.
- **Evidence:** `Test-Path planning/DEVELOPMENT_WORKFLOW.md` was false at clean base `f0e88f0`; the original checkout's untracked candidate still asserted stale 261/.NET and 19/Godot counts.
- **Workaround:** Read the untracked candidate without modifying the dirty checkout, apply `AGENTS.md` and repository truth directly, and verify all counts from `tests/test-manifest.json`.
- **Root cause:** The workflow document and its project-registry companion were created locally but never reviewed and published with the instructions that linked them.
- **Proposed fix:** Import the reviewed workflow document through W2-06, reconcile it to manifest-owned 334 .NET and 23 Godot tests, and keep counts evidence-based.
- **Auto-fix eligibility/rationale:** No — T1; instruction hierarchy and validation policy require explicit review.
- **Resolution/validation:** Final local validation is complete, including the imported workflow and current manifest counts; protected-master delivery remains pending. Do not mark resolved before delivery.
- **Linked entry:** WI-GLOBAL-2026-071 in `C:\Users\hunte\Documents\Codex\WORKFLOW_ISSUES.md`.

### WI-SOCIETIES-2026-003 — Post-W2-05 repository-truth documents contradicted merged implementation

- **Tier:** T1
- **Status:** Monitoring
- **Scope/project:** `E:\AIExperiments\games\societies`
- **Category:** process
- **First seen:** 2026-07-31 (America/Vancouver)
- **Last seen:** 2026-07-31 (America/Vancouver)
- **Occurrences:** 1 W2-06 repository-truth audit
- **Reporter:** main orchestrator and `documentation_worker`
- **Symptom:** After PR #119 merged W2-05, `CURRENT_BUILD.md`, `README.md`, `AGENTS.md`, and `planning/PRODUCT-THESIS.md` retained conflicting claims that schema v7 was unmerged, W2-05 was inactive, or W2-04 was still the current implementation boundary.
- **Impact:** A continuation could repeat delivered work, use obsolete 263/269/333 counts, or activate the wrong milestone despite the top-level current-state paragraph and live Git showing W2-05 merged.
- **Evidence:** Clean base `f0e88f0` contained the stale phrases while PR #119 was verified merged and `tests/test-manifest.json` declared 333 .NET/23 Godot before the W2-06 tooling regression raised the count to 334.
- **Workaround:** Use live Git, the newest internally consistent `CURRENT_BUILD.md` paragraph, source/tests, and current manifest; treat contradictory lower sections as stale until reconciled.
- **Root cause:** W2-05 delivery updated selected handoff lines but did not audit all duplicated current-state summaries.
- **Proposed fix:** Reconcile every owned current-state/index document in W2-06 and prefer lifecycle-stable links over duplicated volatile detail.
- **Auto-fix eligibility/rationale:** No — T1; milestone status and release claims require evidence-backed review.
- **Resolution/validation:** Final local documentation reconciliation is complete; protected-master delivery remains pending. Do not mark resolved before delivery.
- **Linked entry:** WI-GLOBAL-2026-072 in `C:\Users\hunte\Documents\Codex\WORKFLOW_ISSUES.md`.

## Resolved entries

### WI-SOCIETIES-2026-012 — Frozen canonical-manifest fixture inherited checkout line endings

- **Tier:** T1
- **Status:** Resolved
- **Scope/project:** `C:\Users\hunte\.codex\worktrees\b452\societies`; Snow Globe .NET validation
- **Category:** validation
- **First seen:** 2026-08-22 (America/Vancouver)
- **Last seen:** 2026-08-22 (America/Vancouver)
- **Occurrences:** 1 full Snow Globe Release gate after a Windows checkout
- **Reporter:** `deep_reviewer`; captured and resolved by main orchestrator and `bugfix_worker`
- **Symptom:** `OpenRouterPremiumPaidRunReadinessManifestTests.Create_IsCanonicalBoundedRawFreeAndDigestBound` expected CRLF because its raw multiline frozen fixture inherited working-tree line endings, while production deliberately emits canonical LF with `string.Join('\n', ...)`.
- **Impact:** The full suite reported 931 passes and one failure even though the production manifest and digest were unchanged; cross-platform checkout policy could therefore invalidate the delivery gate.
- **Evidence:** The isolated class reproduced the mismatch at character 59. After the test-only repair, the focused class passed 4/4 and production output is explicitly asserted to contain no carriage return.
- **Workaround:** Normalize the source-literal fixture from CRLF to LF before the exact comparison; retain the frozen manifest, canonical digest, and explicit LF assertion.
- **Root cause:** C# raw multiline string literals preserve source-file line endings, but the test treated the fixture as platform-independent canonical text.
- **Proposed fix:** Completed with bounded test-only fixture normalization; production manifest behavior and provider boundaries remain unchanged.
- **Auto-fix eligibility/rationale:** No — T1 full-gate correctness required an exact regression repair and replacement full validation.
- **Resolution/validation:** Resolved. Focused readiness-manifest tests pass 4/4; final full-suite and build evidence is recorded in the current milestone handoff.
- **Linked entry:** WI-GLOBAL-2026-131 in `C:\Users\hunte\Documents\Codex\WORKFLOW_ISSUES.md`.

### WI-SOCIETIES-2026-011 — Ollama benchmark tests used scheduler timing as an in-flight sampling contract

- **Tier:** T1
- **Status:** Resolved
- **Scope/project:** `C:\Users\hunte\.codex\worktrees\b452\societies`; Snow Globe .NET validation
- **Category:** validation
- **First seen:** 2026-08-22 (America/Vancouver)
- **Last seen:** 2026-08-22 (America/Vancouver)
- **Occurrences:** 1 aggregate Snow Globe Release validation
- **Reporter:** main orchestrator and `bugfix_worker`
- **Symptom:** The full Snow Globe suite intermittently reported `inflight_vram_sample_unavailable` instead of the expected parser rejection, counted 13 VRAM samples instead of 12, and could stall, while the full Ollama benchmark class passed 76/76 in isolation.
- **Impact:** Aggregate validation could not provide a trustworthy completion result until the test harness was made deterministic. Production model, provider, network, and sampling behavior were unaffected.
- **Evidence:** The original aggregate run produced the two mismatched expectations. After the test-only repair, the seven original regressions passed five consecutive runs, the affected set passed 14/14, the full Ollama class passed 76/76, and the full Snow Globe Release suite passed 920/920 without hanging.
- **Workaround:** The repair replaces scheduler yields and elapsed-time assumptions with an opt-in per-POST/probe handshake in the fake transport and probe.
- **Root cause:** Tests used `Task.Yield` or a two-millisecond delay to imply that exactly one VRAM probe completed while a fake POST remained pending; parallel suite scheduling could produce zero or two samples.
- **Proposed fix:** Completed. Keep production behavior unchanged and coordinate only the affected fake POST/probe pairs with explicit asynchronous signals.
- **Auto-fix eligibility/rationale:** No — T1 aggregate validation correctness required a focused regression repair and full replacement gate.
- **Resolution/validation:** Resolved. Focused regressions passed 7/7 five times; affected coverage passed 14/14; the class passed 76/76; the full Snow Globe Release suite passed 920/920; the Release build passed with 0 warnings and 0 errors; diff checks are clean.
- **Linked entry:** WI-GLOBAL-2026-130 in `C:\Users\hunte\Documents\Codex\WORKFLOW_ISSUES.md`.

### WI-SOCIETIES-2026-010 — OpenRouter readiness omitted the preserved one-shot state-generation blocker

- **Tier:** T1
- **Status:** Resolved
- **Scope/project:** `C:\Users\hunte\.codex\worktrees\b452\societies`; Snow Globe OpenRouter premium evidence
- **Category:** provider safety
- **First seen:** 2026-08-22 (America/Vancouver)
- **Last seen:** 2026-08-22 (America/Vancouver)
- **Occurrences:** 1 separately authorized fourth preflight invocation
- **Reporter:** main orchestrator
- **Symptom:** The reviewed readiness plan said a fresh authority could be created by invoking `preflight` once, but the fixed production `v1` state tree still preserved Attempt 3's `execution-consumed.tombstone`, runtime authorization, terminal evidence, and validation receipt. The one permitted invocation therefore stopped as `preflight_already_attempted`.
- **Impact:** The fourth authority was consumed without a metadata GET, credential read, new authorization, paid request, charge, or new validation artifact. Retrying, reusing Attempt 3's digest, or moving/deleting its state would violate the no-retry and evidence-preservation boundaries.
- **Evidence:** The invocation returned `OPENROUTER_FAILED code=preflight_already_attempted` / exit 1. `OpenRouterPremiumProductionBridge.PreflightAsync` checks `preflight-failed`, `runtime-authorization`, and `execution-consumed` before metadata verification. The live tree hashes still match Attempt 3: runtime authorization `d13f4aaa9f930f27923147ef7ddcadbcefaf5938ca7217698fb8aa477a683288`, preflight artifact `60e8f0d1b07b6ed7c8f191d94add6f2187035cbc7f6d9492d95dc0ec7c7e19ad`, evidence `af485def08d5c465807d069fc59a566c077f111b003723c0ece75b8db7732ea0`, and receipt `0885c1b530d7a12b27cbe6c7c5eddc6dd27957125fe1650d741f70a4c0332ba3`.
- **Workaround:** The fourth authority remains terminal and may not be retried. Preserve v1 exactly as evidence; all future paid work must use the distinct v2 lifecycle and a newly authorized authority digest.
- **Root cause:** The offline readiness manifest behaviorally covered the in-memory evidence executor but did not cover production state-generation rollover. The documented five-step plan assumed the fixed `v1` path was empty even though preservation of Attempt 3 made that assumption false.
- **Proposed fix:** Completed. The reviewed v2 composition provides exact fixed-v2 selection, anchor-before-root construction, exact O(1) authority lookup, durable claims, claimed-journal execution, final journal/evidence binding, exact-digest validation, and junction/reparse/path-swap containment without consulting or moving v1. A separately authorized one-shot command provisioned the distinct 32-byte state anchor and initialized only the empty v2 scaffold. Local files and the anchor do not claim protection against the owning user, an administrator, or whole-volume rollback.
- **Auto-fix eligibility/rationale:** No — T1 provider-state lifecycle, preserved paid evidence, and no-retry authority required explicit scope, security review, and user authorization.
- **Resolution/validation:** Resolved. The one-time provisioning command succeeded once with exit 0, anchor identity SHA-256 `d6eb68c6f14e32f342caee45f4c13d2398e4c0830e39d3e7cbfe478a10d9a78d`, and matching state-contract digest `f9d12a2c0bcfb60cc874dc49bc60462197700d681a42ae98ce4bcefd28ac8511`; its authority is consumed and it must not be rerun. The implementation gate passed 881/881 full Snow Globe and 70/70 full CLI/security tests; after the Global-mutex correction, focused anchor/admin 19/19, CLI security 14/14, bridge 56/56, and the CLI Release build passed. Independent review found no P0-P2 issue; the abandoned-mutex path lacks a dedicated regression (P3). The separately authorized fifth run then exercised the corrected v2 lifecycle exactly once: authority `40655d2535520757b411595593deda6a714127e5366cf9afb3292aab0f3bc2d6` produced one generation, one execution claim, terminal raw-free evidence `7d449d77c3b82ff1984a0e1d33c3026b80566321938418b2eb363ff1aa9f1bd8`, and one validation claim/receipt `7f42dfd15ffe1e4134d7110a9f491f9dfaae1632d69e14180bf47dbc6056dc49`; no second dispatch or retry occurred and v1 was not accessed. The provider charge remains unknown because local zero settlement is not provider accounting.
- **Linked entry:** WI-GLOBAL-2026-127 in `C:\Users\hunte\Documents\Codex\WORKFLOW_ISSUES.md`.

### WI-SOCIETIES-2026-004 — Canonical matrix generated runner IDs longer than its own safety contract

- **Tier:** T1
- **Status:** Resolved
- **Scope/project:** `E:\AIExperiments\games\societies`
- **Category:** validation
- **First seen:** 2026-07-31 (America/Vancouver)
- **Last seen:** 2026-07-31 (America/Vancouver)
- **Occurrences:** 1 canonical W2-06 matrix failure
- **Reporter:** `devops_worker`
- **Symptom:** `run-performance-pair.ps1` produced a 96-character descriptor and appended `-off`/`-on`, so PerfRunner correctly rejected the resulting 100-character runner ID against its 96-character safe-identifier limit.
- **Impact:** The W2-06 canonical matrix stopped after 1/14 pairs before performance, soak, or forced-invalidation conclusions were available.
- **Evidence:** The first attempt failed on `run_matrix-c3-warm-t1`; `planning/active/evidence/v3-w2-06-technical-validation.json` preserves the exact rejected ID, output paths, and hashes.
- **Workaround:** None was used; the matrix was not retried until a reviewed repair was committed from clean source.
- **Root cause:** Generator and validator length contracts diverged when the metrics-mode suffix was added outside the bounded descriptor.
- **Proposed fix:** Completed: commit `7952f37` derives bounded runner IDs from a 96-bit SHA-256 descriptor fingerprint, writes an early schema-v1 identity artifact, preserves schema-v6 pair results, rejects mixed offline/production modes, and adds a real PowerShell/C# regression.
- **Auto-fix eligibility/rationale:** No — T1; artifact identity, path safety, and canonical evidence required independent review.
- **Resolution/validation:** Resolved. Final independent review reported no P0–P3 findings, and the clean repaired canonical matrix completed 14/14 pairs with contract status `pass`. The separate performance budget remained red and is a product gate, not a recurrence of this tooling defect.
- **Linked entry:** WI-GLOBAL-2026-073 in `C:\Users\hunte\Documents\Codex\WORKFLOW_ISSUES.md`.
