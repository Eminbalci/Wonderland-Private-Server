using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Network;

namespace Game.PlayerRelated
{
    public enum RebornJob : byte
    {
        None = 0,
        Killer = 1,     // ATK Specialist
        Warrior = 2,    // DEF / Tank Specialist
        Knight = 3,     // SPD / Riding Specialist
        Wit = 4,        // MATK Specialist (Sage)
        Priest = 5,     // MDEF / Support Specialist
        Seer = 6        // Sealing / Control Specialist
    }

    public static class RebornManager
    {
        public static bool CanReborn(Player player)
        {
            if (player == null) return false;
            return player.Level >= 100 && !player.Reborn;
        }

        public static bool PerformReborn(Player player, RebornJob job)
        {
            if (player == null) return false;

            if (player.Level < 100)
            {
                SendSystemMsg(player, "You must reach at least Level 100 to undergo Rebirth!");
                return false;
            }

            if (player.Reborn)
            {
                SendSystemMsg(player, "You have already undergone Rebirth!");
                return false;
            }

            if (job == RebornJob.None)
            {
                SendSystemMsg(player, "Please select a valid Rebirth class: Killer, Warrior, Knight, Wit, Priest, or Seer.");
                return false;
            }

            // Perform Rebirth
            player.Job = (Game.RebornJob)(byte)job;
            player.SetLevel(1);

            // Grant Rebirth Cape & Special Class Skill
            ushort capeItemId = GetRebornCapeItem(job);
            if (capeItemId > 0)
            {
                player.Inv.AddItem(capeItemId, 1);
            }

            // Play Rebirth Grand Visual Effect
            SendPacket pVisual = new SendPacket();
            pVisual.Pack8(5);
            pVisual.Pack8(5);
            pVisual.Pack32(player.CharID);
            pVisual.Pack16(60050); // Rebirth aura/ascension effect
            player.CurMap?.Broadcast(pVisual);

            // Announce server-wide
            SendPacket pAnnounce = new SendPacket();
            pAnnounce.Pack8(23);
            pAnnounce.Pack8(57);
            pAnnounce.Pack8(0);
            pAnnounce.PackString($"[Server Announcement] Player {player.CharName} has undergone Rebirth and awakened as a powerful {job}!");
            player.CurMap?.Broadcast(pAnnounce);

            SendSystemMsg(player, $"Congratulations! You are now a Reborn {job} (Lv 1) with unlocked stat potentials!");
            DebugSystem.Write($"[RebornManager] Player {player.CharName} reborn as {job}.");
            return true;
        }

        public static ushort GetRebornCapeItem(RebornJob job)
        {
            switch (job)
            {
                case RebornJob.Killer: return 23001; // Killer Cape
                case RebornJob.Warrior: return 23002; // Warrior Cape
                case RebornJob.Knight: return 23003; // Knight Cape
                case RebornJob.Wit: return 23004; // Wit Cape
                case RebornJob.Priest: return 23005; // Priest Cape
                case RebornJob.Seer: return 23006; // Seer Cape
                default: return 0;
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
