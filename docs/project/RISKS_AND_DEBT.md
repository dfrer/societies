# Societies Risks and Technical Debt

This register distinguishes current reality from planned fixes. Priority reflects threat to the next complete Snow Globe proof, not how technically interesting a problem is.

## P0 — product and authority risks

### Central thesis remains unproven

**Evidence:** the accepted EB-01R scene contains zero participating citizens.

**Consequence:** the project can continue making a better survival/worldcraft shell without proving a society.

**Control:** the next product milestone must explicitly justify how it moves toward one legible citizen decision and shared consequence.

### Roadmap drift through agent momentum

**Evidence:** historical development repeatedly continued through adjacent Codex tasks; provider and recovery work accumulated into many active plans and branches.

**Consequence:** technically coherent work can again outrun product value.

**Control:** one active milestone, bounded packets, independent review, explicit owner activation, and the governance check.

### Automated evidence can be mistaken for acceptance

**Evidence:** prior player-facing builds passed tests and diagnostics but were rejected by the owner.

**Consequence:** a mechanically green project may still fail as a game and as Societies.

**Control:** named human gates; owner verdict recorded exactly; no score inference or average masking.

## P1 — technical and integration risks

### Voxel collision and frame safety

**Evidence:** the accepted line retains a roughly 12.8k collision-shape baseline and a historical median p95 of `51.9392 ms` against a `50 ms` safety line. Evidence across revisions is not yet reconciled for the accepted product route.

**Consequence:** citizen and world complexity may amplify a scaling failure.

**Control:** the selected next milestone must either include a strict budget or explicitly schedule a focused performance proof before breadth.

### Product/lab integration seam does not exist in play

**Evidence:** Snow Globe contracts and provider-neutral machinery exist in the lab, but no gameplay-facing citizen observation/receipt/communication interface is implemented in the accepted scene.

**Consequence:** integration may reveal incompatible identity, timing, persistence, and ownership assumptions.

**Control:** adopt the smallest versioned interface through recorded/deterministic adapters first; preserve runtime validation and exact replay.

### Responsibility hotspots

**Evidence:** `GameManager`, worldcraft HUD/diagnostic code, voxel systems, lab run-store/provider components, and some test files have grown substantially.

**Consequence:** changes become harder to review, agents lose context, and presentation/domain ownership can blur.

**Control:** decompose only when a bounded product task touches a hotspot and characterization tests can preserve behavior. Do not launch a repository-wide rewrite.

### Test-manifest drift — resolved

**Evidence:** the pre-consolidation manifest mismatch was reconciled before integration. `tests/test-manifest.json` now declares the discovered **507 managed / 387 fast / 28 Godot** counts; hosted evidence passed 507/507 complete managed tests, 387/387 fast tests, and 28/28 Godot tests with count enforcement.

**Consequence:** current test-tier claims and CI expectations are aligned with hosted discovery. Future drift remains possible but is no longer an open baseline defect.

**Control:** retain manifest-count enforcement in pull-request and weekly-full workflows; reopen this risk only if hosted discovery and the declared authority diverge.

### Persistence and migration surface area

**Evidence:** product schemas, voxel state, lab sessions, recorded responses, run stores, and provider evidence each have versioned persistence.

**Consequence:** future integration can create partial restore, stale identity, or replay divergence.

**Control:** prepare-before-commit restore, additive migrations, exact fixture tests, and one authoritative ownership model per persisted domain.

## P1 — delivery risks

### Accepted code was not yet on `master` — resolved

**Evidence:** the accepted stack was integrated through PR #183 at `54a4e5c0ea1297438b06e4b40ea14391db343657`; repository-governance closeout was completed at master commit `1eaa1ab6b0c79550a99c9cad68c4ea04e9fdea75`. The exact accepted runtime source remains preserved at `847c86b1c379e6a1dd8d4b7b641c3c89646e28c9`.

**Consequence:** the default branch now contains the accepted integrated stack, so future work no longer needs to choose between `master` and an unmerged accepted delivery head.

**Control:** branch new work from an explicitly verified current `master`; use the preservation branch as historical recovery evidence, not as a development base.

### Historical branch sprawl and stale-base ambiguity

**Evidence:** the former recovery, voxel, and worldcraft pull-request stack is closed or merged ancestry through PR #183. Numerous historical and generated Codex branches still remain in the repository.

**Consequence:** agents can still select stale branches, infer authority from branch names, or duplicate superseded work even though no delivery PR stack remains open.

**Control:** use current `master` plus one bounded active branch; take execution authority only from the active milestone; treat retained branches as history unless explicitly adopted. Do not delete historical branches merely to make the repository look clean.

### Default branch protection was not technically protected — resolved

**Evidence:** PR #186 from `chore/complete-master-protection-v1` merged at `420738bfc1b51cffacd94845b4e10cb9c72db081`. Classic branch protection is now observed on `master`; rulesets are empty. Pull requests, strict/up-to-date `build-test-smoke` and `lab-tests` contexts (GitHub Actions app id `15368`), admin enforcement, conversation resolution, and the configured no-bypass policy are enabled as recorded in `docs/project/CURRENT_STATE.md`.

**Consequence:** the documented PR and validation contract is enforced for the default branch.

**Control:** retain the exact policy and PR #186 evidence in current state; no product or release readiness is inferred from this administrative control.

### Hosted validation gap — resolved

**Evidence:** the final consolidation path received hosted product and laboratory validation before merge: 507/507 complete managed tests, 387/387 pull-request fast tests, 28/28 Godot tests, warning-free authoritative product and laboratory Release builds, and the full Windows Snow Globe suites with 1,186 core passes plus the five documented evidence-only skips, 56 benchmark, 94 recording, and 104 offline OpenRouter tests.

**Consequence:** the accepted integrated baseline no longer depends on local-only evidence. Hosted validation still does not establish visual quality, social coherence, performance acceptance, accessibility, or release readiness.

**Control:** keep both required contexts green on future pull requests and preserve separate runtime and human gates for claims that CI cannot prove.

## P2 — documentation and laboratory debt

### Flat and oversized Snow Globe laboratory

**Evidence:** the lab contains many large source and contract files plus an oversized chronological README.

**Consequence:** orientation is difficult and provider infrastructure dominates the perceived purpose of the lab.

**Control:** concise index and scoped rules now separate purpose from detailed evidence. Physical source decomposition is deferred until a touched subsystem has a tested ownership seam.

### Evidence paths remain under `planning/active/`

**Evidence:** manifests and historical documents reference `planning/active/evidence/`.

**Consequence:** evidence can be mistaken for active planning, and the directory name is semantically wrong.

**Control:** the active README and governance file classify it as compatibility-only. A later migration may move it with automated link and manifest updates.

### Historical planning volume

**Evidence:** years of concept, research, session, provider, and milestone documents remain in the repository.

**Consequence:** retrieval can surface obsolete claims before current truth.

**Control:** current documents and active milestone have explicit authority; historical active plans were moved to a dated archive; future canonical docs remain concise.

## Deferred—not defects by themselves

Networking, broad markets, general governance, large populations, long-term semantic memory, and premium-provider breadth are deferred product scope. They become risks only if reintroduced before Snow Globe proves the chartered loop.

## Risk update rule

Every milestone must state which risk it reduces and what evidence changes the classification. Closing a risk requires evidence, not a completion claim. New risks belong here only when they affect project-level direction; task-local defects remain in their issue, PR, or task packet.
