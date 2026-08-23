using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace DataFiles
{
    /// <summary>
    /// High-performance parser and lookup engine for authentic Wonderland Online Talk.dat dialogue database.
    /// Format: Exactly 17,494 records of 292 bytes each.
    /// Supports both 1-based Record Index (1..17,494) and Direct Byte Offset lookups (e.g. 0x063ED2).
    /// </summary>
    public class PhxTalkDat
    {
        private readonly Dictionary<uint, string> _talkById = new Dictionary<uint, string>();
        private readonly Dictionary<uint, string> _talkByIndex = new Dictionary<uint, string>();
        private readonly Dictionary<uint, string> _talkByOffset = new Dictionary<uint, string>();
        private readonly Dictionary<uint, int> _talkOffsets = new Dictionary<uint, int>();

        public IReadOnlyDictionary<uint, string> AllDialogues => _talkById.Count > 0 ? _talkById : _talkByIndex;
        public int Count => _talkById.Count > 0 ? _talkById.Count : _talkByIndex.Count;

        public PhxTalkDat(string filePath)
        {
            if (File.Exists(filePath))
            {
                Load(filePath);
            }
        }

        public void Load(string filePath)
        {
            try
            {
                _talkById.Clear();
                _talkByIndex.Clear();
                _talkByOffset.Clear();
                _talkOffsets.Clear();

                byte[] data = File.ReadAllBytes(filePath);
                const int recordSize = 292;
                int totalRecords = data.Length / recordSize;

                for (uint r = 0; r < totalRecords; r++)
                {
                    int recOffset = (int)(r * recordSize);
                    if (recOffset + recordSize > data.Length) break;

                    ushort talkId = BitConverter.ToUInt16(data, recOffset);
                    int len = data[recOffset + 2];
                    if (len <= 0 || len > 250) continue;

                    // The reversed text ends right before the 35-byte tail (fffff prefix + 30-byte metadata footer)
                    int textStart = recOffset + recordSize - 35 - len;
                    if (textStart < recOffset || textStart + len > data.Length) continue;

                    byte[] textBytes = new byte[len];
                    for (int i = 0; i < len; i++)
                    {
                        textBytes[i] = data[textStart + len - 1 - i]; // Reverse text while copying
                    }

                    string dialogue = Encoding.Default.GetString(textBytes).Trim();
                    if (string.IsNullOrWhiteSpace(dialogue)) continue;

                    // Strip leading internal control tag fffff if present
                    if (dialogue.StartsWith("fffff"))
                    {
                        dialogue = dialogue.Substring(5).Trim();
                    }

                    if (string.IsNullOrWhiteSpace(dialogue)) continue;

                    if (talkId > 0)
                    {
                        _talkById[talkId] = dialogue;
                        _talkOffsets[talkId] = recOffset;
                    }

                    _talkByIndex[r] = dialogue;
                    _talkByIndex[r + 1] = dialogue;
                    _talkByOffset[(uint)textStart] = dialogue;
                    _talkByOffset[(uint)recOffset] = dialogue;
                }

                TalkResolver.Initialize(this);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[PhxTalkDat] Error reading {filePath}: {ex.Message}");
            }
        }

        public bool TryGetByRecordIndex(uint recordIndex, out string text)
        {
            return _talkByIndex.TryGetValue(recordIndex, out text);
        }

        public bool TryGetByOffset(uint offset, out string text)
        {
            return _talkByOffset.TryGetValue(offset, out text);
        }

        public bool TryGetById(uint id, out string text)
        {
            return _talkById.TryGetValue(id, out text);
        }

        public int GetOffset(uint talkId)
        {
            if (_talkOffsets.TryGetValue(talkId, out var off))
            {
                return off;
            }
            if (talkId > 20000)
            {
                return (int)talkId;
            }
            return (int)(talkId * 292);
        }

        public string GetDialogue(uint idOrOffset)
        {
            return TalkResolver.Resolve(idOrOffset);
        }

        public bool TryGetDialogue(uint idOrOffset, out string dialogue)
        {
            return TalkResolver.TryResolve(idOrOffset, out dialogue);
        }
    }
}
