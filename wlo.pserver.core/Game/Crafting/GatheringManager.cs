using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Network;

namespace Game.Crafting
{
    public enum GatheringType : byte
    {
        None = 0,
        Fishing = 1,
        Mining = 2,
        Woodcutting = 3
    }

    public class GatheringSession
    {
        public Player Player { get; set; }
        public GatheringType Type { get; set; }
        public DateTime StartTime { get; set; } = DateTime.UtcNow;
        public DateTime LastGatherTick { get; set; } = DateTime.UtcNow;

        public GatheringSession(Player p, GatheringType type)
        {
            Player = p;
            Type = type;
            StartTime = DateTime.UtcNow;
            LastGatherTick = DateTime.UtcNow;
        }
    }

    public static class GatheringManager
    {
        private static readonly ConcurrentDictionary<uint, GatheringSession> _activeSessions = new ConcurrentDictionary<uint, GatheringSession>();
        private static readonly Random _rng = new Random();
        private static Timer _gatherTimer;

        // Resource tables
        private static readonly ushort[] _fishItems = new ushort[] { 30003, 30004, 30005, 30006, 30007 }; // Crab, Trout, Salmon, Eel, Seaweed
        private static readonly ushort[] _oreItems = new ushort[] { 27020, 27021, 27022, 27023, 27024 }; // Iron Ore, Copper Ore, Coal, Silver, Gold Ore
        private static readonly ushort[] _woodItems = new ushort[] { 27001, 27002, 27003, 27004, 27005 }; // Wood, Pine, Cypress, Willow, Vine

        public static void Initialize()
        {
            _gatherTimer = new Timer(OnGatherTick, null, 5000, 5000); // Ticks every 5 seconds
            DebugSystem.Write("[GatheringManager] AFK Gathering Service Initialized (5s ticks).");
        }

        public static void StartGathering(Player player, GatheringType type)
        {
            if (player == null || type == GatheringType.None) return;

            var session = new GatheringSession(player, type);
            _activeSessions[player.CharID] = session;

            // Play gathering emote / animation
            SendPacket pAction = new SendPacket();
            pAction.Pack8(5);
            pAction.Pack8(type == GatheringType.Fishing ? (byte)12 : (byte)14);
            pAction.Pack32(player.CharID);
            player.CurMap?.Broadcast(pAction);

            SendSystemMsg(player, $"Started AFK {type}! You will gather resources every few seconds.");
            DebugSystem.Write($"[GatheringManager] Player {player.CharName} started {type}.");
        }

        public static void StopGathering(Player player)
        {
            if (player == null) return;

            if (_activeSessions.TryRemove(player.CharID, out var session))
            {
                SendSystemMsg(player, $"Stopped AFK {session.Type}.");
                DebugSystem.Write($"[GatheringManager] Player {player.CharName} stopped gathering.");
            }
        }

        private static void OnGatherTick(object state)
        {
            foreach (var kvp in _activeSessions)
            {
                var session = kvp.Value;
                var player = session.Player;

                if (player == null || player.CurMap == null)
                {
                    _activeSessions.TryRemove(kvp.Key, out _);
                    continue;
                }

                // Grant resource item based on type
                ushort[] pool = null;
                switch (session.Type)
                {
                    case GatheringType.Fishing: pool = _fishItems; break;
                    case GatheringType.Mining: pool = _oreItems; break;
                    case GatheringType.Woodcutting: pool = _woodItems; break;
                }

                if (pool != null && pool.Length > 0)
                {
                    ushort chosenItem = pool[_rng.Next(pool.Length)];
                    player.Inv.AddItem(chosenItem, 1);

                    SendPacket p = new SendPacket();
                    p.Pack8(23);
                    p.Pack8(57);
                    p.Pack8(0);
                    p.PackString($"[AFK {session.Type}] Gathered 1x item #{chosenItem}!");
                    player.Send(p);
                }
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
