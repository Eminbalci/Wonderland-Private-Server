using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Game.Code;
using Network;

namespace Game.Experimental
{
    public class BatherRecord
    {
        public uint CharId { get; set; }
        public string CharName { get; set; }
        public ushort MapId { get; set; }
        public DateTime EnteredAt { get; set; }
        public DateTime LastTickTime { get; set; }
        public int TotalTicks { get; set; }

        public BatherRecord(Player player, ushort mapId)
        {
            CharId = player?.CharID ?? 0;
            CharName = player?.CharName ?? "Bather";
            MapId = mapId;
            EnteredAt = DateTime.UtcNow;
            LastTickTime = DateTime.UtcNow;
            TotalTicks = 0;
        }
    }

    /// <summary>
    /// Hot Spring Bathing and Rest Vitality Recovery Engine (AC 44 / Spec 44).
    /// Manages hot spring zones, towel verification, and periodic HP/SP restoration ticks.
    /// </summary>
    public class HotSpringBathingEngine
    {
        private readonly ConcurrentDictionary<uint, BatherRecord> m_bathers = new ConcurrentDictionary<uint, BatherRecord>();

        public int ActiveBatherCount => m_bathers.Count;

        public IReadOnlyCollection<BatherRecord> GetBathers() =>
            new List<BatherRecord>(m_bathers.Values).AsReadOnly();

        public bool EnterHotSpring(Player player, ushort mapId)
        {
            if (player == null) return false;

            var rec = new BatherRecord(player, mapId);
            m_bathers[player.CharID] = rec;

            DebugSystem.Write($"[HotSpringBathingEngine] Player {player.CharName} entered Hot Spring on Map {mapId}");

            // S->C AC 44:1 Bathing state entered
            SendPacket p = new SendPacket();
            p.Pack8(44);
            p.Pack8(1);
            p.Pack8(1); // Bathing mode active
            p.Pack16(mapId);
            player.Send(p);

            // Broadcast towel / bathing appearance to map
            SendPacket bcast = Tools.FromFormat("bbdb", 5, 14, player.CharID, (byte)1);
            player.CurMap?.Broadcast(bcast);

            return true;
        }

        public bool PulseRestTick(Player player)
        {
            if (player == null) return false;

            if (m_bathers.TryGetValue(player.CharID, out var rec))
            {
                rec.LastTickTime = DateTime.UtcNow;
                rec.TotalTicks++;

                // Restore 5% of max HP and SP
                int hpGain = Math.Max(50, player.MaxHP / 20);
                int spGain = Math.Max(25, player.MaxSP / 20);

                player.HP = Math.Min(player.MaxHP, player.HP + hpGain);
                player.SP = Math.Min(player.MaxSP, player.SP + spGain);

                DebugSystem.Write($"[HotSpringBathingEngine] Rest Tick for {player.CharName}: +{hpGain} HP, +{spGain} SP (HP: {player.HP}/{player.MaxHP}, SP: {player.SP}/{player.MaxSP})");

                // S->C AC 44:2 Rest Tick sync
                SendPacket p = new SendPacket();
                p.Pack8(44);
                p.Pack8(2);
                p.Pack16((ushort)player.HP);
                p.Pack16((ushort)player.SP);
                player.Send(p);

                return true;
            }
            return false;
        }

        public void ExitHotSpring(Player player)
        {
            if (player == null) return;

            if (m_bathers.TryRemove(player.CharID, out _))
            {
                DebugSystem.Write($"[HotSpringBathingEngine] Player {player.CharName} exited Hot Spring");

                SendPacket p = new SendPacket();
                p.Pack8(44);
                p.Pack8(3);
                p.Pack8(0); // Exited
                player.Send(p);

                // Clear bathing appearance
                SendPacket bcast = Tools.FromFormat("bbdb", 5, 14, player.CharID, (byte)0);
                player.CurMap?.Broadcast(bcast);
            }
        }
    }
}
