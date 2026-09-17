using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Web.Script.Serialization;

namespace Game.PlayerRelated
{
    public class StarterItemEntry
    {
        public int OrderIdx { get; set; }
        public int ItemID { get; set; }
        public string ItemName { get; set; } = string.Empty;
        public int Count { get; set; } = 1;
        public string Description { get; set; } = string.Empty;

        public StarterItemEntry() { }

        public StarterItemEntry(int order, int itemId, string name, int count = 1, string desc = "")
        {
            OrderIdx = order;
            ItemID = itemId;
            ItemName = name;
            Count = Math.Max(1, count);
            Description = desc ?? string.Empty;
        }
    }

    public static class StarterPackManager
    {
        private static readonly List<StarterItemEntry> _items = new List<StarterItemEntry>();
        private static readonly object _lock = new object();

        private static string GetConfigPath()
        {
            return RCLibrary.Core.PathHelper.GetDataFilePath("starter_items.json");
        }

        static StarterPackManager()
        {
            LoadFromDatabase();
        }

        public static void Initialize()
        {
            LoadFromDatabase();
        }

        public static void VerifyTable()
        {
            try
            {
                RCLibrary.Core.DataBase.Execute("CREATE TABLE IF NOT EXISTS starter_items (id INTEGER PRIMARY KEY AUTOINCREMENT, order_idx INT DEFAULT 1, item_id INT NOT NULL, item_name TEXT, count INT DEFAULT 1, description TEXT);");

                // Check for obsolete or non-existent starter item IDs and resync with authentic items
                var dt = RCLibrary.Core.DataBase.Query("SELECT COUNT(*) as cnt FROM starter_items WHERE item_id IN (23050, 23051, 48050, 57001, 34542, 21742, 34330, 34258);");
                if (dt != null && dt.Rows.Count > 0 && Convert.ToInt32(dt.Rows[0]["cnt"]) > 0)
                {
                    DebugSystem.Write("[StarterPackManager] Detected obsolete or invalid starter item IDs in SQLite. Reseeding authentic starter pack...");
                    RCLibrary.Core.DataBase.Execute("DELETE FROM starter_items;");
                    SeedDatabase();
                }
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[StarterPackManager] Error verifying starter_items table: {ex.Message}");
            }
        }

        public static List<StarterItemEntry> GetItems()
        {
            lock (_lock)
            {
                return new List<StarterItemEntry>(_items);
            }
        }

        public static void AddItem(int itemId, string name, int count, string desc)
        {
            lock (_lock)
            {
                int nextOrder = _items.Count > 0 ? _items.Max(i => i.OrderIdx) + 1 : 1;
                var entry = new StarterItemEntry(nextOrder, itemId, name, count, desc);
                _items.Add(entry);
                SaveItemToDatabase(entry);
            }
        }

        public static bool UpdateItem(int order, int itemId, string name, int count, string desc)
        {
            lock (_lock)
            {
                var entry = _items.FirstOrDefault(i => i.OrderIdx == order || i.ItemID == itemId);
                if (entry != null)
                {
                    entry.ItemID = itemId;
                    entry.ItemName = name;
                    entry.Count = Math.Max(1, count);
                    entry.Description = desc ?? string.Empty;
                    SaveToDatabase();
                    return true;
                }
            }
            return false;
        }

        public static bool DeleteItem(int itemId)
        {
            lock (_lock)
            {
                int removed = _items.RemoveAll(i => i.ItemID == itemId);
                if (removed > 0)
                {
                    VerifyTable();
                    RCLibrary.Core.DataBase.Execute($"DELETE FROM starter_items WHERE item_id = {itemId};");
                    return true;
                }
            }
            return false;
        }

        public static void LoadFromFile() => LoadFromDatabase();

        public static void LoadFromDatabase()
        {
            lock (_lock)
            {
                try
                {
                    VerifyTable();
                    var dt = RCLibrary.Core.DataBase.Query("SELECT * FROM starter_items ORDER BY order_idx, id;");
                    if (dt == null || dt.Rows.Count == 0)
                    {
                        // Seed database
                        SeedDatabase();
                        dt = RCLibrary.Core.DataBase.Query("SELECT * FROM starter_items ORDER BY order_idx, id;");
                    }

                    _items.Clear();
                    if (dt != null)
                    {
                        foreach (System.Data.DataRow row in dt.Rows)
                        {
                            int order = Convert.ToInt32(row["order_idx"]);
                            int itemId = Convert.ToInt32(row["item_id"]);
                            string name = row["item_name"]?.ToString() ?? "";
                            int count = Convert.ToInt32(row["count"]);
                            string desc = row["description"]?.ToString() ?? "";

                            _items.Add(new StarterItemEntry(order, itemId, name, count, desc));
                        }
                    }
                    DebugSystem.Write($"[StarterPackManager] Loaded {_items.Count} starter items from SQLite database.");
                }
                catch (Exception ex)
                {
                    DebugSystem.Write($"[StarterPackManager] Error loading starter items: {ex.Message}");
                }
            }
        }

        private static void SeedDatabase()
        {
            List<StarterItemEntry> defaultList = null;
            try
            {
                string targetPath = GetConfigPath();
                if (File.Exists(targetPath))
                {
                    string json = File.ReadAllText(targetPath, Encoding.UTF8);
                    var serializer = new JavaScriptSerializer();
                    defaultList = serializer.Deserialize<List<StarterItemEntry>>(json);
                }
            }
            catch { }

            if (defaultList == null || defaultList.Count == 0 || defaultList.Any(i => i.ItemID == 23050 || i.ItemID == 34542 || i.ItemID == 21742))
            {
                defaultList = new List<StarterItemEntry>
                {
                    new StarterItemEntry(1, 34038, "Notepad", 1, "Beginner guide and notepad"),
                    new StarterItemEntry(2, 34058, "Remote Control", 1, "Auto-combat assistant controller"),
                    new StarterItemEntry(3, 32176, "Fugu Hot Pot", 50, "Full recovery food"),
                    new StarterItemEntry(4, 34014, "Tao Rice Ball", 10, "Pet and character food"),
                    new StarterItemEntry(5, 34026, "Protective EXP Pill", 5, "Prevents EXP loss upon death"),
                    new StarterItemEntry(6, 34169, "Bamboo Dragonfly", 1, "Starter flying mount vehicle"),
                    new StarterItemEntry(7, 34190, "10X Holy EXP Potion", 3, "Boosts experience gain"),
                    new StarterItemEntry(8, 34253, "Training Ticket", 5, "Training island pass")
                };
            }

            foreach (var item in defaultList)
            {
                SaveItemToDatabase(item);
            }
            DebugSystem.Write($"[StarterPackManager] Seeded {defaultList.Count} starter items into SQLite database.");
        }

        private static void SaveItemToDatabase(StarterItemEntry item)
        {
            if (item == null) return;
            try
            {
                VerifyTable();
                string sql = $"INSERT INTO starter_items (order_idx, item_id, item_name, count, description) VALUES ({item.OrderIdx}, {item.ItemID}, '{item.ItemName.Replace("'", "''")}', {item.Count}, '{item.Description.Replace("'", "''")}');";
                RCLibrary.Core.DataBase.Execute(sql);
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[StarterPackManager] Error saving item: {ex.Message}");
            }
        }

        public static void SaveToDatabase()
        {
            try
            {
                VerifyTable();
                lock (_lock)
                {
                    RCLibrary.Core.DataBase.Execute("DELETE FROM starter_items;");
                    foreach (var it in _items)
                    {
                        SaveItemToDatabase(it);
                    }
                }
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[StarterPackManager] Error saving to database: {ex.Message}");
            }
        }

        public static void SaveToFile()
        {
            SaveToDatabase();
        }

        public static bool ImportJson(string json)
        {
            try
            {
                var serializer = new JavaScriptSerializer();
                var list = serializer.Deserialize<List<StarterItemEntry>>(json);
                if (list != null && list.Count > 0)
                {
                    lock (_lock)
                    {
                        _items.Clear();
                        _items.AddRange(list);
                        SaveToFile();
                    }
                    return true;
                }
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[StarterPackManager] Error importing JSON: {ex.Message}");
            }
            return false;
        }

        public static string ExportJson()
        {
            lock (_lock)
            {
                var serializer = new JavaScriptSerializer();
                return serializer.Serialize(_items);
            }
        }

        public static bool HasAnyStarterItem(Player p)
        {
            if (p == null || p.Inv == null) return false;
            lock (_lock)
            {
                foreach (var entry in _items)
                {
                    if (entry.ItemID > 0 && p.Inv.ContainsItem((ushort)entry.ItemID))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        public static void DeliverToPlayer(Player p, bool sendData = true)
        {
            if (p == null || p.Inv == null) return;
            lock (_lock)
            {
                if (_items.Count == 0)
                {
                    LoadFromFile();
                }

                int countAdded = 0;
                foreach (var entry in _items.OrderBy(i => i.OrderIdx))
                {
                    if (entry.ItemID > 0 && entry.Count > 0)
                    {
                        p.Inv.AddItem((ushort)entry.ItemID, (byte)Math.Min(entry.Count, 255), sendData);
                        countAdded++;
                    }
                }
                DebugSystem.Write($"[StarterPackManager] Delivered {countAdded} starter pack items to {p.CharName} (CharID: {p.CharID}, sendData: {sendData})");
            }
        }
    }
}
