using System;
using Game;
using Game.Crafting;
using Network;

namespace Network.ActionCodes
{
    /// <summary>
    /// AC 36: Equipment Durability & Repair Subsystem Protocol.
    /// Handles restoring durability of worn equipment using Repair Wrenches / Spanners or gold fees.
    /// </summary>
    public class AC36 : AC
    {
        public override int ID => 36;

        public override void ProcessPkt(Player c, RecievePacket p)
        {
            if (c == null || p == null) return;

            byte subCode = p.B ?? 1;
            byte slot = 0;
            if (p.Buffer.Length >= 7)
            {
                slot = p.Unpack8();
            }

            DebugSystem.Write($"[AC36] Equipment repair requested by {c.CharName}: SubCode={subCode}, Slot={slot}");

            try
            {
                bool success = EquipmentRepairManager.RepairItem(c, slot);

                SendPacket resp = new SendPacket();
                resp.Pack8((byte)ID);
                resp.Pack8(subCode);
                resp.Pack8(slot);
                resp.Pack8(success ? (byte)1 : (byte)0);
                c.Send(resp);
            }
            catch (Exception ex)
            {
                DebugSystem.Write(new ExceptionData(ex));
            }
        }
    }
}
