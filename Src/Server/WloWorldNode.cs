using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Game;
using Game.Maps;
using Network;
using Plugin;
using RCLibrary.Core.Networking;

namespace Server
{
    /// <summary>
    /// Thread-safe game world cluster node managing local/external player collections
    /// and integrating directly with MapManager.Instance.
    /// </summary>
    public class WloWorldNode : WorldServerHost
    {
        private Thread m_thrd;
        private volatile bool m_killNow;

        private readonly ConcurrentDictionary<uint, Player> m_localPlayers = new ConcurrentDictionary<uint, Player>();
        private readonly ConcurrentDictionary<uint, Player> m_externalPlayers = new ConcurrentDictionary<uint, Player>();

        private readonly ConcurrentQueue<Player> m_connectedQueue = new ConcurrentQueue<Player>();
        private readonly ConcurrentQueue<Player> m_disconnectedQueue = new ConcurrentQueue<Player>();

        public WloWorldNode()
        {
        }

        #region Events
        public event Action<Player[]> onNewPlayer;
        public event Action<Player> onPlayerDced;

        private void onDisconnectedClient(Player src)
        {
            if (src == null) return;
            m_disconnectedQueue.Enqueue(src);
            try
            {
                onPlayerDced?.Invoke(src);
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[WloWorldNode] Error in onPlayerDced invocation: {ex.Message}");
            }
        }
        #endregion

        #region Properties
        /// <summary>
        /// Indicates if the node thread is active and running.
        /// </summary>
        public bool isRunning => m_thrd != null && m_thrd.IsAlive;

        /// <summary>
        /// Read-only snapshot of locally connected players.
        /// </summary>
        public IReadOnlyDictionary<uint, Player> LocalPlayers => m_localPlayers;

        /// <summary>
        /// Read-only snapshot of externally connected players.
        /// </summary>
        public IReadOnlyDictionary<uint, Player> ExternalPlayers => m_externalPlayers;

        /// <summary>
        /// Total connected players across local and external pools.
        /// </summary>
        public int Clients_Connected => m_localPlayers.Count + m_externalPlayers.Count;

        /// <summary>
        /// Total players currently active in-game.
        /// </summary>
        public int Clients_inGame => m_localPlayers.Values.Count(c => c != null && c.Flags != null && c.Flags.HasFlag(PlayerFlag.InMap)) +
                                     m_externalPlayers.Values.Count(c => c != null && c.Flags != null && c.Flags.HasFlag(PlayerFlag.InMap));
        #endregion

        #region Lifecycle
        /// <summary>
        /// Starts the background map and client synchronization loop.
        /// </summary>
        public void Open()
        {
            m_killNow = false;
            DebugSystem.Write("[WloWorldNode] Starting Wlo Game World Node...");

            m_thrd = new Thread(new ThreadStart(MainMapLoop))
            {
                IsBackground = true,
                Name = "WloWorldNode Map Loop"
            };
            m_thrd.Init();

            DebugSystem.Write("[WloWorldNode] World Node started successfully.");
        }

        /// <summary>
        /// Gracefully stops the node, disconnecting all local players with bounded timeout.
        /// </summary>
        public void Close(bool force = false)
        {
            DebugSystem.Write("[WloWorldNode] Stopping World Node...");
            m_killNow = true;

            DebugSystem.Write("[WloWorldNode] Disconnecting local players...");
            Parallel.ForEach(m_localPlayers.Values, p =>
            {
                try
                {
                    p?.Disconnect();
                }
                catch { }
            });

            // Bounded wait up to 3 seconds to prevent thread starvation or deadlocks
            int waitBudgetMs = 3000;
            while (!m_localPlayers.IsEmpty && waitBudgetMs > 0)
            {
                Thread.Sleep(50);
                waitBudgetMs -= 50;
            }

            m_localPlayers.Clear();
            m_externalPlayers.Clear();

            if (m_thrd != null && m_thrd.IsAlive)
            {
                try
                {
                    m_thrd.Join(1000);
                }
                catch { }
                m_thrd = null;
            }

            DebugSystem.Write("[WloWorldNode] World Node stopped.");
        }
        #endregion

        #region Processing Loop
        private void MainMapLoop()
        {
            while (!m_killNow)
            {
                try
                {
                    // 1. Process disconnected queue
                    var dcedPlayers = new List<Player>();
                    while (m_disconnectedQueue.TryDequeue(out var dced) && dced != null)
                    {
                        dcedPlayers.Add(dced);
                    }

                    if (dcedPlayers.Count > 0)
                    {
                        foreach (var src in dcedPlayers)
                        {
                            uint key = src.CharID != 0 ? src.CharID : src.UserID;
                            if (key != 0)
                            {
                                m_localPlayers.TryRemove(key, out _);
                            }
                            DebugSystem.Write($"[WloWorldNode] Client {src.SockAddress()} has Disconnected.");
                        }
                    }

                    // 2. Process connected queue
                    var newPlayers = new List<Player>();
                    while (m_connectedQueue.TryDequeue(out var conn) && conn != null)
                    {
                        newPlayers.Add(conn);
                    }

                    if (newPlayers.Count > 0)
                    {
                        foreach (var p in newPlayers)
                        {
                            p.OnDisconnect = onDisconnectedClient;
                            uint key = p.CharID != 0 ? p.CharID : (p.UserID != 0 ? p.UserID : (uint)p.GetHashCode());
                            m_localPlayers.AddOrUpdate(key, p, (k, old) => p);
                        }

                        try
                        {
                            onNewPlayer?.Invoke(newPlayers.ToArray());
                        }
                        catch (Exception ex)
                        {
                            DebugSystem.Write($"[WloWorldNode] Error in onNewPlayer invocation: {ex.Message}");
                        }
                    }

                    // 3. Process active maps directly via MapManager.Instance
                    var activeMaps = MapManager.Instance?.ActiveMaps;
                    if (activeMaps != null)
                    {
                        var maps = activeMaps.ToList();
                        Parallel.ForEach(maps, curMap =>
                        {
                            try
                            {
                                curMap?.Process();
                            }
                            catch (Exception ex)
                            {
                                DebugSystem.Write($"[WloWorldNode] Error processing map {curMap?.MapID}: {ex.Message}");
                            }
                        });
                    }
                }
                catch (Exception ex)
                {
                    DebugSystem.Write($"[WloWorldNode] Error in MainMapLoop: {ex.Message}");
                }

                Thread.Sleep(50);
            }
        }
        #endregion

        #region Player Operations & WorldServerHost
        /// <summary>
        /// Queues a new client connection for processing on this node.
        /// </summary>
        public void QueueNewPlayer(Player player)
        {
            if (player == null) return;
            m_connectedQueue.Enqueue(player);
        }

        public Player GetPlayer(uint charId)
        {
            if (charId == 0) return null;

            if (m_localPlayers.TryGetValue(charId, out var local) && local != null)
            {
                return local;
            }

            var p = m_localPlayers.Values.FirstOrDefault(x => x != null && (x.CharID == charId || x.UserID == charId));
            if (p != null) return p;

            if (m_externalPlayers.TryGetValue(charId, out var external) && external != null)
            {
                return external;
            }

            return m_externalPlayers.Values.FirstOrDefault(x => x != null && (x.CharID == charId || x.UserID == charId));
        }

        public bool isOnline(uint charId)
        {
            var p = GetPlayer(charId);
            return p != null && !p.isDisconnected();
        }

        public void DisconnectPlayer(uint charId)
        {
            var p = GetPlayer(charId);
            if (p != null)
            {
                p.Disconnect();
                p.CurMap?.RemovePlayer(p);
                m_localPlayers.TryRemove(charId, out _);
                m_externalPlayers.TryRemove(charId, out _);
            }
        }

        public IEnumerable<Player> GetAllPlayers()
        {
            return m_localPlayers.Values
                .Concat(m_externalPlayers.Values)
                .Where(p => p != null && !p.isDisconnected())
                .Distinct()
                .ToList();
        }

        public void Broadcast(SendPacket pkt)
        {
            if (pkt == null) return;
            var activeMaps = MapManager.Instance?.ActiveMaps;
            if (activeMaps != null)
            {
                foreach (var m in activeMaps)
                {
                    m?.Broadcast(pkt);
                }
            }
        }

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

            var activeMaps = MapManager.Instance?.ActiveMaps;
            if (activeMaps != null)
            {
                foreach (var m in activeMaps)
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
            }
        }

        public void Broadcast(SendPacket pkt, string parameter, params object[] To)
        {
            if (pkt == null) return;
            var activeMaps = MapManager.Instance?.ActiveMaps;
            if (activeMaps != null)
            {
                foreach (var m in activeMaps)
                {
                    m?.Broadcast(pkt, parameter, To);
                }
            }
        }

        public void BroadcastTo(SendPacket pkt, uint directTo = 0)
        {
            if (pkt == null) return;
            if (directTo > 0)
            {
                var p = GetPlayer(directTo);
                if (p != null)
                {
                    p.Send(pkt);
                    return;
                }

                var activeMaps = MapManager.Instance?.ActiveMaps;
                if (activeMaps != null)
                {
                    foreach (var m in activeMaps)
                    {
                        m?.Broadcast(pkt, "ID", directTo.ToString());
                    }
                }
            }
            else
            {
                Broadcast(pkt);
            }
        }

        public bool DoAction(uint src, Action<Player> work)
        {
            if (work == null) return false;
            var p = GetPlayer(src);
            if (p != null)
            {
                try
                {
                    work(p);
                    return true;
                }
                catch (Exception ex)
                {
                    DebugSystem.Write(new ExceptionData(ex));
                }
            }
            return false;
        }

        public void onTelePort(TeleportType teletype, byte portalID, Game.Maps.WarpData map, Player target)
        {
            if (target == null || map == null) return;
            Game.GameMap targetMap = Game.Maps.MapManager.Instance?.GetMap(map.DstMap);
            if (targetMap != null)
            {
                targetMap.Teleport(teletype, target, portalID, map);
            }
            else
            {
                DebugSystem.Write($"[WloWorldNode] Map {map.DstMap} not found for teleport.");
            }
        }
        #endregion
    }
}
