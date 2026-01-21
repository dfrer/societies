## Laws class - jurisdiction laws for land claims
## 
## Provides both legacy boolean permit checks (has_harvest_permit, has_build_permit)
## and structured result checks (check_harvest_permit, check_build_permit) that return
## {allowed: bool, reason_code: String, details: Dictionary} compatible with EnforcementResult.
class_name Laws
extends RefCounted

var harvest_permit_required: bool = true
var build_permit_required: bool = true
var fine_base: int = 10
var market_ban_days_on_repeat: int = 2
var repeat_threshold: int = 3
var violation_window_ticks: int = 48
var sales_tax_rate: int = 5  # Percent 0..20

func _init() -> void:
	pass

## Initialize from tuning defaults
func init_from_tuning(tuning: Dictionary) -> void:
	harvest_permit_required = tuning.get("harvest_permit_required_default", true)
	build_permit_required = tuning.get("build_permit_required_default", true)
	fine_base = tuning.get("fine_base", 10)
	market_ban_days_on_repeat = tuning.get("market_ban_days_on_repeat", 2)
	repeat_threshold = tuning.get("repeat_threshold", 3)
	sales_tax_rate = tuning.get("sales_tax_rate_default", 5)
	var ticks_per_day: int = tuning.get("ticks_per_day", 24)
	var window_days: int = tuning.get("violation_window_days", 2)
	violation_window_ticks = window_days * ticks_per_day

## Check if agent has harvest permit
func has_harvest_permit(agent_id: int, agent_faction_id: int, owner_id: int) -> bool:
	if not harvest_permit_required:
		return true
	if owner_id == 0:
		return true  # Unclaimed land
	# Check if it's a faction owner
	if owner_id >= 1000001:
		var faction_id := owner_id - 1000001
		return agent_faction_id == faction_id
	# Agent owner
	return agent_id == owner_id

## Check if agent has build permit
func has_build_permit(agent_id: int, agent_faction_id: int, owner_id: int) -> bool:
	if not build_permit_required:
		return true
	if owner_id == 0:
		return true
	if owner_id >= 1000001:
		var faction_id := owner_id - 1000001
		return agent_faction_id == faction_id
	return agent_id == owner_id

## Structured check for harvest permit - returns {allowed, reason_code, details}
func check_harvest_permit(agent_id: int, agent_faction_id: int, owner_id: int) -> Dictionary:
	if not harvest_permit_required:
		return {"allowed": true, "reason_code": "ok", "details": {"permit_required": false}}
	if owner_id == 0:
		return {"allowed": true, "reason_code": "ok", "details": {"unclaimed_land": true}}
	if owner_id >= 1000001:
		var faction_id := owner_id - 1000001
		if agent_faction_id == faction_id:
			return {"allowed": true, "reason_code": "ok", "details": {"faction_member": true}}
		return {"allowed": false, "reason_code": "no_permit", "details": {"required_faction": faction_id}}
	if agent_id == owner_id:
		return {"allowed": true, "reason_code": "ok", "details": {"owner": true}}
	return {"allowed": false, "reason_code": "no_permit", "details": {"owner_id": owner_id}}

## Structured check for build permit - returns {allowed, reason_code, details}
func check_build_permit(agent_id: int, agent_faction_id: int, owner_id: int) -> Dictionary:
	if not build_permit_required:
		return {"allowed": true, "reason_code": "ok", "details": {"permit_required": false}}
	if owner_id == 0:
		return {"allowed": true, "reason_code": "ok", "details": {"unclaimed_land": true}}
	if owner_id >= 1000001:
		var faction_id := owner_id - 1000001
		if agent_faction_id == faction_id:
			return {"allowed": true, "reason_code": "ok", "details": {"faction_member": true}}
		return {"allowed": false, "reason_code": "no_permit", "details": {"required_faction": faction_id}}
	if agent_id == owner_id:
		return {"allowed": true, "reason_code": "ok", "details": {"owner": true}}
	return {"allowed": false, "reason_code": "no_permit", "details": {"owner_id": owner_id}}

## Calculate sales tax
func calculate_tax(trade_amount: int) -> int:
	return int(floor(float(trade_amount) * sales_tax_rate / 100.0))

## Serialize to dictionary
func to_dict() -> Dictionary:
	return {
		"harvest_permit_required": harvest_permit_required,
		"build_permit_required": build_permit_required,
		"fine_base": fine_base,
		"market_ban_days_on_repeat": market_ban_days_on_repeat,
		"repeat_threshold": repeat_threshold,
		"violation_window_ticks": violation_window_ticks,
		"sales_tax_rate": sales_tax_rate
	}

## Deserialize from dictionary
static func from_dict(d: Dictionary) -> Laws:
	var laws := Laws.new()
	laws.harvest_permit_required = d.get("harvest_permit_required", true)
	laws.build_permit_required = d.get("build_permit_required", true)
	laws.fine_base = int(d.get("fine_base", 10))
	laws.market_ban_days_on_repeat = int(d.get("market_ban_days_on_repeat", 2))
	laws.repeat_threshold = int(d.get("repeat_threshold", 3))
	laws.violation_window_ticks = int(d.get("violation_window_ticks", 48))
	laws.sales_tax_rate = int(d.get("sales_tax_rate", 5))
	return laws
