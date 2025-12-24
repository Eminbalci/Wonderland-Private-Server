using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Network;
using Game;
using Wonderland_Private_Server.Utilities;

namespace Network.ActionCodes
{
    public class AC62 : AC
    {
        public override int ID { get { return 62; } }
        public override void ProcessPkt(Player p, RecievePacket r)
        {
            DebugSystem.Write(DebugItemType.Error, $"[DEBUG] AC62 ProcessPkt - Sub={r.B}");
            switch (r.B)
            {
                case 1: DebugSystem.Write(DebugItemType.Error, "[DEBUG] AC62 - Entering Recv1"); Recv1(p, r); break;
                case 3: Recv3(p, r); break;//rotate, move objects in tent.
                case 4: Recv4(p, r); break; // add item type especial
                default: Console.WriteLine(r.A + "," + r.B + " Has not been coded"); break;
            }
        }
        void Recv1(Player p, RecievePacket r)
        {
            // Packet format: 14 Byte Payload (Header 4 + AC 1 + Sub 1 + Payload 14 = 20)
            // Bag(1) + Slot(1) + X(4 BE) + Y(4 BE) + Floor(4 BE) = 14 Bytes

            if (r.Buffer.Length < 20)
            {
                DebugSystem.Write(DebugItemType.Error, $"[Tent] Ignored short AC62 packet (Len={r.Buffer.Length})");
                return;
            }

            try
            {
                byte bagIndex = r.Unpack8();        // Bag Index
                byte slotIndex = r.Unpack8();       // Slot Index

                // Read coordinates as 32-bit integers LITTLE ENDIAN (Standard)
                uint x = r.Unpack32();
                uint y = r.Unpack32();
                uint floor = r.Unpack32();

                byte rotation = 0; // No rotation in Place packet

                DebugSystem.Write(DebugItemType.Error, $"[Tent] AC62,1 Place: Bag={bagIndex} Slot={slotIndex} Pos=({x},{y}) Flr={floor} (Forcing 0)");

                // FORCE FLOOR 0 as requested
                floor = 0;

                // FORCE PLACE DEBUG MODE
                // Client is sending Slot=0 / Pos=0,0 so standard placement fails.
                // We will force-place "Coconut Basin" (38027) to test visibility.

                if (p.Tent != null)
                {
                    DebugSystem.Write(DebugItemType.Error, "[Tent] FORCE PLACING Coconut Basin (38027) at (43,42)");
                    p.Tent.PlaceItem(38027, 43, 42, 0, 0);

                    // ALWAYS Refresh Tent Items (To trigger Matrix Test) with the new item included
                    p.Tent.SendTentItemsToPlayer(p);

                    // Send Confirmation
                    SendPacket confirmation = new SendPacket();
                    confirmation.PackArray(new byte[] { 62, 1 });
                    confirmation.Pack8(1);
                    p.Send(confirmation);
                }
            }
            catch (Exception ex)
            {
                DebugSystem.Write(DebugItemType.Error, $"[Tent] Error in AC62 Recv1: {ex.Message}");
            }
        }
        void Recv3(Player p, RecievePacket r)
        {
            try
            {
                // Packet format based on user capture: Index(2) + X(4) + Y(4) + Floor(4) + Rot?(1)
                ushort index = r.Unpack16();
                uint x = r.Unpack32();
                uint y = r.Unpack32();
                uint floor = r.Unpack32();
                byte rotation = 0;

                // Try reading rotation if available
                // if (r.Buffer.Count > r.Offset) rotation = r.Unpack8();
                // User capture shows 'ad' (00) at end. Assuming Rot.
                try { rotation = r.Unpack8(); } catch { }

                DebugSystem.Write(DebugItemType.Error, $"[Tent] AC62,3 Move: Index {index} to ({x},{y}) Flr {floor} Rot {rotation}");

                if (p.Tent != null)
                {
                    p.Tent.MoveItem(index, (int)x, (int)y, (int)floor, rotation);
                    p.Tent.SendTentItemsToPlayer(p);
                }
            }
            catch (Exception t) { Console.WriteLine(t); }
        }
        void Recv4(Player p, RecievePacket r)
        {
            try { }

            catch (Exception t) { Console.WriteLine(t); }
        }
    }
}
