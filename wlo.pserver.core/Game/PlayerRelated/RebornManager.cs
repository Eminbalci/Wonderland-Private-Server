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
        public const byte RequiredLevel = 100;

        public static bool CanReborn(Player player)
        {
            if (player == null) return false;
            return player.Level >= RequiredLevel && !player.Reborn;
        }

        public static bool TryParseJob(string text, out RebornJob job)
        {
            job = RebornJob.None;
            if (string.IsNullOrWhiteSpace(text)) return false;
            text = text.Trim();
            if (byte.TryParse(text, out byte n))
            {
                if (n < 1 || n > 6) return false;
                job = (RebornJob)n;
                return true;
            }
            if (text.Equals("sage", StringComparison.OrdinalIgnoreCase) ||
                text.Equals("mage", StringComparison.OrdinalIgnoreCase)) { job = RebornJob.Wit; return true; }
            return Enum.TryParse(text, true, out job) && job != RebornJob.None && Enum.IsDefined(typeof(RebornJob), job);
        }

        /// <summary>
        /// Rebirths a character: sets the reborn job, restarts at level 1 on the
        /// reborn EXP curve, refreshes the client and saves immediately.
        /// ignoreLevel lets GM commands skip the level 100 requirement.
        /// </summary>
        public static bool PerformReborn(Player player, RebornJob job, bool ignoreLevel = false)
        {
            if (player == null) return false;

            if (!ignoreLevel && player.Level < RequiredLevel)
            {
                Notify(player, $"You must reach Level {RequiredLevel} to undergo Rebirth (current: {player.Level}).");
                return false;
            }

            if (player.Reborn)
            {
                Notify(player, "You have already undergone Rebirth!");
                return false;
            }

            if (job == RebornJob.None)
            {
                Notify(player, "Choose a Rebirth class: Killer, Warrior, Knight, Wit, Priest or Seer.");
                return false;
            }

            // Job drives the Reborn flag (Reborn => Job != none), so set it before
            // SetLevel so level 1 is computed on the reborn EXP curve.
            player.Job = (Game.RebornJob)(byte)job;
            player.RebornJob = (byte)job;
            player.SetLevel(1);
            player.FillHP();
            player.FillSP();

            // Refresh the client's EXP/HP/SP and the appearance (reborn flag).
            player.SendStat64(36, player.TotalExp);
            player.Send8_1(true);
            player.CurMap?.Broadcast(Tools.FromFormat("bbdw", 5, 5, player.CharID, (ushort)0));
            SendPacket pVisual = new SendPacket();
            pVisual.Pack8(23);
            pVisual.Pack8(122);
            pVisual.Pack32(player.CharID);
            player.CurMap?.Broadcast(pVisual);

            player.SaveCharacterData();

            Notify(player, $"Congratulations! You are now a Reborn {job} (Lv 1). Please relog to refresh your character.");
            DebugSystem.Write($"[RebornManager] Player {player.CharName} reborn as {job}.");
            return true;
        }

        /// <summary>
        /// Native client 0x1aaf58 (AC79:2): a reborn character changes job using a
        /// Transfer Mirror (34018) and the job cape. The client sends the slots.
        /// </summary>
        public const ushort TransferMirrorItemId = 34018;

        public static bool ChangeJob(Player player, RebornJob job, byte mirrorSlot, byte capeSlot)
        {
            if (player == null) return false;
            if (!player.Reborn) { Notify(player, "Only reborn characters can change job."); return false; }
            if (job == RebornJob.None || (byte)player.Job == (byte)job) { Notify(player, "Choose a different job."); return false; }
            lock (player.Inv.SyncRoot)
            {
                if (mirrorSlot < 1 || mirrorSlot > 50 || player.Inv[mirrorSlot].ItemID != TransferMirrorItemId)
                {
                    Notify(player, "No Transfer Mirror in inventory.");
                    return false;
                }
                if (capeSlot < 1 || capeSlot > 50 || player.Inv[capeSlot].ItemID == 0)
                {
                    Notify(player, "Job's cape must be in inventory.");
                    return false;
                }
                player.Inv.RemoveItem(mirrorSlot, 1, true);
            }
            player.Job = (Game.RebornJob)(byte)job;
            player.RebornJob = (byte)job;
            player.Send8_1(false);
            player.SaveCharacterData();
            Notify(player, $"Your job is now {job}. Please relog to refresh your character.");
            DebugSystem.Write($"[RebornManager] Player {player.CharName} changed job to {job}.");
            return true;
        }

        private static void Notify(Player p, string msg)
        {
            if (p == null || string.IsNullOrEmpty(msg)) return;
            p.SendHeadBanner(msg);
        }
    }
}
