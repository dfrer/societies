# Workflow Issues — Societies

Project-specific workflow friction for `E:\AIExperiments\games\societies`. The global registry is `C:\Users\hunte\Documents\Codex\WORKFLOW_ISSUES.md`.

Statuses: **Open**, **Monitoring**, **Resolved**, **Deferred**, **Rejected**.

## Open entries

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
