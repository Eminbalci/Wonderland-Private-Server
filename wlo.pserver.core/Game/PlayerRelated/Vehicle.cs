using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Network;

namespace Game.PlayerRelated
{
    public enum VehicleType : byte
    {
        None = 0,
        Land = 1,
        Water = 2,
        Air = 3
    }

    public class VehicleItem
    {
        public ushort VehicleID { get; set; }
        public string Name { get; set; } = string.Empty;
        public VehicleType Type { get; set; } = VehicleType.Land;
        public ushort MaxFuel { get; set; } = 1000;
        public ushort CurrentFuel { get; set; } = 1000;
        public ushort MaxHp { get; set; } = 1000;
        public ushort CurrentHp { get; set; } = 1000;
        public byte Capacity { get; set; } = 1; // 1 = solo, 2+ = multi-passenger

        public VehicleItem() { }

        public VehicleItem(ushort id, string name, VehicleType type, ushort maxFuel = 1000, byte cap = 1)
        {
            VehicleID = id;
            Name = name;
            Type = type;
            MaxFuel = maxFuel;
            CurrentFuel = maxFuel;
            MaxHp = 1000;
            CurrentHp = 1000;
            Capacity = cap;
        }
    }

    public static class VehicleManager
    {
        // Native FUN_0016eb64 groups these capsule and unpacked item IDs together.
        public static ushort BaseVehicleID(uint id)
        {
            if (id >= 48019 && id <= 48032) return (ushort)(id - 18);
            if (id == 48033) return 48017;
            if (id == 48034) return 48018;
            if (id == 48036 || id == 48038 || id == 48040 || id == 48042) return (ushort)(id - 1);
            return (ushort)id;
        }

        public static bool IsSameVehicle(uint actual, ushort required)
        {
            return actual > 0 && actual <= ushort.MaxValue && BaseVehicleID(actual) == BaseVehicleID(required);
        }

        public static bool TryGetVehicle(Player player, byte slot, ushort vehicleId, out Game.Code.Item item)
        {
            item = null;
            if (player?.Inv == null || slot < 1 || slot > 50 || vehicleId == 0) return false;
            item = player.Inv[slot];
            return item != null && item.ItemID == vehicleId && item.Ammt > 0 &&
                item.Type == global::DataFiles.eItemType.Vehicle && item.Damage < 100 && !item.isLocked;
        }

        private static void SendToMap(Player player, SendPacket packet)
        {
            player.Send(packet);
            player.CurMap?.Broadcast(packet, "Ex", player.CharID);
        }

        public static SendPacket CreateMountPacket(Player player)
        {
            return Tools.FromFormat("bbbdw", 15, 10, player.MountedVehicleSlot,
                player.CharID, (ushort)player.ActiveVehicleID);
        }

        public static bool PlaceVehicle(Player player, byte slot, ushort vehicleId)
        {
            Game.Code.Item item;
            if (!TryGetVehicle(player, slot, vehicleId, out item) || player.NativeEventActive ||
                Game.Battle.PvEBattleManager.IsInBattle(player)) return false;
            // AC15:18 at 0x44a6dc reads slot, character, item, X and Y.
            // Placement is not boarding; confirmation owns the active vehicle state.
            player.Send(Tools.FromFormat("bbbdwdd", 15, 18, slot, player.CharID,
                vehicleId, (uint)player.CurX, (uint)player.CurY));
            return true;
        }

        public static bool MountVehicle(Player player, ushort vehicleId)
        {
            byte slot;
            if (player?.Inv == null || !player.Inv.ContainsItem(vehicleId, out slot)) return false;
            return MountVehicle(player, slot, vehicleId);
        }

        public static bool MountVehicle(Player player, byte slot, ushort vehicleId)
        {
            Game.Code.Item item;
            if (!TryGetVehicle(player, slot, vehicleId, out item) || player.NativeEventActive ||
                Game.Battle.PvEBattleManager.IsInBattle(player)) return false;
            if (player.ActiveVehicleID == vehicleId && player.MountedVehicleSlot == slot) return true;
            if (player.ActiveVehicleID != 0) DismountVehicle(player);
            player.UnridePet();
            player.MountedVehicleSlot = slot;
            player.ActiveVehicleID = vehicleId;
            SendToMap(player, CreateMountPacket(player));
            DebugSystem.Write($"[VehicleManager] {player.CharName} boarded item {vehicleId} in slot {slot}.");
            return true;
        }

        public static void DismountVehicle(Player player)
        {
            if (player == null) return;
            if (player.ActiveVehicleID != 0)
                SendToMap(player, Tools.FromFormat("bbbd", 15, 11, player.MountedVehicleSlot, player.CharID));
            player.ActiveVehicleID = 0;
            player.MountedVehicleSlot = 0;
        }

        public static void LandVehicle(Player player, ushort vehicleId)
        {
            if (player == null || player.ActiveVehicleID == 0 || player.ActiveVehicleID != vehicleId) return;
            // Only Robinson's authored disposable raft breaks on landing.
            if (vehicleId == 48016) WreckVehicle(player, vehicleId);
            else DismountVehicle(player);
        }

        public static void WreckVehicle(Player player, ushort vehicleId = 0, byte vehicleType = 0x10)
        {
            if (player == null || player.ActiveVehicleID == 0) return;
            ushort id = (ushort)player.ActiveVehicleID;
            if (vehicleId != 0 && vehicleId != id) return;
            byte slot = player.MountedVehicleSlot;
            var item = slot >= 1 && slot <= 50 ? player.Inv?[slot] : null;
            // Never fall back to another raft or an arbitrary bag slot.
            if (item == null || item.ItemID != id || item.Ammt == 0 || item.Type != global::DataFiles.eItemType.Vehicle)
            {
                DismountVehicle(player);
                return;
            }
            if (BaseVehicleID(id) != 48010 && id != 48016) { DismountVehicle(player); return; }
            // Clear riding state before deleting its exact item. Repeated callbacks are harmless.
            player.ActiveVehicleID = 0;
            player.MountedVehicleSlot = 0;
            player.Inv.RemoveItem(slot, 1, senddata: true);
            SendToMap(player, Tools.FromFormat("bbdw", 15, 15, player.CharID, id));
            SendToMap(player, Tools.FromFormat("bbbd", 15, 11, slot, player.CharID));
            player.Send(Tools.FromFormat("bb", 5, 4));
            player.SaveCharacterData();
        }

        public static void SyncVehicleOnMapEntry(Player player)
        {
            if (player == null || player.ActiveVehicleID == 0) return;
            Game.Code.Item item;
            if (!TryGetVehicle(player, player.MountedVehicleSlot, (ushort)player.ActiveVehicleID, out item))
            {
                DismountVehicle(player);
                return;
            }
            SendToMap(player, CreateMountPacket(player));
        }
    }
}
