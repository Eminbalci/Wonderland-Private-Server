using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Game;
using Game.Code;
using Game.Maps;
using Network;
using Plugin;
using RCLibrary.Core.Networking;

namespace Server
{
    /// <summary>
    /// Handles the Recv and SendPacket Proccesing of all clients
    /// </summary>
    public class WorldServer : MapSystem, WorldServerHost, MapHost
    {
        Thread Mainthrd, Eventthrd, AutoSaveThread, MapTickThread;
        bool killFlag;
        readonly ManualResetEvent mylock;
        //readonly Semaphore ProcessLock,SendLock;
        //public event onWorldEvent WorldEvent;
        //event wOnlineCheck OnlineCheck;
        //event wDCPlayerEvent DCPlayer;

        /// <summary>
        /// List of Players
        /// </summary>
        Queue<Player> QueuedPlayerLogin;


        /// <summary>
        /// Maps loaded into the world
        /// </summary>
        new ConcurrentDictionary<ushort, GameMap> MapList;

        // System.Diagnostics.Stopwatch exptimer = new System.Diagnostics.Stopwatch();
        /// <summary>
        /// Clients that are in the Process of Logging into the Server
        /// </summary>
        //public int Clients_Connected { get { int a = 0; ConnectedPlayers.Values.ToList().ForEach(c => a += c.Values.Count); return a; } }
        ///// <summary>
        ///// Clients playing in the Server
        ///// </summary>
        //public int Clients_inGame { get { int a = 0; ConnectedPlayers.Values.ToList().ForEach(c => a += c.Values.Count(n => n.inGame)); return a; } }

        public WorldServer(PluginManager src) : base(src)
        {
            mylock = new ManualResetEvent(false);
            QueuedPlayerLogin = new Queue<Player>();
            MapList = new ConcurrentDictionary<ushort, GameMap>();
            new MapManager(); // Initialize Singleton
        }

        public void OnLogin(Player client)
        {
            QueuedPlayerLogin.Enqueue(client);
        }

        public void QueueAssistToolClient(Socket client)
        {
            //var p = new AssistTool(ref client);
            //if (ConnectedPlayers.ContainsKey(p.ClientIP))
            //{
            //    if (ConnectedPlayers[p.ClientIP].Tool == null)
            //        ConnectedPlayers[p.ClientIP].Tool = p;
            //    else
            //        p.Disconnect();
            //}
            //else
            //{
            //    Client tmp = new Client();
            //    tmp.Tool = p;
            //    ConnectedPlayers.TryAdd(p.ClientIP, tmp);     
            //    mylock.Set();          
            //}

        }

        public void Initialize()
        {
            killFlag = false;
            Mainthrd = new Thread(new ThreadStart(MainLoop));
            Mainthrd.Name = "World Manager Main Thread";
            Mainthrd.Init();
            Eventthrd = new Thread(new ThreadStart(Eventwrk));
            Eventthrd.Name = "World Manager Event Thread";
            Eventthrd.Init();
            AutoSaveThread = new Thread(new ThreadStart(AutoSaveLoop));
            AutoSaveThread.Name = "Auto-Save Thread";
            AutoSaveThread.Init();
            DebugSystem.Write("[WorldServer] Auto-save thread started (saves every 1 second)");
            MapTickThread = new Thread(new ThreadStart(MapTickLoop));
            MapTickThread.Name = "Map & NPC Tick Thread";
            MapTickThread.Init();
            DebugSystem.Write("[WorldServer] Map & NPC Tick Thread started (500ms cycle)");
        }

        public void Kill()
        {
            killFlag = true;
            mylock.Set();
            while (Mainthrd != null && Mainthrd.IsAlive) { Thread.Sleep(1); }
            Mainthrd = null;
            while (Eventthrd != null && Eventthrd.IsAlive) { Thread.Sleep(1); }
            Eventthrd = null;
            while (AutoSaveThread != null && AutoSaveThread.IsAlive) { Thread.Sleep(1); }
            AutoSaveThread = null;
            while (MapTickThread != null && MapTickThread.IsAlive) { Thread.Sleep(1); }
            MapTickThread = null;
        }

        void MainLoop()
        {
            Queue<Player> DeadClients = new Queue<Player>();

            do
            {
                #region Queued Clients


                if (QueuedPlayerLogin.Count > 0)
                {
                    Player src = QueuedPlayerLogin.Dequeue();
                    DebugSystem.Write($"[WorldServer] Dequeued player: {src.UserAcc?.UserName ?? "Unknown"}. Disconnected? {src.isDisconnected()}");

                    if (!src.isDisconnected())
                    {
                        try
                        {
                            DebugSystem.Write($"[WorldServer] Processing login queue for client...");
                            CommenceLogin(src);
                        }
                        catch (Exception ex)
                        {
                            DebugSystem.Write($"[WorldServer] Critical Error in CommenceLogin: {ex.Message}\n{ex.StackTrace}");
                        }
                    }
                    else
                    {
                        DebugSystem.Write($"[WorldServer] Player dropped because isDisconnected() is TRUE.");
                    }
                }
                #endregion
                Thread.Sleep(2);
            }
            while (!killFlag);

            //Parallel.ForEach<Player>(Players, new Action<Player>(c =>
            //        {
            //            c.Disconnect();
            //            onPlayerDisconnected(ref c);
            //        }));



        }



        #region Threads
        //void Mainwrk()// processes connected clients
        //{
        //    exptimer.Start();
        //    do
        //    {
        //        #region Connected Clients
        //        foreach (var c in Players)
        //        {
        //            try
        //            {
        //                Player j;
        //                if (!c.inGame && c.Elapsed > new TimeSpan(0, (c.inGame) ? 35 : 5, 0))
        //                {
        //                    Players.TryRemove(c.ID, out j);

        //                    DebugSystem.Write(String.Format("Client {0} timed out at Login.", j.ClientIP + ":" + j.ClientPort));
        //                    SendPacket sp = new SendPacket(true);// PSENDPACKET PackSend = new SENDPACKET;
        //                    sp.Pack(new byte[] { 1, 7 });//PackSend->Header(63,2);
        //                    j.Send(sp);
        //                    j.BlockSave = true;
        //                    j.Disconnect();
        //                    continue;
        //                }
        //                else if (!c.isAlive)
        //                {
        //                    Players.TryRemove(c.ID, out j);
        //                    onPlayerDisconnected(ref j);
        //                }
        //            }
        //            catch (Exception ex) { DebugSystem.Write(ex); }
        //        }
        //        #endregion


        //        Thread.Sleep(120);

        //        try
        //        {
        //            if (exptimer.Elapsed >= new TimeSpan(0, 1, 20))
        //            {
        //                foreach (var m in MapList.Values.ToList())
        //                    try
        //                    {
        //                        foreach (var p in m.Players.Values.ToList())
        //                            p.CurExp += (int)((p.Level * new Random().Next(1, 700)) * 2.2);
        //                    }
        //                    catch { }
        //                exptimer.Restart();
        //            }
        //        }
        //        catch { }
        //    }
        //    while (!killFlag);

        //    foreach (var p in Players.Values)
        //    {
        //        p.Disconnect();
        //        //onPlayerDisconnected(p);
        //    }

        //    MapList.Clear();
        //}
        void MapTickLoop()
        {
            while (!killFlag)
            {
                try
                {
                    if (MapManager.Instance != null)
                    {
                        var maps = MapManager.Instance.ActiveMaps.ToList();
                        foreach (var map in maps)
                        {
                            try
                            {
                                map.Process();
                            }
                            catch (Exception ex)
                            {
                                DebugSystem.Write($"[WorldServer] Error processing map {map.MapID}: {ex.Message}");
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    DebugSystem.Write($"[WorldServer] Error in MapTickLoop: {ex.Message}");
                }

                Thread.Sleep(500);
            }
        }

        void Mapwrk()// processes tick
        {
            do
            {

                //foreach (var map in MapList.Values.ToList())
                //    map.UpdateMap();
                Thread.Sleep(2);
            }
            while (!killFlag);
        }
        void Eventwrk()// processes connected clients
        {
            do
            {
                Thread.Sleep(120);
            }
            while (!killFlag);
        }
        #endregion

        //public Player GetPlayer(uint ID)
        //{

        //    foreach (var t in Players.Values)
        //            if (t.ID == ID)
        //                return t;

        //    return null; 
        //}
        /// <summary>
        /// Checks by dataBaseID if Online
        /// </summary>
        /// <param CharacterName="ID"></param>
        /// <returns></returns>
        public bool isOnline(uint Databaseid)
        {
            //foreach (var t in Players)
            //        if (t.DataBaseID == Databaseid && t.State != PlayerState.Connected_LoginWindow)
            //            return true;

            return false;
        }
        /// <summary>
        /// Disconnects a player
        /// </summary>
        /// <param CharacterName="id"></param>
        public void DisconnectPlayer(uint Databaseid)
        {
            //foreach (var r in Players)
            //        if (r.DataBaseID == Databaseid) { r.Disconnect(); return; }
        }

        /// <summary>
        /// Used via GM command
        /// </summary>
        /// <param CharacterName="portalID"></param>
        /// <param CharacterName="map"></param>
        /// <param CharacterName="target"></param>
        /// <param CharacterName="arg"></param>
        //public void Teleport(byte portalID, WarpData map, Player target, string arg)
        //{
        //    var mapid = target.CurrentMap.MapID;
        //    Player r = target;
        //    if (MapList.ContainsKey(map.DstMap))
        //        try
        //        {
        //            target.CurrentMap.onWarp_Out(portalID, ref target, map);
        //            MapList[map.DstMap].onWarp_In(portalID, ref r, map);
        //            switch (arg)
        //            {
        //                case "MAPGM":
        //                    {
        //                        foreach (var y in Clients.Values.Where(c => c.CurrentMap.MapID == mapid && c.GM))
        //                        {
        //                            Player u = y;
        //                            MapList[mapid].onWarp_Out(portalID, ref u, map);
        //                            MapList[map.DstMap].onWarp_In(portalID, ref u, map);
        //                        }
        //                    } break;
        //            }
        //        }
        //        catch (Exception d) { DLogger.ErrorLog(d); }
        //    else
        //    {
        //        try
        //        {
        //            foreach (var y in (from c in Assembly.GetExecutingAssembly().GetTypes()
        //                               where c.IsClass && c.IsSubclassOf(typeof(MapPlugin))
        //                               select c))
        //            {
        //                var m = (Activator.CreateInstance(y) as MapPlugin);
        //                if (m.MapID == map.DstMap)
        //                {
        //                    target.CurrentMap.onWarp_Out(portalID, ref target, map);
        //                    m.onWarp_In(portalID, ref r, map);
        //                    switch (arg)
        //                    {
        //                        case "MAPGM":
        //                            {
        //                                foreach (var p in Clients.Values.Where(c => c.CurrentMap.MapID == mapid && c.GM))
        //                                {
        //                                    Player u = p;
        //                                    MapList[mapid].onWarp_Out(portalID, ref u, map);
        //                                    m.onWarp_In(portalID, ref u, map);
        //                                }
        //                            } break;
        //                    }
        //                    MapList.TryAdd(m.MapID, m);
        //                }
        //            }
        //        }
        //        catch (Exception e) { DLogger.ErrorLog(e); }
        //    }
        //}

        public new GameMap GetMap(ushort ID)
        {
            GameMap tmp = null;

            if (MapList.Values.Count(c => c.MapID == ID) == 0)
            {
                if ((tmp = base.GetMap(ID)) != null)
                {
                    //cGlobal.gGameDataBase.SetupMap(ref tmp);
                    if (MapList.TryAdd((ushort)tmp.MapID, tmp))
                        DebugSystem.Write(DebugItemType.Info_Heavy, "Loaded Map {0}", tmp.MapID);
                    return tmp;
                }
                else
                    return null;
            }
            else
                return MapList.Values.Single(c => c.MapID == ID);
        }

        //public bool onTelePort(TeleportType teletype, byte portalID, WarpData map, Player target)
        //{

        //    if (MapList.Values.Count(c => c.MapID == map.DstMap) == 0)
        //    {
        //        //Create Map
        //        GameMap tmp = null;
        //        if ((tmp = GetMap(map.DstMap)) != null)
        //        {
        //            //cGlobal.gGameDataBase.SetupMap(ref tmp);
        //            if (MapList.TryAdd((ushort)tmp.MapID, tmp))
        //                DebugSystem.Write(DebugItemType.Info_Heavy, "Loaded Map {0}", tmp.MapID);
        //        }
        //        else
        //            return false;
        //    }

        //    MapList.Values.Single(c => c.MapID == map.DstMap).Teleport(teletype, target, portalID, map);
        //    return true;
        //}

        /// <summary>
        /// Broadcasts a packet to all
        /// </summary>
        /// <param CharacterName="pkt"></param>
        public void Broadcast(SendPacket pkt)
        {
            //foreach (var p in Players)
            //    p.Send(pkt);
        }
        /// <summary>
        /// Broadcast's a direct packet or a packet that will ignore the selcected
        /// </summary>
        /// <param CharacterName="pkt"></param>
        /// <param CharacterName="directTo">Person to send to/ or avoid</param>
        /// <param CharacterName="avoid">Avoid the Person and send to everyone else</param>
        public void Broadcast(SendPacket pkt, uint? directTo = null, bool exclude = false)
        {
            //foreach (var p in Players)
            //        if (p.ID == directTo && exclude)
            //        continue;
            //        else if (p.ID == directTo && !exclude)
            //    {
            //        p.Send(pkt); return;
            //    }
            //        else if (exclude && p.ID != directTo)
            //        p.Send(pkt);
        }

        //public override void onPlayerDisconnected(ref Player src)
        //{
        //    lock (mylock)
        //    {
        //        //base.onPlayerDisconnected(ref src);
        //        ////if (WorldEvent != null) WorldEvent(c, WorldEventType.PlayerLogoff);
        //        ////dc socket
        //        //src.Disconnect();

        //        ////unlock CharacterName
        //        //cGlobal.gCharacterDataBase.unLockName(src.CharacterName);
        //        ////send dc packet
        //        //SendPacket bye = new SendPacket();
        //        //bye.Pack(new byte[] { 1, 1 });
        //        //bye.Pack(src.ID);
        //        //BroadcastTo(bye, src.ID, true);
        //        ////save data
        //        //if (cGlobal.gCharacterDataBase.WritePlayer(src.ID, src))
        //        //    DebugSystem.Write(src.UserName + " Info has been Fully Saved");

        //        //DebugSystem.Write(String.Format("Client {0} {1} Disconnected.", src.ClientIP + ":" + src.ClientPort, src.UserName));
        //    }
        //}

        //public void SendCurrentPlayers(Player to)
        //{
        //    //SendPacket p = new SendPacket();
        //    //foreach (var y in (from c in Assembly.GetExecutingAssembly().GetTypes()
        //    //                   where c.IsClass && c.IsSubclassOf(typeof(Bots.GmBot))
        //    //                   select c))
        //    //{
        //    //    var c = (Activator.CreateInstance(y) as Bots.GmBot);
        //    //    p = new SendPacket();
        //    //    p.Pack(new byte[] { 4 });
        //    //    p.Pack(c.ID);
        //    //    p.Pack((byte)c.Body); //body style
        //    //    p.Pack((byte)c.Element); //element
        //    //    p.Pack(c.Level); //level
        //    //    p.Pack((c.CurrentMap == null) ? c.LoginMap : c.CurrentMap.MapID); //map id
        //    //    p.Pack(c.X); //x
        //    //    p.Pack(c.Y); //y
        //    //    p.Pack(0); p.Pack(c.Head); p.Pack(0);
        //    //    p.Pack(c.HairColor);
        //    //    p.Pack(c.SkinColor);
        //    //    p.Pack(c.ClothingColor);
        //    //    p.Pack(c.EyeColor);
        //    //    p.Pack(c.WornCount);//clothesAmmt); // ammt of clothes
        //    //    p.Pack(c.Worn_Equips);
        //    //    p.Pack(0); p.Pack(0); //??
        //    //    p.Pack(c.Reborn); //is rebirth
        //    //    p.Pack((byte)c.Job); //rb class
        //    //    p.Pack(c.CharacterName);//(BYTE*)c.CharacterName,c.nameLen); //CharacterName
        //    //    p.Pack(c.Nickname);//(BYTE*)c.nick,c.nickLen); //nickname
        //    //    p.Pack(255); //??
        //    //    to.Send(p);
        //    //}
        //    //foreach (var d in Players.Where(c => c.inGame))
        //    //{
        //    //    Character c = d;
        //    //    p = new SendPacket();
        //    //    p.Pack(new byte[] { 4 });
        //    //    p.Pack(d.ID);
        //    //    p.Pack((byte)c.Body); //body style
        //    //    p.Pack((byte)c.Element); //element
        //    //    p.Pack(c.Level); //level
        //    //    p.Pack(c.CurrentMap.MapID); //map id
        //    //    p.Pack(c.X); //x
        //    //    p.Pack(c.Y); //y
        //    //    p.Pack(0); p.Pack(c.Head); p.Pack(0);
        //    //    p.Pack(c.HairColor);
        //    //    p.Pack(c.SkinColor);
        //    //    p.Pack(c.ClothingColor);
        //    //    p.Pack(c.EyeColor);
        //    //    p.Pack(c.WornCount);//clothesAmmt); // ammt of clothes
        //    //    p.Pack(c.Worn_Equips);
        //    //    p.Pack(0); p.Pack(0); //??
        //    //    p.Pack(c.Reborn); //is rebirth
        //    //    p.Pack((byte)c.Job); //rb class
        //    //    p.Pack(c.CharacterName);//(BYTE*)c.CharacterName,c.nameLen); //CharacterName
        //    //    p.Pack(c.Nickname);//(BYTE*)c.nick,c.nickLen); //nickname
        //    //    p.Pack(255); //??
        //    //    to.Send(p);
        //    //    to.onPlayerLogin(c.ID);
        //    //    d.onPlayerLogin(to.ID);
        //    //}
        //}    

        public void CommenceLogin(Player src)
        {
            DebugSystem.Write("[WorldServer] CommenceLogin started.");

            src.Flags.Add(PlayerFlag.Logging_into_Map);

            src.Send(Tools.FromFormat("bb", 20, 8));
            src.Send(Tools.FromFormat("bbbw", 24, 5, 183, 0));
            src.Send(Tools.FromFormat("bbbw", 24, 5, 53, 0));
            src.Send(Tools.FromFormat("bbbw", 24, 5, 52, 0));
            src.Send(Tools.FromFormat("bbbw", 24, 5, 54, 0));
            src.Send(Tools.FromFormat("bbbswb", 70, 1, 23, "Something", 194, 0));
            src.Send(Tools.FromFormat("bbb", 20, 33, 0));
            Thread.Sleep(5);
            src.Send(Tools.FromFormat("bbb", 14, 13, 3));
            src.Send(Tools.FromFormat("bbw", 75, 8, 0));
            src.Send(new SendPacket(new byte[] { 244, 68, 41, 0, 104, 1, 1, 0, 12, 44, 137, 1, 45, 137, 1, 25, 134, 1, 24, 134, 1, 22, 134, 1, 23, 134, 1, 76, 133, 1, 99, 133, 1, 100, 133, 1, 41, 133, 1, 91, 133, 1, 88, 133, 1 }));

            //------Player Base Info------------------
            DebugSystem.Write("[WorldServer] Loading Final Data...");
            cGlobal.gGameDataBase.LoadFinalData(src);
            src.SendCharacterData();
            cGlobal.gCharacterDataBase.OnCharacterJoin(src);
            src.Disconnected += cGlobal.gCharacterDataBase.OnCharacterLeave;

            // Populate PlayerSkills list in memory (no packets yet — must precede SendAllSkills below)
            Game.SkillRelated.SkillManager.InitializePlayerSkillsNoSend(src);

            // Load player quests from database
            Game.QuestRelated.QuestManager.LoadPlayerQuests(src);

            // Inventory, equipment, gold, settings (before map teleport)
            src.Send(new SendPacket(src.Inv.GetAC23_5()));
            src.Send(new SendPacket(src._23_11Data));
            src.Send(Tools.FromFormat("bbd", 26, 4, src.Gold));
            src.Send(new SendPacket(src.Settings.ToArray()));

            //---------Map Teleport---------------------------------------------------
            GameMap target = MapManager.Instance.GetMap(src.LoginMap);
            if (target == null)
            {
                var ex = new Exception("Map " + src.LoginMap + " not found for player " + src.CharName);
                DebugSystem.Write(new ExceptionData(ex));
                src.Disconnect();
                throw ex;
            }
            DebugSystem.Write($"[WorldServer] Teleporting {src.CharName} to Map {src.LoginMap} (instance: {target.GetHashCode()})");
            target.Teleport(TeleportType.Login, src, 0, new WarpData() { DstMap = src.LoginMap, DstX_Axis = src.CurX, DstY_Axis = src.CurY });

            src.Send(Tools.FromFormat("bbb", 5, 15, 0));
            src.Send(Tools.FromFormat("bbw", 62, 53, 2));
            src.Send(Tools.FromFormat("bbb", 5, 21, src.Slot));

            // AC 5:11 (Unlock Skill) + AC 8:1 stat 110 (Skill Grade) for each learned skill
            // These packets populate the skill book window. Sent BEFORE AC 5:3 below.
            Game.SkillRelated.SkillManager.SendAllSkills(src);

            // Send quest journal
            Game.QuestRelated.QuestManager.SendQuestJournal(src);

            src.Send(Tools.FromFormat("bbb", 5, 14, 2));
            src.Send(Tools.FromFormat("bbb", 5, 16, 0));
            src.Send(Tools.FromFormat("bbbl", 23, 140, 3, DateTime.Now.ToOADate()));
            src.Send(Tools.FromFormat("bbbl", 25, 44, 2, DateTime.Now.ToOADate()));
            src.Send(Tools.FromFormat("bbb", 23, 160, 3));
            src.Send(Tools.FromFormat("bbb", 75, 7, 1));
            src.Send(Tools.FromFormat("bbbs", 23, 57, 0, "Welcome to the  WLO 4 EVER Community Server :! Enjoy !!"));
            src.Send(Tools.FromFormat("bbb", 69, 1, 71));
            src.Send(Tools.FromFormat("bbb", 20, 60, 1));
            src.Send(new SendPacket(new byte[] { 244, 68, 13, 0, 66, 1, 001, 012, 043, 000, 000, 000, 000, 000, 000, 000, 000 }));

            // Clear hotbar / quickbar slots (AC 5:24)
            for (byte a = 1; a < 11; a++)
                src.Send(Tools.FromFormat("bbbw", 5, 24, a, 0));

            src.Send(Tools.FromFormat("bbbw", 23, 162, 2, 0));
            src.Send(Tools.FromFormat("bbd", 26, 10, 0));
            src.Send(Tools.FromFormat("bbw", 23, 204, 1));
            src.Send(Tools.FromFormat("bbbbd", 23, 208, 2, 3, 0));
            src.Send(Tools.FromFormat("bbbbd", 23, 208, 2, 4, 0));
            src.Send(Tools.FromFormat("bb", 1, 11));
            src.Send(Tools.FromFormat("bbbbbb", 15, 19, 4, 6, 9, 94));
            // Authentic Item Mall Login Initialization sequence:
            // S->C A=54 B=201: item mall catalog (dynamic, from ItemMallManager)
            Game.PlayerRelated.ItemMallManager.SendCatalog(src);

            // 3. AC 35 Sub 11
            src.Send(Tools.FromFormat("bb", 35, 11));

            // 4. AC 35 Sub 12 (CharID + 00)
            SendPacket mallUser = new SendPacket();
            mallUser.Pack8(35);
            mallUser.Pack8(12);
            mallUser.Pack32(src.CharID);
            mallUser.Pack8(0);
            src.Send(mallUser);

            src.Send(Tools.FromFormat("bbbbbb", 90, 1, 0, 2, 2, 3));

            // AC 5:3 sent LAST — matching Python server (send_5_3_login at very end of login).
            // Sending it early causes the client to reset its skill state, discarding all prior AC 5:11 packets.
            src.Send_5_3();
            src.Send(Tools.FromFormat("bb", 5, 4));
            src.Send8_1(false);

            src.Flags.Add(PlayerFlag.InMap);

            DebugSystem.Write("[WorldServer] Sending Online Characters (after spawn)...");
            cGlobal.gCharacterDataBase.SendOnlineCharacters(src);
            DebugSystem.Write($"[WorldServer] Broadcasting new player {src.CharName} to all online players...");
            cGlobal.gCharacterDataBase.BroadcastNewPlayer(src);
        }



        void AutoSaveLoop()
        {
            int saveCounter = 0;
            do
            {
                try
                {
                    // Save all online players every second
                    var onlinePlayers = cGlobal.gCharacterDataBase.GetOnlinePlayers();
                    if (onlinePlayers != null && onlinePlayers.Count > 0)
                    {
                        saveCounter++;
                        foreach (var player in onlinePlayers)
                        {
                            try
                            {
                                cGlobal.gCharacterDataBase.WritePlayer(player.CharID, player);
                                if (player.UserAccount != null && player.UserAccount.DataBaseID != 0)
                                {
                                    cGlobal.gUserDataBase?.SetIMPoints(player.UserAccount.DataBaseID, player.UserAccount.IM);
                                }
                            }
                            catch (Exception ex)
                            {
                                DebugSystem.Write($"[AutoSave] Error saving player {player.CharName}: {ex.Message}");
                            }
                        }

                        // Log every 60 seconds (once per minute)
                        if (saveCounter % 60 == 0)
                        {
                            DebugSystem.Write($"[AutoSave] Saved {onlinePlayers.Count} online players (total saves: {saveCounter})");
                        }
                    }
                }
                catch (Exception ex)
                {
                    DebugSystem.Write($"[AutoSave] Critical error in auto-save loop: {ex.Message}");
                }

                Thread.Sleep(1000); // 1 second
            }
            while (!killFlag);

            DebugSystem.Write("[AutoSave] Auto-save thread stopped");
        }
    }
}
