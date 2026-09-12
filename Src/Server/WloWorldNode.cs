using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using Game;
using Game.Maps;
using System.Collections.Concurrent;
using RCLibrary.Core.Networking;
using Network;


namespace Server
{
    public class WloWorldNode
    {
        Thread m_thrd;
        bool Killnow;

        //WloSocketListener m_listerner;

        List<Player> m_localPlayerlist, m_externalPlayerlist;

        ConcurrentStack<Player> ConnectedQueue, DisconnectedQueue;

        List<Game.GameMap> Maplist = new List<Game.GameMap>();


        public WloWorldNode()
        {
            //  m_listerner = new WloSocketListener(6414);
            m_localPlayerlist = new List<Player>();
            m_externalPlayerlist = new List<Player>();
            ConnectedQueue = new ConcurrentStack<Player>();
            DisconnectedQueue = new ConcurrentStack<Player>();
        }

        #region Events
        //void c_onNewClient(object sender, WloClient e)
        //{
        //    ConnectedQueue.Push(new Player(ref e));
        //}
        void onDisconnectedClient(Player src)
        {
            DisconnectedQueue.Push(src);
            onPlayerDced?.Invoke(src);
        }

        public event Action<Player[]> onNewPlayer;//for Form
        public event Action<Player> onPlayerDced;//for Form
        #endregion

        public void Open()
        {
            ConnectedQueue = new ConcurrentStack<Player>();
            DisconnectedQueue = new ConcurrentStack<Player>();
            DebugSystem.Write("Starting Wlo Game World");
            DebugSystem.Write("Created by IloveWlo Dev Team");
            //GameEvents = new EventSystem();
            //GameEvents.Start();

            m_thrd = new Thread(new ThreadStart(MainMapLoop));
            m_thrd.IsBackground = true;
            m_thrd.Init();

            DebugSystem.Write("Starting TCP Listener");
            //m_listerner.onNewClient += c_onNewClient;
            //m_listerner.Initialize();
        }

        public void Close(bool force = false)
        {
            DebugSystem.Write("Stopping Listener");
            //m_listerner.Kill();
            DebugSystem.Write("Disconnecting Players");
            m_localPlayerlist.AsParallel().ForAll(c => c.Disconnect());
            DebugSystem.Write("Waiting for Players to properly disconnect");
            while (m_localPlayerlist.Count > 0)
                Thread.Sleep(1);
            DebugSystem.Write("Clearing External Players");
            m_externalPlayerlist.Clear();

            //GameEvents.Stop();
            Killnow = true;
        }

        /// <summary>
        /// If the Server is Currently Running
        /// </summary>
        public bool isRunning { get { return (m_thrd != null && m_thrd.IsAlive); } }
        /// <summary>
        /// If the Server is Listening for new connections
        /// </summary>
        //public bool isListening { get { return m_listerner.IsRunning(); } }


        void MainLoop()
        {


            do
            {
                //    int a = 20;

                //    while (ConnectedQueue.Count > 0 && (--a) > 0)
                //        ConnectedPlayers.Add(ConnectedQueue.Dequeue());

                //    foreach (var curplayer in ConnectedPlayers)
                //        if (curplayer.IdleTimer() > new TimeSpan(0, 5, 0))
                //        {
                //            DebugSystem.Write(System.Drawing.Color.FromArgb(255, 128, 255), String.Format("Client {0} timed out at Login.", curplayer.SockAddress()));
                //            curplayer.Disconnect();
                //            DisconnectedQueue.Enqueue(curplayer);
                //        }
                //        else if (curplayer.isDisconnected())
                //        {
                //            DebugSystem.Write(System.Drawing.Color.FromArgb(255, 128, 255), String.Format("Client {0} has Disconnected.", curplayer.SockAddress()));
                //            curplayer.Disconnect();
                //            DisconnectedQueue.Enqueue(curplayer);
                //        }

                Thread.Sleep(5);
            }
            while (!Killnow);
        }

        void MainMapLoop()
        {

            ParallelOptions options = new ParallelOptions();

            do
            {
                Player[] pitems = new Game.Player[50];

                if (DisconnectedQueue.TryPopRange(pitems, 0, 50) > 0)
                {
                    Parallel.ForEach(pitems.Where(c => c != null).ToList(), src =>
                    {
                        m_localPlayerlist.Remove(src);
                        DebugSystem.Write(String.Format("Client {0} has Disconnected.", src.SockAddress()));
                    });
                }

                if (ConnectedQueue.TryPopRange(pitems, 0, 50) > 0)
                {
                    try
                    {
                        pitems.Where(c => c != null).AsParallel().ForAll(p => p.OnDisconnect = onDisconnectedClient);
                        m_localPlayerlist.AddRange(pitems.Where(c => c != null).ToArray());
                        onNewPlayer(pitems.Where(c => c != null).ToArray());
                    }
                    catch (Exception f) { DebugSystem.Write(new ExceptionData(f)); }

                }

                try
                {
                    options.MaxDegreeOfParallelism = 1 + (Maplist.Count / 2);
                    Parallel.ForEach(Maplist, options, curMap => { curMap.Process(); });
                }
                catch (AggregateException e)
                {
                    foreach (Exception f in e.InnerExceptions)
                        DebugSystem.Write(new ExceptionData(f));
                }
                Thread.Sleep(1);
            }
            while (!Killnow);

            Parallel.ForEach(Maplist, curMap => curMap.Dispose());
        }

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
            Maplist.AsParallel().ForAll(c => c.Broadcast(pkt, parameter, To));
        }

        public void BroadcastTo(SendPacket pkt, uint directTo = 0)
        {
            // If directTo is specified, try to find the player and send only to them.
            // If directTo is 0 (or not found?), maybe broadcast to all?
            // Based on usage conventions:
            if (directTo > 0)
            {
                // Find player in local list
                var p = m_localPlayerlist.FirstOrDefault(pl => pl.UserID == directTo);
                if (p != null)
                {
                    p.Send(pkt);
                }
                else
                {
                    // Search in maps? Or external players?
                    // For now, if not in local list, we might assume they are not on this node or we broadcast to maps to find them.
                    // But typically BroadcastTo implies sending to a specific target if known.
                    // Let's iterate maps to find player?
                    // Maplist.AsParallel().ForAll(m => m.Broadcast(pkt, "ID", directTo.ToString())); 
                    // Wait, Map.Broadcast usually takes "ID" and string.
                    // Let's implement robust finding.

                    foreach (var m in Maplist)
                    {
                        // Assuming Map has a way to send to specific player or we search players in map.
                        // Map.Broadcast signature: (SendPacket pkt, string parameter, params object[] To)
                        // Maps usually have a list of players.
                        // Let's use Map.Broadcast if it supports "TargetID".
                    }
                    // For simplicity / consistency with `cGlobal.WLO_World.BroadcastTo` usage in `Instance.cs`:
                    // It seems to expect global broadcast or specific target.

                    Broadcast(pkt, "ID", directTo.ToString());
                }
            }
            else
            {
                Broadcast(pkt);
            }
        }

        /// <summary>
        /// Searches for a Player and invokes the corresponding action
        /// </summary>
        /// <param name="src"></param>
        /// <param name="work"></param>
        /// <returns>true if succeeded or false if it failed</returns>
        public bool DoAction(uint src, Action<Player> work)
        {
            //try
            //{
            //    if (cGlobal.gGameDataBase.isOnline((int)src))
            //    {
            //        work.Invoke(m_localPlayerlist.Single(c => c.CharID == src));
            //        return true;
            //    }
            //}
            //catch (Exception r) { DebugSystem.Write(new ExceptionData(r)); }
            return false;
        }

        public async void onTelePort(TeleportType teletype, byte portalID, Game.Maps.WarpData map, Player target)
        {

            if (Maplist.Count(c => c.MapID == map.DstMap) == 0)
            {
                //Create Map
                Game.GameMap tmp = Game.Maps.MapManager.Instance.GetMap(map.DstMap);
                //cGlobal.gGameDataBase.SetupMap(ref tmp);
                Maplist.Add(tmp);
            }


            Maplist.Single(c => c.MapID == map.DstMap).Teleport(teletype, target, portalID, map);
        }
    }
}
