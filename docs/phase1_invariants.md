# Phase 1 Invariants

This document defines the "must remain true" behaviors for the Phase 1 refactor of the Societies simulation.
Any violation of these invariants constitutes a regression.

## 1. Determinism
The simulation must be strictly deterministic.

*   **Same Seed Invariance**: Running the simulation with the same seed and input parameters must produce bit-exact identical output for every tick.
    *   *Metric*: Checksum of the simulation state (SimState hash) at the end of the run.
    *   *Metric*: Daily snapshots of global metrics (e.g., total money, population).
*   **Platform Independence (Target)**: ideally deterministic across runs on the same machine.
*   **Save/Load Invariance**: Saving the simulation state and loading it back must result in a state that produces identical future updates compared to a run that continued without saving. (Already partially covered by `test_save_load.gd`, but critical to maintain).

## 2. Core Gameplay Systems
The following systems must function and produce observable changes in the world state:

*   **Agent Lifecycle**:
    *   Agents spawn and eventually die (starvation/old age/leaving).
    *   Agents consume food (hunger increases, hunger > threshold -> health loss).
*   **Economy**:
    *   **Resources**: Agents gather resources (Berries, Logs, Iron, Stone) from tiles.
    *   **Crafting**: Agents convert raw resources into products (Axes, Pickaxes, etc.) at workshops.
    *   **Market**: Agents place Buy/Sell orders. Trades execute when prices match. Money exchanges hands.
*   **Factions & Governance**:
    *   Factions form when grievance is high.
    *   Factions claim territory.
    *   Factions collect taxes (sales tax, fines).
*   **Ecology**:
    *   Extraction activities generate pollution.
    *   Pollution reduces tile regeneration rates.

## 3. Constraints
*   **No New Features**: Phase 1 is strictly for establishing a baseline and restructuring. No new mechanics should be added.
*   **Constants**: Economic constants (prices, fines, drain rates) should remain unchanged unless explicitly moved to `config/tuning.json` without value modification.

## 4. Baseline Metrics
The baseline run must record the following for comparison:
*   Total Population
*   Total World Wealth (Money + Value of Inventory)
*   Average Pollution
*   Number of Factions
*   Number of Market Trades per Day
