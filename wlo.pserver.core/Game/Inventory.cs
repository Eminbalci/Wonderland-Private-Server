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
    public enum NpcSaleResult : byte
    {
        Success = 0,
        GoldLimit = 1,
        ItemUnavailable = 2,
        TooManyItems = 3,
        Rejected = 4
    }

    public class Inventory
    {
        private static readonly Lazy<Dictionary<ushort, uint[]>> NpcSalePrices = new Lazy<Dictionary<ushort, uint[]>>(() => {
            var rows = new Dictionary<ushort, uint[]>();
            using (var reader = new System.IO.StreamReader(typeof(Inventory).Assembly.GetManifestResourceStream("NpcSalePrices")))
            {
                reader.ReadLine();
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    var fields = line.Split(',');
                    rows[ushort.Parse(fields[0])] = new[] { uint.Parse(fields[1]), uint.Parse(fields[2]) };
                }
            }
            return rows;
        });

        public NpcSaleResult SellToNpc(byte[] slots, byte mode)
        {
            if (slots == null || slots.Length == 0 || mode > 1) return NpcSaleResult.Rejected;
            if (slots.Length > 50) return NpcSaleResult.TooManyItems;
            if (owner?.NpcSaleMode != mode || owner.CurMap == null || owner.NpcSaleMap != owner.CurMap.MapID ||
                slots.Distinct().Count() != slots.Length || slots.Any(s => s < 1 || s > 50) ||
                Game.Battle.PvEBattleManager.IsInBattle(owner)) return NpcSaleResult.Rejected;
            lock (mylock)
            {
                ulong total = 0;
                foreach (byte slot in slots)
                {
                    var item = this[slot]; uint[] price;
                    if (item == null || item.ItemID == 0 || item.Ammt == 0) return NpcSaleResult.ItemUnavailable;
                    // Only slots 1..6 are worn equipment; 7..10 are inventory-use items.
                    bool equipment = item.Data.Equippos >= 1 && item.Data.Equippos <= 6;
                    if (item.Parent != 0 || item.isLocked ||
                        !NpcSalePrices.Value.TryGetValue(item.ItemID, out price) || (price[1] & 16) != 0 ||
                        (mode == 0) != equipment) return NpcSaleResult.Rejected;
                    total += (ulong)price[0] * item.Ammt;
                }
                if (total > 999999 || owner.Eqs.Gold + total > 999999) return NpcSaleResult.GoldLimit;
                if (total > 0 && !owner.Eqs.AddGold((int)total)) return NpcSaleResult.GoldLimit;
                foreach (byte slot in slots) RemoveItem(slot, this[slot].Ammt);
                owner.Eqs.SendGold();
                owner.SaveCharacterData();
                return NpcSaleResult.Success;
            }
        }

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

        public object SyncRoot => mylock;

        internal bool HasItemDefinition(ushort itemID)
        {
            return ItemDat?.GetItemByID(itemID) != null;
        }

        internal int GetPetEquipmentBonus(Player.PlayerPetData pet, Func<Equip, int> stat)
        {
            int bonus = 0;
            for (byte slot = 1; slot <= 6; slot++)
            {
                var equip = GetPetEquipment(pet, slot);
                if (equip != null) bonus += stat(equip);
            }
            return bonus;
        }


        internal Equip GetPetEquipment(Player.PlayerPetData pet, byte slot)
        {
            ushort id = pet.GetEquipmentId(slot);
            var data = id == 0 ? null : ItemDat?.GetItemByID(id);
            if (data == null) return null;
            var item = new Equip();
            item.CopyFrom(data);
            item.Damage = pet.EquipmentMetadata[(slot - 1) * 2];
            item.Forge = pet.EquipmentMetadata[(slot - 1) * 2 + 1];
            return item;
        }

        public bool TryEquipPet(byte petSlot, byte from)
        {
            lock (mylock)
            {
                var pet = owner?.GetClientPet(petSlot);
                if (pet == null || from < 1 || from > 50 || Game.Battle.PvEBattleManager.IsInBattle(owner)) return false;
                var item = this[from];
                byte slot = (byte)item.Wear_At;
                if (item.ItemID == 0 || item.Ammt != 1 || item.isLocked || slot < 1 || slot > 6) return false;
                var previous = GetPetEquipment(pet, slot);
                if (pet.GetEquipmentId(slot) != 0 && previous == null) return false;
                var previousStats = owner.PetEquipmentStats(pet);
                pet.SetEquipment(slot, item);
                item.Clear();
                if (previous != null) item.CopyFrom(previous);
                // Native 23:23 removes the bag item, returns old equipment into that
                // same bag slot, then equips the incoming item on this client pet slot.
                owner.Send(Tools.FromFormat("bbbb", 23, 23, petSlot, from));
                owner.SendPetEquipmentStatChanges(pet, previousStats);
                return true;
            }
        }

        public bool TryUnequipPet(byte petSlot, byte from, byte to)
        {
            lock (mylock)
            {
                var pet = owner?.GetClientPet(petSlot);
                if (pet == null || from < 1 || from > 6 || to < 1 || to > 50 ||
                    Game.Battle.PvEBattleManager.IsInBattle(owner) || this[to].ItemID != 0) return false;
                var item = GetPetEquipment(pet, from);
                if (item == null) return false;
                var previousStats = owner.PetEquipmentStats(pet);
                this[to].CopyFrom(item);
                pet.SetEquipment(from, null);
                owner.Send(Tools.FromFormat("bbbbb", 23, 22, petSlot, from, to));
                owner.SendPetEquipmentStatChanges(pet, previousStats);
                return true;
            }
        }

        public bool TryEquip(EquipManager equips, byte from)
        {
            lock (mylock)
            {
                if (equips == null || from < 1 || from > 50) return false;
                var item = this[from];
                byte slot = (byte)item.Wear_At;
                if (item.ItemID == 0 || item.Ammt != 1 || item.isLocked || slot < 1 || slot > 6) return false;
                var incoming = new Item(item);
                var previous = equips.Wear(slot, incoming);
                if (equips[slot].ItemID != incoming.ItemID) return false;
                item.Clear();
                if (previous != null && previous.ItemID != 0) item.CopyFrom(previous);
                return true;
            }
        }

        public bool TryUnequip(EquipManager equips, byte from, byte to)
        {
            lock (mylock)
            {
                if (equips == null || from < 1 || from > 6 || to < 1 || to > 50) return false;
                if (this[to].ItemID != 0 || equips[from].ItemID == 0) return false;
                // Reserve a valid empty destination before removing the worn item.
                var item = equips.unWear(from);
                if (item == null) return false;
                this[to].CopyFrom(item);
                this[to].Ammt = 1;
                this[to].Parent = 0;
                return true;
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
                if (owner == null || owner.ActiveVehicleID != vehicleItemId || wearAmount == 0) return;
                byte slot = owner.MountedVehicleSlot;
                Game.Code.Item item;
                if (!Game.PlayerRelated.VehicleManager.TryGetVehicle(owner, slot, vehicleItemId, out item))
                {
                    Game.PlayerRelated.VehicleManager.DismountVehicle(owner);
                    return;
                }
                // The existing movement wear path applies to rafts only, not every vehicle.
                if (Game.PlayerRelated.VehicleManager.BaseVehicleID(vehicleItemId) != 48010 && vehicleItemId != 48016) return;
                item.Damage = (byte)Math.Min(100, item.Damage + wearAmount);
                if (item.Damage == 100) Game.PlayerRelated.VehicleManager.WreckVehicle(owner, vehicleItemId);
                else owner.Send(new SendPacket(GetAC23_5()));
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
                if (at < 1 || at > 50 || ammt == 0 || this[at].ItemID == 0 || this[at].Ammt == 0) return null;
                byte removed = (byte)Math.Min(ammt, this[at].Ammt);
                var remItem = new InvItem();
                remItem.CopyFrom(this[at]);
                remItem.Ammt = removed;
                if (owner != null && owner.ActiveVehicleID != 0 && owner.MountedVehicleSlot == at && this[at].Ammt == removed)
                    Game.PlayerRelated.VehicleManager.DismountVehicle(owner);
                if (this[at].Ammt == removed) this[at].Clear();
                else this[at].Ammt -= removed;
                if (senddata && owner != null)
                    owner.Send(Tools.FromFormat("bbbb", 23, 9, at, removed));
                return remItem;
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
        // Plan the entire delivery before changing any slot. AC23:5 is additive,
        // so only the newly delivered quantities may be sent to a connected client.
        public bool TryAddItems(IDictionary<ushort, int> amounts)
        {
            return TryAddItems(amounts, 0, 0);
        }

        public bool CanAddItems(IDictionary<ushort, int> amounts)
        {
            return TryAddItems(amounts, 0, 0, false);
        }

        public bool TryExchangeItem(byte sourceSlot, ushort expectedItemId, IDictionary<ushort, int> rewards)
        {
            if (sourceSlot < 1 || sourceSlot > 50 || expectedItemId == 0) return false;
            return TryAddItems(rewards, sourceSlot, expectedItemId);
        }

        private bool TryAddItems(IDictionary<ushort, int> amounts, byte sourceSlot, ushort expectedItemId, bool commit = true)
        {
            lock (mylock)
            {
                var planned = new InvItem[50];
                for (int i = 0; i < 50; i++)
                {
                    planned[i] = new InvItem();
                    planned[i].CopyFrom(m_Items[i]);
                }
                if (amounts == null || amounts.Count == 0) return false;
                if (sourceSlot != 0)
                {
                    var source = planned[sourceSlot - 1];
                    if (source.isLocked || source.ItemID != expectedItemId || source.Ammt == 0) return false;
                    if (source.Ammt == 1) source.Clear();
                    else source.Ammt--;
                }
                foreach (var amount in amounts)
                {
                    var info = ItemDat?.GetItemByID(amount.Key);
                    if (info == null || amount.Value <= 0) return false;
                    var incoming = new InvItem();
                    incoming.CopyFrom(info);
                    int needed = amount.Value;
                    if (incoming.Stackable)
                        foreach (var slot in planned)
                            if (!slot.isLocked && slot.ItemID == amount.Key && slot.SpaceLeft > 0)
                            {
                                int added = Math.Min(needed, slot.SpaceLeft);
                                slot.Ammt += (byte)added;
                                needed -= added;
                            }
                    foreach (var slot in planned)
                        if (needed > 0 && !slot.isLocked && slot.ItemID == 0)
                        {
                            slot.CopyFrom(incoming);
                            slot.Parent = 0;
                            slot.Ammt = (byte)Math.Min(needed, incoming.Stackable ? 50 : 1);
                            needed -= slot.Ammt;
                        }
                    if (needed > 0) return false;
                }
                if (!commit) return true;
                // Commit only after all rewards fit, including the slot freed by consumption.
                if (sourceSlot != 0) RemoveItem(sourceSlot, 1);
                var delta = new SendPacket();
                delta.Pack8(23); delta.Pack8(5);
                for (int i = 0; i < 50; i++)
                {
                    int added = planned[i].Ammt - m_Items[i].Ammt;
                    if (added <= 0) continue;
                    m_Items[i].CopyFrom(planned[i]);
                    delta.Pack8((byte)(i + 1)); delta.Pack16(planned[i].ItemID);
                    delta.Pack8((byte)added); delta.Pack8(planned[i].Damage);
                    delta.PackArray(planned[i].InventoryMetadata());
                }
                owner?.Send(delta);
                return true;
            }
        }

        // Simulate ordered quest changes, including slots released by turn-in materials.
        public bool TryApplyQuestItems(IEnumerable<KeyValuePair<ushort, int>> changes, bool commit)
        {
            return TryApplyQuestItems(changes, commit, null);
        }

        // Persist the planned reward before publishing it to memory or the client.
        internal bool TryApplyQuestItems(IEnumerable<KeyValuePair<ushort, int>> changes, bool commit,
            Func<InvItem[], bool> persist)
        {
            lock (mylock)
            {
                if (changes == null) return false;
                var planned = new InvItem[50];
                for (int i = 0; i < 50; i++) { planned[i] = new InvItem(); planned[i].CopyFrom(m_Items[i]); }
                foreach (var change in changes)
                {
                    var info = ItemDat?.GetItemByID(change.Key);
                    if (info == null || change.Value == int.MinValue) return false;
                    int needed = Math.Abs(change.Value);
                    if (change.Value < 0)
                    {
                        foreach (var slot in planned)
                            if (!slot.isLocked && slot.ItemID == change.Key && needed > 0)
                            {
                                int take = Math.Min(needed, slot.Ammt);
                                slot.Ammt -= (byte)take; needed -= take;
                                if (slot.Ammt == 0) slot.Clear();
                            }
                    }
                    else
                    {
                        var incoming = new InvItem(); incoming.CopyFrom(info);
                        if (incoming.Stackable)
                            foreach (var slot in planned)
                                if (!slot.isLocked && slot.ItemID == change.Key && slot.SpaceLeft > 0)
                                {
                                    int add = Math.Min(needed, slot.SpaceLeft);
                                    slot.Ammt += (byte)add; needed -= add;
                                }
                        foreach (var slot in planned)
                            if (needed > 0 && !slot.isLocked && slot.ItemID == 0)
                            {
                                slot.CopyFrom(incoming); slot.Parent = 0;
                                slot.Ammt = (byte)Math.Min(needed, incoming.Stackable ? 50 : 1);
                                needed -= slot.Ammt;
                            }
                    }
                    if (needed != 0) return false;
                }
                if (!commit) return true;
                if (persist != null && !persist(planned)) return false;
                var additions = new Dictionary<byte, byte>();
                for (int i = 0; i < 50; i++)
                {
                    byte slot = (byte)(i + 1);
                    if (m_Items[i].ItemID != planned[i].ItemID && m_Items[i].ItemID != 0)
                        RemoveItem(slot, m_Items[i].Ammt);
                    int delta = planned[i].Ammt - m_Items[i].Ammt;
                    if (delta < 0) RemoveItem(slot, (byte)-delta);
                    else if (delta > 0) { m_Items[i].CopyFrom(planned[i]); additions[slot] = (byte)delta; }
                }
                if (additions.Count > 0 && owner != null) SendAddedItems(additions);
                return true;
            }
        }

        public int AddItem(ushort ID, byte amt)
        {
            return AddItem(ID, amt, true);
        }
        public int AddItem(ushort ID, byte amt, bool sendData)
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

            // Unknown IDs render as empty cells on the native client but block server slots.
            if (baseItem == null) return 0;
            InvItem i = new InvItem();
            i.CopyFrom(baseItem);
            i.Ammt = amt;
            return AddItem(i, 0, sendData);
        }

        private void SendAddedItems(IDictionary<byte, byte> additions)
        {
            // AC23:5 adds to the client's existing slots; never resend the whole bag.
            var packet = new SendPacket();
            packet.Pack8(23); packet.Pack8(5);
            foreach (var addition in additions)
            {
                var item = this[addition.Key];
                packet.Pack8(addition.Key); packet.Pack16(item.ItemID);
                packet.Pack8(addition.Value); packet.Pack8(item.Damage);
                packet.PackArray(item.InventoryMetadata());
            }
            owner.Send(packet);
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
                if (item == null || item.ItemID == 0 || ItemDat?.GetItemByID(item.ItemID) == null) return 0;

                int needed = item.Ammt;
                int addedTotal = 0;
                var additions = new Dictionary<byte, byte>();

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
                        additions[at] = (byte)addedTotal;
                        SendAddedItems(additions);
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

                            additions[s] = (byte)toStack;
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

                        additions[s] = (byte)toPlace;
                        DebugSystem.Write($"[Inventory.AddItem] Added new item #{item.ItemID} x{toPlace} into slot {s} for {owner?.CharName ?? "Unknown"}");
                    }
                }

                if (addedTotal > 0 && sendData && owner != null)
                {
                    SendAddedItems(additions);
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
                if (from < 1 || from > 50 || to < 1 || to > 50 || from == to || ammt == 0) return;
                if (owner != null && owner.ActiveVehicleID != 0 &&
                    (from == owner.MountedVehicleSlot || to == owner.MountedVehicleSlot)) return;
                var source = this[from];
                var target = this[to];
                if (source.ItemID == 0 || source.isLocked || target.isLocked) return;
                if (target.ItemID != 0 && (target.ItemID != source.ItemID || !source.Stackable || source.Damage != target.Damage)) return;
                int capacity = target.ItemID == 0 ? (source.Stackable ? 50 : 1) : target.SpaceLeft;
                byte moved = (byte)Math.Min(Math.Min(ammt, source.Ammt), Math.Max(0, capacity));
                if (moved == 0) return;
                // Validate the destination before changing either slot; move only what fits.
                if (target.ItemID == 0)
                {
                    target.CopyFrom(source);
                    target.Parent = 0;
                    target.Ammt = moved;
                }
                else target.Ammt += moved;
                if (source.Ammt == moved) source.Clear();
                else source.Ammt -= moved;
                owner?.Send(Tools.FromFormat("bbbbb", 23, 10, from, moved, to));
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
                            tmp.PackArray(this[a].InventoryMetadata());
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
                            tmp.PackArray(this[a].InventoryMetadata());
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
                        tmp.Add(a, new uint[] { this[a].ItemID, this[a].Damage, this[a].Ammt, a, 0, 0, 0, this[a].Forge });
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
