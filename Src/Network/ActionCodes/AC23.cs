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
            switch (p.B)
            {
                // case 1: Recv1(ref r, p); break;
                case 2: Recv2(r, p); break; // Get item ground
                case 3: Recv3(r, p); break; // drop item g round
                case 10: Recv10(r, p); break;//move item inv
                case 11: Recv11(r, p); break;//item selected to wear in inv
                case 12: Recv12(r, p); break; //item selected to remove
                case 15: Recv15(r, p); break; // OPen tent
                case 25: Recv25(r, p); break; // Request IM Point Balance
                case 26: Recv26(r, p); break; // Buy Item from Mall
                case 54: Recv54(r, p); break; // Request Item Mall Catalog List
                case 77: Recv77(r, p); break; // Request Player Stall / Market Listings
                case 96: Recv96(r, p); break; // Use item from inventory (double-click/use)
                case 124: Recv124(r, p); break; //confirm destroy 
                default: Console.WriteLine("AC " + p.A + "," + p.B + " has not been coded"); break;
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
                byte src = r[2];
                byte ammt = r[3];
                byte dst = r[4];

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

                byte loc = r[2];
                if ((loc > 0) && (loc < 51))
                {
                    //TODO do any checks here to make sure we can wear item or in Equipment class
                    p.WearEQ(loc);
                    p.SaveCharacterData();
                }

            }
            catch (Exception t) { Console.WriteLine(t); }
        }
        void Recv12(Player p, RecievePacket r) //item selected to remove
        {
            try
            {
                byte loc = r[2];
                byte dst = r[3];
                if ((loc > 0) && (loc < 7) && (dst > 0) && (dst < 51))
                {
                    p.unWearEQ(loc, dst);
                    p.SaveCharacterData();
                }
            }
            catch (Exception t) { Console.WriteLine(t); }
        }
        void Recv15(Player p, RecievePacket r)
        {
            try
            {
                byte pos = r.Unpack8();
                if (p.Inv[pos].ItemID == 36002)
                {
                    p.Tent.Open();
                }
            }
            catch (Exception t) { Console.WriteLine(t); }
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