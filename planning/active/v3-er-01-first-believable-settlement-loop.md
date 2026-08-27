# ER-01 First Believable Settlement Loop

## Outcome contract

- **Player result:** one 8-10 minute route from a curated start through harvesting, central-depot contribution, a deliberate two-policy wetland choice, one readable citizen interest, and one shared wetland consequence.
- **Owned boundary:** catalog profile metadata plus Godot input/presentation adapters. `PrototypeRuntimeSession` remains the only owner of resources, contributions, civic policy, citizen interests, wetland state, event history, persistence, and replay.
- **Non-goals:** W3-06+, former Week 4 work, general governance, providers/Snow Globe, markets, multiplayer, combat, new simulation state, schema changes, and a generalized UI framework.
- **Value gate:** normal play leads with one need, one contextual prompt, concise authoritative feedback, and a deliberate policy surface; debug metrics are optional.
- **Delivery boundary:** reviewed HUD/interaction recovery candidate delivered in draft PR #177 with authoritative local validation and clean-commit performance evidence. The draft must not merge until required CI is green, the recorded performance safety failure is resolved or explicitly dispositioned, and user-led play acceptance passes; ER-01 cannot be called complete before those gates.

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

- The normal HUD is an organic field board rather than a diagnostic wall: one compact settlement need, one center-bottom contextual interaction, one separate authoritative result, and a three-step `GATHER / DEPOT / DECIDE` rail derived from read-only runtime facts. Profile controls and diagnostics stay behind F1.
- The two civic buttons remain hidden until a real contribution exists. When the decision opens, the cursor is released and the player can choose `[4] Protect wetland` or `[5] Draw down wetland` from a readable tradeoff surface. Escape closes or reopens the decision without competing input owners.
- Both civic buttons and the `4`/`5` shortcuts emit the same player intent. `GameManager` rejects it until the authoritative contribution total is positive, then delegates to the existing `SelectCivicPolicy` command. The runtime remains the sole policy and consequence authority.
- F1 exposes the existing inventory, world, settlement, inspector, crisis, and debug readings as an optional diagnostic layer.
- Resource focus now distinguishes visible-but-too-far, actionable, rejected, successful, and depleted states; the depot uses the same presentation language. The ray may identify a resource at 7 m, but harvesting remains limited to 4.5 m and out-of-range input is rejected before it reaches the runtime. Current focus and previous action result are separate cues so stale success cannot recolor a blocked prompt.

## Manual acceptance routes

### Marsh Recovery

1. Start **Marsh Recovery** and read its Need, wetter-ground/dense-reed cue, and one citizen's material interest; locate a reed bed at the waterline.
2. Focus the reed bed, harvest it with **E**, then find the highlighted central depot and contribute with **E**.
3. After the first real contribution, use the released cursor to read and click either policy. Number keys remain optional shortcuts.
4. Confirm the result card says whether the named citizen supports or opposes the choice, gives the material reason, and shows the changed wetland health.
5. Restart and choose the other policy; state the tradeoff in your own words.

### Lean Stores

1. Start **Lean Stores** and identify the low-stock pressure plus the drier-ground/sparse-reed world cue.
2. Harvest berries, contribute at the central depot, then make either wetland choice through the visible buttons.
3. Confirm that the starting pressure, resource route, and world cue differ from Marsh Recovery while the same interaction/authority rules hold.
4. Record whether the goal, interaction, citizen response, choice, and consequence were understandable without a developer key list.

## User-led acceptance result

**Fail — 2026-08-26.** The user reported: “Its looking okay, still needs massive work,” followed by “all of those are weak but the HUD and ineractions are probably the weakest.” This fails all five required gates: agency, interaction quality, causal clarity, HUD hierarchy, and meaningful variation. HUD hierarchy and interaction quality are the priority defects.

The technical evidence remains valid, but ER-01 is not accepted and draft PR #177 must remain unmerged. The authorized narrow HUD/interaction recovery revision is now implemented; the prior failure remains authoritative until the user replays both profiles and records a new result.

### Recovery replay route

From the isolated worktree, launch the Mono build in PowerShell with:

```powershell
& "$env:LOCALAPPDATA\Microsoft\WinGet\Packages\GodotEngine.GodotEngine.Mono_Microsoft.Winget.Source_8wekyb3d8bbwe\Godot_v4.6.2-stable_mono_win64\Godot_v4.6.2-stable_mono_win64.exe" --path src/societies
```

1. Launch the project and play **Marsh Recovery** with F1 closed. Confirm the field board makes the immediate need and next interaction clear.
2. Aim at a reed from outside interaction range, move close enough for `[E] Harvest`, harvest until its depleted state is visible, then contribute at the depot.
3. Confirm the decision surface opens, choose either policy with the mouse, and read the citizen and wetland consequence.
4. Press F1, switch to **Lean Stores**, close diagnostics, and repeat with berries while choosing the opposite policy.
5. Judge agency, interaction quality, causal clarity, HUD hierarchy, and meaningful variation. Automated evidence cannot answer those questions.

## Validation boundary

Focused automated checks cover both generated profile contrasts, replay, harvest/contribution/civic command ownership, the contribution-gated mouse/key decision, independent settlement-step derivation, near/far/depleted interaction states, separation of current prompt from prior result, optional diagnostics, Escape ownership, and conservative 1280x720 hierarchy bounds. They do not establish rendered visual quality, real OS cursor feel, or user acceptance; that requires the manual routes above.

The recovery-focused managed selection passes 30/30. The first full recovery wrapper exposed one real Escape regression after 491/491 managed tests: Godot was 25/26. After centralizing Escape ownership in `GameManager`, the final authoritative wrapper passes 491/491 managed tests and 26/26 Godot tests. Production Release and ExportRelease builds both complete with zero warnings and zero errors. Independent deep review is GO with no P0-P3 findings. These checks prove the bounded command ownership, replay, interaction-state, and layout contracts; they do not prove that the route feels believable or that the rendered hierarchy is acceptable.

The clean canonical Release matrix at implementation commit `d73c4e45bb18289852ee21347549e564d1f36063` completes all 14/14 deterministic correctness/evidence pairs, but its budget status is `safety_failure`. The reference median p95 is `51.9392 ms` against the `50 ms` safety threshold; median maximum is `191.8185 ms` against `250 ms`. Both soaks remain safe and deterministic at p95 `49.6365/43.3379 ms`; forced invalidation is correct at `24.0009 ms`. Non-gating 24-citizen stress remains characterization-red at `153.159/195.5762 ms`. No causal attribution to this UI pass is claimed, and the canonical run was not repeated to chase a greener sample.

Draft PR #177 remains deliberately draft and unmerged. Its exact pushed recovery/evidence head and required CI result are recorded at delivery time; neither automated evidence nor CI can replace the two-profile user replay above.
