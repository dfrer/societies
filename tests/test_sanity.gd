extends "res://tests/test_case.gd"

func _run() -> void:
    subtest("Sanity Check", _sanity)

func _sanity() -> void:
    assert_true(true, "It works")
