# Societies Risks and Technical Debt

This register distinguishes current reality from planned fixes. Priority reflects threat to the next complete Snow Globe proof, not how technically interesting a problem is.

## P0 — product and authority risks

### Central thesis remains unproven

**Evidence:** the accepted EB-01R scene contains zero participating citizens.

**Consequence:** the project can continue making a better survival/worldcraft shell without proving a society.

**Control:** `SNOW-GLOBE-SOCIAL-KERNEL-V1` requires exactly Mara, Ivo, and Sena in one authoritative causeway consequence. A one-citizen route is only an internal interface checkpoint; owner acceptance of all three is the product gate.

### Visual-production gate reopened

**Evidence:** the owner rejected the first Packet 02 visual presentation as “basically none of this is near actual visual completleness, needs massive work”. Headless evidence has no GPU/render driver and cannot establish visual quality. Existing route characterization is process p95 **7.7562 ms** and physics p95 **20.7071 ms**; physics misses the 16.67 ms target while passing the 33.33 ms hard-safety line.

**Consequence:** social breadth must not resume on a primitive presenter or dressed-up blockout. The representative Causeway district needs one coherent weathered civic-folk wetland direction, authored asset provenance, contextual interaction feedback, and an actual in-engine owner gate.

**Control:** recover deterministic seams in Packet 02A, then deliver exactly one near-final district in Packet 02V. Record the displayed baseline using the protocol in the active milestone; Packet 03 remains unstarted until owner acceptance. No quality reduction may green an unrelated metric.

### A narrow social slice can become a scripted quest or player-puppet show

**Evidence:** no accepted Mara/Ivo/Sena, trust, commitment, or causeway domain exists yet. The legacy civic/wetland route and current cognition precursor can demonstrate mechanics without proving citizen knowledge, independent agency, or a lived social consequence.

**Consequence:** a fixed scenario could appear coherent while dialogue order, hidden omniscient state, or player-triggered puppetry determines every result. It would not prove a society.

**Control:** require bounded citizen-known facts, citizen-to-citizen observation/response, state-driven contribution/counter/refusal, a zero-input route from scenario start through the following morning, material custody/consumption/withholding, promise/breach, stale-world/time paths, causal perturbations, independent citizen action, and next-morning consequences. No majority tally, privileged player choice, or magical veto; influence is limited to role authority, knowledge, custody, labor, obligations, trust, dependency, and world facts. Presentation and communication cannot own or invent authoritative facts.

### Material sacrifice can be replaced by a free compromise

**Evidence:** the selected scenario needs an exposed player shelter and a small player-owned dry-timber reserve, but those authoritative custody and obligation facts do not yet exist.

**Consequence:** contributing material could become a fake choice, while a compromise could instantly satisfy everyone without resource, labor, ecological, commitment, or relationship cost.

**Control:** keeping the reserved timber is allowed and observed; contributing it removes a real personal repair option. Staged compromise requires immediate extra material, player labor, citizen cooperation, and a dated restoration/repair obligation. Next-morning delivery/fulfillment or breach derives from recorded state and has consequences.

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

**Evidence:** accepted-scene characterization records 64 collision bodies, 12,777 initial collision shapes, and 12,781 shapes after an edit. A historical `51.9392 ms` safety miss remains unresolved, and no representative frame/physics timing baseline exists for the fixed accepted-scene Causeway route.

**Consequence:** citizen and world complexity may amplify a scaling failure.

**Control:** packet 01 establishes three warm fixed-route trials before citizen breadth and reports p50/p95/p99/max frame and physics timing, collision counts, and backlog. Target p95 is at most 16.67 ms, hard safety is at most 33.33 ms, and same-route p95 regression is at most 10%. Unchanged terrain collision count cannot grow without evidence. Misses remain misses; broad refactoring is prohibited; a hard-safety breach stops breadth or milestone passage without explicit owner disposition.

### Product/lab integration seam does not exist in play

**Evidence:** Snow Globe contracts and provider-neutral machinery exist in the lab, but no gameplay-facing citizen observation/receipt/communication interface is implemented in the accepted scene. `PrototypeCognitionModule` is a narrow strict precursor whose current apply path records an informational event only.

**Consequence:** integration may reveal incompatible identity, timing, persistence, and ownership assumptions.

**Control:** adopt a product-owned versioned request bound to interaction, citizen, tick, state version/digest, observation digest, citizen-known facts, scenario-specific allowed actions, and cancellation/deadline. Canonical request ≤16 KiB, receipt ≤8 KiB, depth ≤4, fact refs ≤16, recent-event refs ≤8, commitment refs ≤8, proposals ≤8, and communication ≤512 UTF-8 bytes; reject oversized, over-deep, duplicate, unknown-field, or noncanonical input before validation/communication/persistence. Keep closed proposal separate from bounded communication; validate before append; persist the validated receipt/result, commitment, and events; replay never calls an adapter. Absent, malformed, late, stale, cancelled, and unavailable paths are typed, and fallback preserves the same identity and action vocabulary. Only deterministic and recorded adapters are in scope; provider, CLI, billing, raw-response, and operator-store boundaries stay out.

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

**Consequence:** the default branch now contains the accepted integrated stack and delivered visual-recovery planning amendment, so future work no longer needs to choose between current `master` authority and stale delivery heads.

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
