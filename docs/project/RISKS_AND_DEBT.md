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

### Test-manifest drift

**Evidence:** `tests/test-manifest.json` declares 494 managed and 27 Godot tests while the accepted handoff reports 507 and 28.

**Consequence:** tier claims and CI expectations can become dishonest even when tests pass.

**Control:** use hosted discovery from the consolidation PR, reconcile counts and tier filters in a focused follow-up if needed, and never manually guess counts.

### Persistence and migration surface area

**Evidence:** product schemas, voxel state, lab sessions, recorded responses, run stores, and provider evidence each have versioned persistence.

**Consequence:** future integration can create partial restore, stale identity, or replay divergence.

**Control:** prepare-before-commit restore, additive migrations, exact fixture tests, and one authoritative ownership model per persisted domain.

## P1 — delivery risks

### Accepted code is not yet on `master`

**Evidence:** the accepted stack is fourteen commits ahead of the current default branch.

**Consequence:** new work may start from the wrong base and status documents can disagree with the default branch.

**Control:** one consolidation PR from the accepted head; no new product stack until integration.

### Stacked PR and branch ambiguity

**Evidence:** several open recovery/voxel/worldcraft PRs represent predecessor or stacked delivery paths. The repository also retains many generated Codex branches.

**Consequence:** agents can select obsolete bases or duplicate work.

**Control:** preserve the exact accepted archive branch, close predecessor PRs as superseded after the consolidation PR exists, use `master` plus one active branch, and treat other branches as history unless explicitly adopted.

### Default branch is not technically protected

**Evidence:** the repository ruleset API returns no active rulesets; the connected integration cannot write classic branch-protection settings.

**Consequence:** a direct or force push can bypass the documented PR and validation contract.

**Control:** issue [#184](https://github.com/dfrer/societies/issues/184) defines the exact manual configuration and verification. Keep this risk open until the rule is observed.

### Local evidence is stronger than hosted evidence

**Evidence:** the accepted handoff reports extensive local tests and desktop capture, while hosted workflow status was not attached to the final stacked head.

**Consequence:** environment-specific or integration failures may remain hidden.

**Control:** run the required GitHub Actions suite on the consolidation PR and record the result before merge.

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
