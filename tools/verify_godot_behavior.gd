extends SceneTree

class Test:
	var d = {}
	var a = []

func _init():
	var t1 = Test.new()
	var t2 = Test.new()
	
	t1.d["key"] = "val"
	t1.a.append(1)
	
	print("T2 Dict: ", t2.d)
	print("T2 Array: ", t2.a)
	
	if t2.d.has("key"):
		print("DANGER: Dict is shared!")
	else:
		print("SAFE: Dict is unique.")
		
	if not t2.a.is_empty():
		print("DANGER: Array is shared!")
	else:
		print("SAFE: Array is unique.")
	
	quit()
