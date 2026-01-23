## Recipe - defines a crafting recipe from JSON
class_name Recipe
extends RefCounted

var id: String = ""
var name: String = ""
var station: String = "workshop"  # Required station type
var inputs: Dictionary = {}  # item_name -> qty
var outputs: Dictionary = {}  # item_name -> qty
var fuel: Dictionary = {}  # item_name -> qty (options)
var ticks: int = 30
var tier: String = "basic"

func _init() -> void:
	pass

## Calculate total input cost based on market prices
func get_input_cost(market: Market) -> float:
	var total := 0.0
	for item in inputs:
		var qty: int = inputs[item]
		var price: float = market.get_ref_price(item)
		total += price * qty
	for item in fuel:
		var qty: int = fuel[item]
		var price: float = market.get_ref_price(item)
		total += price * qty
	return total

## Calculate output value based on market prices
func get_output_value(market: Market) -> float:
	var total := 0.0
	for item in outputs:
		var qty: int = outputs[item]
		var price: float = market.get_ref_price(item)
		total += price * qty
	return total

## Check if recipe is profitable at given margin
func is_profitable(market: Market, margin: float) -> bool:
	var cost := get_input_cost(market)
	var value := get_output_value(market)
	return value >= cost * margin

## Serialize to dictionary
func to_dict() -> Dictionary:
	return {
		"id": id,
		"name": name,
		"station": station,
		"inputs": inputs.duplicate(),
		"outputs": outputs.duplicate(),
		"fuel": fuel.duplicate(),
		"ticks": ticks,
		"tier": tier
	}

## Deserialize from dictionary
static func from_dict(d: Dictionary) -> Recipe:
	var recipe := Recipe.new()
	recipe.id = d.get("id", "")
	recipe.name = d.get("name", "")
	recipe.station = d.get("station", "workshop")
	recipe.tier = d.get("tier", "basic")
	# Convert inputs values to int (JSON may parse as float)
	var inp: Dictionary = d.get("inputs", {})
	recipe.inputs = {}
	for key in inp:
		recipe.inputs[key] = int(inp[key])
	var fuel_inputs: Dictionary = d.get("fuel", {})
	recipe.fuel = {}
	for key in fuel_inputs:
		recipe.fuel[key] = int(fuel_inputs[key])
	# Convert outputs values to int
	var outp: Dictionary = d.get("outputs", {})
	recipe.outputs = {}
	for key in outp:
		recipe.outputs[key] = int(outp[key])
	recipe.ticks = int(d.get("ticks", 30))
	return recipe
