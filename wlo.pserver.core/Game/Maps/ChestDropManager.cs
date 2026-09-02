using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Game.Maps
{
    public class ChestLootEntry
    {
        public ushort ItemID { get; set; }
        public string ItemName { get; set; }
        public byte Count { get; set; }
        public int Weight { get; set; }

        public ChestLootEntry() { }

        public ChestLootEntry(ushort itemId, string itemName, byte count = 1, int weight = 100)
        {
            ItemID = itemId;
            ItemName = itemName;
            Count = count;
            Weight = weight;
        }
    }

    public static class ChestDropManager
    {
        private static readonly Random _rng = new Random();
        private static readonly object _lock = new object();
        private static readonly string ConfigPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "chest_drops.txt");

        public static int DefaultRespawnSeconds { get; set; } = 60;

        // Map-specific loot pools (MapID -> List of entries)
        public static readonly Dictionary<uint, List<ChestLootEntry>> MapLootTables = new Dictionary<uint, List<ChestLootEntry>>();
        // Category fallback loot pools
        public static readonly Dictionary<string, List<ChestLootEntry>> CategoryLootTables = new Dictionary<string, List<ChestLootEntry>>(StringComparer.OrdinalIgnoreCase);

        static ChestDropManager()
        {
            InitializeLootTables();
            LoadFromFile();
        }

        public static void InitializeLootTables()
        {
            lock (_lock)
            {
                MapLootTables.Clear();
                CategoryLootTables.Clear();

                // 1. Map 10036 (Shipwreck Beach / South Island Coast)
                MapLootTables[10036] = new List<ChestLootEntry>
                {
                    new ChestLootEntry(41066, "1 pcs Coconut", 1, 40),
                    new ChestLootEntry(28014, "1 pcs Fresh Fruit", 1, 30),
                    new ChestLootEntry(28001, "1 pcs Sea Water", 1, 15),
                    new ChestLootEntry(27001, "1 pcs Ordinary Wood", 1, 15)
                };

                // 2. Map 10001 (Kelan Woods / Forests)
                MapLootTables[10001] = new List<ChestLootEntry>
                {
                    new ChestLootEntry(28006, "1 pcs Red Apple", 1, 35),
                    new ChestLootEntry(28012, "1 pcs Mushroom", 1, 25),
                    new ChestLootEntry(30001, "1 pcs Herb Potion", 1, 20),
                    new ChestLootEntry(27002, "1 pcs Pine Wood", 1, 20)
                };

                // 3. Map 10010 (Kelan Village / Residential)
                MapLootTables[10010] = new List<ChestLootEntry>
                {
                    new ChestLootEntry(30259, "1 pcs Black Medicine", 1, 40),
                    new ChestLootEntry(28003, "1 pcs Cooking Salt", 1, 20),
                    new ChestLootEntry(28015, "1 pcs White Rice", 1, 20),
                    new ChestLootEntry(28007, "1 pcs Fresh Milk", 1, 20)
                };

                // 4. Map 10020 (Maka Cave / Underground Mines)
                MapLootTables[10020] = new List<ChestLootEntry>
                {
                    new ChestLootEntry(24001, "1 pcs Iron Ore", 1, 35),
                    new ChestLootEntry(24002, "1 pcs Copper Ore", 1, 25),
                    new ChestLootEntry(24005, "1 pcs Coal", 1, 25),
                    new ChestLootEntry(24010, "1 pcs Gold Sand", 1, 15)
                };

                // Category Fallbacks
                CategoryLootTables["coconut"] = new List<ChestLootEntry>
                {
                    new ChestLootEntry(41066, "1 pcs Coconut", 1, 80),
                    new ChestLootEntry(28014, "1 pcs Fresh Fruit", 1, 20)
                };

                CategoryLootTables["medicine"] = new List<ChestLootEntry>
                {
                    new ChestLootEntry(30259, "1 pcs Black Medicine", 1, 70),
                    new ChestLootEntry(30001, "1 pcs Herb Potion", 1, 30)
                };

                CategoryLootTables["headband"] = new List<ChestLootEntry>
                {
                    new ChestLootEntry(22061, "1 pcs Headband", 1, 100)
                };

                CategoryLootTables["ore"] = new List<ChestLootEntry>
                {
                    new ChestLootEntry(24001, "1 pcs Iron Ore", 1, 40),
                    new ChestLootEntry(24002, "1 pcs Copper Ore", 1, 30),
                    new ChestLootEntry(24005, "1 pcs Coal", 1, 30)
                };

                CategoryLootTables["default_chest"] = new List<ChestLootEntry>
                {
                    new ChestLootEntry(28014, "1 pcs Fresh Fruit", 1, 40),
                    new ChestLootEntry(30001, "1 pcs Herb Potion", 1, 30),
                    new ChestLootEntry(27001, "1 pcs Ordinary Wood", 1, 20),
                    new ChestLootEntry(28001, "1 pcs Sea Water", 1, 10)
                };
            }
        }

        public static void SaveToFile(string path = null)
        {
            lock (_lock)
            {
                try
                {
                    string target = path ?? ConfigPath;
                    string dir = Path.GetDirectoryName(target);
                    if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                        Directory.CreateDirectory(dir);

                    StringBuilder sb = new StringBuilder();
                    sb.AppendLine($"# RespawnSeconds={DefaultRespawnSeconds}");

                    foreach (var kvp in MapLootTables)
                    {
                        sb.AppendLine($"[MAP:{kvp.Key}]");
                        foreach (var entry in kvp.Value)
                        {
                            sb.AppendLine($"{entry.ItemID}|{entry.ItemName}|{entry.Count}|{entry.Weight}");
                        }
                    }

                    foreach (var kvp in CategoryLootTables)
                    {
                        sb.AppendLine($"[CAT:{kvp.Key}]");
                        foreach (var entry in kvp.Value)
                        {
                            sb.AppendLine($"{entry.ItemID}|{entry.ItemName}|{entry.Count}|{entry.Weight}");
                        }
                    }

                    File.WriteAllText(target, sb.ToString(), Encoding.UTF8);
                }
                catch { }
            }
        }

        public static void LoadFromFile(string path = null)
        {
            lock (_lock)
            {
                try
                {
                    string target = path ?? ConfigPath;
                    if (!File.Exists(target))
                    {
                        SaveToFile(target);
                        return;
                    }

                    string[] lines = File.ReadAllLines(target, Encoding.UTF8);
                    string currentTarget = null;
                    bool isMap = false;
                    List<ChestLootEntry> currentList = null;

                    foreach (var rawLine in lines)
                    {
                        string line = rawLine.Trim();
                        if (string.IsNullOrEmpty(line) || line.StartsWith("//")) continue;

                        if (line.StartsWith("# RespawnSeconds="))
                        {
                            if (int.TryParse(line.Substring("# RespawnSeconds=".Length), out int sec) && sec > 0)
                                DefaultRespawnSeconds = sec;
                            continue;
                        }

                        if (line.StartsWith("[MAP:") && line.EndsWith("]"))
                        {
                            if (currentTarget != null && currentList != null)
                            {
                                if (isMap && uint.TryParse(currentTarget, out uint mid))
                                    MapLootTables[mid] = currentList;
                                else if (!isMap)
                                    CategoryLootTables[currentTarget] = currentList;
                            }

                            currentTarget = line.Substring(5, line.Length - 6);
                            isMap = true;
                            currentList = new List<ChestLootEntry>();
                            continue;
                        }

                        if (line.StartsWith("[CAT:") && line.EndsWith("]"))
                        {
                            if (currentTarget != null && currentList != null)
                            {
                                if (isMap && uint.TryParse(currentTarget, out uint mid))
                                    MapLootTables[mid] = currentList;
                                else if (!isMap)
                                    CategoryLootTables[currentTarget] = currentList;
                            }

                            currentTarget = line.Substring(5, line.Length - 6);
                            isMap = false;
                            currentList = new List<ChestLootEntry>();
                            continue;
                        }

                        if (currentList != null && line.Contains("|"))
                        {
                            string[] parts = line.Split('|');
                            if (parts.Length >= 4 && ushort.TryParse(parts[0], out ushort itemId))
                            {
                                string name = parts[1];
                                byte count = byte.TryParse(parts[2], out byte c) ? c : (byte)1;
                                int weight = int.TryParse(parts[3], out int w) ? w : 100;
                                currentList.Add(new ChestLootEntry(itemId, name, count, weight));
                            }
                        }
                    }

                    if (currentTarget != null && currentList != null)
                    {
                        if (isMap && uint.TryParse(currentTarget, out uint mid))
                            MapLootTables[mid] = currentList;
                        else if (!isMap)
                            CategoryLootTables[currentTarget] = currentList;
                    }
                }
                catch { }
            }
        }

        public static List<ChestLootEntry> GetLootForTarget(string targetKey, bool isMap)
        {
            lock (_lock)
            {
                if (isMap && uint.TryParse(targetKey, out uint mapId))
                {
                    if (MapLootTables.TryGetValue(mapId, out var list))
                        return new List<ChestLootEntry>(list);
                }
                else if (!isMap)
                {
                    if (CategoryLootTables.TryGetValue(targetKey, out var list))
                        return new List<ChestLootEntry>(list);
                }
                return new List<ChestLootEntry>();
            }
        }

        public static void SetLootForTarget(string targetKey, bool isMap, List<ChestLootEntry> entries)
        {
            lock (_lock)
            {
                if (isMap && uint.TryParse(targetKey, out uint mapId))
                {
                    MapLootTables[mapId] = new List<ChestLootEntry>(entries);
                }
                else if (!isMap)
                {
                    CategoryLootTables[targetKey] = new List<ChestLootEntry>(entries);
                }
                SaveToFile();
            }
        }

        public static void RemoveTarget(string targetKey, bool isMap)
        {
            lock (_lock)
            {
                if (isMap && uint.TryParse(targetKey, out uint mapId))
                {
                    MapLootTables.Remove(mapId);
                }
                else if (!isMap)
                {
                    CategoryLootTables.Remove(targetKey);
                }
                SaveToFile();
            }
        }

        /// <summary>
        /// Rolls an authentic drop from the map or category loot pool.
        /// </summary>
        public static ChestLootEntry RollDrop(uint mapId, string propName)
        {
            lock (_lock)
            {
                string lower = (propName ?? "").ToLower().Trim();

                // Specific Category Matches
                if (lower.Contains("coconut") && CategoryLootTables.ContainsKey("coconut"))
                    return PickRandom(CategoryLootTables["coconut"]);
                if ((lower.Contains("cabinet") || lower.Contains("shelf") || lower.Contains("bick")) && CategoryLootTables.ContainsKey("medicine"))
                    return PickRandom(CategoryLootTables["medicine"]);
                if ((lower.Contains("headband") || lower.Contains("bush")) && CategoryLootTables.ContainsKey("headband"))
                    return PickRandom(CategoryLootTables["headband"]);
                if ((lower.Contains("mine") || lower.Contains("ore") || lower.Contains("vein")) && CategoryLootTables.ContainsKey("ore"))
                    return PickRandom(CategoryLootTables["ore"]);

                // Map specific match
                if (MapLootTables.TryGetValue(mapId, out var mapList) && mapList.Count > 0)
                {
                    return PickRandom(mapList);
                }

                // Default chest fallback
                if (CategoryLootTables.TryGetValue("default_chest", out var def) && def.Count > 0)
                    return PickRandom(def);

                return new ChestLootEntry(28014, "1 pcs Fresh Fruit", 1, 100);
            }
        }

        private static ChestLootEntry PickRandom(List<ChestLootEntry> entries)
        {
            if (entries == null || entries.Count == 0)
                return new ChestLootEntry(28014, "1 pcs Fresh Fruit", 1, 100);

            int totalWeight = entries.Sum(e => e.Weight);
            if (totalWeight <= 0) return entries[0];

            int roll = _rng.Next(0, totalWeight);
            int cumulative = 0;

            foreach (var entry in entries)
            {
                cumulative += entry.Weight;
                if (roll < cumulative)
                    return entry;
            }

            return entries[entries.Count - 1];
        }
    }
}
