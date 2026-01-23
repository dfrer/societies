## Deterministic RNG wrapper
## Uses Godot's built-in deterministic rand_from_seed function
class_name RNG
extends RefCounted

var _state: int = 0

func _init() -> void:
	# Use a fixed default seed for determinism - DO NOT use hash() as it returns
	# different values between process runs
	_state = 12345

## Initialize RNG with a seed
func init_seed(seed_value: int) -> void:
	_state = seed_value

## Get current RNG state for serialization
func get_state() -> Dictionary:
	return {
		"state": str(_state),
		"format_version": "3.0"
	}

## Restore RNG state from dictionary
func set_state(d: Dictionary) -> void:
	# Support legacy hex format by resetting if not v3.0, or try to parse
	# Ideally we just take the int.
	var format = d.get("format_version", "1.0")
	if format == "3.0":
		_state = int(str(d.get("state", "0")))
	elif format == "2.0":
		# Try to convert legacy hex state to int state if possible, 
		# but likely better to just start fresh or warn.
		# For tests, we usually restore what we just saved.
		var s = d.get("state", "0")
		var sign := 1
		if s.begins_with("-"):
			sign = -1
			s = s.substr(1)
		if s.begins_with("0x"): s = s.substr(2)
		_state = sign * s.hex_to_int()
	else:
		_state = int(str(d.get("seed", "0"))) # Fallback to seed for legacy

## Get random integer
func randi() -> int:
	var res = rand_from_seed(_state)
	_state = res[1]
	return res[0]

## Get random float in range [0, 1]
func randf() -> float:
	var res = rand_from_seed(_state)
	_state = res[1]
	# Map uint32-like result to [0, 1]
	# rand_from_seed returns int (64-bit signed in Godot 4).
	# Use mask to ensure positive
	return (res[0] & 0xFFFFFFFF) / 4294967295.0

## Get random integer in range [a, b] inclusive
func randi_range(a: int, b: int) -> int:
	if a == b: return a
	var r = self.randi()
	# Ensure positive modulo
	var range_size = b - a + 1
	var mod = r % range_size
	if mod < 0: mod += range_size
	return a + mod

## Get random float in range [a, b]
func randf_range(a: float, b: float) -> float:
	return a + (b - a) * self.randf()

## Serialize to dictionary
func to_dict() -> Dictionary:
	return get_state()

## Deserialize from dictionary
static func from_dict(d: Dictionary) -> RNG:
	var rng := RNG.new()
	rng.set_state(d)
	return rng
