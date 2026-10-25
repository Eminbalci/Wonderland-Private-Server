using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Network;

namespace Game.Crafting
{
    public static class ForgingManager
    {
        public static bool ForgeGem(Player player, byte equipSlot, byte gemSlot)
        {
            if (player == null) return false;

            ushort equipId = player.Inv.GetItemIdAtSlot(equipSlot);
            ushort gemId = player.Inv.GetItemIdAtSlot(gemSlot);

            if (equipId == 0 || gemId == 0)
            {
                SendSystemMsg(player, "Please select both an equipment and a Gem/Spar from your inventory!");
                return false;
            }

            string gemStat = string.Empty;
            switch (gemId)
            {
                case 47001: gemStat = "+24 ATK Spar"; break;
                case 47002: gemStat = "+24 DEF Spar"; break;
                case 47003: gemStat = "+24 MATK Spar"; break;
                case 47004: gemStat = "+24 MDEF Spar"; break;
                case 47005: gemStat = "+24 SPD Spar"; break;
                case 47010: gemStat = "Brilliant Diamond (+42 Stats)"; break;
                default:
                    SendSystemMsg(player, "The selected material is not a valid Forging Spar or Diamond!");
                    return false;
            }

            // Remove the gem
            player.Inv.RemoveItemAtSlot(gemSlot, 1);

            // Play forging effect (AC 23:122) and clear any shapeshift (AC 5:5 with 0)
            SendPacket p = new SendPacket();
            p.Pack8(23);
            p.Pack8(122);
            p.Pack32(player.CharID);
            player.CurMap?.Broadcast(p);
            player.CurMap?.Broadcast(Tools.FromFormat("bbdw", 5, 5, player.CharID, (ushort)0));

            SendSystemMsg(player, $"[Forging Success!] Successfully embedded {gemStat} into your equipment #{equipId}!");
            DebugSystem.Write($"[ForgingManager] Player {player.CharName} forged #{gemId} onto #{equipId}.");
            return true;
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
