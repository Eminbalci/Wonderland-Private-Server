using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Web.Script.Serialization;
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

        private static string JsonPath => RCLibrary.Core.PathHelper.GetDataFilePath("item_mall.json");
        private static string TxtPath => RCLibrary.Core.PathHelper.GetDataFilePath("item_mall.txt");

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
        // Database Loading & Persistence
        // -------------------------------------------------------------
        public static void VerifyTable()
        {
            try
            {
                RCLibrary.Core.DataBase.Execute(@"CREATE TABLE IF NOT EXISTS item_mall (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    item_id INT NOT NULL,
                    item_name TEXT,
                    category TEXT,
                    category_id INT DEFAULT 1,
                    point_cost INT DEFAULT 0,
                    original_price INT DEFAULT 0,
                    gold_cost INT DEFAULT 0,
                    count INT DEFAULT 1,
                    is_hot INT DEFAULT 0,
                    is_new INT DEFAULT 0,
                    is_limited INT DEFAULT 0,
                    on_sale INT DEFAULT 0,
                    discount INT DEFAULT 100,
                    badge INT DEFAULT 0,
                    order_idx INT DEFAULT 0,
                    is_bonus INT DEFAULT 0,
                    subcategory_id INT DEFAULT 0,
                    description TEXT
                );");
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[ItemMall] Error verifying item_mall table: {ex.Message}");
            }
        }

        public static void LoadFromFile() => LoadFromDatabase();

        public static void LoadFromDatabase()
        {
            try
            {
                VerifyTable();
                var dt = RCLibrary.Core.DataBase.Query("SELECT * FROM item_mall ORDER BY order_idx, item_id;");

                if (dt == null || dt.Rows.Count == 0)
                {
                    // Table empty: seed from JSON/TXT files
                    SeedDatabaseFromFiles();
                    dt = RCLibrary.Core.DataBase.Query("SELECT * FROM item_mall ORDER BY order_idx, item_id;");
                }

                lock (_lock)
                {
                    _pointsCatalog.Clear();
                    _bonusCatalog.Clear();
                    _pointsMap.Clear();
                    _bonusMap.Clear();

                    if (dt != null)
                    {
                        foreach (System.Data.DataRow row in dt.Rows)
                        {
                            var entry = new MallItemEntry
                            {
                                ItemID = Convert.ToUInt16(row["item_id"]),
                                ItemName = row["item_name"]?.ToString() ?? "",
                                Category = row["category"]?.ToString() ?? "",
                                CategoryID = Convert.ToByte(row["category_id"]),
                                PointCost = Convert.ToInt32(row["point_cost"]),
                                OriginalPrice = Convert.ToInt32(row["original_price"]),
                                GoldCost = Convert.ToInt32(row["gold_cost"]),
                                Count = Convert.ToByte(row["count"]),
                                IsHot = Convert.ToByte(row["is_hot"]),
                                IsNew = Convert.ToByte(row["is_new"]),
                                IsLimited = Convert.ToByte(row["is_limited"]),
                                OnSale = Convert.ToByte(row["on_sale"]),
                                Discount = Convert.ToByte(row["discount"]),
                                Badge = Convert.ToByte(row["badge"]),
                                OrderIndex = Convert.ToUInt16(row["order_idx"]),
                                IsBonus = Convert.ToByte(row["is_bonus"]),
                                SubCategoryID = Convert.ToByte(row["subcategory_id"])
                            };

                            if (entry.IsBonus > 0)
                            {
                                _bonusCatalog.Add(entry);
                                _bonusMap[entry.ItemID] = entry;
                            }
                            else
                            {
                                _pointsCatalog.Add(entry);
                                _pointsMap[entry.ItemID] = entry;
                            }
                        }
                    }

                    _pointsCatalog.Sort((a, b) => a.OrderIndex.CompareTo(b.OrderIndex));
                    _bonusCatalog.Sort((a, b) => a.OrderIndex.CompareTo(b.OrderIndex));
                }

                DebugSystem.Write($"[ItemMall] Successfully loaded {_pointsCatalog.Count} Points Mall items and {_bonusCatalog.Count} Bonus Mall items from SQLite database.");
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[ItemMall] Error loading from database: {ex.Message}");
            }
        }

        private static void SeedDatabaseFromFiles()
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

            List<MallItemEntry> parsedItems = null;
            if (candidateJson != null)
            {
                try
                {
                    string jsonContent = File.ReadAllText(candidateJson, Encoding.UTF8);
                    parsedItems = ParseJsonCatalog(jsonContent);
                }
                catch { }
            }

            if (parsedItems == null || parsedItems.Count == 0)
            {
                // Hardcoded minimum fallback
                parsedItems = new List<MallItemEntry>
                {
                    new MallItemEntry(47010, "Brilliant Diamond (+42 Stats)", "Gems", 250, 1),
                    new MallItemEntry(47001, "+24 ATK Spar", "Gems", 120, 1),
                    new MallItemEntry(47002, "+24 DEF Spar", "Gems", 120, 1),
                    new MallItemEntry(48050, "Magic Repair Wrench", "Special", 80, 1),
                    new MallItemEntry(36007, "Luxury Airship Ticket", "Vehicles", 500, 1),
                    new MallItemEntry(30025, "Golden Rice Ball x10", "Pets", 50, 10)
                };
            }

            foreach (var it in parsedItems)
            {
                SaveItemToDatabase(it);
            }
            DebugSystem.Write($"[ItemMall] Seeded {parsedItems.Count} items into SQLite database.");
        }

        public static void SaveItemToDatabase(MallItemEntry it)
        {
            if (it == null) return;
            try
            {
                VerifyTable();
                string sql = $@"INSERT INTO item_mall 
                    (item_id, item_name, category, category_id, point_cost, original_price, gold_cost, count, is_hot, is_new, is_limited, on_sale, discount, badge, order_idx, is_bonus, subcategory_id)
                    VALUES ({it.ItemID}, '{it.ItemName.Replace("'", "''")}', '{it.Category.Replace("'", "''")}', {it.CategoryID}, {it.PointCost}, {it.OriginalPrice}, {it.GoldCost}, {it.Count}, {it.IsHot}, {it.IsNew}, {it.IsLimited}, {it.OnSale}, {it.Discount}, {it.Badge}, {it.OrderIndex}, {it.IsBonus}, {it.SubCategoryID});";
                RCLibrary.Core.DataBase.Execute(sql);
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[ItemMall] Error saving item to database: {ex.Message}");
            }
        }

        public static List<MallItemEntry> ParseJsonCatalog(string json)
        {
            var result = new List<MallItemEntry>();
            if (string.IsNullOrWhiteSpace(json)) return result;
            try
            {
                var serializer = new JavaScriptSerializer();
                var items = serializer.Deserialize<List<Dictionary<string, object>>>(json);
                if (items != null)
                {
                    foreach (var dict in items)
                    {
                        var entry = new MallItemEntry();
                        if (dict.TryGetValue("item_id", out var idVal) && idVal != null)
                            entry.ItemID = Convert.ToUInt16(idVal);
                        if (dict.TryGetValue("item_name", out var nameVal) && nameVal != null)
                            entry.ItemName = nameVal.ToString();
                        if (dict.TryGetValue("category", out var catVal) && catVal != null)
                            entry.Category = catVal.ToString();
                        if (dict.TryGetValue("category_id", out var catIdVal) && catIdVal != null)
                            entry.CategoryID = Convert.ToByte(catIdVal);
                        if (dict.TryGetValue("point_cost", out var costVal) && costVal != null)
                            entry.PointCost = Convert.ToInt32(costVal);
                        if (dict.TryGetValue("original_price", out var origVal) && origVal != null)
                            entry.OriginalPrice = Convert.ToInt32(origVal);
                        if (dict.TryGetValue("gold_cost", out var goldVal) && goldVal != null)
                            entry.GoldCost = Convert.ToInt32(goldVal);
                        if (dict.TryGetValue("count", out var countVal) && countVal != null)
                            entry.Count = Convert.ToByte(countVal);
                        if (dict.TryGetValue("is_hot", out var hotVal) && hotVal != null)
                            entry.IsHot = Convert.ToByte(hotVal);
                        if (dict.TryGetValue("is_new", out var newVal) && newVal != null)
                            entry.IsNew = Convert.ToByte(newVal);
                        if (dict.TryGetValue("is_limited", out var limVal) && limVal != null)
                            entry.IsLimited = Convert.ToByte(limVal);
                        if (dict.TryGetValue("on_sale", out var saleVal) && saleVal != null)
                            entry.OnSale = Convert.ToByte(saleVal);
                        if (dict.TryGetValue("discount", out var discVal) && discVal != null)
                            entry.Discount = Convert.ToByte(discVal);
                        if (dict.TryGetValue("badge", out var badgeVal) && badgeVal != null)
                            entry.Badge = Convert.ToByte(badgeVal);
                        if (dict.TryGetValue("order_idx", out var ordVal) && ordVal != null)
                            entry.OrderIndex = Convert.ToUInt16(ordVal);
                        if (dict.TryGetValue("is_bonus", out var bonVal) && bonVal != null)
                            entry.IsBonus = Convert.ToByte(bonVal);
                        if (dict.TryGetValue("subcategory_id", out var subVal) && subVal != null)
                            entry.SubCategoryID = Convert.ToByte(subVal);
                        result.Add(entry);
                    }
                }
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[ItemMall] Error parsing JSON catalog: {ex.Message}");
            }
            return result;
        }

        public static void SaveToFile()
        {
            // Backward-compatible alias for saving to database
            LoadFromDatabase();
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
