using System;
using System.Collections.Generic;
using System.IO;
using System.Web.Script.Serialization;
using DataFiles;
using Game.Battle;
using Network;

namespace Game.PlayerRelated
{
    /// <summary>
    /// Opens "Boxed_Item" (item type 17) packages such as the Item Mall weapon,
    /// costume and wing boxes. The client Item.dat stores the packed item ID at
    /// record offset 425; Data/boxed_items.json is generated from that field.
    /// </summary>
    public static class BoxedItemManager
    {
        public class Entry
        {
            public ushort box_id { get; set; }
            public ushort item_id { get; set; }
            public int quantity { get; set; }
        }

        private static Dictionary<ushort, Entry> boxes = new Dictionary<ushort, Entry>();
        private static PhxItemDat items;

        public static void Load(PhxItemDat data, string path)
        {
            var loaded = new Dictionary<ushort, Entry>();
            try
            {
                if (File.Exists(path))
                {
                    var config = new JavaScriptSerializer().Deserialize<Entry[]>(File.ReadAllText(path));
                    foreach (var e in config ?? new Entry[0])
                    {
                        if (e == null || e.box_id == 0 || e.item_id == 0) continue;
                        if (e.quantity < 1 || e.quantity > 255) e.quantity = 1;
                        if (data?.GetItemByID(e.item_id) == null)
                        {
                            DebugSystem.Write($"[BoxedItem] Skipping box {e.box_id}: content item {e.item_id} is not in the item data.");
                            continue;
                        }
                        loaded[e.box_id] = e;
                    }
                }
                else
                {
                    DebugSystem.Write($"[BoxedItem] {path} not found; item boxes cannot be opened.");
                }
            }
            catch (Exception ex)
            {
                loaded.Clear();
                DebugSystem.Write("[BoxedItem] Failed to load box table: " + ex.Message);
            }
            items = data;
            boxes = loaded;
            DebugSystem.Write($"[BoxedItem] Loaded {boxes.Count} openable item boxes.");
        }

        public static bool IsBox(ushort id) { return boxes.ContainsKey(id); }

        /// <summary>
        /// Returns true when the slot holds a known box (handled, even if it could not be opened).
        /// </summary>
        public static bool TryOpen(Player player, byte slot)
        {
            if (player == null || slot < 1 || slot > 50) return false;
            lock (player.Inv.SyncRoot)
            {
                var item = player.Inv[slot];
                if (item == null || !boxes.TryGetValue(item.ItemID, out var box)) return false;
                if (item.isLocked || item.Ammt == 0 || PvEBattleManager.IsInBattle(player))
                {
                    player.SendHeadBanner("This box cannot be opened right now.");
                    return true;
                }
                if (!player.Inv.TryExchangeItem(slot, box.box_id, new Dictionary<ushort, int> { { box.item_id, box.quantity } }))
                {
                    player.SendHeadBanner("Please free an inventory slot to open this box.");
                    return true;
                }
                string name = "Item #" + box.item_id;
                var info = items?.GetItemByID(box.item_id);
                if (info?.ItemName != null)
                    name = System.Text.Encoding.ASCII.GetString(info.ItemName).Trim('\0', ' ');
                player.Send(Tools.FromFormat("bb", 23, 15));
                player.SendHeadBanner("Opened box: received [" + name + "] x" + box.quantity + ".");
                player.SaveCharacterData();
                DebugSystem.Write($"[BoxedItem] {player.CharName} opened box {box.box_id} -> {box.item_id} x{box.quantity}");
                return true;
            }
        }
    }
}
