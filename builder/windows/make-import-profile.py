"""Prepare a Windows folder-import profile from a built port and official pack."""
import argparse
import io
import struct
import sys
import zipfile
import zlib
from pathlib import Path

root = Path(__file__).resolve().parents[2]
sys.path[:0] = [str(root / "tools/port_common"), str(root / "builder")]
from staging import engine_files, data_files
from emerald3ds_builder import pak


def delta(old, new):
    size = 24
    lookup = {old[i:i + size]: i for i in range(0, len(old) - size + 1, 8)}
    output = io.BytesIO()
    i = literal = 0
    while i + size <= len(new):
        start = lookup.get(new[i:i + size])
        if start is None:
            i += 1
            continue
        if i > literal:
            output.write(struct.pack("<ii", -1, i - literal))
            output.write(new[literal:i])
        count = size
        while (i + count < len(new) and start + count < len(old)
               and new[i + count] == old[start + count]):
            count += 1
        output.write(struct.pack("<ii", start, count))
        i += count
        literal = i
    if literal < len(new):
        output.write(struct.pack("<ii", -1, len(new) - literal))
        output.write(new[literal:])
    return output.getvalue()


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("port", type=Path)
    parser.add_argument("official_pack", type=Path)
    parser.add_argument("output", type=Path)
    args = parser.parse_args()
    fs = args.port / "romfs"
    with pak.PakReader(args.official_pack) as source:
        if (source.abi, source.rom_sha1.hex()) != (
            0x5A00A173, "fe1558a3dcb0360ab558969e09b690888b846dd9"
        ):
            parser.error("Use the data pack from official Emerald 3DS v0.5.0 Spanish.")
    with pak.PakReader(args.official_pack) as source, zipfile.ZipFile(
        args.output, "w", zipfile.ZIP_DEFLATED, compresslevel=6
    ) as target:
        target.write(args.port / "emerald3ds.elf", "emerald3ds.elf")
        target.write(args.port / "emerald3ds.smdh", "emerald3ds.smdh")
        for path, file in engine_files(fs):
            target.write(file, "romfs/" + path)
        entries = []
        for path, file in data_files(fs):
            if path == "mods/una-pantalla.ini":
                continue
            entry = source.entries.get(pak.path_id(path))
            if entry is None:
                raise ValueError("Official pack lacks " + path)
            data = file.read_bytes()
            crc = zlib.crc32(data)
            patch = "-"
            if (crc, len(data)) != (entry.crc32, entry.raw_size):
                patch = "patch/" + str(len(entries))
                target.writestr(patch, delta(source.read(path), data))
                print("Relinked", path)
            entries.append(f"{path}\t{entry.id}\t{crc}\t{len(data)}\t{patch}")
        target.writestr("profile.txt", f"{source.abi:08x}\t{source.rom_sha1.hex()}\n" + "\n".join(entries))


if __name__ == "__main__":
    main()
