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

    public enum NpcBuyResult : byte
    {
        Success = 0,
        Rejected = 1,
        NotEnoughGold = 2,
        InventoryFull = 3,
        ItemUnavailable = 1,
        TooManyItems = 1
    }

    public class Inventory
    {
        private static readonly Lazy<Dictionary<ushort, uint[]>> NpcSalePrices = new Lazy<Dictionary<ushort, uint[]>>(() => {
            var rows = new Dictionary<ushort, uint[]>();
            var stream = typeof(Inventory).Assembly.GetManifestResourceStream("NpcSalePrices");
            if (stream != null)
            {
                using (var reader = new System.IO.StreamReader(stream))
                {
                    reader.ReadLine();
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        var fields = line.Split(',');
                        if (fields.Length >= 3 && ushort.TryParse(fields[0], out ushort id) && uint.TryParse(fields[1], out uint p1) && uint.TryParse(fields[2], out uint p2))
                        {
                            rows[id] = new[] { p1, p2 };
                        }
                    }
                }
            }
            if (rows.Count == 0)
            {
                string[] candidates = { "Data/npc_sale_prices.csv", "../Data/npc_sale_prices.csv", "../../Data/npc_sale_prices.csv" };
                foreach (var p in candidates)
                {
                    if (System.IO.File.Exists(p))
                    {
                        using (var reader = new System.IO.StreamReader(p))
                        {
                            reader.ReadLine();
                            string line;
                            while ((line = reader.ReadLine()) != null)
                            {
                                var fields = line.Split(',');
                                if (fields.Length >= 3 && ushort.TryParse(fields[0], out ushort id) && uint.TryParse(fields[1], out uint p1) && uint.TryParse(fields[2], out uint p2))
                                {
                                    rows[id] = new[] { p1, p2 };
                                }
                            }
                        }
                        break;
                    }
                }
            }
            return rows;
        });

        private static readonly Lazy<Dictionary<ushort, uint>> NpcBuyPrices = new Lazy<Dictionary<ushort, uint>>(() => {
            var rows = new Dictionary<ushort, uint>();
            var stream = typeof(Inventory).Assembly.GetManifestResourceStream("NpcBuyPrices");
            if (stream != null)
            {
                using (var reader = new System.IO.StreamReader(stream))
                {
                    reader.ReadLine();
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        var fields = line.Split(',');
                        if (fields.Length >= 2 && ushort.TryParse(fields[0], out ushort id) && uint.TryParse(fields[1], out uint price))
                        {
                            rows[id] = price;
                        }
                    }
                }
            }

            if (rows.Count == 0)
            {
                string[] candidates = { "Data/npc_buy_prices.csv", "../Data/npc_buy_prices.csv", "../../Data/npc_buy_prices.csv" };
                foreach (var p in candidates)
                {
                    if (System.IO.File.Exists(p))
                    {
                        using (var reader = new System.IO.StreamReader(p))
                        {
                            reader.ReadLine();
                            string line;
                            while ((line = reader.ReadLine()) != null)
                            {
                                var fields = line.Split(',');
                                if (fields.Length >= 2 && ushort.TryParse(fields[0], out ushort id) && uint.TryParse(fields[1], out uint price))
                                {
                                    rows[id] = price;
                                }
                            }
                        }
                        break;
                    }
                }
            }

            if (rows.Count == 0)
            {
                string[] itemDatPaths = { "Data/Item.dat", "../Data/Item.dat", "../../Data/Item.dat" };
                foreach (var p in itemDatPaths)
                {
                    if (System.IO.File.Exists(p))
                    {
                        try
                        {
                            byte[] bytes = System.IO.File.ReadAllBytes(p);
                            int recSize = 451;
                            int count = bytes.Length / recSize;
                            for (int i = 1; i < count; i++)
                            {
                                int offset = i * recSize;
                                ushort rawId = BitConverter.ToUInt16(bytes, offset + 16);
                                ushort id = (ushort)((rawId ^ 0xEFC3) - 9);
                                uint rawBuy = BitConverter.ToUInt32(bytes, offset + 114);
                                uint buy = (uint)((rawBuy ^ 0x0B80F4B4) - 9);
                                if (id >= 10000 && id <= 60000)
                                {
                                    rows[id] = buy;
                                }
                            }
                        }
                        catch { }
                        break;
                    }
                }
            }

            return rows;
        });

        public ushort ResolveCatalogItem(ushort slotOrItemId)
        {
            if (slotOrItemId >= 1000) return slotOrItemId;
            if (slotOrItemId == 0) return 0;

            byte shopType = 3; // 3 = Props Shop, 2 = Weapon Shop
            if (owner != null)
            {
                if (owner.CurrentShopCatalogId == 0x0001FB84u) shopType = 2; // Weapon Shop
                else if (owner.CurrentShopCatalogId == 0x0001FB83u) shopType = 2; // Armor Shop
                else if (owner.CurrentShopCatalogId == 0x0001FB85u) shopType = 3; // Props Shop
            }

            // 1. Try resolving from active map's InteractiveInfo in EveData
            if (owner?.CurMap != null)
            {
                var mapData = DataBase.GameDataBase.GlobalInstance?.EveDat?.GetMapData((ushort)owner.CurMap.MapID);
                if (mapData != null && mapData.InteractiveInfo != null && mapData.InteractiveInfo.Count > 0)
                {
                    var shopEntry = mapData.InteractiveInfo.FirstOrDefault(e => e.unknownbyte1 == shopType && e.subentry != null && e.subentry.Count > 0);
                    if (shopEntry == null)
                    {
                        shopEntry = mapData.InteractiveInfo.FirstOrDefault(e => (e.unknownbyte1 == 2 || e.unknownbyte1 == 3) && e.subentry != null && e.subentry.Count > 0);
                    }

                    if (shopEntry != null && shopEntry.subentry != null && shopEntry.subentry.Count > 0)
                    {
                        var sub = shopEntry.subentry.FirstOrDefault(s => s.unknownbyte1 == slotOrItemId);
                        if (sub == null && slotOrItemId >= 1 && slotOrItemId <= shopEntry.subentry.Count)
                        {
                            sub = shopEntry.subentry[slotOrItemId - 1];
                        }

                        if (sub != null)
                        {
                            ushort itemId = (ushort)(sub.unknownbyte2 | (sub.unknownbyte3 << 8));
                            if (itemId > 0) return itemId;
                        }
                    }
                }
            }

            // 2. Fallback catalog tables if map InteractiveInfo is not present
            ushort[] fallback = (shopType == 2)
                ? new ushort[] { 17064, 14032, 21061, 18032, 15032, 24067 }
                : new ushort[] { 32011, 32002, 45001, 45007, 42008, 45007 };

            if (slotOrItemId >= 1 && slotOrItemId <= fallback.Length)
            {
                return fallback[slotOrItemId - 1];
            }

            return slotOrItemId;
        }

        public NpcSaleResult SellToNpc(byte[] slots, byte mode)
        {
            if (slots == null || slots.Length == 0 || mode > 1) return NpcSaleResult.Rejected;
            if (slots.Length > 50) return NpcSaleResult.TooManyItems;
            if (owner?.CurMap == null || Game.Battle.PvEBattleManager.IsInBattle(owner)) return NpcSaleResult.Rejected;
            if (slots.Distinct().Count() != slots.Length || slots.Any(s => s < 1 || s > 50)) return NpcSaleResult.Rejected;

            // Session check: ensure the player has an active sale or shop session on this map
            bool hasValidSaleSession = (owner.NpcSaleMode != null || owner.CurrentShopCatalogId != 0 || (owner.LastNpcClick != 0 && owner.LastNpcMap == owner.CurMap.MapID)) &&
                                       (owner.NpcSaleMap == 0 || owner.NpcSaleMap == owner.CurMap.MapID);
            if (!hasValidSaleSession) return NpcSaleResult.Rejected;

            lock (mylock)
            {
                ulong total = 0;
                foreach (byte slot in slots)
                {
                    var item = this[slot]; uint[] price;
                    if (item == null || item.ItemID == 0 || item.Ammt == 0) return NpcSaleResult.ItemUnavailable;
                    // Only slots 1..6 are worn equipment; 7..10 are inventory-use items.
                    bool equipment = item.Data != null && item.Data.Equippos >= 1 && item.Data.Equippos <= 6;
                    if (item.Parent != 0 || item.isLocked || (mode == 0) != equipment) return NpcSaleResult.Rejected;
                    if (!NpcSalePrices.Value.TryGetValue(item.ItemID, out price) || (price[1] & 16) != 0) return NpcSaleResult.Rejected;
                    total += (ulong)price[0] * item.Ammt;
                }
                if (total > 999999 || owner.Eqs.Gold + total > 999999) return NpcSaleResult.GoldLimit;
                if (total > 0 && !owner.Eqs.AddGold((int)total)) return NpcSaleResult.GoldLimit;
                foreach (byte slot in slots) RemoveItem(slot, this[slot].Ammt);
                owner.NpcSaleMode = mode;
                owner.Eqs.SendGold();
                owner.SaveCharacterData();
                return NpcSaleResult.Success;
            }
        }

        public NpcBuyResult BuyFromNpc(Game.Maps.Code.ShoppingCart[] cartItems)
        {
            if (cartItems == null || cartItems.Length == 0) return NpcBuyResult.Rejected;
            if (cartItems.Length > 10) return NpcBuyResult.TooManyItems;
            if (owner?.CurMap == null || Game.Battle.PvEBattleManager.IsInBattle(owner)) return NpcBuyResult.Rejected;

            lock (mylock)
            {
                ulong totalCost = 0;
                var requestedItems = new Dictionary<ushort, int>();

                foreach (var entry in cartItems)
                {
                    if (entry.ammt == 0) continue;
                    ushort itemId = ResolveCatalogItem(entry.slot);

                    var itemInfo = ItemDat?.GetItemByID(itemId);
                    if (itemInfo == null) return NpcBuyResult.ItemUnavailable;

                    uint unitPrice = 0;
                    if (NpcBuyPrices.Value.TryGetValue(itemId, out uint buyPrice) && buyPrice > 0)
                    {
                        unitPrice = buyPrice;
                    }
                    else
                    {
                        uint[] priceInfo;
                        NpcSalePrices.Value.TryGetValue(itemId, out priceInfo);
                        unitPrice = (priceInfo != null && priceInfo[0] > 0) ? (priceInfo[0] * 10) : 10u;
                    }
                    totalCost += (ulong)unitPrice * entry.ammt;

                    if (requestedItems.ContainsKey(itemId))
                        requestedItems[itemId] += entry.ammt;
                    else
                        requestedItems[itemId] = entry.ammt;
                }

                if (requestedItems.Count == 0) return NpcBuyResult.Rejected;
                if (totalCost > (ulong)owner.Eqs.Gold) return NpcBuyResult.NotEnoughGold;
                if (!CanAddItems(requestedItems)) return NpcBuyResult.InventoryFull;

                if (totalCost > 0 && !owner.Eqs.TakeGold((int)totalCost)) return NpcBuyResult.NotEnoughGold;

                foreach (var kvp in requestedItems)
                {
                    AddItem(kvp.Key, (byte)Math.Min((int)byte.MaxValue, kvp.Value), true);
                }

                owner.Eqs.SendGold();
                owner.SaveCharacterData();
                return NpcBuyResult.Success;
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

        public static int GetCol(byte slot) => (slot - 1) % 5;
        public static int GetRow(byte slot) => (slot - 1) / 5;
        public static byte GetSlot(int col, int row) => (byte)(row * 5 + col + 1);

        public byte GetItemWidth(ushort itemId)
        {
            var info = ItemDat?.GetItemByID(itemId);
            if (info != null && info.cellwidth > 0 && info.cellwidth <= 10) return info.cellwidth;
            return 1;
        }

        public byte GetItemHeight(ushort itemId)
        {
            var info = ItemDat?.GetItemByID(itemId);
            if (info != null && info.cellheight > 0 && info.cellheight <= 10) return info.cellheight;
            return 1;
        }

        public bool CanFitItem(ushort itemId, byte rootSlot, byte ignoreRootSlot = 0)
        {
            if (rootSlot < 1 || rootSlot > 50) return false;
            int col = GetCol(rootSlot);
            int row = GetRow(rootSlot);
            int w = GetItemWidth(itemId);
            int h = GetItemHeight(itemId);

            if (col + w > 5 || row + h > 10) return false;

            for (int r = 0; r < h; r++)
            {
                for (int c = 0; c < w; c++)
                {
                    byte s = GetSlot(col + c, row + r);
                    if (s < 1 || s > 50) return false;
                    if (ignoreRootSlot > 0 && (s == ignoreRootSlot || m_Items[s - 1].Parent == ignoreRootSlot))
                        continue;
                    if (m_Items[s - 1].ItemID != 0 || m_Items[s - 1].Parent != 0 || m_Items[s - 1].isLocked)
                        return false;
                }
            }
            return true;
        }

        public byte FindAvailableSlot(ushort itemId)
        {
            for (byte s = 1; s <= 50; s++)
            {
                if (CanFitItem(itemId, s))
                    return s;
            }
            return 0;
        }

        public void LockSlaveSlots(byte rootSlot, ushort itemId)
        {
            if (rootSlot < 1 || rootSlot > 50) return;
            int col = GetCol(rootSlot);
            int row = GetRow(rootSlot);
            int w = GetItemWidth(itemId);
            int h = GetItemHeight(itemId);

            for (int r = 0; r < h; r++)
            {
                for (int c = 0; c < w; c++)
                {
                    byte s = GetSlot(col + c, row + r);
                    if (s >= 1 && s <= 50)
                    {
                        if (s == rootSlot)
                        {
                            m_Items[s - 1].Parent = 0;
                            m_Items[s - 1].isLocked = false;
                        }
                        else
                        {
                            m_Items[s - 1].Clear();
                            m_Items[s - 1].Parent = rootSlot;
                            m_Items[s - 1].isLocked = true;
                        }
                    }
                }
            }
        }

        public void UnlockSlaveSlots(byte rootSlot)
        {
            for (int i = 0; i < 50; i++)
            {
                if (m_Items[i].Parent == rootSlot)
                {
                    m_Items[i].Clear();
                }
            }
        }

        public void RebuildOccupancyGrid()
        {
            lock (mylock)
            {
                // 1. Clear any dangling slave markers
                for (int i = 0; i < 50; i++)
                {
                    if (m_Items[i].ItemID == 0)
                    {
                        m_Items[i].Parent = 0;
                        m_Items[i].isLocked = false;
                    }
                }

                // 2. Lock slave cells for all multi-cell items
                for (byte s = 1; s <= 50; s++)
                {
                    var item = m_Items[s - 1];
                    if (item != null && item.ItemID > 0 && item.Parent == 0)
                    {
                        int w = GetItemWidth(item.ItemID);
                        int h = GetItemHeight(item.ItemID);
                        if (w > 1 || h > 1)
                        {
                            LockSlaveSlots(s, item.ItemID);
                        }
                    }
                }
            }
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
                if (at < 1 || at > 50 || ammt == 0) return null;
                if (this[at].Parent != 0) at = this[at].Parent;
                if (at < 1 || at > 50 || this[at].ItemID == 0 || this[at].Ammt == 0) return null;
                byte removed = (byte)Math.Min(ammt, this[at].Ammt);
                var remItem = new InvItem();
                remItem.CopyFrom(this[at]);
                remItem.Ammt = removed;
                if (owner != null && owner.ActiveVehicleID != 0 && owner.MountedVehicleSlot == at && this[at].Ammt == removed)
                    Game.PlayerRelated.VehicleManager.DismountVehicle(owner);
                if (this[at].Ammt == removed)
                {
                    this[at].Clear();
                    UnlockSlaveSlots(at);
                }
                else
                {
                    this[at].Ammt -= removed;
                }
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
            if (amounts == null || amounts.Count == 0) return true;
            lock (mylock)
            {
                bool[] occupied = new bool[50];
                for (int i = 0; i < 50; i++)
                {
                    occupied[i] = (m_Items[i].ItemID > 0 || m_Items[i].Parent > 0 || m_Items[i].isLocked);
                }

                var remaining = new Dictionary<ushort, int>();
                foreach (var kvp in amounts)
                {
                    ushort itemId = kvp.Key;
                    int needed = kvp.Value;
                    var info = ItemDat?.GetItemByID(itemId);
                    int w = GetItemWidth(itemId);
                    int h = GetItemHeight(itemId);
                    bool stackable = (info != null && w <= 1 && h <= 1);

                    if (stackable)
                    {
                        for (byte s = 1; s <= 50 && needed > 0; s++)
                        {
                            var cur = this[s];
                            if (cur != null && cur.ItemID == itemId && cur.SpaceLeft > 0 && cur.Parent == 0)
                            {
                                int toStack = Math.Min(needed, cur.SpaceLeft);
                                needed -= toStack;
                            }
                        }
                    }

                    if (needed > 0)
                    {
                        remaining[itemId] = needed;
                    }
                }

                foreach (var kvp in remaining)
                {
                    ushort itemId = kvp.Key;
                    int needed = kvp.Value;
                    int w = GetItemWidth(itemId);
                    int h = GetItemHeight(itemId);

                    while (needed > 0)
                    {
                        int toPlace = (w == 1 && h == 1) ? Math.Min(needed, 50) : 1;
                        bool placed = false;

                        for (int slot = 1; slot <= 50; slot++)
                        {
                            int col = (slot - 1) % 5;
                            int row = (slot - 1) / 5;
                            if (col + w > 5 || row + h > 10) continue;

                            bool canFit = true;
                            for (int r = 0; r < h; r++)
                            {
                                for (int c = 0; c < w; c++)
                                {
                                    int idx = (row + r) * 5 + (col + c);
                                    if (occupied[idx])
                                    {
                                        canFit = false;
                                        break;
                                    }
                                }
                                if (!canFit) break;
                            }

                            if (canFit)
                            {
                                for (int r = 0; r < h; r++)
                                    for (int c = 0; c < w; c++)
                                        occupied[(row + r) * 5 + (col + c)] = true;

                                placed = true;
                                needed -= toPlace;
                                break;
                            }
                        }

                        if (!placed) return false;
                    }
                }

                return true;
            }
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

                int w = GetItemWidth(item.ItemID);
                int h = GetItemHeight(item.ItemID);
                bool isMultiCell = (w > 1 || h > 1);

                // Case 1: Specific slot requested
                if (at >= 1 && at <= 50)
                {
                    var target = this[at];
                    if (target.ItemID == 0 && target.Parent == 0 && !target.isLocked && CanFitItem(item.ItemID, at))
                    {
                        target.CopyFrom(item);
                        target.Parent = 0;
                        int toPut = (item.Stackable && !isMultiCell) ? Math.Min(needed, 50) : 1;
                        target.Ammt = (byte)toPut;
                        LockSlaveSlots(at, item.ItemID);
                        addedTotal = toPut;
                    }
                    else if (target.ItemID == item.ItemID && target.SpaceLeft > 0 && !isMultiCell)
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
                // Pass 1: If stackable and not multi-cell, stack onto existing stacks
                if (item.Stackable && !isMultiCell)
                {
                    for (byte s = 1; s <= 50 && needed > 0; s++)
                    {
                        var cur = this[s];
                        if (cur != null && cur.ItemID == item.ItemID && cur.SpaceLeft > 0 && cur.Parent == 0)
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

                // Pass 2: Place remaining into free slots that fit 2D bounds
                while (needed > 0)
                {
                    byte freeSlot = FindAvailableSlot(item.ItemID);
                    if (freeSlot == 0)
                    {
                        DebugSystem.Write($"[Inventory.AddItem] No available 2D space for item #{item.ItemID} ({w}x{h}) for {owner?.CharName ?? "Unknown"}");
                        break;
                    }

                    var cur = this[freeSlot];
                    cur.CopyFrom(item);
                    cur.Parent = 0;
                    int toPlace = (item.Stackable && !isMultiCell) ? Math.Min(needed, 50) : 1;
                    cur.Ammt = (byte)toPlace;
                    LockSlaveSlots(freeSlot, item.ItemID);
                    needed -= toPlace;
                    addedTotal += toPlace;

                    additions[freeSlot] = (byte)toPlace;
                    DebugSystem.Write($"[Inventory.AddItem] Added new item #{item.ItemID} ({w}x{h}) x{toPlace} into slot {freeSlot} for {owner?.CharName ?? "Unknown"}");
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

                // If 'from' is a slave slot, redirect to master root
                if (this[from].Parent != 0) from = this[from].Parent;
                if (from < 1 || from > 50) return;

                var source = this[from];
                if (source.ItemID == 0 || source.Ammt == 0) return;

                int w = GetItemWidth(source.ItemID);
                int h = GetItemHeight(source.ItemID);
                bool isMultiCell = (w > 1 || h > 1);

                var target = this[to];

                // Stacking onto target (only for 1x1 stackable items of same ID)
                if (!isMultiCell && target.ItemID == source.ItemID && source.Stackable && target.SpaceLeft > 0 && source.Damage == target.Damage && target.Parent == 0)
                {
                    byte moved = (byte)Math.Min(Math.Min(ammt, source.Ammt), target.SpaceLeft);
                    if (moved > 0)
                    {
                        target.Ammt += moved;
                        if (source.Ammt == moved)
                        {
                            source.Clear();
                            UnlockSlaveSlots(from);
                        }
                        else
                        {
                            source.Ammt -= moved;
                        }
                        owner?.Send(Tools.FromFormat("bbbbb", 23, 10, from, moved, to));
                    }
                    return;
                }

                // Moving to destination slot with 2D bounds check
                if (CanFitItem(source.ItemID, to, ignoreRootSlot: from))
                {
                    byte moved = source.Ammt;
                    UnlockSlaveSlots(from);
                    target.CopyFrom(source);
                    target.Parent = 0;
                    target.Ammt = moved;
                    LockSlaveSlots(to, target.ItemID);
                    source.Clear();
                    owner?.Send(Tools.FromFormat("bbbbb", 23, 10, from, moved, to));
                    DebugSystem.Write($"[Inventory.MoveItem] Moved item #{target.ItemID} ({w}x{h}) from slot {from} to slot {to} for {owner?.CharName ?? "Unknown"}");
                }
                else
                {
                    DebugSystem.Write($"[Inventory.MoveItem] Rejected move for item #{source.ItemID} ({w}x{h}) from slot {from} to slot {to}: target space unavailable.");
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
            get { lock (mylock) { return m_Items.Count(c => c != null && (c.ItemID > 0 || c.Parent > 0 || c.isLocked)); } }
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
