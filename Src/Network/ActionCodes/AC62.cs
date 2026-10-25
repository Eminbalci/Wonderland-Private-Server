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
            r.SetPtr(6); // Skip packet header (4B), ActionCode (1B), SubCode (1B)
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
                // Packet format: Index(2 LE) + X(4 LE) + Y(4 LE) + Z(4 LE) + Rot?(1)
                ushort rawIndex = r.Unpack16();
                uint x = r.Unpack32();
                uint y = r.Unpack32();
                uint z = r.Unpack32();
                if (z == 0) z = 1;
                byte rotation = 0;

                try { rotation = r.Unpack8(); } catch { }

                DebugSystem.Write(DebugItemType.Error, $"[Tent] AC62,3 Move: Index {rawIndex} to ({x},{y}) Z {z} Rot {rotation}");

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
                        p.Tent.MoveItem((ushort)targetIndex, (int)x, (int)y, (int)z, rotation);

                        // Send move confirmation to client: [62, 3, rawIndex:2, x:4, y:4, z:4, rot:1]
                        SendPacket confirmation = new SendPacket();
                        confirmation.Pack8(62);
                        confirmation.Pack8(3);
                        confirmation.Pack16(rawIndex);
                        confirmation.Pack32(x);
                        confirmation.Pack32(y);
                        confirmation.Pack32(z); // Z = 1 (Elevation layer MUST be 1)
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
            try
            {
                // Client drops an accessory tool (e.g. Stone Knife 38058, Wooden Saw 38060) onto a tent appliance (Workbench 38049)
                // Packet format from aLogin disassembly: [AC 62, Sub 4, slotIndex: 1B, rawTargetIndex: 2B LE]
                byte slotIndex = r.Unpack8();
                ushort rawTargetIndex = r.Unpack16();

                DebugSystem.Write(DebugItemType.Error, $"[Tent] AC62,4 AddToolAccessory: Slot={slotIndex}, TargetIndex={rawTargetIndex} by {p.CharName}");

                if (p.Tent == null || p.CurMap != p.Tent)
                {
                    DebugSystem.Write(DebugItemType.Error, $"[Tent] Rejected AC62,4: {p.CharName} is not inside their own tent.");
                    return;
                }

                if (slotIndex < 1 || slotIndex > 50 || p.Inv == null || p.Inv[slotIndex] == null || p.Inv[slotIndex].ItemID == 0)
                {
                    DebugSystem.Write(DebugItemType.Error, $"[Tent] Rejected AC62,4: Invalid slot {slotIndex} for {p.CharName}");
                    return;
                }

                ushort toolItemId = p.Inv[slotIndex].ItemID;

                // Validate target tent appliance (1-based index)
                int targetIdx = -1;
                if (rawTargetIndex >= 1 && rawTargetIndex <= p.Tent.TentObjects.Count)
                {
                    targetIdx = rawTargetIndex - 1;
                }
                else if (rawTargetIndex < p.Tent.TentObjects.Count)
                {
                    targetIdx = rawTargetIndex;
                }

                if (targetIdx < 0 || targetIdx >= p.Tent.TentObjects.Count)
                {
                    DebugSystem.Write(DebugItemType.Error, $"[Tent] Rejected AC62,4: Host index {rawTargetIndex} out of range (count={p.Tent.TentObjects.Count})");
                    return;
                }

                var targetItem = p.Tent.TentObjects[targetIdx];
                if (targetItem == null || targetItem.ItemID == 0)
                {
                    DebugSystem.Write(DebugItemType.Error, $"[Tent] Rejected AC62,4: Host item at index {targetIdx} is null or empty");
                    return;
                }

                // Verify target is an appliance that accepts tools (Workbench 38049)
                if (targetItem.ItemID != 38049)
                {
                    DebugSystem.Write(DebugItemType.Error, $"[Tent] Rejected AC62,4: Target item #{targetItem.ItemID} is not a Work Platform / Workbench.");
                    return;
                }

                // Verify tool is not already attached to this appliance
                if (targetItem.HasTool(toolItemId))
                {
                    DebugSystem.Write(DebugItemType.Error, $"[Tent] Rejected AC62,4: Tool #{toolItemId} is already attached to host #{targetItem.ItemID}");
                    return;
                }

                if (targetItem.AttachedTools.Count >= 10)
                {
                    DebugSystem.Write(DebugItemType.Error, $"[Tent] Rejected AC62,4: Host #{targetItem.ItemID} has reached max accessory slots (10)");
                    return;
                }

                // Remove 1x tool from inventory
                p.Inv.RemoveItem(slotIndex, 1, true);

                // Attach tool to target appliance
                targetItem.AddAttachedTool(toolItemId);

                // Assign accessory index: total objects + total accessories
                ushort accessoryIndex = (ushort)(p.Tent.CalculateTotalItemCount());

                // Send AC 62:5 confirmation to client: [62, 5, accessoryIndex:2 LE, toolItemId:2 LE, hostIndex:2 LE]
                SendPacket confirmation = new SendPacket();
                confirmation.Pack8(62);
                confirmation.Pack8(5);
                confirmation.Pack16(accessoryIndex);
                confirmation.Pack16(toolItemId);
                confirmation.Pack16(rawTargetIndex);
                p.Send(confirmation);

                // Broadcast updated tent layout to ensure all occupants stay in sync
                p.Tent.SendTentItemsToAll();

                // Persist changes to database
                cGlobal.gCharacterDataBase?.SaveTentData(p);
                p.SaveCharacterData();

                DebugSystem.Write(DebugItemType.Error, $"[Tent] Successfully attached tool #{toolItemId} to Workbench #{targetItem.ItemID} (AccessoryIndex={accessoryIndex}, HostIndex={rawTargetIndex}) for {p.CharName}");
            }
            catch (Exception ex)
            {
                DebugSystem.Write(DebugItemType.Error, $"[Tent] Error in AC62 Recv4: {ex}");
            }
        }
    }
}
