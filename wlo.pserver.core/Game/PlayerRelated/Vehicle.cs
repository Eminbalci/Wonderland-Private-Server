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
        private static readonly Dictionary<ushort, VehicleItem> _vehicleTemplates = new Dictionary<ushort, VehicleItem>();
        private static readonly object _lock = new object();

        static VehicleManager()
        {
            // Water Vehicles (Rafts & Ships)
            Register(new VehicleItem(36001, "Raft", VehicleType.Water, 0, 1));
            Register(new VehicleItem(36002, "Canoe", VehicleType.Water, 0, 1));
            Register(new VehicleItem(36003, "Sailboat", VehicleType.Water, 0, 4));
            Register(new VehicleItem(36004, "Steamboat", VehicleType.Water, 2000, 4));
            Register(new VehicleItem(36005, "Submarine", VehicleType.Water, 3000, 4));

            // Air Vehicles
            Register(new VehicleItem(36006, "Hot Air Balloon", VehicleType.Air, 1500, 2));
            Register(new VehicleItem(36007, "Airship", VehicleType.Air, 5000, 4));
            Register(new VehicleItem(36008, "UFO", VehicleType.Air, 9999, 4));

            // Land Vehicles (Motorbike, Beetle Car, etc.)
            Register(new VehicleItem(36010, "Bicycle", VehicleType.Land, 0, 1));
            Register(new VehicleItem(36011, "Motorcycle", VehicleType.Land, 1000, 2));
            Register(new VehicleItem(36012, "Beetle Car", VehicleType.Land, 2000, 4));
        }

        public static void Register(VehicleItem v)
        {
            lock (_lock)
            {
                _vehicleTemplates[v.VehicleID] = v;
            }
        }

        public static VehicleItem GetTemplate(ushort vid)
        {
            lock (_lock)
            {
                _vehicleTemplates.TryGetValue(vid, out var v);
                return v;
            }
        }

        public static bool MountVehicle(Player player, ushort vehicleId)
        {
            if (player == null) return false;

            // Dismount any pet first
            player.UnridePet();

            player.ActiveVehicleID = vehicleId;

            // Send Mount Packet (AC 15:10)
            SendPacket vp = new SendPacket();
            vp.Pack8(15);
            vp.Pack8(10);
            vp.Pack32(player.CharID);
            vp.Pack16(vehicleId);

            player.CurMap?.Broadcast(vp);

            // Send Fuel Status (AC 15:14)
            var templ = GetTemplate(vehicleId);
            if (templ != null && templ.MaxFuel > 0)
            {
                SendFuelUpdate(player, templ.CurrentFuel, templ.MaxFuel);
            }

            SendSystemMsg(player, $"Boarded vehicle {(templ != null ? templ.Name : vehicleId.ToString())}!");
            DebugSystem.Write($"[VehicleManager] Player {player.CharName} mounted vehicle #{vehicleId}.");
            return true;
        }

        public static void DismountVehicle(Player player)
        {
            if (player == null || player.ActiveVehicleID == 0) return;

            ushort prevVid = (ushort)player.ActiveVehicleID;
            player.ActiveVehicleID = 0;

            // Send Dismount Packet (AC 15:11)
            SendPacket vp = new SendPacket();
            vp.Pack8(15);
            vp.Pack8(11);
            vp.Pack32(player.CharID);

            player.CurMap?.Broadcast(vp);

            SendSystemMsg(player, "Dismounted from vehicle.");
            DebugSystem.Write($"[VehicleManager] Player {player.CharName} dismounted vehicle #{prevVid}.");
        }

        public static void WreckVehicle(Player player, ushort vehicleId = 0, byte vehicleType = 0x10)
        {
            if (player == null) return;

            ushort vid = vehicleId > 0 ? vehicleId : (ushort)player.ActiveVehicleID;
            if (vid == 0) vid = 48016;

            // Determine vehicle inventory slot
            byte slot = player.MountedVehicleSlot;
            if (slot == 0 || player.Inv == null || player.Inv[slot].ItemID == 0)
            {
                if (player.Inv != null)
                {
                    for (byte s = 1; s <= 50; s++)
                    {
                        var it = player.Inv[s];
                        if (it != null && (it.ItemID == vid || it.ItemID == 48016 || it.ItemID == 48010))
                        {
                            slot = s;
                            break;
                        }
                    }
                }
            }
            if (slot == 0) slot = 16; // Fallback to default slot 16

            // 1. Send AC 15 Sub 14: Final state (PCAP Frame 6992: 0F 0E [slot] [charId] D6 01 00 00 00 00)
            SendPacket statePkt = new SendPacket();
            statePkt.PackArray(new byte[] { 15, 14, slot });
            statePkt.Pack32(player.CharID);
            statePkt.PackArray(new byte[] { 0xD6, 0x01, 0, 0, 0, 0 });
            player.Send(statePkt);
            player.CurMap?.Broadcast(statePkt);

            // 2. Remove vehicle item from inventory (PCAP Frame 6992: 17 09 [slot] 01)
            if (player.Inv != null)
            {
                if (slot > 0 && player.Inv[slot].ItemID > 0)
                {
                    player.Inv.RemoveItem(slot, 1, senddata: true);
                }
                else
                {
                    player.Inv.RemoveItemById(vid, 1);
                }
            }

            // 3. Vehicle wreck packet (AC 15:15) (PCAP Frame 6992: 0F 0F [charId] [vid])
            SendPacket wreckPkt = new SendPacket();
            wreckPkt.Pack8(15);
            wreckPkt.Pack8(15);
            wreckPkt.Pack32(player.CharID);
            wreckPkt.Pack16(vid);
            player.Send(wreckPkt);
            player.CurMap?.Broadcast(wreckPkt);

            // 4. Unmount packet (AC 15:11) (PCAP Frame 6992: 0F 0B [slot] [charId])
            SendPacket unmountPkt = new SendPacket();
            unmountPkt.PackArray(new byte[] { 15, 11, slot });
            unmountPkt.Pack32(player.CharID);
            player.Send(unmountPkt);
            player.CurMap?.Broadcast(unmountPkt);

            // Reset active state
            player.ActiveVehicleID = 0;
            player.MountedVehicleSlot = 0;
            player.RideVehicle("");

            // 5. Movement refresh & persistence
            player.Send(Tools.FromFormat("bb", 5, 4));
            player.SendSystemMessage("The wooden raft broke apart upon landing on the shore. You are now walking on foot.");
            player.SaveCharacterData();

            DebugSystem.Write($"[VehicleManager] Player {player.CharName}'s vehicle #{vid} wrecked upon reaching shore (removed from slot {slot}).");
        }

        public static void ConsumeFuel(Player player, ushort amount = 1)
        {
            if (player == null || player.ActiveVehicleID == 0) return;

            var templ = GetTemplate((ushort)player.ActiveVehicleID);
            if (templ != null && templ.MaxFuel > 0)
            {
                if (templ.CurrentFuel >= amount)
                {
                    templ.CurrentFuel -= amount;
                }
                else
                {
                    templ.CurrentFuel = 0;
                    SendSystemMsg(player, "Vehicle is out of fuel!");
                }

                SendFuelUpdate(player, templ.CurrentFuel, templ.MaxFuel);
            }
        }

        public static void RefuelVehicle(Player player, ushort fuelAmount)
        {
            if (player == null || player.ActiveVehicleID == 0) return;

            var templ = GetTemplate((ushort)player.ActiveVehicleID);
            if (templ != null && templ.MaxFuel > 0)
            {
                templ.CurrentFuel = (ushort)Math.Min(templ.MaxFuel, templ.CurrentFuel + fuelAmount);
                SendFuelUpdate(player, templ.CurrentFuel, templ.MaxFuel);
                SendSystemMsg(player, $"Refueled vehicle! Fuel: {templ.CurrentFuel}/{templ.MaxFuel}");
            }
        }

        public static void SendFuelUpdate(Player player, ushort fuelLeft, ushort maxFuel)
        {
            if (player == null) return;
            SendPacket p = new SendPacket();
            p.Pack8(15);
            p.Pack8(14);
            p.Pack32(player.CharID);
            p.Pack16(fuelLeft);
            p.Pack16(maxFuel);
            player.Send(p);
        }

        public static void SyncVehicleOnMapEntry(Player player)
        {
            if (player == null || player.ActiveVehicleID == 0) return;

            SendPacket vp = new SendPacket();
            vp.Pack8(15);
            vp.Pack8(10);
            vp.Pack32(player.CharID);
            vp.Pack16((ushort)player.ActiveVehicleID);

            player.CurMap?.Broadcast(vp);
        }

        private static void SendSystemMsg(Player p, string msg)
        {
            if (p == null || string.IsNullOrEmpty(msg)) return;
            SendPacket s = new SendPacket();
            s.Pack8(23);
            s.Pack8(57);
            s.Pack8(0);
            s.PackString(msg);
            p.Send(s);
        }
    }
}
