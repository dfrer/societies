## EnforcementResult - structured result type for enforcement checks
## Provides type-safe construction and canonical reason codes for all enforcement scenarios
class_name EnforcementResult
extends RefCounted

# =============================================================================
# Reason Codes - canonical registry for all enforcement scenarios
# =============================================================================

## Action is permitted - no enforcement issue
const OK := "ok"

## Agent lacks required permit for this action on this land
const NO_PERMIT := "no_permit"

## Violation was detected and enforcement action taken
const DETECTED := "detected"

## Agent is currently banned from market access
const MARKET_BANNED := "market_banned"

## Agent is trespassing on restricted territory
const TRESPASSING := "trespassing"

## Trade blocked due to faction embargo policy
const EMBARGO := "embargo"

## Agent lacks funds for required payment
const INSUFFICIENT_FUNDS := "insufficient_funds"

## Agent does not own the required items
const MISSING_ITEMS := "missing_items"

# =============================================================================
# Result Properties
# =============================================================================

## Whether the action is allowed to proceed
var allowed: bool = false

## Reason code explaining the result (see constants above)
var reason_code: String = ""

## Additional context about the enforcement result
## Common keys: owner_id, permit_type, violation_type, fine_applied, 
##              ban_until_tick, ticks_remaining, required_faction, etc.
var details: Dictionary = {}

# =============================================================================
# Factory Methods
# =============================================================================

## Create a result indicating the action is allowed
static func ok(p_details: Dictionary = {}) -> EnforcementResult:
	var result := EnforcementResult.new()
	result.allowed = true
	result.reason_code = OK
	result.details = p_details
	return result

## Create a result indicating the action is denied
static func denied(p_reason_code: String, p_details: Dictionary = {}) -> EnforcementResult:
	var result := EnforcementResult.new()
	result.allowed = false
	result.reason_code = p_reason_code
	result.details = p_details
	return result

## Create from a dictionary (for interop with existing code)
static func from_dict(d: Dictionary) -> EnforcementResult:
	var result := EnforcementResult.new()
	result.allowed = d.get("allowed", false)
	result.reason_code = d.get("reason_code", "")
	result.details = d.get("details", {})
	return result

# =============================================================================
# Instance Methods
# =============================================================================

func _init() -> void:
	pass

## Convert to dictionary format for compatibility with existing code
func to_dict() -> Dictionary:
	return {
		"allowed": allowed,
		"reason_code": reason_code,
		"details": details
	}

## Check if this result allows the action
func is_allowed() -> bool:
	return allowed

## Check if this result blocks the action
func is_denied() -> bool:
	return not allowed

## Get a human-readable description of this result
func get_description() -> String:
	if allowed:
		return "Action allowed: %s" % reason_code
	else:
		return "Action denied: %s" % reason_code
