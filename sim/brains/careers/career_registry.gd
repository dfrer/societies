## CareerRegistry - definitions for career ladders and requirements
class_name CareerRegistry
extends RefCounted

const CAREERS := {
	"gatherer": {
		"ladder": ["gatherer", "cook"],
		"preferred_resource": "berry",
		"required_tools": [],
		"preferred_workshops": ["kitchen"]
	},
	"cook": {
		"ladder": ["cook", "baker"],
		"preferred_resource": "berry",
		"required_tools": [],
		"preferred_workshops": ["kitchen", "stove"]
	},
	"baker": {
		"ladder": ["baker"],
		"preferred_resource": "grain",
		"required_tools": [],
		"preferred_workshops": ["baker", "stove"]
	},
	"logger": {
		"ladder": ["logger", "carpenter"],
		"preferred_resource": "tree",
		"required_tools": ["Axe"],
		"preferred_workshops": ["carpenter"]
	},
	"miner": {
		"ladder": ["miner", "blacksmith"],
		"preferred_resource": "ore",
		"required_tools": ["Pickaxe"],
		"preferred_workshops": ["smelter"]
	},
	"blacksmith": {
		"ladder": ["blacksmith"],
		"preferred_resource": "ore",
		"required_tools": ["Pickaxe"],
		"preferred_workshops": ["forge", "smithy"]
	}
}

static func get_career_definition(career_type: String) -> Dictionary:
	return CAREERS.get(career_type, {})

static func get_preferred_resource(career_type: String) -> String:
	var def := get_career_definition(career_type)
	return def.get("preferred_resource", "")

static func get_required_tools(career_type: String) -> Array:
	var def := get_career_definition(career_type)
	return def.get("required_tools", [])

static func get_preferred_workshops(career_type: String) -> Array:
	var def := get_career_definition(career_type)
	return def.get("preferred_workshops", [])
