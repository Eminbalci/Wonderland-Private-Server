using System;
using Game;
using Network;

namespace Network.ActionCodes
{
    /// <summary>
    /// AC 27: System Announcement & In-Game Bulletin Board Protocol.
    /// Handles querying official announcements, server patch notes, and bulletin boards.
    /// </summary>
    public class AC27 : AC
    {
        public override int ID => 27;

        public override void ProcessPkt(Player c, RecievePacket p)
        {
            if (c == null || p == null) return;

            byte subCode = p.B ?? 1;

            try
            {
                // Subcode 2: Discard / Throw away item (Confirmed via shoplarincalismamantigi.pcapng)
                // C->S: 1b 02 <slot> <count>
                // S->C: 17 09 <slot> <count> (via c.Inv.RemoveItem)
                // S->C: 1b 02 00 (ACK)
                // S->C: 1b 03
                // S->C: 1b 04
                if (subCode == 2)
                {
                    p.SetPtr(6);
                    byte slot = p.Unpack8();
                    byte count = 1;
                    try { count = p.Unpack8(); } catch { count = 1; }
                    if (count == 0) count = 1;

                    if (slot >= 1 && slot <= 50 && c.Inv != null)
                    {
                        c.Inv.RemoveItem(slot, count);
                        c.SaveCharacterData();

                        SendPacket ack = new SendPacket();
                        ack.Pack8(27);
                        ack.Pack8(2);
                        ack.Pack8(0);
                        c.Send(ack);

                        SendPacket s3 = new SendPacket();
                        s3.Pack8(27);
                        s3.Pack8(3);
                        c.Send(s3);

                        SendPacket s4 = new SendPacket();
                        s4.Pack8(27);
                        s4.Pack8(4);
                        c.Send(s4);

                        DebugSystem.Write($"[AC27.Recv2] Player {c.CharName} discarded slot {slot} x{count}");
                        return;
                    }
                }

                DebugSystem.Write($"[AC27] Bulletin query from {c.CharName}: SubCode={subCode}");
                SendPacket resp = new SendPacket();
                resp.Pack8((byte)ID);
                resp.Pack8(subCode);
                resp.PackStringN("Welcome to Wonderland Private Server!");
                c.Send(resp);
            }
            catch (Exception ex)
            {
                DebugSystem.Write(new ExceptionData(ex));
            }
        }
    }
}
