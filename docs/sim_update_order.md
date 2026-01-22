# Simulation Update Order

This document defines the canonical execution order of systems within the Simulation loop.

## Overview

The simulation `Sim._tick()` executes a pipeline of `ISimSystem` components. Each system implements a `tick(sim, dt)` method and optionally a `tick_daily(sim)` method.

## Execution Pipeline

The systems are executed in the following strict order:

1.  **EnvironmentSystem**
    *   **Daily**: Regenerates resources (berries, trees) based on pollution-modified rates. Decays pollution.
    *   **Tick**: (No-op)

2.  **EconomySystem**
    *   **Daily**: Generates new contracts based on economic and environmental conditions.
    *   **Tick**: Expired orders and contracts are processed. *Note: Order expiration happens before matching.*

3.  **JobBoardSystem**
    *   **Daily**: Posts new gather/contract activities based on resource nodes and contracts.
    *   **Tick**: Refreshes job availability and prunes inactive activities.

4.  **GovernanceSystem**
    *   **Daily**: Updates factions (formation, recruitment, territory expansion, voting).
    *   **Tick**: (No-op)

5.  **MetricsSystem**
    *   **Daily**: Captures a snapshot of simulation state for history/analysis.
    *   **Tick**: (No-op)

6.  **TimeSystem**
    *   **Tick**: Increments `state.tick`.

7.  **WorkshopSystem**
    *   **Tick**: Processes active crafting jobs in workshops.

8.  **TaskProjectSystem**
    *   **Tick**: Advances communal projects (build completion/abandonment) before agents act.

9.  **AgentsSystem**
    *   **Tick**: Updates agent hunger (pollution-affected). Agents decide and execute actions (move, harvest, craft, trade).

10. **EconomyResolutionSystem**
    *   **Tick**: Executes market matching (matches buy/sell orders). *Executed post-agent to maintain legacy behavior where agents act before trades clear.*

## Rationale for Split Economy

The **Economy** logic is split into `EconomySystem` (Pre-Agent) and `EconomyResolutionSystem` (Post-Agent) to preserve the original monolithic execution order:
- **Pre-Agent**: Contract generation and Order expiration must happen before agents decide actions so they react to valid state.
- **Post-Agent**: Market matching happens after all agents have placed orders for the tick, ensuring fair batch clearing (or maintaining the specific legacy timing).
