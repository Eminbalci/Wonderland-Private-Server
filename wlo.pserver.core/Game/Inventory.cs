using System;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DataFiles;
using Network;
using RCLibrary.Core.Networking;


namespace Game.Code
{
    public class Inventory
    {
        global::DataFiles.PhxItemDat ItemDat;


        Player owner;
        private readonly object mylock;
        private InvItem[] m_Items;

        public Inventory(Player src, global::DataFiles.PhxItemDat ItemDat)
        {
            owner = src;
            this.ItemDat = ItemDat;
            mylock = new object();
            m_Items = new InvItem[50];
            for (int a = 0; a < 50; a++)
                m_Items[a] = new InvItem();
        }

        public InvItem this[byte key]
        {
            get
            {
                lock (mylock) return m_Items[key - 1];
            }
        }

        #region Events/Funcs/Actions
        public Item onWearEquip(byte loc)
        {
            if ((loc > 0) && (loc < 51))
            {
                if (this[loc].ItemID > 0)
                {
                    InvItem i = new InvItem();
                    i.CopyFrom(this[loc]);
                    this[loc].Clear();
                    return i;
                }
                else
                    return null;
            }
            else
                return null;
        }

        public bool onUnEquip(Item src, byte loc, bool senddata)
        {
            if (src == null) return false;
            return (AddItem(src, loc, senddata) > 0);
        }

        public void onItemDropped_fromMap(byte loc, byte ammt)
        {
            RemoveItem(loc, ammt);
        }

        public bool onItemPickedUp_fromMap(Item item)
        {
            return (AddItem(item) > 0);
        }

        public ushort GetItemIdAtSlot(byte loc)
        {
            if (loc > 0 && loc <= 50)
            {
                lock (mylock) return this[loc].ItemID;
            }
            return 0;
        }

        public void RemoveItemAtSlot(byte loc, byte ammt = 1)
        {
            if (loc > 0 && loc <= 50)
            {
                RemoveItem(loc, ammt);
            }
        }
        #endregion


        public bool ContainsItem(ushort ItemID)
        {
            lock (mylock)
            {
                for (byte a = 1; a < 51; a++)
                {
                    if (this[a].ItemID == ItemID)
                        return true;
                }
                return false;
            }
        }
        /// <summary>
        /// Used to check if an item exists in the list
        /// </summary>
        /// <param name="ItemID"></param>
        /// <param name="slot"></param>
        /// <returns>true if exists and returns the first slot location of the item</returns>
        public bool ContainsItem(ushort ItemID, out byte slot)
        {
            lock (mylock)
            {
                for (byte a = 1; a < 51; a++)
                {
                    if (this[a].ItemID == ItemID)
                    {
                        slot = a;
                        return true;
                    }
                }
                slot = 0;
                return false;
            }
        }

        public int GetItemCount(ushort ItemID)
        {
            lock (mylock)
            {
                int total = 0;
                for (byte a = 1; a < 51; a++)
                {
                    if (this[a].ItemID == ItemID)
                    {
                        total += Math.Max(1, (int)this[a].Ammt);
                    }
                }
                return total;
            }
        }

        public bool RemoveItemById(ushort itemId, byte ammt = 1)
        {
            if (ContainsItem(itemId, out byte slot))
            {
                RemoveItem(slot, ammt);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Applies durability damage / wear to the active vehicle in inventory on movement.
        /// </summary>
        public void ApplyVehicleWear(ushort vehicleItemId, byte wearAmount = 1)
        {
            lock (mylock)
            {
                if (ContainsItem(vehicleItemId, out byte slot))
                {
                    var item = this[slot];
                    if (item != null && item.ItemID > 0)
                    {
                        int newDmg = item.Damage + wearAmount;
                        if (newDmg >= 100)
                        {
                            item.Damage = 100;
                            RemoveItem(slot, 1);

                            // Send Raft Wreck Animation (AC 15:15)
                            SendPacket wreck = new SendPacket();
                            wreck.Pack8(15);
                            wreck.Pack8(15);
                            wreck.Pack32(owner.CharID);
                            wreck.Pack16(vehicleItemId);
                            owner.Send(wreck);
                            owner.CurMap?.Broadcast(wreck);

                            // Dismount player
                            owner.ActiveVehicleID = 0;
                            owner.RideVehicle("");
                            owner.SaveCharacterData();
                            owner.SendSystemMessage("Your raft broke into pieces from wear and tear!");
                        }
                        else
                        {
                            item.Damage = (byte)newDmg;
                            owner.Send(new SendPacket(GetAC23_5()));
                        }
                    }
                }
            }
        }
        /// <summary>
        /// Clears the Inventory
        /// </summary>
        public void RemoveAll(bool force)
        {
            lock (mylock)
            {
                for (byte a = 1; a < 51; a++)
                    if (!force)
                    {
                        if (this[a].ItemID > 0)
                            RemoveItem((byte)(a), this[a].Ammt);
                    }
                    else
                        this[a].Clear();
            }
        }
        /// <summary>
        /// Removes and Item from the Inventory
        /// </summary>
        /// <param name="at"></param>
        /// <param name="ammt"></param>
        /// <param name="senddata"></param>
        /// <returns>the Item Removed</returns>
        public InvItem RemoveItem(byte at, byte ammt, bool senddata = true)
        {
            lock (mylock)
            {
                if (at < 1 || at > 50) return null;
                InvItem remItem = new InvItem();
                remItem.CopyFrom(this[at]);
                try
                {
                    if (remItem.ItemID > 0)
                    {
                        if (this[at].Ammt <= ammt)
                        {
                            this[at].Clear();
                        }
                        else
                        {
                            this[at].Ammt -= ammt;
                        }
                        if (senddata && owner != null)
                        {
                            owner.Send(Tools.FromFormat("bbbb", 23, 9, at, ammt));
                        }
                        return remItem;
                    }
                }
                catch (Exception e) { DebugSystem.Write(new ExceptionData(e)); }
                return null;
            }
        }
        public bool RemoveItem(ushort itemId, byte count = 1)
        {
            lock (mylock)
            {
                byte needed = count;
                for (byte a = 1; a < 51; a++)
                {
                    if (this[a].ItemID == itemId)
                    {
                        byte toTake = Math.Min(this[a].Ammt, needed);
                        RemoveItem(a, toTake);
                        needed -= toTake;
                        if (needed == 0) break;
                    }
                }
                return needed == 0;
            }
        }
        public void AddItem(ushort ID, byte amt)
        {
            AddItem(ID, amt, true);
        }
        public void AddItem(ushort ID, byte amt, bool sendData)
        {
            PhxItemInfo baseItem = null;
            try
            {
                if (ItemDat != null)
                {
                    baseItem = ItemDat.GetItemByID(ID);
                }
            }
            catch { }

            if (baseItem == null)
            {
                baseItem = new PhxItemInfo() { ItemID = ID, ItemName = Encoding.ASCII.GetBytes("Item " + ID), cellwidth = 1, cellheight = 1 };
            }
            InvItem i = new InvItem();
            i.CopyFrom(baseItem);
            i.Ammt = amt;
            AddItem(i, 0, sendData);
        }
        /// <summary>
        /// Adds an item to the Inventory
        /// </summary>
        /// <param name="item"></param>
        /// <param name="at">default will choose next available space or tries to add at a specific place</param>
        /// <param name="sendData">whether to send data to client</param>
        public int AddItem(Item item, byte at = 0, bool sendData = true)
        {
            lock (mylock)
            {
                if (item == null || item.ItemID == 0) return 0;

                int needed = item.Ammt;
                int addedTotal = 0;

                // Case 1: Specific slot requested
                if (at >= 1 && at <= 50)
                {
                    var target = this[at];
                    if (target.ItemID == 0)
                    {
                        target.CopyFrom(item);
                        target.Parent = 0;
                        int toPut = item.Stackable ? Math.Min(needed, 50) : 1;
                        target.Ammt = (byte)toPut;
                        addedTotal = toPut;
                    }
                    else if (target.ItemID == item.ItemID && target.SpaceLeft > 0)
                    {
                        int toStack = Math.Min(needed, target.SpaceLeft);
                        target.Ammt += (byte)toStack;
                        addedTotal = toStack;
                    }

                    if (addedTotal > 0 && sendData && owner != null)
                    {
                        SendPacket tmp = new SendPacket();
                        tmp.Pack8(23);
                        tmp.Pack8(6);
                        tmp.Pack16(item.ItemID);
                        tmp.Pack8((byte)addedTotal);
                        tmp.PackArray(new byte[28]);
                        owner.Send(tmp);
                        owner.Send(new SendPacket(GetAC23_5()));
                        DebugSystem.Write($"[Inventory.AddItem] Placed item #{item.ItemID} x{addedTotal} into slot {at} for {owner?.CharName ?? "Unknown"}");
                    }
                    return addedTotal;
                }

                // Case 2: Dynamic placement (at == 0)
                // Pass 1: If stackable, stack onto existing stacks with SpaceLeft > 0
                if (item.Stackable)
                {
                    for (byte s = 1; s <= 50 && needed > 0; s++)
                    {
                        var cur = this[s];
                        if (cur != null && cur.ItemID == item.ItemID && cur.SpaceLeft > 0)
                        {
                            int toStack = Math.Min(needed, cur.SpaceLeft);
                            cur.Ammt += (byte)toStack;
                            needed -= toStack;
                            addedTotal += toStack;

                            if (sendData && owner != null)
                            {
                                SendPacket tmp = new SendPacket();
                                tmp.Pack8(23);
                                tmp.Pack8(6);
                                tmp.Pack16(item.ItemID);
                                tmp.Pack8((byte)toStack);
                                tmp.PackArray(new byte[28]);
                                owner.Send(tmp);
                            }
                            DebugSystem.Write($"[Inventory.AddItem] Stacked item #{item.ItemID} x{toStack} onto slot {s} for {owner?.CharName ?? "Unknown"} (New Ammt: {cur.Ammt})");
                        }
                    }
                }

                // Pass 2: Place remaining into empty slots (ItemID == 0)
                for (byte s = 1; s <= 50 && needed > 0; s++)
                {
                    var cur = this[s];
                    if (cur != null && cur.ItemID == 0)
                    {
                        cur.CopyFrom(item);
                        cur.Parent = 0;
                        int toPlace = item.Stackable ? Math.Min(needed, 50) : 1;
                        cur.Ammt = (byte)toPlace;
                        needed -= toPlace;
                        addedTotal += toPlace;

                        if (sendData && owner != null)
                        {
                            SendPacket tmp = new SendPacket();
                            tmp.Pack8(23);
                            tmp.Pack8(6);
                            tmp.Pack16(item.ItemID);
                            tmp.Pack8((byte)toPlace);
                            tmp.PackArray(new byte[28]);
                            owner.Send(tmp);
                        }
                        DebugSystem.Write($"[Inventory.AddItem] Added new item #{item.ItemID} x{toPlace} into slot {s} for {owner?.CharName ?? "Unknown"}");
                    }
                }

                if (addedTotal > 0 && sendData && owner != null)
                {
                    owner.Send(new SendPacket(GetAC23_5()));
                }

                return addedTotal;
            }
        }
        /// <summary>
        /// Moves and item in the Inventory List
        /// </summary>
        /// <param name="from"></param>
        /// <param name="to"></param>
        /// <param name="ammt"></param>
        public void MoveItem(byte from, byte to, byte ammt)
        {
            lock (mylock)
            {
                if (this[from].ItemID == 0 || from == to) return;

                SendPacket tmp = new SendPacket();
                tmp.Pack8(23);
                tmp.Pack8(10);
                tmp.Pack8(from);

                var item = RemoveItem(from, ammt, false);

                if (item != null && (this[to].ItemID == 0 || (this[to].ItemID == item.ItemID)))
                {
                    byte wasplaced = (byte)AddItem(item, to, false);
                    if (wasplaced > 0)
                    {
                        tmp.Pack8(wasplaced);
                        tmp.Pack8(to);
                        owner.Send(tmp);
                    }
                }
            }
        }

        public void ProcessSocket(RecievePacket p)
        {
            p.SetPtr();

            var a = p.Unpack8();
            var b = p.Unpack8();

            if (a != 23) return;

            switch (b)
            {
                #region move item inv
                case 10:
                    {
                        byte src = p.Unpack8();
                        byte ammt = p.Unpack8();
                        byte dst = p.Unpack8();

                        if (((src > 0) && (src < 51)) && ((dst > 0) && (dst < 51)) && ((ammt > 0) && (ammt < 51)))
                            MoveItem(src, dst, ammt);
                    }
                    break;
                #endregion
                #region item being used
                case 15:
                    {
                        byte pos = p.Unpack8();

                        if (this[pos].ItemID > 0)
                            switch (this[pos].Type)
                            {
                                case eItemType.Tent: owner.Tent.Open(); break;
                            }
                    }
                    break;
                #endregion
                #region destroy an item
                case 3:// drop item to floor 124:
                    {
                        byte pos = p.Unpack8();
                        byte qnt = p.Unpack8();
                        byte ukn = p.Unpack8(); //??

                        if (this[pos].ItemID > 0)
                        {
                            // test confirm destroy item
                            owner.Send(Tools.FromFormat("bbWb", 23, 26, this[pos].ItemID, qnt));
                            RemoveItem(pos, qnt);
                        }
                    }
                    break;
                    #endregion
            }
        }

        bool CanPlace(byte cell, Item item)
        {
            lock (mylock)
            {
                if (cell < 1 || cell > 50 || item == null) return false;
                var cur = this[cell];
                if (cur == null) return false;
                if (cur.ItemID == 0) return true;
                if (cur.ItemID == item.ItemID && cur.SpaceLeft > 0) return true;
                return false;
            }
        }
        public int FilledCount
        {
            get { lock (mylock) { return m_Items.Count(c => c != null && c.ItemID > 0); } }
        }
        public int unFilledCount
        {
            get { lock (mylock) { return 50 - FilledCount; } }
        }

        public byte[] GetAC23_5()
        {
            lock (mylock)
            {
                SendPacket tmp = new SendPacket();
                tmp.Pack8(23);
                tmp.Pack8(5);
                if (FilledCount > 0)
                {
                    for (byte a = 1; a < 51; a++)
                        if (this[a].ItemID != 0)
                        {
                            tmp.Pack8(a);
                            tmp.Pack16(this[a].ItemID);
                            tmp.Pack8(this[a].Ammt);
                            tmp.Pack8(this[a].Damage);
                            tmp.PackArray(new byte[26]);
                        }
                }
                return tmp.Buffer;
            }
        }

        public byte[] GetAC30_5(byte actionCode = 30, byte subCode = 5)
        {
            lock (mylock)
            {
                SendPacket tmp = new SendPacket();
                tmp.Pack8(actionCode);
                tmp.Pack8(subCode);
                if (FilledCount > 0)
                {
                    for (byte a = 1; a < 51; a++)
                        if (this[a].ItemID != 0)
                        {
                            tmp.Pack8(a);
                            tmp.Pack16(this[a].ItemID);
                            tmp.Pack8(this[a].Ammt);
                            tmp.Pack8(this[a].Damage);
                            tmp.PackArray(new byte[26]);
                        }
                }
                return tmp.Buffer;
            }
        }

        public Dictionary<byte, uint[]> InventoryDBData
        {
            get
            {
                lock (mylock)
                {
                    Dictionary<byte, uint[]> tmp = new Dictionary<byte, uint[]>();

                    for (byte a = 1; a < 51; a++)
                        tmp.Add(a, new uint[] { this[a].ItemID, this[a].Damage, this[a].Ammt, a, 0, 0, 0, 0 });
                    return tmp;
                }
            }
        }



        public void onItemUsed(byte slot, byte tgrt, byte ammt)
        {
            //Get item first
            //if (m_Items[slot].ItemID > 0)
            //{
            //    switch (m_Items[slot].Type)
            //    {
            //        case eItemType.tent: host.Tent.Open(); break;
            //        default:
            //            {
            //                host.DataOut = SendType.Multi;
            //                //switch (tgrt)
            //                //{
            //                //    case 0: for (int a = 0; a < 2; a++) host.AddStat((byte)Items[slot].Data.StatusType[a], (byte)Items[slot].Data.StatusUp[a]); break;
            //                //    //default:for (int a = 0; a < 2; a++) 
            //                //}
            //                m_Items[slot].Ammt -= 1;
            //                SendPacket p = new SendPacket();
            //                p.PackArray(new byte[] { 23, 9 });
            //                p.Pack((byte)slot);
            //                p.Pack((byte)ammt);
            //                host.Send(p);
            //                p = new SendPacket();
            //                p.PackArray(new byte[] { 23, 15 });
            //                host.Send(p);
            //                host.DataOut = SendType.Normal;
            //            } break;
            //    }
            //}
        }
        public void onItemCanceled(byte slot)
        {
            // Reserved for client item cancel events
        }
        int MatrixtoNumber(int a, int b) => (a * 5) + (b - 5); // WLO specific
        byte[] NumbertoMatrix(int a)
        {
            if (a <= 0) return new byte[] { 0, 0 };
            byte s = (byte)(((a - 1) / 5) + 1);
            byte t = (byte)(((a - 1) % 5) + 1);
            return new byte[] { s, t };
        } // WLO specific
    }

    public class TentInventoryManager : Inventory
    {

        public TentInventoryManager(Player src, global::DataFiles.PhxItemDat ItemDat)
            : base(src, ItemDat)
        {

        }
    }
}
