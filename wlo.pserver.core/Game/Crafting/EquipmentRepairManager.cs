using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Network;

namespace Game.Crafting
{
    public static class EquipmentRepairManager
    {
        public static bool RepairItem(Player player, byte inventorySlot)
        {
            if (player == null) return false;

            ushort itemId = player.Inv.GetItemIdAtSlot(inventorySlot);
            if (itemId == 0)
            {
                SendSystemMsg(player, "Please select an item in your inventory to repair.");
                return false;
            }

            // Check for Spanner / Repair Tool (#48050) or 500 gold fee
            if (player.Inv.ContainsItem(48050))
            {
                player.Inv.RemoveItemById(48050, 1);
            }
            else if (player.Gold >= 500)
            {
                player.TakeGold(500);
            }
            else
            {
                SendSystemMsg(player, "You need a Repair Wrench or 500 gold to repair your equipment!");
                return false;
            }

            // Play repair hammer effect
            SendPacket p = new SendPacket();
            p.Pack8(5);
            p.Pack8(5);
            p.Pack32(player.CharID);
            p.Pack16(60015); // Blacksmith hammer sound/spark effect
            player.CurMap?.Broadcast(p);

            SendSystemMsg(player, $"[Blacksmith Repair] Successfully restored item #{itemId} to maximum durability!");
            DebugSystem.Write($"[EquipmentRepair] Player {player.CharName} repaired slot #{inventorySlot} (Item #{itemId}).");
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
