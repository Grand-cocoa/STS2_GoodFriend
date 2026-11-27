extends SceneTree

var pack := PCKPacker.new()

func _init() -> void:
	var args := OS.get_cmdline_user_args()
	if args.size() != 2 or pack.pck_start(args[0]) != OK:
		push_error("Could not start GoodFriend resource pack")
		quit(1)
		return
	var files := FileAccess.get_file_as_string(args[1]).split("\n", false)
	for resource_path in files:
		if resource_path.ends_with(".png"):
			var remap := ConfigFile.new()
			if remap.load(resource_path + ".import") != OK:
				push_error("Texture was not imported: " + resource_path)
				quit(1)
				return
			var texture_path: String = remap.get_value("remap", "path")
			if not add_file(resource_path + ".import") or not add_file(texture_path):
				quit(1)
				return
		elif not add_file(resource_path):
			quit(1)
			return
	var error := pack.flush()
	if error != OK:
		push_error(error_string(error))
	quit(0 if error == OK else 1)

func add_file(path: String) -> bool:
	var result := pack.add_file(path, ProjectSettings.globalize_path(path))
	if result != OK:
		push_error("Cannot pack " + path + " (" + error_string(result) + ")")
	return result == OK
