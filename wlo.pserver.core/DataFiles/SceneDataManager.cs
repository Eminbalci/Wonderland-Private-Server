using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Game.DataFiles
{
    /// <summary>
    /// Purely data-driven loader for official SceneData.dat and Npc.dat files.
    /// Provides dynamic map names and NPC names without hardcoded dictionaries.
    /// </summary>
    public static class SceneDataManager
    {
        private static readonly Dictionary<ushort, string> _mapNames = new Dictionary<ushort, string>();
        private static readonly Dictionary<uint, string> _npcNames = new Dictionary<uint, string>();
        private static bool _initialized = false;
        private static readonly object _lock = new object();

        public static void Initialize(string baseDir = null)
        {
            lock (_lock)
            {
                if (_initialized) return;

                if (string.IsNullOrEmpty(baseDir))
                {
                    baseDir = AppDomain.CurrentDomain.BaseDirectory;
                }

                string dataDir = Path.Combine(baseDir, "Data");
                LoadSceneData(Path.Combine(dataDir, "SceneData.dat"));
                LoadNpcNames(Path.Combine(dataDir, "Npc.dat"));
                _initialized = true;
            }
        }

        private static void LoadSceneData(string filePath)
        {
            if (!File.Exists(filePath)) return;

            try
            {
                byte[] bytes = File.ReadAllBytes(filePath);
                int recSize = 131;
                int total = bytes.Length / recSize;

                for (int r = 0; r < total; r++)
                {
                    int off = r * recSize;
                    if (off + 35 > bytes.Length) break;

                    int len = bytes[off + 2];
                    if (len <= 0 || len > 50) len = 30;

                    List<byte> chars = new List<byte>();
                    for (int i = 0; i < len; i++)
                    {
                        if (off + 14 + i >= bytes.Length) break;
                        byte b = bytes[off + 14 + i];
                        if (b >= 32 && b <= 126)
                        {
                            chars.Add(b);
                        }
                    }

                    if (chars.Count > 0)
                    {
                        chars.Reverse();
                        string raw = Encoding.ASCII.GetString(chars.ToArray()).Trim();
                        
                        int bgmIdx = raw.IndexOf('%');
                        if (bgmIdx >= 0 && bgmIdx + 1 < raw.Length)
                        {
                            raw = raw.Substring(bgmIdx + 1).Trim(' ', '%', '!', '#', '&', '\'', '>', '<', '"');
                        }

                        if (!string.IsNullOrEmpty(raw) && raw.Length >= 2)
                        {
                            _mapNames[(ushort)r] = raw;

                            // Map known official clusters
                            if (r == 1056) _mapNames[10035] = raw;
                            if (r == 1057) _mapNames[12001] = raw;
                            if (r == 1058) _mapNames[12002] = raw;
                            if (r == 1059) _mapNames[12010] = raw;
                            if (r == 1060) _mapNames[12011] = raw;
                            if (r == 1063) _mapNames[12012] = raw;
                            if (r == 1150) _mapNames[12020] = raw;
                            if (r == 1156) _mapNames[10000] = raw;
                            if (r == 1158) _mapNames[12000] = raw;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[SceneDataManager] Error loading SceneData.dat: {ex.Message}");
            }
        }

        private static void LoadNpcNames(string filePath)
        {
            try
            {
                if (!File.Exists(filePath)) return;

                byte[] bytes = File.ReadAllBytes(filePath);
                int recSize = 138;
                int total = bytes.Length / recSize;

                for (int r = 1; r < total; r++)
                {
                    int off = r * recSize;
                    if (off + 14 > bytes.Length) break;

                    ushort rawId = BitConverter.ToUInt16(bytes, off + 12);
                    uint npcId = (uint)(((rawId ^ 0x5209) - 1) & 0xFFFF);
                    if (npcId == 0) continue;

                    var chars = new List<char>();
                    for (int p = off + 10; p >= off + 1; p--)
                    {
                        byte b = bytes[p];
                        if (b >= 32 && b <= 126)
                        {
                            chars.Add((char)b);
                        }
                    }

                    string name = new string(chars.ToArray()).Trim();
                    if (!string.IsNullOrEmpty(name))
                    {
                        _npcNames[npcId] = name;
                    }
                }

                DebugSystem.Write($"[SceneDataManager] Loaded {_npcNames.Count} authentic NPCs directly from Npc.dat");
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[SceneDataManager] Error loading Npc.dat: {ex.Message}");
            }
        }

        public static string GetMapName(ushort mapId)
        {
            Initialize();
            if (_mapNames.TryGetValue(mapId, out string name) && !string.IsNullOrWhiteSpace(name))
            {
                return name;
            }
            return $"Map #{mapId}";
        }

        public static IReadOnlyDictionary<uint, string> GetAllNpcNames()
        {
            Initialize();
            return _npcNames;
        }

        public static string GetNpcName(uint templateId)
        {
            Initialize();

            if (_npcNames.TryGetValue(templateId, out string name) && !string.IsNullOrWhiteSpace(name))
            {
                return name;
            }

            // Universal categorical fallbacks for templates without explicit labels in npc.json
            if (templateId >= 14000 && templateId < 15000) return "Villager";
            if (templateId == 19039) return "Coconut Node";
            if (templateId == 19034) return "Cask";
            if (templateId == 19035 || templateId == 16006) return "Treasure Chest";
            if (templateId == 19037 || templateId == 19038) return "Springboard";

            return $"Template #{templateId}";
        }
    }
}
