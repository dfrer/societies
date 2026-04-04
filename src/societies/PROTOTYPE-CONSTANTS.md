# Prototype Constants

Magic numbers hardcoded in the current prototype codebase that have no equivalent in `planning/meta/technical-constants.md`. These constants control the actual behavior of the running Prototype V2 M3 settlement simulation.

> **Source files**: `src/societies/scripts/simulation/PrototypeSettlementSimulation.cs`, `src/societies/scripts/core/GameManager.cs`

## Citizen Tick Constants (PrototypeSettlementSimulation.cs, ~lines 16-23)

| Constant Name | Value | Source File (approx. line) | Closest Planning Equivalent |
|---|---|---|---|
| `CitizenTravelUnitsPerTick` | `0.78f` | PrototypeSettlementSimulation.cs:16 | None |
| `MinimumTravelTicks` | `4` | PrototypeSettlementSimulation.cs:17 | None |
| `HarvestTicks` | `10` | PrototypeSettlementSimulation.cs:18 | `PRODUCE_TIME_PROCESS_RESOURCE = 5.0f` (different unit: seconds vs ticks) |
| `DepositTicks` | `4` | PrototypeSettlementSimulation.cs:19 | None |
| `EatTicks` | `10` | PrototypeSettlementSimulation.cs:20 | None |
| `SleepTicks` | `28` | PrototypeSettlementSimulation.cs:21 | None |
| `HearthBurnIntervalTicks` | `80` | PrototypeSettlementSimulation.cs:22 | None |
| `PathBuildTicks` | `6` | PrototypeSettlementSimulation.cs:23 | None |

## Structure Costs (PrototypeSettlementSimulation.cs, ~lines 25-53)

| Constant Name | Value | Source File (approx. line) | Closest Planning Equivalent |
|---|---|---|---|
| `HutCost` | timber=6, thatch=4 | PrototypeSettlementSimulation.cs:25-29 | None (building costs not defined in planning constants) |
| `StorehouseCost` | timber=8, brick=6 | PrototypeSettlementSimulation.cs:31-35 | None |
| `DryingRackCost` | timber=4, stone=2 | PrototypeSettlementSimulation.cs:37-41 | None |
| `KilnCost` | timber=4, stone=4 | PrototypeSettlementSimulation.cs:43-47 | None |
| `RemoteDepotCost` | timber=6, stone=2 | PrototypeSettlementSimulation.cs:49-53 | None |

## Hardcoded Defaults (GameManager.cs, ~lines 17-28)

| Constant Name | Value | Source File (approx. line) | Closest Planning Equivalent |
|---|---|---|---|
| `TickIntervalSeconds` | `1.0/20.0` (0.05) | GameManager.cs:17 | `TICK_INTERVAL_SECONDS = 0.05` (Section 1) |
| `DefaultScenarioId` | `"balanced_basin"` | GameManager.cs:18 | None |
| `_initialTrees` | `36` | GameManager.cs:25 | None |
| `_initialRocks` | `24` | GameManager.cs:26 | None |
| `_initialBerryBushes` | `14` | GameManager.cs:27 | None |
| `_initialWorkers` | `3` | GameManager.cs:28 | None |

## Other Hardcoded Values Found in PrototypeSettlementSimulation.cs

| Constant Name | Value | Source File (approx. line) | Closest Planning Equivalent |
|---|---|---|---|
| Central depot store capacity | `120` | PrototypeSettlementSimulation.cs:91 | `INVENTORY_SLOTS_PLAYER = 64` (different concept) |
| Navigation rules version | `1` | PrototypeSettlementSimulation.cs:74 | None |
| Slope threshold for traversable cells | `18.0f` degrees | PrototypeSettlementSimulation.cs:179 | None |

## Summary

- **22 constants** total found in prototype code with no planning equivalent
- **5 constants** with some loosely related planning entry but different semantics/units
- **17 constants** with absolutely no planning equivalent

These prototype constants should be considered the de facto values for the current running build. Any future planning document updates should reconcile with these actual implementation values.
