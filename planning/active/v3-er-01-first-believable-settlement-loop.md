# ER-01 First Believable Settlement Loop

## Outcome contract

- **Player result:** one 8-10 minute route from a curated start through harvesting, central-depot contribution, a deliberate two-policy wetland choice, one readable citizen interest, and one shared wetland consequence.
- **Owned boundary:** catalog profile metadata plus Godot input/presentation adapters. `PrototypeRuntimeSession` remains the only owner of resources, contributions, civic policy, citizen interests, wetland state, event history, persistence, and replay.
- **Non-goals:** W3-06+, former Week 4 work, general governance, providers/Snow Globe, markets, multiplayer, combat, new simulation state, schema changes, and a generalized UI framework.
- **Value gate:** normal play leads with one need, one contextual prompt, concise authoritative feedback, and a deliberate policy surface; debug metrics are optional.
- **Delivery boundary:** reviewed technical candidate with authoritative local validation and clean-commit performance evidence. Draft-PR delivery remains before handoff; user-led play acceptance remains required before ER-01 can be called complete.

## Implemented design

The existing `wetland_builder` and `empty_stores` scenarios are the only curated ER-01 profiles:

| Profile | Scenario and seed | Resource approach | Immediate pressure | World cue |
|---|---|---|---|---|
| Marsh Recovery | `wetland_builder`, `8192` | Harvest reeds; contribute | Shelter needs thatch now | Wetter ground; dense reeds |
| Lean Stores | `empty_stores`, `1701` | Harvest berries; contribute | Meals and hearth fuel are low | Drier ground; sparse reeds |

They use the existing catalog and seed route; no presentation-local world data or randomizer exists.
`GameManager` exposes the HUD only an immutable option projection containing scenario identity and
profile display fields; mutable scenario, world-generation, stock, crisis, and seed definitions remain
inside the catalog/runtime boundary.

- The normal HUD presents a compact goal, world cue, one citizen's derived material interest, interaction prompt, action feedback, two profile choices, and two civic buttons. After selection, the same read-only citizen projection says whether that citizen supports or opposes the choice and why; no UI state is stored.
- Both civic buttons and the `4`/`5` shortcuts emit the same player intent. `GameManager` rejects it until the authoritative contribution total is positive, then delegates to the existing `SelectCivicPolicy` command. The runtime remains the sole policy and consequence authority.
- F1 exposes the existing inventory, world, settlement, inspector, crisis, and debug readings as an optional diagnostic layer.
- A resource's focus highlight and the depot focus ring are presentation-only. Harvest/contribution success, rejection, and depletion wording is calculated from existing command results and runtime projections.

## Manual acceptance routes

### Marsh Recovery

1. Start **Marsh Recovery** and read its Need, wetter-ground/dense-reed cue, and one citizen's material interest; locate a reed bed at the waterline.
2. Focus the reed bed, harvest it with **E**, then find the highlighted central depot and contribute with **E**.
3. Read the two policy buttons before choosing either one; no developer key or diagnostic view is required.
4. Confirm the result card says whether the named citizen supports or opposes the choice, gives the material reason, and shows the changed wetland health.
5. Restart and choose the other policy; state the tradeoff in your own words.

### Lean Stores

1. Start **Lean Stores** and identify the low-stock pressure plus the drier-ground/sparse-reed world cue.
2. Harvest berries, contribute at the central depot, then make either wetland choice through the visible buttons.
3. Confirm that the starting pressure, resource route, and world cue differ from Marsh Recovery while the same interaction/authority rules hold.
4. Record whether the goal, interaction, citizen response, choice, and consequence were understandable without a developer key list.

## Validation boundary

Focused automated checks cover generated wetness/biome/resource contrasts, starting pressure, same-profile replay of harvest/contribution/civic commands, contribution-gated button/key intent, the immutable profile-option surface, in-tree focus and post-depletion safety, normal citizen response copy, optional diagnostics, and conservative 1280x720 goal/profile/civic fit and collision budgets at the live 17px goal and default 16px button fonts. They do not establish rendered visual quality or user acceptance; that requires the manual routes above.

The final authoritative local wrapper passes 489/489 managed tests and 26/26 Godot tests. Production Release and ExportRelease builds both complete with zero warnings and zero errors. Independent deep review is GO with no P0-P3 findings. These checks prove the bounded command ownership, replay, interaction-state, and layout contracts; they do not prove that the route feels believable or that the rendered hierarchy is acceptable.

The clean canonical Release matrix at implementation commit `f6eab718a2e8514a3ed46d5819a1a1fbe00db000` passes all 14/14 correctness/evidence contracts and the hard safety gate. The reference median p95/max is `48.5517/169.351 ms`; both soaks are safe and deterministic; forced invalidation is correct at `21.9172 ms`. The overall budget is still `target_missed`, and non-gating 24-citizen stress remains characterization-red at `152.0242/189.584 ms`. This is safety evidence, not a performance-target claim.
