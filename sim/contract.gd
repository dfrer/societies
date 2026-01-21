## Contract class - delivery contract with escrow
class_name Contract
extends RefCounted

## Status values
const STATUS_POSTED := "posted"
const STATUS_ACCEPTED := "accepted"
const STATUS_COMPLETED := "completed"
const STATUS_FAILED := "failed"
const STATUS_EXPIRED := "expired"

var id: int = 0
var status: String = STATUS_POSTED
var issuer_type: String = "agent"  # "agent" or "faction"
var issuer_id: int = 0
var worker_id: int = 0  # 0 if unassigned
var item: String = ""
var qty: int = 1
var payout: int = 0
var escrow: int = 0  # Coins held in escrow
var created_tick: int = 0
var accepted_tick: int = -1
var deadline_tick: int = 0
var delivery_pos_x: int = 48
var delivery_pos_y: int = 48
var delivered_qty: int = 0

func _init() -> void:
	pass

## Check if contract is active (posted or accepted)
func is_active() -> bool:
	return status == STATUS_POSTED or status == STATUS_ACCEPTED

## Check if contract is available for acceptance
func is_available() -> bool:
	return status == STATUS_POSTED

## Check if contract can be completed
func can_complete() -> bool:
	return status == STATUS_ACCEPTED and delivered_qty >= qty

## Mark as accepted by worker
func accept(agent_id: int, tick: int) -> void:
	if status == STATUS_POSTED:
		status = STATUS_ACCEPTED
		worker_id = agent_id
		accepted_tick = tick

## Mark as completed and return escrow amount
func complete() -> int:
	if status == STATUS_ACCEPTED:
		status = STATUS_COMPLETED
		var payout_amount := escrow
		escrow = 0
		return payout_amount
	return 0

## Mark as failed/expired and return escrow for refund
func fail_or_expire(is_expired: bool = false) -> int:
	if status == STATUS_ACCEPTED or status == STATUS_POSTED:
		status = STATUS_EXPIRED if is_expired else STATUS_FAILED
		var refund := escrow
		escrow = 0
		return refund
	return 0

## Check if deadline has passed
func is_past_deadline(current_tick: int) -> bool:
	return current_tick > deadline_tick

## Serialize to dictionary
func to_dict() -> Dictionary:
	return {
		"id": id,
		"status": status,
		"issuer_type": issuer_type,
		"issuer_id": issuer_id,
		"worker_id": worker_id,
		"item": item,
		"qty": qty,
		"payout": payout,
		"escrow": escrow,
		"created_tick": created_tick,
		"accepted_tick": accepted_tick,
		"deadline_tick": deadline_tick,
		"delivery_pos_x": delivery_pos_x,
		"delivery_pos_y": delivery_pos_y,
		"delivered_qty": delivered_qty
	}

## Deserialize from dictionary
static func from_dict(d: Dictionary) -> Contract:
	var contract := Contract.new()
	contract.id = int(d.get("id", 0))
	contract.status = d.get("status", STATUS_POSTED)
	contract.issuer_type = d.get("issuer_type", "agent")
	contract.issuer_id = int(d.get("issuer_id", 0))
	contract.worker_id = int(d.get("worker_id", 0))
	contract.item = d.get("item", "")
	contract.qty = int(d.get("qty", 1))
	contract.payout = int(d.get("payout", 0))
	contract.escrow = int(d.get("escrow", 0))
	contract.created_tick = int(d.get("created_tick", 0))
	contract.accepted_tick = int(d.get("accepted_tick", -1))
	contract.deadline_tick = int(d.get("deadline_tick", 0))
	contract.delivery_pos_x = int(d.get("delivery_pos_x", 48))
	contract.delivery_pos_y = int(d.get("delivery_pos_y", 48))
	contract.delivered_qty = int(d.get("delivered_qty", 0))
	return contract
