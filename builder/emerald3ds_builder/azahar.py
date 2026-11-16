"""Build a stand-alone Emerald CXI and optionally install its LayeredFS mod."""

import hashlib
import os
from pathlib import Path
import shutil
import subprocess
import sys
import tempfile

from .errors import BuilderError


def register(parser):
    parser.add_argument("--port", type=Path, required=True, help="compiled 3ds_port directory")
    parser.add_argument("--output", type=Path, required=True, help="output directory")
    parser.add_argument("--makerom", default="makerom", help="makerom executable or path")
    parser.add_argument("--title-key", help="reuse a Title ID across rebuilds of the same language")
    parser.add_argument("--mod", action="store_true", help="export the separate LayeredFS mod")
    group = parser.add_mutually_exclusive_group()
    group.add_argument("--install-mod", action="store_true", help="detect the PC emulator directory")
    group.add_argument("--azahar-dir", type=Path, help="install the mod to this emulator directory")


def _emulator_dir():
    if sys.platform == "win32":
        roots = [Path(os.environ.get("APPDATA", Path.home() / "AppData/Roaming"))]
    elif sys.platform == "darwin":
        roots = [Path.home() / "Library/Application Support"]
    else:
        roots = [Path(os.environ.get("XDG_DATA_HOME", Path.home() / ".local/share"))]
    found = [root / name for root in roots for name in ("AzaharPlus", "Azahar", "azaharplus", "azahar")
             if (root / name / "config").is_dir()]
    found = list({path.resolve(): path for path in found}.values())
    if len(found) != 1:
        raise BuilderError("Could not select one Azahar installation. Use --azahar-dir.")
    return found[0]


def _mod_path(root, title_id):
    return root / "load" / "mods" / title_id / "romfs" / "mods" / "una-pantalla.ini"


def _write_mod(source, dest):
    dest.parent.mkdir(parents=True, exist_ok=True)
    try:
        with dest.open("xb") as target:
            target.write(source.read_bytes())
    except FileExistsError:
        print("Kept existing mod settings at %s" % dest)


def run(args):
    port = args.port.resolve()
    elf = port / "emerald3ds.elf"
    icon = port / "emerald3ds.smdh"
    romfs = port / "romfs"
    spec = port / "emerald3ds-azahar.rsf"
    mod = port / "mods" / "una-pantalla.ini"
    install_dir = _emulator_dir() if args.install_mod else args.azahar_dir
    if install_dir is not None and not (install_dir / "config").is_dir():
        raise BuilderError("The Azahar user directory must contain its config folder.")
    with_mod = args.mod or install_dir is not None
    for required in (elf, icon, spec, romfs / "data.embedded", romfs / "engine" / "abi.bin"):
        if not required.is_file():
            raise BuilderError("Missing %s. Build the port with EMBED_DATA=1 first." % required)
    if with_mod and not mod.is_file():
        raise BuilderError("Missing the LayeredFS mod template: %s" % mod)
    if (romfs / "mods" / "una-pantalla.ini").exists():
        raise BuilderError("Remove the installed mod from the input RomFS before packaging.")
    makerom = shutil.which(args.makerom)
    if not makerom:
        raise BuilderError("makerom was not found. Pass its path with --makerom.")

    # Keep generated IDs within the homebrew range. A named key lets a user
    # rebuild without changing the existing emulator mod folder.
    digest = hashlib.sha256(b"emerald3ds-azahar\0")
    if args.title_key:
        digest.update(args.title_key.encode("utf-8"))
    else:
        with elf.open("rb") as stream:
            for block in iter(lambda: stream.read(1024 * 1024), b""):
                digest.update(block)
    unique_id = 0xF0000 | int.from_bytes(digest.digest()[:2], "big")
    title_id = f"{0x0004000000000000 | (unique_id << 8):016X}"
    output = args.output.resolve()
    output.mkdir(parents=True, exist_ok=True)
    destination = output / f"Emerald3DS-{title_id}.cxi"
    try:
        with tempfile.TemporaryDirectory(prefix="emerald-cxi-", dir=output) as temp:
            staging = Path(temp)
            # Azahar's LayeredFS lookup needs a file to override in RomFS.
            staged_romfs = staging / "romfs"
            shutil.copytree(romfs, staged_romfs)
            marker = staged_romfs / "mods" / "una-pantalla.ini"
            marker.parent.mkdir(exist_ok=True)
            marker.write_text("[emerald3ds-screen-mod-disabled]\n", encoding="ascii")
            rsf = staging / "app.rsf"
            rsf.write_text(spec.read_text(encoding="ascii").replace("@UNIQUE_ID@", f"0x{unique_id:05X}")
                           .replace("@ROMFS@", staged_romfs.as_posix()), encoding="utf-8")
            cxi = staging / "app.cxi"
            subprocess.run([makerom, "-f", "ncch", "-o", str(cxi), "-rsf", str(rsf),
                            "-elf", str(elf), "-icon", str(icon), "-target", "t"], check=True)
            with cxi.open("rb") as stream:
                stream.seek(0x100)
                header = stream.read(0x20)
            if header[:4] != b"NCCH" or int.from_bytes(header[0x18:0x20], "little") != int(title_id, 16):
                raise BuilderError("makerom did not produce a CXI with the expected Title ID.")
            os.replace(cxi, destination)
        if with_mod:
            _write_mod(mod, _mod_path(output, title_id))
        if install_dir is not None:
            installed = _mod_path(install_dir, title_id)
            _write_mod(mod, installed)
            print("LayeredFS: %s" % installed)
    except (OSError, subprocess.CalledProcessError) as exc:
        raise BuilderError("Could not package Emerald for Azahar: %s" % exc) from exc
    print("Title ID: %s\nCXI: %s" % (title_id, destination))
    return 0
