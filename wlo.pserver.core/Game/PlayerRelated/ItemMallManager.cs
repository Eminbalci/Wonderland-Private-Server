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
        public int OriginalPrice { get; set; }
        public int GoldCost { get; set; }
        public byte Count { get; set; }
        public byte IsHot { get; set; }
        public byte IsNew { get; set; }
        public byte IsLimited { get; set; }
        public byte OnSale { get; set; }
        public byte Discount { get; set; }
        public byte Badge { get; set; }
        public byte CategoryID { get; set; }
        public ushort OrderIndex { get; set; }
        public byte IsBonus { get; set; }
        public byte SubCategoryID { get; set; }

        public MallItemEntry()
        {
            Count = 1;
            Discount = 100;
            SubCategoryID = 1;
        }

        public MallItemEntry(ushort id, string name, string category, int cost, byte count = 1)
        {
            ItemID = id;
            ItemName = name;
            Category = category;
            PointCost = cost;
            OriginalPrice = cost;
            Count = count;
            Discount = 100;
            SubCategoryID = 1;
            CategoryID = ItemMallManager.ResolveCategoryId(category);
        }
    }

    public static class ItemMallManager
    {
        private static readonly List<MallItemEntry> _pointsCatalog = new List<MallItemEntry>();
        private static readonly List<MallItemEntry> _bonusCatalog = new List<MallItemEntry>();
        private static readonly Dictionary<int, MallItemEntry> _pointsMap = new Dictionary<int, MallItemEntry>();
        private static readonly Dictionary<int, MallItemEntry> _bonusMap = new Dictionary<int, MallItemEntry>();
        private static readonly object _lock = new object();

        private static readonly string JsonPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "item_mall.json");
        private static readonly string TxtPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "item_mall.txt");

        public static event Action OnCatalogChanged;
        public static Action<uint, int> OnPointsChanged;
        public static Action<uint, int> OnBonusPointsChanged;

        static ItemMallManager()
        {
            Initialize();
        }

        public static void Initialize()
        {
            LoadFromFile();
        }

        public static byte ResolveCategoryId(string category)
        {
            if (string.IsNullOrEmpty(category)) return 1;
            string cat = category.Trim().ToLowerInvariant();

            if (cat == "1" || cat == "hot") return 1;
            if (cat == "2" || cat == "armory" || cat == "armor" || cat == "armors" || cat.Contains("cloth") || cat.Contains("shield") || cat.Contains("helm") || cat.Contains("boot")) return 2;
            if (cat == "3" || cat == "weaponry" || cat == "weapon" || cat == "weapons" || cat.Contains("sword") || cat.Contains("gun") || cat.Contains("bow") || cat.Contains("wand") || cat.Contains("staff")) return 3;
            if (cat == "4" || cat == "grocery" || cat == "groceries" || cat == "consumable" || cat == "consumables" || cat.Contains("pot") || cat.Contains("pill") || cat.Contains("scroll") || cat.Contains("gem") || cat.Contains("spar") || cat.Contains("oil") || cat.Contains("diamond") || cat.Contains("food") || cat.Contains("rice")) return 4;
            if (cat == "5" || cat == "furniture" || cat == "furn" || cat == "tent" || cat == "house" || cat == "vehic" || cat == "mount") return 5;
            if (cat == "6" || cat.Contains("slot") || cat.Contains("machine") || cat.Contains("minigame")) return 6;
            if (cat == "7" || cat.Contains("forg") || cat.Contains("refin")) return 7;

            return 1;
        }

        public static List<MallItemEntry> GetCatalog(bool isBonus = false)
        {
            lock (_lock)
            {
                var src = isBonus ? _bonusCatalog : _pointsCatalog;
                return src.Select(x => new MallItemEntry
                {
                    ItemID = x.ItemID,
                    ItemName = x.ItemName,
                    Category = x.Category,
                    PointCost = x.PointCost,
                    OriginalPrice = x.OriginalPrice,
                    GoldCost = x.GoldCost,
                    Count = x.Count,
                    IsHot = x.IsHot,
                    IsNew = x.IsNew,
                    IsLimited = x.IsLimited,
                    OnSale = x.OnSale,
                    Discount = x.Discount,
                    Badge = x.Badge,
                    CategoryID = x.CategoryID,
                    OrderIndex = x.OrderIndex,
                    IsBonus = x.IsBonus,
                    SubCategoryID = x.SubCategoryID
                }).ToList();
            }
        }

        public static List<MallItemEntry> GetCatalog()
        {
            return GetCatalog(false);
        }

        public static MallItemEntry GetItem(ushort itemId, bool isBonus = false)
        {
            lock (_lock)
            {
                var map = isBonus ? _bonusMap : _pointsMap;
                if (map.TryGetValue(itemId, out var entry))
                {
                    return entry;
                }
                var list = isBonus ? _bonusCatalog : _pointsCatalog;
                return list.FirstOrDefault(i => i.ItemID == itemId);
            }
        }

        public static void SetCatalog(List<MallItemEntry> newCatalog, bool isBonus = false)
        {
            lock (_lock)
            {
                var targetList = isBonus ? _bonusCatalog : _pointsCatalog;
                var targetMap = isBonus ? _bonusMap : _pointsMap;

                targetList.Clear();
                targetMap.Clear();

                if (newCatalog != null)
                {
                    targetList.AddRange(newCatalog);
                    foreach (var it in targetList)
                    {
                        targetMap[it.ItemID] = it;
                    }
                }
            }
            SaveToFile();
            OnCatalogChanged?.Invoke();
        }

        public static bool AddOrUpdateItem(ushort itemId, string name, string category, int cost, byte count, bool isBonus = false)
        {
            lock (_lock)
            {
                var targetList = isBonus ? _bonusCatalog : _pointsCatalog;
                var targetMap = isBonus ? _bonusMap : _pointsMap;

                var existing = targetList.FirstOrDefault(i => i.ItemID == itemId);
                if (existing != null)
                {
                    existing.ItemName = name;
                    existing.Category = category;
                    existing.PointCost = cost;
                    existing.OriginalPrice = cost;
                    existing.Count = count;
                    existing.CategoryID = ResolveCategoryId(category);
                }
                else
                {
                    var entry = new MallItemEntry(itemId, name, category, cost, count)
                    {
                        IsBonus = (byte)(isBonus ? 1 : 0)
                    };
                    targetList.Add(entry);
                    targetMap[itemId] = entry;
                }
            }
            SaveToFile();
            OnCatalogChanged?.Invoke();
            return true;
        }

        public static bool AddOrUpdateItem(ushort itemId, string name, string category, int cost, byte count)
        {
            return AddOrUpdateItem(itemId, name, category, cost, count, false);
        }

        public static bool MoveItem(int index, bool moveUp, bool isBonus = false)
        {
            bool moved = false;
            lock (_lock)
            {
                var targetList = isBonus ? _bonusCatalog : _pointsCatalog;
                if (moveUp && index > 0 && index < targetList.Count)
                {
                    var item = targetList[index];
                    targetList.RemoveAt(index);
                    targetList.Insert(index - 1, item);
                    moved = true;
                }
                else if (!moveUp && index >= 0 && index < targetList.Count - 1)
                {
                    var item = targetList[index];
                    targetList.RemoveAt(index);
                    targetList.Insert(index + 1, item);
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

        public static bool RemoveItem(ushort itemId, bool isBonus = false)
        {
            bool removed = false;
            lock (_lock)
            {
                var targetList = isBonus ? _bonusCatalog : _pointsCatalog;
                var targetMap = isBonus ? _bonusMap : _pointsMap;

                int count = targetList.RemoveAll(i => i.ItemID == itemId);
                targetMap.Remove(itemId);
                removed = count > 0;
            }
            if (removed)
            {
                SaveToFile();
                OnCatalogChanged?.Invoke();
            }
            return removed;
        }

        public static bool RemoveItem(ushort itemId)
        {
            return RemoveItem(itemId, false);
        }

        // -------------------------------------------------------------
        // User Points & Bonus Points Management
        // -------------------------------------------------------------
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

        public static int GetUserBonusPoints(Player player)
        {
            if (player?.UserAccount == null) return 0;
            return player.UserAccount.IMBonus;
        }

        public static void SetUserBonusPoints(Player player, int bonusPoints)
        {
            if (player?.UserAccount == null) return;
            player.UserAccount.IMBonus = Math.Max(0, bonusPoints);
            try
            {
                OnBonusPointsChanged?.Invoke(player.UserAccount.DataBaseID, player.UserAccount.IMBonus);
            }
            catch { }
            SendPointBalance(player);
        }

        public static void AddUserBonusPoints(Player player, int bonusPoints)
        {
            if (player?.UserAccount == null) return;
            player.UserAccount.IMBonus = Math.Max(0, player.UserAccount.IMBonus + bonusPoints);
            try
            {
                OnBonusPointsChanged?.Invoke(player.UserAccount.DataBaseID, player.UserAccount.IMBonus);
            }
            catch { }
            SendPointBalance(player);
        }

        // -------------------------------------------------------------
        // Protocol Serialization (Port 6414)
        // -------------------------------------------------------------
        /// <summary>
        /// Native Client Mall Points Packet: S->C AC 75 Sub 3
        /// Payload: [75, 3, im_points(uint32), bonus_points(uint32), 0(uint16), 0(uint8)]
        /// Total length: 13 bytes. Authentic pcap layout (itemmalldatalari.pcapng).
        /// </summary>
        public static void SendPointBalance(Player player)
        {
            if (player == null || player.UserAccount == null) return;
            int points = GetUserPoints(player);
            int bonusPoints = GetUserBonusPoints(player);

            try
            {
                SendPacket pBalance = new SendPacket();
                pBalance.Pack8(75);
                pBalance.Pack8(3);
                pBalance.Pack32((uint)points);
                pBalance.Pack32((uint)bonusPoints);
                pBalance.Pack16(0);
                pBalance.Pack8(0);
                player.Send(pBalance);
                DebugSystem.Write($"[ItemMallManager] Dispatched AC 75:3 Points ({points} IM, {bonusPoints} Bonus) to {player.CharName}");
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[ItemMallManager] Error sending AC 75:3 balance: {ex.Message}");
            }
        }

        /// <summary>
        /// Dispatches authentic Item Mall catalog:
        /// - Points Mall: S->C AC 75 Sub 1 (152 authentic items)
        /// - Bonus Mall:  S->C AC 75 Sub 10 (71 authentic items)
        /// Each item is exactly 10 bytes:
        ///   [0-1] item_id (uint16_LE)
        ///   [2]   count (uint8: 1 for single, 5/20/50 for bundle)
        ///   [3-4] base_price / original_price (uint16_LE)
        ///   [5]   discount percentage (uint8: 100=no sale, 80=20% off)
        ///   [6]   badge tag (uint8: 0=normal, 1=NEW, 2=HOT, 3=LIMITED)
        ///   [7]   category_id (uint8: 1=Weaponry, 2=Armory, 3=Grocery single, 4=Grocery pack, 5=Furniture)
        ///   [8-9] order_idx (uint16_LE: display ordering index)
        /// </summary>
        public static void SendCatalog(Player player, bool isBonus = false)
        {
            if (player == null) return;
            try
            {
                List<MallItemEntry> catalog = GetCatalog(isBonus);
                byte subCode = (byte)(isBonus ? 10 : 1);

                SendPacket pMall = new SendPacket();
                pMall.Pack8(75);
                pMall.Pack8(subCode);
                pMall.Pack16((ushort)catalog.Count);

                foreach (var item in catalog)
                {
                    pMall.Pack16(item.ItemID);
                    pMall.Pack8(Math.Max((byte)1, item.Count));

                    ushort basePrice = (ushort)Math.Min(65535, item.OriginalPrice > 0 ? item.OriginalPrice : item.PointCost);
                    pMall.Pack16(basePrice);

                    byte disc = item.Discount > 0 ? item.Discount : (byte)100;
                    if (disc >= 100 && item.OnSale > 0 && item.OriginalPrice > item.PointCost && item.OriginalPrice > 0)
                    {
                        disc = (byte)Math.Max(1, Math.Min(99, (item.PointCost * 100) / item.OriginalPrice));
                    }
                    pMall.Pack8(disc);

                    byte badge = item.Badge;
                    if (badge == 0)
                    {
                        if (item.IsNew > 0) badge = 1;
                        else if (item.IsHot > 0) badge = 2;
                        else if (item.IsLimited > 0) badge = 3;
                    }
                    pMall.Pack8(badge);

                    byte catByte = item.CategoryID > 0 ? item.CategoryID : ResolveCategoryId(item.Category);
                    pMall.Pack8(catByte);

                    ushort orderVal = item.OrderIndex > 0 ? item.OrderIndex : basePrice;
                    pMall.Pack16(orderVal);
                }

                player.Send(pMall);
                DebugSystem.Write($"[ItemMallManager] Dispatched AC 75:{subCode} ({(isBonus ? "Bonus" : "Points")} Mall, {catalog.Count} items) to {player.CharName}");
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[ItemMallManager] SendCatalog error: {ex.Message}");
            }
        }

        public static void SendCatalog(Player player)
        {
            SendCatalog(player, false);
        }

        /// <summary>
        /// Sends authentic map-entry / mall initialization sequence:
        /// 1. AC 75 Sub 1 (Points Mall catalog: 152 items)
        /// 2. AC 75 Sub 10 (Bonus Mall catalog: 71 items)
        /// 3. AC 75 Sub 8 (Mall settings: [75, 8, 0, 0])
        /// 4. AC 75 Sub 7 (Mall status: [75, 7, 1])
        /// 5. AC 75 Sub 3 (Points & Bonus Points balance: 13 bytes)
        /// </summary>
        public static void SendInitialMallSync(Player player)
        {
            if (player == null) return;
            try
            {
                SendCatalog(player, isBonus: false);
                SendCatalog(player, isBonus: true);

                SendPacket s8 = new SendPacket();
                s8.Pack8(75);
                s8.Pack8(8);
                s8.Pack8(0);
                s8.Pack8(0);
                player.Send(s8);

                SendPacket s7 = new SendPacket();
                s7.Pack8(75);
                s7.Pack8(7);
                s7.Pack8(1);
                player.Send(s7);

                SendPointBalance(player);
                DebugSystem.Write($"[ItemMallManager] Initial mall synchronization sequence completed for {player.CharName}");
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[ItemMallManager] SendInitialMallSync error: {ex.Message}");
            }
        }

        // -------------------------------------------------------------
        // Item Mall Purchasing Logic
        // -------------------------------------------------------------
        public static bool PurchaseItem(Player player, ushort itemId, byte quantity = 1, bool isBonus = false)
        {
            if (player == null || player.UserAccount == null) return false;
            if (quantity <= 0) quantity = 1;

            MallItemEntry entry = GetItem(itemId, isBonus);
            if (entry == null)
            {
                SendSystemMsg(player, "The selected item is no longer available in the Item Mall.");
                return false;
            }

            int totalCost = entry.PointCost * quantity;
            int currentBalance = isBonus ? GetUserBonusPoints(player) : GetUserPoints(player);
            string pointLabel = isBonus ? "Bonus Points" : "IM Points";

            if (currentBalance < totalCost)
            {
                SendSystemMsg(player, $"Insufficient {pointLabel}! Required: {totalCost} (Current: {currentBalance}).");
                return false;
            }

            // Deduct Points
            if (isBonus)
            {
                SetUserBonusPoints(player, currentBalance - totalCost);
            }
            else
            {
                SetUserPoints(player, currentBalance - totalCost);
            }

            // Deliver Item to Inventory
            byte totalItemCount = (byte)Math.Min(255, entry.Count * quantity);
            player.Inv.AddItem(entry.ItemID, totalItemCount);

            // Revert disguise to normal model if disguised
            SendPacket pRestore = new SendPacket();
            pRestore.Pack8(5);
            pRestore.Pack8(5);
            pRestore.Pack32(player.CharID);
            pRestore.Pack16(0);
            player.CurMap?.Broadcast(pRestore);

            // Sync updated balance
            SendPointBalance(player);

            int remaining = isBonus ? GetUserBonusPoints(player) : GetUserPoints(player);
            SendSystemMsg(player, $"🎉 Successfully purchased {totalItemCount}x {entry.ItemName} for {totalCost} {pointLabel}! (Remaining: {remaining})");
            DebugSystem.Write($"[ItemMall] Player {player.CharName} purchased {quantity}x #{itemId} ({entry.ItemName}) for {totalCost} {pointLabel}.");
            return true;
        }

        public static bool PurchaseItem(Player player, ushort itemId, byte quantity = 1)
        {
            return PurchaseItem(player, itemId, quantity, false);
        }

        // -------------------------------------------------------------
        // File Loading & Parsing (JSON & TXT)
        // -------------------------------------------------------------
        public static void LoadFromFile()
        {
            string candidateJson = null;
            string[] jsonPaths = new string[]
            {
                JsonPath,
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "Data", "item_mall.json"),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "bin", "Debug", "Data", "item_mall.json"),
                Path.Combine(Directory.GetCurrentDirectory(), "Data", "item_mall.json")
            };

            foreach (var p in jsonPaths)
            {
                if (!string.IsNullOrEmpty(p) && File.Exists(p))
                {
                    candidateJson = Path.GetFullPath(p);
                    break;
                }
            }

            if (candidateJson != null)
            {
                try
                {
                    string jsonContent = File.ReadAllText(candidateJson, Encoding.UTF8);
                    var parsedItems = ParseJsonCatalog(jsonContent);

                    if (parsedItems.Count > 0)
                    {
                        lock (_lock)
                        {
                            _pointsCatalog.Clear();
                            _bonusCatalog.Clear();
                            _pointsMap.Clear();
                            _bonusMap.Clear();

                            foreach (var it in parsedItems)
                            {
                                if (it.IsBonus > 0)
                                {
                                    _bonusCatalog.Add(it);
                                    _bonusMap[it.ItemID] = it;
                                }
                                else
                                {
                                    _pointsCatalog.Add(it);
                                    _pointsMap[it.ItemID] = it;
                                }
                            }

                            _pointsCatalog.Sort((a, b) => a.OrderIndex.CompareTo(b.OrderIndex));
                            _bonusCatalog.Sort((a, b) => a.OrderIndex.CompareTo(b.OrderIndex));
                        }

                        DebugSystem.Write($"[ItemMall] Successfully loaded {_pointsCatalog.Count} Points Mall items and {_bonusCatalog.Count} Bonus Mall items from '{candidateJson}'.");
                        return;
                    }
                }
                catch (Exception ex)
                {
                    DebugSystem.Write($"[ItemMall] Warning loading JSON catalog: {ex.Message}. Falling back to TXT.");
                }
            }

            // Fallback to item_mall.txt
            LoadFromTxtFile();
        }

        private static List<MallItemEntry> ParseJsonCatalog(string json)
        {
            var list = new List<MallItemEntry>();
            if (string.IsNullOrEmpty(json)) return list;

            int idx = 0;
            while ((idx = json.IndexOf('{', idx)) != -1)
            {
                int end = json.IndexOf('}', idx);
                if (end == -1) break;
                string block = json.Substring(idx + 1, end - idx - 1);
                idx = end + 1;

                var entry = new MallItemEntry();
                var lines = block.Split(new char[] { ',', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var line in lines)
                {
                    int colon = line.IndexOf(':');
                    if (colon <= 0) continue;
                    string key = line.Substring(0, colon).Trim().Trim('"');
                    string val = line.Substring(colon + 1).Trim().Trim('"', ' ', '\t');

                    switch (key.ToLowerInvariant())
                    {
                        case "item_id": if (ushort.TryParse(val, out ushort id)) entry.ItemID = id; break;
                        case "item_name": entry.ItemName = val; break;
                        case "category": entry.Category = val; break;
                        case "category_id": if (byte.TryParse(val, out byte cid)) entry.CategoryID = cid; break;
                        case "point_cost": if (int.TryParse(val, out int pc)) entry.PointCost = pc; break;
                        case "original_price": if (int.TryParse(val, out int op)) entry.OriginalPrice = op; break;
                        case "gold_cost": if (int.TryParse(val, out int gc)) entry.GoldCost = gc; break;
                        case "count": if (byte.TryParse(val, out byte cnt)) entry.Count = cnt; break;
                        case "is_hot": if (byte.TryParse(val, out byte ih)) entry.IsHot = ih; break;
                        case "is_new": if (byte.TryParse(val, out byte inw)) entry.IsNew = inw; break;
                        case "is_limited": if (byte.TryParse(val, out byte il)) entry.IsLimited = il; break;
                        case "on_sale": if (byte.TryParse(val, out byte os)) entry.OnSale = os; break;
                        case "discount": if (byte.TryParse(val, out byte dc)) entry.Discount = dc; break;
                        case "badge": if (byte.TryParse(val, out byte bd)) entry.Badge = bd; break;
                        case "order_idx": if (ushort.TryParse(val, out ushort oi)) entry.OrderIndex = oi; break;
                        case "is_bonus": if (byte.TryParse(val, out byte ib)) entry.IsBonus = ib; break;
                        case "subcategory_id": if (byte.TryParse(val, out byte sc)) entry.SubCategoryID = sc; break;
                    }
                }
                if (entry.ItemID > 0)
                {
                    if (entry.OriginalPrice <= 0) entry.OriginalPrice = entry.PointCost;
                    if (entry.Discount <= 0) entry.Discount = 100;
                    if (entry.CategoryID <= 0) entry.CategoryID = ResolveCategoryId(entry.Category);
                    list.Add(entry);
                }
            }
            return list;
        }

        private static void LoadFromTxtFile()
        {
            try
            {
                string dir = Path.GetDirectoryName(TxtPath);
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

                if (!File.Exists(TxtPath))
                {
                    lock (_lock)
                    {
                        _pointsCatalog.Clear();
                        _pointsCatalog.Add(new MallItemEntry(47010, "Brilliant Diamond (+42 Stats)", "Gems", 250, 1));
                        _pointsCatalog.Add(new MallItemEntry(47001, "+24 ATK Spar", "Gems", 120, 1));
                        _pointsCatalog.Add(new MallItemEntry(47002, "+24 DEF Spar", "Gems", 120, 1));
                        _pointsCatalog.Add(new MallItemEntry(47003, "+24 MATK Spar", "Gems", 120, 1));
                        _pointsCatalog.Add(new MallItemEntry(47004, "+24 MDEF Spar", "Gems", 120, 1));
                        _pointsCatalog.Add(new MallItemEntry(47005, "+24 SPD Spar", "Gems", 120, 1));
                        _pointsCatalog.Add(new MallItemEntry(48050, "Magic Repair Wrench", "Special", 80, 1));
                        _pointsCatalog.Add(new MallItemEntry(36007, "Luxury Airship Ticket", "Vehicles", 500, 1));
                        _pointsCatalog.Add(new MallItemEntry(36008, "Space UFO Ticket", "Vehicles", 750, 1));
                        _pointsCatalog.Add(new MallItemEntry(30025, "Golden Rice Ball x10", "Pets", 50, 10));
                        _pointsCatalog.Add(new MallItemEntry(48033, "Zodiac Master Chest", "Special", 300, 1));
                    }
                    SaveToFile();
                    return;
                }

                var lines = File.ReadAllLines(TxtPath, Encoding.UTF8);
                lock (_lock)
                {
                    _pointsCatalog.Clear();
                    _pointsMap.Clear();
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
                                var entry = new MallItemEntry(id, name, cat, cost, count);
                                _pointsCatalog.Add(entry);
                                _pointsMap[id] = entry;
                            }
                        }
                    }
                }
                DebugSystem.Write($"[ItemMall] Loaded {_pointsCatalog.Count} Mall items from TXT.");
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[ItemMall] Error loading catalog from TXT: {ex.Message}");
            }
        }

        public static void SaveToFile()
        {
            try
            {
                string dir = Path.GetDirectoryName(TxtPath);
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

                var sb = new StringBuilder();
                sb.AppendLine("# WLO Item Mall Catalog Configuration");
                sb.AppendLine("# Format: ItemID|ItemName|Category|PointCost|Count");
                lock (_lock)
                {
                    foreach (var item in _pointsCatalog)
                    {
                        sb.AppendLine($"{item.ItemID}|{item.ItemName}|{item.Category}|{item.PointCost}|{item.Count}");
                    }
                }
                File.WriteAllText(TxtPath, sb.ToString(), Encoding.UTF8);
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
