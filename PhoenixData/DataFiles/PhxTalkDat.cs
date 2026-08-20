using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace DataFiles
{
    public class PhxTalkDat
    {
        private readonly Dictionary<uint, string> _talkStrings = new Dictionary<uint, string>();

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
                byte[] data = File.ReadAllBytes(filePath);
                if (data.Length < 4) return;

                int maxSlots = data.Length / 4;

                for (uint talkId = 1; talkId < maxSlots; talkId++)
                {
                    int offset = BitConverter.ToInt32(data, (int)(talkId * 4));
                    if (offset > 0 && offset < data.Length - 4)
                    {
                        _talkOffsets[talkId] = offset;
                        string decoded = ExtractReversedString(data, offset);
                        if (!string.IsNullOrWhiteSpace(decoded))
                        {
                            _talkStrings[talkId] = decoded;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[PhxTalkDat] Error reading {filePath}: {ex.Message}");
            }
        }

        private string ExtractReversedString(byte[] data, int offset)
        {
            try
            {
                int end = Math.Min(data.Length, offset + 512);
                List<char> chars = new List<char>();

                for (int i = offset; i < end; i++)
                {
                    byte b = data[i];
                    if (b == 0)
                    {
                        if (chars.Count >= 3) break;
                    }
                    else if (b >= 32 && b <= 126)
                    {
                        chars.Add((char)b);
                    }
                    else if (chars.Count >= 3)
                    {
                        break;
                    }
                }

                if (chars.Count == 0) return null;

                chars.Reverse();
                string result = new string(chars.ToArray()).Trim();

                // Strip leading internal WLO control prefixes like "fffff" or color tags
                if (result.StartsWith("fffff"))
                {
                    result = result.Substring(5).Trim();
                }

                return result;
            }
            catch
            {
                return null;
            }
        }

        private readonly Dictionary<uint, int> _talkOffsets = new Dictionary<uint, int>();

        public int GetOffset(uint talkId)
        {
            if (_talkOffsets.TryGetValue(talkId, out var off))
            {
                return off;
            }
            return 0;
        }

        public string GetDialogue(uint talkId)
        {
            if (_talkStrings.TryGetValue(talkId, out var s))
            {
                return s;
            }
            return null;
        }

        public bool TryGetDialogue(uint talkId, out string dialogue)
        {
            return _talkStrings.TryGetValue(talkId, out dialogue);
        }

        public int Count => _talkStrings.Count;
    }
}
