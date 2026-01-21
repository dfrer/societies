class_name TestUtils
extends RefCounted

static func assert_true(condition: bool, msg: String) -> String:
	if not condition:
		return "Assertion failed: %s (Expected true, got false)" % msg
	return ""
static func assert_false(condition: bool, msg: String) -> String:
	if condition:
		return "Assertion failed: %s (Expected false, got true)" % msg
	return ""
static func assert_eq(actual, expected, msg: String) -> String:
	if actual != expected:
		return "Assertion failed: %s (Expected %s, got %s)" % [msg, str(expected), str(actual)]
	return ""
static func assert_ne(actual, expected, msg: String) -> String:
	if actual == expected:
		return "Assertion failed: %s (Expected not %s, but got it)" % [msg, str(expected)]
	return ""
static func assert_approx(actual: float, expected: float, epsilon: float, msg: String) -> String:
	if abs(actual - expected) > epsilon:
		return "Assertion failed: %s (Expected %f +/- %f, got %f)" % [msg, expected, epsilon, actual]
	return ""
static func assert_between(value: float, low: float, high: float, msg: String) -> String:
	if value < low or value > high:
		return "Assertion failed: %s (Expected between %f and %f, got %f)" % [msg, low, high, value]
	return ""
