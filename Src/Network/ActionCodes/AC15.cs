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
    /// Handles Companion, Mount/Ride, Rest, and Vehicle actions (AC 15).
    /// </summary>
    public class AC15 : AC
    {
        public override int ID { get { return 15; } }

        public override void ProcessPkt(Player player, RecievePacket p)
        {
            switch (p.Unpack8())
            {
                case 7: Recv7(player, p); break;   // Raft sailing start
                case 9: Recv9(player, p); break;   // Raft board / mount placed vehicle
                case 10: Recv10(player, p); break; // Raft dismount / destroy
                case 11: Recv11(player, p); break; // Ride Companion (Mount)
                case 12: Recv12(player, p); break; // Rest Companion
                case 14: Recv14(player, p); break; // Use Raft / Vehicle
                default:
                    DebugSystem.Write($"[AC15] Action Code 15 sub-command not handled");
                    break;
            }
        }

        /// <summary>
        /// Board Placed Raft/Vehicle: C->S [15, 9, ...]
        /// </summary>
        private void Recv9(Player player, RecievePacket p)
        {
            try
            {
                p.Unpack8(); // skip
                ushort itemId = p.Unpack16();
                if (itemId == 0) itemId = 48016; // default raft

                player.UnridePet();
                player.RideVehicle(itemId.ToString());

                DebugSystem.Write($"[AC15] Player {player.CharName} mounted placed vehicle ID {itemId}");
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[AC15.Recv9] Error: {ex.Message}");
            }
        }

        /// <summary>
        /// Ride Companion (Mount): C->S [15, 11, slot (1B), pet_id (4B)]
        /// </summary>
        private void Recv11(Player player, RecievePacket p)
        {
            try
            {
                byte slot = p.Unpack8();
                uint petId = p.Unpack32();

                // Send mount confirmation: S->C [15, 16, slot (1B), char_id (4B), pet_id (4B), 26 zero bytes]
                SendPacket ridePkt = new SendPacket();
                ridePkt.PackArray(new byte[] { 15, 16 });
                ridePkt.Pack8(slot);
                ridePkt.Pack32(player.CharID);
                ridePkt.Pack32(petId);
                for (int i = 0; i < 26; i++) ridePkt.Pack8(0);

                player.Send(ridePkt);
                player.CurMap?.Broadcast(ridePkt);

                DebugSystem.Write($"[AC15] Player {player.CharName} mounted companion ID {petId} (Slot {slot})");
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[AC15.Recv11] Error: {ex.Message}");
            }
        }

        /// <summary>
        /// Rest Companion: C->S [15, 12, slot (1B), pet_id (4B)]
        /// </summary>
        private void Recv12(Player player, RecievePacket p)
        {
            try
            {
                byte slot = p.Unpack8();
                uint petId = p.Unpack32();

                // Dismount / Rest broadcast
                SendPacket restPkt = Tools.FromFormat("bbd", 15, 17, player.CharID);
                player.Send(restPkt);
                player.CurMap?.Broadcast(restPkt);

                DebugSystem.Write($"[AC15] Player {player.CharName} rested companion ID {petId} (Slot {slot})");
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[AC15.Recv12] Error: {ex.Message}");
            }
        }

        /// <summary>
        /// Use Raft / Vehicle from inventory: C->S [15, 14, 0x15 (1B), item_id (2B)]
        /// Toggles mount / dismount with proximity check to nearest shore.
        /// </summary>
        private void Recv14(Player player, RecievePacket p)
        {
            try
            {
                p.Unpack8(); // skip 0x15
                ushort itemId = p.Unpack16();
                if (itemId == 0) itemId = 48016;

                // If player is already riding this vehicle -> Dismount toggle!
                if (player.ActiveVehicleID == itemId || (player.ActiveVehicleID > 0 && (itemId == 48016 || itemId == 48005 || itemId == 48014 || itemId == 48011 || itemId == 48050 || itemId == 36007 || itemId == 36008)))
                {
                    bool isNearShore = false;
                    WarpData shoreWarp = null;

                    if (player.CurMap != null)
                    {
                        if (player.CurMap.MapID == 10036)
                        {
                            // Distance to beach (1038, 2235)
                            int dx = (int)player.CurX - 1038;
                            int dy = (int)player.CurY - 2235;
                            double dist = Math.Sqrt(dx * dx + dy * dy);
                            if (dist <= 800 || (player.CurX >= 600 && player.CurX <= 1600 && player.CurY >= 1800 && player.CurY <= 2850))
                            {
                                isNearShore = true;
                                shoreWarp = new WarpData() { DstMap = 10036, DstX_Axis = 1038, DstY_Axis = 2235 };
                            }
                        }
                        else
                        {
                            // On other maps, allow dismount if not on water or near shore
                            isNearShore = true;
                        }
                    }

                    if (isNearShore)
                    {
                        player.RideVehicle("");
                        if (shoreWarp != null)
                        {
                            player.CurMap.Teleport(TeleportType.CmD, player, 0, shoreWarp);
                        }
                        player.SendSystemMessage("🚶 Dismounted from vehicle.");
                        DebugSystem.Write($"[AC15] Player {player.CharName} dismounted vehicle {itemId} safely.");
                    }
                    else
                    {
                        player.SendSystemMessage("⚠️ Can't exit here: You are too far from shore to dismount.");
                        DebugSystem.Write($"[AC15] Player {player.CharName} tried to dismount vehicle {itemId} but is too far from shore (X:{player.CurX}, Y:{player.CurY}).");
                    }
                    return;
                }

                player.UnridePet();
                player.RideVehicle(itemId.ToString());

                DebugSystem.Write($"[AC15] Player {player.CharName} mounted raft/vehicle ID {itemId}");
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[AC15.Recv14] Error: {ex.Message}");
            }
        }

        /// <summary>
        /// Raft sailing start: C->S [15, 7, 0x15 (1B), item_id (2B)]
        /// </summary>
        private void Recv7(Player player, RecievePacket p)
        {
            try
            {
                p.Unpack8();
                ushort itemId = p.Unpack16();
                if (itemId == 0) itemId = 48016;

                player.ActiveVehicleID = itemId;

                DebugSystem.Write($"[AC15] Player {player.CharName} navigating on raft ID {itemId} on Map {player.CurMap?.MapID}");
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[AC15.Recv7] Error: {ex.Message}");
            }
        }

        /// <summary>
        /// Raft break / wreck / dismount: C->S [15, 10, 0x15, item_id (2B)] (PCAP Frame 1058-1059)
        /// </summary>
        private void Recv10(Player player, RecievePacket p)
        {
            try
            {
                p.Unpack8();
                ushort itemId = p.Unpack16();
                if (itemId == 0) itemId = 48016;

                bool isNearShore = false;
                WarpData shoreWarp = null;

                if (player.CurMap != null && player.CurMap.MapID == 10036)
                {
                    int dx = (int)player.CurX - 1038;
                    int dy = (int)player.CurY - 2235;
                    double dist = Math.Sqrt(dx * dx + dy * dy);
                    if (dist <= 800 || (player.CurX >= 600 && player.CurX <= 1600 && player.CurY >= 1800 && player.CurY <= 2850))
                    {
                        isNearShore = true;
                        shoreWarp = new WarpData() { DstMap = 10036, DstX_Axis = 1038, DstY_Axis = 2235 };
                    }
                }
                else
                {
                    isNearShore = true;
                }

                if (!isNearShore)
                {
                    player.SendSystemMessage("⚠️ Can't exit here: You are too far from shore.");
                    return;
                }

                player.ActiveVehicleID = 0;
                player.RideVehicle("");

                if (shoreWarp != null)
                {
                    player.CurMap.Teleport(TeleportType.CmD, player, 0, shoreWarp);
                }

                DebugSystem.Write($"[AC15.Recv10] Player {player.CharName} dismounted raft ID {itemId} safely.");
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[AC15.Recv10] Error: {ex.Message}");
            }
        }
    }
}
