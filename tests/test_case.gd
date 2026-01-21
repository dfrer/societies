class_name TestCase
extends RefCounted
const TestUtils = preload("res://tests/test_utils.gd")

var _failures: PackedStringArray = []
var _current_subtest_label: String = ""
func run() -> bool:
	_failures.clear()
	_run()
	return _failures.is_empty()
# Overlay for subclasses to override
func _run() -> void:
	pass
func get_failures() -> PackedStringArray:
	return _failures
func subtest(label: String, fn: Callable) -> void:
	var old_label := _current_subtest_label
	if _current_subtest_label.is_empty():
		_current_subtest_label = label
	else:
		_current_subtest_label = "%s > %s" % [_current_subtest_label, label]
	fn.call()
	_current_subtest_label = old_label
func fail(msg: String) -> void:
	if not _current_subtest_label.is_empty():
		_failures.append("[%s] %s" % [_current_subtest_label, msg])
	else:
		_failures.append(msg)
func check(condition: bool, msg: String) -> bool:
	if not condition:
		fail(msg)
		return false
	return true
# Helper wrappers around TestUtils
func assert_true(cond: bool, msg: String) -> void:
	var err := TestUtils.assert_true(cond, msg)
	if not err.is_empty(): fail(err)
func assert_false(cond: bool, msg: String) -> void:
	var err := TestUtils.assert_false(cond, msg)
	if not err.is_empty(): fail(err)
func assert_eq(actual, expected, msg: String) -> void:
	var err := TestUtils.assert_eq(actual, expected, msg)
	if not err.is_empty(): fail(err)
func assert_ne(actual, expected, msg: String) -> void:
	var err := TestUtils.assert_ne(actual, expected, msg)
	if not err.is_empty(): fail(err)
func assert_approx(actual: float, expected: float, epsilon: float, msg: String) -> void:
	var err := TestUtils.assert_approx(actual, expected, epsilon, msg)
	if not err.is_empty(): fail(err)
func assert_between(val: float, low: float, high: float, msg: String) -> void:
	var err := TestUtils.assert_between(val, low, high, msg)
	if not err.is_empty(): fail(err)
