# V3 W3-05 Targeted Tests and Author Smoke

## Outcome contract

**Outcome:** Prove the complete existing deterministic civic loop with fixed-seed automated
coverage and one honest local author smoke, without adding a new gameplay system or changing the
W3-04 cognition contract.

**Owned slice:** Focused managed and Godot tests, test-manifest registration, a machine-readable
W3-05 evidence record, exact author-smoke evidence or manual instructions if observation is not
reliable, and milestone truth in `CURRENT_BUILD.md`, `WORKFLOW.md`, `README.md`, and the active
Weeks 3-4 plan. Production/runtime changes are out of scope unless a blocking defect is first
proved and separately justified.

**Non-goals:** No W4 work; live model/provider, network, credential, payment, retry, provider
selection, or Snow Globe integration; second policy/world authority; schema or migration change;
restoration jobs, markets, general law-system expansion, broad UI, multiplayer, deployment,
release work, unrelated refactor, or performance optimization.

**Value gate:** Clear the reliability and author-observation gate for the already-implemented civic
decision loop while preserving deterministic validation as the sole authority for state changes.

**Acceptance:**

1. Fixed-seed Protect and DrawDown paths show conflicting citizen material interests, structured
   cognition reasons, the selected policy, and the resulting wetland consequence.
2. Valid proposal plus missing, invalid, cancelled, timed-out, and unavailable fallback paths are
   exercised through the existing evaluator, validator, and event-application path.
3. Invalid, stale, malformed, oversized, excessively deep, incoherent, and duplicate-use inputs
   reject without cognition-driven policy mutation; accepted decisions append exactly one
   `civic.cognition.decision` event.
4. Schema-v9 save/resume or replay evidence is equivalent where applicable, including policy,
   wetland, citizen reason, cognition source/event, snapshot, and final-state identity.
5. A no-input/offline baseline continues without any model service.
6. One local end-to-end Godot author smoke records the exact commit, scenario, seed, actions,
   observed policy, citizen reasons, wetland consequence, cognition source/event, and visual or
   interaction limitations. If reliable interactive judgment is unavailable, automated work still
   completes and the exact manual instructions and expected observations replace any pass claim.

**Evidence:** Focused W3-05 tests; relevant W3-01 through W3-04 regressions;
`scripts/run-prototype-validation.ps1`; Release and ExportRelease production builds; Godot
headless validation; `git diff --check`; machine-readable W3-05 validation/smoke evidence; required
CI; and independent deep review with no unresolved P0-P2 findings. Preserve W3-04's canonical
performance evidence rather than rerunning the matrix when production/runtime behavior is
unchanged.

**Delivery boundary:** Coherent local implementation and evidence commits on
`feature/v3-w3-05-targeted-tests-smoke`, push, pull request, required checks, merge, actual merge
state recorded in repository-truth documents (with a docs-only follow-up PR if necessary),
ancestor verification against the latest `origin/master`, and a clean isolated worktree. The
primary dirty checkout remains untouched; W4 and all live-provider work remain inactive.
