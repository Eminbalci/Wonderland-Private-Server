using System;
using System.Collections.Generic;
using Game;
using Game.Code;

namespace Network.ActionCodes
{
    public class AC30 : AC
    {
        public override int ID { get { return 30; } }

        public override void ProcessPkt(Player p, RecievePacket r)
        {
            r.SetPtr(6);
            int length = r.Buffer.Length - r.GetPtr();
            if (r.B < 1 || r.B > 5) return;
            bool changed = false;
            lock (p.Inv.SyncRoot)
            lock (p.Storage.SyncRoot)
            {
                // Native 30:1/2 contains selected slots, not slot + quantity.
                // Native 30:3 is storage source/destination; 30:4/5 is destination/source.
                if ((r.B == 1 || r.B == 2) && length > 0 && length <= 50)
                {
                    var slots = new HashSet<byte>();
                    for (int i = 0; i < length; i++) slots.Add(r.Unpack8());
                    foreach (byte slot in slots)
                        changed |= Transfer(p, r.B == 2 ? p.Inv : p.Storage,
                            r.B == 2 ? p.Storage : p.Inv, slot, 0);
                }
                else if (length == 2)
                {
                    byte first = r.Unpack8(), second = r.Unpack8();
                    if (first >= 1 && first <= 50 && second >= 1 && second <= 50)
                    {
                        if (r.B == 3) changed = Transfer(p, p.Storage, p.Storage, first, second);
                        else if (r.B == 4) changed = Transfer(p, p.Inv, p.Storage, second, first);
                        else if (r.B == 5) changed = Transfer(p, p.Storage, p.Inv, second, first);
                    }
                }

                // Storage lists are additive, so clear before sending an authoritative snapshot.
                // AC29 belongs to the gold bank and must never be emitted by item transfers.
                p.Send(Tools.FromFormat("bb", 30, 8));
                p.Send(new SendPacket(p.Storage.GetAC30_5(30, 1)));
                p.Send(Tools.FromFormat("bb", 30, 6));
                p.Send(Tools.FromFormat("bb", 30, 7));
            }
            if (changed) p.SaveCharacterData();
        }

        static bool Transfer(Player p, Inventory source, Inventory destination, byte from, byte to)
        {
            if (from < 1 || from > 50 || to > 50 || (source == destination && from == to)) return false;
            var item = source[from];
            if (item.ItemID == 0 || item.Ammt == 0 || item.isLocked) return false;
            int remaining = item.Ammt;
            var additions = new Dictionary<byte, byte>();
            // Plan capacity first. Automatic transfers must fit completely; a targeted merge
            // transfers only what fits and leaves the remainder in the source slot.
            for (int pass = 0; pass < 2 && remaining > 0; pass++)
            for (byte slot = 1; slot <= 50 && remaining > 0; slot++)
            {
                if (to != 0 && slot != to) continue;
                var target = destination[slot];
                if (target.isLocked || (source == destination && slot == from)) continue;
                bool empty = target.ItemID == 0;
                if (empty != (pass == 1)) continue;
                if (!empty && (target.ItemID != item.ItemID || !item.Stackable || target.Damage != item.Damage)) continue;
                int capacity = empty ? (item.Stackable ? 50 : 1) : Math.Max(0, 50 - target.Ammt);
                byte amount = (byte)Math.Min(remaining, capacity);
                if (amount == 0) continue;
                additions.Add(slot, amount);
                remaining -= amount;
            }
            if (additions.Count == 0 || (to == 0 && remaining != 0)) return false;
            byte moved = (byte)(item.Ammt - remaining);
            var delta = new SendPacket();
            delta.Pack8(23); delta.Pack8(5);
            foreach (var addition in additions)
            {
                var target = destination[addition.Key];
                if (target.ItemID == 0)
                {
                    target.CopyFrom(item);
                    target.Parent = 0;
                    target.Ammt = addition.Value;
                }
                else target.Ammt += addition.Value;
                if (destination == p.Inv)
                {
                    delta.Pack8(addition.Key); delta.Pack16(item.ItemID);
                    delta.Pack8(addition.Value); delta.Pack8(item.Damage);
                    delta.PackArray(item.InventoryMetadata());
                }
            }
            source.RemoveItem(from, moved, source == p.Inv);
            // Only the newly received quantities are additive bag updates.
            if (destination == p.Inv) p.Send(delta);
            return true;
        }
    }
}
