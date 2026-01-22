## Unit tests for the JobBoard class
extends "res://tests/test_case.gd"

func _run() -> void:
	subtest("Post and Claim Activity", _test_post_and_claim)
	subtest("Available Activities Sorted", _test_sorted_available)
	subtest("Serialization Roundtrip", _test_serialization_roundtrip)
	subtest("Prune Inactive", _test_prune_inactive)

func _test_post_and_claim() -> void:
	var board := JobBoard.new()
	var activity := board.post_gather_node(10, "berry", 5)
	assert_eq(activity.get("type", ""), JobBoard.ACTIVITY_GATHER_NODE, "Activity type should match")
	assert_eq(activity.get("status", ""), JobBoard.STATUS_AVAILABLE, "Activity should start available")
	var claimed := board.claim_activity(activity.get("activity_id", -1), 7, 6)
	assert_true(claimed, "Should claim available activity")
	var fetched := board.get_activity(activity.get("activity_id", -1))
	assert_eq(fetched.get("status", ""), JobBoard.STATUS_CLAIMED, "Activity should be claimed")
	assert_eq(fetched.get("worker_id", -1), 7, "Worker ID should be set")

func _test_sorted_available() -> void:
	var board := JobBoard.new()
	var a := board.post_gather_node(3, "berry", 1)
	var b := board.post_gather_node(4, "tree", 1)
	board.claim_activity(a.get("activity_id", -1), 1, 2)
	var available := board.get_available_activities(JobBoard.ACTIVITY_GATHER_NODE)
	assert_eq(available.size(), 1, "Only one available activity should remain")
	assert_eq(available[0].get("activity_id", -1), b.get("activity_id", -2), "Available activity should be the unclaimed one")

func _test_serialization_roundtrip() -> void:
	var board := JobBoard.new()
	board.post_gather_node(42, "ore", 10)
	board.post_accept_contract(5, 10)
	var dict := board.to_dict()
	var restored := JobBoard.from_dict(dict)
	assert_eq(restored.activities.size(), 2, "Activities should roundtrip")
	assert_eq(restored.activities[0].get("activity_id", -1), 1, "Activity IDs should match")
	assert_eq(restored.activities[1].get("type", ""), JobBoard.ACTIVITY_ACCEPT_CONTRACT, "Activity type should match")

func _test_prune_inactive() -> void:
	var board := JobBoard.new()
	var a := board.post_gather_node(1, "berry", 1)
	var b := board.post_gather_node(2, "tree", 2)
	board.complete_activity(a.get("activity_id", -1), 3)
	board.cancel_activity(b.get("activity_id", -1), 3)
	board.prune_inactive(1)
	assert_eq(board.activities.size(), 1, "Should prune to max inactive")
