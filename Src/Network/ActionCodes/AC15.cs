using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Network;
using Game;
using Game.Code;
using Game.Maps;
using wlo.pserver.core.Game;

namespace Network.ActionCodes
{
    /// <summary>
    /// Handles Companion, Mount/Ride, Rest, and Vehicle/Raft actions (AC 15).
    /// Verified byte-for-byte from official capture 'denizetiklayarakraftabinmeveisinlandiktansonrasahiletiklayarakraftikiriprafttaninme.pcapng'.
    /// </summary>
    public class AC15 : AC
    {
        public override int ID { get { return 15; } }

        public override void ProcessPkt(Player player, RecievePacket p)
        {
            if (player == null || p == null) return;

            byte sub = p.Unpack8();
            switch (sub)
            {
                case 7:  Recv7(player, p); break;   // Raft sailing confirmation
                case 9:  Recv9(player, p); break;   // Raft board / mount placed vehicle
                case 10: Recv10(player, p); break; // Raft dismount & break on shore (Frame 6950-6992)
                case 11: Recv11(player, p); break; // Ride Companion (Mount)
                case 12: Recv12(player, p); break; // Rest Companion
                case 13: Recv13(player, p); break; // Dismount ACK
                case 14: Recv14(player, p); break; // Use Raft / Vehicle (Frame 4129)
                default:
                    DebugSystem.Write($"[AC15] Subcode {sub} received");
                    break;
            }
        }

        /// <summary>
        /// Handles Client clicking water to board Raft / Vehicle: C->S [15, 14, type (1B), item_id (2B)]
        /// Official PCAP Frame 4129 -> Responds with AC 15 Sub 18 (Durability/Stats).
        /// </summary>
        private void Recv14(Player player, RecievePacket p)
        {
            try
            {
                byte vehicleType = p.Unpack8(); // 0x10 = Raft
                ushort vehicleId = 48016; // 0xBB90 default
                if (p.Buffer.Count() - p.GetPtr() >= 2) vehicleId = p.Unpack16();

                DebugSystem.Write($"[AC15.Recv14] Player {player.CharName} boarding vehicle (Type: 0x{vehicleType:X}, ID: {vehicleId})");

                // S->C AC 15 Sub 18: [15, 18, type (1B), char_id (4B), vehicle_id (2B), durability (8B)]
                SendPacket resp = new SendPacket();
                resp.PackArray(new byte[] { 15, 18, vehicleType });
                resp.Pack32(player.CharID);
                resp.Pack16(vehicleId);
                resp.Pack32(3042);  // 0x00000BE2 = Initial durability
                resp.Pack32(2075);  // Max durability / param
                player.Send(resp);
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[AC15.Recv14] Error: {ex.Message}");
            }
        }

        /// <summary>
        /// Client confirms boarding raft: C->S [15, 7, type (1B), item_id (2B)]
        /// Official PCAP Frame 4160 -> Responds with AC 15 Sub 10 (Mount ACK) + AC 15 Sub 14 (Active state).
        /// </summary>
        private void Recv7(Player player, RecievePacket p)
        {
            try
            {
                byte vehicleType = p.Unpack8();
                ushort vehicleId = 48016;
                if (p.Buffer.Count() - p.GetPtr() >= 2) vehicleId = p.Unpack16();

                player.ActiveVehicleID = vehicleId;
                player.RideVehicle(vehicleId.ToString());

                // S->C AC 15 Sub 10: Mount confirmation
                SendPacket mountPkt = new SendPacket();
                mountPkt.PackArray(new byte[] { 15, 10, vehicleType });
                mountPkt.Pack32(player.CharID);
                mountPkt.Pack16(vehicleId);
                player.Send(mountPkt);
                player.CurMap?.Broadcast(mountPkt);

                // S->C AC 15 Sub 14: Active state
                SendPacket statePkt = new SendPacket();
                statePkt.PackArray(new byte[] { 15, 14, vehicleType });
                statePkt.Pack32(player.CharID);
                statePkt.PackArray(new byte[] { 0, 0, 0, 0, 0, 0 });
                player.Send(statePkt);
                player.CurMap?.Broadcast(statePkt);

                player.SendSystemMessage("⛵ You are now sailing on your raft!");
                DebugSystem.Write($"[AC15.Recv7] Player {player.CharName} successfully mounted raft {vehicleId}");
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[AC15.Recv7] Error: {ex.Message}");
            }
        }

        /// <summary>
        /// Client clicks beach shore to land, break raft, and dismount on foot: C->S [15, 10, type (1B), item_id (2B)]
        /// Official PCAP Frame 6950-6992.
        /// </summary>
        private void Recv10(Player player, RecievePacket p)
        {
            try
            {
                byte vehicleType = 0x10;
                ushort vehicleId = 48016;
                if (p.Buffer.Count() - p.GetPtr() >= 1) vehicleType = p.Unpack8();
                if (p.Buffer.Count() - p.GetPtr() >= 2) vehicleId = p.Unpack16();

                DebugSystem.Write($"[AC15.Recv10] Player {player.CharName} landing on shore from raft {vehicleId}");

                // 1. Send AC 15 Sub 14: Final state
                SendPacket statePkt = new SendPacket();
                statePkt.PackArray(new byte[] { 15, 14, vehicleType });
                statePkt.Pack32(player.CharID);
                statePkt.PackArray(new byte[] { 0xD6, 0x01, 0, 0, 0, 0 });
                player.Send(statePkt);
                player.CurMap?.Broadcast(statePkt);

                // 2. Send AC 23 Sub 9: Raft Break Notice
                SendPacket breakNotice = new SendPacket();
                breakNotice.PackArray(new byte[] { 23, 9, vehicleType, 1 });
                player.Send(breakNotice);

                // 3. Send AC 15 Sub 15: Destroy / Remove vehicle
                SendPacket destroyPkt = new SendPacket();
                destroyPkt.PackArray(new byte[] { 15, 15 });
                destroyPkt.Pack32(player.CharID);
                destroyPkt.Pack16(vehicleId);
                player.Send(destroyPkt);
                player.CurMap?.Broadcast(destroyPkt);

                // 4. Send AC 15 Sub 11: Reset to walking on foot
                SendPacket walkPkt = new SendPacket();
                walkPkt.PackArray(new byte[] { 15, 11, vehicleType });
                walkPkt.Pack32(player.CharID);
                player.Send(walkPkt);
                player.CurMap?.Broadcast(walkPkt);

                player.ActiveVehicleID = 0;
                player.RideVehicle("");

                player.SendSystemMessage("🏖️ The wooden raft broke apart upon landing on the shore. You are now walking on foot.");
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[AC15.Recv10] Error: {ex.Message}");
            }
        }

        private void Recv13(Player player, RecievePacket p)
        {
            DebugSystem.Write($"[AC15.Recv13] Player {player.CharName} acknowledged dismount.");
        }

        private void Recv9(Player player, RecievePacket p)
        {
            try
            {
                p.Unpack8();
                ushort itemId = p.Unpack16();
                if (itemId == 0) itemId = 48016;

                player.UnridePet();
                player.RideVehicle(itemId.ToString());
                DebugSystem.Write($"[AC15] Player {player.CharName} mounted placed vehicle ID {itemId}");
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[AC15.Recv9] Error: {ex.Message}");
            }
        }

        private void Recv11(Player player, RecievePacket p)
        {
            try
            {
                byte slot = p.Unpack8();
                uint petId = p.Unpack32();

                player.PutPetToRide(petId.ToString());
                DebugSystem.Write($"[AC15] Player {player.CharName} mounted companion ID {petId} (Slot {slot})");
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[AC15.Recv11] Error: {ex.Message}");
            }
        }

        private void Recv12(Player player, RecievePacket p)
        {
            try
            {
                byte slot = p.Unpack8();
                uint petId = p.Unpack32();

                player.UnridePet();
                DebugSystem.Write($"[AC15] Player {player.CharName} rested/dismounted companion ID {petId} (Slot {slot})");
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[AC15.Recv12] Error: {ex.Message}");
            }
        }
    }
}
