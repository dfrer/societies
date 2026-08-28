# AGENTS.md — Societies Project Operating Contract

## Mission

Build Societies toward this product north star:

> A deterministic civilization and ecology simulation where humans and AI citizens work, trade, negotiate, govern, and experience shared consequences.

Snow Globe is the first bounded realization of that idea. It must become a small but complete society proof before the project expands into a larger civilization simulation.

## Owner and agent roles

The project owner is the **vision, product, and human-acceptance authority**. Do not require the owner to choose files, classes, algorithms, migrations, test filters, or refactor tactics. Translate technical decisions into understandable options, consequences, and a recommendation.

Agents are responsible for technical investigation, planning translation, bounded implementation, validation, review, documentation, and honest handoff. An execution agent does not gain roadmap authority merely because it completed the previous task.

## Required reading order

Before substantial work, read:

1. `project-governance.json`
2. `docs/project/CHARTER.md`
3. `docs/project/CURRENT_STATE.md`
4. `planning/active/MILESTONE.md`
5. `docs/project/ARCHITECTURE.md`
6. `docs/project/DEVELOPMENT_PROCESS.md`
7. the nearest scoped `AGENTS.md`
8. only then the relevant code, tests, ADRs, evidence, and historical material

Do not begin by reading the largest historical status file or by treating an old plan as current.

## Authority order

When sources disagree, use this order:

1. verified source behavior, tests, runtime artifacts, and explicit human acceptance;
2. `docs/project/CURRENT_STATE.md`;
3. `planning/active/MILESTONE.md` for authorized work;
4. `docs/project/CHARTER.md` and accepted decisions;
5. `docs/project/ARCHITECTURE.md` and `DEVELOPMENT_PROCESS.md`;
6. ADRs and bounded evidence;
7. archived plans, old completion reports, branch names, and prior agent summaries.

A passing test does not overrule a failed human product gate. A historical completion claim does not activate work.

## Current development state

- Accepted runtime baseline: `847c86b1c379e6a1dd8d4b7b641c3c89646e28c9`.
- Preservation branch: `archive/pre-consolidation-2026-08-27`.
- The accepted EB-01R slice proves bounded founder worldcraft and a substantially better interaction surface.
- It does **not** yet prove participating citizens, integrated Snow Globe cognition, a negotiated shared consequence, performance acceptance, accessibility, or release readiness.
- The selected active milestone is `SNOW-GLOBE-SOCIAL-KERNEL-V1 — Causeway Before Nightfall`; the accepted scene still contains zero participating citizens.
- The planning branch authorizes no implementation. After the planning PR merges to `master`, feature work is permitted only through the milestone's ordered packets, beginning with packet 01 and proceeding one merged, reconciled PR at a time.

## Non-negotiable product and architecture rules

1. `PrototypeRuntimeSession` and deterministic domain code own world facts and every state-changing outcome.
2. Godot scenes, presenters, HUDs, diagnostics, and model adapters consume or propose; they do not own hidden gameplay truth.
3. LLMs may interpret bounded observations, deliberate, communicate, summarize bounded memory, and propose closed actions. They never mutate world state or invent authoritative facts.
4. Replay, persistence, fallback, and offline operation must remain possible without calling a model.
5. The player is an embodied resident-founder with limited formal authority. Influence comes from contribution, commitments, persuasion, and consequences—not omnipotent commands.
6. Citizens must be allowed to counter, delay, refuse, withdraw, or proceed for legible material and social reasons.
7. Provider identity, credentials, billing, routing internals, and raw responses stay outside the player-facing product.
8. Human acceptance is required for visual, interaction, social, and experiential claims.

## Work-selection rules

- There is exactly one active milestone: `planning/active/MILESTONE.md`.
- Read `project-governance.json` for the merge-conditioned authorization state: before planning-PR merge is false; after merge is true only for ordered packets.
- Select the next task from the milestone's ordered missing evidence, not from an agent's closing recommendation.
- Every implementation task must state outcome, owned boundary, non-goals, acceptance, validation, human gate, and stop conditions.
- Prefer the smallest vertical change that proves a product-relevant fact.
- Do not add adjacent systems, generalized frameworks, provider depth, content breadth, or speculative abstractions unless the active milestone requires them.
- Do not reactivate archived plans by editing them. Create an explicit decision and update the active milestone.
- When a human decision is required, stop with concrete options and a recommendation. Do not fill the gap with implementation.

## Required execution cycle

1. **Orient:** inspect branch, status, authority documents, relevant code, tests, and prior evidence.
2. **Plan:** restate the bounded outcome and identify what is already proven versus assumed.
3. **Implement:** make the smallest coherent change; preserve unrelated work.
4. **Verify:** run focused checks, then every triggered integration, replay, persistence, build, CI, performance, or human gate.
5. **Review:** inspect the actual diff and failure paths independently of the completion summary.
6. **Report:** separate implemented facts, test evidence, human evidence, unresolved risks, and prohibited follow-on work.
7. **Integrate:** use a focused PR; do not merge red or unverified required gates.
8. **Archive:** move completed or stopped plans out of `planning/active/` before activating another milestone.

## Evidence honesty

Use these labels precisely:

- **Implemented** — code exists.
- **Mechanically validated** — named automated checks passed.
- **Runtime observed** — a named executable route was inspected.
- **Human accepted** — the owner explicitly accepted the stated product gate.
- **Characterized** — measured without passing a target.
- **Deferred** — intentionally not built.
- **Blocked** — a named dependency or decision prevents work.

Never convert characterization into acceptance, infer numeric scores the owner did not provide, claim CI from local tests, or claim a complete society from infrastructure alone.

## Git and delivery rules

- Work in an isolated branch or worktree from an explicit base SHA.
- One branch owns one bounded outcome.
- Branch each authorized packet from the then-current `master` only after the preceding packet is merged and its gates are reconciled; do not stack the sequence.
- Show `git status`, exact validation commands, failures, and the final commit SHA in every handoff.
- Pull requests must use `.github/pull_request_template.md` and identify the human gate.
- `master` is the integration branch. Archive branches preserve historical states and are not development bases.
- Do not delete historical material merely to make the repository look clean; move it into the defined archive.

## Validation baseline

```powershell
python scripts/check-project-governance.py
dotnet build src/societies/Societies.csproj --configuration Release
dotnet test tests/Societies.Core.Tests/Societies.Core.Tests.csproj --configuration Release
godot --headless --path src/societies res://tests/HeadlessTestRunner.tscn
```

Use `tests/test-manifest.json`, the active milestone, and nearby scoped instructions to determine additional required suites. Performance and release claims require their dedicated clean-source routes.

## Handoff format

```markdown
## Outcome
- Product result:
- Owned boundary:
- Non-goals preserved:

## Repository truth
- Base branch/SHA:
- Changed files:
- Final commit:
- Working tree:

## Evidence
- Commands and results:
- Runtime observation:
- Human gate:

## Risks and limits
- Still red or unverified:
- Assumptions:

## Authority
- Active milestone state:
- Follow-on work authorized: yes/no
- Next owner decision, if any:
```

The purpose of this contract is not bureaucracy. It is to let the owner contribute primarily through vision, planning, and judgment while preventing locally impressive agent work from quietly changing the project.
