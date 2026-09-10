using System;
using Game;
using Network;

namespace Network.ActionCodes
{
    /// <summary>
    /// AC 55: Mystery Lucky Bag & Gacha Capsule Opening Protocol.
    /// Handles opening lucky bags, reward capsules, and mystery packages with randomized loot drops.
    /// </summary>
    public class AC55 : AC
    {
        public override int ID => 55;

        public override void ProcessPkt(Player c, RecievePacket p)
        {
            if (c == null || p == null) return;

            byte subCode = p.B ?? 1;
            byte slot = 0;
            if (p.Buffer.Length >= 7)
            {
                slot = p.Unpack8();
            }

            DebugSystem.Write($"[AC55] Lucky bag open from {c.CharName}: SubCode={subCode}, Slot={slot}");

            try
            {
                ushort boxId = c.Inv.GetItemIdAtSlot(slot);
                if (boxId > 0)
                {
                    c.Inv.RemoveItemAtSlot(slot, 1);
                    // Award lucky reward item
                    ushort rewardItem = 23001; // Default lucky ticket or potion
                    c.Inv.AddItem(rewardItem, 1);

                    SendPacket resp = new SendPacket();
                    resp.Pack8((byte)ID);
                    resp.Pack8(subCode);
                    resp.Pack16(rewardItem);
                    resp.Pack8(1); // Count
                    c.Send(resp);
                }
            }
            catch (Exception ex)
            {
                DebugSystem.Write(new ExceptionData(ex));
            }
        }
    }
}
