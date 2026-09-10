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
        private static readonly string ConfigPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "starter_items.json");

        static StarterPackManager()
        {
            LoadFromFile();
        }

        public static void Initialize()
        {
            LoadFromFile();
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
                _items.Add(new StarterItemEntry(nextOrder, itemId, name, count, desc));
                SaveToFile();
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
                    SaveToFile();
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
                    SaveToFile();
                    return true;
                }
            }
            return false;
        }

        public static void LoadFromFile()
        {
            lock (_lock)
            {
                try
                {
                    string dataDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data");
                    if (!Directory.Exists(dataDir)) Directory.CreateDirectory(dataDir);

                    if (File.Exists(ConfigPath))
                    {
                        string json = File.ReadAllText(ConfigPath, Encoding.UTF8);
                        var serializer = new JavaScriptSerializer();
                        var list = serializer.Deserialize<List<StarterItemEntry>>(json);
                        if (list != null && list.Count > 0)
                        {
                            _items.Clear();
                            _items.AddRange(list);
                            return;
                        }
                    }
                }
                catch (Exception ex)
                {
                    DebugSystem.Write($"[StarterPackManager] Error loading JSON: {ex.Message}");
                }

                if (_items.Count == 0)
                {
                    _items.Clear();
                    _items.Add(new StarterItemEntry(1, 34038, "Starter Gift 1", 1, "Beginner gift package"));
                    _items.Add(new StarterItemEntry(2, 34058, "Remote Control", 1, "Auto-combat and assistant remote control"));
                    _items.Add(new StarterItemEntry(3, 34332, "Mini Dragonfly", 5, "Starter flying mount vehicle"));
                    _items.Add(new StarterItemEntry(4, 32176, "Spicy Hot Pot", 50, "Full recovery food"));
                    _items.Add(new StarterItemEntry(5, 34026, "Protective Exp Pill", 10, "Prevents EXP loss upon death"));
                    _items.Add(new StarterItemEntry(6, 34542, "Substitute Doll", 1, "Prevents companion amity drop upon death"));
                    _items.Add(new StarterItemEntry(7, 21742, "Goddess Robe", 1, "Starter protective equipment"));
                    _items.Add(new StarterItemEntry(8, 34330, "Mini HP Potion", 1, "Starter HP healing potions"));
                    _items.Add(new StarterItemEntry(9, 34190, "10x Holy EXP Potion", 5, "Boosts experience gain"));
                    _items.Add(new StarterItemEntry(10, 34258, "Training Ticket", 5, "Instant training island pass"));
                    SaveToFile();
                }
            }
        }

        public static void SaveToFile()
        {
            try
            {
                lock (_lock)
                {
                    var serializer = new JavaScriptSerializer();
                    string json = serializer.Serialize(_items);
                    File.WriteAllText(ConfigPath, json, Encoding.UTF8);
                }
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[StarterPackManager] Error saving JSON: {ex.Message}");
            }
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

        public static void DeliverToPlayer(Player p)
        {
            if (p == null) return;
            lock (_lock)
            {
                foreach (var entry in _items)
                {
                    p.Inv.AddItem((ushort)entry.ItemID, (byte)entry.Count);
                }
            }
        }
    }
}
