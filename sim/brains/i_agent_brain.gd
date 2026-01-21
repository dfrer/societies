## Abstract interface for agent decision-making logic
## Extend this class to create custom brains (e.g., LLM-powered)
class_name IAgentBrain
extends RefCounted

## Decide the next action for an agent given current simulation state
## Returns an action dictionary (e.g., {"type": "IDLE"})
func decide_action(agent: Agent, world: World, market: Market, 
                   contracts_system: ContractsSystem, tuning: Dictionary, 
                   recipes: Dictionary = {}, state: SimState = null) -> Dictionary:
    return Actions.idle()
