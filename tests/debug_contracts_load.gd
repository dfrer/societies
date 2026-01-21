extends SceneTree

const ContractsSystem = preload("res://sim/contracts_system.gd")

func _ready() -> void:
	var contracts := ContractsSystem.new()
	print("ContractsSystem loaded successfully: ", contracts != null)
	quit()