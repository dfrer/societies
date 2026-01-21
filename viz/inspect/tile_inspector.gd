class_name TileInspector
extends VBoxContainer

## Displays tile ownership and jurisdiction laws.

@onready var position_label: Label = $PositionLabel
@onready var owner_label: Label = $OwnerLabel
@onready var owner_type_label: Label = $OwnerTypeLabel
@onready var laws_container: VBoxContainer = $LawsContainer
@onready var harvest_permit_label: Label = $LawsContainer/HarvestPermitLabel
@onready var build_permit_label: Label = $LawsContainer/BuildPermitLabel
@onready var fine_base_label: Label = $LawsContainer/FineBaseLabel
@onready var sales_tax_label: Label = $LawsContainer/SalesTaxLabel


func _ready() -> void:
	clear()


## Update tile display
func update_tile(coords: Vector2i, owner_id: int, laws: Laws, owner_type: String) -> void:
	position_label.text = "Position: (%d, %d)" % [coords.x, coords.y]
	
	if owner_id > 0:
		if World.is_faction_owner(owner_id):
			var faction_id := World.faction_id_from_owner(owner_id)
			owner_label.text = "Owner: Faction #%d" % faction_id
		else:
			owner_label.text = "Owner: Agent #%d" % owner_id
	else:
		owner_label.text = "Owner: None"
	
	owner_type_label.text = "Claim Type: %s" % owner_type
	
	# Update laws display
	laws_container.visible = true
	harvest_permit_label.text = "Harvest Permit: %s" % ("Required" if laws.harvest_permit_required else "Not Required")
	build_permit_label.text = "Build Permit: %s" % ("Required" if laws.build_permit_required else "Not Required")
	fine_base_label.text = "Fine Base: %d" % laws.fine_base
	sales_tax_label.text = "Sales Tax: %d%%" % laws.sales_tax_rate


## Clear the inspector
func clear() -> void:
	position_label.text = "Position: --"
	owner_label.text = "Owner: --"
	owner_type_label.text = "Claim Type: --"
	laws_container.visible = false
