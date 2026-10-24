using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Game;
using Game.Code;
using Game.PlayerRelated;
using Network;
using Game.Maps;
using wlo.pserver.core.Game;

namespace Network.ActionCodes
{
    public class AC23 : AC
    {
        public override int ID { get { return 23; } }
        public override void ProcessPkt(Player r, RecievePacket p)
        {
            p.SetPtr(6);
            switch (p.B)
            {
                // case 1: Recv1(ref r, p); break;
                case 2: Recv2(r, p); break; // Get item ground
                case 3: Recv3(r, p); break; // drop item ground
                case 10: Recv10(r, p); break;// move item inv
                case 11: Recv11(r, p); break;// item selected to wear in inv
                case 12: Recv12(r, p); break;// item selected to remove
                case 14: Recv14(r, p); break;// Compound synthesis
                case 17: RecvPetEquipment(r, p, false); break; // Pet wear
                case 18: RecvPetEquipment(r, p, true); break; // Pet remove
                case 15: Recv15(r, p); break;// Quick HP/MP refill / Open tent
                case 25: Recv25(r, p); break;// Request IM Point Balance
                case 26: Recv26(r, p); break;// Buy Item from Mall
                case 30: Recv30(r, p); break; // Client native Private Shop Open
                case 31: Recv31(r, p); break; // Client native Private Shop Close
                case 32: Recv32(r, p); break; // Client native Private Shop Browse / Open Shop Form
                case 33: Recv33(r, p); break; // Client native Private Shop Purchase Confirmation
                case 63: Recv63(r, p); break; // Client native Pet Stall Purchase Confirmation (0x3F)
                case 68: Recv68(r, p); break; // Client native Utensil/Vehicle Stall Purchase Confirmation (0x44)
                case 54: Recv54(r, p); break; // Client clearing fishing / gathering state (0x17:0x36)
                case 75: RecvPack(r, p); break; // Original pack/egg native double-click
                case 128: RecvPack(r, p); break; // Later packs native double-click
                case 77: Recv77(r, p); break;// Request Player Stall / Market Listings
                case 96: Recv96(r, p); break;// Use item from inventory (double-click/use)
                case 124: Recv124(r, p); break;// confirm destroy 
                default: DebugSystem.Write($"AC {p.A},{p.B} has not been coded"); break;
            }
        }

        void RecvPetEquipment(Player p, RecievePacket r, bool remove)
        {
            if (r.Buffer.Length - r.GetPtr() != (remove ? 3 : 2)) return;
            byte petSlot = r.Unpack8();
            byte from = r.Unpack8();
            bool changed = remove
                ? p.Inv.TryUnequipPet(petSlot, from, r.Unpack8())
                : p.Inv.TryEquipPet(petSlot, from);
            if (changed) p.SaveCharacterData();
        }

        void RecvPack(Player p, RecievePacket r)
        {
            // Both native commands carry a UInt16 inventory slot, not an item ID.
            if (r.Buffer.Length - r.GetPtr() != 2) return;
            ushort slot = r.Unpack16();
            if (slot < 1 || slot > 50) return;
            if (!GachaManager.TryOpen(p, (byte)slot))
                p.SendHeadBanner("This pack has no configured rewards. Item retained.");
        }

        void Recv96(Player p, RecievePacket r)
        {
            try
            {
                if (r.Buffer.Length - r.GetPtr() != 1) return;
                byte slot = r.Unpack8();
                if (slot < 1 || slot > 50) return;
                var item = p.Inv[slot];
                if (item == null || item.ItemID == 0) return;

                if (Game.PetRelated.PetVoucherManager.TryRedeem(p, slot)) return;
                if (GachaManager.TryOpen(p, slot)) return;

                ushort itemId = item.ItemID;
                string itemName = !string.IsNullOrEmpty(item.Name) ? item.Name.Trim('\0', ' ') : $"Item #{itemId}";

                // 1. Tent (36002)
                if (itemId == 36002)
                {
                    p.Tent.Open();
                    return;
                }

                // 2. Equipable items
                var itemInfo = cGlobal.ItemDatManager?.GetItemByID(itemId);
                if (itemInfo != null && itemInfo.Equippos >= 1 && itemInfo.Equippos <= 6)
                {
                    p.WearEQ(slot);
                    p.SaveCharacterData();
                    return;
                }

                // 3. Special Quest Items & Star Currency (#30025)
                if (itemId == 30025) // Star
                {
                    p.Send(Tools.FromFormat("bbbs", 23, 57, 0, "Stars are special quest tokens used for skill learning, resets, and rebirth quests."));
                    return;
                }

                if (!TryUseRecoveryItem(p, slot, 1, 0))
                    p.SendHeadBanner("Select a suitable target to use this item.");
            }
            catch (Exception t)
            {
                DebugSystem.Write($"[AC23.Recv96] Error using item: {t.Message}");
            }
        }

        void Recv54(Player p, RecievePacket r)
        {
            try
            {
                // AC 23:54 (0x17:0x36) = Client clearing fishing / gathering state (LocalPlayer + 0x20b5 = 0)
                p.Fishing = false;
                Game.Crafting.GatheringManager.StopGathering(p);
                DebugSystem.Write($"[AC23.Recv54] Cleared fishing/gathering state for {p.CharName}");
            }
            catch (Exception t) { Console.WriteLine(t); }
        }

        /// <summary>
        /// Send item mall catalog to player. Called on map-enter (authentic server behavior).
        /// S->C: A=54, B=201(0xC9), [0x00, count(1B), items(3B each)]
        /// Delegates to ItemMallManager.SendCatalog.
        /// </summary>
        public static void SendCatalog(Player p)
        {
            Game.PlayerRelated.ItemMallManager.SendCatalog(p);
        }

        void Recv77(Player p, RecievePacket r)
        {
            try
            {
                // AC 23:77 = Client requesting stall/player-shop list
                // Response sequence (pcap confirmed):
                //   S->C [23, 4, 0]    = stall list, 0 active stalls
                //   S->C [23, 102]     = end of stall list
                // Without these, client shows "Can't load list" indefinitely.
                SendPacket pkt = new SendPacket();
                pkt.Pack8(23);
                pkt.Pack8(4);
                pkt.Pack8(0); // stall count = 0
                p.Send(pkt);

                SendPacket endPkt = new SendPacket();
                endPkt.Pack8(23);
                endPkt.Pack8(102);
                p.Send(endPkt);

                DebugSystem.Write($"[AC23.Recv77] Sent empty stall list to {p.CharName}");
            }
            catch (Exception t) { Console.WriteLine(t); }
        }

        void Recv30(Player p, RecievePacket r)
        {
            try
            {
                // AC 23:30 = Client native Private Shop setup packet
                // Payload: [Title Len (1B)] [Title String] [Count (1B)] { [InvSlot (1B)] [Qty (1B)] [Price (4B)] }
                if (p == null || r == null) return;

                string shopTitle = r.UnpackString();
                if (string.IsNullOrWhiteSpace(shopTitle)) shopTitle = "Private Shop";
                byte count = r.Unpack8();

                var stallItems = new List<Game.Experimental.StallItem>();
                for (int i = 0; i < count && i < 10; i++)
                {
                    if (r.Buffer.Length - r.GetPtr() < 6) break;
                    byte invSlot = r.Unpack8();
                    byte qty = r.Unpack8();
                    uint price = r.Unpack32();

                    var invItem = p.Inv?[invSlot];
                    ushort itemId = invItem?.ItemID ?? 0;
                    string itemName = null;
                    try { itemName = invItem?.Name?.Trim('\0', ' '); } catch { }
                    if (string.IsNullOrWhiteSpace(itemName))
                        itemName = Game.Battle.MonsterDropManager.ResolveItemName(itemId);
                    if (string.IsNullOrWhiteSpace(itemName) || itemName == "Item #0")
                    {
                        if (itemId == 0) itemId = (ushort)(10000 + invSlot);
                        itemName = $"Merchandise #{itemId}";
                    }

                    int itemQty = qty > 0 ? qty : 1;
                    if (invItem != null && invItem.Ammt > 0)
                    {
                        itemQty = Math.Min((int)invItem.Ammt, itemQty);
                    }

                    stallItems.Add(new Game.Experimental.StallItem(
                        (byte)stallItems.Count,
                        itemId,
                        itemName,
                        itemQty,
                        price,
                        invSlot
                    ));
                }

                if (stallItems.Count == 0)
                {
                    stallItems.Add(new Game.Experimental.StallItem(0, 10001, "Superior Healing Draught", 50, 120, 0));
                }

                var stall = Game.Experimental.ExperimentalFeaturesManager.Instance.PlayerStallBazaar?.OpenStall(p, shopTitle, stallItems);
                if (stall != null)
                {
                    p.SendSystemMessage($"[Private Shop] Your shop '{stall.StallName}' is now open for business!");
                    DebugSystem.Write($"[AC23.Recv30] Opened native private shop '{stall.StallName}' for {p.CharName} with {stall.Items.Count} items.");
                }
                else
                {
                    p.SendSystemMessage("[Private Shop] Failed to open shop.");
                }
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[AC23.Recv30] Exception opening stall: {ex.Message}\n{ex.StackTrace}");
            }
        }

        void Recv31(Player p, RecievePacket r)
        {
            try
            {
                if (p == null) return;
                Game.Experimental.ExperimentalFeaturesManager.Instance.PlayerStallBazaar?.ClosePersonalStall(p);
                p.SendSystemMessage("[Private Shop] Your shop has been closed.");
                DebugSystem.Write($"[AC23.Recv31] Closed native private shop for {p.CharName}");
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[AC23.Recv31] Exception closing stall: {ex.Message}");
            }
        }

        void Recv32(Player p, RecievePacket r)
        {
            try
            {
                if (p == null || r == null) return;
                if (r.Buffer.Length - r.GetPtr() < 4) return;
                uint vendorCharId = r.Unpack32();

                var bazaar = Game.Experimental.ExperimentalFeaturesManager.Instance.PlayerStallBazaar;
                var stall = bazaar?.GetStall(vendorCharId);

                if (stall == null)
                {
                    p.SendSystemMessage("[Private Shop] This stall is no longer available.");
                    DebugSystem.Write($"[AC23.Recv32] {p.CharName} requested stall {vendorCharId}, but stall was not found.");
                    return;
                }

                // 1. Send type-specific authentic browse viewport packet
                if (stall.StallType == Game.Experimental.StallType.Pet)
                {
                    var petBrowsePkt = Game.Experimental.PlayerStallBazaarEngine.BuildPetStallBrowse113Packet(stall);
                    if (petBrowsePkt != null) p.Send(petBrowsePkt);
                }
                else if (stall.StallType == Game.Experimental.StallType.VehicleUtensil)
                {
                    var utensilBrowsePkt = Game.Experimental.PlayerStallBazaarEngine.BuildUtensilStallBrowse133Packet(stall);
                    if (utensilBrowsePkt != null) p.Send(utensilBrowsePkt);
                }
                else
                {
                    var browse33Pkt = Game.Experimental.PlayerStallBazaarEngine.BuildStallBrowse33Packet(stall);
                    if (browse33Pkt != null) p.Send(browse33Pkt);
                }

                // 2. Also send fallback AC 20:15 and AC 56:3
                var browse20Pkt = Game.Experimental.PlayerStallBazaarEngine.BuildStallBrowsePacket(stall);
                if (browse20Pkt != null) p.Send(browse20Pkt);

                var catalogPkt = Game.Experimental.PlayerStallBazaarEngine.BuildStallCatalogPacket(stall);
                if (catalogPkt != null) p.Send(catalogPkt);

                // 3. Send structured system message catalog and chat announcement
                string stockTypeStr = stall.IsInfinite ? "INFINITE STOCK" : "LIMITED STOCK";
                string typeHeader = stall.StallType == Game.Experimental.StallType.Pet ? "PET BAZAAR" : (stall.StallType == Game.Experimental.StallType.VehicleUtensil ? "VEHICLE & UTENSIL BAZAAR" : "BAZAAR");
                p.SendSystemMessage($"==================================================");
                p.SendSystemMessage($"[{typeHeader}] {stall.StallName.ToUpper()}");
                p.SendSystemMessage($"Merchant: {stall.VendorName} | Mode: {stockTypeStr}");
                p.SendSystemMessage($"--------------------------------------------------");
                if (stall.Items.Count == 0)
                {
                    p.SendSystemMessage("This stall is currently out of stock.");
                }
                else
                {
                    foreach (var item in stall.Items)
                    {
                        string qtyStr = (stall.IsInfinite || item.IsInfinite) ? "[∞ Stock]" : $"[Stock: {item.Quantity}]";
                        p.SendSystemMessage($"  Slot #{item.SlotIndex}: {item.ItemName} (#{item.ItemId}) {qtyStr} - {item.PriceGold:N0} Gold");
                    }
                    p.SendSystemMessage($"--------------------------------------------------");
                    p.SendSystemMessage($"Click an item in shop window, or type: :buystall <slot 0..{stall.Items.Count - 1}> [quantity]");
                }
                p.SendSystemMessage($"==================================================");

                DebugSystem.Write($"[AC23.Recv32] {p.CharName} opened stall '{stall.StallName}' (Vendor ID: {vendorCharId}, Type: {stall.StallType}) with {stall.Items.Count} items.");
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[AC23.Recv32] Exception handling stall browse: {ex.Message}\n{ex.StackTrace}");
            }
        }

        void Recv33(Player p, RecievePacket r)
        {
            try
            {
                if (p == null || r == null) return;
                if (r.Buffer.Length - r.GetPtr() < 4) return;
                uint vendorCharId = r.Unpack32();

                var bazaar = Game.Experimental.ExperimentalFeaturesManager.Instance.PlayerStallBazaar;
                var stall = bazaar?.GetStall(vendorCharId);

                if (stall == null)
                {
                    p.SendHeadBanner("This stall is no longer available.");
                    p.SendSystemMessage("[Private Shop] This stall is no longer available.");
                    return;
                }

                int purchasedCount = 0;
                while (r.Buffer.Length - r.GetPtr() >= 8)
                {
                    byte slotIndex = r.Unpack8();
                    byte buyQty = r.Unpack8();
                    ushort itemId = r.Unpack16();
                    uint unitPrice = r.Unpack32();

                    if (buyQty == 0) buyQty = 1;

                    bool success = bazaar.PurchaseItem(p, vendorCharId, slotIndex, buyQty);
                    if (success)
                    {
                        purchasedCount++;
                    }
                }

                if (purchasedCount > 0)
                {
                    p.SaveCharacterData();

                    var updatedStall = bazaar.GetStall(vendorCharId);
                    if (updatedStall != null && updatedStall.Items.Count > 0)
                    {
                        var refreshPkt = Game.Experimental.PlayerStallBazaarEngine.BuildStallBrowse33Packet(updatedStall);
                        if (refreshPkt != null) p.Send(refreshPkt);
                    }
                }
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[AC23.Recv33] Exception handling stall purchase: {ex.Message}\n{ex.StackTrace}");
            }
        }

        void Recv63(Player p, RecievePacket r)
        {
            try
            {
                // AC 23:63 (0x3F) - Pet Stall Purchase Confirmation (from Form_BuyPet1..3Stall_1)
                // C->S: [VendorCharId: 4B] { [SelectedSlot: 1B] }
                if (p == null || r == null) return;
                if (r.Buffer.Length - r.GetPtr() < 4) return;
                uint vendorCharId = r.Unpack32();

                var bazaar = Game.Experimental.ExperimentalFeaturesManager.Instance.PlayerStallBazaar;
                var stall = bazaar?.GetStall(vendorCharId);
                if (stall == null)
                {
                    p.SendHeadBanner("This pet stall is no longer available.");
                    return;
                }

                while (r.Buffer.Length - r.GetPtr() >= 1)
                {
                    byte slotIndex = r.Unpack8();
                    bazaar.PurchaseItem(p, vendorCharId, slotIndex, 1);
                }
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[AC23.Recv63] Error handling pet purchase: {ex.Message}");
            }
        }

        void Recv68(Player p, RecievePacket r)
        {
            try
            {
                // AC 23:68 (0x44) - Utensil/Vehicle Stall Purchase Confirmation (from Form_Buy1..3Utensil_1)
                // C->S: [VendorCharId: 4B] { [SlotIndex: 1B] [Qty: 1B] }
                if (p == null || r == null) return;
                if (r.Buffer.Length - r.GetPtr() < 4) return;
                uint vendorCharId = r.Unpack32();

                var bazaar = Game.Experimental.ExperimentalFeaturesManager.Instance.PlayerStallBazaar;
                var stall = bazaar?.GetStall(vendorCharId);
                if (stall == null)
                {
                    p.SendHeadBanner("This vehicle/utensil stall is no longer available.");
                    return;
                }

                while (r.Buffer.Length - r.GetPtr() >= 1)
                {
                    byte slotIndex = r.Unpack8();
                    byte qty = 1;
                    if (r.Buffer.Length - r.GetPtr() >= 1) qty = r.Unpack8();
                    if (qty == 0) qty = 1;

                    // Form_Buy1..3Utensil_1 uses 1-based slots, map 1..3 to 0..2 if needed
                    if (slotIndex > 0 && stall.Items.All(i => i.SlotIndex != slotIndex) && stall.Items.Any(i => i.SlotIndex == slotIndex - 1))
                    {
                        slotIndex = (byte)(slotIndex - 1);
                    }

                    bazaar.PurchaseItem(p, vendorCharId, slotIndex, qty);
                }
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[AC23.Recv68] Error handling utensil purchase: {ex.Message}");
            }
        }

        void Recv25(Player p, RecievePacket r)
        {
            try
            {
                Game.PlayerRelated.ItemMallManager.SendPointBalance(p);
            }
            catch (Exception t) { Console.WriteLine(t); }
        }

        void Recv26(Player p, RecievePacket r)
        {
            try
            {
                ushort itemId = r.Unpack16();
                byte count = 1;
                try { count = r.Unpack8(); } catch { count = 1; }
                if (count == 0) count = 1;
                Game.PlayerRelated.ItemMallManager.PurchaseItem(p, itemId, count);
                p.SaveCharacterData();
            }
            catch (Exception t) { Console.WriteLine(t); }
        }

        void Recv1(Player p, RecievePacket r)
        {
            try
            {

            }
            catch (Exception t) { Console.WriteLine(t); }
        }

        void Recv2(Player p, RecievePacket r)
        {
            try
            {
                if (p?.CurMap == null) return;
                ushort pos = 0;
                int rem = r.Buffer.Length - r.GetPtr();
                if (rem >= 2) pos = r.Unpack16();
                else if (rem == 1) pos = r.Unpack8();
                if (pos == 0) return;

                ((GameMap)p.CurMap).onItemPickup(p, pos);
                p.SaveCharacterData();
            }
            catch (Exception t) { Console.WriteLine(t); }
        }

        void Recv3(Player p, RecievePacket r)
        {
            try
            {
                byte pos = r.Unpack8();
                byte qnt = r.Unpack8();
                byte ukn = r.Unpack8();
                var item = p.Inv[pos];

                if (item != null)
                {
                    if (item.Dropable)
                    {
                        ((GameMap)p.CurMap).onItemDrop(p, pos, qnt);
                        p.SaveCharacterData();
                    }
                    else
                    {
                        // test need ASK destroy
                        SendPacket s = new SendPacket();
                        s.PackArray(new byte[] { 23, 212, 255 });
                        s.Pack8(pos);
                        s.Pack16(item.ItemID);
                        s.Pack8(qnt);
                        p.Send(s);
                    }
                }
            }
            catch (Exception t) { Console.WriteLine(t); }
        }

        void Recv10(Player p, RecievePacket r) // move item inventory
        {
            try
            {
                byte src = r.Unpack8();
                byte ammt = r.Unpack8();
                byte dst = r.Unpack8();

                if (((src > 0) && (src < 51)) && ((dst > 0) && (dst < 51)) && ((ammt > 0) && (ammt < 51)))
                {
                    p.Inv.MoveItem(src, dst, ammt);
                    p.SaveCharacterData();
                }
            }
            catch (Exception t) { Console.WriteLine(t); }
        }

        void Recv11(Player p, RecievePacket r)
        {
            try
            {
                byte loc = r.Unpack8();
                if ((loc > 0) && (loc < 51))
                {
                    p.WearEQ(loc);
                    p.SaveCharacterData();
                }
            }
            catch (Exception t) { Console.WriteLine(t); }
        }

        void Recv12(Player p, RecievePacket r) // item selected to remove
        {
            try
            {
                byte loc = r.Unpack8();
                byte dst = r.Unpack8();
                if ((loc > 0) && (loc < 7) && (dst > 0) && (dst < 51))
                {
                    p.unWearEQ(loc, dst);
                    p.SaveCharacterData();
                }
            }
            catch (Exception t) { Console.WriteLine(t); }
        }

        /// <summary>
        /// AC 23:14 - Compound Synthesis (Confirmed via yerdenitemalipcompounddaikiitemikaristirdim.pcapng)
        /// C->S: 17 0e <count=2> <slot1> <slot2>
        /// S->C: 17 09 <slot1> <amt1>, 17 09 <slot2> <amt2> (Remove ingredients)
        /// S->C: 17 08 <targetSlot> <resultItemId(2B)> <count(1B)> <28B zeros> (Add result)
        /// S->C: 17 0d <resultItemId(2B)> <count(1B)> <targetSlot> (Success notification/popup)
        /// S->C: 17 7a <charId(4B)> (Broadcast animation)
        /// </summary>
        void Recv14(Player p, RecievePacket r)
        {
            try
            {
                byte count = r.Unpack8();
                if (count < 2) return;
                byte slot1 = r.Unpack8();
                byte slot2 = r.Unpack8();

                if (slot1 < 1 || slot1 > 50 || slot2 < 1 || slot2 > 50 || slot1 == slot2) return;

                var item1 = p.Inv[slot1];
                var item2 = p.Inv[slot2];
                if (item1 == null || item1.ItemID == 0 || item2 == null || item2.ItemID == 0) return;

                ushort id1 = item1.ItemID;
                ushort id2 = item2.ItemID;

                var recipe = Game.Crafting.AlchemyManager.FindRecipe(id1, id2);
                ushort resultItemId = 27008; // Charcoal / Ash default fallback
                if (recipe != null)
                {
                    resultItemId = recipe.OutputItem;
                }
                else
                {
                    resultItemId = (id1 == id2) ? id1 : (ushort)Math.Max(id1, id2);
                }

                byte targetSlot = Math.Min(slot1, slot2);

                // Deduct ingredients (p.Inv.RemoveItem sends AC 23:9 for each removed slot)
                p.Inv.RemoveItem(slot1, 1);
                p.Inv.RemoveItem(slot2, 1);

                // Add crafted item into targetSlot
                var baseItem = cGlobal.ItemDatManager?.GetItemByID(resultItemId);
                if (baseItem == null)
                {
                    baseItem = new DataFiles.PhxItemInfo
                    {
                        ItemID = resultItemId,
                        ItemName = Encoding.ASCII.GetBytes("Item " + resultItemId),
                        cellwidth = 1,
                        cellheight = 1
                    };
                }
                var resultItem = new InvItem();
                resultItem.CopyFrom(baseItem);
                resultItem.Ammt = 1;
                p.Inv.AddItem(resultItem, targetSlot, false);

                // Send authentic AC 23:8 [17 08 <targetSlot> <resultId (2B)> <count (1B)> <28B zeros>]
                SendPacket s8 = new SendPacket();
                s8.Pack8(23);
                s8.Pack8(8);
                s8.Pack8(targetSlot);
                s8.Pack16(resultItemId);
                s8.Pack8(1);
                s8.PackArray(new byte[28]);
                p.Send(s8);

                // Send authentic AC 23:13 [17 0d <resultId (2B)> <count (1B)> <targetSlot>]
                SendPacket s13 = new SendPacket();
                s13.Pack8(23);
                s13.Pack8(13);
                s13.Pack16(resultItemId);
                s13.Pack8(1);
                s13.Pack8(targetSlot);
                p.Send(s13);

                // Broadcast synthesis visual effect AC 23:122 [17 7a <charId (4B)>]
                SendPacket s122 = new SendPacket();
                s122.Pack8(23);
                s122.Pack8(122);
                s122.Pack32(p.CharID);
                p.CurMap?.Broadcast(s122);

                p.SaveCharacterData();
                DebugSystem.Write($"[AC23.Recv14] {p.CharName} compounded slot {slot1} (#{id1}) + slot {slot2} (#{id2}) -> #{resultItemId} at slot {targetSlot}");
            }
            catch (Exception t) { DebugSystem.Write(new ExceptionData(t)); }
        }

        // Both native use-item commands share the same quantity and target rules.
        bool TryUseRecoveryItem(Player p, byte slot, byte requestedCount, ushort target)
        {
            if (slot < 1 || slot > 50 || requestedCount == 0 || target > 4) return false;
            lock (p.Inv.SyncRoot)
            {
                var item = p.Inv[slot];
                if (item.ItemID == 0 || item.Ammt == 0 || item.isLocked) return false;
                var info = cGlobal.ItemDatManager?.GetItemByID(item.ItemID);
                if (info == null || (info.Equippos >= 1 && info.Equippos <= 6)) return false;
                if (info.StatusType != null && info.StatusType.Contains((ushort)64))
                    return Game.PetRelated.PetAmityManager.TryFeedPet(p, slot, requestedCount, (byte)target);
                int hpGain = 0, spGain = 0;
                if (info.StatusType != null && info.StatusUp != null)
                {
                    for (int i = 0; i < Math.Min(info.StatusType.Length, info.StatusUp.Length); i++)
                    {
                        int recovery = Math.Max(0, info.StatusUp[i] - 100);
                        if (info.StatusType[i] == 25 || info.StatusType[i] == 207) hpGain += recovery;
                        else if (info.StatusType[i] == 26 || info.StatusType[i] == 208) spGain += recovery;
                    }
                }
                if (hpGain <= 0 && spGain <= 0) return false;
                var pet = target == 0 ? null : p.GetClientPet((byte)target);
                if (target != 0 && pet == null) return false;
                int hp = pet == null ? p.Eqs.CurHP : pet.HP;
                int sp = pet == null ? p.Eqs.CurSP : pet.SP;
                int maxHp = pet == null ? p.Eqs.FullHP : pet.MaxHP;
                int maxSp = pet == null ? p.Eqs.FullSP : pet.MaxSP;
                if ((hpGain <= 0 || hp >= maxHp) && (spGain <= 0 || sp >= maxSp)) return false;
                byte count = (byte)Math.Min(requestedCount, item.Ammt);
                var consumed = p.Inv.RemoveItem(slot, count);
                if (consumed == null) return false;
                hp = (int)Math.Min(maxHp, (long)hp + Math.Max(0, hpGain) * (long)consumed.Ammt);
                sp = (int)Math.Min(maxSp, (long)sp + Math.Max(0, spGain) * (long)consumed.Ammt);
                if (pet == null)
                {
                    p.Eqs.CurHP = hp;
                    p.Eqs.CurSP = sp;
                    p.Eqs.Send8_1();
                }
                else
                {
                    pet.HP = hp;
                    pet.SP = sp;
                    Game.QuestRelated.QuestManager.SendPetProgression(p, pet);
                }
                // Native AC23:15 plays sound\wav0152.wav after successful use.
                p.Send(Tools.FromFormat("bb", 23, 15));
                p.SaveCharacterData();
                return true;
            }
        }

        void Recv15(Player p, RecievePacket r)
        {
            try
            {
                if (r.Buffer.Length - r.GetPtr() < 1) return;
                byte slot = r.Unpack8();
                byte count = 1;
                try { if (r.Buffer.Length - r.GetPtr() >= 1) count = r.Unpack8(); } catch { count = 1; }
                ushort target = 0;
                try { if (r.Buffer.Length - r.GetPtr() >= 2) target = r.Unpack16(); } catch { target = 0; }

                if (slot < 1 || slot > 50) return;
                var item = p.Inv[slot];
                if (item == null || item.ItemID == 0) return;
                ushort itemId = item.ItemID;

                // 1. Pet Vouchers & Gacha
                if (Game.PetRelated.PetVoucherManager.TryRedeem(p, slot, count, target)) return;
                if (GachaManager.IsGacha(itemId))
                {
                    if (target == 0 && count == 1) GachaManager.TryOpen(p, slot);
                    else p.SendHeadBanner("Open one gacha pack at a time on your character.");
                    return;
                }

                // 2. Tent Item (36002 or legacy 32000 or Type Tent)
                if (itemId == 36002 || itemId == 32000 || item.Type == global::DataFiles.eItemType.Tent)
                {
                    if (p.Tent != null) p.Tent.Open();
                    return;
                }

                // 3. Equipable Items (Equippos 1..6)
                var itemInfo = cGlobal.ItemDatManager?.GetItemByID(itemId);
                if (itemInfo != null && itemInfo.Equippos >= 1 && itemInfo.Equippos <= 6)
                {
                    p.WearEQ(slot);
                    p.SaveCharacterData();
                    return;
                }

                // 4. Vehicles and Mounts (Bamboo Dragonfly 34169, Raft 48010/48016, Type Vehicle)
                if (itemId == 34169 || item.Type == global::DataFiles.eItemType.Vehicle || (itemId >= 48010 && itemId <= 48045))
                {
                    if (p.ActiveVehicleID == itemId)
                    {
                        VehicleManager.DismountVehicle(p);
                    }
                    else
                    {
                        VehicleManager.MountVehicle(p, slot, itemId);
                    }
                    return;
                }

                // 5. Utility: Notepad (34038)
                if (itemId == 34038)
                {
                    NotebookManager.SendQuestJournal(p);
                    return;
                }

                // 6. Utility: Remote Control (34058 or 32075)
                if (itemId == 34058 || itemId == 32075)
                {
                    p.Send(Tools.FromFormat("bb", 29, 6));
                    return;
                }

                // 7. Utility: Return Scrolls (Kelan 30001, Teleport 34163)
                if (itemId == 30001 || itemId == 34163)
                {
                    p.Inv.RemoveItem(slot, 1);
                    WarpData warp = new WarpData { DstMap = 12000, DstX_Axis = 892, DstY_Axis = 734 };
                    p.CurMap?.Teleport(TeleportType.Regular, p, 0, warp);
                    p.SaveCharacterData();
                    return;
                }

                // 8. Consumable: EXP Potions (StatusType contains 250)
                if (itemInfo != null && itemInfo.StatusType != null && itemInfo.StatusType.Contains((ushort)250))
                {
                    p.Inv.RemoveItem(slot, 1);
                    p.Send(Tools.FromFormat("bb", 23, 15));
                    p.SendSystemMessage($"Activated EXP Potion (#{itemId}).");
                    p.SaveCharacterData();
                    return;
                }

                // 9. Consumable: Recovery items & Pet Food
                if (!TryUseRecoveryItem(p, slot, count, target))
                    p.SendHeadBanner("This item cannot benefit the selected target right now.");
            }
            catch (Exception t) { DebugSystem.Write(new ExceptionData(t)); }
        }

        void Recv124(Player p, RecievePacket r)
        {
            try
            {
                byte pos = r.Unpack8();
                byte qnt = r.Unpack8();
                byte ukn = r.Unpack8(); //??
                var item = p.Inv[pos];
                if (item != null)
                {
                    // test confirm destroy item
                    SendPacket s = new SendPacket();
                    s.PackArray(new byte[] { 23, 26 });
                    s.Pack16(item.ItemID);
                    s.Pack8(qnt);
                    p.Send(s);
                    p.Inv.RemoveItem(pos, qnt);
                    p.SaveCharacterData();
                }
            }
            catch (Exception t) { Console.WriteLine(t); }
        }
    }
}