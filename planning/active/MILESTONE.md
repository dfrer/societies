# CONSOLIDATION-V1 — Establish the Durable Societies Starting Point

- **Status:** Integrated; owner next-product-proof selection pending
- **Activated:** 2026-08-27
- **Integrated:** 2026-08-28
- **Product feature work authorized:** No
- **Accepted runtime baseline:** `847c86b1c379e6a1dd8d4b7b641c3c89646e28c9`
- **Preservation branch:** `archive/pre-consolidation-2026-08-27`
- **Consolidation integration commit:** `54a4e5c0ea1297438b06e4b40ea14391db343657`
- **Governance workflow integration:** `420738bfc1b51cffacd94845b4e10cb9c72db081`
- **Branch-protection documentation closeout commit:** `1eaa1ab6b0c79550a99c9cad68c4ea04e9fdea75`

## Outcome

Create one durable repository and authority system from which all future Societies and Snow Globe development can proceed without rediscovering the project, following stale plans, trusting agent summaries, or requiring the owner to manage source-level decisions.

The repository integration and validation work is complete. This milestone remains active only as a deliberate feature freeze until the owner-led planning discussion selects the next bounded product proof.

## Why this remains the active milestone

The current Snow Globe direction is producing genuine progress, but the repository previously carried the failure patterns of the pre-V3 period: oversized append-only status documents, many historical plans marked active, stacked PR authority, branch sprawl, and technical infrastructure that could continue under its own momentum. The durable authority system now prevents those records from silently reactivating themselves.

No replacement product milestone has been selected yet. Keeping `CONSOLIDATION-V1` active with feature work unauthorized is more truthful than allowing the final implementation recommendation from an old plan or agent session to become the next roadmap by default.

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

- [x] The consolidation branch contains the accepted runtime and all governance changes.
- [x] The project governance and local-link checks pass.
- [x] Required product and Snow Globe laboratory validation triggered by the integration PR passes.
- [x] Consolidation PR #183 is reviewed and integrated into `master` at `54a4e5c0ea1297438b06e4b40ea14391db343657`.
- [x] Obsolete stacked PRs are closed or recognized as merged ancestry with auditable links to #183.
- [x] `planning/active/` contains only this milestone, its index, and compatibility evidence.
- [x] Audit and complete branch protection under issue #184: classic protection is active with no active rulesets; pull requests, strict/up-to-date `build-test-smoke` and `lab-tests` contexts (GitHub Actions app id `15368`), admin enforcement, conversation resolution, and no bypass actors are enforced as recorded in current state.
- [ ] The owner and planning agent explicitly select the next product proof after reviewing current state and risks.

## Integration evidence

- accepted source preserved at `archive/pre-consolidation-2026-08-27` -> `847c86b1c379e6a1dd8d4b7b641c3c89646e28c9`;
- final consolidation head `898f34220fcf106f73fb7a02b474eaaa9af729c9` was 34 commits ahead and 0 behind before merge;
- signed merge commit `54a4e5c0ea1297438b06e4b40ea14391db343657` has the old `master` and final consolidation head as parents;
- governance and patch-whitespace checks passed in both permanent hosted gates;
- complete product managed suite: 507/507;
- permanent pull-request fast gate: 387/387 with count enforcement;
- Godot headless gate: 28/28 with count enforcement;
- Snow Globe core: 1,186 passed, 5 evidence-only skips, 0 failed;
- benchmark, recording, and OpenRouter CLI suites: 56/56, 94/94, and 104/104;
- no open pull requests remain after the superseded chain closeout;
- branch-protection repair branch `chore/complete-master-protection-v1` -> PR #186 -> merge `420738bfc1b51cffacd94845b4e10cb9c72db081`;
- PR #187 became CLEAN after its unrelated template checks, then closed unmerged with its branch deleted; PR #188 became CLEAN after its harmless lab README checks, then closed unmerged with its branch deleted;
- branch-protection documentation closeout PR #189 -> commit `c0a425dca97c59f305622cbda5ae27a36d66ef49` -> master closeout commit `1eaa1ab6b0c79550a99c9cad68c4ea04e9fdea75`.

## Stop conditions

Stop and surface the conflict if:

- the accepted source differs from `847c86b1c379e6a1dd8d4b7b641c3c89646e28c9` without an explicit decision;
- consolidation requires guessing about unpushed local work;
- a documentation move would discard or overwrite unique evidence;
- CI exposes a runtime regression in the accepted stack;
- the next feature is being selected merely because an agent recommends adjacent work.

## Next owner decision

Discuss whether the next bounded proof should first:

1. reconcile and lock the visual/product target around the accepted EB-01R worldcraft base;
2. prove one participating citizen through the gameplay-facing cognition interface;
3. address the known performance/collision architecture before adding citizens; or
4. use a deliberately coupled slice that proves the minimum of these together.

The planning discussion must explain tradeoffs and give a recommendation. The owner should not be asked to choose source-level implementation details.
