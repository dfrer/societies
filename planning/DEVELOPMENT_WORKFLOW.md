# Societies Development Delivery Workflow

Use this workflow for substantial features, fixes, performance work, and milestones. It complements `AGENTS.md` and does not replace verified repository evidence.

## Authority and outcome contract

Use this order when sources disagree: live source/tests/runtime artifacts and clean validation; `CURRENT_BUILD.md`; `planning/active/`; older planning as intent only. Before changing status claims, reconcile them to code and evidence.

```text
Outcome: [player-visible or simulation result]
Owned slice: [bounded subsystem/files]
Non-goals: [explicit exclusions]
Value gate: [agency, legibility, reliability, determinism, performance, or delivery]
Acceptance: [observable behavior and invariants]
Evidence: [focused tests, headless validation, artifacts, matrix, or observation]
Delivery boundary: [local change, commit, PR, merge, or approval]
```

Deterministic simulation owns world truth and state-changing outcomes. LLMs may interpret, communicate, negotiate, summarize, or propose validated actions, but never mutate state directly. Humans remain consequential, and offline/model failure must preserve simulation and replay.

## Preflight and validation

```powershell
git status --short --branch
dotnet --version
godot --version
```

Preserve unrelated changes and identify permissions, export tools, fixtures, and delivery authority before depending on them. Use the smallest applicable focused checks during iteration. Before merge or handoff, run the manifest-owned fast tier (current manifest: 229 fast, 11 integration, 94 soak .NET; 23 Godot), relevant Release/ExportRelease builds, and Godot headless checks. Run full validation and the performance matrix for milestone, performance, or release claims.

Performance claims require clean ExportRelease evidence, artifact identity, deterministic contracts, and tail behavior. A better median does not make a gate green when p95, maximum, soak, stress, determinism, or artifact contracts remain outside budget. Keep characterization separate from safety gates.

## Completion and handoff

A substantial slice is complete when its bounded outcome works, focused and triggered validation passes, deterministic/persistence/fallback invariants remain intact, status/evidence files match reality, and `git diff --check` passes. Do not substitute a plan, debug-only run, partial test set, or waiver for required evidence.

```markdown
## Outcome
- Player/product result: <observable result>
- Owned boundary and non-goals: <scope>
- Value gate cleared: <evidence>

## Repository truth
- Base: <branch, commit, preserved changes>
- Implemented: <subsystems and state effects>

## Evidence
- Focused checks: `<command>` - <result>
- Integrated validation: `<command>` - <result or blocked boundary>
- Runtime/Release evidence: <artifact, matrix, hashes, CI, PR, or observation>

## Risks and gate state
- Still red or unverified: <material item>
- Assumptions: <clearly separated>

## Continue with
- Next bounded slice: <highest-value unblocked outcome>
- First command or missing input: <exact resumption point>
```

Do not label a milestone complete while a required gate remains red or unverified.
