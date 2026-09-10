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
        private static string ConfigPath => RCLibrary.Core.PathHelper.GetDataFilePath("chest_drops.txt");

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

        public static void VerifyTable()
        {
            try
            {
                bool needsRecreate = false;
                try
                {
                    var testDt = RCLibrary.Core.DataBase.Query("SELECT map_id FROM chest_drops LIMIT 1;");
                    if (testDt == null) needsRecreate = true;
                }
                catch
                {
                    needsRecreate = true;
                }

                if (needsRecreate)
                {
                    RCLibrary.Core.DataBase.Execute("DROP TABLE IF EXISTS chest_drops;");
                }

                RCLibrary.Core.DataBase.Execute(@"CREATE TABLE IF NOT EXISTS chest_drops (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    map_id INT DEFAULT 0,
                    category TEXT,
                    item_id INT NOT NULL,
                    item_name TEXT,
                    count INT DEFAULT 1,
                    chance INT DEFAULT 20
                );");
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[ChestDropManager] Error verifying chest_drops table: {ex.Message}");
            }
        }

        public static void SaveToDatabase()
        {
            lock (_lock)
            {
                try
                {
                    VerifyTable();
                    RCLibrary.Core.DataBase.Execute("DELETE FROM chest_drops;");

                    foreach (var kvp in MapLootTables)
                    {
                        foreach (var entry in kvp.Value)
                        {
                            string sql = $"INSERT INTO chest_drops (map_id, category, item_id, item_name, count, chance) VALUES ({kvp.Key}, NULL, {entry.ItemID}, '{entry.ItemName.Replace("'", "''")}', {entry.Count}, {entry.Weight});";
                            RCLibrary.Core.DataBase.Execute(sql);
                        }
                    }

                    foreach (var kvp in CategoryLootTables)
                    {
                        foreach (var entry in kvp.Value)
                        {
                            string sql = $"INSERT INTO chest_drops (map_id, category, item_id, item_name, count, chance) VALUES (0, '{kvp.Key.Replace("'", "''")}', {entry.ItemID}, '{entry.ItemName.Replace("'", "''")}', {entry.Count}, {entry.Weight});";
                            RCLibrary.Core.DataBase.Execute(sql);
                        }
                    }
                }
                catch (Exception ex)
                {
                    DebugSystem.Write($"[ChestDropManager] Error saving to database: {ex.Message}");
                }
            }
        }

        public static void SaveToFile(string path = null)
        {
            SaveToDatabase();
        }

        public static void LoadFromFile(string path = null) => LoadFromDatabase();

        public static void LoadFromDatabase()
        {
            lock (_lock)
            {
                try
                {
                    VerifyTable();
                    var dt = RCLibrary.Core.DataBase.Query("SELECT * FROM chest_drops;");

                    if (dt == null || dt.Rows.Count == 0)
                    {
                        // Seed database
                        SeedDatabase();
                        dt = RCLibrary.Core.DataBase.Query("SELECT * FROM chest_drops;");
                    }

                    MapLootTables.Clear();
                    CategoryLootTables.Clear();

                    if (dt != null)
                    {
                        foreach (System.Data.DataRow row in dt.Rows)
                        {
                            uint mid = Convert.ToUInt32(row["map_id"]);
                            string cat = row["category"]?.ToString();
                            ushort itemId = Convert.ToUInt16(row["item_id"]);
                            string itemName = row["item_name"]?.ToString() ?? "";
                            byte count = Convert.ToByte(row["count"]);
                            int weight = Convert.ToInt32(row["chance"]);

                            var entry = new ChestLootEntry(itemId, itemName, count, weight);

                            if (mid > 0)
                            {
                                if (!MapLootTables.ContainsKey(mid)) MapLootTables[mid] = new List<ChestLootEntry>();
                                MapLootTables[mid].Add(entry);
                            }
                            else if (!string.IsNullOrEmpty(cat))
                            {
                                if (!CategoryLootTables.ContainsKey(cat)) CategoryLootTables[cat] = new List<ChestLootEntry>();
                                CategoryLootTables[cat].Add(entry);
                            }
                        }
                    }

                    DebugSystem.Write($"[ChestDropManager] Loaded {MapLootTables.Count} map drop tables and {CategoryLootTables.Count} category drop tables from SQLite database.");
                }
                catch (Exception ex)
                {
                    DebugSystem.Write($"[ChestDropManager] Error loading chest drops: {ex.Message}");
                }
            }
        }

        private static void SeedDatabase()
        {
            // Seed defaults
            InitializeLootTables();
            SaveToDatabase();
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
