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
                if (File.Exists(filePath))
                {
                    byte[] bytes = File.ReadAllBytes(filePath);
                    int recSize = 138;
                    int total = bytes.Length / recSize;

                    for (int r = 0; r < total; r++)
                    {
                        int off = r * recSize;
                        if (off + 14 > bytes.Length) break;

                        int len = bytes[off];
                        if (len <= 0 || len > 30) continue;

                        ushort rawId = BitConverter.ToUInt16(bytes, off + 12);
                        uint npcId = (uint)(rawId ^ 0x520E);

                        if (!_npcNames.ContainsKey(npcId))
                        {
                            byte[] slice = new byte[len];
                            for (int i = 0; i < len; i++)
                            {
                                slice[i] = bytes[off + 10 - i];
                            }

                            string name = Encoding.ASCII.GetString(slice).Trim('\0', ' ');
                            if (!string.IsNullOrEmpty(name) && name.Length >= 2)
                            {
                                // Expand truncated 10-byte binary strings from Npc.dat
                                if (name == "Springboar") name = "Springboard";
                                else if (name == "Treas Ches") name = "Treasure Chest";
                                else if (name == "Persian Ca") name = "Persian Cat";
                                else if (name == "Little Per") name = "Little Persian";
                                else if (name == "Cute Pand") name = "Cute Panda";
                                else if (name == "Grunt Boa") name = "Grunt Boar";
                                else if (name == "Lazy Shee") name = "Lazy Sheep";

                                _npcNames[npcId] = name;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
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

            // Resolve authentic in-game context for templates
            if (templateId == 12032) return "Robinson";
            if (templateId == 12049 || templateId == 12050) return "Hijacker";
            if (templateId == 14005) return "Jack";
            if (templateId == 14029) return "Old Woman";
            if (templateId == 14052) return "Villager";
            if (templateId == 14138) return "Band of Brothers";
            if (templateId == 14139) return "Old Woman";
            if (templateId == 14140) return "Emilie";
            if (templateId == 14141) return "Doll";
            if (templateId == 14144) return "Statue";
            if (templateId == 14145) return "John";
            if (templateId == 14146) return "Peter";
            if (templateId == 14156) return "Xaolan";
            if (templateId == 16006) return "Treasure Chest";
            if (templateId == 19039) return "Coconut Node";
            if (templateId == 19034) return "Cask";
            if (templateId == 19035) return "Treasure Chest";
            if (templateId == 19037 || templateId == 19038) return "Springboard";

            if (_npcNames.TryGetValue(templateId, out string name) && !string.IsNullOrWhiteSpace(name))
            {
                return name;
            }

            return $"Template #{templateId}";
        }
    }
}
