"""Build the gameplay DLL and a resource-only Godot pack, then verify and zip the install."""
import argparse
import json
import shutil
import subprocess
import tempfile
from pathlib import Path
from zipfile import ZIP_DEFLATED, ZipFile

ROOT = Path(__file__).resolve().parents[1]


def package_windows(mod_archive):
    scripts = ("Local-Coop-Test.cmd", "Local-Coop-Player.cmd", "Local-Coop-Workshop.ps1")
    for suffix, include_mod in [("-Windows", True), ("-Windows-Launchers", False)]:
        archive = mod_archive.with_name(f"{mod_archive.stem}{suffix}.zip")
        temporary = archive.with_suffix(".zip.tmp")
        with ZipFile(temporary, "w", compression=ZIP_DEFLATED) as out:
            for script in scripts:
                out.write(ROOT / "tools" / script, script)
            if include_mod:
                with ZipFile(mod_archive) as installed:
                    for entry in installed.infolist():
                        out.writestr(f"mods/{entry.filename}", installed.read(entry))
        temporary.replace(archive)
        print(archive)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--dotnet", default="dotnet")
    parser.add_argument("--godot", default="godot")
    parser.add_argument("--game-data", type=Path, help="Directory containing sts2.dll")
    parser.add_argument("--no-restore", action="store_true")
    parser.add_argument("--output", type=Path, default=ROOT / "dist")
    args = parser.parse_args()

    build = [args.dotnet, "build", str(ROOT / "GoodFriend.csproj"), "-c", "Release", "--nologo"]
    if args.no_restore:
        build.append("--no-restore")
    if args.game_data:
        build.append(f"-p:Sts2DataDir={args.game_data.resolve()}")
    subprocess.run(build, check=True)
    manifest = json.loads((ROOT / "GoodFriend.json").read_text(encoding="utf-8"))
    args.output.mkdir(parents=True, exist_ok=True)
    archive = args.output / f"GoodFriend-{manifest['version']}.zip"

    with tempfile.TemporaryDirectory() as tmp:
        staging = Path(tmp) / "assets"
        checker = Path(tmp) / "empty"
        staging.mkdir()
        checker.mkdir()
        for project in (staging, checker):
            shutil.copyfile(ROOT / "project.godot", project / "project.godot")
        shutil.copytree(ROOT / "GoodFriend", staging / "GoodFriend", ignore=shutil.ignore_patterns("*.import"))
        resources = sorted("res://" + p.relative_to(staging).as_posix()
                           for p in (staging / "GoodFriend").rglob("*") if p.is_file())
        resource_list = Path(tmp) / "resources.txt"
        resource_list.write_text("\n".join(resources), encoding="utf-8")
        pck = Path(tmp) / "GoodFriend.pck"
        for project, command in [
            (staging, ["--editor", "--import"]),
            (staging, ["--script", str(ROOT / "tools/pack.gd"), "--", str(pck), str(resource_list)]),
            (checker, ["--script", str(ROOT / "tools/check_pack.gd"), "--", str(pck), str(resource_list)]),
        ]:
            subprocess.run([args.godot, "--headless", "--path", str(project), *command], check=True)
        temporary_zip = archive.with_suffix(".zip.tmp")
        with ZipFile(temporary_zip, "w", compression=ZIP_DEFLATED) as out:
            for path in (ROOT / "GoodFriend.json", ROOT / "bin/Release/net9.0/GoodFriend.dll", pck):
                out.write(path, f"GoodFriend/{path.name}")
        temporary_zip.replace(archive)
    print(archive)
    package_windows(archive)


if __name__ == "__main__":
    main()
