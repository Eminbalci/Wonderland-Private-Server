using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Game.Battle
{
    public class MonsterDropEntry
    {
        public ushort ItemID { get; set; }
        public string ItemName { get; set; }
        public byte MinCount { get; set; } = 1;
        public byte MaxCount { get; set; } = 1;
        public double DropRatePercent { get; set; } = 50.0; // 0.0 - 100.0%

        public MonsterDropEntry() { }

        public MonsterDropEntry(ushort itemId, string itemName, byte minCount = 1, byte maxCount = 1, double dropRate = 50.0)
        {
            ItemID = itemId;
            ItemName = itemName;
            MinCount = minCount;
            MaxCount = maxCount;
            DropRatePercent = dropRate;
        }
    }

    public class RolledDropItem
    {
        public ushort ItemID { get; set; }
        public string ItemName { get; set; }
        public byte Count { get; set; }

        public RolledDropItem(ushort itemId, string itemName, byte count)
        {
            ItemID = itemId;
            ItemName = itemName;
            Count = count;
        }
    }

    public static class MonsterDropManager
    {
        private static readonly Random _rng = new Random();
        private static readonly object _lock = new object();
        private static string ConfigPath => RCLibrary.Core.PathHelper.GetDataFilePath("monster_drops.txt");

        // Monster Template ID -> List of drop entries
        public static readonly Dictionary<uint, List<MonsterDropEntry>> MonsterLootTables = new Dictionary<uint, List<MonsterDropEntry>>();
        
        // Monster Name Pattern -> List of drop entries
        public static readonly Dictionary<string, List<MonsterDropEntry>> PatternLootTables = new Dictionary<string, List<MonsterDropEntry>>(StringComparer.OrdinalIgnoreCase);

        // Global fallback drops by monster level brackets
        public static readonly Dictionary<int, List<MonsterDropEntry>> LevelBracketLootTables = new Dictionary<int, List<MonsterDropEntry>>();

        public static event Action OnLootTablesChanged;

        public static Dictionary<uint, List<MonsterDropEntry>> GetAllDrops()
        {
            lock (_lock)
            {
                return new Dictionary<uint, List<MonsterDropEntry>>(MonsterLootTables);
            }
        }

        public static List<MonsterDropEntry> GetDrops(uint monsterTid)
        {
            lock (_lock)
            {
                if (MonsterLootTables.TryGetValue(monsterTid, out var list))
                {
                    return new List<MonsterDropEntry>(list);
                }
                return new List<MonsterDropEntry>();
            }
        }

        public static void AddOrUpdateDrop(uint monsterTid, ushort itemId, string itemName, byte minCount, byte maxCount, double dropRate)
        {
            lock (_lock)
            {
                if (!MonsterLootTables.ContainsKey(monsterTid))
                {
                    MonsterLootTables[monsterTid] = new List<MonsterDropEntry>();
                }

                var list = MonsterLootTables[monsterTid];
                var existing = list.FirstOrDefault(e => e.ItemID == itemId);
                if (existing != null)
                {
                    existing.ItemName = itemName;
                    existing.MinCount = minCount;
                    existing.MaxCount = maxCount;
                    existing.DropRatePercent = dropRate;
                }
                else
                {
                    list.Add(new MonsterDropEntry(itemId, itemName, minCount, maxCount, dropRate));
                }
            }
            SaveToFile();
            OnLootTablesChanged?.Invoke();
        }

        public static bool RemoveDrop(uint monsterTid, ushort itemId)
        {
            bool removed = false;
            lock (_lock)
            {
                if (MonsterLootTables.TryGetValue(monsterTid, out var list))
                {
                    int count = list.RemoveAll(e => e.ItemID == itemId);
                    removed = count > 0;
                    if (list.Count == 0)
                    {
                        MonsterLootTables.Remove(monsterTid);
                    }
                }
            }
            if (removed)
            {
                SaveToFile();
                OnLootTablesChanged?.Invoke();
            }
            return removed;
        }

        public static bool ClearMonsterDrops(uint monsterTid)
        {
            bool removed = false;
            lock (_lock)
            {
                removed = MonsterLootTables.Remove(monsterTid);
            }
            if (removed)
            {
                SaveToFile();
                OnLootTablesChanged?.Invoke();
            }
            return removed;
        }

        public static void ClearAllDrops()
        {
            lock (_lock)
            {
                MonsterLootTables.Clear();
                PatternLootTables.Clear();
                LevelBracketLootTables.Clear();
            }
            SaveToFile();
            OnLootTablesChanged?.Invoke();
        }

        static MonsterDropManager()
        {
            InitializeLootTables();
            LoadFromFile();
        }

        public static void InitializeLootTables()
        {
            lock (_lock)
            {
                MonsterLootTables.Clear();
                PatternLootTables.Clear();
                LevelBracketLootTables.Clear();

                // 1. Wolf / Wolf Guard (TID 11066)
                MonsterLootTables[11066] = new List<MonsterDropEntry>
                {
                    new MonsterDropEntry(30001, "Wolf Meat", 1, 2, 70.0),
                    new MonsterDropEntry(30015, "Wolf Pelt", 1, 1, 55.0),
                    new MonsterDropEntry(30020, "Beast Fang", 1, 1, 40.0),
                    new MonsterDropEntry(30201, "Small HP Potion", 1, 1, 30.0)
                };

                // 2. Wild Boar (TID 11012)
                MonsterLootTables[11012] = new List<MonsterDropEntry>
                {
                    new MonsterDropEntry(30002, "Pork Meat", 1, 2, 75.0),
                    new MonsterDropEntry(30016, "Boar Leather", 1, 1, 50.0),
                    new MonsterDropEntry(30021, "Boar Tusk", 1, 1, 35.0),
                    new MonsterDropEntry(28014, "Fresh Fruit", 1, 1, 30.0)
                };

                // 3. Crab / Beach Crawler (TID 11005)
                MonsterLootTables[11005] = new List<MonsterDropEntry>
                {
                    new MonsterDropEntry(30003, "Crab Meat", 1, 2, 70.0),
                    new MonsterDropEntry(28002, "Seaweed", 1, 2, 60.0),
                    new MonsterDropEntry(27010, "Shell Fragment", 1, 1, 45.0),
                    new MonsterDropEntry(46001, "Small Pearl", 1, 1, 15.0)
                };

                // 4. Snake / Viper (TID 11018)
                MonsterLootTables[11018] = new List<MonsterDropEntry>
                {
                    new MonsterDropEntry(30010, "Snake Gall", 1, 1, 65.0),
                    new MonsterDropEntry(30017, "Snake Skin", 1, 1, 50.0),
                    new MonsterDropEntry(30205, "Antidote Herb", 1, 1, 35.0)
                };

                // 5. Cave Bat (TID 11022)
                MonsterLootTables[11022] = new List<MonsterDropEntry>
                {
                    new MonsterDropEntry(30011, "Bat Wing", 1, 2, 70.0),
                    new MonsterDropEntry(30022, "Bat Fang", 1, 1, 45.0),
                    new MonsterDropEntry(27020, "Dark Stone", 1, 1, 25.0)
                };

                // 6. Tree Spirit / Treant (TID 11030)
                MonsterLootTables[11030] = new List<MonsterDropEntry>
                {
                    new MonsterDropEntry(27001, "Ordinary Wood", 1, 3, 80.0),
                    new MonsterDropEntry(30012, "Tree Sap", 1, 2, 55.0),
                    new MonsterDropEntry(28015, "Magic Leaf", 1, 1, 35.0),
                    new MonsterDropEntry(48001, "Wooden Plank", 1, 1, 20.0)
                };

                // 7. Tiger (TID 11050)
                MonsterLootTables[11050] = new List<MonsterDropEntry>
                {
                    new MonsterDropEntry(30004, "Tiger Meat", 1, 2, 65.0),
                    new MonsterDropEntry(30018, "Tiger Fur", 1, 1, 50.0),
                    new MonsterDropEntry(30023, "Tiger Claw", 1, 1, 35.0),
                    new MonsterDropEntry(46005, "Gold Nugget", 1, 1, 15.0)
                };

                // 8. Grape Monster (TID 17003, 1831, 154)
                MonsterLootTables[17003] = new List<MonsterDropEntry>
                {
                    new MonsterDropEntry(28005, "Grape", 1, 2, 75.0),
                    new MonsterDropEntry(28014, "Fresh Fruit", 1, 1, 45.0),
                    new MonsterDropEntry(30201, "Small HP Potion", 1, 1, 30.0)
                };
                MonsterLootTables[1831] = MonsterLootTables[17003];
                MonsterLootTables[154] = MonsterLootTables[17003];

                // 9. Apple Monster (TID 17001)
                MonsterLootTables[17001] = new List<MonsterDropEntry>
                {
                    new MonsterDropEntry(28006, "Red Apple", 1, 2, 75.0),
                    new MonsterDropEntry(28014, "Fresh Fruit", 1, 1, 45.0),
                    new MonsterDropEntry(30201, "Small HP Potion", 1, 1, 30.0)
                };

                // 10. Pineapple Monster (TID 17002, 1832, 223)
                MonsterLootTables[17002] = new List<MonsterDropEntry>
                {
                    new MonsterDropEntry(28007, "Small Pineapple", 1, 2, 75.0),
                    new MonsterDropEntry(28014, "Fresh Fruit", 1, 1, 45.0),
                    new MonsterDropEntry(30201, "Small HP Potion", 1, 1, 30.0)
                };
                MonsterLootTables[1832] = MonsterLootTables[17002];
                MonsterLootTables[223] = MonsterLootTables[17002];

                // 11. Kiwi Monster (TID 17004, 1830, 222)
                MonsterLootTables[17004] = new List<MonsterDropEntry>
                {
                    new MonsterDropEntry(28008, "Little Gooseberry", 1, 2, 75.0),
                    new MonsterDropEntry(28014, "Fresh Fruit", 1, 1, 45.0),
                    new MonsterDropEntry(30201, "Small HP Potion", 1, 1, 30.0)
                };
                MonsterLootTables[1830] = MonsterLootTables[17004];
                MonsterLootTables[222] = MonsterLootTables[17004];

                // 12. Banana Monster (TID 1833, 216)
                MonsterLootTables[1833] = new List<MonsterDropEntry>
                {
                    new MonsterDropEntry(28008, "Latania", 1, 2, 75.0),
                    new MonsterDropEntry(28014, "Fresh Fruit", 1, 1, 45.0),
                    new MonsterDropEntry(30201, "Small HP Potion", 1, 1, 30.0)
                };
                MonsterLootTables[216] = MonsterLootTables[1833];

                // 13. Lazy Snail (TID 97)
                MonsterLootTables[97] = new List<MonsterDropEntry>
                {
                    new MonsterDropEntry(30013, "Grume of Snail", 1, 2, 75.0),
                    new MonsterDropEntry(28014, "Soybean", 1, 2, 50.0),
                    new MonsterDropEntry(27010, "White Clay", 1, 1, 35.0),
                    new MonsterDropEntry(27001, "Clay", 1, 1, 35.0)
                };

                // 14. Delicate Monster (TID 217)
                MonsterLootTables[217] = new List<MonsterDropEntry>
                {
                    new MonsterDropEntry(30010, "Musk", 1, 2, 70.0),
                    new MonsterDropEntry(28015, "Pollen", 1, 2, 50.0),
                    new MonsterDropEntry(30201, "Small HP Potion", 1, 1, 30.0)
                };

                // 15. Jellyfish / Slime (TID 17005)
                MonsterLootTables[17005] = new List<MonsterDropEntry>
                {
                    new MonsterDropEntry(28001, "Fresh Water", 1, 2, 70.0),
                    new MonsterDropEntry(28002, "Seaweed", 1, 1, 45.0),
                    new MonsterDropEntry(30203, "Small SP Potion", 1, 1, 25.0)
                };

                // --- Pattern Name Fallback Tables ---
                PatternLootTables["grape"] = MonsterLootTables[17003];
                PatternLootTables["apple"] = MonsterLootTables[17001];
                PatternLootTables["pineapple"] = MonsterLootTables[17002];
                PatternLootTables["kiwi"] = MonsterLootTables[17004];
                PatternLootTables["banana"] = MonsterLootTables[1833];
                PatternLootTables["snail"] = MonsterLootTables[97];
                PatternLootTables["delicate"] = MonsterLootTables[217];
                PatternLootTables["jellyfish"] = MonsterLootTables[17005];
                PatternLootTables["slime"] = MonsterLootTables[17005];
                PatternLootTables["wolf"] = MonsterLootTables[11066];
                PatternLootTables["boar"] = MonsterLootTables[11012];
                PatternLootTables["pig"] = MonsterLootTables[11012];
                PatternLootTables["crab"] = MonsterLootTables[11005];
                PatternLootTables["snake"] = MonsterLootTables[11018];
                PatternLootTables["viper"] = MonsterLootTables[11018];
                PatternLootTables["bat"] = MonsterLootTables[11022];
                PatternLootTables["tree"] = MonsterLootTables[11030];
                PatternLootTables["tiger"] = MonsterLootTables[11050];
                PatternLootTables["spider"] = new List<MonsterDropEntry>
                {
                    new MonsterDropEntry(30013, "Spider Silk", 1, 2, 70.0),
                    new MonsterDropEntry(30024, "Spider Venom", 1, 1, 45.0),
                    new MonsterDropEntry(30201, "Small HP Potion", 1, 1, 25.0)
                };
                PatternLootTables["monkey"] = new List<MonsterDropEntry>
                {
                    new MonsterDropEntry(28008, "Fresh Banana", 1, 2, 75.0),
                    new MonsterDropEntry(28009, "Sweet Peach", 1, 1, 50.0),
                    new MonsterDropEntry(27005, "Tough Vine", 1, 1, 35.0)
                };

                // --- Level Bracket Fallbacks (1-10, 11-30, 31-60, 61+) ---
                LevelBracketLootTables[1] = new List<MonsterDropEntry> // Lv 1-10
                {
                    new MonsterDropEntry(28006, "Red Apple", 1, 2, 60.0),
                    new MonsterDropEntry(28014, "Fresh Fruit", 1, 1, 45.0),
                    new MonsterDropEntry(30201, "Small HP Potion", 1, 1, 30.0),
                    new MonsterDropEntry(27001, "Ordinary Wood", 1, 1, 25.0)
                };

                LevelBracketLootTables[2] = new List<MonsterDropEntry> // Lv 11-30
                {
                    new MonsterDropEntry(30001, "Beast Meat", 1, 2, 65.0),
                    new MonsterDropEntry(30015, "Animal Hide", 1, 1, 50.0),
                    new MonsterDropEntry(30202, "Medium HP Potion", 1, 1, 35.0),
                    new MonsterDropEntry(30203, "Small SP Potion", 1, 1, 30.0)
                };

                LevelBracketLootTables[3] = new List<MonsterDropEntry> // Lv 31-60
                {
                    new MonsterDropEntry(30004, "Prime Meat", 1, 3, 70.0),
                    new MonsterDropEntry(30018, "Tough Leather", 1, 2, 55.0),
                    new MonsterDropEntry(30204, "Large HP Potion", 1, 1, 40.0),
                    new MonsterDropEntry(46005, "Gold Nugget", 1, 1, 25.0)
                };
            }
        }

        public static List<RolledDropItem> RollDrops(uint monsterTid, string monsterName, int monsterLevel)
        {
            var drops = new List<RolledDropItem>();
            List<MonsterDropEntry> pool = null;

            lock (_lock)
            {
                // 1. Specific Monster TID Match
                if (monsterTid > 0 && MonsterLootTables.TryGetValue(monsterTid, out var tidList))
                {
                    pool = tidList;
                }

                // 2. Pattern Match by Monster Name
                if (pool == null && !string.IsNullOrEmpty(monsterName))
                {
                    string lower = monsterName.ToLower();
                    foreach (var kvp in PatternLootTables)
                    {
                        if (lower.Contains(kvp.Key))
                        {
                            pool = kvp.Value;
                            break;
                        }
                    }
                }

                // 3. Fallback to Level Bracket
                if (pool == null)
                {
                    int bracket = 1;
                    if (monsterLevel > 30) bracket = 3;
                    else if (monsterLevel > 10) bracket = 2;

                    LevelBracketLootTables.TryGetValue(bracket, out pool);
                }

                if (pool == null || pool.Count == 0) return drops;

                // Roll each drop entry independently by its drop rate percentage
                foreach (var entry in pool)
                {
                    double roll = _rng.NextDouble() * 100.0;
                    if (roll <= entry.DropRatePercent)
                    {
                        byte count = entry.MinCount;
                        if (entry.MaxCount > entry.MinCount)
                        {
                            count = (byte)_rng.Next(entry.MinCount, entry.MaxCount + 1);
                        }
                        drops.Add(new RolledDropItem(entry.ItemID, entry.ItemName, count));

                        // In authentic WLO, regular monsters usually drop at most 1 item per kill
                        if (drops.Count >= 2) break;
                    }
                }
            }

            return drops;
        }

        public static void VerifyTable()
        {
            try
            {
                bool needsRecreate = false;
                try
                {
                    var testDt = RCLibrary.Core.DataBase.Query("SELECT monster_tid FROM monster_drops LIMIT 1;");
                    if (testDt == null) needsRecreate = true;
                }
                catch
                {
                    needsRecreate = true;
                }

                if (needsRecreate)
                {
                    RCLibrary.Core.DataBase.Execute("DROP TABLE IF EXISTS monster_drops;");
                }

                RCLibrary.Core.DataBase.Execute(@"CREATE TABLE IF NOT EXISTS monster_drops (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    monster_tid INT DEFAULT 0,
                    monster_pattern TEXT,
                    item_id INT NOT NULL,
                    item_name TEXT,
                    min_count INT DEFAULT 1,
                    max_count INT DEFAULT 1,
                    drop_rate REAL DEFAULT 10.0
                );");
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[MonsterDropManager] Error verifying monster_drops table: {ex.Message}");
            }
        }

        public static void LoadFromFile() => LoadFromDatabase();

        public static void LoadFromDatabase()
        {
            try
            {
                VerifyTable();
                var dt = RCLibrary.Core.DataBase.Query("SELECT * FROM monster_drops;");

                if (dt == null || dt.Rows.Count == 0)
                {
                    // Table empty: seed from txt or defaults
                    SeedDatabase();
                    dt = RCLibrary.Core.DataBase.Query("SELECT * FROM monster_drops;");
                }

                lock (_lock)
                {
                    MonsterLootTables.Clear();
                    PatternLootTables.Clear();

                    if (dt != null)
                    {
                        foreach (System.Data.DataRow row in dt.Rows)
                        {
                            uint tid = Convert.ToUInt32(row["monster_tid"]);
                            string pattern = row["monster_pattern"]?.ToString();
                            ushort itemId = Convert.ToUInt16(row["item_id"]);
                            string itemName = row["item_name"]?.ToString() ?? "";
                            byte minCount = Convert.ToByte(row["min_count"]);
                            byte maxCount = Convert.ToByte(row["max_count"]);
                            double dropRate = Convert.ToDouble(row["drop_rate"]);

                            var entry = new MonsterDropEntry(itemId, itemName, minCount, maxCount, dropRate);

                            if (tid > 0)
                            {
                                if (!MonsterLootTables.ContainsKey(tid)) MonsterLootTables[tid] = new List<MonsterDropEntry>();
                                MonsterLootTables[tid].Add(entry);
                            }
                            else if (!string.IsNullOrEmpty(pattern))
                            {
                                if (!PatternLootTables.ContainsKey(pattern)) PatternLootTables[pattern] = new List<MonsterDropEntry>();
                                PatternLootTables[pattern].Add(entry);
                            }
                        }
                    }
                }

                OnLootTablesChanged?.Invoke();
                DebugSystem.Write($"[MonsterDropManager] Loaded {MonsterLootTables.Count} TID tables and {PatternLootTables.Count} Pattern tables from SQLite database.");
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[MonsterDropManager] Error loading drops from database: {ex.Message}");
            }
        }

        private static void SeedDatabase()
        {
            if (File.Exists(ConfigPath))
            {
                try
                {
                    var lines = File.ReadAllLines(ConfigPath, Encoding.UTF8);
                    foreach (var rawLine in lines)
                    {
                        string line = rawLine.Trim();
                        if (string.IsNullOrEmpty(line) || line.StartsWith("#") || line.StartsWith("//")) continue;

                        var parts = line.Split('|');
                        if (parts.Length < 2) continue;

                        string header = parts[0].Trim();
                        uint tid = 0;
                        string pattern = null;

                        if (header.StartsWith("TID:", StringComparison.OrdinalIgnoreCase))
                            uint.TryParse(header.Substring(4).Trim(), out tid);
                        else if (header.StartsWith("NAME:", StringComparison.OrdinalIgnoreCase))
                            pattern = header.Substring(5).Trim();

                        for (int i = 1; i < parts.Length; i++)
                        {
                            var tokens = parts[i].Trim().Split(',');
                            if (tokens.Length >= 2 && ushort.TryParse(tokens[0].Trim(), out ushort itemId))
                            {
                                string itemName = tokens[1].Trim();
                                byte min = tokens.Length > 2 && byte.TryParse(tokens[2].Trim(), out byte mn) ? mn : (byte)1;
                                byte max = tokens.Length > 3 && byte.TryParse(tokens[3].Trim(), out byte mx) ? mx : min;
                                double rate = tokens.Length > 4 && double.TryParse(tokens[4].Trim(), out double r) ? r : 50.0;

                                string sql = $"INSERT INTO monster_drops (monster_tid, monster_pattern, item_id, item_name, min_count, max_count, drop_rate) VALUES ({tid}, '{pattern?.Replace("'", "''")}', {itemId}, '{itemName.Replace("'", "''")}', {min}, {max}, {rate});";
                                RCLibrary.Core.DataBase.Execute(sql);
                            }
                        }
                    }
                    return;
                }
                catch { }
            }

            // Defaults if file not found
            RCLibrary.Core.DataBase.Execute("INSERT INTO monster_drops (monster_tid, monster_pattern, item_id, item_name, min_count, max_count, drop_rate) VALUES (11066, NULL, 30001, 'Wolf Meat', 1, 2, 70.0);");
            RCLibrary.Core.DataBase.Execute("INSERT INTO monster_drops (monster_tid, monster_pattern, item_id, item_name, min_count, max_count, drop_rate) VALUES (11066, NULL, 30015, 'Wolf Pelt', 1, 1, 50.0);");
            RCLibrary.Core.DataBase.Execute("INSERT INTO monster_drops (monster_tid, monster_pattern, item_id, item_name, min_count, max_count, drop_rate) VALUES (0, 'wolf', 30001, 'Wolf Meat', 1, 2, 60.0);");
            RCLibrary.Core.DataBase.Execute("INSERT INTO monster_drops (monster_tid, monster_pattern, item_id, item_name, min_count, max_count, drop_rate) VALUES (0, 'bat', 28014, 'Fresh Fruit', 1, 1, 40.0);");
        }

        public static void SaveToDatabase()
        {
            try
            {
                VerifyTable();
                lock (_lock)
                {
                    RCLibrary.Core.DataBase.Execute("DELETE FROM monster_drops;");

                    foreach (var kvp in MonsterLootTables)
                    {
                        foreach (var e in kvp.Value)
                        {
                            string sql = $"INSERT INTO monster_drops (monster_tid, monster_pattern, item_id, item_name, min_count, max_count, drop_rate) VALUES ({kvp.Key}, NULL, {e.ItemID}, '{e.ItemName.Replace("'", "''")}', {e.MinCount}, {e.MaxCount}, {e.DropRatePercent});";
                            RCLibrary.Core.DataBase.Execute(sql);
                        }
                    }

                    foreach (var kvp in PatternLootTables)
                    {
                        foreach (var e in kvp.Value)
                        {
                            string sql = $"INSERT INTO monster_drops (monster_tid, monster_pattern, item_id, item_name, min_count, max_count, drop_rate) VALUES (0, '{kvp.Key.Replace("'", "''")}', {e.ItemID}, '{e.ItemName.Replace("'", "''")}', {e.MinCount}, {e.MaxCount}, {e.DropRatePercent});";
                            RCLibrary.Core.DataBase.Execute(sql);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[MonsterDropManager] Error saving monster drops to database: {ex.Message}");
            }
        }

        public static void SaveToFile()
        {
            SaveToDatabase();
        }

        /// <summary>
        /// Automatically extracts authentic drop tables directly from client/server Npc.dat.
        /// </summary>
        public static void LoadFromNpcDat(string npcDatPath)
        {
            if (string.IsNullOrEmpty(npcDatPath) || !File.Exists(npcDatPath)) return;

            try
            {
                byte[] data = File.ReadAllBytes(npcDatPath);
                int structSize = 138;
                int count = data.Length / structSize;
                int loaded = 0;

                lock (_lock)
                {
                    for (int i = 1; i < count; i++)
                    {
                        int offset = i * structSize;
                        if (offset + structSize > data.Length) break;

                        // Decoded NPC ID from Bytes 12-13 (val ^ 0x5209) - 1
                        ushort rawId = BitConverter.ToUInt16(data, offset + 12);
                        ushort npcId = (ushort)(((rawId ^ 0x5209) - 1) & 0xFFFF);
                        if (npcId == 0 || MonsterLootTables.ContainsKey(npcId)) continue;

                        // Decoded ItemID1..5 from Bytes 64..73 (val ^ 0x5209) - 1
                        ushort it1 = (ushort)(((BitConverter.ToUInt16(data, offset + 64) ^ 0x5209) - 1) & 0xFFFF);
                        ushort it2 = (ushort)(((BitConverter.ToUInt16(data, offset + 66) ^ 0x5209) - 1) & 0xFFFF);
                        ushort it3 = (ushort)(((BitConverter.ToUInt16(data, offset + 68) ^ 0x5209) - 1) & 0xFFFF);
                        ushort it4 = (ushort)(((BitConverter.ToUInt16(data, offset + 70) ^ 0x5209) - 1) & 0xFFFF);
                        ushort it5 = (ushort)(((BitConverter.ToUInt16(data, offset + 72) ^ 0x5209) - 1) & 0xFFFF);

                        var dropList = new List<MonsterDropEntry>();
                        if (it1 > 0 && it1 < 65000) dropList.Add(new MonsterDropEntry(it1, GetItemName(it1), 1, 1, 70.0));
                        if (it2 > 0 && it2 < 65000 && it2 != it1) dropList.Add(new MonsterDropEntry(it2, GetItemName(it2), 1, 1, 50.0));
                        if (it3 > 0 && it3 < 65000 && it3 != it1 && it3 != it2) dropList.Add(new MonsterDropEntry(it3, GetItemName(it3), 1, 1, 35.0));
                        if (it4 > 0 && it4 < 65000) dropList.Add(new MonsterDropEntry(it4, GetItemName(it4), 1, 1, 20.0));
                        if (it5 > 0 && it5 < 65000) dropList.Add(new MonsterDropEntry(it5, GetItemName(it5), 1, 1, 10.0));

                        if (dropList.Count > 0)
                        {
                            MonsterLootTables[npcId] = dropList;
                            loaded++;
                        }
                    }
                }

                DebugSystem.Write($"[MonsterDropManager] Loaded {loaded} authentic monster drop tables directly from Npc.dat (Total monster loot tables: {MonsterLootTables.Count}).");
                OnLootTablesChanged?.Invoke();
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[MonsterDropManager] Error loading Npc.dat drops: {ex.Message}");
            }
        }

        public static Func<ushort, string> ItemNameResolver { get; set; }

        public static string ResolveItemName(ushort itemId)
        {
            try
            {
                if (ItemNameResolver != null)
                {
                    string res = ItemNameResolver(itemId);
                    if (!string.IsNullOrEmpty(res)) return res;
                }
            }
            catch { }
            return $"Item #{itemId}";
        }

        private static string GetItemName(ushort itemId) => ResolveItemName(itemId);
    }
}
