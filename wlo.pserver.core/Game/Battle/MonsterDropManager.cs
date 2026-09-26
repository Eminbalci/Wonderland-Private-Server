using System;
using System.Collections.Generic;
using System.Globalization;
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

        public static double DropRateMultiplier { get; set; } = 1.0;

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
            dropRate = Math.Max(0.0, Math.Min(100.0, dropRate));
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
            Initialize();
        }

        public static void Initialize()
        {
            InitializeLootTables();
            LoadFromFile();
        }

        private static Dictionary<uint, List<MonsterDropEntry>> nativeLootTables = new Dictionary<uint, List<MonsterDropEntry>>();

        public static void InitializeLootTables()
        {
            lock (_lock)
            {
                MonsterLootTables.Clear();
                PatternLootTables.Clear();
                LevelBracketLootTables.Clear();
                // NPC records are authoritative. The old guessed meat/potion IDs
                // were actually quest items, scrolls and vouchers.
                nativeLootTables = ReadNativeLoot(RCLibrary.Core.PathHelper.GetDataFilePath("Npc.dat"));
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

                if (pool == null || pool.Count == 0) return drops;

                // Roll each drop entry independently by calibrated drop rate percentage
                // Server balance policy: scale configured rates by 0.35 with a 5% floor.
                foreach (var entry in pool)
                {
                    List<MonsterDropEntry> native;
                    if (!nativeLootTables.TryGetValue(monsterTid, out native) ||
                        !native.Any(e => e.ItemID == entry.ItemID) || entry.DropRatePercent <= 0 ||
                        entry.MinCount < 1 || entry.MaxCount < entry.MinCount || entry.MaxCount > 50)
                        continue;
                    double roll = _rng.NextDouble() * 100.0;
                    double calibratedRate = Math.Max(5.0, entry.DropRatePercent * 0.35 * Math.Max(0.1, DropRateMultiplier));
                    if (roll <= calibratedRate)
                    {
                        string resolvedItemName = null;
                        try
                        {
                            resolvedItemName = ItemNameResolver?.Invoke(entry.ItemID);
                        }
                        catch { }

                        // Never send a made-up item record to the client. Unknown IDs caused
                        // acquisition popups followed by missing items / false full-bag errors.
                        if (string.IsNullOrWhiteSpace(resolvedItemName))
                        {
                            DebugSystem.Write($"[MonsterDropManager] Skipped unknown drop item #{entry.ItemID} for monster #{monsterTid}.");
                            continue;
                        }

                        byte count = entry.MinCount;
                        if (entry.MaxCount > entry.MinCount)
                        {
                            count = (byte)_rng.Next(entry.MinCount, entry.MaxCount + 1);
                        }
                        drops.Add(new RolledDropItem(entry.ItemID, resolvedItemName, count));

                        // Award at most one loot entry; its configured stack quantity is retained.
                        if (drops.Count >= 1) break;
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
                var native = ReadNativeLoot(RCLibrary.Core.PathHelper.GetDataFilePath("Npc.dat"));
                VerifyTable();

                // Interpret old locale-corrupted rates without rewriting the live database.
                bool legacyRates = false;
                var rateCheck = RCLibrary.Core.DataBase.Query("SELECT MAX(drop_rate) AS max_rate FROM monster_drops;");
                if (rateCheck != null && rateCheck.Rows.Count > 0 && rateCheck.Rows[0]["max_rate"] != DBNull.Value)
                    legacyRates = Convert.ToDouble(rateCheck.Rows[0]["max_rate"], CultureInfo.InvariantCulture) > 100.0;

                var dt = RCLibrary.Core.DataBase.Query("SELECT * FROM monster_drops;");

                lock (_lock)
                {
                    nativeLootTables = native;
                    int rejected = 0;
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
                            double dropRate = Math.Max(0.0, Math.Min(100.0,
                                Convert.ToDouble(row["drop_rate"], CultureInfo.InvariantCulture) / (legacyRates ? 10.0 : 1.0)));

                            List<MonsterDropEntry> allowed;
                            if (!native.TryGetValue(tid, out allowed) || !allowed.Any(e => e.ItemID == itemId) ||
                                minCount < 1 || maxCount < minCount || maxCount > 50)
                            {
                                rejected++;
                                continue;
                            }

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
                    foreach (var pair in native)
                        if (!MonsterLootTables.ContainsKey(pair.Key))
                            MonsterLootTables[pair.Key] = new List<MonsterDropEntry>(pair.Value);
                    DebugSystem.Write($"[MonsterDropManager] Ignored {rejected} unverified loot rows; using native NPC item IDs. Database unchanged.");
                }

                OnLootTablesChanged?.Invoke();
                DebugSystem.Write($"[MonsterDropManager] Loaded {MonsterLootTables.Count} TID tables and {PatternLootTables.Count} Pattern tables from SQLite database.");
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[MonsterDropManager] Error loading drops from database: {ex.Message}");
            }
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
                            string rateSql = Math.Max(0.0, Math.Min(100.0, e.DropRatePercent)).ToString(CultureInfo.InvariantCulture);
                            string sql = $"INSERT INTO monster_drops (monster_tid, monster_pattern, item_id, item_name, min_count, max_count, drop_rate) VALUES ({kvp.Key}, NULL, {e.ItemID}, '{e.ItemName.Replace("'", "''")}', {e.MinCount}, {e.MaxCount}, {rateSql});";
                            RCLibrary.Core.DataBase.Execute(sql);
                        }
                    }

                    foreach (var kvp in PatternLootTables)
                    {
                        foreach (var e in kvp.Value)
                        {
                            string rateSql = Math.Max(0.0, Math.Min(100.0, e.DropRatePercent)).ToString(CultureInfo.InvariantCulture);
                            string sql = $"INSERT INTO monster_drops (monster_tid, monster_pattern, item_id, item_name, min_count, max_count, drop_rate) VALUES (0, '{kvp.Key.Replace("'", "''")}', {e.ItemID}, '{e.ItemName.Replace("'", "''")}', {e.MinCount}, {e.MaxCount}, {rateSql});";
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
        private static Dictionary<uint, List<MonsterDropEntry>> ReadNativeLoot(string path)
        {
            var result = new Dictionary<uint, List<MonsterDropEntry>>();
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return result;
            byte[] data = File.ReadAllBytes(path);
            if (data.Length % 138 != 0)
                throw new InvalidDataException("Invalid Npc.dat record size; refusing guessed loot.");
            double[] rates = { 70, 50, 35, 20, 10 };
            for (int offset = 138; offset + 138 <= data.Length; offset += 138)
            {
                uint id = (ushort)((BitConverter.ToUInt16(data, offset + 12) ^ 0x5209) - 1);
                if (id == 0) continue;
                var entries = new List<MonsterDropEntry>();
                for (int slot = 0; slot < 5; slot++)
                {
                    ushort item = (ushort)((BitConverter.ToUInt16(data, offset + 64 + slot * 2) ^ 0x5209) - 1);
                    if (item > 0 && item < 65000 && !entries.Any(e => e.ItemID == item))
                        entries.Add(new MonsterDropEntry(item, GetItemName(item), 1, 1, rates[slot]));
                }
                result[id] = entries;
            }
            return result;
        }

        public static void LoadFromNpcDat(string npcDatPath)
        {
            try
            {
                var native = ReadNativeLoot(npcDatPath);
                lock (_lock)
                {
                    nativeLootTables = native;
                    foreach (var pair in native)
                        if (!MonsterLootTables.ContainsKey(pair.Key))
                            MonsterLootTables[pair.Key] = new List<MonsterDropEntry>(pair.Value);
                }
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
