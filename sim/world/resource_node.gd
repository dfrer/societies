## ResourceNode - represents a harvestable resource location
class_name ResourceNode
extends RefCounted

## Unique identifier
var id: int = 0

## Resource type: "berry", "tree", "ore"
var type: String = ""

## Position in world
var pos_x: int = 0
var pos_y: int = 0

## Current stock and capacity
var stock: int = 0
var max_stock: int = 10

## Perishability system
var spoil_rate: float = 0.0  # per day
var quality: float = 1.0
var last_decay_tick: int = 0

## Used for generating unique IDs - REMOVED for determinism
# static var _next_id: int = 1

func _init() -> void:
	pass


## Check if node has at least this much stock
func has_stock(amount: int) -> bool:
	return stock >= amount

## Add stock (capped at max_stock)
func add_stock(amount: int) -> void:
	stock = mini(stock + amount, max_stock)

## Remove stock and return amount actually removed
func remove_stock(amount: int) -> int:
	var removed := mini(amount, stock)
	stock -= removed
	return removed

## Get the item type name for this resource
func get_item_type() -> String:
	match type:
		"berry":
			return "Berries"
		"tree":
			return "Logs"
		"ore":
			return "Ore"
		"stone":
			return "Stone"
		_:
			return type.capitalize()

## Apply decay based on time passed
func apply_decay(tick: int, ticks_per_day: int) -> void:
	if spoil_rate <= 0.0 or last_decay_tick >= tick:
		return
	
	var days_passed = float(tick - last_decay_tick) / float(ticks_per_day)
	var decay_amount = stock * spoil_rate * days_passed
	stock = maxi(0, int(stock - decay_amount))
	last_decay_tick = tick

## Set spoil rate based on resource type
func set_type_spoilage() -> void:
	match type:
		"berry":
			spoil_rate = 0.33  # Rot in ~3 days
		"tree":
			spoil_rate = 0.14  # Decay in ~7 days
		"ore":
			spoil_rate = 0.0   # Permanent
		"stone":
			spoil_rate = 0.0   # Permanent
		_:
			spoil_rate = 0.0

## Get effective stock with quality multiplier
func get_effective_stock() -> int:
	return int(float(stock) * quality)

## Serialize to dictionary
func to_dict() -> Dictionary:
	return {
		"id": id,
		"type": type,
		"pos_x": pos_x,
		"pos_y": pos_y,
		"stock": stock,
		"max_stock": max_stock,
		"spoil_rate": snappedf(spoil_rate, 0.00000001),
		"quality": snappedf(quality, 0.00000001),
		"last_decay_tick": last_decay_tick
	}

## Deserialize from dictionary
static func from_dict(d: Dictionary) -> ResourceNode:
	var node := ResourceNode.new()
	node.id = int(d.get("id", 0))
	node.type = d.get("type", "")
	node.pos_x = int(d.get("pos_x", 0))
	node.pos_y = int(d.get("pos_y", 0))
	node.stock = int(d.get("stock", 0))
	node.max_stock = int(d.get("max_stock", 10))
	node.spoil_rate = float(d.get("spoil_rate", 0.0))
	node.quality = float(d.get("quality", 1.0))
	node.last_decay_tick = int(d.get("last_decay_tick", 0))
	return node
