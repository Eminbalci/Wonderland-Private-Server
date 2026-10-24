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
                case 2: Recv2(p, r); break; // Pick up item from tent into inventory
                case 3: Recv3(p, r); break; // rotate, move objects in tent.
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

                DebugSystem.Write(DebugItemType.Error, $"[Tent] AC62,1 Place: Bag={bagIndex} Slot={slotIndex} Pos=({x},{y}) Flr={floor} (Forcing 0)");

                // FORCE FLOOR 0 as requested
                floor = 0;

                if (p.Tent != null)
                {
                    if (p.CurMap != p.Tent)
                    {
                        DebugSystem.Write(DebugItemType.Error, $"[Tent] Rejected AC62 placement: {p.CharName} is not inside their own tent.");
                        return;
                    }

                    if (p.Inv == null || slotIndex == 0 || slotIndex > 50 || p.Inv[slotIndex] == null || p.Inv[slotIndex].ItemID == 0)
                    {
                        DebugSystem.Write(DebugItemType.Error, $"[Tent] Rejected AC62 placement: Invalid slot {slotIndex} for {p.CharName}");
                        return;
                    }

                    ushort placeItemId = p.Inv[slotIndex].ItemID;
                    p.Inv.RemoveItem(slotIndex, 1, true);

                    p.Tent.PlaceItem(placeItemId, (int)x, (int)y, (int)floor, 0);

                    // Send authentic 61-byte AC 62:1 confirmation
                    SendPacket confirmation = new SendPacket();
                    confirmation.Pack8(62);
                    confirmation.Pack8(1);
                    confirmation.Pack16((ushort)p.Tent.TentObjects.Count);
                    confirmation.Pack16(placeItemId);
                    confirmation.Pack32(x);
                    confirmation.Pack32(y);
                    confirmation.Pack32(1); // Z = 1
                    confirmation.Pack8(0);  // rotate = 0
                    confirmation.PackArray(new byte[32]);
                    confirmation.Pack8((byte)floor);
                    confirmation.PackArray(new byte[9]);
                    p.Send(confirmation);

                    p.Tent.SendTentItemsToAll();

                    cGlobal.gCharacterDataBase?.SaveTentData(p);
                }
            }
            catch (Exception ex)
            {
                DebugSystem.Write(DebugItemType.Error, $"[Tent] Error in AC62 Recv1: {ex.Message}");
            }
        }
        void Recv2(Player p, RecievePacket r)
        {
            // Packet format: Index(2 bytes LE)
            try
            {
                if (p.Tent == null || p.CurMap != p.Tent)
                {
                    DebugSystem.Write(DebugItemType.Error, $"[Tent] Rejected AC62,2 pickup: {p.CharName} is not inside their own tent.");
                    return;
                }

                ushort rawIndex = r.Unpack16();
                DebugSystem.Write(DebugItemType.Error, $"[Tent] AC62,2 Pickup: Index {rawIndex} by {p.CharName}");

                int targetIndex = -1;
                if (rawIndex >= 1 && rawIndex <= p.Tent.TentObjects.Count)
                {
                    targetIndex = rawIndex - 1;
                }
                else if (rawIndex < p.Tent.TentObjects.Count)
                {
                    targetIndex = rawIndex;
                }

                if (targetIndex < 0 || p.Tent.TentObjects == null || targetIndex >= p.Tent.TentObjects.Count)
                {
                    DebugSystem.Write(DebugItemType.Error, $"[Tent] Pickup failed: Index {rawIndex} out of range (Count: {p.Tent.TentObjects?.Count ?? 0})");
                    return;
                }

                var item = p.Tent.TentObjects[targetIndex];
                if (item == null || item.ItemID == 0)
                {
                    DebugSystem.Write(DebugItemType.Error, $"[Tent] Pickup failed: Null or zero ItemID at index {targetIndex}");
                    return;
                }

                if (p.Inv == null || p.Inv.unFilledCount <= 0)
                {
                    DebugSystem.Write(DebugItemType.Error, $"[Tent] Pickup failed: Inventory is full for {p.CharName}");
                    return;
                }

                ushort itemID = item.ItemID;

                // Add item back to inventory
                int added = p.Inv.AddItem(itemID, 1, true);
                if (added <= 0)
                {
                    DebugSystem.Write(DebugItemType.Error, $"[Tent] Pickup failed: AddItem returned {added} for {p.CharName}");
                    return;
                }

                // Remove from tent
                p.Tent.RemoveItem((ushort)targetIndex);

                // Send pickup confirmation to client: [62, 2, rawIndex:2 LE]
                SendPacket confirmation = new SendPacket();
                confirmation.Pack8(62);
                confirmation.Pack8(2);
                confirmation.Pack16(rawIndex);
                p.Send(confirmation);

                // Broadcast updated items to all occupants
                p.Tent.SendTentItemsToAll();

                cGlobal.gCharacterDataBase?.SaveTentData(p);
                DebugSystem.Write(DebugItemType.Error, $"[Tent] Successfully picked up item {itemID} from index {targetIndex} (raw {rawIndex}) into inventory for {p.CharName}");
            }
            catch (Exception ex)
            {
                DebugSystem.Write(DebugItemType.Error, $"[Tent] Error in AC62 Recv2: {ex.Message}");
            }
        }
        void Recv3(Player p, RecievePacket r)
        {
            try
            {
                // Packet format: Index(2 LE) + X(4 LE) + Y(4 LE) + Floor(4 LE) + Rot?(1)
                ushort rawIndex = r.Unpack16();
                uint x = r.Unpack32();
                uint y = r.Unpack32();
                uint floor = r.Unpack32();
                byte rotation = 0;

                try { rotation = r.Unpack8(); } catch { }

                DebugSystem.Write(DebugItemType.Error, $"[Tent] AC62,3 Move: Index {rawIndex} to ({x},{y}) Flr {floor} Rot {rotation}");

                if (p.Tent != null)
                {
                    if (p.CurMap != p.Tent)
                    {
                        DebugSystem.Write(DebugItemType.Error, $"[Tent] Rejected AC62 move: {p.CharName} is not inside their own tent.");
                        return;
                    }

                    int targetIndex = -1;
                    if (rawIndex >= 1 && rawIndex <= p.Tent.TentObjects.Count)
                    {
                        targetIndex = rawIndex - 1;
                    }
                    else if (rawIndex < p.Tent.TentObjects.Count)
                    {
                        targetIndex = rawIndex;
                    }

                    if (targetIndex >= 0 && targetIndex < p.Tent.TentObjects.Count)
                    {
                        p.Tent.MoveItem((ushort)targetIndex, (int)x, (int)y, (int)floor, rotation);

                        // Send move confirmation to client: [62, 3, rawIndex:2, x:4, y:4, z:4, rot:1]
                        SendPacket confirmation = new SendPacket();
                        confirmation.Pack8(62);
                        confirmation.Pack8(3);
                        confirmation.Pack16(rawIndex);
                        confirmation.Pack32(x);
                        confirmation.Pack32(y);
                        confirmation.Pack32(1); // Z = 1 (Elevation layer MUST be 1)
                        confirmation.Pack8(rotation);
                        p.Send(confirmation);

                        p.Tent.SendTentItemsToAll();

                        cGlobal.gCharacterDataBase?.SaveTentData(p);
                    }
                    else
                    {
                        DebugSystem.Write(DebugItemType.Error, $"[Tent] Move failed: Index {rawIndex} out of range (Count: {p.Tent.TentObjects.Count})");
                    }
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
