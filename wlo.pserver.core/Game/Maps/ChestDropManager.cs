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
        public int Gold { get; set; }

        public bool IsGold => Gold > 0 || (ItemID == 0 && Gold > 0);

        public ChestLootEntry() { }

        public ChestLootEntry(ushort itemId, string itemName, byte count = 1, int weight = 100, int gold = 0)
        {
            ItemID = itemId;
            ItemName = itemName;
            Count = count;
            Weight = weight;
            Gold = gold;
        }

        public static ChestLootEntry CreateGold(int gold, int weight = 100)
        {
            return new ChestLootEntry(0, $"{gold} Gold", 1, weight, gold);
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

        public static bool IsBeachMap(uint mapId)
        {
            return mapId == 10035 || mapId == 10036 || mapId == 10037 || mapId == 10038 || mapId == 10039;
        }

        public static bool IsCrate(string propName, uint templateId = 0)
        {
            if (templateId == 19034 || templateId == 16001 || templateId == 16007 || 
                templateId == 19036 || templateId == 19037 || templateId == 19272)
                return true;

            string lower = (propName ?? "").ToLower().Trim();
            return lower.Contains("crate") || lower.Contains("cask") || lower.Contains("barrel") ||
                   lower.Contains("box") || lower.Contains("cargo") || lower.Contains("wreckage") ||
                   lower.Contains("driftwood");
        }

        public static bool IsChest(string propName, uint templateId = 0)
        {
            if (templateId == 19035 || templateId == 16000 || templateId == 16002 || 
                templateId == 16003 || templateId == 16006 || templateId == 16008 || 
                templateId == 16009 || templateId == 16010 || templateId == 16011 || 
                templateId == 19038 || templateId == 19114 || templateId == 19115 || 
                templateId == 19116 || templateId == 19117 || templateId == 19157)
                return true;

            string lower = (propName ?? "").ToLower().Trim();
            return lower.Contains("chest") || lower.Contains("ches");
        }

        public static void InitializeLootTables()
        {
            lock (_lock)
            {
                MapLootTables.Clear();
                CategoryLootTables.Clear();

                // 1. Beach Crates: Common survival resources (Wood, Melon, Healing items)
                CategoryLootTables["beach_crate"] = new List<ChestLootEntry>
                {
                    // Common survival resources: Wood
                    new ChestLootEntry(45001, "Ordinary Wood", 2, 35),
                    new ChestLootEntry(45005, "Wood", 2, 25),
                    new ChestLootEntry(45002, "Oak wood", 1, 15),

                    // Common survival resources: Melon
                    new ChestLootEntry(41028, "Melon", 1, 30),
                    new ChestLootEntry(41047, "Watermelon", 1, 25),
                    new ChestLootEntry(41053, "Muskmelon", 1, 15),

                    // Common survival resources: Healing & Survival food
                    new ChestLootEntry(41066, "Coconut", 2, 30),
                    new ChestLootEntry(32011, "Bread", 2, 25),
                    new ChestLootEntry(34014, "Tao Rice Ball", 1, 20),
                    new ChestLootEntry(33019, "Recovery Potion", 1, 20),
                    new ChestLootEntry(33046, "Recovery Medicine", 1, 15),
                    new ChestLootEntry(30259, "Black Medicine", 1, 10)
                };

                // 2. Beach Chests: Beginner equipment or gold
                CategoryLootTables["beach_chest"] = new List<ChestLootEntry>
                {
                    // Beginner weapons
                    new ChestLootEntry(10031, "Copper Knife", 1, 20),
                    new ChestLootEntry(10032, "Bronze Knife", 1, 15),
                    new ChestLootEntry(11031, "Copper Sword", 1, 20),
                    new ChestLootEntry(11032, "Bronze Sword", 1, 15),
                    new ChestLootEntry(13066, "Wooden Bow", 1, 15),
                    new ChestLootEntry(14093, "Wooden Wand", 1, 15),

                    // Beginner armor & wearables
                    new ChestLootEntry(22061, "Headband", 1, 25),
                    new ChestLootEntry(21073, "Cotton Clothes", 1, 20),
                    new ChestLootEntry(23064, "Wooden Bracers", 1, 20),
                    new ChestLootEntry(24013, "Wooden Shoes", 1, 20),

                    // Gold rewards
                    ChestLootEntry.CreateGold(100, 35),
                    ChestLootEntry.CreateGold(250, 25),
                    ChestLootEntry.CreateGold(500, 15)
                };

                // Map 10036 (Shipwreck Beach / South Island Coast) fallback table
                MapLootTables[10036] = new List<ChestLootEntry>(CategoryLootTables["beach_crate"]);

                // Map 10001 (Kelan Woods / Forests)
                MapLootTables[10001] = new List<ChestLootEntry>
                {
                    new ChestLootEntry(28006, "Red Apple", 1, 35),
                    new ChestLootEntry(28012, "Mushroom", 1, 25),
                    new ChestLootEntry(30001, "Herb Potion", 1, 20),
                    new ChestLootEntry(27002, "Pine Wood", 1, 20)
                };

                // Map 10010 (Kelan Village / Residential)
                MapLootTables[10010] = new List<ChestLootEntry>
                {
                    new ChestLootEntry(30259, "Black Medicine", 1, 40),
                    new ChestLootEntry(28003, "Cooking Salt", 1, 20),
                    new ChestLootEntry(28015, "White Rice", 1, 20),
                    new ChestLootEntry(28007, "Fresh Milk", 1, 20)
                };

                // Map 10020 (Maka Cave / Underground Mines)
                MapLootTables[10020] = new List<ChestLootEntry>
                {
                    new ChestLootEntry(24001, "Iron Ore", 1, 35),
                    new ChestLootEntry(24002, "Copper Ore", 1, 25),
                    new ChestLootEntry(24005, "Coal", 1, 25),
                    new ChestLootEntry(24010, "Gold Sand", 1, 15)
                };

                // Category Fallbacks
                CategoryLootTables["coconut"] = new List<ChestLootEntry>
                {
                    new ChestLootEntry(41066, "Coconut", 1, 80),
                    new ChestLootEntry(28014, "Fresh Fruit", 1, 20)
                };

                CategoryLootTables["medicine"] = new List<ChestLootEntry>
                {
                    new ChestLootEntry(30259, "Black Medicine", 1, 70),
                    new ChestLootEntry(30001, "Herb Potion", 1, 30)
                };

                CategoryLootTables["headband"] = new List<ChestLootEntry>
                {
                    new ChestLootEntry(22061, "Headband", 1, 100)
                };

                CategoryLootTables["ore"] = new List<ChestLootEntry>
                {
                    new ChestLootEntry(24001, "Iron Ore", 1, 40),
                    new ChestLootEntry(24002, "Copper Ore", 1, 30),
                    new ChestLootEntry(24005, "Coal", 1, 30)
                };

                CategoryLootTables["default_crate"] = new List<ChestLootEntry>(CategoryLootTables["beach_crate"]);

                CategoryLootTables["default_chest"] = new List<ChestLootEntry>
                {
                    new ChestLootEntry(28014, "Fresh Fruit", 1, 35),
                    new ChestLootEntry(30001, "Herb Potion", 1, 25),
                    new ChestLootEntry(45001, "Ordinary Wood", 1, 20),
                    ChestLootEntry.CreateGold(100, 20)
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
                    chance INT DEFAULT 20,
                    gold INT DEFAULT 0
                );");

                try
                {
                    RCLibrary.Core.DataBase.Execute("ALTER TABLE chest_drops ADD COLUMN gold INT DEFAULT 0;");
                }
                catch { }
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
                            string sql = $"INSERT INTO chest_drops (map_id, category, item_id, item_name, count, chance, gold) VALUES ({kvp.Key}, NULL, {entry.ItemID}, '{entry.ItemName.Replace("'", "''")}', {entry.Count}, {entry.Weight}, {entry.Gold});";
                            RCLibrary.Core.DataBase.Execute(sql);
                        }
                    }

                    foreach (var kvp in CategoryLootTables)
                    {
                        foreach (var entry in kvp.Value)
                        {
                            string sql = $"INSERT INTO chest_drops (map_id, category, item_id, item_name, count, chance, gold) VALUES (0, '{kvp.Key.Replace("'", "''")}', {entry.ItemID}, '{entry.ItemName.Replace("'", "''")}', {entry.Count}, {entry.Weight}, {entry.Gold});";
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

                    if (dt != null && dt.Rows.Count > 0)
                    {
                        MapLootTables.Clear();
                        CategoryLootTables.Clear();

                        foreach (System.Data.DataRow row in dt.Rows)
                        {
                            uint mid = Convert.ToUInt32(row["map_id"]);
                            string cat = row["category"]?.ToString();
                            ushort itemId = Convert.ToUInt16(row["item_id"]);
                            string itemName = row["item_name"]?.ToString() ?? "";
                            byte count = Convert.ToByte(row["count"]);
                            int weight = Convert.ToInt32(row["chance"]);
                            int gold = 0;
                            if (row.Table.Columns.Contains("gold") && row["gold"] != DBNull.Value)
                                gold = Convert.ToInt32(row["gold"]);

                            var entry = new ChestLootEntry(itemId, itemName, count, weight, gold);

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

                        DebugSystem.Write($"[ChestDropManager] Loaded {MapLootTables.Count} map drop tables and {CategoryLootTables.Count} category drop tables from SQLite database.");
                    }
                }
                catch (Exception ex)
                {
                    DebugSystem.Write($"[ChestDropManager] Error loading chest drops: {ex.Message}");
                }
            }
        }

        private static void SeedDatabase()
        {
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
        /// Rolls an authentic drop from the map or category loot pool with template ID awareness.
        /// </summary>
        public static ChestLootEntry RollDrop(uint mapId, string propName, uint templateId = 0)
        {
            lock (_lock)
            {
                string lower = (propName ?? "").ToLower().Trim();

                // 1. Beach maps specific crate vs chest differentiation
                if (IsBeachMap(mapId))
                {
                    if (IsCrate(propName, templateId) && CategoryLootTables.TryGetValue("beach_crate", out var beachCrates) && beachCrates.Count > 0)
                        return PickRandom(beachCrates);

                    if (IsChest(propName, templateId) && CategoryLootTables.TryGetValue("beach_chest", out var beachChests) && beachChests.Count > 0)
                        return PickRandom(beachChests);
                }

                // 2. Specific Resource / Gathering Category Matches
                if (lower.Contains("coconut") && CategoryLootTables.ContainsKey("coconut"))
                    return PickRandom(CategoryLootTables["coconut"]);
                if ((lower.Contains("cabinet") || lower.Contains("shelf") || lower.Contains("bick")) && CategoryLootTables.ContainsKey("medicine"))
                    return PickRandom(CategoryLootTables["medicine"]);
                if ((lower.Contains("headband") || lower.Contains("bush")) && CategoryLootTables.ContainsKey("headband"))
                    return PickRandom(CategoryLootTables["headband"]);
                if ((lower.Contains("mine") || lower.Contains("ore") || lower.Contains("vein")) && CategoryLootTables.ContainsKey("ore"))
                    return PickRandom(CategoryLootTables["ore"]);

                // 3. Generic Crate category fallback
                if (IsCrate(propName, templateId))
                {
                    if (CategoryLootTables.TryGetValue("beach_crate", out var crates) && crates.Count > 0)
                        return PickRandom(crates);
                }

                // 4. Map specific match
                if (MapLootTables.TryGetValue(mapId, out var mapList) && mapList.Count > 0)
                {
                    return PickRandom(mapList);
                }

                // 5. Default chest fallback
                if (CategoryLootTables.TryGetValue("default_chest", out var def) && def.Count > 0)
                    return PickRandom(def);

                return new ChestLootEntry(45001, "Ordinary Wood", 1, 100);
            }
        }

        private static ChestLootEntry PickRandom(List<ChestLootEntry> entries)
        {
            if (entries == null || entries.Count == 0)
                return new ChestLootEntry(45001, "Ordinary Wood", 1, 100);

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
