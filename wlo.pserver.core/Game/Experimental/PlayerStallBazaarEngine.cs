using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using Game.Code;
using Game.Maps;
using Network;
using RCLibrary.Core;

namespace Game.Experimental
{
    public enum StallType : byte
    {
        Item = 1,
        Pet = 2,
        VehicleUtensil = 3
    }

    public class StallItem
    {
        public byte SlotIndex { get; set; }
        public byte InvSlot { get; set; }
        public ushort ItemId { get; set; }
        public string ItemName { get; set; }
        public int Quantity { get; set; }
        public int MaxQuantity { get; set; }
        public uint PriceGold { get; set; }
        public bool IsInfinite { get; set; }

        public StallItem(byte slot, ushort id, string name, int qty, uint price, byte invSlot = 0, bool isInfinite = false)
        {
            SlotIndex = slot;
            InvSlot = invSlot;
            ItemId = id;
            ItemName = name;
            Quantity = qty;
            MaxQuantity = qty > 0 ? qty : 50;
            PriceGold = price;
            IsInfinite = isInfinite;
        }
    }

    public class PlayerStall
    {
        public uint VendorCharId { get; set; }
        public string VendorName { get; set; }
        public Player VendorPlayer { get; set; }
        public string StallName { get; set; }
        public ushort MapId { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
        public bool IsInfinite { get; set; }
        public StallType StallType { get; set; } = StallType.Item;
        public DateTime OpenedAt { get; set; }
        public List<StallItem> Items { get; } = new List<StallItem>();
        public byte ClickId { get; set; }
        public StallVendorNpc NpcInstance { get; set; }

        public PlayerStall(Player vendor, string name)
        {
            VendorPlayer = vendor;
            VendorCharId = vendor?.CharID ?? 0;
            VendorName = vendor?.CharName ?? "Merchant";
            StallName = name ?? "Player Shop";
            MapId = (ushort)(vendor?.CurMap?.MapID ?? 0);
            X = vendor?.CurX ?? 0;
            Y = vendor?.CurY ?? 0;
            IsInfinite = false;
            StallType = StallType.Item;
            OpenedAt = DateTime.UtcNow;
        }
    }

    public class StallVendorNpc : Game.Maps.QuestNpc
    {
        public PlayerStall Stall { get; }

        public StallVendorNpc(PlayerStall stall, byte clickId)
        {
            Stall = stall ?? throw new ArgumentNullException(nameof(stall));
            CickID = clickId;
            TemplateID = 14134; // Authentic Props Keep merchant model
            Name = !string.IsNullOrWhiteSpace(stall.StallName) ? stall.StallName : "Stall Merchant";
            MapID = stall.MapId;
            X = (ushort)stall.X;
            Y = (ushort)stall.Y;
            SpawnX = (ushort)stall.X;
            SpawnY = (ushort)stall.Y;
            WalkBehavior = 1; // Stationary anchored merchant
            EntityType = 1;
        }

        public override void Interact(Player player)
        {
            if (player == null || Stall == null) return;
            PlayerStallBazaarEngine.OnStallInteracted(player, Stall, this);
        }
    }

    /// <summary>
    /// Player Overworld Stall Vending and Pet Bazaar Engine (AC 53 / AC 94 &amp; Spec 53).
    /// Manages player vending kiosks in market hubs, sales pricing, and item transactions.
    /// Injects dual-entity representation (Map NPC in Map.NPCs + Visual Actor Appearance with Signboard).
    /// Supports synthetic vendor spawning, infinite vs limited stock, and dual-database persistence (SQLite &amp; MySQL).
    /// </summary>
    public class PlayerStallBazaarEngine
    {
        private readonly ConcurrentDictionary<uint, PlayerStall> m_stalls = new ConcurrentDictionary<uint, PlayerStall>();
        private readonly object m_stallLock = new object();
        private bool m_databaseInitialized = false;

        public int ActiveStallCount => m_stalls.Count;

        public IReadOnlyCollection<PlayerStall> GetStalls() => m_stalls.Values.ToList().AsReadOnly();

        public PlayerStall GetStall(uint vendorCharId)
        {
            m_stalls.TryGetValue(vendorCharId, out var stall);
            return stall;
        }

        public uint GenerateSyntheticVendorId()
        {
            lock (m_stallLock)
            {
                uint candidate = 8900001;
                while (m_stalls.ContainsKey(candidate))
                {
                    candidate++;
                }
                return candidate;
            }
        }

        #region Database Persistence (SQLite & MySQL)

        public void EnsureDatabaseTable()
        {
            try
            {
                string sql = "CREATE TABLE IF NOT EXISTS custom_stalls (" +
                             "vendor_id INT NOT NULL PRIMARY KEY, " +
                             "vendor_name TEXT NOT NULL, " +
                             "stall_title TEXT NOT NULL, " +
                             "map_id INT NOT NULL, " +
                             "x INT NOT NULL, " +
                             "y INT NOT NULL, " +
                             "is_infinite INT DEFAULT 0, " +
                             "stall_type INT DEFAULT 1, " +
                             "items_data TEXT NOT NULL, " +
                             "created_at TEXT" +
                             ");";
                RCLibrary.Core.DataBase.Execute(sql);

                // Auto-migration for existing databases that lack stall_type
                try
                {
                    RCLibrary.Core.DataBase.Execute("ALTER TABLE custom_stalls ADD COLUMN stall_type INT DEFAULT 1;");
                }
                catch { }

                DebugSystem.Write("[PlayerStallBazaarEngine] custom_stalls table verified.");
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[PlayerStallBazaarEngine] EnsureDatabaseTable error: {ex.Message}");
            }
        }

        public void SaveStallToDatabase(PlayerStall stall)
        {
            if (stall == null) return;
            try
            {
                EnsureDatabaseTable();
                string sql = "INSERT OR REPLACE INTO custom_stalls (vendor_id, vendor_name, stall_title, map_id, x, y, is_infinite, stall_type, items_data, created_at) " +
                             "VALUES (@vendor_id, @vendor_name, @stall_title, @map_id, @x, @y, @is_infinite, @stall_type, @items_data, @created_at);";

                RCLibrary.Core.DataBase.Execute(
                    sql,
                    new DbParam("@vendor_id", (int)stall.VendorCharId),
                    new DbParam("@vendor_name", stall.VendorName ?? "Merchant"),
                    new DbParam("@stall_title", stall.StallName ?? "Player Shop"),
                    new DbParam("@map_id", (int)stall.MapId),
                    new DbParam("@x", stall.X),
                    new DbParam("@y", stall.Y),
                    new DbParam("@is_infinite", stall.IsInfinite ? 1 : 0),
                    new DbParam("@stall_type", (int)stall.StallType),
                    new DbParam("@items_data", SerializeItems(stall.Items)),
                    new DbParam("@created_at", stall.OpenedAt.ToString("yyyy-MM-dd HH:mm:ss"))
                );
                DebugSystem.Write($"[PlayerStallBazaarEngine] Saved stall '{stall.StallName}' (Vendor ID: {stall.VendorCharId}, Type: {stall.StallType}) to database.");
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[PlayerStallBazaarEngine] SaveStallToDatabase error: {ex.Message}");
            }
        }

        public void DeleteStallFromDatabase(uint vendorCharId)
        {
            try
            {
                EnsureDatabaseTable();
                string sql = "DELETE FROM custom_stalls WHERE vendor_id = @vendor_id;";
                RCLibrary.Core.DataBase.Execute(sql, new DbParam("@vendor_id", (int)vendorCharId));
                DebugSystem.Write($"[PlayerStallBazaarEngine] Deleted stall {vendorCharId} from database.");
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[PlayerStallBazaarEngine] DeleteStallFromDatabase error: {ex.Message}");
            }
        }

        public void ClearAllStallsFromDatabase()
        {
            try
            {
                EnsureDatabaseTable();
                RCLibrary.Core.DataBase.Execute("DELETE FROM custom_stalls;");
                DebugSystem.Write("[PlayerStallBazaarEngine] All stalls deleted from database.");
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[PlayerStallBazaarEngine] ClearAllStallsFromDatabase error: {ex.Message}");
            }
        }

        public void LoadStallsFromDatabase()
        {
            try
            {
                EnsureDatabaseTable();
                var dt = RCLibrary.Core.DataBase.Query("SELECT vendor_id, vendor_name, stall_title, map_id, x, y, is_infinite, stall_type, items_data, created_at FROM custom_stalls;");
                if (dt == null || dt.Rows.Count == 0)
                {
                    DebugSystem.Write("[PlayerStallBazaarEngine] No saved custom stalls in database.");
                    return;
                }

                int loadedCount = 0;
                foreach (DataRow row in dt.Rows)
                {
                    try
                    {
                        uint vendorId = Convert.ToUInt32(row["vendor_id"]);
                        string vendorName = Convert.ToString(row["vendor_name"]);
                        string stallTitle = Convert.ToString(row["stall_title"]);
                        ushort mapId = Convert.ToUInt16(row["map_id"]);
                        int x = Convert.ToInt32(row["x"]);
                        int y = Convert.ToInt32(row["y"]);
                        bool isInfinite = Convert.ToInt32(row["is_infinite"]) == 1;

                        StallType stallType = StallType.Item;
                        if (dt.Columns.Contains("stall_type") && row["stall_type"] != DBNull.Value)
                        {
                            stallType = (StallType)Convert.ToByte(row["stall_type"]);
                        }

                        string itemsData = Convert.ToString(row["items_data"]);
                        var items = DeserializeItems(itemsData, isInfinite);

                        // Spawn without re-persisting to DB
                        var stall = SpawnCustomStall(vendorId, vendorName, stallTitle, mapId, x, y, items, isInfinite, persistToDb: false, stallType: stallType);
                        if (stall != null)
                        {
                            if (row["created_at"] != null && DateTime.TryParse(row["created_at"].ToString(), out var dtOpened))
                            {
                                stall.OpenedAt = dtOpened;
                            }
                            loadedCount++;
                        }
                    }
                    catch (Exception exRow)
                    {
                        DebugSystem.Write($"[PlayerStallBazaarEngine] Error loading stall row: {exRow.Message}");
                    }
                }
                DebugSystem.Write($"[PlayerStallBazaarEngine] Successfully restored {loadedCount} custom stall(s) from database.");
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[PlayerStallBazaarEngine] LoadStallsFromDatabase error: {ex.Message}");
            }
        }

        public void InitializeDatabase()
        {
            lock (m_stallLock)
            {
                if (m_databaseInitialized) return;
                try
                {
                    EnsureDatabaseTable();
                    LoadStallsFromDatabase();
                    m_databaseInitialized = true;
                }
                catch (Exception ex)
                {
                    DebugSystem.Write($"[PlayerStallBazaarEngine] InitializeDatabase error: {ex.Message}");
                }
            }
        }

        #endregion

        public PlayerStall OpenPersonalStall(Player vendor, string stallTitle)
        {
            if (vendor == null) return null;

            var stallItems = new List<StallItem>();
            if (vendor.Inv != null)
            {
                lock (vendor.Inv)
                {
                    for (byte s = 1; s <= 50; s++)
                    {
                        var it = vendor.Inv[s];
                        if (it != null && it.ItemID > 0 && it.Ammt > 0)
                        {
                            string itName = Game.Battle.MonsterDropManager.ResolveItemName((ushort)it.ItemID);
                            if (string.IsNullOrWhiteSpace(itName)) itName = $"Item #{it.ItemID}";
                            uint unitPrice = (uint)Math.Max(50, (int)(it.Level > 0 ? it.Level * 100 : 100));
                            stallItems.Add(new StallItem((byte)stallItems.Count, (ushort)it.ItemID, itName, it.Ammt, unitPrice));
                            if (stallItems.Count >= 10) break;
                        }
                    }
                }
            }

            if (stallItems.Count == 0)
            {
                // Default high-value market goods catalog
                stallItems.Add(new StallItem(0, 10001, "Superior Healing Draught", 50, 120));
                stallItems.Add(new StallItem(1, 10002, "Elixir of Vitality", 25, 300));
                stallItems.Add(new StallItem(2, 26315, "Meteoric Star Iron", 10, 2500));
                stallItems.Add(new StallItem(3, 27001, "Spacetime Warp Stone", 5, 5000));
                stallItems.Add(new StallItem(4, 26399, "Ancient Treasure Casket", 2, 15000));
            }

            return OpenStall(vendor, stallTitle, stallItems);
        }

        public void ClosePersonalStall(Player vendor)
        {
            if (vendor == null) return;
            CloseStall(vendor);
        }

        public PlayerStall OpenStall(Player vendor, string stallTitle, List<StallItem> items, ushort? customMapId = null, int? customX = null, int? customY = null)
        {
            if (vendor == null || items == null || items.Count == 0) return null;

            try
            {
                lock (m_stallLock)
                {
                    var stall = new PlayerStall(vendor, stallTitle);
                    if (customMapId.HasValue) stall.MapId = customMapId.Value;
                    if (customX.HasValue) stall.X = customX.Value;
                    if (customY.HasValue) stall.Y = customY.Value;

                    stall.Items.AddRange(items.Take(10)); // Max 10 slots
                    m_stalls[vendor.CharID] = stall;

                    DebugSystem.Write($"[PlayerStallBazaarEngine] Stall '{stall.StallName}' opened by {vendor.CharName} on Map {stall.MapId} ({stall.X}, {stall.Y}) with {stall.Items.Count} items.");

                    // Put character into sitting posture
                    vendor.Emote = 1;
                    vendor.CurMap?.Broadcast(BuildSittingPosePacket(vendor.CharID, 1));
                    vendor.Send(BuildSittingPosePacket(vendor.CharID, 1));

                    // S->C AC 53:1 Stall opened ACK
                    SendPacket p = new SendPacket();
                    p.Pack8(53);
                    p.Pack8(1);
                    p.PackString(stall.StallName ?? "Player Shop");
                    p.Pack8((byte)stall.Items.Count);
                    vendor.Send(p);

                    // S->C AC 23:30 Native Private Shop Confirmation (triggers FUN_004546e8 in client)
                    var nativeConfirm = BuildStallNativeConfirmPacket(stall);
                    if (nativeConfirm != null) vendor.Send(nativeConfirm);

                    // Broadcast authentic stall table and signboard banner (AC 23:31) to map peers and self
                    var mapStatePkt = BuildStallMapStatePacket(stall);
                    vendor.CurMap?.Broadcast(mapStatePkt);
                    vendor.Send(mapStatePkt);

                    // Broadcast kiosk appearance to map peers and self (AC 5:16)
                    SendPacket bcast = new SendPacket();
                    bcast.Pack8(5);
                    bcast.Pack8(16);
                    bcast.Pack32(vendor.CharID);
                    bcast.Pack8(1);
                    vendor.CurMap?.Broadcast(bcast);
                    vendor.Send(bcast);

                    // Broadcast stall signboard banner (AC 56:30) to map peers and self
                    var signPkt = BuildStallSignPacket(stall);
                    vendor.CurMap?.Broadcast(signPkt);
                    vendor.Send(signPkt);

                    // Synchronize with legacy StallManager for compatibility
                    try
                    {
                        var legacyItems = stall.Items.Select(i => new Game.PlayerRelated.StallItem(i.SlotIndex, i.ItemId, i.PriceGold, (byte)Math.Min(i.Quantity, 255))).ToList();
                        Game.PlayerRelated.StallManager.OpenStall(vendor, stall.StallName, legacyItems);
                    }
                    catch { }

                    return stall;
                }
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[PlayerStallBazaarEngine] OpenStall error: {ex.Message}");
                return null;
            }
        }

        public PlayerStall SpawnCustomStall(uint vendorId, string vendorName, string stallTitle, ushort mapId, int x, int y, List<StallItem> items = null, bool isInfinite = false, bool persistToDb = true, StallType stallType = StallType.Item)
        {
            try
            {
                lock (m_stallLock)
                {
                    if (vendorId == 0) vendorId = GenerateSyntheticVendorId();

                    var stall = new PlayerStall(null, stallTitle)
                    {
                        VendorCharId = vendorId,
                        VendorName = string.IsNullOrWhiteSpace(vendorName) ? $"Merchant #{vendorId}" : vendorName.Trim(),
                        StallName = string.IsNullOrWhiteSpace(stallTitle) ? (stallType == StallType.Pet ? "Companion Pet Shop" : (stallType == StallType.VehicleUtensil ? "Vehicles & Appliances Shop" : "Overworld Bazaar Kiosk")) : stallTitle.Trim(),
                        MapId = mapId,
                        X = x,
                        Y = y,
                        IsInfinite = isInfinite,
                        StallType = stallType,
                        OpenedAt = DateTime.UtcNow
                    };

                    if (items != null && items.Count > 0)
                    {
                        foreach (var it in items.Take(10))
                        {
                            if (isInfinite) it.IsInfinite = true;
                            stall.Items.Add(it);
                        }
                    }
                    else
                    {
                        if (stallType == StallType.Pet)
                        {
                            // Authentic companion pet defaults (NPC template IDs)
                            stall.Items.Add(new StallItem(0, 10001, "Roca (Level 1 Companion)", 1, 10000, 0, isInfinite));
                            stall.Items.Add(new StallItem(1, 10002, "Niss (Level 1 Companion)", 1, 15000, 0, isInfinite));
                            stall.Items.Add(new StallItem(2, 10003, "Fred (Level 1 Companion)", 1, 20000, 0, isInfinite));
                            stall.Items.Add(new StallItem(3, 10004, "Cliff (Level 1 Companion)", 1, 25000, 0, isInfinite));
                            stall.Items.Add(new StallItem(4, 10007, "Xaolan (Level 1 Companion)", 1, 30000, 0, isInfinite));
                        }
                        else if (stallType == StallType.VehicleUtensil)
                        {
                            // Authentic vehicles and household appliances
                            stall.Items.Add(new StallItem(0, 48010, "Raft (Water Transport)", 1, 5000, 0, isInfinite));
                            stall.Items.Add(new StallItem(1, 48009, "Sailboat (Water Transport)", 1, 25000, 0, isInfinite));
                            stall.Items.Add(new StallItem(2, 26002, "Alchemy Pot (Appliance)", 1, 8000, 0, isInfinite));
                        }
                        else
                        {
                            // Default diversified goods catalog (verified in itemDat.wpdat)
                            stall.Items.Add(new StallItem(0, 30167, "Rainbow Drop", 50, 120, 0, isInfinite));
                            stall.Items.Add(new StallItem(1, 30162, "Safety Bell", 25, 300, 0, isInfinite));
                            stall.Items.Add(new StallItem(2, 30253, "Magic Potion", 50, 500, 0, isInfinite));
                            stall.Items.Add(new StallItem(3, 30323, "Magical Cure Potion", 50, 1200, 0, isInfinite));
                            stall.Items.Add(new StallItem(4, 27001, "Orchid Candy", 50, 2500, 0, isInfinite));
                        }
                    }

                    // Target Map Resolution
                    GameMap targetMap = MapManager.Instance?.GetMap(mapId);

                    // Dynamic ClickID allocation on target map (starts at 200 to never collide with native NPCs 1..50)
                    byte clickId = 200;
                    if (targetMap?.NPCs != null && targetMap.NPCs.Count > 0)
                    {
                        int maxId = targetMap.NPCs.Max(n => (int)n.CickID);
                        clickId = (byte)Math.Min(250, Math.Max(200, maxId + 1));
                        while (targetMap.NPCs.Any(n => n.CickID == clickId) && clickId < 254)
                        {
                            clickId++;
                        }
                    }

                    var stallNpc = new StallVendorNpc(stall, clickId);
                    stall.ClickId = clickId;
                    stall.NpcInstance = stallNpc;

                    // Attach Stall NPC to target map so Map.SendMapInfo and AC 22:4 include it
                    if (targetMap != null)
                    {
                        lock (targetMap)
                        {
                            if (targetMap.NPCs == null) targetMap.NPCs = new List<InteractableObjects>();
                            targetMap.NPCs.RemoveAll(n => n.CickID == clickId);
                            targetMap.NPCs.Add(stallNpc);
                        }
                    }

                    m_stalls[vendorId] = stall;
                    string stockMode = isInfinite ? "Infinite" : "Limited";
                    DebugSystem.Write($"[PlayerStallBazaarEngine] Custom Stall '{stall.StallName}' (ClickID: {clickId}, Vendor ID: {vendorId}) spawned at Map {mapId} ({x}, {y}) for {stall.VendorName} with {stall.Items.Count} items ({stockMode} Stock).");

                    // Broadcast live Actor Avatar, Sitting Pose, Kiosk Table, Signboard Banner, and NPC to players on the map
                    if (targetMap != null && targetMap.PlayersList != null && targetMap.PlayersList.Count > 0)
                    {
                        var actorSpawn = BuildActorSpawnPacket(stall);
                        var actorEquip = BuildActorEquipPacket(stall);
                        var actorOnline = Tools.FromFormat("bbdb", 10, 3, stall.VendorCharId, (byte)255);
                        var actorPos = BuildActorPosPacket(stall);
                        var sitPose = BuildSittingPosePacket(stall.VendorCharId, 1);
                        var stallMapState = BuildStallMapStatePacket(stall);
                        var kioskPkt = BuildKioskStatePacket(stall.VendorCharId, 1);
                        var stallSign = BuildStallSignPacket(stall);
                        var npcSpawn = BuildNpcSpawnPacket(stallNpc);

                        targetMap.Broadcast(actorSpawn);
                        targetMap.Broadcast(actorEquip);
                        targetMap.Broadcast(actorOnline);
                        targetMap.Broadcast(actorPos);
                        targetMap.Broadcast(sitPose);
                        targetMap.Broadcast(stallMapState);
                        targetMap.Broadcast(kioskPkt);
                        targetMap.Broadcast(stallSign);
                        if (npcSpawn != null) targetMap.Broadcast(npcSpawn);
                    }

                    if (persistToDb)
                    {
                        SaveStallToDatabase(stall);
                    }

                    return stall;
                }
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[PlayerStallBazaarEngine] SpawnCustomStall error: {ex.Message}");
                return null;
            }
        }

        public bool RemoveCustomStall(uint vendorCharId)
        {
            lock (m_stallLock)
            {
                if (!m_stalls.TryRemove(vendorCharId, out var stall))
                {
                    return false;
                }

                try
                {
                    GameMap targetMap = MapManager.Instance?.GetMap(stall.MapId);
                    if (targetMap != null)
                    {
                        lock (targetMap)
                        {
                            if (targetMap.NPCs != null)
                            {
                                targetMap.NPCs.RemoveAll(n => n.CickID == stall.ClickId);
                            }
                        }

                        if (targetMap.PlayersList != null && targetMap.PlayersList.Count > 0)
                        {
                            if (stall.ClickId > 0)
                            {
                                targetMap.Broadcast(BuildNpcDespawnPacket(stall.ClickId, (ushort)stall.X, (ushort)stall.Y));
                            }
                            targetMap.Broadcast(BuildStallDespawnPacket(stall.VendorCharId));
                            targetMap.Broadcast(BuildStallSignClearPacket(stall.VendorCharId));
                            targetMap.Broadcast(BuildKioskStatePacket(stall.VendorCharId, 0));
                            targetMap.Broadcast(BuildSittingPosePacket(stall.VendorCharId, 0));
                            targetMap.Broadcast(BuildStallNativeClosePacket(stall.VendorCharId, 33));
                            targetMap.Broadcast(BuildActorDespawnPacket(stall.VendorCharId));
                        }
                    }

                    DeleteStallFromDatabase(vendorCharId);
                    DebugSystem.Write($"[PlayerStallBazaarEngine] Stall '{stall.StallName}' (Vendor ID: {vendorCharId}) removed from Map {stall.MapId} and database.");
                    return true;
                }
                catch (Exception ex)
                {
                    DebugSystem.Write($"[PlayerStallBazaarEngine] RemoveCustomStall error: {ex.Message}");
                    return false;
                }
            }
        }

        public void ClearAllStalls()
        {
            lock (m_stallLock)
            {
                foreach (var stall in m_stalls.Values.ToList())
                {
                    RemoveCustomStall(stall.VendorCharId);
                }
                ClearAllStallsFromDatabase();
                m_stalls.Clear();
                DebugSystem.Write("[PlayerStallBazaarEngine] All active stalls cleared and purged from database.");
            }
        }

        public void ReattachStallsToMap(GameMap map)
        {
            if (map == null) return;
            try
            {
                lock (m_stallLock)
                {
                    var matchingStalls = m_stalls.Values.Where(s => s.MapId == (ushort)map.MapID).ToList();
                    foreach (var stall in matchingStalls)
                    {
                        if (stall.NpcInstance != null)
                        {
                            if (map.NPCs == null) map.NPCs = new List<InteractableObjects>();
                            if (!map.NPCs.Any(n => n.CickID == stall.ClickId))
                            {
                                map.NPCs.Add(stall.NpcInstance);
                                DebugSystem.Write($"[PlayerStallBazaarEngine] Re-attached stall NPC '{stall.StallName}' (ClickID: {stall.ClickId}) to Map {map.MapID}");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[PlayerStallBazaarEngine] ReattachStallsToMap error: {ex.Message}");
            }
        }

        public void SyncMapStallsToPlayer(Player player, ushort mapId)
        {
            if (player == null) return;
            try
            {
                lock (m_stallLock)
                {
                    var matchingStalls = m_stalls.Values.Where(s => s.MapId == mapId).ToList();
                    foreach (var stall in matchingStalls)
                    {
                        // If this stall is a synthetic custom stall (vendor is not an online connected player whose body was sent in Map.Warp_In)
                        bool isConnectedPlayer = (player.CurMap as GameMap)?.PlayersList?.Any(p => p.CharID == stall.VendorCharId) ?? false;
                        if (!isConnectedPlayer)
                        {
                            player.Send(BuildActorSpawnPacket(stall));
                            player.Send(BuildActorEquipPacket(stall));
                            player.Send(Tools.FromFormat("bbdb", 10, 3, stall.VendorCharId, (byte)255));
                            player.Send(BuildActorPosPacket(stall));
                        }

                        player.Send(BuildSittingPosePacket(stall.VendorCharId, 1));
                        player.Send(BuildStallMapStatePacket(stall));
                        player.Send(BuildKioskStatePacket(stall.VendorCharId, 1));
                        player.Send(BuildStallSignPacket(stall));
                    }
                }
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[PlayerStallBazaarEngine] SyncMapStallsToPlayer error: {ex.Message}");
            }
        }

        public static void OnStallInteracted(Player player, PlayerStall stall, StallVendorNpc npc)
        {
            if (player == null || stall == null) return;

            DebugSystem.Write($"[StallVendorNpc] Player {player.CharName} interacted with stall '{stall.StallName}' (Vendor: {stall.VendorName}, Type: {stall.StallType})");

            // 1. Send type-specific authentic browse viewport packet
            if (stall.StallType == StallType.Pet)
            {
                // Authentic Pet Stall Browse Viewport (AC 23:113 -> Form_BuyPet1..3Stall_1)
                var petBrowsePkt = BuildPetStallBrowse113Packet(stall);
                if (petBrowsePkt != null) player.Send(petBrowsePkt);
            }
            else if (stall.StallType == StallType.VehicleUtensil)
            {
                // Authentic Vehicle & Utensil Stall Browse Viewport (AC 23:133 -> Form_Buy1..3Utensil_1)
                var utensilBrowsePkt = BuildUtensilStallBrowse133Packet(stall);
                if (utensilBrowsePkt != null) player.Send(utensilBrowsePkt);
            }
            else
            {
                // Authentic Item Stall Browse Viewport (AC 23:33 -> Form_BuyItemStall_1)
                var browse33Pkt = BuildStallBrowse33Packet(stall);
                if (browse33Pkt != null) player.Send(browse33Pkt);
            }

            // 2. Send AC 20:15 (0x14:0x0F) - Alternative stall browse packet
            var browsePkt = BuildStallBrowsePacket(stall);
            if (browsePkt != null) player.Send(browsePkt);

            // 3. Send AC 56:3 - Alternative stall catalog protocol
            var catalogPkt = BuildStallCatalogPacket(stall);
            if (catalogPkt != null) player.Send(catalogPkt);

            // 4. Output catalog to player chat / system message for immediate readability
            string stockTypeStr = stall.IsInfinite ? "INFINITE STOCK" : "LIMITED STOCK";
            string typeHeader = stall.StallType == StallType.Pet ? "PET BAZAAR" : (stall.StallType == StallType.VehicleUtensil ? "VEHICLE & UTENSIL BAZAAR" : "ITEM BAZAAR");
            player.SendSystemMessage($"==================================================");
            player.SendSystemMessage($"[{typeHeader}] {stall.StallName.ToUpper()}");
            player.SendSystemMessage($"Merchant: {stall.VendorName} | Mode: {stockTypeStr}");
            player.SendSystemMessage($"Location: Map {stall.MapId} ({stall.X}, {stall.Y})");
            player.SendSystemMessage($"--------------------------------------------------");
            if (stall.Items.Count == 0)
            {
                player.SendSystemMessage("This stall is currently out of stock.");
            }
            else
            {
                foreach (var item in stall.Items)
                {
                    string qtyStr = (stall.IsInfinite || item.IsInfinite) ? "[∞ Stock]" : $"[Stock: {item.Quantity}]";
                    player.SendSystemMessage($"  Slot #{item.SlotIndex}: {item.ItemName} (#{item.ItemId}) {qtyStr} - {item.PriceGold:N0} Gold");
                }
                player.SendSystemMessage($"--------------------------------------------------");
                player.SendSystemMessage($"Click an item in shop window, or type: :buystall <slot 0..{stall.Items.Count - 1}> [quantity]");
            }
            player.SendSystemMessage($"==================================================");

            // 5. Release/unlock player movement
            player.Send(Tools.FromFormat("bb", 20, 8));
        }

        public bool PurchaseItem(Player buyer, uint vendorCharId, byte slotIndex, int quantity = 1)
        {
            if (buyer == null) return false;

            try
            {
                lock (m_stallLock)
                {
                    if (!m_stalls.TryGetValue(vendorCharId, out var stall)) return false;

                    var item = stall.Items.FirstOrDefault(i => i.SlotIndex == slotIndex);
                    if (item == null && slotIndex > 0 && slotIndex <= stall.Items.Count)
                    {
                        item = stall.Items[slotIndex - 1];
                    }
                    else if (item == null && slotIndex < stall.Items.Count)
                    {
                        item = stall.Items[slotIndex];
                    }
                    if (item == null) return false;

                    bool isItemInfinite = stall.IsInfinite || item.IsInfinite;
                    if (!isItemInfinite && item.Quantity < quantity)
                    {
                        buyer.SendHeadBanner("This item is out of stock.");
                        return false;
                    }

                    uint totalCost = item.PriceGold * (uint)quantity;
                    if (buyer.Gold < totalCost)
                    {
                        buyer.SendHeadBanner("Insufficient gold to purchase this item.");
                        return false;
                    }

                    if (stall.StallType == StallType.Pet)
                    {
                        // Pet Stall: Companion Pet Party Joining Logic
                        var roster = buyer.PlayerPets;
                        if (roster == null)
                        {
                            buyer.SendHeadBanner("Pet roster unavailable.");
                            return false;
                        }

                        lock (roster)
                        {
                            if (roster.Values.Any(petEntry => petEntry != null && petEntry.PetID != 0 && Player.IsSamePetOrCompanion(petEntry.PetID, item.ItemId)))
                            {
                                buyer.SendHeadBanner("This companion is already in your party.");
                                return false;
                            }
                            byte petSlot = 1;
                            while (petSlot <= 4 && roster.ContainsKey(petSlot)) petSlot++;
                            if (petSlot > 4 || roster.Values.Count(petEntry => petEntry != null && petEntry.PetID != 0) >= 4)
                            {
                                buyer.SendHeadBanner("Your pet party is full (Max 4 pets). Free a slot first.");
                                return false;
                            }

                            buyer.Gold -= totalCost;

                            if (isItemInfinite)
                            {
                                item.Quantity = item.MaxQuantity > 0 ? item.MaxQuantity : 1;
                            }
                            else
                            {
                                item.Quantity -= 1;
                                if (item.Quantity <= 0)
                                {
                                    stall.Items.Remove(item);
                                }
                                if (stall.VendorPlayer == null)
                                {
                                    SaveStallToDatabase(stall);
                                }
                            }

                            string petName = !string.IsNullOrWhiteSpace(item.ItemName) ? item.ItemName.Split('(')[0].Trim() : DataFiles.SceneDataManager.GetNpcName(item.ItemId);
                            var pet = new Player.PlayerPetData
                            {
                                Slot = petSlot,
                                PetID = item.ItemId,
                                PetName = petName,
                                Level = 1,
                                Amity = 60
                            };
                            pet.InitializeBaseStats();
                            pet.NormalizeClientStats(true);
                            buyer.RegisterClientPet(pet);
                            roster[petSlot] = pet;
                            buyer.Send(QuestRelated.QuestManager.CreatePetPacket(buyer, pet.PetID, pet.Slot,
                                pet.HP, pet.MaxHP, pet.SP, pet.MaxSP, pet.Amity, pet.Level,
                                pet.Str, pet.Con, pet.Int, pet.Wis, pet.Agi, pet.Exp, pet.Reborn, pet.Job));
                            QuestRelated.QuestManager.SendPetProgression(buyer, pet);
                            var namePacket = QuestRelated.QuestManager.CreatePetNamePacket(buyer, pet);
                            if (namePacket != null) buyer.Send(namePacket);
                            buyer.Send(Tools.FromFormat("bb", 23, 15));
                            buyer.SendHeadBanner($"{pet.PetName} joined your party.");
                            buyer.SaveCharacterData();
                        }
                    }
                    else
                    {
                        // Item Stall & Vehicle/Utensil Stall: Inventory Bag Logic
                        if (buyer.Inv != null && buyer.Inv.HasItemDefinition(item.ItemId) && !buyer.Inv.CanAddItems(new Dictionary<ushort, int> { { item.ItemId, quantity } }))
                        {
                            buyer.SendHeadBanner("Your inventory is full.");
                            return false;
                        }

                        buyer.Gold -= totalCost;

                        if (isItemInfinite)
                        {
                            item.Quantity = item.MaxQuantity > 0 ? item.MaxQuantity : 50;
                        }
                        else
                        {
                            item.Quantity -= quantity;
                            if (item.Quantity <= 0)
                            {
                                stall.Items.Remove(item);
                            }
                            if (stall.VendorPlayer == null)
                            {
                                SaveStallToDatabase(stall);
                            }
                        }

                        // Grant item or vehicle to buyer bag
                        buyer.Inv?.AddItem(item.ItemId, (byte)quantity);
                        buyer.SendHeadBanner($"Purchased {quantity}x {item.ItemName} ({totalCost:N0}G)");
                    }

                    // Credit vendor if online
                    if (stall.VendorPlayer != null)
                    {
                        stall.VendorPlayer.Gold += totalCost;
                        stall.VendorPlayer.Send(Tools.FromFormat("bbd", 26, 4, (uint)stall.VendorPlayer.Gold));
                        if (item.InvSlot > 0 && !isItemInfinite)
                        {
                            stall.VendorPlayer.Inv?.RemoveItem(item.InvSlot, (byte)quantity);
                        }
                        stall.VendorPlayer.SendHeadBanner($"{buyer.CharName} purchased {quantity}x {item.ItemName} for {totalCost:N0}G");
                    }

                    string stockModeStr = isItemInfinite ? "(Infinite Stock)" : $"({item.Quantity} remaining)";
                    DebugSystem.Write($"[PlayerStallBazaarEngine] {buyer.CharName} purchased {quantity}x {item.ItemName} from {stall.VendorName} for {totalCost:N0}G {stockModeStr}");

                    // Synchronize Buyer's Gold on Client HUD and Form_BuyItemStall_1 (AC 26:4)
                    buyer.Send(Tools.FromFormat("bbd", 26, 4, (uint)buyer.Gold));

                    // S->C AC 53:2 Purchase ACK
                    SendPacket p = new SendPacket();
                    p.Pack8(53);
                    p.Pack8(2);
                    p.Pack8(slotIndex);
                    p.Pack32(buyer.Gold);
                    buyer.Send(p);

                    // Refresh buyer's open viewport GUI with updated catalog
                    if (stall.Items.Count > 0)
                    {
                        if (stall.StallType == StallType.Pet)
                        {
                            var refreshPkt = BuildPetStallBrowse113Packet(stall);
                            if (refreshPkt != null) buyer.Send(refreshPkt);
                        }
                        else if (stall.StallType == StallType.VehicleUtensil)
                        {
                            var refreshPkt = BuildUtensilStallBrowse133Packet(stall);
                            if (refreshPkt != null) buyer.Send(refreshPkt);
                        }
                        else
                        {
                            var refreshPkt = BuildStallBrowse33Packet(stall);
                            if (refreshPkt != null) buyer.Send(refreshPkt);
                        }
                    }
                    else
                    {
                        buyer.Send(BuildStallFormClosePacket(1));
                    }

                    return true;
                }
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[PlayerStallBazaarEngine] PurchaseItem error: {ex.Message}");
                return false;
            }
        }

        public void CloseStall(Player vendor)
        {
            if (vendor == null) return;

            try
            {
                bool removed = m_stalls.TryRemove(vendor.CharID, out var stall);
                string stallName = stall?.StallName ?? "Private Shop";
                DebugSystem.Write($"[PlayerStallBazaarEngine] Stall '{stallName}' closed for {vendor.CharName}");

                // S->C AC 23:165 1 Native Form_PrivateShop Close (closes Form_PrivateShop, avoids "Stall is in use" popup)
                var formClose = BuildStallFormClosePacket(1);
                vendor.Send(formClose);

                // Reset posture to standing
                vendor.Emote = 0;
                var standPose = BuildSittingPosePacket(vendor.CharID, 0);
                vendor.Send(standPose);
                vendor.CurMap?.Broadcast(standPose, "Ex", vendor.CharID);

                // S->C AC 53:3 Stall closed ACK
                SendPacket p = new SendPacket();
                p.Pack8(53);
                p.Pack8(3); // Closed
                vendor.Send(p);

                // S->C AC 23:32 Native Private Shop Closed (destroys 3D table and signboard)
                var despawn32 = BuildStallDespawnPacket(vendor.CharID);
                vendor.Send(despawn32);
                vendor.CurMap?.Broadcast(despawn32, "Ex", vendor.CharID);

                // S->C AC 23:33 Native Pet Stall Closed (triggers FUN_00454db0 in client, clears actor+0x2232)
                var nativeClose33 = BuildStallNativeClosePacket(vendor.CharID, 33);
                vendor.Send(nativeClose33);
                vendor.CurMap?.Broadcast(nativeClose33, "Ex", vendor.CharID);

                // Clear kiosk appearance for map peers and self (AC 5:16)
                var clearKiosk = BuildKioskStatePacket(vendor.CharID, 0);
                vendor.Send(clearKiosk);
                vendor.CurMap?.Broadcast(clearKiosk, "Ex", vendor.CharID);

                // Clear stall signboard banner for map peers and self (AC 56:30)
                var clearSign = BuildStallSignClearPacket(vendor.CharID);
                vendor.Send(clearSign);
                vendor.CurMap?.Broadcast(clearSign, "Ex", vendor.CharID);

                // Synchronize with legacy StallManager
                try
                {
                    Game.PlayerRelated.StallManager.CloseStall(vendor);
                }
                catch { }
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[PlayerStallBazaarEngine] CloseStall error: {ex.Message}");
            }
        }

        #region Protocol Packet Builders

        public static SendPacket BuildActorSpawnPacket(PlayerStall stall)
        {
            if (stall == null) return null;
            SendPacket p = new SendPacket();
            p.Pack8(4);
            p.Pack32(stall.VendorCharId);
            p.Pack8(3); // BodyStyle.Big_Male (3)
            p.Pack8(1); // Element: Earth (1)
            p.Pack8(50); // Level 50
            p.Pack16(stall.MapId);
            p.Pack16((ushort)stall.X);
            p.Pack16((ushort)stall.Y);
            p.Pack8(0); // Direction
            p.Pack8(0); // Head: Daniel (0)
            p.Pack8(0);
            p.Pack16(44828); // HairColor
            p.Pack16(6781); // SkinColor
            p.Pack16(44828); // ClothingColor
            p.Pack16(6781); // EyeColor
            p.Pack8(2); // WornCount: 2
            p.Pack16(21004); // Daniel's Outfit (Item 21004)
            p.Pack16(24004); // Daniel's Boots (Item 24004)
            p.Pack32(0);
            p.Pack8(0);
            p.PackBool(false); // Reborn
            p.Pack8(0); // Job: None
            p.PackString(stall.VendorName ?? "Merchant");
            p.PackString(string.Empty);
            p.Pack8(255);
            p.Pack32(0); // Guild ID
            p.Pack8(1);
            return p;
        }

        public static SendPacket BuildActorEquipPacket(PlayerStall stall)
        {
            if (stall == null) return null;
            SendPacket p = new SendPacket();
            p.Pack8(5);
            p.Pack8(0);
            p.Pack32(stall.VendorCharId);
            p.Pack16(21004); // Daniel's Outfit
            p.Pack16(24004); // Daniel's Boots
            return p;
        }

        public static SendPacket BuildActorPosPacket(PlayerStall stall)
        {
            if (stall == null) return null;
            SendPacket p = new SendPacket();
            p.Pack8(7);
            p.Pack32(stall.VendorCharId);
            p.Pack16(stall.MapId);
            p.Pack16((ushort)stall.X);
            p.Pack16((ushort)stall.Y);
            return p;
        }

        public static SendPacket BuildSittingPosePacket(uint vendorCharId, byte emote = 1)
        {
            SendPacket p = new SendPacket();
            p.Pack8(32);
            p.Pack8(2);
            p.Pack32(vendorCharId);
            p.Pack8(emote); // 1 = Sitting stance, 0 = Standing stance
            return p;
        }

        public static SendPacket BuildKioskStatePacket(uint vendorCharId, byte active)
        {
            SendPacket p = new SendPacket();
            p.Pack8(5);
            p.Pack8(16);
            p.Pack32(vendorCharId);
            p.Pack8(active);
            return p;
        }

        public static SendPacket BuildStallSignPacket(PlayerStall stall)
        {
            if (stall == null) return null;
            SendPacket p = new SendPacket();
            p.Pack8(56);
            p.Pack8(30);
            p.Pack32(stall.VendorCharId);
            p.PackString(stall.StallName ?? "Player Shop");
            return p;
        }

        public static SendPacket BuildStallSignClearPacket(uint vendorCharId)
        {
            SendPacket p = new SendPacket();
            p.Pack8(56);
            p.Pack8(30);
            p.Pack32(vendorCharId);
            p.PackString(string.Empty);
            return p;
        }

        public static SendPacket BuildStallNativeConfirmPacket(PlayerStall stall)
        {
            if (stall == null) return null;
            SendPacket p = new SendPacket();
            p.Pack8(23);
            p.Pack8(30);
            p.PackString(stall.StallName ?? "Private Shop");
            p.Pack8((byte)stall.Items.Count);
            foreach (var it in stall.Items)
            {
                p.Pack8(it.InvSlot > 0 ? it.InvSlot : it.SlotIndex);
                p.Pack32(it.PriceGold);
            }
            return p;
        }

        public static SendPacket BuildStallMapStatePacket(PlayerStall stall)
        {
            if (stall == null) return null;
            SendPacket p = new SendPacket();
            p.Pack8(23);
            p.Pack8(31);
            p.Pack32(stall.VendorCharId);
            string title = stall.StallName ?? "Player Shop";
            byte[] titleBytes = System.Text.Encoding.ASCII.GetBytes(title);
            p.Pack8((byte)Math.Min(titleBytes.Length, 255));
            p.PackArray(titleBytes);
            p.Pack8((byte)stall.StallType); // 1 = Item Shop, 2 = Pet Shop, 3 = Vehicle / Utensil Shop
            return p;
        }

        public static SendPacket BuildStallDespawnPacket(uint vendorCharId)
        {
            SendPacket p = new SendPacket();
            p.Pack8(23);
            p.Pack8(32);
            p.Pack32(vendorCharId);
            return p;
        }

        public static SendPacket BuildStallFormClosePacket(byte formType = 1)
        {
            SendPacket p = new SendPacket();
            p.Pack8(23);
            p.Pack8(165); // 0xA5
            p.Pack8(formType); // 1 = Form_PrivateShop, 2 = Form_PetStall
            return p;
        }

        public static SendPacket BuildStallNativeClosePacket(uint vendorCharId, byte subCode = 32)
        {
            SendPacket p = new SendPacket();
            p.Pack8(23);
            p.Pack8(subCode);
            p.Pack32(vendorCharId);
            return p;
        }

        public static SendPacket BuildActorDespawnPacket(uint vendorCharId)
        {
            SendPacket p = new SendPacket();
            p.Pack8(12);
            p.Pack32(vendorCharId);
            p.Pack16(0);
            p.Pack16(0);
            p.Pack16(0);
            p.Pack16(0);
            p.Pack8(0);
            return p;
        }

        public static SendPacket BuildNpcSpawnPacket(StallVendorNpc npc)
        {
            if (npc == null) return null;
            SendPacket p = new SendPacket();
            p.Pack8(22);
            p.Pack8(4);
            p.Pack16(npc.CickID);
            p.Pack16(QuestNpc.GetIdleAnimationFrame(npc.TemplateID));
            p.Pack16(npc.X);
            p.Pack16(npc.Y);
            p.Pack8(npc.EntityType);
            p.Pack32(0);
            p.Pack8(0);
            return p;
        }

        public static SendPacket BuildNpcDespawnPacket(ushort clickId, ushort x, ushort y)
        {
            SendPacket p = new SendPacket();
            p.Pack8(22);
            p.Pack8(4);
            p.Pack16(clickId);
            p.Pack16(0xFFFF); // Concealed/despawned
            p.Pack16(x);
            p.Pack16(y);
            p.Pack8(2); // Hidden
            p.Pack32(0x03E7FC18u);
            p.Pack8(0);
            return p;
        }

        public static SendPacket BuildStallBrowse33Packet(PlayerStall stall)
        {
            if (stall == null) return null;
            SendPacket p = new SendPacket();
            p.Pack8(23);
            p.Pack8(33);
            p.Pack32(stall.VendorCharId);
            p.Pack32(0); // PointsCardDay
            p.Pack32(0); // PointsCardPoints
            p.Pack32(0); // PointsCardItem (0 indicates regular Item Stall, non-zero triggers PointsCard branch)
            p.Pack8(0);  // PointsCardType / CardType (byte 17 of param_2, aligns header at 18B so item 1 starts at offset 18 / index 19)

            // Form_BuyItemStall_1 displays up to 5 items in its 3-column DataGrid
            int count = Math.Min(stall.Items.Count, 5);
            for (int i = 0; i < count; i++)
            {
                var it = stall.Items[i];
                bool isInf = stall.IsInfinite || it.IsInfinite;
                byte currentQty = (byte)Math.Min(isInf ? 50 : it.Quantity, 50);
                byte maxQty = 50;

                // Exact 39-byte record alignment unpacked by FUN_0033fa00
                p.Pack8(it.SlotIndex);        // +0x00: 1B SlotIndex
                p.Pack16(it.ItemId);          // +0x01: 2B ItemId
                p.Pack8(currentQty);          // +0x03: 1B CurrentQuantity
                p.Pack8(maxQty);              // +0x04: 1B MaxQuantity
                p.Pack32(it.PriceGold);       // +0x05: 4B UnitPrice (Gold)
                p.Pack8(100);                 // +0x09: 1B Durability
                p.Pack8(100);                 // +0x0A: 1B MaxDurability
                p.Pack16(0);                  // +0x0B: 2B ForgeLevel / Potential
                p.PackArray(new byte[8]);     // +0x0D: 8B Double timestamp / zeroes
                p.Pack8(0);                   // +0x15: 1B Amity
                p.Pack8(0);                   // +0x16: 1B Damage
                p.Pack16(0);                  // +0x17: 2B Jewel
                p.Pack8(0);                   // +0x19: 1B Skill
                p.Pack8(0);                   // +0x1A: 1B Unknown
                p.Pack32(0);                  // +0x1B: 4B Reserved
                p.Pack16(0);                  // +0x1F: 2B Padding1
                p.Pack32(0);                  // +0x21: 4B Padding2
                p.Pack16(0);                  // +0x25: 2B Padding3 (Total: 39 bytes per item)
            }
            return p;
        }

        /// <summary>
        /// Authentic Client Companion Pet Stall Browse Viewport (AC 23:113).
        /// Wire structure unpacked by FUN_0032def0 in aLogin.exe:
        /// Header: [AC: 23] [Sub: 113] [VendorCharId: 4B]
        /// N * 225-byte pet records (0xE1 bytes per pet):
        /// - 1B: Slot Index
        /// - 2B: Pet NPC ID (TemplateID)
        /// - 4B: Price Gold
        /// - 1B: Level
        /// - 4B: HP
        /// - 4B: SP
        /// - 2B: Element / Reborn
        /// - 126B: 6 Worn Equipment Slots (6 * 21B zeros)
        /// - 81B: Name & Stats Block (16B name + 5B stats + zeros)
        /// Total = 1 + 2 + 4 + 1 + 4 + 4 + 2 + 126 + 81 = 225 bytes per pet.
        /// Client dynamically loads Form_BuyPet1Stall_1 (1-5 pets), Form_BuyPet2Stall_1 (6-10 pets), or Form_BuyPet3Stall_1 (11-15 pets).
        /// </summary>
        public static SendPacket BuildPetStallBrowse113Packet(PlayerStall stall)
        {
            if (stall == null) return null;
            SendPacket p = new SendPacket();
            p.Pack8(23);
            p.Pack8(113);
            p.Pack32(stall.VendorCharId);

            int petCount = Math.Min(stall.Items.Count, 15);
            for (int i = 0; i < petCount; i++)
            {
                var it = stall.Items[i];
                ushort petNpcId = it.ItemId;
                uint price = it.PriceGold;
                byte level = 1;
                uint hp = 1000;
                uint sp = 500;
                ushort element = 1;

                p.Pack8((byte)i);           // +0x00: 1B Slot Index
                p.Pack16(petNpcId);         // +0x01: 2B Pet Npc ID
                p.Pack32(price);            // +0x03: 4B Price Gold
                p.Pack8(level);             // +0x07: 1B Level
                p.Pack32(hp);               // +0x08: 4B HP
                p.Pack32(sp);               // +0x0C: 4B SP
                p.Pack16(element);          // +0x10: 2B Element / Flags
                p.PackArray(new byte[126]); // +0x12: 126B Equipment slots (6 * 21B zeros)

                // 81-byte Stats & Name block
                byte[] nameStatBlock = new byte[81];
                string petName = !string.IsNullOrWhiteSpace(it.ItemName) ? it.ItemName.Split('(')[0].Trim() : DataFiles.SceneDataManager.GetNpcName(petNpcId) ?? "Companion";
                byte[] nameBytes = System.Text.Encoding.ASCII.GetBytes(petName);
                Array.Copy(nameBytes, nameStatBlock, Math.Min(nameBytes.Length, 16));
                nameStatBlock[16] = 20; // Str
                nameStatBlock[17] = 20; // Con
                nameStatBlock[18] = 20; // Int
                nameStatBlock[19] = 20; // Wis
                nameStatBlock[20] = 20; // Agi
                nameStatBlock[21] = 60; // Amity
                p.PackArray(nameStatBlock);
            }
            return p;
        }

        /// <summary>
        /// Authentic Client Vehicle & Utensil Stall Browse Viewport (AC 23:133).
        /// Wire structure unpacked by FUN_003325bc in aLogin.exe:
        /// Header: [AC: 23] [Sub: 133] [VendorCharId: 4B] [Padding: 11B] (Total 17B header, local_10 = 0x11)
        /// Utensil records: N * 15 bytes (0x0F bytes per item):
        /// - 1B: Slot Index (1..3)
        /// - 2B: Item ID
        /// - 1B: Durability
        /// - 1B: Max Durability
        /// - 4B: Price Gold
        /// - 4B: Potential / Attribute
        /// - 2B: Padding
        /// Total = 1 + 2 + 1 + 1 + 4 + 4 + 2 = 15 bytes per item.
        /// Client dynamically loads Form_Buy1Utensil_1 (1 item) or Form_Buy3Utensil_1 (2-3 items).
        /// </summary>
        public static SendPacket BuildUtensilStallBrowse133Packet(PlayerStall stall)
        {
            if (stall == null) return null;
            SendPacket p = new SendPacket();
            p.Pack8(23);
            p.Pack8(133);
            p.Pack32(stall.VendorCharId);
            p.PackArray(new byte[11]); // 11B padding aligns header at 17B (Delphi 1-indexed string index 18)

            int count = Math.Min(stall.Items.Count, 3);
            for (int i = 0; i < count; i++)
            {
                var it = stall.Items[i];
                p.Pack8((byte)(i + 1));     // 1B Slot Index (1-indexed for Form_Buy1..3Utensil)
                p.Pack16(it.ItemId);        // 2B Item ID
                p.Pack8(100);               // 1B Durability
                p.Pack8(100);               // 1B Max Durability
                p.Pack32(it.PriceGold);     // 4B Price Gold
                p.Pack32(0);                // 4B Potential / Attribute
                p.Pack16(0);                // 2B Padding
            }
            return p;
        }

        public static SendPacket BuildStallBrowsePacket(PlayerStall stall)
        {
            if (stall == null) return null;
            SendPacket p = new SendPacket();
            p.Pack8(20);
            p.Pack8(15);
            p.Pack32(stall.VendorCharId);
            p.Pack8((byte)stall.Items.Count);
            foreach (var it in stall.Items)
            {
                bool isInf = stall.IsInfinite || it.IsInfinite;
                p.Pack16(it.ItemId);
                p.Pack8((byte)Math.Min(isInf ? 99 : it.Quantity, 99));
                p.Pack8(50);
                p.Pack32(it.PriceGold);
                p.Pack16(100); // Durability
                p.Pack16(100); // MaxDurability
                p.Pack8(0); // ForgeLevel
                p.Pack8(0); // Potential
                p.Pack16(0); // Jewel
                p.Pack32(0); // Reserved
                p.Pack16(0); // Damage
                p.Pack16(0); // Skill
                p.Pack8(0); // Amity
                for (int k = 0; k < 14; k++) p.Pack8(0); // Metadata (14 bytes)
            }
            return p;
        }

        public static SendPacket BuildStallCatalogPacket(PlayerStall stall)
        {
            if (stall == null) return null;
            SendPacket p = new SendPacket();
            p.Pack8(56);
            p.Pack8(3);
            p.Pack32(stall.VendorCharId);
            p.PackString(stall.StallName ?? "Player Shop");
            p.Pack8((byte)stall.Items.Count);
            foreach (var it in stall.Items)
            {
                bool isInf = stall.IsInfinite || it.IsInfinite;
                p.Pack8(it.SlotIndex);
                p.Pack16(it.ItemId);
                p.Pack32(it.PriceGold);
                p.Pack8((byte)Math.Min(isInf ? 255 : it.Quantity, 255));
            }
            return p;
        }

        public static string SerializeItems(List<StallItem> items)
        {
            if (items == null || items.Count == 0) return string.Empty;
            var records = items.Select(i =>
            {
                string safeName = (i.ItemName ?? string.Empty).Replace('|', '_').Replace(';', '_');
                return $"{i.SlotIndex}|{i.ItemId}|{i.Quantity}|{i.PriceGold}|{(i.IsInfinite ? 1 : 0)}|{safeName}";
            });
            return string.Join(";", records);
        }

        public static List<StallItem> DeserializeItems(string data, bool stallInfiniteFallback = false)
        {
            var list = new List<StallItem>();
            if (string.IsNullOrWhiteSpace(data)) return list;

            var records = data.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var rec in records)
            {
                var parts = rec.Split('|');
                if (parts.Length >= 5)
                {
                    if (byte.TryParse(parts[0], out byte slot) &&
                        ushort.TryParse(parts[1], out ushort itemId) &&
                        int.TryParse(parts[2], out int qty) &&
                        uint.TryParse(parts[3], out uint price))
                    {
                        bool isInf = parts[4] == "1" || stallInfiniteFallback;
                        string name = parts.Length > 5 ? parts[5] : Game.Battle.MonsterDropManager.ResolveItemName(itemId) ?? $"Item #{itemId}";
                        list.Add(new StallItem(slot, itemId, name, qty, price, 0, isInf));
                    }
                }
            }
            return list;
        }

        public static List<StallItem> ParseItemList(string input, bool isInfinite = false)
        {
            var list = new List<StallItem>();
            if (string.IsNullOrWhiteSpace(input)) return list;

            var tokens = input.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var token in tokens)
            {
                if (token.Contains(':'))
                {
                    var parts = token.Split(':');
                    if (parts.Length >= 2 &&
                        ushort.TryParse(parts[0], out ushort itemId) &&
                        uint.TryParse(parts[1], out uint price))
                    {
                        int qty = 50;
                        if (parts.Length >= 3 && int.TryParse(parts[2], out int parsedQty))
                        {
                            qty = Math.Max(1, parsedQty);
                        }
                        else if (isInfinite)
                        {
                            qty = 50;
                        }

                        string name = Game.Battle.MonsterDropManager.ResolveItemName(itemId);
                        if (string.IsNullOrWhiteSpace(name)) name = $"Item #{itemId}";

                        list.Add(new StallItem((byte)list.Count, itemId, name, qty, price, 0, isInfinite));
                        if (list.Count >= 10) break;
                    }
                }
            }
            return list;
        }

        #endregion
    }
}
