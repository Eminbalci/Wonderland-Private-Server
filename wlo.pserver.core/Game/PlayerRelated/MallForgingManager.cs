using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Web.Script.Serialization;
using DataFiles;
using Game.Battle;
using Game.Code;
using Network;

namespace Game.PlayerRelated
{
    // Strong Scroll forging changes the equipment ID along the native client table.
    // It is distinct from point/token forging, which changes per-item forge metadata.
    public static class MallForgingManager
    {
        public class Configuration
        {
            public ushort[][] families { get; set; }
            public ushort[] point_items { get; set; }
        }
        private class Upgrade
        {
            public ushort Next;
            public byte Scrolls;
        }
        private static Dictionary<ushort, Upgrade> upgrades = new Dictionary<ushort, Upgrade>();
        private static PhxItemDat items;
        private static HashSet<ushort> pointItems = new HashSet<ushort>();
        private const int PointCost = 3; // Native client 24d881: price per attempt.
        private const ushort StrongScroll = 30101;

        public static void Load(PhxItemDat data, string path)
        {
            var loaded = new Dictionary<ushort, Upgrade>();
            var points = new HashSet<ushort>();
            try
            {
                var config = new JavaScriptSerializer().Deserialize<Configuration>(File.ReadAllText(path));
                if (config?.families == null || config.families.Length == 0 || data.GetItemByID(StrongScroll) == null)
                    throw new InvalidDataException("Missing forging data");
                if (config.point_items == null) throw new InvalidDataException("Missing point-forging eligibility");
                foreach (var id in config.point_items)
                    if (id == 0 || !points.Add(id)) throw new InvalidDataException("Invalid point-forging item");
                foreach (var family in config.families)
                {
                    if (family == null || family.Length < 2 || family.Length > 11)
                        throw new InvalidDataException("Invalid forging family");
                    for (int i = 0; i < family.Length; i++)
                    {
                        if (family[i] == 0 || loaded.ContainsKey(family[i]))
                            throw new InvalidDataException("Duplicate or empty forging item");
                        loaded.Add(family[i], new Upgrade {
                            Next = i + 1 < family.Length ? family[i + 1] : (ushort)0,
                            Scrolls = (byte)(i + 1)
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                loaded.Clear();
                points.Clear();
                DebugSystem.Write("[Forging] Disabled invalid configuration: " + ex.Message);
            }
            items = data;
            upgrades = loaded;
            pointItems = points;
        }

        private static bool ForgeWithPoints(Player player, byte slot, Func<bool> successRoll)
        {
            var item = player.Inv[slot];
            if (items?.GetItemByID(item.ItemID) == null || !pointItems.Contains(item.ItemID) || item.Wear_At < (eWearSlot)1 || item.Wear_At > (eWearSlot)5)
            {
                Reject(player, "This equipment cannot be forged.");
                return false;
            }
            if (item.Data.StatusType == null || item.Data.StatusUp == null ||
                !Enumerable.Range(0, Math.Min(2, Math.Min(item.Data.StatusType.Length, item.Data.StatusUp.Length)))
                    .Any(i => item.Data.StatusType[i] != 0 && item.Data.StatusType[i] != 250 && item.Data.StatusUp[i] >= 100))
            {
                Reject(player, "This equipment has no eligible stat to improve.");
                return false;
            }
            if (item.Forge >= 200)
            {
                Reject(player, "This equipment is already at its maximum forging progress.");
                return false;
            }
            int balance = ItemMallManager.GetUserPoints(player);
            if (balance < PointCost)
            {
                Reject(player, "Forging requires 3 IM Points.", 4);
                ItemMallManager.SendPointBalance(player);
                return false;
            }
            bool success = successRoll();
            if (success) item.Forge++;
            ItemMallManager.SetUserPoints(player, balance - PointCost);
            // Native success updates only forge metadata, never additive inventory.
            if (success) player.Send(Tools.FromFormat("bbbbbb", 75, 6, 1, slot, item.Forge, 0));
            else player.Send(Tools.FromFormat("bbb", 75, 6, 2));
            player.SaveCharacterData();
            return success;
        }

        private static void Reject(Player player, string message, byte result = 8)
        {
            // 75:6 resets the native pending/locked selection; banner alone does not.
            player.Send(Tools.FromFormat("bbb", 75, 6, result));
            player.SendHeadBanner(message);
        }

        public static bool Forge(Player player, byte slot)
        {
            return ForgeCore(player, slot, RollSuccess);
        }

        private static bool RollSuccess()
        {
            var value = new byte[1];
            using (var rng = RandomNumberGenerator.Create()) rng.GetBytes(value);
            return (value[0] & 1) == 0; // Exactly 50%, independent for every attempt.
        }

        internal static bool ForgeCore(Player player, byte slot, Func<bool> successRoll)
        {
            if (player?.UserAccount == null) return false;
            // Match cart lock order so buying and forging cannot spend the same points.
            lock (player.UserAccount)
            lock (player.Inv.SyncRoot)
            {
                if (slot < 1 || slot > 50 || PvEBattleManager.IsInBattle(player))
                {
                    Reject(player, "Forging is unavailable right now.");
                    return false;
                }
                var source = player.Inv[slot];
                if (source.ItemID == 0 || source.Ammt != 1 || source.isLocked || source.Parent != 0)
                {
                    Reject(player, "Select an unlocked equipment item in your inventory.");
                    return false;
                }
                Upgrade upgrade;
                if (!upgrades.TryGetValue(source.ItemID, out upgrade))
                {
                    return ForgeWithPoints(player, slot, successRoll);
                }
                if (upgrade.Next == 0)
                {
                    Reject(player, "This equipment is already at its maximum upgrade.");
                    return false;
                }
                var next = items?.GetItemByID(upgrade.Next);
                if (next == null || next.Equippos < 1 || next.Equippos > 6 || next.Equippos != source.Data.Equippos)
                {
                    Reject(player, "Upgrade item data is unavailable. Equipment and scrolls retained.");
                    return false;
                }
                var costs = new List<KeyValuePair<byte, byte>>();
                int needed = upgrade.Scrolls;
                for (byte i = 1; i <= 50 && needed > 0; i++)
                {
                    var material = player.Inv[i];
                    if (material.ItemID != StrongScroll || material.isLocked || material.Parent != 0) continue;
                    byte take = (byte)Math.Min(needed, material.Ammt);
                    if (take == 0) continue;
                    costs.Add(new KeyValuePair<byte, byte>(i, take));
                    needed -= take;
                }
                if (needed != 0)
                {
                    Reject(player, "This upgrade requires " + upgrade.Scrolls + " Strong Scroll(s).", 5);
                    return false;
                }

                // Plan the replacement and all costs before mutating anything. Keep the
                // same slot and durability; upgraded stats come from the native item data.
                var replacement = new InvItem();
                replacement.CopyFrom(next);
                replacement.Damage = source.Damage;
                var added = new SendPacket();
                added.Pack8(23); added.Pack8(5);
                added.Pack8(slot); added.Pack16(replacement.ItemID);
                added.Pack8(1); added.Pack8(replacement.Damage);
                added.PackArray(new byte[26]);

                foreach (var cost in costs) player.Inv.RemoveItem(cost.Key, cost.Value, false);
                source.CopyFrom(replacement);
                foreach (var cost in costs)
                    player.Send(Tools.FromFormat("bbbb", 23, 9, cost.Key, cost.Value));
                // AC23:5 is additive: remove the old ID before adding its replacement.
                player.Send(Tools.FromFormat("bbbb", 23, 9, slot, 1));
                player.Send(added);
                // Native result 6 displays success, plays SEB0300 and clears selection.
                player.Send(Tools.FromFormat("bbb", 75, 6, 6));
                player.SaveCharacterData();
                return true;
            }
        }
    }
}
