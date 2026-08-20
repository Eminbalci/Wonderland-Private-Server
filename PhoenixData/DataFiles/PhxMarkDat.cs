using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace DataFiles
{
    public class QuestMarkEntry
    {
        public uint MarkID { get; set; }
        public string Title { get; set; }
        public string Location { get; set; }
        public string Description { get; set; }
    }

    public class PhxMarkDat
    {
        private readonly Dictionary<uint, QuestMarkEntry> _marks = new Dictionary<uint, QuestMarkEntry>();

        public PhxMarkDat(string filePath)
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
                if (data.Length < 256) return;

                int maxSlots = Math.Min(data.Length / 4, 30000);

                for (uint markId = 1; markId < maxSlots; markId++)
                {
                    int offset = BitConverter.ToInt32(data, (int)(markId * 4));
                    if (offset > 0 && offset < data.Length - 4)
                    {
                        var entry = ParseEntry(data, offset, markId);
                        if (entry != null && !string.IsNullOrWhiteSpace(entry.Title))
                        {
                            _marks[markId] = entry;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[PhxMarkDat] Error reading {filePath}: {ex.Message}");
            }
        }

        private QuestMarkEntry ParseEntry(byte[] data, int offset, uint markId)
        {
            try
            {
                int end = Math.Min(data.Length, offset + 256);
                List<string> extractedStrings = new List<string>();
                List<char> currChars = new List<char>();

                for (int i = offset; i < end; i++)
                {
                    byte b = data[i];
                    if (b >= 32 && b <= 126)
                    {
                        currChars.Add((char)b);
                    }
                    else
                    {
                        if (currChars.Count >= 3)
                        {
                            currChars.Reverse();
                            string s = new string(currChars.ToArray()).Trim();
                            if (!string.IsNullOrEmpty(s) && !s.Contains("'s's's"))
                            {
                                extractedStrings.Add(s);
                            }
                        }
                        currChars.Clear();
                    }
                }

                if (currChars.Count >= 3)
                {
                    currChars.Reverse();
                    string s = new string(currChars.ToArray()).Trim();
                    if (!string.IsNullOrEmpty(s) && !s.Contains("'s's's"))
                    {
                        extractedStrings.Add(s);
                    }
                }

                if (extractedStrings.Count == 0) return null;

                var entry = new QuestMarkEntry { MarkID = markId };
                entry.Title = extractedStrings[0];
                if (extractedStrings.Count > 1) entry.Location = extractedStrings[1];
                if (extractedStrings.Count > 2) entry.Description = extractedStrings[2];

                return entry;
            }
            catch
            {
                return null;
            }
        }

        public QuestMarkEntry GetMark(uint markId)
        {
            if (_marks.TryGetValue(markId, out var entry)) return entry;
            return null;
        }

        public IReadOnlyDictionary<uint, QuestMarkEntry> AllMarks => _marks;

        public int Count => _marks.Count;
    }
}
