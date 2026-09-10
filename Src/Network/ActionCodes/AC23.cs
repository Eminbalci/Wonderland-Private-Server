using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Game;
using Game.Code;
using Game.PlayerRelated;
using Network;
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
                case 15: Recv15(r, p); break;// Quick HP/MP refill / Open tent
                case 25: Recv25(r, p); break;// Request IM Point Balance
                case 26: Recv26(r, p); break;// Buy Item from Mall
                case 54: Recv54(r, p); break;// Request Item Mall Catalog List
                case 77: Recv77(r, p); break;// Request Player Stall / Market Listings
                case 96: Recv96(r, p); break;// Use item from inventory (double-click/use)
                case 124: Recv124(r, p); break;// confirm destroy 
                default: DebugSystem.Write($"AC {p.A},{p.B} has not been coded"); break;
            }
        }

        void Recv96(Player p, RecievePacket r)
        {
            try
            {
                byte slot = r.Unpack8();
                if (slot < 1 || slot > 50) return;
                var item = p.Inv[slot];
                if (item == null || item.ItemID == 0) return;

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
                if (itemInfo != null && itemInfo.Equippos > 0)
                {
                    p.WearEQ(slot);
                    return;
                }

                // 3. Special Quest Items & Star Currency (#30025)
                if (itemId == 30025) // Star
                {
                    p.Send(Tools.FromFormat("bbbs", 23, 57, 0, "Stars are special quest tokens used for skill learning, resets, and rebirth quests."));
                    return;
                }

                // 4. Pet Vouchers / Summon Cards / Quest Item Vouchers
                if (itemName.ToLower().Contains("vouche") || itemName.ToLower().Contains("card") || (itemInfo != null && itemInfo.ItemType == 14))
                {
                    p.Inv.RemoveItem(slot, 1);
                    p.Send(Tools.FromFormat("bbbs", 23, 57, 0, $"Used {itemName}! Pet voucher successfully redeemed."));
                    DebugSystem.Write($"[AC23.Recv96] {p.CharName} used pet voucher {itemName} (#{itemId}).");
                    return;
                }

                // 5. Food / Potions / Healing / Consumable items
                int hpGain = 0;
                int spGain = 0;
                if (itemInfo != null)
                {
                    if (itemInfo.StatusType != null && itemInfo.StatusUp != null)
                    {
                        for (int i = 0; i < Math.Min(itemInfo.StatusType.Length, itemInfo.StatusUp.Length); i++)
                        {
                            if (itemInfo.StatusType[i] == 207) hpGain += itemInfo.StatusUp[i];
                            else if (itemInfo.StatusType[i] == 208) spGain += itemInfo.StatusUp[i];
                        }
                    }
                }

                // Fallback for standard food / potions if not in ItemDat
                if (hpGain == 0 && spGain == 0)
                {
                    if (itemId >= 28001 && itemId <= 28050) // Fruit / Food
                    {
                        hpGain = 60;
                        spGain = 40;
                    }
                    else if (itemId >= 30201 && itemId <= 30210) // Potions
                    {
                        hpGain = 150;
                        spGain = 80;
                    }
                    else if (itemId >= 23001 && itemId <= 23060) // Syrups / Candies
                    {
                        hpGain = 100;
                        spGain = 100;
                    }
                }

                if (hpGain > 0 || spGain > 0)
                {
                    if (p.Eqs != null)
                    {
                        p.Eqs.CurHP = Math.Min(p.Eqs.FullHP, p.Eqs.CurHP + hpGain);
                        p.Eqs.CurSP = Math.Min(p.Eqs.FullSP, p.Eqs.CurSP + spGain);
                        p.Eqs.Send8_1();
                    }

                    p.Inv.RemoveItem(slot, 1);
                    p.Send(Tools.FromFormat("bbbs", 23, 57, 0, $"Used {itemName}! Recovered {hpGain} HP and {spGain} SP."));
                    p.SaveCharacterData();
                    DebugSystem.Write($"[AC23.Recv96] {p.CharName} consumed {itemName} (#{itemId}) at slot {slot}: +{hpGain} HP, +{spGain} SP.");
                    return;
                }

                // Generic Consumable fallback
                p.Inv.RemoveItem(slot, 1);
                p.Send(Tools.FromFormat("bbbs", 23, 57, 0, $"Used {itemName}!"));
                p.SaveCharacterData();
                DebugSystem.Write($"[AC23.Recv96] {p.CharName} used generic item {itemName} (#{itemId}) at slot {slot}.");
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
                // Dispatch catalog and point balance so client flag 0x5698 is active
                ItemMallManager.SendCatalog(p);
                ItemMallManager.SendPointBalance(p);

                DebugSystem.Write($"[AC23.Recv54] Item Mall catalog and balance dispatched to {p.CharName}");
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
                byte pos = r.Unpack8();
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

        /// <summary>
        /// AC 23:15 - Quick HP/MP Refill Button & Tent (Confirmed via hpmpdoldurmabutonu.pcapng)
        /// C->S: 17 0f <slot> <count> <target(2B: 0=player, >0=pet)>
        /// S->C: 17 d0 01 <slot> <remainingAmmt> 00 00 00 (Remaining quantity update)
        /// S->C: 05 01 <charId> <curHp> (HP/SP visual update)
        /// S->C: 08 01 ... (Player stat sync) or 08 02 ... (Pet stat sync)
        /// </summary>
        void Recv15(Player p, RecievePacket r)
        {
            try
            {
                byte pos = r.Unpack8();
                byte count = 1;
                try { count = r.Unpack8(); } catch { count = 1; }
                if (count == 0) count = 1;

                ushort target = 0;
                try { target = r.Unpack16(); } catch { target = 0; }

                if (pos < 1 || pos > 50) return;
                var item = p.Inv[pos];
                if (item == null || item.ItemID == 0) return;

                // 1. Tent
                if (item.ItemID == 36002)
                {
                    p.Tent.Open();
                    return;
                }

                // 2. Quick HP/MP Consumable Recovery
                int hpGain = 0;
                int spGain = 0;
                var itemInfo = cGlobal.ItemDatManager?.GetItemByID(item.ItemID);
                if (itemInfo != null && itemInfo.StatusType != null && itemInfo.StatusUp != null)
                {
                    for (int i = 0; i < Math.Min(itemInfo.StatusType.Length, itemInfo.StatusUp.Length); i++)
                    {
                        if (itemInfo.StatusType[i] == 207) hpGain += itemInfo.StatusUp[i];
                        else if (itemInfo.StatusType[i] == 208) spGain += itemInfo.StatusUp[i];
                    }
                }
                if (hpGain == 0 && spGain == 0)
                {
                    if (item.ItemID >= 28001 && item.ItemID <= 28050) { hpGain = 60; spGain = 40; }
                    else if (item.ItemID >= 30201 && item.ItemID <= 30210) { hpGain = 150; spGain = 80; }
                    else if (item.ItemID >= 23001 && item.ItemID <= 23060) { hpGain = 100; spGain = 100; }
                    else { hpGain = 50; spGain = 50; }
                }

                if (hpGain > 0 || spGain > 0)
                {
                    // Check target: 0 = Player, >0 = Pet Slot
                    if (target == 0)
                    {
                        if (p.Eqs != null)
                        {
                            p.Eqs.CurHP = Math.Min(p.Eqs.FullHP, p.Eqs.CurHP + hpGain * count);
                            p.Eqs.CurSP = Math.Min(p.Eqs.FullSP, p.Eqs.CurSP + spGain * count);
                            p.Eqs.Send8_1();
                        }
                        p.Send(Tools.FromFormat("bbdw", 5, 1, p.CharID, (ushort)(p.Eqs != null ? p.Eqs.CurHP : 0)));
                    }
                    else if (p.PlayerPets != null)
                    {
                        byte petSlot = (byte)target;
                        Player.PlayerPetData pet = null;
                        if (p.PlayerPets.TryGetValue(petSlot, out var pPet)) pet = pPet;
                        else if (petSlot > 0 && p.PlayerPets.TryGetValue((byte)(petSlot - 1), out var pPet0)) pet = pPet0;
                        else if (p.PlayerPets.Values.Any(x => x.IsBattle)) pet = p.PlayerPets.Values.FirstOrDefault(x => x.IsBattle);

                        if (pet != null)
                        {
                            pet.HP = Math.Min(pet.MaxHP, pet.HP + hpGain * count);
                            pet.SP = Math.Min(pet.MaxSP, pet.SP + spGain * count);
                            p.Send(Tools.FromFormat("bbbbdd", 8, 2, 25, petSlot, (uint)pet.HP, 0));
                            p.Send(Tools.FromFormat("bbbbdd", 8, 2, 26, petSlot, (uint)pet.SP, 0));
                            p.Send(Tools.FromFormat("bbdw", 5, 1, pet.PetID, (ushort)pet.HP));
                        }
                    }

                    // Deduct item count or remove
                    if (item.Ammt > count)
                    {
                        item.Ammt -= count;
                        SendPacket s208 = new SendPacket();
                        s208.Pack8(23);
                        s208.Pack8(208);
                        s208.Pack8(1);
                        s208.Pack8(pos);
                        s208.Pack8(item.Ammt);
                        s208.Pack8(0);
                        s208.Pack8(0);
                        s208.Pack8(0);
                        p.Send(s208);
                    }
                    else
                    {
                        p.Inv.RemoveItem(pos, item.Ammt);
                    }

                    p.SaveCharacterData();
                    DebugSystem.Write($"[AC23.Recv15] Quick refill applied by {p.CharName}: Slot {pos}, Target {target}, +{hpGain * count} HP, +{spGain * count} SP");
                }
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