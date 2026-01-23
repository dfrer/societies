## Bundles all context needed by planners to avoid long parameter lists
class_name PlannerContext
extends RefCounted

var world: World
var market: Market
var contracts_system: ContractsSystem
var tuning: Dictionary
var recipes: Dictionary
var state: SimState

static func create(world: World, market: Market, contracts_system: ContractsSystem,
		tuning: Dictionary, recipes: Dictionary, state: SimState) -> PlannerContext:
	var ctx := PlannerContext.new()
	ctx.world = world
	ctx.market = market
	ctx.contracts_system = contracts_system
	ctx.tuning = tuning
	ctx.recipes = recipes
	ctx.state = state
	return ctx
