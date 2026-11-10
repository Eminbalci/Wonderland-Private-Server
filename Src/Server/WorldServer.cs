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
    /// Handles world server lifecycle, player login processing, map synchronization,
    /// periodic autosaves, and network broadcast operations.
    /// </summary>
    public class WorldServer : MapSystem, WorldServerHost, MapHost
    {
        private Thread Mainthrd, Eventthrd, AutoSaveThread, MapTickThread;
        private volatile bool killFlag;
        private readonly ManualResetEvent mylock;

        /// <summary>
        /// Thread-safe queue of incoming players pending world login processing.
        /// </summary>
        private readonly ConcurrentQueue<Player> QueuedPlayerLogin;

        /// <summary>
        /// Underlying concurrent map dictionary synchronized with MapManager.Instance.ActiveMaps.
        /// </summary>
        private readonly ConcurrentDictionary<ushort, GameMap> _mapList;

        /// <summary>
        /// Current state of the world server.
        /// </summary>
        public WorldServerState State { get; private set; } = WorldServerState.Stopped;

        /// <summary>
        /// Maps loaded into the world, synchronized with MapManager.Instance.ActiveMaps.
        /// </summary>
        public new ConcurrentDictionary<ushort, GameMap> MapList
        {
            get
            {
                SyncMapList();
                return _mapList;
            }
        }

        public WorldServer(PluginManager src) : base(src)
        {
            mylock = new ManualResetEvent(false);
            QueuedPlayerLogin = new ConcurrentQueue<Player>();
            _mapList = new ConcurrentDictionary<ushort, GameMap>();
            base.MapList = _mapList;

            new MapManager(); // Initialize Singleton

            Game.PlayerRelated.Friendlist.IsPlayerOnlineHandler = (charId) => isOnline(charId);
        }

        #region Login Processing
        /// <summary>
        /// Queues a newly authenticated player for login initialization into the world.
        /// </summary>
        public void OnLogin(Player client)
        {
            if (client == null) return;
            QueuedPlayerLogin.Enqueue(client);
            mylock.Set();
        }

        public void QueueAssistToolClient(Socket client)
        {
            // Reserved for future assistant tooling integrations
        }

        private void MainLoop()
        {
            do
            {
                #region Queued Clients
                while (QueuedPlayerLogin.TryDequeue(out Player src))
                {
                    if (src == null) continue;

                    DebugSystem.Write($"[WorldServer] Dequeued player: {src.UserAcc?.UserName ?? "Unknown"}. Disconnected? {src.isDisconnected()}");

                    if (!src.isDisconnected())
                    {
                        try
                        {
                            DebugSystem.Write("[WorldServer] Processing login queue for client...");
                            CommenceLogin(src);
                        }
                        catch (Exception ex)
                        {
                            DebugSystem.Write($"[WorldServer] Critical Error in CommenceLogin: {ex.Message}\n{ex.StackTrace}");
                        }
                    }
                    else
                    {
                        DebugSystem.Write("[WorldServer] Player dropped because isDisconnected() is TRUE.");
                    }
                }
                #endregion

                Thread.Sleep((int)WorldTickRate.Realtime);
            }
            while (!killFlag);
        }

        /// <summary>
        /// Finalizes character loading, sends initial sync packets, delivers starter packs/outfits,
        /// and teleports player to their designated world map.
        /// </summary>
        public void CommenceLogin(Player src)
        {
            if (src == null) return;

            DebugSystem.Write("[WorldServer] CommenceLogin started.");

            cGlobal.gCharacterDataBase?.OnCharacterJoin(src);
            src.Disconnected += cGlobal.gCharacterDataBase.OnCharacterLeave;
            src.Disconnected += (s) => Network.ActionCodes.AC14.NotifyFriendsStatus(s, false);
            src.Disconnected += (s) => Game.PlayerRelated.GuildManager.HandlePlayerLogout(s);

            src.Flags.Add(PlayerFlag.Logging_into_Map);

            src.Send(Tools.FromFormat("bb", 20, 8));
            src.Send(Tools.FromFormat("bbbw", 24, 5, 183, 0));
            src.Send(Tools.FromFormat("bbbw", 24, 5, 53, 0));
            src.Send(Tools.FromFormat("bbbw", 24, 5, 52, 0));
            src.Send(Tools.FromFormat("bbbw", 24, 5, 54, 0));
            src.Send(Tools.FromFormat("bbb", 20, 33, 0));
            src.Send(Tools.FromFormat("bbb", 14, 13, 3));
            src.Send(Tools.FromFormat("bbw", 75, 8, 0));
            src.Send(new SendPacket(new byte[] { 244, 68, 41, 0, 104, 1, 1, 0, 12, 44, 137, 1, 45, 137, 1, 25, 134, 1, 24, 134, 1, 22, 134, 1, 23, 134, 1, 76, 133, 1, 99, 133, 1, 100, 133, 1, 41, 133, 1, 91, 133, 1, 88, 133, 1 }));

            //------Player Base Info------------------
            DebugSystem.Write("[WorldServer] Loading Final Data...");
            cGlobal.gGameDataBase?.LoadFinalData(src);
            src.SendCharacterData();
            src.Send(Tools.FromFormat("bbdw", 5, 5, src.CharID, (ushort)0)); // Ensure any shapeshift morph is cleared on login
            Network.ActionCodes.AC14.NotifyFriendsStatus(src, true);

            // Populate PlayerSkills list in memory (no packets yet — must precede SendAllSkills below)
            Game.SkillRelated.SkillManager.InitializePlayerSkillsNoSend(src);
            Game.SkillRelated.SkillManager.CheckAndUnlockProgressionSkillsNoSend(src);

            // Load player quests from database
            Game.QuestRelated.QuestManager.LoadPlayerQuests(src);
            // Characters reborn before this fix lack the Job Palace completion mark.
            Game.PlayerRelated.RebornManager.EnsureJobPalaceCompletion(src, false);
            cGlobal.gCharacterDataBase?.LoadMonsterBook(src);
            Game.PlayerRelated.NotebookManager.SendMonsterBook(src);

            // Starter item pack fallback delivery for level 1 players without starter items
            if ((src.Eqs?.Level ?? 1) <= 1 && !Game.PlayerRelated.StarterPackManager.HasAnyStarterItem(src))
            {
                DebugSystem.Write($"[WorldServer] Level 1 player {src.CharName} is missing starter items. Delivering starter item pack fallback...");
                Game.PlayerRelated.StarterPackManager.DeliverToPlayer(src, sendData: false);
                try { cGlobal.gCharacterDataBase?.WritePlayer(src.CharID, src); } catch { }
            }

            // Starter beginner outfit fallback for level 1 characters without any equipment worn
            if ((src.Eqs?.Level ?? 1) <= 1 && (src.WornCount == 0))
            {
                DebugSystem.Write($"[WorldServer] Level 1 player {src.CharName} has no equipment worn. Re-equipping beginner outfit...");
                src.SetBeginnerOutfit();
                try { cGlobal.gCharacterDataBase?.WritePlayer(src.CharID, src); } catch { }
            }

            // 1. AC 5:3 Base Stats and Learned Skills (must precede map teleport)
            src.Send_5_3();
            src.Send8_1(false);

            // 2. Inventory, equipment, gold, settings (before map teleport)
            src.Send(new SendPacket(src.Inv.GetAC23_5()));
            src.Send(new SendPacket(src._23_11Data));
            src.Send(Tools.FromFormat("bbd", 26, 4, src.Gold));
            src.Send(new SendPacket(src.Settings.ToArray()));

            // AC5:3 above already restores skill grades/EXP, including the character stunt.
            // Replaying learned/grade packets here triggers login banners and sounds.
            // Keep the final refresh; new skills still use the incremental unlock path.
            src.Send(Tools.FromFormat("bb", 5, 4));

            //---------Map Teleport---------------------------------------------------
            GameMap target = MapManager.Instance?.GetMap(src.LoginMap) ?? GetMap(src.LoginMap);
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
            src.SendRecordPointStatus();
            src.Send(Game.PlayerRelated.ItemCardManager.BuildIndex());
            Game.PlayerRelated.TeleportScrollManager.SendAllPoints(src);
            // Send this player's friend list and refresh online friends so they see him online.
            Network.ActionCodes.AC14.NotifyFriendsStatus(src, true);

            src.Send(Tools.FromFormat("bbb", 5, 14, 2));
            src.Send(Tools.FromFormat("bbbl", 23, 140, 3, DateTime.Now.ToOADate()));
            src.Send(Tools.FromFormat("bbbl", 25, 44, 2, DateTime.Now.ToOADate()));
            src.Send(Tools.FromFormat("bbb", 23, 160, 3));
            src.Send(Tools.FromFormat("bbb", 75, 7, 1));
            // Clear hotbar / quickbar slots (AC 5:24)
            for (byte a = 1; a < 11; a++)
                src.Send(Tools.FromFormat("bbbw", 5, 24, a, 0));

            src.Send(Tools.FromFormat("bbbw", 23, 162, 2, 0));
            src.Send(Tools.FromFormat("bbd", 26, 10, 0));
            src.Send(Tools.FromFormat("bbw", 23, 204, 1));
            src.Send(Tools.FromFormat("bbbbd", 23, 208, 2, 3, 0));
            src.Send(Tools.FromFormat("bbbbd", 23, 208, 2, 4, 0));
            src.Send(Tools.FromFormat("bb", 1, 11));
            // Rebuild earned story constellations from durable quest completion flags.
            Game.QuestRelated.QuestManager.SendStoryConstellations(src);

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

            // Dispatches authentic Item Mall initial synchronization sequence (AC 75:1, AC 75:10, AC 75:8, AC 75:7, AC 75:3)
            Game.PlayerRelated.ItemMallManager.SendInitialMallSync(src);

            src.Flags.Add(PlayerFlag.InMap);
        }
        #endregion

        #region Lifecycle
        public void Initialize()
        {
            killFlag = false;
            State = WorldServerState.Starting;

            Mainthrd = new Thread(new ThreadStart(MainLoop))
            {
                Name = "World Manager Main Thread",
                IsBackground = true
            };
            Mainthrd.Init();

            Eventthrd = new Thread(new ThreadStart(Eventwrk))
            {
                Name = "World Manager Event Thread",
                IsBackground = true
            };
            Eventthrd.Init();

            AutoSaveThread = new Thread(new ThreadStart(AutoSaveLoop))
            {
                Name = "Auto-Save Thread",
                IsBackground = true
            };
            AutoSaveThread.Init();
            DebugSystem.Write("[WorldServer] Auto-save thread started (saves every 1 second)");

            MapTickThread = new Thread(new ThreadStart(MapTickLoop))
            {
                Name = "Map & NPC Tick Thread",
                IsBackground = true
            };
            MapTickThread.Init();
            DebugSystem.Write("[WorldServer] Map & NPC Tick Thread started (500ms cycle)");

            // Initialize tent manufacturing / crafting timer system
            Network.ActionCodes.AC64.Init();

            // Restore persistent custom stalls from database (SQLite / MySQL)
            try
            {
                Game.Experimental.ExperimentalFeaturesManager.Instance.PlayerStallBazaar?.InitializeDatabase();
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[WorldServer] Error initializing custom stalls: {ex.Message}");
            }

            State = WorldServerState.Running;
        }

        public void Kill()
        {
            State = WorldServerState.Stopping;
            killFlag = true;
            mylock.Set();

            if (Mainthrd != null && Mainthrd.IsAlive) { Mainthrd.Join(1000); Mainthrd = null; }
            if (Eventthrd != null && Eventthrd.IsAlive) { Eventthrd.Join(1000); Eventthrd = null; }
            if (AutoSaveThread != null && AutoSaveThread.IsAlive) { AutoSaveThread.Join(1000); AutoSaveThread = null; }
            if (MapTickThread != null && MapTickThread.IsAlive) { MapTickThread.Join(1000); MapTickThread = null; }

            State = WorldServerState.Stopped;
        }
        #endregion

        #region Background Threads
        private void MapTickLoop()
        {
            while (!killFlag)
            {
                try
                {
                    SyncMapList();

                    var maps = MapManager.Instance?.ActiveMaps?.ToList() ?? _mapList.Values.ToList();
                    foreach (var map in maps)
                    {
                        if (map == null) continue;
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
                catch (Exception ex)
                {
                    DebugSystem.Write($"[WorldServer] Error in MapTickLoop: {ex.Message}");
                }

                Thread.Sleep((int)WorldTickRate.MapTick);
            }
        }

        private void Eventwrk()
        {
            do
            {
                try
                {
                    // Inspect online players to gracefully clean up disconnected clients
                    var players = GetAllPlayers();
                    foreach (var p in players)
                    {
                        if (p == null) continue;
                        if (p.isDisconnected())
                        {
                            DisconnectPlayer(p.CharID);
                        }
                    }
                }
                catch (Exception ex)
                {
                    DebugSystem.Write($"[WorldServer] Error in Eventwrk: {ex.Message}");
                }

                Thread.Sleep((int)WorldTickRate.EventTick);
            }
            while (!killFlag);
        }

        private void AutoSaveLoop()
        {
            int saveCounter = 0;
            DateTime lastSlowWarning = DateTime.MinValue;
            do
            {
                try
                {
                    var onlinePlayers = GetAllPlayers().ToList();
                    Services.ServerMetrics.ObserveOnlinePlayers(onlinePlayers.Count);
                    if (onlinePlayers.Count > 0)
                    {
                        saveCounter++;
                        var saveWatch = global::System.Diagnostics.Stopwatch.StartNew();
                        foreach (var player in onlinePlayers)
                        {
                            if (player == null || player.isDisconnected()) continue;
                            try
                            {
                                cGlobal.gCharacterDataBase?.WritePlayer(player.CharID, player);
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

                        saveWatch.Stop();
                        Services.ServerMetrics.RecordAutoSave(onlinePlayers.Count, saveWatch.Elapsed.TotalMilliseconds);
                        if (saveWatch.ElapsedMilliseconds > 1000 && (DateTime.Now - lastSlowWarning).TotalSeconds >= 60)
                        {
                            lastSlowWarning = DateTime.Now;
                            DebugSystem.Write($"[AutoSave] Warning: saving {onlinePlayers.Count} players took {saveWatch.ElapsedMilliseconds} ms.");
                        }

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

                int intervalMs = Math.Max(1, cGlobal.SrvSettings?.AutoSaveIntervalSeconds ?? 1) * 1000;
                for (int waited = 0; waited < intervalMs && !killFlag; waited += (int)WorldTickRate.AutoSaveStep)
                {
                    Thread.Sleep((int)WorldTickRate.AutoSaveStep);
                }
            }
            while (!killFlag);

            DebugSystem.Write("[AutoSave] Auto-save thread stopped");
        }
        #endregion

        #region Map & Player Synchronization
        /// <summary>
        /// Synchronizes the local map collection with all active maps currently loaded in MapManager.
        /// </summary>
        public void SyncMapList()
        {
            if (MapManager.Instance == null) return;
            try
            {
                var active = MapManager.Instance.ActiveMaps;
                if (active != null)
                {
                    foreach (var map in active)
                    {
                        if (map != null)
                        {
                            _mapList.AddOrUpdate((ushort)map.MapID, map, (k, old) => map);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[WorldServer] Error syncing MapList: {ex.Message}");
            }
        }

        /// <summary>
        /// Returns all active maps across MapManager.Instance and MapList.
        /// </summary>
        public IEnumerable<GameMap> GetActiveMaps()
        {
            SyncMapList();
            if (MapManager.Instance != null)
            {
                return MapManager.Instance.ActiveMaps;
            }
            return _mapList.Values.ToList();
        }

        public new GameMap GetMap(ushort ID)
        {
            if (MapManager.Instance != null)
            {
                var map = MapManager.Instance.GetMap(ID);
                if (map != null)
                {
                    _mapList.AddOrUpdate(ID, map, (k, old) => map);
                    return map;
                }
            }

            if (_mapList.TryGetValue(ID, out var cachedMap) && cachedMap != null)
            {
                return cachedMap;
            }

            GameMap tmp = base.GetMap(ID);
            if (tmp != null)
            {
                _mapList.AddOrUpdate((ushort)tmp.MapID, tmp, (k, old) => tmp);
                DebugSystem.Write(DebugItemType.Info_Heavy, "Loaded Map {0}", tmp.MapID);
                return tmp;
            }

            return null;
        }
        #endregion

        #region WorldServerHost Implementation
        /// <summary>
        /// Retrieves an online player by character ID or user ID.
        /// </summary>
        public Player GetPlayer(uint charId)
        {
            if (charId == 0) return null;

            try
            {
                var online = cGlobal.gCharacterDataBase?.GetOnlinePlayers();
                var p = online?.FirstOrDefault(x => x != null && (x.CharID == charId || x.UserID == charId));
                if (p != null) return p;

                var maps = GetActiveMaps();
                foreach (var m in maps)
                {
                    if (m?.PlayersList == null) continue;
                    var mp = m.PlayersList.FirstOrDefault(x => x != null && (x.CharID == charId || x.UserID == charId));
                    if (mp != null) return mp;
                }
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[WorldServer] Error in GetPlayer({charId}): {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Checks whether a player is online and connected.
        /// </summary>
        public bool isOnline(uint charId)
        {
            if (charId == 0) return false;
            var p = GetPlayer(charId);
            return p != null && !p.isDisconnected();
        }

        /// <summary>
        /// Disconnects a player and safely unregisters them from the map and character database.
        /// </summary>
        public void DisconnectPlayer(uint charId)
        {
            if (charId == 0) return;
            try
            {
                var p = GetPlayer(charId);
                if (p != null)
                {
                    DebugSystem.Write($"[WorldServer] Disconnecting player {p.CharName} ({charId})...");
                    p.Disconnect();
                    p.CurMap?.RemovePlayer(p);
                    cGlobal.gCharacterDataBase?.OnCharacterLeave(p);
                }
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[WorldServer] Error in DisconnectPlayer({charId}): {ex.Message}");
            }
        }

        /// <summary>
        /// Returns an enumerable snapshot of all online players currently in the world.
        /// </summary>
        public IEnumerable<Player> GetAllPlayers()
        {
            var result = new Dictionary<uint, Player>();
            try
            {
                var dbPlayers = cGlobal.gCharacterDataBase?.GetOnlinePlayers();
                if (dbPlayers != null)
                {
                    foreach (var p in dbPlayers)
                    {
                        if (p != null && p.CharID > 0)
                            result[p.CharID] = p;
                    }
                }

                var maps = GetActiveMaps();
                foreach (var m in maps)
                {
                    var mPlayers = m?.PlayersList;
                    if (mPlayers != null)
                    {
                        foreach (var p in mPlayers)
                        {
                            if (p != null && p.CharID > 0 && !result.ContainsKey(p.CharID))
                                result[p.CharID] = p;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[WorldServer] Error in GetAllPlayers: {ex.Message}");
            }

            return result.Values.ToList();
        }

        /// <summary>
        /// Broadcasts a network packet to all players on all active maps.
        /// </summary>
        public void Broadcast(SendPacket pkt)
        {
            if (pkt == null) return;
            var activeMaps = GetActiveMaps();
            foreach (var m in activeMaps)
            {
                try
                {
                    m?.Broadcast(pkt);
                }
                catch (Exception ex)
                {
                    DebugSystem.Write($"[WorldServer] Error broadcasting packet: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Broadcasts a network packet with optional single-target routing or exclusion.
        /// </summary>
        public void Broadcast(SendPacket pkt, uint? directTo = null, bool exclude = false)
        {
            if (pkt == null) return;

            if (directTo.HasValue && !exclude)
            {
                var target = GetPlayer(directTo.Value);
                if (target != null)
                {
                    target.Send(pkt);
                    return;
                }
            }

            var activeMaps = GetActiveMaps();
            foreach (var m in activeMaps)
            {
                try
                {
                    if (directTo.HasValue)
                    {
                        if (exclude)
                            m?.Broadcast(pkt, "Ex", directTo.Value);
                        else
                            m?.Broadcast(pkt, "ID", directTo.Value);
                    }
                    else
                    {
                        m?.Broadcast(pkt);
                    }
                }
                catch (Exception ex)
                {
                    DebugSystem.Write($"[WorldServer] Error broadcasting packet: {ex.Message}");
                }
            }
        }
        #endregion

        #region Static Helpers
        public static void SendChatMessage(Player p, byte chatType, string message)
        {
            if (chatType == 4 || p == null || string.IsNullOrEmpty(message)) return;
            SendPacket pkt = new SendPacket();
            pkt.Pack8(2); // ActionCode 2 (Chat)
            pkt.Pack8(chatType); // 4 = GM (Red/Orange), 1 = World (Yellow), 3 = Channel (Blue), 6 = Whisper (Pink)
            pkt.Pack32(0); // 4-byte Sender Char ID (0 for System/GM)
            pkt.PackStringN(message); // Raw ASCII characters without length prefix
            p.Send(pkt);
        }

        public static void SendPopupPrompt(Player p, string message)
        {
            if (p == null || string.IsNullOrEmpty(message)) return;
            SendPacket s = new SendPacket();
            s.Pack8(23);
            s.Pack8(57);
            s.Pack8(0);
            s.PackString(message);
            p.Send(s);
        }

        public static void DispatchLoginMotd(Player src)
        {
            try
            {
                if (src == null) return;
                var motdList = cGlobal.SrvSettings?.GetAllWelcomeMessages();
                if ((motdList == null || motdList.Count == 0) && !string.IsNullOrWhiteSpace(Server.ServerStatusManager.Motd))
                {
                    motdList = new List<string> { Server.ServerStatusManager.Motd };
                }

                if (motdList != null && motdList.Count > 0)
                {
                    SendPopupPrompt(src, motdList[0]);
                    DebugSystem.Write($"[WorldServer] Dispatched MOTD dialog to {src.CharName}");
                }
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[WorldServer] Error dispatching MOTD: {ex.Message}");
            }
        }
        #endregion
    }
}
