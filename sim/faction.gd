## Faction class - represents a political faction/country
class_name Faction
extends RefCounted

var id: int = 0
var name: String = ""
var founder_agent_id: int = 0
var members: Array = []  # Agent IDs, sorted ascending
var treasury: int = 0
var created_tick: int = 0
var home_pos: Vector2i = Vector2i.ZERO
var openness: float = 0.5  # 0 = protectionist, 1 = free trade / open borders
var leader_id: int = 0
var election_tick: int = 0
var leader_salary_rate: int = 5 # Coins per day

## Frontier state for expansion efficiency (Phase 4)
var frontier_tiles: Array = []  # Array of Vector2i

## Charter contains governance rules and active proposals
var charter: Dictionary = {
	"voting_method": "majority",
	"proposal_duration_ticks": 24,  # 1 day default
	"proposals": []
}

## Trade relations: target_group_key -> {"policy": "open"|"tariff"|"embargo", "tariff_rate": int}
## Key format: "faction:<id>" or "faction:0" for factionless agents
var relations: Dictionary = {}

## Proposal ID counter
var next_proposal_id: int = 1

func _init() -> void:
	members = []
	relations = {}
	charter = {
		"voting_method": "majority",
		"proposal_duration_ticks": 24,
		"proposals": []
	}

## Initialize a new faction
func init_faction(faction_id: int, faction_name: String, founder_id: int, 
				  pos: Vector2i, tick: int, ticks_per_day: int) -> void:
	id = faction_id
	name = faction_name
	founder_agent_id = founder_id
	home_pos = pos
	created_tick = tick
	members = [founder_id]
	leader_id = founder_id
	election_tick = tick + (ticks_per_day * 7) # First election in 7 days
	charter["proposal_duration_ticks"] = ticks_per_day  # 1 day

## Add a member (keeps array sorted)
func add_member(agent_id: int) -> void:
	if agent_id in members:
		return
	members.append(agent_id)
	members.sort()

## Remove a member
func remove_member(agent_id: int) -> void:
	var idx := members.find(agent_id)
	if idx >= 0:
		members.remove_at(idx)

## Check if agent is a member
func is_member(agent_id: int) -> bool:
	return agent_id in members

## Get number of members
func get_member_count() -> int:
	return members.size()

## Get the relation key for an agent based on their faction membership
static func relation_key_for_agent(agent) -> String:
	if agent.faction_id == 0:
		return "faction:0"
	return "faction:%d" % agent.faction_id

## Get trade policy for an agent (policy + tariff_rate)
## Returns Dictionary with {policy, tariff_rate}
func get_trade_policy_for_agent(agent, tuning: Dictionary) -> Dictionary:
	# Same faction = always open with no tariff
	if agent.faction_id == id:
		return {"policy": "open", "tariff_rate": 0}
	
	var key := relation_key_for_agent(agent)
	
	if relations.has(key):
		return relations[key].duplicate()
	
	# Fallback to defaults from tuning
	if agent.faction_id == 0:
		return {
			"policy": tuning.get("default_factionless_policy", "tariff"),
			"tariff_rate": tuning.get("default_factionless_tariff_rate", 5)
		}
	else:
		return {
			"policy": tuning.get("default_relation_policy", "open"),
			"tariff_rate": tuning.get("default_relation_tariff_rate", 0)
		}

## Set trade relation toward a target group
func set_relation(target_key: String, policy: String, tariff_rate: int) -> void:
	relations[target_key] = {
		"policy": policy,
		"tariff_rate": tariff_rate
	}

## Initialize default relations on faction creation
func init_default_relations(tuning: Dictionary) -> void:
	# Set default policy toward factionless agents
	var factionless_policy: String = tuning.get("default_factionless_policy", "tariff")
	var factionless_rate: int = tuning.get("default_factionless_tariff_rate", 5)
	relations["faction:0"] = {
		"policy": factionless_policy,
		"tariff_rate": factionless_rate
	}

## Create a new proposal
## Proposal schema:
## {
##   "proposal_id": int,
##   "created_tick": int,
##   "expires_tick": int,
##   "proposer_agent_id": int,
##   "law_owner_id": int,
##   "changes": { "harvest_permit_required": bool, "build_permit_required": bool,
##                "sales_tax_rate": int, "fine_base": int,
##                "set_relation_policy": {"target_key": str, "policy": str, "tariff_rate": int} },
##   "votes_for": [int],
##   "votes_against": [int]
## }
func create_proposal(proposer_id: int, changes: Dictionary, 
					 law_owner_id: int, current_tick: int) -> Dictionary:
	var proposal := {
		"proposal_id": next_proposal_id,
		"created_tick": current_tick,
		"expires_tick": current_tick + charter["proposal_duration_ticks"],
		"proposer_agent_id": proposer_id,
		"law_owner_id": law_owner_id,
		"changes": changes.duplicate(),
		"votes_for": [],
		"votes_against": []
	}
	next_proposal_id += 1
	charter["proposals"].append(proposal)
	return proposal

## Record a vote on a proposal
func vote_on_proposal(proposal_id: int, agent_id: int, vote_for: bool) -> void:
	for proposal in charter["proposals"]:
		if proposal["proposal_id"] == proposal_id:
			if vote_for:
				if agent_id not in proposal["votes_for"]:
					proposal["votes_for"].append(agent_id)
			else:
				if agent_id not in proposal["votes_against"]:
					proposal["votes_against"].append(agent_id)
			return

## Get active proposals (not expired)
func get_active_proposals(current_tick: int) -> Array:
	var active := []
	for proposal in charter["proposals"]:
		if proposal["expires_tick"] > current_tick:
			active.append(proposal)
	return active

## Get expired proposals that need resolution
func get_expired_proposals(current_tick: int) -> Array:
	var expired := []
	for proposal in charter["proposals"]:
		if proposal["expires_tick"] <= current_tick:
			expired.append(proposal)
	return expired

## Remove a proposal
func remove_proposal(proposal_id: int) -> void:
	charter["proposals"] = charter["proposals"].filter(
		func(p): return p["proposal_id"] != proposal_id
	)

## Serialize to dictionary
func to_dict() -> Dictionary:
	return {
		"id": id,
		"name": name,
		"founder_agent_id": founder_agent_id,
		"members": members.duplicate(),
		"treasury": treasury,
		"created_tick": created_tick,
		"home_pos_x": home_pos.x,
		"home_pos_y": home_pos.y,
		"charter": {
			"voting_method": charter.get("voting_method", "majority"),
			"proposal_duration_ticks": charter.get("proposal_duration_ticks", 24),
			"proposals": charter.get("proposals", []).duplicate(true)
		},
		"relations": relations.duplicate(true),
		"next_proposal_id": next_proposal_id,
		"openness": snappedf(openness, 0.00000001),
		"frontier_tiles": _serialize_frontier_tiles(),
		"leader_id": leader_id,
		"election_tick": election_tick,
		"leader_salary_rate": leader_salary_rate
	}

func _serialize_frontier_tiles() -> Array:
	var result := []
	for pos in frontier_tiles:
		result.append({"x": pos.x, "y": pos.y})
	return result

## Deserialize from dictionary
static func from_dict(d: Dictionary) -> Faction:
	var faction := Faction.new()
	faction.id = int(d.get("id", 0))
	faction.name = d.get("name", "")
	faction.founder_agent_id = int(d.get("founder_agent_id", 0))
	# Convert members to int array
	var members_data: Array = d.get("members", [])
	faction.members = []
	for m in members_data:
		faction.members.append(int(m))
	faction.treasury = int(d.get("treasury", 0))
	faction.created_tick = int(d.get("created_tick", 0))
	faction.home_pos = Vector2i(int(d.get("home_pos_x", 0)), int(d.get("home_pos_y", 0)))
	faction.next_proposal_id = int(d.get("next_proposal_id", 1))
	faction.openness = snappedf(float(d.get("openness", 0.5)), 0.00000001)
	faction.leader_id = int(d.get("leader_id", 0))
	faction.election_tick = int(d.get("election_tick", 0))
	faction.leader_salary_rate = int(d.get("leader_salary_rate", 5))

	var charter_data: Dictionary = d.get("charter", {})
	# Convert proposals with proper types
	var proposals_data: Array = charter_data.get("proposals", [])
	var fixed_proposals := []
	for p in proposals_data:
		var fixed_p := {}
		fixed_p["proposal_id"] = int(p.get("proposal_id", 0))
		fixed_p["created_tick"] = int(p.get("created_tick", 0))
		fixed_p["expires_tick"] = int(p.get("expires_tick", 0))
		fixed_p["proposer_agent_id"] = int(p.get("proposer_agent_id", 0))
		fixed_p["law_owner_id"] = int(p.get("law_owner_id", 0))
		# Sanitize changes with proper types
		var changes_data: Dictionary = p.get("changes", {})
		var fixed_changes := {}
		for ck in changes_data:
			var cv = changes_data[ck]
			if ck in ["sales_tax_rate", "fine_base"]:
				fixed_changes[ck] = int(cv)
			elif ck == "set_relation_policy" and cv is Dictionary:
				fixed_changes[ck] = {
					"target_key": cv.get("target_key", ""),
					"policy": cv.get("policy", "open"),
					"tariff_rate": int(cv.get("tariff_rate", 0))
				}
			else:
				fixed_changes[ck] = cv
		fixed_p["changes"] = fixed_changes
		var votes_for: Array = p.get("votes_for", [])
		fixed_p["votes_for"] = []
		for v in votes_for:
			fixed_p["votes_for"].append(int(v))
		var votes_against: Array = p.get("votes_against", [])
		fixed_p["votes_against"] = []
		for v in votes_against:
			fixed_p["votes_against"].append(int(v))
		fixed_proposals.append(fixed_p)
	faction.charter = {
		"voting_method": charter_data.get("voting_method", "majority"),
		"proposal_duration_ticks": int(charter_data.get("proposal_duration_ticks", 24)),
		"proposals": fixed_proposals
	}
	
	# Deserialize trade relations
	var relations_data: Dictionary = d.get("relations", {})
	faction.relations = {}
	for key in relations_data:
		var rel: Dictionary = relations_data[key]
		faction.relations[key] = {
			"policy": rel.get("policy", "open"),
			"tariff_rate": int(rel.get("tariff_rate", 0))
		}
	
	# Deserialize frontier
	var frontier_data: Array = d.get("frontier_tiles", [])
	faction.frontier_tiles = []
	for p_data in frontier_data:
		faction.frontier_tiles.append(Vector2i(int(p_data.get("x", 0)), int(p_data.get("y", 0))))

	return faction
