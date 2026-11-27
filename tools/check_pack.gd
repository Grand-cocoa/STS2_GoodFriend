extends SceneTree

# Run from an empty project so a missing packaged resource cannot fall back to a source file.
func _init() -> void:
	var args := OS.get_cmdline_user_args()
	if args.size() != 2 or not ProjectSettings.load_resource_pack(args[0]):
		push_error("Cannot mount resource pack")
		quit(1)
		return
	var paths := FileAccess.get_file_as_string(args[1]).split("\n", false)
	for path in paths:
		if path.ends_with(".png"):
			var image := ResourceLoader.load(path) as Texture2D
			var expected := Vector2i(96, 96) if "/icons/" in path else Vector2i(500, 380)
			if not image or Vector2i(image.get_size()) != expected:
				push_error("Missing or invalid texture " + path)
				quit(1)
				return
		elif not JSON.parse_string(FileAccess.get_file_as_string(path)) is Dictionary:
			push_error("Invalid localization " + path)
			quit(1)
			return
	print("Verified ", paths.size(), " resources from the mounted GoodFriend PCK")
	quit(0)
