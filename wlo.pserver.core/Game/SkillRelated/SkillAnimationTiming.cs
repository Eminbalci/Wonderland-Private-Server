using System;
using System.Collections.Generic;
using System.IO;
using RCLibrary.Core;

namespace Game.SkillRelated
{
    // Server pacing estimates from the native animation paths, not completion ACKs.
    // The unverified header +5 field is deliberately NOT used as a duration.
    internal static class SkillAnimationTiming
    {
        private static readonly Lazy<Dictionary<ushort, int>> Durations =
            new Lazy<Dictionary<ushort, int>>(Load);

        internal static int GetDelayMs(ushort skillId, int fallbackMs, int marginMs)
        {
            int estimate;
            int delay = Durations.Value.TryGetValue(skillId, out estimate)
                ? Math.Max(fallbackMs, estimate + marginMs) : fallbackMs;
            // Coherent Strength (Wuwan): return stutter was reported with the generic
            // 4866ms estimate. Reserve a conservative return/camera
            // window for this ID only; 6200ms is a pacing budget, not a measured duration.
            return skillId == 30074 ? Math.Max(delay, 6200) : delay;
        }

        private static Dictionary<ushort, int> Load()
        {
            try
            {
                string path = PathHelper.GetDataFilePath("SkillData.MBTM");
                using (var stream = File.OpenRead(path))
                {
                    var durations = Read(stream);
                    DebugSystem.Write($"[SkillAnimationTiming] Loaded {durations.Count} path timing estimates from {path}; unverified skills retain fallback pacing.");
                    return durations;
                }
            }
            catch (Exception ex) when (ex is IOException || ex is InvalidDataException || ex is UnauthorizedAccessException || ex is ArgumentException)
            {
                DebugSystem.Write($"[SkillAnimationTiming] Using fallback pacing: {ex.Message}");
                return new Dictionary<ushort, int>();
            }
        }

        // Kept separate from startup so real MBTM files and malformed fixtures can be checked.
        internal static Dictionary<ushort, int> Read(Stream stream)
        {
            var result = new Dictionary<ushort, int>();
            if (stream.Length < 2 || stream.Length > 64 * 1024 * 1024)
                throw new InvalidDataException("Invalid MBTM length.");
            using (var reader = new BinaryReader(stream, System.Text.Encoding.UTF8, true))
            {
                stream.Position = stream.Length - 2;
                int count = reader.ReadUInt16();
                long indexStart = stream.Length - 2 - count * 10L;
                if (count == 0 || indexStart < 0)
                    throw new InvalidDataException("Invalid MBTM index.");
                var ids = new HashSet<ushort>();
                long previousEnd = 0;
                for (int i = 0; i < count; i++)
                {
                    stream.Position = indexStart + i * 10L;
                    ushort id = reader.ReadUInt16();
                    long offset = reader.ReadUInt32();
                    long size = reader.ReadUInt32();
                    if (!ids.Add(id) || offset != previousEnd || size < 27 || offset + size > indexStart)
                        throw new InvalidDataException("Invalid MBTM record bounds or duplicate ID.");
                    previousEnd = offset + size;
                    stream.Position = offset;
                    // A record-local stream prevents a bad count from reading the next skill/index.
                    byte[] record = reader.ReadBytes((int)size);
                    int estimate;
                    if (TryEstimate(record, out estimate)) result.Add(id, estimate);
                }
                if (previousEnd != indexStart)
                    throw new InvalidDataException("Incomplete MBTM index.");
            }
            return result;
        }

        private static bool TryEstimate(byte[] record, out int estimateMs)
        {
            estimateMs = 0;
            try
            {
                using (var stream = new MemoryStream(record, false))
                using (var reader = new BinaryReader(stream))
                {
                    stream.Position = 3;
                    byte approachMode = reader.ReadByte();
                    byte returnMode = reader.ReadByte();
                    // Only the two native constant-speed modes have been verified.
                    if (approachMode > 1 || returnMode > 1) return false;
                    stream.Position = 25;
                    int eventCount = reader.ReadByte();
                    stream.Position += eventCount * 5;
                    int count = reader.ReadByte();
                    if (count == 0 || count > 50) return false;
                    var speed = new double[count];
                    double duration = 0;
                    for (int i = 0; i < count; i++)
                    {
                        reader.ReadByte(); // animation code
                        reader.ReadByte(); // direction/frame code
                        speed[i] = reader.ReadDouble();
                        uint hold = reader.ReadUInt32();
                        if (double.IsNaN(speed[i]) || double.IsInfinity(speed[i]) || speed[i] < 0 || hold > 30000)
                            return false;
                        duration += hold;
                    }
                    // Other path/phase mappings need further decoding; do not guess them.
                    if (reader.ReadByte() != count) return false;
                    double previousX = 0, previousY = 0;
                    for (int i = 0; i < count; i++)
                    {
                        double x = reader.ReadInt32(), y = reader.ReadInt32();
                        double distance = Math.Max(Math.Abs(x - previousX), Math.Abs(y - previousY));
                        // Native movement advances the dominant axis in pixels per millisecond.
                        double pixelsPerMs = i == 0 ? (approachMode == 0 ? 0.6 : 0.8) : speed[i];
                        if (distance > 0)
                        {
                            if (pixelsPerMs <= 0) return false;
                            duration += distance / pixelsPerMs;
                        }
                        previousX = x;
                        previousY = y;
                    }
                    reader.ReadByte(); // effect count; effect tails are not decoded here
                    duration += Math.Max(Math.Abs(previousX), Math.Abs(previousY)) / (returnMode == 0 ? 0.6 : 0.8);
                    // Allow a scheduling tick per phase/approach/return. Native equal-position
                    // steps sleep 35ms. Camera, actor variants and effect tails remain estimates:
                    // callers preserve the old minimum and the user's extra pacing margin.
                    duration += (count + 2) * 35;
                    if (duration <= 0 || duration > 30000) return false;
                    estimateMs = (int)Math.Ceiling(duration);
                    return true;
                }
            }
            catch (EndOfStreamException) { return false; }
        }
    }
}
