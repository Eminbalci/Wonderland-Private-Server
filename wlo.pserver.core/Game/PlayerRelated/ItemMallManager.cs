using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Network;

namespace Game.PlayerRelated
{
    public class MallItemEntry
    {
        public ushort ItemID { get; set; }
        public string ItemName { get; set; }
        public string Category { get; set; }
        public int PointCost { get; set; }
        public byte Count { get; set; }

        public MallItemEntry() { }

        public MallItemEntry(ushort id, string name, string category, int cost, byte count = 1)
        {
            ItemID = id;
            ItemName = name;
            Category = category;
            PointCost = cost;
            Count = count;
        }
    }

    public static class ItemMallManager
    {
        private static readonly List<MallItemEntry> _catalog = new List<MallItemEntry>();
        private static readonly object _lock = new object();
        private static readonly string ConfigPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "item_mall.txt");

        public static event Action OnCatalogChanged;
        public static Action<uint, int> OnPointsChanged;

        static ItemMallManager()
        {
            Initialize();
        }

        public static void Initialize()
        {
            LoadFromFile();
        }

        public static List<MallItemEntry> GetCatalog()
        {
            lock (_lock)
            {
                return _catalog.Select(x => new MallItemEntry(x.ItemID, x.ItemName, x.Category, x.PointCost, x.Count)).ToList();
            }
        }

        public static void SetCatalog(List<MallItemEntry> newCatalog)
        {
            lock (_lock)
            {
                _catalog.Clear();
                if (newCatalog != null)
                {
                    _catalog.AddRange(newCatalog);
                }
            }
            SaveToFile();
            OnCatalogChanged?.Invoke();
        }

        public static bool AddOrUpdateItem(ushort itemId, string name, string category, int cost, byte count)
        {
            lock (_lock)
            {
                var existing = _catalog.FirstOrDefault(i => i.ItemID == itemId);
                if (existing != null)
                {
                    existing.ItemName = name;
                    existing.Category = category;
                    existing.PointCost = cost;
                    existing.Count = count;
                }
                else
                {
                    _catalog.Add(new MallItemEntry(itemId, name, category, cost, count));
                }
            }
            SaveToFile();
            OnCatalogChanged?.Invoke();
            return true;
        }

        public static bool MoveItem(int index, bool moveUp)
        {
            bool moved = false;
            lock (_lock)
            {
                if (moveUp && index > 0 && index < _catalog.Count)
                {
                    var item = _catalog[index];
                    _catalog.RemoveAt(index);
                    _catalog.Insert(index - 1, item);
                    moved = true;
                }
                else if (!moveUp && index >= 0 && index < _catalog.Count - 1)
                {
                    var item = _catalog[index];
                    _catalog.RemoveAt(index);
                    _catalog.Insert(index + 1, item);
                    moved = true;
                }
            }
            if (moved)
            {
                SaveToFile();
                OnCatalogChanged?.Invoke();
            }
            return moved;
        }

        public static bool RemoveItem(ushort itemId)
        {
            bool removed = false;
            lock (_lock)
            {
                int count = _catalog.RemoveAll(i => i.ItemID == itemId);
                removed = count > 0;
            }
            if (removed)
            {
                SaveToFile();
                OnCatalogChanged?.Invoke();
            }
            return removed;
        }

        public static int GetUserPoints(Player player)
        {
            if (player?.UserAccount == null) return 0;
            return player.UserAccount.IM;
        }

        public static void SetUserPoints(Player player, int points)
        {
            if (player?.UserAccount == null) return;
            player.UserAccount.IM = Math.Max(0, points);
            try
            {
                OnPointsChanged?.Invoke(player.UserAccount.DataBaseID, player.UserAccount.IM);
            }
            catch { }
            SendPointBalance(player);
        }

        public static void AddUserPoints(Player player, int points)
        {
            if (player?.UserAccount == null) return;
            player.UserAccount.IM = Math.Max(0, player.UserAccount.IM + points);
            try
            {
                OnPointsChanged?.Invoke(player.UserAccount.DataBaseID, player.UserAccount.IM);
            }
            catch { }
            SendPointBalance(player);
        }

        public static void SendPointBalance(Player player)
        {
            if (player == null || player.UserAccount == null) return;
            int points = GetUserPoints(player);

            try
            {
                // Native Client Mall Points Packet: S->C AC 75 Sub 3 [Points(uint16)]
                SendPacket pBalance = new SendPacket();
                pBalance.Pack8(75);
                pBalance.Pack8(3);
                pBalance.Pack16((ushort)Math.Min(65535, points));
                player.Send(pBalance);
                DebugSystem.Write($"[ItemMallManager] Dispatched AC 75:3 Points ({points} IM) to {player.CharName}");
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[ItemMallManager] Error sending AC 75:3 balance: {ex.Message}");
            }
        }

        /// <summary>
        /// Send item mall catalog to player.
        /// Sends both native client in-game catalog (AC 75 Sub 1) and legacy broadcast (AC 54 Sub 201).
        /// </summary>
        public static void SendCatalog(Player player)
        {
            if (player == null) return;
            try
            {
                List<MallItemEntry> catalog = GetCatalog();

                // 1. Native In-Game Client Item Mall Packet: S->C AC 75 Sub 1
                // Header: [75, 1, count(uint16)]
                // Per-item (10 bytes): [ItemID(2B), Flag(1B), Price(2B), Tag(1B), Cat(1B), SubCat(1B), Stock(2B)]
                SendPacket pMall = new SendPacket();
                pMall.Pack8(75);
                pMall.Pack8(1);
                pMall.Pack16((ushort)catalog.Count);
                foreach (var item in catalog)
                {
                    pMall.Pack16(item.ItemID);
                    pMall.Pack8(0); // Flag
                    pMall.Pack16((ushort)Math.Min(65535, item.PointCost)); // Price
                    pMall.Pack8(1); // Tag (1=Hot, 2=New)
                    string cat = (item.Category ?? "").ToLowerInvariant();
                    byte catId = 1; // 1=Hot, 2=Armory, 3=Weaponry, 4=Grocery, 5=Furniture
                    if (cat.Contains("hot")) catId = 1;
                    else if (cat.Contains("armor") || cat.Contains("armory") || cat.Contains("cloth") || cat.Contains("gear") || cat.Contains("shield") || cat.Contains("head") || cat.Contains("boot")) catId = 2;
                    else if (cat.Contains("weapon") || cat.Contains("sword") || cat.Contains("gun") || cat.Contains("bow") || cat.Contains("wand") || cat.Contains("staff")) catId = 3;
                    else if (cat.Contains("groc") || cat.Contains("item") || cat.Contains("gem") || cat.Contains("spar") || cat.Contains("oil") || cat.Contains("star") || cat.Contains("diamond") || cat.Contains("consum")) catId = 4;
                    else if (cat.Contains("furn") || cat.Contains("house") || cat.Contains("vehic") || cat.Contains("spec") || cat.Contains("mount") || cat.Contains("transport")) catId = 5;
                    pMall.Pack8(catId);
                    pMall.Pack8(1); // SubCategory
                    pMall.Pack16(999); // Stock
                }
                player.Send(pMall);

                DebugSystem.Write($"[ItemMallManager] Catalog sent ({catalog.Count} items via AC75:1) to {player.CharName}");
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[ItemMallManager] SendCatalog error: {ex.Message}");
            }
        }


        public static bool PurchaseItem(Player player, ushort itemId, byte quantity = 1)
        {
            if (player == null || player.UserAccount == null) return false;
            if (quantity <= 0) quantity = 1;

            MallItemEntry entry = null;
            lock (_lock)
            {
                entry = _catalog.FirstOrDefault(i => i.ItemID == itemId);
            }

            if (entry == null)
            {
                SendSystemMsg(player, "The selected item is no longer available in the Item Mall.");
                return false;
            }

            int totalCost = entry.PointCost * quantity;
            int userPoints = player.UserAccount.IM;

            if (userPoints < totalCost)
            {
                SendSystemMsg(player, $"Insufficient IM Points! Required: {totalCost} Points (Current: {userPoints} Points).");
                return false;
            }

            // Deduct Points
            player.UserAccount.IM -= totalCost;

            // Persist points immediately to Database
            try
            {
                OnPointsChanged?.Invoke(player.UserAccount.DataBaseID, player.UserAccount.IM);
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[ItemMallManager] Error saving IM points to DB: {ex.Message}");
            }

            // Deliver Item
            byte totalItemCount = (byte)Math.Min(255, entry.Count * quantity);
            player.Inv.AddItem(entry.ItemID, totalItemCount);

            // Synchronize balance
            SendPointBalance(player);

            // Revert any disguise to authentic character appearance (ModelID = 0)
            SendPacket pRestore = new SendPacket();
            pRestore.Pack8(5);
            pRestore.Pack8(5);
            pRestore.Pack32(player.CharID);
            pRestore.Pack16(0); // 0 = normal character model
            player.CurMap?.Broadcast(pRestore);

            SendSystemMsg(player, $"[Item Mall] Successfully purchased {totalItemCount}x {entry.ItemName} for {totalCost} Points!");
            DebugSystem.Write($"[ItemMall] Player {player.CharName} purchased {quantity}x #{itemId} for {totalCost} IM points.");
            return true;
        }

        public static void LoadFromFile()
        {
            try
            {
                string dir = Path.GetDirectoryName(ConfigPath);
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

                if (!File.Exists(ConfigPath))
                {
                    // Create default WLO Item Mall inventory
                    lock (_lock)
                    {
                        _catalog.Clear();
                        _catalog.Add(new MallItemEntry(47010, "Brilliant Diamond (+42 Stats)", "Gems", 250, 1));
                        _catalog.Add(new MallItemEntry(47001, "+24 ATK Spar", "Gems", 120, 1));
                        _catalog.Add(new MallItemEntry(47002, "+24 DEF Spar", "Gems", 120, 1));
                        _catalog.Add(new MallItemEntry(47003, "+24 MATK Spar", "Gems", 120, 1));
                        _catalog.Add(new MallItemEntry(47004, "+24 MDEF Spar", "Gems", 120, 1));
                        _catalog.Add(new MallItemEntry(47005, "+24 SPD Spar", "Gems", 120, 1));
                        _catalog.Add(new MallItemEntry(48050, "Magic Repair Wrench", "Special", 80, 1));
                        _catalog.Add(new MallItemEntry(36007, "Luxury Airship Ticket", "Vehicles", 500, 1));
                        _catalog.Add(new MallItemEntry(36008, "Space UFO Ticket", "Vehicles", 750, 1));
                        _catalog.Add(new MallItemEntry(30025, "Golden Rice Ball x10", "Pets", 50, 10));
                        _catalog.Add(new MallItemEntry(48033, "Zodiac Master Chest", "Special", 300, 1));
                    }
                    SaveToFile();
                    return;
                }

                var lines = File.ReadAllLines(ConfigPath, Encoding.UTF8);
                lock (_lock)
                {
                    _catalog.Clear();
                    foreach (var rawLine in lines)
                    {
                        string line = rawLine.Trim();
                        if (string.IsNullOrEmpty(line) || line.StartsWith("#")) continue;

                        var parts = line.Split('|');
                        if (parts.Length >= 5)
                        {
                            if (ushort.TryParse(parts[0], out ushort id) &&
                                int.TryParse(parts[3], out int cost) &&
                                byte.TryParse(parts[4], out byte count))
                            {
                                string name = parts[1].Trim();
                                string cat = parts[2].Trim();
                                _catalog.Add(new MallItemEntry(id, name, cat, cost, count));
                            }
                        }
                    }
                }
                DebugSystem.Write($"[ItemMall] Loaded {_catalog.Count} Mall items.");
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[ItemMall] Error loading catalog: {ex.Message}");
            }
        }

        public static void SaveToFile()
        {
            try
            {
                string dir = Path.GetDirectoryName(ConfigPath);
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

                var sb = new StringBuilder();
                sb.AppendLine("# WLO Item Mall Catalog Configuration");
                sb.AppendLine("# Format: ItemID|ItemName|Category|PointCost|Count");
                lock (_lock)
                {
                    foreach (var item in _catalog)
                    {
                        sb.AppendLine($"{item.ItemID}|{item.ItemName}|{item.Category}|{item.PointCost}|{item.Count}");
                    }
                }
                File.WriteAllText(ConfigPath, sb.ToString(), Encoding.UTF8);
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[ItemMall] Error saving catalog: {ex.Message}");
            }
        }

        private static void SendSystemMsg(Player p, string msg)
        {
            if (p == null || string.IsNullOrEmpty(msg)) return;
            SendPacket s = new SendPacket();
            s.Pack8(23);
            s.Pack8(57);
            s.Pack8(0);
            s.PackString(msg);
            p.Send(s);
        }
    }
}
