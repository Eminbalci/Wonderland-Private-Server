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

    public class GameMap : Plugin.PluginObj, IDisposable, IMap
    {
        readonly object mlock = new object();

        Queue<Task> QueuedTasks = new Queue<Task>(252);

        protected List<Player> m_playerlist;
        protected List<Item> ItemsDropped;
        //protected ConcurrentDictionary<int, Battle> Battles;
        protected ConcurrentDictionary<uint, Tent> Tents;
        protected Dictionary<byte, WarpDest> Destinations;
        protected Dictionary<byte, WarpPortal> Portals;
        protected List<Game.Maps.InteractableObjects> NPCs;

        protected Queue<Player> DisconnectedQueue;
        protected Queue<KeyValuePair<DateTime, Action>> WaitingtoLogin;

        protected uint m_mapid;
        protected string m_name;

        bool shutdown = false;



        public GameMap()
        {

            m_playerlist = new List<Player>();
            ItemsDropped = new List<Item>(255);
            DisconnectedQueue = new Queue<Player>(50);
            WaitingtoLogin = new Queue<KeyValuePair<DateTime, Action>>(105);
            Tents = new ConcurrentDictionary<uint, Tent>();
            //Battles = new ConcurrentDictionary<int, Battle>();
            Destinations = new Dictionary<byte, WarpDest>();
            Portals = new Dictionary<byte, WarpPortal>();
            NPCs = new List<Game.Maps.InteractableObjects>();
        }

        public List<Player> PlayersList { get { return m_playerlist; } }
        public List<Game.Maps.InteractableObjects> NpcList { get { return NPCs; } }

        public GameMap(Plugin.PluginHost host, System.IO.FileInfo src)
            : base(src)
        {

            myhost = (Plugin.PluginHost)host;
            m_playerlist = new List<Player>();
            ItemsDropped = new List<Item>(255);
            DisconnectedQueue = new Queue<Player>(50);
            WaitingtoLogin = new Queue<KeyValuePair<DateTime, Action>>(105);
            Tents = new ConcurrentDictionary<uint, Tent>();
            //Battles = new ConcurrentDictionary<int, Battle>();
            Destinations = new Dictionary<byte, WarpDest>();
            Portals = new Dictionary<byte, WarpPortal>();
            NPCs = new List<Game.Maps.InteractableObjects>();

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

        public void onItemDrop(Player src, byte loc, byte ammt)
        {
            Task dropItem = new Task(() =>
            {
                byte amt = ammt;
                //send Request to Inv to drop item

                if (src.Inv[loc].ItemID > 0)
                {
                    //if item does not equal null, inv has an item that needs to be dropped in map
                    //if item is not inv will send destroy packet.

                    var rand = new Random();
                    int cnt = 0;

                    for (int a = 0; a < 256; a++)
                    {
                        if (cnt < amt && ItemsDropped[a].ItemID == 0)
                        {
                            DroppedItem gi = new DroppedItem();
                            gi.CopyFrom(src.Inv[loc]);
                            gi.Ammt = 1;
                            gi.X = src.CurX;
                            gi.Y = src.CurY;
                            //gi..StartCountDown();


                            //src.SendPacket(Tools.FromFormat("bbwwwdb", 23, 3, gi.ItemID, gi.DropX, gi.DropY, 0, 1));
                            //Broadcast(Tools.FromFormat("bbwwwdb", 23, 3, gi.ItemID, gi.DropX, gi.DropY, 0, 0), "Ex", src.CharID);
                            cnt++;
                        }
                        else if (cnt >= amt)
                            break;
                    }
                    onItemDropped_fromMap(loc, (byte)cnt);
                }
            }, TaskCreationOptions.PreferFairness);
            QueuedTasks.Enqueue(dropItem);
        }
        public void onItemPickup(Player src, byte pos)
        {
            Task dropItem = new Task(() =>
            {
                byte loc = pos;
                if (ItemsDropped[loc - 1].ItemID > 0)
                {
                    Item res = new Item();
                    res.CopyFrom(ItemsDropped[loc - 1]);
                    ItemsDropped[loc - 1].Clear();

                    if (onItemPickup_fromMap(res))
                    {
                        //foreach (ItemsinMapEntries o in ItemAreas)
                        //{
                        //    if (DroppedItems[itemIndex].entryid == o.clickID && DroppedItems[itemIndex].Respawns)
                        //    {
                        //        o.pickedup = true;
                        //        o.dropin = DateTime.Now.AddMinutes(2);
                        //    }
                        //}
                        src.Send(Tools.FromFormat("bbwb", 23, 2, res.ItemID, 1));
                        Broadcast(Tools.FromFormat("bbwb", 23, 2, res.ItemID, 0), "Ex", src.CharID);
                    }
                }
            });
            QueuedTasks.Enqueue(dropItem);
        }
        #endregion

        #region Warping

        public void RemovePlayer(Player p)
        {
            if (p != null && m_playerlist.Contains(p))
            {
                m_playerlist.Remove(p);
                DebugSystem.Write($"[Map.RemovePlayer] Removed {p.CharName} from map {MapID}. Remaining players: {m_playerlist.Count}");
            }
        }

        protected virtual void Warp_In(TeleportType teletype, Player src, WarpData from = null, byte portalID = 0)
        {
            DebugSystem.Write(DebugItemType.Info_Heavy, "{0} warping into {1}", src.CharName, MapName);

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

            // 1. Send AC 3 (Character Entity Spawn) to self
            src.Send(src.ToAC3Packet());

            // 2. Send AC 12 (Map Warp / Coordinate Initialization) to self and peers
            SendAc12(src, portalID, from ?? new WarpData { DstMap = (ushort)MapID, DstX_Axis = src.CurX, DstY_Axis = src.CurY });

            // 3. Send AC 7 (Position sync) to player
            SendPacket sp7 = new SendPacket();
            sp7.Pack8(7);
            sp7.Pack32(src.CharID);
            sp7.Pack16((ushort)MapID);
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

            // 6. Send AC 5:4 (Finalize Scene Load) to player
            SendPacket selfFin = new SendPacket();
            selfFin.Pack8(5);
            selfFin.Pack8(4);
            src.Send(selfFin);

            foreach (var r in m_playerlist)
            {
                if (r != src)
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

                    // Send new arrival's vehicle to existing player
                    if (src.ActiveVehicleID > 0)
                    {
                        SendPacket srcVeh = new SendPacket();
                        srcVeh.Pack8((byte)15);
                        srcVeh.Pack8((byte)10);
                        srcVeh.Pack8(0);
                        srcVeh.Pack32(src.CharID);
                        srcVeh.Pack16((ushort)src.ActiveVehicleID);
                        r.Send(srcVeh);
                    }

                    // Send new arrival's mount to existing player
                    if (src.ActiveMountID > 0)
                    {
                        SendPacket srcMount = new SendPacket();
                        srcMount.Pack8((byte)15);
                        srcMount.Pack8((byte)16);
                        srcMount.Pack8(1);
                        srcMount.Pack32(src.CharID);
                        srcMount.Pack32(src.ActiveMountID);
                        for (int i = 0; i < 26; i++) srcMount.Pack8(0);
                        r.Send(srcMount);
                    }

                    // Send new arrival's battle pet to existing player
                    if (src.ActivePetID > 0)
                    {
                        var srcPet = src.PlayerPets?.Values?.FirstOrDefault(x => x.PetID == src.ActivePetID);
                        string srcPetName = srcPet?.PetName ?? QuestRelated.QuestManager.GetNpcName(src.ActivePetID);

                        // AC 15:4 Map Pet Visual Entity
                        SendPacket srcPetMapPkt = new SendPacket();
                        srcPetMapPkt.PackArray(new byte[] { 15, 4 });
                        srcPetMapPkt.Pack32(src.CharID);
                        srcPetMapPkt.Pack32(src.ActivePetID);
                        srcPetMapPkt.Pack8(0);
                        srcPetMapPkt.Pack8(1);
                        srcPetMapPkt.PackString(srcPetName);
                        srcPetMapPkt.Pack16(0);
                        r.Send(srcPetMapPkt);

                        // AC 15:1 Pet Info
                        SendPacket srcPetPkt;
                        if (srcPet != null)
                        {
                            srcPetPkt = QuestRelated.QuestManager.CreatePetPacket(src, srcPet.PetID, srcPet.Slot, srcPet.HP, srcPet.MaxHP, srcPet.SP, srcPet.MaxSP, srcPet.Amity, srcPet.Level);
                        }
                        else
                        {
                            srcPetPkt = QuestRelated.QuestManager.CreatePetPacket(src, src.ActivePetID, 1);
                        }
                        r.Send(srcPetPkt);

                        // AC 19:4 Following
                        SendPacket srcFollow = new SendPacket();
                        srcFollow.Pack8(19);
                        srcFollow.Pack8(4);
                        srcFollow.Pack32(src.CharID);
                        srcFollow.Pack32(src.ActivePetID);
                        r.Send(srcFollow);

                        // AC 13:5 Follow formation
                        SendPacket srcPetFollow = new SendPacket();
                        srcPetFollow.PackArray(new byte[] { 13, 5 });
                        srcPetFollow.Pack32(src.CharID);
                        srcPetFollow.Pack32(src.ActivePetID);
                        r.Send(srcPetFollow);

                        // AC 5:8 Sprite refresh
                        SendPacket srcRefresh = new SendPacket();
                        srcRefresh.PackArray(new byte[] { 5, 8 });
                        srcRefresh.Pack32(src.CharID);
                        srcRefresh.Pack8(0);
                        r.Send(srcRefresh);
                    }

                    // send to me - send existing player's FULL spawn data to new arrival
                    DebugSystem.Write($"[Map.Warp_In] Sending {r.CharName} full spawn to {src.CharName}");
                    src.Send(r.ToAC4Packet());

                    // Send existing player's equipment to new arrival
                    p = new SendPacket();
                    p.Pack8((byte)5);
                    p.Pack8((byte)0);
                    p.Pack32(r.CharID);
                    p.PackArray(r.Worn_Equips);
                    src.Send(p);

                    // Send existing player's vehicle if they have one
                    if (r.ActiveVehicleID > 0)
                    {
                        p = new SendPacket();
                        p.Pack8((byte)15);
                        p.Pack8((byte)10); // Ride vehicle
                        p.Pack8(0);
                        p.Pack32(r.CharID);
                        p.Pack16((ushort)r.ActiveVehicleID);
                        src.Send(p);
                        DebugSystem.Write($"[Map.Warp_In] Sent vehicle {r.ActiveVehicleID} of {r.CharName} to {src.CharName}");
                    }

                    // Send existing player's mount (riding pet) if they have one
                    if (r.ActiveMountID > 0)
                    {
                        p = new SendPacket();
                        p.Pack8((byte)15);
                        p.Pack8((byte)16); // Mount/ride pet
                        p.Pack8(1);
                        p.Pack32(r.CharID);
                        p.Pack32(r.ActiveMountID);
                        for (int i = 0; i < 26; i++) p.Pack8(0);
                        src.Send(p);
                        DebugSystem.Write($"[Map.Warp_In] Sent mount {r.ActiveMountID} of {r.CharName} to {src.CharName}");
                    }

                    // Send existing player's battle pet to new arrival (AC 15:4 Visual + AC 15:1 Pet Info + AC 19:4 Following + AC 13:5 Formation + AC 5:8 Sprite Refresh)
                    if (r.ActivePetID > 0)
                    {
                        var rPet = r.PlayerPets?.Values?.FirstOrDefault(x => x.PetID == r.ActivePetID);
                        string rPetName = rPet?.PetName ?? QuestRelated.QuestManager.GetNpcName(r.ActivePetID);

                        // AC 15:4 Map Pet Visual Entity
                        SendPacket rPetMapPkt = new SendPacket();
                        rPetMapPkt.PackArray(new byte[] { 15, 4 });
                        rPetMapPkt.Pack32(r.CharID);
                        rPetMapPkt.Pack32(r.ActivePetID);
                        rPetMapPkt.Pack8(0);
                        rPetMapPkt.Pack8(1);
                        rPetMapPkt.PackString(rPetName);
                        rPetMapPkt.Pack16(0);
                        src.Send(rPetMapPkt);

                        // AC 15:1 Pet Info
                        SendPacket rPetPkt;
                        if (rPet != null)
                        {
                            rPetPkt = QuestRelated.QuestManager.CreatePetPacket(r, rPet.PetID, rPet.Slot, rPet.HP, rPet.MaxHP, rPet.SP, rPet.MaxSP, rPet.Amity, rPet.Level);
                        }
                        else
                        {
                            rPetPkt = QuestRelated.QuestManager.CreatePetPacket(r, r.ActivePetID, 1);
                        }
                        src.Send(rPetPkt);

                        // AC 19:4 Pet Following
                        p = new SendPacket();
                        p.Pack8((byte)19);
                        p.Pack8((byte)4); // Pet battle following
                        p.Pack32(r.CharID); // Owner CharID
                        p.Pack32(r.ActivePetID); // Pet ID
                        src.Send(p);

                        // Send AC 13:5 to attach pet as follower to r
                        SendPacket petFollow = new SendPacket();
                        petFollow.PackArray(new byte[] { 13, 5 });
                        petFollow.Pack32(r.CharID);
                        petFollow.Pack32(r.ActivePetID);
                        src.Send(petFollow);

                        // Send AC 5:8 appearance refresh
                        SendPacket petRefresh = new SendPacket();
                        petRefresh.PackArray(new byte[] { 5, 8 });
                        petRefresh.Pack32(r.CharID);
                        petRefresh.Pack8(0);
                        src.Send(petRefresh);

                        DebugSystem.Write($"[Map.Warp_In] Sent battle pet {r.ActivePetID} (owner: {r.CharName}) to {src.CharName}");
                    }

                    p = new SendPacket();
                    p.Pack8((byte)7);
                    p.Pack32(r.CharID);
                    p.Pack16((ushort)MapID);
                    p.Pack16(r.CurX);
                    p.Pack16(r.CurY);
                    src.Send(p);
                }
            }

            SendMapInfo(src);

            // Synchronize player's own companion pets to themselves and the map
            if (src.PlayerPets != null && src.PlayerPets.Count > 0)
            {
                foreach (var kvp in src.PlayerPets)
                {
                    var pet = kvp.Value;
                    if (pet != null && pet.PetID > 0)
                    {
                        // Send authentic AC 15:1 pet recruit data to owner and map peers
                        SendPacket petPkt = QuestRelated.QuestManager.CreatePetPacket(src, pet.PetID, pet.Slot, pet.HP, pet.MaxHP, pet.SP, pet.MaxSP, pet.Amity, pet.Level);
                        src.Send(petPkt);
                        Broadcast(petPkt, "Ex", src.CharID);
                        QuestRelated.QuestManager.SendPetSkills(src, pet.PetID, pet.Slot);

                        if (pet.IsBattle || src.ActivePetID == pet.PetID || src.ActivePetID == 0)
                        {
                            src.ActivePetID = pet.PetID;
                            pet.IsBattle = true;

                            // AC 19:1 Set battle companion state to owner
                            src.Send(Tools.FromFormat("bbd", 19, 1, pet.PetID));

                            // AC 15:4 Map Pet Visual Entity to owner and map peers
                            SendPacket petMapPkt = new SendPacket();
                            petMapPkt.PackArray(new byte[] { 15, 4 });
                            petMapPkt.Pack32(src.CharID);
                            petMapPkt.Pack32(pet.PetID);
                            petMapPkt.Pack8(0);
                            petMapPkt.Pack8(1);
                            petMapPkt.PackString(pet.PetName ?? QuestRelated.QuestManager.GetNpcName(pet.PetID));
                            petMapPkt.Pack16(0);
                            src.Send(petMapPkt);
                            Broadcast(petMapPkt, "Ex", src.CharID);

                            // AC 19:4 Broadcast battle companion following player to all players on map
                            SendPacket followPkt = new SendPacket();
                            followPkt.Pack8(19);
                            followPkt.Pack8(4);
                            followPkt.Pack32(src.CharID);
                            followPkt.Pack32(pet.PetID);
                            src.Send(followPkt);
                            Broadcast(followPkt, "Ex", src.CharID);

                            // AC 13:5 Broadcast companion follow formation to peers
                            SendPacket petFollow = new SendPacket();
                            petFollow.PackArray(new byte[] { 13, 5 });
                            petFollow.Pack32(src.CharID);
                            petFollow.Pack32(pet.PetID);
                            src.Send(petFollow);
                            Broadcast(petFollow, "Ex", src.CharID);

                            // AC 5:8 Appearance refresh
                            SendPacket petRefresh = new SendPacket();
                            petRefresh.PackArray(new byte[] { 5, 8 });
                            petRefresh.Pack32(src.CharID);
                            petRefresh.Pack8(0);
                            src.Send(petRefresh);
                            Broadcast(petRefresh, "Ex", src.CharID);

                            DebugSystem.Write($"[Map.Warp_In] Dispatched companion '{pet.PetName}' (ID: {pet.PetID}) to {src.CharName} and broadcast following state to peers");
                        }
                    }
                }
            }

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

                    src.Send(Tools.FromFormat("bbbs", 23, 57, 0, "✨ Astrologer Laura gifted you the Space Tent & Remote Control!"));
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

            SendAc12(src, portalID, To, toTent);
            m_playerlist.Remove(src);
        }

        public Game.DataFiles.MapData mapData
        {
            get { return DataBase.GameDataBase.GlobalInstance?.EveDat?.GetMapData(Convert.ToUInt16(this.MapID)); }
        }

        public bool LookupPortal(ushort portalID, int px, int py, out ushort dstMap, out ushort dstX, out ushort dstY)
        {
            dstMap = 0; dstX = 0; dstY = 0;

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
                if ((DateTime.UtcNow - sender.LastTeleportTime).TotalMilliseconds < 1000)
                {
                    DebugSystem.Write($"[Teleport] Portal cooldown active (1s) for {sender.CharName}. Request ignored.");
                    sender.Send(Tools.FromFormat("bb", 20, 8));
                    return false;
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
                            catch (Exception ex) { DebugSystem.Write(new ExceptionData(ex)); }

                            if (Game.Maps.MapManager.Instance != null && (map = Game.Maps.MapManager.Instance.GetMap(dstMap)) != null)
                            {
                                try
                                {
                                    map.Warp_In(teletype, sender, new WarpData() { DstMap = dstMap, DstX_Axis = dstX, DstY_Axis = dstY }, (byte)(portalID & 0xFF));
                                }
                                catch (Exception ex) { DebugSystem.Write(new ExceptionData(ex)); }
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
                                catch (Exception ex) { DebugSystem.Write(new ExceptionData(ex)); }

                                try
                                {
                                    map.Warp_In(teletype, sender, new WarpData() { DstMap = dstMap, DstX_Axis = dstX, DstY_Axis = dstY }, (byte)(portalID & 0xFF));
                                }
                                catch (Exception ex) { DebugSystem.Write(new ExceptionData(ex)); }
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
                    {
                        //WarpInfo f = new WarpInfo();
                        //f.clickID = i.id;
                        //f.mapID = i.mapTo;
                        //f.x = i.x;
                        //f.y = i.y;
                        //globals.gServer.Multipkt_Request(t);
                        //var map = Phase1Warp(t, f, false);
                        //globals.gServer.Queue_Request(t);
                        //map.Phase2Warp(t);
                        //globals.packet.cCharacter.DatatoSend.Enqueue(globals.gServer.GenerateQueuepkt(t));
                        //globals.gServer.SendCombinepkt(t);
                    }
                    break;
                case TeleportType.Quest:
                    {
                        //var exitpoint = mapData.Entry_Points[Entry - 1];
                        //var WarpID = (int)mapData.Events[exitpoint.unknownbytearray1[0] - 1].SubEntry[0].SubEntry[0].dialog2;
                        //globals.gServer.Queue_Request(t);

                        //var map = Phase1Warp(t, mapData.WarpLoc[WarpID - 1]);
                        //globals.packet.cCharacter.DatatoSend.Enqueue(globals.gServer.GenerateQueuepkt(t));
                        //globals.gServer.Queue_Request(t);
                        //map.Phase2Warp(t);
                        //globals.packet.cCharacter.DatatoSend.Enqueue(globals.gServer.GenerateQueuepkt(t));
                    }
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

                        Warp_Out((byte)(portalID & 0xFF), sender, warp, (teletype == TeleportType.Tent));// warp out of map
                        sender.CurX = warp.DstX_Axis;//switch x
                        sender.CurY = warp.DstY_Axis;//switch y
                        Tents[warp.DstMap].Warp_In(TeleportType.Tent, sender, warp);
                    }
                    break;
                #endregion
                case TeleportType.Tool:/*t.inv.RemoveInv((byte)Entry, 1);*/ break;
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
                    npcListPkt.Pack16(npc.CickID);
                    QuestNpc qn = npc as QuestNpc;
                    ushort state = 0x0000;

                    if (qn != null && t.HasRecruitedCompanion(qn.Name, (ushort)qn.TemplateID))
                    {
                        state = 0xFFFF; // Hidden / Recruited Companion
                    }
                    else if (qn != null && (qn.IsStaticNpc() || qn.TemplateID >= 19000))
                    {
                        bool isOpened = qn.IsBroken;
                        if (!isOpened && t.Quests != null && eveData != null)
                        {
                            var ev = eveData.Events?.FirstOrDefault(e => e.clickID == qn.CickID);
                            if (ev != null && ev.SubEntry != null)
                            {
                                foreach (var s in ev.SubEntry)
                                {
                                    if (s.unknownword1 > 0 && t.Quests.TryGetValue(s.unknownword1, out var pq) && pq.State == Game.QuestRelated.QuestState.Completed)
                                    {
                                        isOpened = true;
                                        break;
                                    }
                                }
                            }
                        }
                        state = isOpened ? (ushort)0x0001 : (ushort)0x0000;
                    }
                    else
                    {
                        bool isVisible = Game.QuestRelated.PreEventInterpreter.ShouldNpcBeVisible(t, (ushort)this.MapID, (ushort)npc.CickID);
                        state = isVisible ? (ushort)0x00FF : (ushort)0xFFFF;
                    }

                    npcListPkt.Pack16(state);
                    npcListPkt.Pack16(npc.X);
                    npcListPkt.Pack16(npc.Y);
                    npcListPkt.Pack8(1);
                    npcListPkt.Pack8(0);
                    npcListPkt.Pack32(0);
                }
                tmp.Add(npcListPkt);

                // Hide already recruited companion NPCs and quest-stage actors from player's map view (AC 22:10)
                foreach (var npc in this.NPCs)
                {
                    QuestNpc qn = npc as QuestNpc;
                    if (qn != null)
                    {
                        bool isRecruited = t.HasRecruitedCompanion(qn.Name, (ushort)qn.TemplateID);
                        bool isVisible = Game.QuestRelated.PreEventInterpreter.ShouldNpcBeVisible(t, (ushort)this.MapID, (ushort)npc.CickID);
                        if (isRecruited || (qn.IsBroken && qn.RespawnTime == DateTime.MaxValue) || !isVisible)
                        {
                            tmp.Add(Tools.FromFormat("bbwbb", 22, 10, (ushort)qn.CickID, (byte)0xFF, (byte)0xFF));
                        }
                    }
                }

                // Evaluate native map PreEvents
                Game.QuestRelated.PreEventInterpreter.EvaluateMapPreEvents(t, (ushort)this.MapID);
            }
            #endregion
            #region Send Item
            //if (Items_Dropped.Count > 0)
            //{
            //    int unk = 0;
            //    p = new SendPacket();
            //    p.PackArray(new byte[] { 23, 4 });
            //    for (byte a = 0; a < Items_Dropped.ToList().Count; a++)
            //    {
            //        if (Items_Dropped[a].NonExpirable)
            //        {
            //            p.Pack((byte)3);
            //            p.Pack((byte)1);
            //            unk = Items_Dropped[a].Control;
            //        }

            //        p.Pack16((ushort)a);
            //        p.Pack((ushort)Items_Dropped[a].ItemID);
            //        p.Pack16((ushort)Items_Dropped[a].X);
            //        p.Pack16((ushort)Items_Dropped[a].Y);
            //        p.Pack((uint)unk);
            //    }
            //    t.Send(p);
            //}
            #endregion
            //SendNpcs(t);
            //SendItems(t);
            //SendOpenTents(t);


            foreach (var r in m_playerlist)
            {
                tmp.Add(Tools.FromFormat("bbd", 23, 122, r.CharID));
                tmp.Add(Tools.FromFormat("bbdb", 10, 3, r.CharID, 255));

                if (r.CharID != t.CharID)
                {
                    //r.SendPacket(Tools.FromFormat("bbd", 23, 122, t.CharID));
                    //r.SendPacket(Tools.FromFormat("bbdb", 10, 3, t.CharID, 255));

                    if (r.Emote != 0)
                        tmp.Add(Tools.FromFormat("bbdb", 32, 2, r.CharID, r.Emote));

                    #region Pets in Map
                    //if (t.Pets.BattlePet != null)//to them
                    //{
                    //    SendPacket tmp = new SendPacket();
                    //    tmp.PackArray(new byte[] { 15, 4 });
                    //    tmp.Pack(t.CharID);
                    //    tmp.Pack(t.Pets.BattlePet.ID);
                    //    tmp.Pack((byte)0);
                    //    tmp.Pack((byte)1);
                    //    tmp.PackString(t.Pets.BattlePet.Name);
                    //    tmp.Pack16(0);//weapon
                    //    r.Send(tmp);
                    //}
                    //if (r.Pets.BattlePet != null)//to me
                    //{
                    //    SendPacket tmp = new SendPacket();
                    //    tmp.PackArray(new byte[] { 15, 4 });
                    //    tmp.Pack(r.CharID);
                    //    tmp.Pack(r.Pets.BattlePet.ID);
                    //    tmp.Pack((byte)0);
                    //    tmp.Pack((byte)1);
                    //    tmp.PackString(r.Pets.BattlePet.Name);
                    //    tmp.Pack16(0);//weapon
                    //    t.Send(tmp);
                    //}

                    #endregion
                    #region Riceball
                    //if (characters_in_map[a].riceBall.id > 0)
                    //{
                    //    if (characters_in_map[a].riceBall.active) g.ac5.Send_5(characters_in_map[a].riceBall.id, characters_in_map[a], t);
                    //}
                    //if (t.riceBall.id > 0)
                    //{
                    //    if (t.riceBall.active) g.ac5.Send_5(t.riceBall.id, t, characters_in_map[a]);
                    //}
                    #endregion
                    #region Team
                    //if (t.MyTeam.PartyLeader && t.MyTeam.hasParty && plist[a] != t)
                    //{
                    //    SendPacket fg = t.MyTeam._13_6;
                    //    plist[a].Send(fg);
                    //}
                    //if (plist[a].MyTeam.PartyLeader && plist[a].MyTeam.hasParty)
                    //{
                    //    SendPacket fg = plist[a].MyTeam._13_6;
                    //    t.Send(fg);
                    //}
                    #endregion
                    //if (Player.PlayerID != t.PlayerID)
                    //g.ac23.Send_74(Player.PlayerID, 0, c); //TODO find out what this does
                    #region Pets in Map
                    //AC 15,4 //possibly pet info for players on map with pets

                    //if (plist[a].CharacterState == PlayerState.inBattle)
                    //{
                    //    SendPacket qp = new SendPacket(t);
                    //    qp.PackArray(new byte[]{(11, 4);
                    //    qp.Pack((byte)2);
                    //    qp.Pack(plist[a].CharacterID);
                    //    qp.Pack16(0);
                    //    qp.Pack((byte)0);
                    //    qp.Send();
                    //}
                    #endregion
                    //23_76                    
                }
                tmp.Add(Tools.FromFormat("bbd", 23, 76, r.CharID));

            }
            //39_9
            //SendPacket gh = new SendPacket(g);
            //gh.PackArray(new byte[] { 244, 68, 5, 0, 22, 6, 1, 0, 1, 244, 68, 5, 0, 22, 6, 21, 0, 1, 244, 68, 5, 0, 22, 6, 22, 0, 1, 244, 68, 5, 0, 22, 6, 23, 0, 1, 244, 68, 5, 0, 22, 6, 24, 0, 1, });
            // cServer.Send(gh, t);
            /*tmp = new SendPacket(g);
            tmp.PackArray(new byte[]{(6, 2);
            tmp.Pack((byte)1);
            tmp.SetSize();
            tmp.Player = t;
            tmp.Send();
            for (int a = 0; a < 1; a++)
            {
                gh = new SendPacket(g);
                gh.PackArray(new byte[] { 244, 68, 2, 0, 20, 11, 244, 68, 2, 0, 20, 10 });
                t.DatatoSend.Enqueue(gh);
            }
            for (int a = 0; a < 1; a++)
            {
                gh = new SendPacket(g);
                gh.PackArray(new byte[] { 244, 68, 2, 0, 20, 10 });
                t.DatatoSend.Enqueue(gh);
            }*/
            tmp.Add(Tools.FromFormat("bb", 23, 102));
            tmp.Add(Tools.FromFormat("bb", 20, 8));
            t.LastTeleportTime = DateTime.UtcNow;
            t.Flags.Add(PlayerFlag.InMap); //t.CharacterState = PlayerState.inMap;
            t.Send(new SendPacket(tmp.End()));

            // Personal Client-Side NPC Visibility Sync (Only hides completed/recruited NPCs for this specific player)
            QuestRelated.QuestManager.SyncPerPlayerNpcVisibility(t, (ushort)this.MapID);

            // Sync Guild Insignia
            t.CurGuild?.SendInsignia(t);

            // Sync Active Vehicle
            PlayerRelated.VehicleManager.SyncVehicleOnMapEntry(t);
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
            // Ensure tent exists in dictionary (lazy loading)
            if (p.Tent != null && !Tents.ContainsKey((ushort)ID))
            {
                Tents.TryAdd((ushort)ID, p.Tent);
                DebugSystem.Write(DebugItemType.Error, $"[Map] Lazy-loaded tent {ID} into Tents dictionary for player {p.CharName}");
            }

            WarpData tmp = new WarpData();
            tmp.DstMap = (ushort)ID;
            tmp.DstX_Axis = 460;
            tmp.DstY_Axis = 700;
            Teleport(TeleportType.Tent, p, 0, tmp);
        }

        void SendOpenTents(Player p)
        {
            if (Tents.Count > 0)
            {
                SendPacket tmp = new SendPacket();
                tmp.Pack8((byte)65);
                tmp.Pack8((byte)3);
                Parallel.ForEach(Tents.Values, r =>
                {
                    tmp.Pack32(r.MapID);
                    tmp.Pack16(36002);
                    tmp.Pack32(r.X);
                    tmp.Pack32(r.Y);
                    tmp.Pack8((byte)0);
                    tmp.Pack8((byte)0);
                });
                p.Send(tmp);
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

        void SendAc12(Player target, byte portalID, WarpData To, bool toTent = false)
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
            target.Send(sp);
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
                    var qNpc = npc as Game.Maps.QuestNpc;
                    string nName = qNpc != null ? qNpc.Name : $"NPC_{clickID}";
                    uint nTid = qNpc != null ? qNpc.TemplateID : 0;
                    DebugSystem.Write($"[ProcessInteraction] Found NPC #{clickID} '{nName}' (TID: {nTid}), calling Interact for player {player.CharName}");
                    npc.Interact(player);
                    return true;
                }
                else
                {
                    DebugSystem.Write($"[ProcessInteraction] Auto-Importing Unknown NPC with click_id {clickID}");

                    // Auto-Import using Player's coordinates
                    // Use GameDataBase.GlobalInstance singleton
                    string npcName = $"Imported {clickID}";
                    string npcType = "QuestNpc";
                    ushort npcLv = 1;
                    uint npcHp = 100;
                    byte npcElement = 0;

                    // Try to finding matching template in npc_data
                    try
                    {
                        var template = GameDataBase.GlobalInstance.GetDataTable($"SELECT * FROM npc_data WHERE id={clickID} LIMIT 1");
                        if (template != null && template.Rows.Count > 0)
                        {
                            npcName = template.Rows[0]["name"].ToString();
                            npcLv = Convert.ToUInt16(template.Rows[0]["level"]);
                            npcHp = Convert.ToUInt32(template.Rows[0]["hp"]);
                            npcElement = Convert.ToByte(template.Rows[0]["element"]);
                            DebugSystem.Write($"[ProcessInteraction] Match FOUND for ID {clickID}: {npcName} Lv.{npcLv} HP.{npcHp}");
                        }
                        else
                        {
                            DebugSystem.Write($"[ProcessInteraction] NO MATCH for ID {clickID} in npc_data table. Table might be empty or ID mismatch.");
                        }
                    }
                    catch (Exception ex)
                    {
                        DebugSystem.Write($"[ProcessInteraction] Lookup Error: {ex.Message}");
                    }

                    if (GameDataBase.GlobalInstance != null && GameDataBase.GlobalInstance.AddNPC((int)this.MapID, clickID, npcType, npcName, player.CurX, player.CurY, 0))
                    {
                        var newNpc = new Game.Maps.QuestNpc();
                        newNpc.CickID = clickID;
                        newNpc.X = player.CurX;
                        newNpc.Y = player.CurY;
                        newNpc.Name = npcName;
                        newNpc.Level = npcLv;
                        newNpc.HP = npcHp;
                        newNpc.Element = npcElement;
                        NPCs.Add(newNpc);

                        newNpc.Interact(player);
                        return true;
                    }
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
