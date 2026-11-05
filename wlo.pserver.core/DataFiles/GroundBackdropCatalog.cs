using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Text;

namespace Game.DataFiles
{
    /// <summary>Immutable native map defaults and spatial battle-scene overrides.</summary>
    public sealed class GroundBackdropCatalog
    {
        private const int NamedIndexEntryBytes = 29;
        private const int NamedOffsetPosition = 21;
        private const int MaximumMapBytes = 16 * 1024 * 1024;
        private const int MaximumGridAxis = 900;
        private const int LayerBytes = 6;
        private const int SoundZoneBytes = 6;
        private const int ObjectBytes = 8;
        private const int RegionBytes = 9;
        private readonly IReadOnlyDictionary<ushort, GroundMapBackdrop> maps;

        public IReadOnlyDictionary<ushort, GroundMapBackdrop> Maps { get { return maps; } }
        public static readonly GroundBackdropCatalog Empty = new GroundBackdropCatalog(new Dictionary<ushort, GroundMapBackdrop>());

        private GroundBackdropCatalog(Dictionary<ushort, GroundMapBackdrop> values)
        {
            maps = new ReadOnlyDictionary<ushort, GroundMapBackdrop>(values);
        }

        public static GroundBackdropCatalog Load(string path)
        {
            using (var stream = File.OpenRead(path)) return Load(stream);
        }

        // Archive payloads are plaintext. The footer index has 29-byte entries
        // and a final UInt16 count (aLogin FUN_0010f154).
        public static GroundBackdropCatalog Load(Stream stream)
        {
            if (stream == null || !stream.CanRead || !stream.CanSeek)
                throw new ArgumentException("Ground.MMG requires a readable, seekable stream.", "stream");
            var result = new Dictionary<ushort, GroundMapBackdrop>();
            using (var reader = new BinaryReader(stream, Encoding.ASCII, true))
            {
                if (stream.Length < sizeof(ushort)) throw new InvalidDataException("Ground.MMG footer is truncated.");
                stream.Position = stream.Length - sizeof(ushort);
                int count = reader.ReadUInt16();
                long indexStart = stream.Length - sizeof(ushort) - (long)count * NamedIndexEntryBytes;
                if (count == 0 || indexStart < 0) throw new InvalidDataException("Ground.MMG index is invalid.");
                long nextOffset = 0;
                for (int i = 0; i < count; i++)
                {
                    stream.Position = indexStart + (long)i * NamedIndexEntryBytes;
                    byte[] index = reader.ReadBytes(NamedIndexEntryBytes);
                    int nameLength = index[0];
                    if (nameLength >= NamedOffsetPosition) throw new InvalidDataException("Ground.MMG entry name is invalid.");
                    string name = Encoding.ASCII.GetString(index, 1, nameLength);
                    uint offset = BitConverter.ToUInt32(index, NamedOffsetPosition);
                    uint length = BitConverter.ToUInt32(index, NamedOffsetPosition + sizeof(uint));
                    if (offset != nextOffset || length == 0 || (long)offset + length > indexStart)
                        throw new InvalidDataException("Ground.MMG entry overlaps its index or another payload: " + name);
                    nextOffset = (long)offset + length;
                    if (!name.EndsWith(".map", StringComparison.OrdinalIgnoreCase)) continue;
                    ushort mapId;
                    if (!ushort.TryParse(name.Substring(0, name.Length - 4), NumberStyles.None, CultureInfo.InvariantCulture, out mapId) || mapId == 0 || result.ContainsKey(mapId))
                        throw new InvalidDataException("Ground.MMG map ID is invalid or duplicated: " + name);
                    if (length > MaximumMapBytes) throw new InvalidDataException("Ground.MMG map payload is too large: " + name);
                    stream.Position = offset;
                    byte[] payload = reader.ReadBytes((int)length);
                    if (payload.Length != length) throw new InvalidDataException("Ground.MMG map is truncated: " + name);
                    result.Add(mapId, ReadMap(payload));
                }
                if (nextOffset != indexStart) throw new InvalidDataException("Ground.MMG has unindexed payload bytes.");
            }
            return new GroundBackdropCatalog(result);
        }

        private static void Skip(BinaryReader reader, int count)
        {
            if (count < 0 || count > reader.BaseStream.Length - reader.BaseStream.Position)
                throw new InvalidDataException("Ground.MMG map section is truncated.");
            reader.BaseStream.Position += count;
        }

        private static GroundMapBackdrop ReadMap(byte[] payload)
        {
            using (var reader = new BinaryReader(new MemoryStream(payload, false)))
            {
                uint width = reader.ReadUInt32(), height = reader.ReadUInt32();
                int layers = reader.ReadByte();
                Skip(reader, layers * LayerBytes);
                int columns = reader.ReadUInt16(), rows = reader.ReadUInt16();
                if (width == 0 || height == 0 || columns == 0 || rows == 0 || columns > MaximumGridAxis || rows > MaximumGridAxis)
                    throw new InvalidDataException("Ground.MMG map grid is invalid.");
                Skip(reader, columns * rows);
                Skip(reader, reader.ReadUInt16() * SoundZoneBytes);
                Skip(reader, reader.ReadUInt16() * ObjectBytes);
                reader.ReadByte(); // Unresolved map-tail parameter, usually 100.
                byte defaultSelector = reader.ReadByte();
                int count = reader.ReadByte();
                if ((long)count * RegionBytes > reader.BaseStream.Length - reader.BaseStream.Position)
                    throw new InvalidDataException("Ground.MMG backdrop rectangles are truncated.");
                var regions = new List<GroundBackdropRegion>(count);
                for (int i = 0; i < count; i++)
                {
                    var region = new GroundBackdropRegion(reader.ReadByte(), reader.ReadUInt16(), reader.ReadUInt16(), reader.ReadUInt16(), reader.ReadUInt16());
                    if (region.Left > region.Right || region.Top > region.Bottom)
                        throw new InvalidDataException("Ground.MMG backdrop rectangle is inverted.");
                    regions.Add(region);
                }
                return new GroundMapBackdrop(defaultSelector, regions.AsReadOnly());
            }
        }

        public bool TryResolve(ushort mapId, int x, int y, out byte selector)
        {
            GroundMapBackdrop map;
            if (!maps.TryGetValue(mapId, out map)) { selector = 0; return false; }
            foreach (var region in map.Regions)
            {
                // Ordered first-match and inclusive rectangle boundaries.
                // Closed Legacy server's overlap tie-breaking remains unverified.
                if (x >= region.Left && x <= region.Right && y >= region.Top && y <= region.Bottom)
                { selector = region.Selector; return true; }
            }
            selector = map.DefaultSelector;
            return true;
        }
    }

    public sealed class GroundMapBackdrop
    {
        public readonly byte DefaultSelector;
        public readonly ReadOnlyCollection<GroundBackdropRegion> Regions;
        internal GroundMapBackdrop(byte selector, ReadOnlyCollection<GroundBackdropRegion> regions)
        { DefaultSelector = selector; Regions = regions; }
    }

    public sealed class GroundBackdropRegion
    {
        public readonly byte Selector;
        public readonly ushort Left, Top, Right, Bottom;
        internal GroundBackdropRegion(byte selector, ushort left, ushort top, ushort right, ushort bottom)
        { Selector = selector; Left = left; Top = top; Right = right; Bottom = bottom; }
    }
}
