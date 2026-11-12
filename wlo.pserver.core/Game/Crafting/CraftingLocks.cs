using Game;
using Game.Code;

namespace Game.Crafting
{
    /// <summary>
    /// Item-lock checks for crafting consumption. Mirrors Wonderland-Gonline's
    /// game.PreserveItemLocks / ErrItemLocked: a locked item is reserved by an
    /// active operation and must not be consumed by crafting.
    /// </summary>
    public static class CraftingLocks
    {
        /// <summary>True when any of the listed bag slots holds a locked item.</summary>
        public static bool AnyLockedSlot(Inventory inv, params byte[] slots)
        {
            if (inv == null || slots == null) return false;
            foreach (byte slot in slots)
            {
                if (slot < 1 || slot > 50) continue;
                var item = inv[slot];
                if (item != null && item.isLocked) return true;
            }
            return false;
        }

        /// <summary>True when any bag slot holding this item id is locked.</summary>
        public static bool AnyLockedId(Inventory inv, ushort itemId)
        {
            if (inv == null || itemId == 0) return false;
            for (byte slot = 1; slot <= 50; slot++)
            {
                var item = inv[slot];
                if (item != null && item.ItemID == itemId && item.isLocked) return true;
            }
            return false;
        }
    }
}
