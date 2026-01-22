## ClaimsSystem - expands organization claims and assigns zoning
class_name ClaimsSystem
extends ISimSystem

func tick(sim: RefCounted, state: SimState) -> void:
	pass

func tick_daily(sim: RefCounted, state: SimState) -> void:
	if state.organizations.is_empty():
		return
	for organization in state.organizations:
		_expand_claims_for_organization(organization, state)

func _expand_claims_for_organization(organization: Organization, state: SimState) -> void:
	var owner_id := organization.get_owner_id()
	_ensure_town_center_claimed(organization, state, owner_id)

	var claims_per_day := state.get_tuning_int("organization_claims_per_day", 4)
	var max_radius := state.get_tuning_int("organization_claim_radius", 12)
	if claims_per_day <= 0:
		return

	var claims := state.world.get_claims_by_owner().get(owner_id, [])
	if claims.is_empty():
		return

	var claimed_lookup := {}
	for claim in claims:
		claimed_lookup[_key(claim.x, claim.y)] = true

	for i in range(claims_per_day):
		var candidate := _find_next_claim_tile(organization, state, claimed_lookup, max_radius)
		if candidate == Vector2i(-1, -1):
			return
		state.world.set_claim_owner(candidate.x, candidate.y, owner_id)
		var dist := absi(candidate.x - organization.town_center.x) + absi(candidate.y - organization.town_center.y)
		state.world.set_zone_tag(candidate.x, candidate.y, organization.get_zone_for_distance(dist))
		claimed_lookup[_key(candidate.x, candidate.y)] = true

		state.log_event("organization_tile_claimed", {
			"organization_id": organization.id,
			"pos": [candidate.x, candidate.y]
		})

func _ensure_town_center_claimed(organization: Organization, state: SimState, owner_id: int) -> void:
	var center := organization.town_center
	if not state.world.is_valid(center.x, center.y):
		return
	if state.world.get_claim_owner(center.x, center.y) != 0:
		return
	state.world.set_claim_owner(center.x, center.y, owner_id)
	state.world.set_zone_tag(center.x, center.y, "town_center")

func _find_next_claim_tile(organization: Organization, state: SimState, claimed_lookup: Dictionary, max_radius: int) -> Vector2i:
	var candidates := {}
	for key in claimed_lookup.keys():
		var parts: Array[String] = str(key).split(",")
		var x := int(parts[0])
		var y := int(parts[1])
		for offset in [Vector2i(1, 0), Vector2i(-1, 0), Vector2i(0, 1), Vector2i(0, -1)]:
			var nx := x + offset.x
			var ny := y + offset.y
			if not state.world.is_valid(nx, ny):
				continue
			if state.world.is_claimed(nx, ny):
				continue
			var dist := absi(nx - organization.town_center.x) + absi(ny - organization.town_center.y)
			if dist > max_radius:
				continue
			var candidate_key := _key(nx, ny)
			if candidates.has(candidate_key):
				continue
			candidates[candidate_key] = {"x": nx, "y": ny, "dist": dist}

	if candidates.is_empty():
		return Vector2i(-1, -1)

	var ordered := candidates.values()
	ordered.sort_custom(func(a, b):
		if a["dist"] == b["dist"]:
			if a["x"] == b["x"]:
				return a["y"] < b["y"]
			return a["x"] < b["x"]
		return a["dist"] < b["dist"]
	)
	var best := ordered[0]
	return Vector2i(best["x"], best["y"])

func _key(x: int, y: int) -> String:
	return "%d,%d" % [x, y]
