# Active Development Plans

Documents in this directory are short-horizon execution plans grounded in the authoritative Godot build. They take precedence over older aspirational planning documents for their stated execution window, but they do not override the code or `CURRENT_BUILD.md` as statements of what is already implemented.

## Current Plan

The July 13-24, 2026 execution window is historical. W3-01 through W3-05 were later delivered as bounded engineering slices, but both ER-01 and its HUD/interaction recovery failed the product bar. The active path now defines and rebuilds the player-facing Snow Globe experience before any further feature expansion.

- **Stopped/Closed:** [V3 two-week development plan](v3-two-week-development-plan.md) — July 13–24, 2026; W2-06 concluded Stop Feature Expansion with required gates unmet.
- **Active planning reset:** [Snow Globe Frontend Product Recovery Plan](v3-snow-globe-frontend-recovery-plan.md) — F0 product definition, F1 visual target, then a replacement Golden Three/Golden Fifteen presentation path.
- **Superseded/Failed:** [V3 Experience Recovery plan](v3-experience-recovery-plan.md) — ER-01 and its recovery refined the old demo without reaching the required product quality.
- **Draft/Conditional historical plan:** [V3 Weeks 3-4 development plan](v3-weeks-3-4-development-plan.md) - W3-01 through W3-05 are historical deliveries; W3-06+ and the former Week 4 path remain inactive.

The engineering evidence remains valid, but it does not establish a satisfying playable experience. No gameplay implementation is active until F0 defines the player fantasy and F1 establishes an accepted in-engine visual target. The reset does not authorize live providers, broad governance, markets, multiplayer, or unbounded content production. See [the product thesis](../PRODUCT-THESIS.md), [CURRENT_BUILD.md](../../CURRENT_BUILD.md), and the [recovery plan](v3-snow-globe-frontend-recovery-plan.md).

## Status Convention

- **Ready** — agreed scope, not yet started
- **Active** — currently being executed
- **Blocked** — cannot proceed without a named dependency or decision
- **Complete** — exit gates passed and results recorded
- **Stopped/Closed** — decision recorded with one or more required gates unmet; continuation requires an explicit new decision
- **Superseded** — replaced by a newer plan
- **Draft/Conditional** - proposed follow-on scope; it has an explicit activation decision and is not executable yet

When this plan finishes, record its result in the plan itself before moving it to `planning/archives/`.
