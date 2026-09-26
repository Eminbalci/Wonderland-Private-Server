using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DataBase;
using DataFiles;
using Game;
using Game.Code;
using Game.Maps;
using Network;
using RCLibrary.Core.Networking;

namespace Game
{
    public interface IMap
    {
        uint MapID { get; set; }
        MapType Type { get; }
        void Broadcast(SendPacket pkt);
        void Broadcast(SendPacket pkt, string parameter, params object[] To);
        bool Teleport(TeleportType teletype, Player sender, ushort portalID = 0, WarpData warp = null);
        bool ProcessInteraction(byte clickID, Player player);
        void RemovePlayer(Player p);
        Game.DataFiles.MapData mapData { get; }
    }

    public class MapGroundItem
    {
        public byte Slot { get; set; }
        public ushort ClickID { get; set; }
        public ushort ItemID { get; set; }
        public string Name { get; set; }
        public ushort X { get; set; }
        public ushort Y { get; set; }
        public int RespawnSeconds { get; set; } = 120;
        public bool IsPickedUp { get; set; } = false;
        public DateTime RespawnTime { get; set; } = DateTime.MinValue;
    }

    public class GameMap : Plugin.PluginObj, IDisposable, IMap
    {
        readonly object mlock = new object();

        Queue<Task> QueuedTasks = new Queue<Task>(252);

        protected List<Player> m_playerlist = new List<Player>();
        protected Dictionary<byte, DroppedItem> ItemsDropped = new Dictionary<byte, DroppedItem>();
        protected List<MapGroundItem> GroundItems = new List<MapGroundItem>();
        protected ConcurrentDictionary<uint, Tent> Tents = new ConcurrentDictionary<uint, Tent>();
        protected Dictionary<byte, WarpDest> Destinations = new Dictionary<byte, WarpDest>();
        protected Dictionary<byte, WarpPortal> Portals = new Dictionary<byte, WarpPortal>();
        public List<Game.Maps.InteractableObjects> NPCs = new List<Game.Maps.InteractableObjects>();

        protected Queue<Player> DisconnectedQueue = new Queue<Player>(50);
        protected Queue<KeyValuePair<DateTime, Action>> WaitingtoLogin = new Queue<KeyValuePair<DateTime, Action>>(105);

        protected uint m_mapid;
        protected string m_name;

        bool shutdown = false;

        public GameMap()
        {
        }

        public List<Player> PlayersList { get { return m_playerlist; } }
        public List<Game.Maps.InteractableObjects> NpcList { get { return NPCs; } }
        public List<MapGroundItem> GroundItemList { get { lock (mlock) return GroundItems; } }

        public GameMap(Plugin.PluginHost host, System.IO.FileInfo src)
            : base(src)
        {
            myhost = (Plugin.PluginHost)host;

            try
            {
                if (src != null)
                {
                    string filename = System.IO.Path.GetFileNameWithoutExtension(src.Name);
                    uint parsedId;
                    if (uint.TryParse(filename, out parsedId))
                    {
                        MapID = parsedId;
                        DebugSystem.Write("[GameMap] Parsed MapID: " + MapID + " from " + src.Name);
                    }
                    else
                    {
                        DebugSystem.Write("[GameMap] Could not parse MapID from filename: " + src.Name);
                    }
                }
            }
            catch (Exception ex)
            {
                DebugSystem.Write("[GameMap] Error parsing MapID: " + ex.Message);
            }

            LoadData();

        }


        /// <summary>
        /// Called to Load Additional Data contained within the individual maps
        /// </summary>
        protected virtual void LoadData()
        {
            this.LogInfo("tesT");
            DebugSystem.Write("[" + Assembly.GetAssembly(this.GetType()).FullName + "] - Initializing Map " + MapID + " - " + MapName);

            var myDllAssembly = Assembly.GetAssembly(this.GetType());

            #region load Interactable Objects for this map
            ReloadSpawns();
            #endregion

            #region load Warp Destinations for this map

            foreach (var y in (from c in myDllAssembly.GetTypes()
                               where c.IsClass && c.IsPublic && typeof(WarpDest).IsAssignableFrom(c)
                               select c))
            {
                WarpDest m = null;
                try
                {
                    m = (Activator.CreateInstance(y) as WarpDest);
                }
                catch { DebugSystem.Write("failed to load Warp Destination " + (Activator.CreateInstance(y) as WarpDest).clickID); }
                if (!Destinations.ContainsKey((byte)m.clickID))
                    Destinations.Add((byte)m.clickID, m);

            }
            //DLogger.DllLog((myDllAssembly == null) ? Assembly.GetExecutingAssembly().FullName : myDllAssembly.FullName, "loaded " + Destinations.Count + " Warp Destinations");
            #endregion

            #region load Warp Portals for this map

            foreach (var y in (from c in myDllAssembly.GetTypes()
                               where c.IsClass && c.IsPublic && typeof(WarpPortal).IsAssignableFrom(c)
                               select c))
            {
                WarpPortal m = null;
                try
                {
                    m = (Activator.CreateInstance(y) as WarpPortal);
                }
                catch { DebugSystem.Write("failed to load Warp Portal " + (Activator.CreateInstance(y) as MapObject).CickID); }
                if (!Portals.ContainsKey((byte)m.CickID))
                    Portals.Add((byte)m.CickID, m);
            }

            //DLogger.DllLog((myDllAssembly == null) ? Assembly.GetExecutingAssembly().FullName : myDllAssembly.FullName, "loaded " + Portals.Count + " Warp Portals");
            #endregion

        }

        public void ReloadSpawns()
        {
            if (DataBase.GameDataBase.GlobalInstance == null) return;

            if (NPCs == null) NPCs = new List<Game.Maps.InteractableObjects>();
            else NPCs.Clear();

            // 1. Native Map Loading (Primary Source)
            try
            {
                // Access EveManager via the singleton GameDataBase instance
                var mapData = DataBase.GameDataBase.GlobalInstance.EveDat.GetMapData(Convert.ToUInt16(this.MapID));
                if (mapData != null && mapData.Npclist != null)
                {
                    foreach (var entry in mapData.Npclist)
                    {
                        try
                        {
                            // Skip corrupted entries with invalid clickId or zero coordinates
                            if (entry.clickId == 0 || (entry.x == 0 && entry.y == 0))
                                continue;

                            Game.Maps.QuestNpc newNpc = new Game.Maps.QuestNpc();
                            newNpc.MapID = (ushort)this.MapID;
                            newNpc.CickID = entry.clickId;
                            newNpc.TemplateID = entry.npcId;
                            newNpc.EntityType = entry.unknownbyte1 > 0 ? entry.unknownbyte1 : (byte)1;
                            newNpc.X = (ushort)entry.x;
                            newNpc.Y = (ushort)entry.y;
                            newNpc.SpawnX = (ushort)entry.x;
                            newNpc.SpawnY = (ushort)entry.y;
                            newNpc.WalkBehavior = entry.unknownbyte4;
                            newNpc.WalkSteps = entry.walksteps ?? new List<DataFiles.npcWalkStep>();
                            newNpc.NextWalkTime = DateTime.Now.AddMilliseconds(Game.Maps.QuestNpc.NextRandom(1000, 8000));

                            // Use native name from eve.Emg by default
                            newNpc.Name = !string.IsNullOrEmpty(entry.Name) ? entry.Name.Trim('\0') : $"NPC_{entry.npcId}";

                            // Set default stats
                            newNpc.Level = 1;
                            newNpc.HP = 100;
                            newNpc.Element = 0;

                            // Resolve authentic NPC info from npc_data database matching Python server
                            try
                            {
                                if (DataBase.GameDataBase.GlobalInstance != null)
                                {
                                    var info = DataBase.GameDataBase.GlobalInstance.ResolveNpcInfo((ushort)this.MapID, (byte)newNpc.CickID, (ushort)newNpc.TemplateID);
                                    if (info != null && !string.IsNullOrEmpty(info.Name))
                                    {
                                        newNpc.Name = info.Name;
                                        newNpc.Level = (ushort)info.Level;
                                        newNpc.HP = (uint)info.HP;
                                        newNpc.Element = (byte)info.Element;
                                    }
                                }
                            }
                            catch (Exception npcDatEx)
                            {
                                DebugSystem.Write($"[Map {MapID}] NPC lookup exception for TID {newNpc.TemplateID}: {npcDatEx.Message}");
                            }

                            NPCs.Add(newNpc);
                            DebugSystem.Write($"[Map {MapID}] Loaded Native NPC {newNpc.Name} (TID {newNpc.TemplateID}) at {newNpc.X},{newNpc.Y}");
                        }
                        catch (Exception ex)
                        {
                            DebugSystem.Write(ex.ToString());
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                DebugSystem.Write("Error loading Native Map Data for Map " + this.MapID);
                DebugSystem.Write(ex.ToString());
            }

            // 2. Database Overrides (Secondary Source) and Custom Spawns
            var n = DataBase.GameDataBase.GlobalInstance.GetNPCsForMap(this.MapID);
            if (n != null)
            {
                foreach (System.Data.DataRow row in n.Rows)
                {
                    try
                    {
                        var newNpc = new Game.Maps.QuestNpc();
                        newNpc.CickID = Convert.ToByte(row["click_id"]);
                        newNpc.X = Convert.ToUInt16(row["x"]);
                        newNpc.Y = Convert.ToUInt16(row["y"]);
                        newNpc.Name = row["npc_name"].ToString();

                        int templateId = 0;
                        try { templateId = Convert.ToInt32(row["template_id"]); } catch { }
                        newNpc.TemplateID = (uint)templateId;

                        // Lookup stats from npc_data if available
                        try
                        {
                            int lookupId = (templateId > 0) ? templateId : newNpc.CickID;
                            var template = DataBase.GameDataBase.GlobalInstance.GetDataTable($"SELECT * FROM npc_data WHERE id={lookupId} LIMIT 1");
                            if (template != null && template.Rows.Count > 0)
                            {
                                newNpc.Level = Convert.ToUInt16(template.Rows[0]["level"]);
                                newNpc.HP = Convert.ToUInt32(template.Rows[0]["hp"]);
                                newNpc.Element = Convert.ToByte(template.Rows[0]["element"]);
                                if (newNpc.Name == "Unknown" || newNpc.Name.StartsWith("Imported"))
                                    newNpc.Name = template.Rows[0]["name"].ToString();
                            }
                        }
                        catch { }

                        // Override Native Spawn if exists
                        var existing = NPCs.FirstOrDefault(existingNpc => existingNpc.CickID == newNpc.CickID);
                        if (existing != null)
                        {
                            NPCs.Remove(existing);
                        }
                        NPCs.Add(newNpc);
                        DebugSystem.Write($"[Map {MapID}] Loaded DB NPC {newNpc.Name} (ID {newNpc.CickID}) at {newNpc.X},{newNpc.Y}");
                    }
                    catch { }
                }
            }

            // 3. Native Ground Items from eve.Emg (ItemAreas)
            try
            {
                if (GroundItems == null) GroundItems = new List<MapGroundItem>();
                else GroundItems.Clear();

                var mapData = DataBase.GameDataBase.GlobalInstance.EveDat.GetMapData(Convert.ToUInt16(this.MapID));
                if (mapData != null && mapData.ItemAreas != null && mapData.ItemAreas.Count > 0)
                {
                    byte slotIdx = 1;
                    foreach (var it in mapData.ItemAreas)
                    {
                        if (it.itemID == 0 || (it.x == 0 && it.y == 0)) continue;

                        int respawnSec = (it.unknownword1 > 0) ? (int)it.unknownword1 : 120;
                        var gItem = new MapGroundItem
                        {
                            Slot = (byte)(it.clickID > 0 && it.clickID < 256 ? it.clickID : slotIdx),
                            ClickID = it.clickID,
                            ItemID = (ushort)it.itemID,
                            Name = !string.IsNullOrEmpty(it.Name) ? it.Name.Trim('\0', ' ') : Game.Battle.MonsterDropManager.ResolveItemName((ushort)it.itemID),
                            X = (ushort)it.x,
                            Y = (ushort)it.y,
                            RespawnSeconds = respawnSec,
                            IsPickedUp = false,
                            RespawnTime = DateTime.MinValue
                        };
                        slotIdx++;
                        GroundItems.Add(gItem);
                        DebugSystem.Write($"[Map {MapID}] Loaded Ground Item #{gItem.ItemID} '{gItem.Name}' at slot {gItem.Slot} ({gItem.X}, {gItem.Y}), respawn: {gItem.RespawnSeconds}s");
                    }
                }
            }
            catch (Exception itemEx)
            {
                DebugSystem.Write($"[Map {MapID}] Error loading ground items: {itemEx.Message}");
            }
        }


        #region Properties
        public virtual MapType Type { get { return MapType.RegularMap; } }
        public virtual uint MapID { get { lock (mlock) return m_mapid; } set { lock (mlock) m_mapid = value; } }
        public virtual string MapName { get { return ""; } }
        #endregion

        public void Dispose()
        {
        }

        public void Process()
        {
            try
            {
                Parallel.ForEach(m_playerlist, player =>
                {
                    if (player.IdleTimer() > new TimeSpan(0, 30, 0) || player.isDisconnected())
                    {
                        player.Disconnect();
                        DisconnectedQueue.Enqueue(player);
                    }
                    else
                        player.ProcessSocket();
                });

                // Update NPC AI and roaming (matching Python server's npc_walk_loop)
                if (m_playerlist.Count > 0 && NPCs.Count > 0)
                {
                    DateTime now = DateTime.Now;
                    for (int i = 0; i < NPCs.Count; i++)
                    {
                        if (NPCs[i] is Game.Maps.QuestNpc qNpc)
                        {
                            qNpc.Update(now, this);
                        }
                    }
                }

                // Serialize respawn and pickup against the same map lock.
                lock (mlock)
                {
                    DateTime now = DateTime.Now;
                    foreach (var gi in GroundItems)
                        if (gi.IsPickedUp && now >= gi.RespawnTime)
                        {
                            gi.IsPickedUp = false;
                            gi.RespawnTime = DateTime.MinValue;
                            Broadcast(GroundItemPacket(gi.Slot, gi.ItemID, gi.X, gi.Y));
                        }
                }
            }
            catch { }

            #region Task Handling
            if (QueuedTasks.Count > 0)
            {
                if (QueuedTasks.Peek().IsCompleted)
                {
                    DebugSystem.Write(DebugItemType.Info_Heavy, "Map {0} Task ID: {1} " + ((!QueuedTasks.Peek().IsFaulted) ? "has completed successfully" : "has faulted with Exception " + string.Join(",", QueuedTasks.Peek().Exception.InnerExceptions)), MapID, QueuedTasks.Peek().Id);

                    if (QueuedTasks.Peek().IsFaulted)
                        DebugSystem.Write(new ExceptionData(QueuedTasks.Peek().Exception));
                    QueuedTasks.Dequeue();
                }
                else if (QueuedTasks.Peek().Status == TaskStatus.Created)
                    QueuedTasks.Peek().Start();
                else if (shutdown && QueuedTasks.Peek().Status != TaskStatus.Created)
                    QueuedTasks.Enqueue(QueuedTasks.Dequeue());
            }
            #endregion

            if (WaitingtoLogin.Count > 0 && WaitingtoLogin.Peek().Key < DateTime.Now)
                WaitingtoLogin.Dequeue().Value.Invoke();

            while (DisconnectedQueue.Count > 0)
            {
                var p = DisconnectedQueue.Dequeue();
                m_playerlist.Remove(p);
            }
        }

        public virtual void Process(Player src, RecievePacket data)
        {
            // Implementation for packet processing if needed
        }

        #region Events/Funcs/Actions
        public Action<byte, byte> onItemDropped_fromMap;
        public Func<Item, bool> onItemPickup_fromMap;

        #endregion

        #region Item

        private static SendPacket GroundItemPacket(byte slot, ushort item, ushort x, ushort y)
        {
            var packet = new SendPacket();
            packet.PackArray(new byte[] { 23, 4 });
            AppendGroundItem(packet, slot, item, x, y);
            return packet;
        }

        private static void AppendGroundItem(SendPacket packet, byte slot, ushort item, ushort x, ushort y)
        {
            packet.Pack8(3);
            packet.Pack16(slot);
            packet.Pack32(item);
            packet.Pack16(x);
            packet.Pack16(y);
            packet.Pack32(0);
        }

        public void onItemDrop(Player src, byte loc, byte ammt)
        {
            if (src?.Inv == null || loc < 1 || loc > 50 || ammt == 0) return;
            lock (mlock)
            lock (src.Inv.SyncRoot)
            {
                if (!ReferenceEquals(src.CurMap, this) || src.NativeEventActive ||
                    Game.Battle.PvEBattleManager.IsInBattle(src)) return;
                var item = src.Inv[loc];
                if (item.ItemID == 0 || item.isLocked || !item.Dropable || ammt > item.Ammt) return;

                // Native nodes retain their IDs while waiting to respawn.
                // Reserve click IDs too, so pickups cannot target another item.
                var free = new List<byte>();
                for (int slot = 1; slot <= 255 && free.Count < ammt; slot++)
                    if (!ItemsDropped.ContainsKey((byte)slot) && !GroundItems.Any(g =>
                        g.Slot == slot || g.ClickID == slot))
                        free.Add((byte)slot);
                if (free.Count < ammt)
                {
                    src.SendHeadBanner("There is no room to drop these items here.");
                    return;
                }

                var removed = src.Inv.RemoveItem(loc, ammt);
                if (removed == null) return;
                var packet = new SendPacket();
                packet.PackArray(new byte[] { 23, 4 });
                foreach (byte slot in free)
                {
                    var drop = new DroppedItem();
                    drop.CopyFrom(removed);
                    drop.Ammt = 1;
                    drop.Parent = 0;
                    drop.X = src.CurX;
                    drop.Y = src.CurY;
                    ItemsDropped.Add(slot, drop);
                    AppendGroundItem(packet, slot, drop.ItemID, drop.X, drop.Y);
                }
                Broadcast(packet);
                DebugSystem.Write($"[Map {MapID}] {src.CharName} dropped #{removed.ItemID} x{removed.Ammt} from bag slot {loc}.");
            }
            src.SaveCharacterData();
        }

        public void onItemPickup(Player src, byte pos)
        {
            if (src?.Inv == null || pos == 0) return;
            lock (mlock)
            {
                if (!ReferenceEquals(src.CurMap, this) || src.NativeEventActive ||
                    Game.Battle.PvEBattleManager.IsInBattle(src)) return;
                DroppedItem drop;
                if (ItemsDropped.TryGetValue(pos, out drop))
                {
                    double dx = src.CurX - drop.X, dy = src.CurY - drop.Y;
                    if (dx * dx + dy * dy > 180 * 180) return;
                    if (src.Inv.AddItem(drop) != 1)
                    {
                        src.SendHeadBanner("Your inventory is full.");
                        return;
                    }
                    ItemsDropped.Remove(pos);
                    src.Send(Tools.FromFormat("bbwb", 23, 2, (ushort)pos, (byte)1));
                    Broadcast(Tools.FromFormat("bbwb", 23, 2, (ushort)pos, (byte)0), "Ex", src.CharID);
                    DebugSystem.Write($"[Map {MapID}] {src.CharName} picked up dropped item #{drop.ItemID} from ground slot {pos}.");
                }
                else
                {
                    // A repeated pickup must not fall through to a neighboring node.
                    var gi = GroundItems.FirstOrDefault(g => g.Slot == pos)
                        ?? GroundItems.FirstOrDefault(g => g.ClickID == pos);
                    if (gi == null || gi.IsPickedUp) return;
                    double dx = src.CurX - gi.X, dy = src.CurY - gi.Y;
                    if (dx * dx + dy * dy > 180 * 180) return;
                    if (src.Inv.AddItem(gi.ItemID, 1) != 1)
                    {
                        src.SendHeadBanner("Your inventory is full.");
                        return;
                    }
                    gi.IsPickedUp = true;
                    gi.RespawnTime = DateTime.Now.AddSeconds(gi.RespawnSeconds);
                    src.Send(Tools.FromFormat("bbwb", 23, 2, (ushort)gi.Slot, (byte)1));
                    Broadcast(Tools.FromFormat("bbwb", 23, 2, (ushort)gi.Slot, (byte)0), "Ex", src.CharID);
                    DebugSystem.Write($"[Map {MapID}] {src.CharName} picked up ground item #{gi.ItemID} from slot {gi.Slot}.");
                }
            }
            src.SaveCharacterData();
        }
        #endregion

        #region Warping

        public void RemovePlayer(Player p)
        {
            p?.ClearInteraction();
            if (p != null && m_playerlist.Contains(p))
            {
                m_playerlist.Remove(p);
                DebugSystem.Write($"[Map.RemovePlayer] Removed {p.CharName} from map {MapID}. Remaining players: {m_playerlist.Count}");
            }
        }

        private static void SendPeerCompanionAndVehicle(Player recipient, Player actor)
        {
            if (recipient == null || actor == null) return;

            // Send actor's vehicle to recipient
            if (actor.ActiveVehicleID > 0)
            {
                recipient.Send(PlayerRelated.VehicleManager.CreateMountPacket(actor));
            }

            // Send actor's mount to recipient
            if (actor.ActiveMountID > 0)
            {
                SendPacket mount = new SendPacket();
                mount.Pack8(15);
                mount.Pack8(16);
                mount.Pack8(1);
                mount.Pack32(actor.CharID);
                mount.Pack32(actor.ActiveMountID);
                for (int i = 0; i < 26; i++) mount.Pack8(0);
                recipient.Send(mount);
            }

            // Send actor's battle pet to recipient
            if (actor.ActivePetID > 0)
            {
                var pet = actor.PlayerPets?.Values?.FirstOrDefault(x => Player.IsSamePetOrCompanion(x.PetID, actor.ActivePetID));
                string petName = pet?.PetName ?? QuestRelated.QuestManager.GetNpcName(actor.ActivePetID);

                // AC 15:4 Map Pet Visual Entity
                SendPacket petMapPkt = actor.CreatePetMapPacket(actor.ActivePetID, petName);
                recipient.Send(petMapPkt);

                // AC 19:4 is owner-only and takes a pet ID, not an owner/pet pair.

                // AC 13:5 Follow formation
                SendPacket petFollow = new SendPacket();
                petFollow.PackArray(new byte[] { 13, 5 });
                petFollow.Pack32(actor.CharID);
                petFollow.Pack32(actor.ActivePetID);
                recipient.Send(petFollow);

                // AC 5:8 Sprite refresh
                SendPacket petRefresh = new SendPacket();
                petRefresh.PackArray(new byte[] { 5, 8 });
                petRefresh.Pack32(actor.CharID);
                petRefresh.Pack8(0);
                recipient.Send(petRefresh);
            }
        }

        protected virtual void Warp_In(TeleportType teletype, Player src, WarpData from = null, byte portalID = 0)
        {
            DebugSystem.Write(DebugItemType.Info_Heavy, "{0} warping into {1}", src.CharName, MapName);

            src.Flags.Remove(PlayerFlag.InMap);
            src.CurMap = this;
            src.CurX = from.DstX_Axis;//switch x
            src.CurY = from.DstY_Axis;//switch y

            if (!m_playerlist.Contains(src))
            {
                m_playerlist.Add(src);
                DebugSystem.Write($"[Map.Warp_In] Added {src.CharName} to m_playerlist. Total players: {m_playerlist.Count}");
            }
            else
            {
                DebugSystem.Write($"[Map.Warp_In] Player {src.CharName} already in m_playerlist. Total players: {m_playerlist.Count}");
            }

            // Map 60002 draws the territory emblem before finishing its transition.
            // Native AC70:1:23 resolves the emblem ID to a bitmap index; an absent
            // emblem must resolve to -1 instead of the client's default index 0.
            // No territory owner/emblem is currently persisted by this server.
            if (MapID == 60002)
                src.Send(Tools.FromFormat("bbbbd", 70, 1, 23, 0, 0u));

            // 1. Send AC 12 (Map Warp / Coordinate Initialization) to self and peers
            SendAc12(src, portalID, from ?? new WarpData { DstMap = (ushort)MapID, DstX_Axis = src.CurX, DstY_Axis = src.CurY }, Type == MapType.Tent);

            // 3. Send AC 7 (Position sync) to player
            SendPacket sp7 = new SendPacket();
            sp7.Pack8(7);
            sp7.Pack32(src.CharID);
            sp7.Pack16((ushort)((this.Type == MapType.Tent || this is Tent) ? 63507 : MapID));
            sp7.Pack16(src.CurX);
            sp7.Pack16(src.CurY);
            src.Send(sp7);

            // 4. Send AC 5:0 (Equips / Appearance) to player
            SendPacket selfEq = new SendPacket();
            selfEq.Pack8((byte)5);
            selfEq.Pack8((byte)0);
            selfEq.Pack32(src.CharID);
            selfEq.PackArray(src.Worn_Equips);
            src.Send(selfEq);

            // 5. Send AC 5:8 (Sprite Render Refresh) to player
            SendPacket selfSpr = new SendPacket();
            selfSpr.Pack8((byte)5);
            selfSpr.Pack8((byte)8);
            selfSpr.Pack32(src.CharID);
            selfSpr.Pack8((byte)0);
            src.Send(selfSpr);

            // AC5:4 is sent by AC12:1 after the client acknowledges this map load.

            foreach (var r in m_playerlist)
            {
                if (r != src)
                {
                    if (!src.IsInvisible)
                    {
                        // send to them - send new arrival's spawn to existing player
                        DebugSystem.Write($"[Map.Warp_In] Sending {src.CharName} spawn to {r.CharName}");
                        r.Send(src.ToAC4Packet());

                        SendPacket p = new SendPacket();
                        p.Pack8((byte)5);
                        p.Pack8((byte)0);
                        p.Pack32(src.CharID);
                        p.PackArray(src.Worn_Equips);
                        r.Send(p);

                        p = new SendPacket();
                        p.Pack8((byte)10);
                        p.Pack8((byte)3);
                        p.Pack32(src.CharID);
                        p.Pack8((byte)255);
                        r.Send(p);

                        if (teletype == TeleportType.Login)
                        {
                            p = new SendPacket();
                            p.Pack8((byte)5);
                            p.Pack8((byte)8);
                            p.Pack32(src.CharID);
                            p.Pack8((byte)0);
                            r.Send(p);
                        }

                        // Send new arrival's companion & vehicle to existing player
                        SendPeerCompanionAndVehicle(r, src);
                    }

                    if (!r.IsInvisible)
                    {
                        // send to me - send existing player's FULL spawn data to new arrival
                        DebugSystem.Write($"[Map.Warp_In] Sending {r.CharName} full spawn to {src.CharName}");
                        src.Send(r.ToAC4Packet());

                        // Send existing player's equipment to new arrival
                        SendPacket p = new SendPacket();
                        p.Pack8((byte)5);
                        p.Pack8((byte)0);
                        p.Pack32(r.CharID);
                        p.PackArray(r.Worn_Equips);
                        src.Send(p);

                        // Send existing player's companion & vehicle to new arrival
                        SendPeerCompanionAndVehicle(src, r);

                        p = new SendPacket();
                        p.Pack8((byte)7);
                        p.Pack32(r.CharID);
                        p.Pack16((ushort)MapID);
                        p.Pack16(r.CurX);
                        p.Pack16(r.CurY);
                        src.Send(p);
                    }
                }
            }

            SendMapInfo(src);

            // Synchronize player's own companion pets to themselves and the map
            if (src.PlayerPets != null && src.PlayerPets.Count > 0)
            {
                var petRoster = src.PlayerPets.Values
                    .Where(pet => pet != null && pet.PetID > 0)
                    .OrderBy(pet => pet.Slot)
                    .ToList();

                // Allocate session slots once, then restore the native login roster silently.
                if (!src.PetRosterSynchronized)
                {
                    foreach (var pet in petRoster) pet.ClientSlot = 0;
                    foreach (var pet in petRoster)
                    {
                        src.RegisterClientPet(pet);
                    }
                    SendPacket rosterPacket = QuestRelated.QuestManager.CreatePetListPacket(src);
                    if (rosterPacket != null) src.Send(rosterPacket);
                    src.PetRosterSynchronized = true;
                }

                // All slot-addressed updates use the client allocation, not DB order.
                foreach (var pet in petRoster)
                {
                    if (pet.ClientSlot == 0) continue;
                    pet.NormalizeExpForLevel();
                    pet.NormalizeClientStats(inventory: src.Inv);
                    QuestRelated.QuestManager.SendPetProgression(src, pet);
                    SendPacket petNamePacket = QuestRelated.QuestManager.CreatePetNamePacket(src, pet);
                    if (petNamePacket != null) src.Send(petNamePacket);

                    if (pet.IsBattle || (src.ActivePetID > 0 && Player.IsSamePetOrCompanion(pet.PetID, src.ActivePetID)))
                    {
                            // Resolve the broadcast-safe companion ID (e.g. Robinson: 12032 DB -> 12178 client display)
                            uint broadcastPetId = Player.GetCompanionBroadcastId(pet.PetID);
                            src.ActivePetID = broadcastPetId;
                            pet.IsBattle = true;

                            // AC 19:1 Set battle companion state to owner
                            src.Send(Tools.FromFormat("bbd", 19, 1, broadcastPetId));

                            // AC 15:4 describes a remote map pet; never send it to its owner.
                            SendPacket petMapPkt = src.CreatePetMapPacket(broadcastPetId, pet.PetName);
                            Broadcast(petMapPkt, "Ex", src.CharID);

                            // AC 19:1 above is sufficient for the owner; never broadcast 19:4.

                            // AC 13:5 Broadcast companion follow formation to peers
                            SendPacket petFollow = new SendPacket();
                            petFollow.PackArray(new byte[] { 13, 5 });
                            petFollow.Pack32(src.CharID);
                            petFollow.Pack32(broadcastPetId);
                            src.Send(petFollow);
                            Broadcast(petFollow, "Ex", src.CharID);

                            // AC 5:8 Appearance refresh
                            SendPacket petRefresh = new SendPacket();
                            petRefresh.PackArray(new byte[] { 5, 8 });
                            petRefresh.Pack32(src.CharID);
                            petRefresh.Pack8(0);
                            src.Send(petRefresh);
                            Broadcast(petRefresh, "Ex", src.CharID);

                            DebugSystem.Write($"[Map.Warp_In] Dispatched companion '{pet.PetName}' (ID: {broadcastPetId}) to {src.CharName} and broadcast following state to peers");
                    }
                }
            }

            // An empty roster is initialized too; later rewards allocate session slots.
            src.PetRosterSynchronized = true;

            // Synchronize party following formation on map entry
            if (src.m_teammembers != null && src.m_teammembers.Count > 0)
            {
                var leader = src.m_teammembers.FirstOrDefault(x => x.PartyLeader);
                if (leader != null)
                {
                    if (src == leader)
                    {
                        foreach (var m in src.m_teammembers)
                        {
                            if (m != null && m != leader && m.CurMap == src.CurMap)
                            {
                                SendPacket partyFollow = new SendPacket();
                                partyFollow.PackArray(new byte[] { 13, 5 });
                                partyFollow.Pack32(leader.CharID);
                                partyFollow.Pack32(m.CharID);
                                m.Send(partyFollow);
                                Broadcast(partyFollow, "Ex", leader.CharID);
                            }
                        }
                    }
                    else if (leader.CurMap == src.CurMap)
                    {
                        SendPacket partyFollow = new SendPacket();
                        partyFollow.PackArray(new byte[] { 13, 5 });
                        partyFollow.Pack32(leader.CharID);
                        partyFollow.Pack32(src.CharID);
                        src.Send(partyFollow);
                        Broadcast(partyFollow, "Ex", leader.CharID);
                    }

                    // Refresh party list & stats on map transition
                    leader.BroadcastPartyUpdate();
                }
            }

            // Authentic: real server sends A=54 B=201 catalog on every map enter (not just login)
            if (teletype != TeleportType.Login)
            {
                Game.PlayerRelated.ItemMallManager.SendCatalog(src);
            }

            // Synchronize active and completed quest flags for client PreEvent NPC rendering
            Game.QuestRelated.QuestManager.SendAllQuestFlags(src);
            Game.QuestRelated.QuestManager.ReplayActorVisibility(src, this);
            Game.QuestRelated.PreEventInterpreter.EvaluateMapPreEvents(src, (ushort)this.MapID);

            // Astrologer Laura Exit Cutscene & Space Tent Gift (Map 10001 -> Map 10000)
            if (MapID == 10000 && src.PrevMap?.DstMap == 10001)
            {
                bool hasTent = src.Inv != null && (src.Inv.ContainsItem(32000, out _) || src.Inv.ContainsItem(32001, out _));
                if (!hasTent)
                {
                    // 1. Play Tent acquisition cutscene frame
                    src.Send(Tools.FromFormat("bbbbwb", 20, 1, 0, 1, (ushort)30126, (byte)0));

                    // 2. Add Space Tent (32000) and Space Remote (32075) to inventory
                    if (src.Inv != null)
                    {
                        src.Inv.AddItem(32000, 1);
                        src.Inv.AddItem(32075, 1);
                    }

                    // 3. Mark Astrologer Tent Quest (10035) Completed
                    if (src.Quests == null) src.Quests = new Dictionary<uint, QuestRelated.PlayerQuest>();
                    src.Quests[10035] = new QuestRelated.PlayerQuest(10035, QuestRelated.QuestState.Completed, 1)
                    {
                        CompletedAt = DateTime.UtcNow
                    };
                    QuestRelated.QuestManager.SavePlayerQuest(src, 10035);
                    QuestRelated.QuestManager.SendQuestUpdate(src, 10035, QuestRelated.QuestState.Completed);

                    src.Send(Tools.FromFormat("bbbs", 23, 57, 0, " Astrologer Laura gifted you the Space Tent & Remote Control!"));
                    DebugSystem.Write($"[Map.Warp_In] Astrologer Laura cutscene executed: Granted Space Tent (32000) and Remote (32075) to {src.CharName}");
                }
            }
        }

        protected virtual void Warp_Out(byte portalID, Player src, WarpData To, bool toTent = false)
        {
            src.PrevMap = new WarpData();
            src.PrevMap.DstMap = (ushort)MapID;
            src.PrevMap.DstX_Axis = src.CurX;
            src.PrevMap.DstY_Axis = src.CurY;

            // Departure only notifies old-map peers. Warp_In sends the one
            // load command to the moving player after destination state is set.
            SendAc12(src, portalID, To, toTent, sendToTarget: false);
            m_playerlist.Remove(src);
        }

        public Game.DataFiles.MapData mapData
        {
            get { return DataBase.GameDataBase.GlobalInstance?.EveDat?.GetMapData(Convert.ToUInt16(this.MapID)); }
        }

        public bool LookupPortal(ushort portalID, int px, int py, out ushort dstMap, out ushort dstX, out ushort dstY)
        {
            dstMap = 0; dstX = 0; dstY = 0;

            // Tent exit portal handling: if this map is a tent, return to saved overworld location
            if (this.Type == MapType.Tent || this is Tent)
            {
                if (this is Tent t && t.OwnerMap != null)
                {
                    dstMap = (ushort)t.OwnerMap.MapID;
                    dstX = (ushort)t.X;
                    dstY = (ushort)t.Y;
                    return true;
                }
            }

            // 1. Check local Portals and Destinations dictionaries (legacy overrides)
            if (portalID <= byte.MaxValue && Portals.ContainsKey((byte)portalID) && Destinations.ContainsKey((byte)Portals[(byte)portalID].DstID))
            {
                byte destId = (byte)Portals[(byte)portalID].DstID;
                var target = Destinations[destId];
                dstMap = (ushort)target.DstID;
                dstX = (ushort)target.DstX;
                dstY = (ushort)target.DstY;
                return true;
            }

            // 2. Check Database Overrides (PortalDataBase)
            if (PortalDataBase.Instance != null)
            {
                try
                {
                    var portalData = PortalDataBase.Instance.GetPortalsForMap(MapID);
                    if (portalData != null)
                    {
                        foreach (System.Data.DataRow row in portalData.Rows)
                        {
                            if (Convert.ToUInt16(row["portalID"]) == portalID)
                            {
                                byte destId = Convert.ToByte(row["destID"]);
                                var destData = PortalDataBase.Instance.GetDestinationsForMap(MapID);
                                if (destData != null)
                                {
                                    foreach (System.Data.DataRow dRow in destData.Rows)
                                    {
                                        if (Convert.ToByte(dRow["destID"]) == destId)
                                        {
                                            dstMap = Convert.ToUInt16(dRow["dstMap"]);
                                            dstX = Convert.ToUInt16(dRow["dstX"]);
                                            dstY = Convert.ToUInt16(dRow["dstY"]);
                                            return true;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                catch { }
            }

            var currentWarpLoc = mapData?.WarpLoc;
            if (currentWarpLoc != null && currentWarpLoc.Count > 0)
            {
                // 3. Priority A: Geometric reverse matching based on player stepping position (px, py)
                if (px > 0 && py > 0)
                {
                    DataFiles.WarpInfo bestWarp = null;
                    double bestDist = 999999;

                    foreach (var w in currentWarpLoc)
                    {
                        var dstMapData = DataBase.GameDataBase.GlobalInstance?.EveDat?.GetMapData(w.mapID);
                        if (dstMapData != null && dstMapData.WarpLoc != null)
                        {
                            foreach (var revW in dstMapData.WarpLoc)
                            {
                                if (revW.mapID == MapID)
                                {
                                    double dist = Math.Sqrt(Math.Pow(px - (int)revW.x, 2) + Math.Pow(py - (int)revW.y, 2));
                                    if (dist < bestDist)
                                    {
                                        bestDist = dist;
                                        bestWarp = w;
                                    }
                                }
                            }
                        }
                    }

                    if (bestWarp != null && bestDist < 600 && bestWarp.mapID > 0)
                    {
                        dstMap = bestWarp.mapID;
                        dstX = (ushort)bestWarp.x;
                        dstY = (ushort)bestWarp.y;
                        DebugSystem.Write($"[Portal] Geometric reverse match: Map {MapID} pos({px},{py}) -> Map {dstMap} ({dstX},{dstY}) dist={bestDist:F1}");
                        return true;
                    }
                }

                // 4. Priority B: Direct match on clickID == portalID (Authentic Eve.emg Portal Click ID)
                foreach (var w in currentWarpLoc)
                {
                    if (w.clickID == portalID && w.mapID > 0)
                    {
                        dstMap = w.mapID;
                        dstX = (ushort)w.x;
                        dstY = (ushort)w.y;
                        DebugSystem.Write($"[Portal] Eve.emg 'clickID' match: Map {MapID} portal {portalID} -> Map {dstMap} ({dstX},{dstY})");
                        return true;
                    }
                }

                // 5. Priority C: 1-based index match (portalID <= Count)
                if (portalID >= 1 && portalID <= currentWarpLoc.Count)
                {
                    var w = currentWarpLoc[portalID - 1];
                    if (w.mapID > 0)
                    {
                        dstMap = w.mapID;
                        dstX = (ushort)w.x;
                        dstY = (ushort)w.y;
                        DebugSystem.Write($"[Portal] Eve.emg index match: Map {MapID} portal #{portalID} -> Map {dstMap} ({dstX},{dstY})");
                        return true;
                    }
                }

                // 6. Priority D: Gray-decoded portalID match
                ushort grayID = GrayDecode(portalID);
                if (grayID != portalID)
                {
                    foreach (var w in currentWarpLoc)
                    {
                        if (w.clickID == grayID && w.mapID > 0)
                        {
                            dstMap = w.mapID;
                            dstX = (ushort)w.x;
                            dstY = (ushort)w.y;
                            DebugSystem.Write($"[Portal] Gray-decoded match: Map {MapID} portal {portalID}->{grayID} -> Map {dstMap} ({dstX},{dstY})");
                            return true;
                        }
                    }
                }

                // 7. Priority E: Single-exit fallback (if map has exactly one valid warp destination)
                var validWarps = currentWarpLoc.Where(w => w.mapID > 0).ToList();
                if (validWarps.Count == 1)
                {
                    var singleWarp = validWarps[0];
                    dstMap = singleWarp.mapID;
                    dstX = (ushort)singleWarp.x;
                    dstY = (ushort)singleWarp.y;
                    DebugSystem.Write($"[Portal] Single-exit fallback: Map {MapID} -> Map {dstMap} ({dstX},{dstY})");
                    return true;
                }
            }

            // 9. Emergency Fallback for invalid/test maps (e.g. Map < 1000)
            if (MapID < 1000)
            {
                dstMap = 12000;
                dstX = 892;
                dstY = 734;
                DebugSystem.Write($"[Portal] Invalid Map #{MapID} Emergency Recovery -> Map 12000 (892, 734)");
                return true;
            }

            return false;
        }

        private static ushort GrayDecode(ushort n)
        {
            ushort mask = n;
            while (mask > 0)
            {
                mask >>= 1;
                n ^= mask;
            }
            return n;
        }

        public bool Teleport(TeleportType teletype, Player sender, ushort portalID = 0, WarpData warp = null)
        {
            if (sender == null) return false;

            if (teletype == TeleportType.Regular)
            {
                double elapsedMs = (DateTime.UtcNow - sender.LastTeleportTime).TotalMilliseconds;
                if (elapsedMs < 2500)
                {
                    DebugSystem.Write($"[Teleport] Portal cooldown active ({elapsedMs:F0}ms / 2500ms) for {sender.CharName}. Request ignored.");
                    sender.Send(Tools.FromFormat("bb", 20, 8));
                    return false;
                }
                if (elapsedMs < 4000 && sender.LastSpawnX > 0 && sender.LastSpawnY > 0)
                {
                    double spawnDist = Math.Sqrt(Math.Pow((int)sender.CurX - (int)sender.LastSpawnX, 2) + Math.Pow((int)sender.CurY - (int)sender.LastSpawnY, 2));
                    if (spawnDist < 120)
                    {
                        DebugSystem.Write($"[Teleport] Spawn proximity guard active for {sender.CharName} (dist={spawnDist:F1}px from spawn {sender.LastSpawnX},{sender.LastSpawnY}). Request ignored.");
                        sender.Send(Tools.FromFormat("bb", 20, 8));
                        return false;
                    }
                }
                DebugSystem.Write($"[Teleport] Req: {teletype}, PortalID: {portalID}, Leader: {sender.PartyLeader}, Members: {sender.m_teammembers?.Count ?? 0}");
            }

            SendPacket tmp = new SendPacket();

            if (teletype != TeleportType.Login)
            {
                if (teletype == TeleportType.Regular || teletype == TeleportType.CmD)
                    sender.Send(Tools.FromFormat("bb", 20, 7));

                tmp.Pack8((byte)23);
                tmp.Pack8((byte)32);
                tmp.Pack32(sender.CharID);
                sender.Send(tmp);
                tmp = new SendPacket();
                tmp.Pack8((byte)23);
                tmp.Pack8((byte)112);
                tmp.Pack32(sender.CharID);
                sender.Send(tmp);
                tmp = new SendPacket();
                tmp.Pack8((byte)23);
                tmp.Pack8((byte)132);
                tmp.Pack32(sender.CharID);
                sender.Send(tmp);
            }

            sender.Flags.Add(PlayerFlag.Warping);

            switch (teletype)
            {
                #region Regular Warp
                case TeleportType.Regular:
                    {
                        ushort dstMap = 0, dstX = 0, dstY = 0;
                        bool foundPortal = false;

                        if (warp != null)
                        {
                            dstMap = warp.DstMap;
                            dstX = warp.DstX_Axis;
                            dstY = warp.DstY_Axis;
                            foundPortal = true;
                        }
                        else if (MapID == 11094)
                        {
                            // Carnie exit portal: return to saved previous location!
                            if (sender.CarnieReturnMap != null && sender.CarnieReturnMap.DstMap > 0)
                            {
                                dstMap = sender.CarnieReturnMap.DstMap;
                                dstX = sender.CarnieReturnMap.DstX_Axis;
                                dstY = sender.CarnieReturnMap.DstY_Axis;
                                foundPortal = true;
                                DebugSystem.Write($"[Carnie Exit] Returning {sender.CharName} from Map 11094 to saved Map {dstMap} ({dstX},{dstY})");
                            }
                            else
                            {
                                dstMap = 11016;
                                dstX = 1181;
                                dstY = 243;
                                foundPortal = true;
                                DebugSystem.Write($"[Carnie Exit] Returning {sender.CharName} from Map 11094 to fallback Starter Beach");
                            }
                        }
                        else if (Type == MapType.Tent || this is Tent)
                        {
                            // Tent exit portal: return to saved overworld location!
                            if (sender.TentReturnMap != null && sender.TentReturnMap.DstMap > 0)
                            {
                                dstMap = sender.TentReturnMap.DstMap;
                                dstX = sender.TentReturnMap.DstX_Axis;
                                dstY = sender.TentReturnMap.DstY_Axis;
                                foundPortal = true;
                                DebugSystem.Write($"[Tent Exit Portal] Returning {sender.CharName} from Tent {MapID} to saved Map {dstMap} ({dstX},{dstY})");
                            }
                            else if (this is Tent tent && tent.OwnerMap != null)
                            {
                                dstMap = (ushort)tent.OwnerMap.MapID;
                                dstX = (ushort)tent.X;
                                dstY = (ushort)tent.Y;
                                foundPortal = true;
                                DebugSystem.Write($"[Tent Exit Portal] Returning {sender.CharName} from Tent {MapID} to owner map {dstMap} ({dstX},{dstY})");
                            }
                            else if (sender.PrevMap != null && sender.PrevMap.DstMap > 0)
                            {
                                dstMap = sender.PrevMap.DstMap;
                                dstX = sender.PrevMap.DstX_Axis;
                                dstY = sender.PrevMap.DstY_Axis;
                                foundPortal = true;
                                DebugSystem.Write($"[Tent Exit Portal] Returning {sender.CharName} from Tent {MapID} to PrevMap {dstMap} ({dstX},{dstY})");
                            }
                            else
                            {
                                dstMap = 12000;
                                dstX = 892;
                                dstY = 734;
                                foundPortal = true;
                                DebugSystem.Write($"[Tent Exit Portal] Returning {sender.CharName} from Tent {MapID} to fallback Map {dstMap} ({dstX},{dstY})");
                            }
                        }
                        else
                        {
                            foundPortal = LookupPortal(portalID, sender.CurX, sender.CurY, out dstMap, out dstX, out dstY);
                        }

                        if (!foundPortal)
                        {
                            tmp = new SendPacket();
                            tmp.PackArray(new byte[] { 20, 8 });
                            sender.Send(tmp);
                            DebugSystem.Write($"[Teleport] Portal {portalID} not found on map {MapID} at pos({sender.CurX},{sender.CurY})");
                            return false;
                        }

                        GameMap map = null;
                        if (Game.Maps.MapManager.Instance != null)
                        {
                            map = Game.Maps.MapManager.Instance.GetMap(dstMap);
                        }

                        if (Type != MapType.RegularMap && portalID == 1) // create warp from Prev Map
                        {
                            try
                            {
                                Warp_Out((byte)(portalID & 0xFF), sender, sender.PrevMap);
                            }
                            catch (Exception ex) { DebugSystem.Write($"[Teleport] Warp_Out exception (Map {MapID}→PrevMap): {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}"); }

                            if (Game.Maps.MapManager.Instance != null && (map = Game.Maps.MapManager.Instance.GetMap(dstMap)) != null)
                            {
                                try
                                {
                                    map.Warp_In(teletype, sender, new WarpData() { DstMap = dstMap, DstX_Axis = dstX, DstY_Axis = dstY }, (byte)(portalID & 0xFF));
                                }
                                catch (Exception ex) { DebugSystem.Write($"[Teleport] Warp_In exception (Map {MapID}→{dstMap}): {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}"); }
                            }
                            break;
                        }
                        else
                        {
                            if (map != null)
                            {
                                try
                                {
                                    Warp_Out((byte)(portalID & 0xFF), sender, new WarpData() { DstMap = dstMap, DstX_Axis = dstX, DstY_Axis = dstY });
                                }
                                catch (Exception ex) { DebugSystem.Write($"[Teleport] Warp_Out exception (Map {MapID}→{dstMap}): {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}"); }

                                try
                                {
                                    map.Warp_In(teletype, sender, new WarpData() { DstMap = dstMap, DstX_Axis = dstX, DstY_Axis = dstY }, (byte)(portalID & 0xFF));
                                }
                                catch (Exception ex) { DebugSystem.Write($"[Teleport] Warp_In exception (Map {MapID}→{dstMap}): {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}"); }
                                break;
                            }

                            else
                            {
                                tmp = new SendPacket();
                                tmp.PackArray(new byte[] { 20, 8 });
                                sender.Send(tmp);
                                DebugSystem.Write($"[Teleport] Target map {dstMap} not loaded for portal {portalID}");
                                return false;
                            }
                        }
                    }
                #endregion
                case TeleportType.Special:
                case TeleportType.Quest:
                case TeleportType.Tool:
                    break;
                #region Tent Warp
                case TeleportType.Tent:
                    {
                        // Check if tent exists
                        if (!Tents.ContainsKey(warp.DstMap))
                        {
                            DebugSystem.Write(DebugItemType.Error, $"[ERROR] Tent {warp.DstMap} not found in Tents dictionary!");
                            break;
                        }

                        // Save pre-warp overworld coordinates before switching to interior tent coordinates
                        if (sender.CurMap != null && sender.CurMap.Type != MapType.Tent)
                        {
                            sender.TentReturnMap = new WarpData()
                            {
                                DstMap = (ushort)sender.CurMap.MapID,
                                DstX_Axis = sender.CurX,
                                DstY_Axis = sender.CurY
                            };
                            sender.PrevMap = new WarpData()
                            {
                                DstMap = (ushort)sender.CurMap.MapID,
                                DstX_Axis = sender.CurX,
                                DstY_Axis = sender.CurY
                            };
                            DebugSystem.Write($"[Teleport.Tent] Saved return location for {sender.CharName}: Map {sender.CurMap.MapID} ({sender.CurX},{sender.CurY})");
                        }

                        Warp_Out((byte)(portalID & 0xFF), sender, warp, (teletype == TeleportType.Tent));// warp out of map
                        sender.CurX = warp.DstX_Axis;//switch x
                        sender.CurY = warp.DstY_Axis;//switch y
                        Tents[warp.DstMap].Warp_In(TeleportType.Tent, sender, warp);
                    }
                    break;
                #endregion
                #region Cmd Warp
                case TeleportType.CmD:
                    {
                        // Use MapManager to get the singleton map instance
                        GameMap map = null;
                        if (MapManager.Instance != null)
                        {
                            map = MapManager.Instance.GetMap((ushort)warp.DstMap);
                        }
                        else
                        {
                            // Fallback if manager not initialized (shouldn't happen)
                            map = new GameMap();
                            map.MapID = warp.DstMap;
                            DebugSystem.Write("[Teleport] WARNING: MapManager.Instance is null, creating new map interface!");
                        }

                        Warp_Out((byte)(portalID & 0xFF), sender, warp, (map.Type == MapType.Tent));// warp out of map
                        map.Warp_In(teletype, sender, new WarpData() { DstMap = (ushort)warp.DstMap, DstX_Axis = (ushort)warp.DstX_Axis, DstY_Axis = (ushort)warp.DstY_Axis }, (byte)(portalID & 0xFF));
                    }
                    break;
                #endregion
                case TeleportType.Login: Warp_In(teletype, sender, new WarpData() { DstMap = (ushort)warp.DstMap, DstX_Axis = (ushort)warp.DstX_Axis, DstY_Axis = (ushort)warp.DstY_Axis }); break;
            }

            // Party follow: If sender is party leader, teleport all team members
            // Only for Regular teleports (portal usage), not for CmD/Special to avoid recursion
            if (teletype == TeleportType.Regular && sender.PartyLeader && sender.m_teammembers != null && sender.m_teammembers.Count > 1)
            {
                DebugSystem.Write($"[Teleport] Party leader {sender.CharName} is teleporting. Bringing team members...");

                // Create warp data from sender's new position
                WarpData teamWarp = new WarpData()
                {
                    DstMap = (ushort)(sender.CurMap != null ? sender.CurMap.MapID : 0),
                    DstX_Axis = sender.CurX,
                    DstY_Axis = sender.CurY
                };

                if (teamWarp.DstMap > 0)
                {
                    foreach (var member in sender.m_teammembers.ToList())
                    {
                        if (member != null && member.CharID != sender.CharID && member.CurMap != null)
                        {
                            try
                            {
                                DebugSystem.Write($"[Teleport] Teleporting team member {member.CharName} to follow leader to map {teamWarp.DstMap} ({teamWarp.DstX_Axis},{teamWarp.DstY_Axis})");
                                // Teleport member to same destination using CmD type to avoid recursion
                                member.CurMap.Teleport(TeleportType.CmD, member, portalID, teamWarp);
                            }
                            catch (Exception ex)
                            {
                                DebugSystem.Write($"[Teleport] Error teleporting team member {member.CharName}: {ex.Message}");
                            }
                        }
                    }
                }
            }

            return true;
        }

        protected virtual void SendMapInfo(Player t, bool login = false)
        {
            RCLibrary.Core.Networking.PacketBuilder tmp = new RCLibrary.Core.Networking.PacketBuilder();
            tmp.Begin(null);
            tmp.Add(Tools.FromFormat("bb", 23, 138));
            /* p = new SendPacket();
             p.PackArray(new byte[]{(6, 2);
             p.Pack(1);
             p.SetSize();
             g.SendPacket(t, p);*/
            #region Send Npc (AC 22:4)
            if (this.NPCs != null && this.NPCs.Count > 0)
            {
                SendPacket npcListPkt = new SendPacket();
                npcListPkt.Pack8(22);
                npcListPkt.Pack8(4);

                var eveData = DataBase.GameDataBase.GlobalInstance?.EveDat?.GetMapData((ushort)this.MapID);

                foreach (var npc in this.NPCs.OrderBy(n => n.CickID))
                {
                    QuestNpc qn = npc as QuestNpc;
                    bool isRecruited = qn != null && t.HasStoryCompanionInParty((ushort)qn.TemplateID);
                    bool isHiddenByPreEvent = !Game.QuestRelated.PreEventInterpreter.ShouldNpcBeVisible(t, (ushort)this.MapID, (ushort)npc.CickID);
                    bool isDead = qn != null && qn.IsBroken && qn.RespawnTime == DateTime.MaxValue;

                    bool isHidden = isRecruited || isHiddenByPreEvent || isDead;

                    if (this.MapID == 12000 && qn != null)
                        DebugSystem.Write($"[Map12000.NPC] CickID={npc.CickID} TID={qn.TemplateID} Name={qn.Name} Hidden={isHidden} X={npc.X} Y={npc.Y}");

                    ushort idleFrame = QuestNpc.GetIdleAnimationFrame(qn?.TemplateID ?? 0);
                    ushort state = idleFrame;

                    if (isHidden)
                    {
                        state = 0xFFFF; // Authentic WLO despawned/concealed entity state
                    }
                    else if (qn != null && (qn.IsStaticNpc() || qn.TemplateID >= 19000))
                    {
                        // Check if this prop is tied to a one-time per-player quest
                        bool isQuestProp = false;
                        bool isOpened = false;

                        if (t.Quests != null && eveData != null)
                        {
                            var ev = eveData.Events?.FirstOrDefault(e => e.clickID == qn.CickID);
                            if (ev != null && ev.SubEntry != null)
                            {
                                foreach (var s in ev.SubEntry)
                                {
                                    if (s.unknownword1 > 0)
                                    {
                                        isQuestProp = true;
                                        if (t.Quests.TryGetValue(s.unknownword1, out var pq) && pq.State == Game.QuestRelated.QuestState.Completed)
                                        {
                                            isOpened = true;
                                            break;
                                        }
                                    }
                                }
                            }
                        }

                        // Only non-quest renewable gathering nodes (ore, wood, clay) check shared qn.IsBroken
                        if (!isQuestProp)
                        {
                            isOpened = qn.IsBroken;
                        }

                        // Authentic WLO protocol:
                        // Hold frame 0 for interactive props; FF would loop their frames
                        // 0x0001 is the opened / broken animation frame
                        state = isOpened ? (ushort)0x0001 : idleFrame;
                    }
                    else
                    {
                        state = idleFrame;
                    }

                    byte entityType = isHidden ? (byte)2 : (byte)1;
                    uint duration = isHidden ? 0x03E7FC18u : 0u;

                    // Pack NPC record matching official wire protocol:
                    // [ClickID:w, State:w, X:w, Y:w, EntityType:b, Duration:d, StateFlag:b] (14 bytes)
                    npcListPkt.Pack16(npc.CickID);
                    npcListPkt.Pack16(state);
                    npcListPkt.Pack16(npc.X);
                    npcListPkt.Pack16(npc.Y);
                    npcListPkt.Pack8(entityType);
                    npcListPkt.Pack32(duration);
                    npcListPkt.Pack8(0);
                }
                tmp.Add(npcListPkt);
            }
            #endregion
            #region Send Ground Items (AC 23:4)
            lock (mlock)
            {
                var itemPkt = new SendPacket();
                itemPkt.PackArray(new byte[] { 23, 4 });
                foreach (var gi in GroundItems.Where(g => !g.IsPickedUp))
                    AppendGroundItem(itemPkt, gi.Slot, gi.ItemID, gi.X, gi.Y);
                foreach (var pair in ItemsDropped)
                    AppendGroundItem(itemPkt, pair.Key, pair.Value.ItemID, pair.Value.X, pair.Value.Y);
                if (GroundItems.Any(g => !g.IsPickedUp) || ItemsDropped.Count > 0)
                    tmp.Add(itemPkt);
            }
            #endregion
            //SendNpcs(t);
            //SendItems(t);
            SendOpenTents(t);


            foreach (var r in m_playerlist)
            {
                tmp.Add(Tools.FromFormat("bbd", 23, 122, r.CharID));
                tmp.Add(Tools.FromFormat("bbdb", 10, 3, r.CharID, 255));

                if (r.CharID != t.CharID)
                {
                    if (r.Emote != 0)
                        tmp.Add(Tools.FromFormat("bbdb", 32, 2, r.CharID, r.Emote));
                }
                tmp.Add(Tools.FromFormat("bbd", 23, 76, r.CharID));

            }
            tmp.Add(Tools.FromFormat("bb", 23, 102));

            // Authentic WLO protocol (Official PCAP Seq 979..1000):
            // Conceal recruited companions, PreEvent-hidden actors, and broken props inside the Concealment Synchronization Window
            // strictly BEFORE AC 20:8 movement unfreeze and rendering initiation
            t.ClearInteraction();
            t.HiddenNpcClickIDs.Clear();
            lock (t.SentQuestMinimapMarkers) t.SentQuestMinimapMarkers.Clear();
            if (this.NPCs != null && this.NPCs.Count > 0)
            {
                foreach (var npc in this.NPCs)
                {
                    QuestNpc qn = npc as QuestNpc;
                    bool isRecruited = qn != null && t.HasStoryCompanionInParty((ushort)qn.TemplateID);
                    bool isHiddenByPreEvent = !Game.QuestRelated.PreEventInterpreter.ShouldNpcBeVisible(t, (ushort)this.MapID, (ushort)npc.CickID);
                    bool isDead = qn != null && qn.IsBroken && qn.RespawnTime == DateTime.MaxValue;

                    if (isRecruited || isHiddenByPreEvent || isDead)
                    {
                        t.HiddenNpcClickIDs.Add((ushort)npc.CickID);
                    }
                }
            }

            tmp.Add(Tools.FromFormat("bb", 20, 8));
            // Complete scene loading after AC12:1, not while queuing map data.
            t.LastSpawnX = t.CurX;
            t.LastSpawnY = t.CurY;
            t.LastTeleportTime = DateTime.UtcNow;
            // AC12:1 transitions Warping to InMap after native loading finishes.
            t.Send(new SendPacket(tmp.End()));

            // Personal Client-Side NPC Visibility Sync (Dynamic Quest Stage evaluation)
            QuestRelated.QuestManager.SyncPerPlayerNpcVisibility(t, (ushort)this.MapID);

            // Sync Guild Insignia
            t.CurGuild?.SendInsignia(t);

            // Active vehicle is restored by AC12:1 after native map loading finishes.
        }

        #endregion

        #region Tent
        public void onTentOpened(Tent Tentsrc)
        {
            if (Tents.TryAdd(Tentsrc.MapID, Tentsrc))
                Broadcast(Tools.FromFormat("bbdWddW", 65, 1, Tentsrc.MapID, 36002, Tentsrc.X, Tentsrc.Y, 0));
        }
        public void onTentClosing(Tent tent)
        {
            if (Tents.TryRemove(tent.MapID, out tent))
                Broadcast(Tools.FromFormat("bbd", 65, 4, tent.MapID));
        }
        public void onEnterTent(UInt32 ID, Player p)
        {
            if (p == null) return;

            // Save player's current overworld location before entering tent
            if (this.Type != MapType.Tent)
            {
                ushort retX = (ushort)p.CurX;
                ushort retY = (ushort)p.CurY;
                if ((retX == 0 || retY == 0) && Tents.ContainsKey(ID) && Tents[ID].X > 0)
                {
                    retX = (ushort)Tents[ID].X;
                    retY = (ushort)Tents[ID].Y;
                }

                p.TentReturnMap = new WarpData()
                {
                    DstMap = (ushort)this.MapID,
                    DstX_Axis = retX,
                    DstY_Axis = retY
                };
                p.PrevMap = new WarpData()
                {
                    DstMap = (ushort)this.MapID,
                    DstX_Axis = retX,
                    DstY_Axis = retY
                };
                DebugSystem.Write($"[Map.onEnterTent] Saved return location for {p.CharName}: Map {this.MapID} ({retX},{retY})");
            }

            // Ensure tent exists in dictionary
            if (!Tents.ContainsKey(ID))
            {
                if (p.CharID == ID && p.Tent != null)
                {
                    Tents.TryAdd(ID, p.Tent);
                    DebugSystem.Write($"[Map] Restored owner tent {ID} for {p.CharName}");
                }
                else
                {
                    var owner = DataBase.CharacterDataBase.GlobalInstance?.GetOnlinePlayers()?.FirstOrDefault(x => x.CharID == ID);
                    if (owner?.Tent != null)
                    {
                        Tents.TryAdd(ID, owner.Tent);
                        DebugSystem.Write($"[Map] Restored visitor tent {ID} owned by {owner.CharName}");
                    }
                }
            }

            if (!Tents.ContainsKey(ID))
            {
                DebugSystem.Write(DebugItemType.Error, $"[Map] Tent {ID} could not be resolved.");
                return;
            }

            var targetTent = Tents[ID];

            // Lock guard: if tent is locked, non-owners cannot enter
            if (targetTent.Locked && p.CharID != ID)
            {
                p.Send(Tools.FromFormat("bbbs", 23, 57, 0, "Çadır kilitli! (Tent is locked)"));
                DebugSystem.Write($"[Map.onEnterTent] {p.CharName} blocked from entering locked tent {ID}");
                return;
            }

            // Configure return location on the tent instance
            targetTent.SetReturnLocation((ushort)this.MapID, (ushort)p.CurX, (ushort)p.CurY);

            WarpData tmp = new WarpData();
            tmp.DstMap = (ushort)ID;
            tmp.DstX_Axis = 460;
            tmp.DstY_Axis = 700;
            Teleport(TeleportType.Tent, p, 0, tmp);
        }

        void SendOpenTents(Player p)
        {
            if (Tents != null && Tents.Count > 0)
            {
                foreach (var r in Tents.Values)
                {
                    if (r != null && !r.IsClosed)
                    {
                        p.Send(Tools.FromFormat("bbdWddW", 65, 1, r.MapID, 36002, r.X, r.Y, 0));
                    }
                }
            }
        }

        #endregion

        /// <summary>
        /// Broadcasts a packet to all who are in a Map
        /// </summary>
        /// <param CharacterName="pkt"></param>
        public void Broadcast(SendPacket pkt)
        {
            Broadcast(pkt, "ALL");
        }
        /// <summary>
        /// Broadcasts a SendPacket to certain people who are in a Map
        /// </summary>
        /// <param name="pkt"></param>
        /// <param name="To">"Multiple target IDs as string to send to specific people"</param>
        public void Broadcast(SendPacket pkt, string parameter, params object[] To)
        {
            try
            {
                var players = m_playerlist.ToList();
                switch (parameter)
                {
                    case "ALL":
                        foreach (var c in players) c.Send(pkt);
                        break;
                    case "Ex":
                        foreach (var c in players)
                        {
                            if (To.Count(d => Convert.ToUInt32(d) == c.CharID) == 0)
                                c.Send(pkt);
                        }
                        break;
                    case "To":
                        foreach (var c in players)
                        {
                            if (To.Count(d => Convert.ToUInt32(d) == c.CharID) > 0)
                                c.Send(pkt);
                        }
                        break;
                }
            }
            catch { }
        }

        void SendAc12(Player target, byte portalID, WarpData To, bool toTent = false, bool sendToTarget = true)
        {
            SendPacket sp = new SendPacket();
            sp.Pack8(12);
            sp.Pack32(target.CharID);
            sp.Pack16((toTent) ? (ushort)63507 : To.DstMap);
            sp.Pack16(To.DstX_Axis);
            sp.Pack16(To.DstY_Axis);
            sp.Pack16(portalID);
            sp.Pack8(0);
            if (toTent)
            {
                sp.Pack16(1);
                sp.Pack8(1);
                sp.Pack8(1);
            }
            if (sendToTarget) target.Send(sp);
            Broadcast(sp, "Ex", target.CharID);
        }

        public bool ProcessInteraction(byte clickID, Player player)
        {
            try
            {
                // Find NPC with matching clickID
                var npc = NPCs?.FirstOrDefault(n => n.CickID == clickID);

                if (npc != null)
                {
                    // Spatial proximity check: verify player is within 200 units of NPC
                    double dx = player.CurX - npc.X;
                    double dy = player.CurY - npc.Y;
                    if ((dx * dx) + (dy * dy) > (200 * 200))
                    {
                        DebugSystem.Write($"[ProcessInteraction] Proximity check failed: {player.CharName} at ({player.CurX}, {player.CurY}) is too far from NPC #{clickID} at ({npc.X}, {npc.Y}).");
                        return false;
                    }

                    var qNpc = npc as Game.Maps.QuestNpc;
                    string nName = qNpc != null ? qNpc.Name : $"NPC_{clickID}";
                    uint nTid = qNpc != null ? qNpc.TemplateID : 0;

                    // Block interaction if this NPC is a recruited companion or hidden by PreEvents
                    if (qNpc != null && (player.HasStoryCompanionInParty((ushort)qNpc.TemplateID) || !Game.QuestRelated.PreEventInterpreter.ShouldNpcBeVisible(player, (ushort)this.MapID, clickID)))
                    {
                        DebugSystem.Write($"[ProcessInteraction] Blocked interaction on recruited/hidden NPC #{clickID} '{nName}' for player {player.CharName}");
                        Game.QuestRelated.PreEventInterpreter.SendActorHide(player, clickID);
                        return false;
                    }

                    DebugSystem.Write($"[ProcessInteraction] Found NPC #{clickID} '{nName}' (TID: {nTid}), calling Interact for player {player.CharName}");
                    npc.Interact(player);
                    return true;
                }
                else
                {
                    DebugSystem.Write($"[ProcessInteraction] Unknown NPC #{clickID} on Map #{this.MapID} clicked by {player.CharName}. Interaction rejected.");
                    return false;
                }
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[ProcessInteraction] Error: {ex.Message}");
                return false;
            }
        }

    }
}
