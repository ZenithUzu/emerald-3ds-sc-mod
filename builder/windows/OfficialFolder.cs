using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace Emerald3DS {
    internal static class OfficialFolder {
        sealed class Entry {
            public long Offset;
            public int Size;
            public uint Crc;
        }

        public static string FindPack(string folder) {
            foreach (var candidate in new[] { folder, Path.Combine(folder, "emerald3ds"),
                                              Path.Combine(folder, "3ds", "emerald3ds") }) {
                var pack = Path.Combine(candidate, "emerald3ds.pak");
                if (File.Exists(pack) && File.Exists(Path.Combine(candidate, "Emerald3DS.3dsx"))) return pack;
            }
            throw new IOException("Select the extracted Emerald 3DS folder containing Emerald3DS.3dsx and emerald3ds.pak.");
        }

        public static void Prepare(string folder, string destination, Stream profile) {
            using (var archive = new ZipArchive(profile, ZipArchiveMode.Read))
            using (var source = new BinaryReader(File.OpenRead(FindPack(folder))))
            using (var manifest = new StreamReader(archive.GetEntry("profile.txt").Open())) {
                var expected = manifest.ReadLine().Split('\t');
                if (Encoding.ASCII.GetString(source.ReadBytes(8)) != "EM3DPAK\0" || source.ReadUInt32() != 1)
                    throw new IOException("The selected game data pack is invalid.");
                var abi = source.ReadUInt32().ToString("x8");
                var rom = BitConverter.ToString(source.ReadBytes(20)).Replace("-", "").ToLowerInvariant();
                if (abi != expected[0] || rom != expected[1])
                    throw new IOException("This installer profile supports the official Emerald 3DS v0.5.0 Spanish folder.");
                var count = source.ReadUInt32();
                var index = source.ReadInt64();
                if (count > 20000 || index < 64 || index > source.BaseStream.Length - 40L * count)
                    throw new IOException("The game data pack index is invalid.");
                source.BaseStream.Position = index;
                var entries = new Dictionary<ulong, Entry>();
                for (int i = 0; i < count; i++) {
                    var id = source.ReadUInt64();
                    source.ReadUInt32();
                    uint flags = source.ReadUInt32();
                    long offset = source.ReadInt64();
                    int size = source.ReadInt32();
                    int raw = source.ReadInt32();
                    uint crc = source.ReadUInt32();
                    source.ReadUInt32();
                    if (flags != 0 || size != raw || size < 0 || offset < 0 || offset > source.BaseStream.Length - size)
                        throw new IOException("The game data pack contains an invalid entry.");
                    entries.Add(id, new Entry { Offset = offset, Size = size, Crc = crc });
                }

                foreach (var file in archive.Entries) {
                    if (!file.FullName.StartsWith("romfs/") &&
                        file.FullName != "emerald3ds.elf" && file.FullName != "emerald3ds.smdh") continue;
                    var path = Path.Combine(destination, file.FullName);
                    Directory.CreateDirectory(Path.GetDirectoryName(path));
                    file.ExtractToFile(path);
                }
                string line;
                while ((line = manifest.ReadLine()) != null) {
                    var info = line.Split('\t');
                    Entry entry;
                    if (!entries.TryGetValue(ulong.Parse(info[1], CultureInfo.InvariantCulture), out entry))
                        throw new IOException("The game data pack is missing " + info[0]);
                    source.BaseStream.Position = entry.Offset;
                    var bytes = source.ReadBytes(entry.Size);
                    if (Crc(bytes) != entry.Crc) throw new IOException("The game data pack is damaged: " + info[0]);
                    int expectedSize = int.Parse(info[3], CultureInfo.InvariantCulture);
                    if (info[4] != "-") {
                        using (var patch = new BinaryReader(archive.GetEntry(info[4]).Open()))
                            bytes = Apply(bytes, patch, expectedSize);
                    }
                    if (bytes.Length != expectedSize || Crc(bytes) != uint.Parse(info[2], CultureInfo.InvariantCulture))
                        throw new IOException("This game data does not match the mod: " + info[0]);
                    var path = Path.Combine(destination, "romfs", info[0]);
                    Directory.CreateDirectory(Path.GetDirectoryName(path));
                    File.WriteAllBytes(path, bytes);
                }
            }
        }

        static byte[] Apply(byte[] source, BinaryReader patch, int length) {
            var result = new byte[length];
            int cursor = 0;
            while (cursor < result.Length) {
                int offset = patch.ReadInt32();
                int count = patch.ReadInt32();
                if (count <= 0 || count > length - cursor || offset < -1 ||
                    (offset >= 0 && offset > source.Length - count))
                    throw new IOException("Invalid game compatibility patch.");
                if (offset == -1) {
                    var literal = patch.ReadBytes(count);
                    if (literal.Length != count) throw new EndOfStreamException();
                    Buffer.BlockCopy(literal, 0, result, cursor, count);
                } else {
                    Buffer.BlockCopy(source, offset, result, cursor, count);
                }
                cursor += count;
            }
            return result;
        }

        static readonly uint[] CrcTable = MakeCrcTable();

        static uint[] MakeCrcTable() {
            var table = new uint[256];
            for (uint i = 0; i < table.Length; i++) {
                uint value = i;
                for (int bit = 0; bit < 8; bit++)
                    value = (value >> 1) ^ ((value & 1) == 0 ? 0 : 0xedb88320U);
                table[i] = value;
            }
            return table;
        }

        static uint Crc(byte[] data) {
            uint value = 0xffffffff;
            foreach (byte item in data) value = (value >> 8) ^ CrcTable[(value ^ item) & 255];
            return ~value;
        }
    }
}
