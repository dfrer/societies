# CONSOLIDATION-V1 — Establish the Durable Societies Starting Point

- **Status:** Active
- **Activated:** 2026-08-27
- **Product feature work authorized:** No
- **Accepted runtime baseline:** `847c86b1c379e6a1dd8d4b7b641c3c89646e28c9`
- **Preservation branch:** `archive/pre-consolidation-2026-08-27`

## Outcome

Create one durable repository and authority system from which all future Societies and Snow Globe development can proceed without rediscovering the project, following stale plans, trusting agent summaries, or requiring the owner to manage source-level decisions.

## Why this is the active milestone

The current Snow Globe direction is producing genuine progress, but the repository still carries the failure patterns of the pre-V3 period: oversized append-only status documents, many historical plans marked active, stacked PR authority, branch sprawl, and technical infrastructure that can continue under its own momentum. Continuing feature work before correcting that operating system would make the present convergence fragile.

## Scope

1. Preserve the exact accepted EB-01R state.
2. Make the accepted stack the single candidate integration path to `master`.
3. Establish one authority hierarchy and one active milestone.
4. Replace oversized root status ledgers with concise maintained documents.
5. Archive historical plans and reports without deleting evidence.
6. Define product/runtime/lab/planning/test ownership through scoped agent instructions.
7. Add automated governance, product, and laboratory checks plus a PR contract.
8. Record current architecture, history, risks, decisions, and unproven product claims.
9. Close or supersede obsolete stacked PRs after the consolidation PR is safely available.
10. Hold a separate owner-led planning discussion to select the next bounded product proof.

## Non-goals

- no new gameplay features;
- no provider expansion, paid calls, routing generations, or model comparisons;
- no broad code rewrite or cosmetic source movement;
- no claim that EB-01R is a functioning society;
- no resolution of performance, accessibility, or release gates by documentation;
- no automatic activation of F1, F2, EB-02, citizen integration, or any old roadmap label.

## Exit gates

- [ ] The consolidation branch contains the accepted runtime and all governance changes.
- [ ] The project governance and local-link checks pass.
- [ ] Required product and Snow Globe lab validation triggered by the integration PR passes.
- [ ] The consolidation PR is reviewed and integrated into `master`.
- [ ] Obsolete stacked PRs are closed as superseded with an auditable link to the consolidation PR.
- [ ] `planning/active/` contains only this milestone, its index, and compatibility evidence.
- [ ] Branch protection is enabled; until the integration can configure it, issue #184 remains the explicit manual administrative blocker.
- [ ] The owner and planning agent explicitly select the next product proof after reviewing current state and risks.

## Evidence required

- exact base and final commit identities;
- repository governance and local-link check output;
- full triggered GitHub Actions results for product and laboratory paths;
- diff review showing runtime behavior was not altered by consolidation-specific commits;
- final open-PR and authority-state report;
- owner decision for the next milestone, recorded in a new active plan.

## Stop conditions

Stop and surface the conflict if:

- the accepted head differs from `847c86b1c379e6a1dd8d4b7b641c3c89646e28c9` without an explicit decision;
- consolidation requires guessing about unpushed local work;
- a documentation move would discard or overwrite unique evidence;
- CI exposes a runtime regression in the accepted stack;
- the next feature is being selected merely because an agent recommends adjacent work.

## Next owner decision

After repository integration, discuss whether the next bounded proof should first:

1. reconcile and lock the visual/product target around the accepted EB-01R worldcraft base;
2. prove one participating citizen through the gameplay-facing cognition interface;
3. address the known performance/collision architecture before adding citizens; or
4. use a deliberately coupled slice that proves the minimum of these together.

The planning discussion must explain tradeoffs and give a recommendation. The owner should not be asked to choose source-level implementation details.
