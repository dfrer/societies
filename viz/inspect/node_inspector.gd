class_name NodeInspector
extends VBoxContainer

## Displays resource node or workshop details.

@onready var no_node_label: Label = $NoNodeLabel
@onready var resource_container: VBoxContainer = $ResourceContainer
@onready var resource_type_label: Label = $ResourceContainer/ResourceTypeLabel
@onready var resource_stock_label: Label = $ResourceContainer/ResourceStockLabel
@onready var resource_pos_label: Label = $ResourceContainer/ResourcePosLabel
@onready var resource_id_label: Label = $ResourceContainer/ResourceIdLabel

@onready var workshop_container: VBoxContainer = $WorkshopContainer
@onready var workshop_id_label: Label = $WorkshopContainer/WorkshopIdLabel
@onready var workshop_owner_label: Label = $WorkshopContainer/WorkshopOwnerLabel
@onready var workshop_status_label: Label = $WorkshopContainer/WorkshopStatusLabel
@onready var workshop_queue_label: Label = $WorkshopContainer/WorkshopQueueLabel
@onready var workshop_job_label: Label = $WorkshopContainer/WorkshopJobLabel


func _ready() -> void:
	clear()


## Update with resource node data
func update_resource_node(node: ResourceNode) -> void:
	no_node_label.visible = false
	resource_container.visible = true
	workshop_container.visible = false
	
	resource_type_label.text = "Type: %s" % node.type.capitalize()
	resource_stock_label.text = "Stock: %d / %d" % [node.stock, node.max_stock]
	resource_pos_label.text = "Position: (%d, %d)" % [node.pos_x, node.pos_y]
	resource_id_label.text = "Node ID: %d" % node.id


## Update with workshop data
func update_workshop(workshop: Workshop) -> void:
	no_node_label.visible = false
	resource_container.visible = false
	workshop_container.visible = true
	
	workshop_id_label.text = "Workshop ID: %d" % workshop.id
	
	if workshop.built_by > 0:
		workshop_owner_label.text = "Owner: Agent #%d" % workshop.built_by
	else:
		workshop_owner_label.text = "Owner: Public"
	
	if workshop.is_built:
		workshop_status_label.text = "Status: Ready"
	else:
		workshop_status_label.text = "Status: Building (%d/%d)" % [workshop.build_progress, workshop.build_total]
	
	workshop_queue_label.text = "Queue: %d jobs" % workshop.queue.size()
	
	var current_job := workshop.get_current_job()
	if current_job.is_empty():
		workshop_job_label.text = "Current Job: None"
	else:
		var recipe_id: String = current_job.get("recipe_id", "unknown")
		var progress: int = current_job.get("progress", 0)
		var total: int = current_job.get("total_ticks", 1)
		workshop_job_label.text = "Current Job: %s (%d/%d)" % [recipe_id, progress, total]


## Clear the inspector
func clear() -> void:
	no_node_label.visible = true
	resource_container.visible = false
	workshop_container.visible = false
