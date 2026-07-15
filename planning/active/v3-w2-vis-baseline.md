# V3-W2-VIS Visual and representational baseline

## Outcome contract

- Outcome: five deterministic `empty_stores` reference frames make the crisis, player contribution, citizens, settlement state, and terminal outcome legible before observed playtests.
- Owned boundary: capture/reproduction tooling, reference evidence, headless representation and HUD checks, and baseline documentation. This does not create production art, change simulation rules, or activate Weeks 3-4.
- Value gate: player legibility and reproducible visual-review evidence.
- Acceptance: the capture manifest identifies the build and every fixed presentation input; all five named frames exist; pure HUD checks prove readable non-overlapping 1920x1080 and 1280x720 layouts; ordinary play text exposes the crisis, time, directive, contributions, conditions, inspection reason, and terminal outcome.
- Evidence: `scripts/capture-v3-w2-vis.ps1`, the Godot headless suite, `planning/active/evidence/v3-w2-vis-baseline-validation.json`, and tracked frames under `docs/evidence/v3-w2-vis/<sha>/`.
- Delivery boundary: local evidence only. A green baseline requires a clean-build capture and the representative `empty_stores` performance pair; neither may be inferred from historical timing evidence.

## Canonical capture contract

| Input | Locked value |
|---|---|
| Scenario / seed | `empty_stores` / `1701` |
| Simulation ticks / hours | `arrival`, `settlement_overview`, and `contribution_point` are captured at tick `0` / hour `10.5`; `citizen_inspection` advances authoritatively to tick `1`; `terminal_crisis` advances to the authoritative `PrototypeVisualCaptureConfiguration.TerminalCrisisTick` recorded in the manifest. The 10.5 terminal provenance is tick `9777`, `Collapsed` / `IncapacitatedHold`, `8148` events, SHA-256 `69f3e22402e31a53b1d4c16899883956fcc5fdb14fbe47d8a4eb8baef007174f`. Every frame records both the derived simulation hour and the active day-length/tick-interval inputs that produced it. |
| Initial simulation hour | `10.5` |
| Presentation lighting hour | locked at `10.5` |
| Terminal simulation hour | deterministically derived by authoritative advancement to the manifest-recorded `TerminalCrisisTick`; it is not locked to the initial hour |
| Lighting multiplier | `1.0` |
| Debug overlays | Disabled; HUD remains enabled |
| Resolution | Capture output records the actual viewport; review targets are 1920x1080 and 1280x720 |
| Graphics | The capture manifest records Godot's active runtime renderer method plus the project renderer setting (`engine_default` when no explicit override exists); `reproduction.json` preserves that exact runtime record rather than claiming generic defaults. |

The public `GameManager` capture API is the only state/camera driver: configure before entering the tree, apply the canonical scenario, advance with authoritative ticks, then select the named preset. It prevents a screenshot script from becoming an alternate simulation authority.

| Frame | Preset | Required visible reading |
|---|---|---|
| Arrival | `arrival` | first-person approach, normal crisis HUD, distinguishable settlement silhouettes |
| Settlement overview | `settlement_overview` | observer view of huts, depot, planned corridor segments, queued construction, citizens, loose resources |
| Contribution point | `contribution_point` | player-facing depot/stockpile contribution point at the recorded in-range player/depot pose, with a visible `Contributed` success cue |
| Citizen inspection | `citizen_inspection` | selected citizen, role/needs/order reason, directive cue |
| Terminal crisis | `terminal_crisis` | terminal `Collapsed` / `IncapacitatedHold` outcome and its causal crisis summary at the manifest-recorded authoritative terminal tick |

## Representation matrix

| Subject | Placeholder convention | State cue | Intended next-milestone treatment |
|---|---|---|---|
| Citizens | upright, phase-tinted placeholder bodies with readable spacing | active phase color; inspector supplies needs and `Why:` reason | replace meshes/animation; keep stable selection and readable state signaling |
| Huts | plain warm box markers | built versus planned/queued marker | replace mesh/material only; retain footprint and state signaling |
| Depot / stockpile | central crates around the hearth hub | contribution-point sign/light, stock count label | retain central authority location and interaction affordance |
| Loose resources | small ground props with resource colors | distinct logs, stone, reeds, berries silhouettes | replace models; preserve category color/icon mapping |
| Path corridors | every authoritative segment retains a stable cue; translucent cool `PLANNED PATH` versus opaque warm `BUILT PATH` | the tick-zero reference overview truthfully shows planned segments; completed segments switch to the distinct built convention | retain stable segment identity and planned/built contrast; replace geometry only if needed |
| Queued construction | planned marker / incomplete silhouette | planned or blocked label/cue | retain queue and blocked semantics, replace temporary marker |
| Interactable objects | local prompt near usable target | contribution success versus blocked/out-of-range text and cue color | retain prompt hierarchy; replace iconography as assets arrive |
| Crisis / directive / terminal | procedural HUD panel | Neutral, Food & Fuel, Shelter, Stable, Collapsed, blocked, and success have distinct cue mapping | retain wording/state hierarchy; production styling deferred |

## Scale and visual rules

- Ground-scale objects use a human-height citizen as the reference; current huts are intentionally plain box markers, while the depot reads at distance through its crate silhouette.
- Use warm settlement/hearth tones against cooler terrain, directive colors distinct from terminal-state colors, and no debug-only color dependency.
- Material contrast is more important than surface detail: path, resource, construction, and contribution surfaces must remain separable under locked 10:30 lighting.
- Crisis panel is the normal-view source for remaining time, directive, contributions, four conditions, holds, and terminal outcome. Inspector text carries the citizen reason; it is not a debug overlay.
- Responsive HUD cards are bounded and non-overlapping at 1920x1080 and 1280x720; card text may compact but may not lose the required state fields.

## Replacement map

| Element | Temporary or retained | Replacement rule |
|---|---|---|
| Primitive citizens, huts, crate/resource props, queued markers | Temporary | Replace with authored art only when the visual cue and footprint remain testable in the reference route. |
| Planned/built path corridor contrast and stable segment cues, depot location, contribution prompt, citizen selection, crisis state text, HUD information hierarchy | Retained | Preserve semantics and named capture presets through the next milestone. |
| Procedural lighting/material palette | Temporary baseline | Art pass may change style after playtest evidence, but must re-capture all five frames. |

## Capture, reproduction, and artifact locations

Use a known Godot 4.6.2 .NET executable explicitly; this avoids an unversioned PATH dependency.

```powershell
.\scripts\capture-v3-w2-vis.ps1 -GodotPath "C:\tmp\godot-4.6.2-mono\Godot_v4.6.2-stable_mono_win64\Godot_v4.6.2-stable_mono_win64.exe"
```

The script rejects a dirty source tree, explicitly rebuilds the current `HEAD` managed project in `Debug` before launch, verifies and SHA-256 hashes Godot's Debug managed assembly, and records the build command/result/configuration/hash in `reproduction.json`. The canonical GUI capture is validated at build `2393e884b902d54b50f54d8dfcd966f9bbae11f0` under `docs/evidence/v3-w2-vis/2393e884b902d54b50f54d8dfcd966f9bbae11f0/`: schema-4 manifest, exact 1920x1080 `forward_plus`, five PNGs, and reproduction record. The managed Debug assembly hash is `7fb5b1b3...` (full value in the manifest/reproduction record), terminal trace is tick `9777` hash `69f3e224...`, and the capture script exits 0 while removing scratch state. Temporary `.capture-work` compiler state is deleted on both success and failure. `-Headless` is only a transport diagnostic: its output cannot be marked reviewed visual evidence.

Representative timing is separately reproducible with:

```powershell
.\scripts\run-performance-pair.ps1 -GodotPath "C:\tmp\godot-4.6.2-mono\Godot_v4.6.2-stable_mono_win64\Godot_v4.6.2-stable_mono_win64.exe" -Scenario empty_stores -Seed 1701 -Citizens 12 -Ticks 300 -CacheMode cold -AllowDirtySource
```

That is an explicitly non-baseline editor characterization when source is dirty. A green V3-W2-VIS performance claim requires a clean applicable W1-03 safety comparison; record both p95 and maximum, not only the median.

## Visual-vs-rules-vs-pacing confusion taxonomy

| Report classification | Record when | Example |
|---|---|---|
| Visual | Player cannot see, distinguish, find, or decode a state despite correct rules | depot versus crate ambiguity; queued hut indistinguishable from built hut |
| Rules | Player sees the cue but cannot predict the deterministic consequence or command rule | unclear which items can contribute; directive does not explain assignment change |
| Pacing | Player understands the state and rule but timing/progression feels too fast, slow, or idle | terminal pressure or work completion timing feels unfair |

Observed playtest notes must record one primary category, screenshot/preset if applicable, scenario tick, and confidence. Do not classify a visual issue as a balance or simulation-rule defect without evidence.

## Status reconciliation

W2-04 merged through PR #117 at master `d519d4d`. V3-W2-VIS implementation and full test gates are green: Release .NET 269/269 (0 failed/skipped), Debug build 0 warnings/errors, Godot 22/22, and deep review zero P0-P3 before rendered evidence; subsequent capture fixes retained a green Debug build and Godot 22/22. The canonical GUI capture is complete at build `2393e884b902d54b50f54d8dfcd966f9bbae11f0`, with five tracked PNGs, schema-4 manifest, reproduction record, terminal tick `9777` trace `69f3e224...`, assembly hash `7fb5b1b3...`, exact 1920x1080 `forward_plus`, and capture exit 0. Rendered-frame review found product cues legible; dark wedges appear consistent with placeholder heightfield/directional-light shading and remain a visual residual for human confirmation. The milestone remains **NOT complete** because representative `empty_stores` ReleaseExport W1-03 safety evidence is blocked: ReleaseExport templates are in protected user AppData and access is denied under the current session approval limit, while the crisis-active Debug pair deliberately refuses schema-v6 snapshot artifacts until W2-05. A balanced_basin 12c/300 Debug dirty-source smoke is characterization only (deterministic hash `fd518fe0...`, p95 off/on `55.9687`/`51.2184` ms; max off `667.0811` ms) and is not W1-03 evidence. Do not activate W2-05 or Weeks 3-4. The Weeks 3-4 plan remains Draft/Conditional and inactive.
