using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Network;

namespace Game.Battle
{
    public static class PvPManager
    {
        private static readonly Dictionary<uint, uint> _pendingDuelRequests = new Dictionary<uint, uint>(); // TargetID -> ChallengerID
        private static readonly object _lock = new object();

        public static void RequestDuel(Player challenger, Player target)
        {
            if (challenger == null || target == null) return;

            if (challenger.CharID == target.CharID)
            {
                SendSystemMsg(challenger, "You cannot duel yourself!");
                return;
            }

            if (challenger.BattleScene != null || target.BattleScene != null)
            {
                SendSystemMsg(challenger, "Either you or the opponent is currently in battle!");
                return;
            }

            lock (_lock)
            {
                _pendingDuelRequests[target.CharID] = challenger.CharID;
            }

            // Send Duel Request Prompt (AC 11:1)
            SendPacket pReq = new SendPacket();
            pReq.Pack8(11);
            pReq.Pack8(1);
            pReq.Pack32(challenger.CharID);
            target.Send(pReq);

            SendSystemMsg(challenger, $"Duel challenge sent to {target.CharName}!");
        }

        public static void AcceptDuel(Player target)
        {
            if (target == null) return;
            uint challengerId = 0;

            lock (_lock)
            {
                if (!_pendingDuelRequests.TryGetValue(target.CharID, out challengerId))
                    return;
                _pendingDuelRequests.Remove(target.CharID);
            }

            Player challenger = null;
            if (target.CurMap is GameMap curMap)
            {
                challenger = curMap.PlayersList.FirstOrDefault(p => p.CharID == challengerId);
            }

            if (challenger == null)
            {
                SendSystemMsg(target, "Your challenger is no longer available.");
                return;
            }

            StartPvPBattle(challenger, target);
        }

        public static void DeclineDuel(Player target)
        {
            if (target == null) return;
            lock (_lock)
            {
                if (_pendingDuelRequests.TryGetValue(target.CharID, out uint challengerId))
                {
                    _pendingDuelRequests.Remove(target.CharID);
                    if (target.CurMap is GameMap curMap)
                    {
                        var challenger = curMap.PlayersList.FirstOrDefault(p => p.CharID == challengerId);
                        if (challenger != null)
                        {
                            SendSystemMsg(challenger, $"{target.CharName} declined your duel challenge.");
                        }
                    }
                }
            }
        }

        public static void StartPvPBattle(Player player1, Player player2)
        {
            if (player1 == null || player2 == null) return;

            ushort bgId = (player1.CurMap != null && player1.CurMap.MapID < 10000) ? (ushort)player1.CurMap.MapID : (ushort)1;

            // Send Battle Initialization Packets (AC 11:3 & AC 27 Battle Start)
            SendPacket pStart1 = new SendPacket();
            pStart1.Pack8(27);
            pStart1.Pack8(1); // Battle Start
            pStart1.Pack16(bgId); // Map Background ID
            pStart1.Pack8(2); // 2 Combatants
            player1.Send(pStart1);

            SendPacket pStart2 = new SendPacket();
            pStart2.Pack8(27);
            pStart2.Pack8(1);
            pStart2.Pack16(bgId);
            pStart2.Pack8(2);
            player2.Send(pStart2);

            SendSystemMsg(player1, $"[PvP Duel] Battle started against {player2.CharName}!");
            SendSystemMsg(player2, $"[PvP Duel] Battle started against {player1.CharName}!");

            DebugSystem.Write($"[PvPManager] PvP Duel initialized between {player1.CharName} and {player2.CharName}.");
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
