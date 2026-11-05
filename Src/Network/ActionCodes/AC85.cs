using System;
using System.Collections.Generic;
using System.Linq;
using Game;
using Game.Code;
using Game.Experimental;
using Network;

namespace Network.ActionCodes
{
    /// <summary>
    /// Action Code 85: Instance Dungeons, Cooperative Chambers, and Wave Trials.
    /// Wire handler for Maps 30000+ trial chambers and Form_CreateDupMis / Form_ChooseCooperation.
    /// </summary>
    public class AC85 : AC
    {
        public override int ID => 85;

        public override void ProcessPkt(Player r, RecievePacket p)
        {
            if (r == null || p == null) return;

            if (!ExperimentalFeaturesManager.Instance.EnableInstanceDungeons)
            {
                DebugSystem.Write($"[AC85] Instance dungeons feature is disabled in experimental toggles.");
                return;
            }

            switch (p.B)
            {
                case 1: Recv1(r, p); break;  // Create / Enter Instance or Query
                case 2: Recv2(r, p); break;  // Exit / Leave Instance or Tab List
                case 3: Recv3(r, p); break;  // Create Instance
                case 4: Recv4(r, p); break;  // Pre-join / Inspect
                case 5: Recv5(r, p); break;  // Join Room / Enter
                case 6: Recv6(r, p); break;  // Exit instance
                case 7: Recv7(r, p); break;  // 0x07: Opcode 0x0755 (MAP_WARP / EVENT_SCRIPT ACK)
                case 10: Recv10(r, p); break;// Member Roster / Wave Advance
                case 11: Recv11(r, p); break;// Tabs
                case 12: Recv12(r, p); break;// Start / Launch Instance
                case 13: Recv13(r, p); break;// Dismiss member
                default:
                    DebugSystem.Write($"[AC85] Unhandled sub-opcode: AC 85,{p.B} from {r.CharName}");
                    break;
            }
        }

        /// <summary>
        /// SubCode 1: Create / Enter Instance or query room catalog.
        /// When client confirms room entry, it sends 85:1 to initiate launch.
        /// </summary>
        private void Recv1(Player p, RecievePacket r)
        {
            try
            {
                r.SetPtr(6);
                var engine = ExperimentalFeaturesManager.Instance.InstanceDungeons;
                if (engine == null) return;

                if (r.Buffer.Length >= 8)
                {
                    ushort mapId = (ushort)r.Unpack16();
                    byte maxMembers = r.Buffer.Length >= 9 ? r.Unpack8() : (byte)4;

                    var existingRoom = engine.GetPlayerRoom(p.CharID);
                    if (existingRoom != null && existingRoom.LeaderCharId == p.CharID && !existingRoom.IsInProgress)
                    {
                        // Room already created by this leader; launch the instance
                        engine.StartInstance(p);
                    }
                    else
                    {
                        // Create brand new dungeon room
                        engine.CreateRoom(p, $"{p.CharName}'s Trial", mapId, maxMembers);
                    }
                }
                else
                {
                    // Empty payload: Check if player is leader of an existing room launching the instance
                    var existingRoom = engine.GetPlayerRoom(p.CharID);
                    if (existingRoom != null && existingRoom.LeaderCharId == p.CharID && !existingRoom.IsInProgress)
                    {
                        engine.StartInstance(p);
                    }
                    else
                    {
                        engine.SendRoomList(p);
                    }
                }
            }
            catch (Exception ex)
            {
                DebugSystem.Write(new ExceptionData(ex));
            }
        }

        /// <summary>
        /// SubCode 2: Exit / Leave Instance Dungeon or Tab List.
        /// Delphi client unit MZ_DupMis.pas FUN_001a3cb0 transmits 0x55:0x02 to leave chamber.
        /// </summary>
        private void Recv2(Player p, RecievePacket r)
        {
            try
            {
                r.SetPtr(6);
                var engine = ExperimentalFeaturesManager.Instance.InstanceDungeons;
                if (engine == null) return;

                var existingRoom = engine.GetPlayerRoom(p.CharID);
                if (existingRoom != null)
                {
                    engine.LeaveRoom(p);
                }
                else if (r.Buffer.Length >= 7)
                {
                    byte tab = r.Unpack8();
                    engine.SendTabs(p, tab);
                }
                else
                {
                    engine.LeaveRoom(p);
                }
            }
            catch (Exception ex)
            {
                DebugSystem.Write(new ExceptionData(ex));
            }
        }

        /// <summary>
        /// SubCode 3: Explicit Room Creation with map and party parameters.
        /// </summary>
        private void Recv3(Player p, RecievePacket r)
        {
            try
            {
                r.SetPtr(6);
                ushort mapId = r.Buffer.Length >= 8 ? (ushort)r.Unpack16() : (ushort)30001;
                byte maxMembers = r.Buffer.Length >= 9 ? r.Unpack8() : (byte)4;
                ExperimentalFeaturesManager.Instance.InstanceDungeons?.CreateRoom(p, $"{p.CharName}'s Trial", mapId, maxMembers);
            }
            catch (Exception ex)
            {
                DebugSystem.Write(new ExceptionData(ex));
            }
        }

        /// <summary>
        /// SubCode 4: Pre-join query / room inspection.
        /// </summary>
        private void Recv4(Player p, RecievePacket r)
        {
            try
            {
                r.SetPtr(6);
                uint roomId = r.Buffer.Length >= 10 ? (uint)r.Unpack32() : (r.Buffer.Length >= 8 ? (uint)r.Unpack16() : 1001);
                ExperimentalFeaturesManager.Instance.InstanceDungeons?.QueryOrPreJoin(p, roomId);
            }
            catch (Exception ex)
            {
                DebugSystem.Write(new ExceptionData(ex));
            }
        }

        /// <summary>
        /// SubCode 5: Join an existing room.
        /// </summary>
        private void Recv5(Player p, RecievePacket r)
        {
            try
            {
                r.SetPtr(6);
                uint roomId = r.Buffer.Length >= 10 ? (uint)r.Unpack32() : (r.Buffer.Length >= 8 ? (uint)r.Unpack16() : 1001);
                ExperimentalFeaturesManager.Instance.InstanceDungeons?.JoinRoom(p, roomId);
            }
            catch (Exception ex)
            {
                DebugSystem.Write(new ExceptionData(ex));
            }
        }

        /// <summary>
        /// SubCode 6: Exit instance.
        /// </summary>
        private void Recv6(Player p, RecievePacket r)
        {
            try
            {
                r.SetPtr(6);
                ExperimentalFeaturesManager.Instance.InstanceDungeons?.LeaveRoom(p);
            }
            catch (Exception ex)
            {
                DebugSystem.Write(new ExceptionData(ex));
            }
        }

        /// <summary>
        /// SubCode 7: Map warp ACK & unfreeze mobility.
        /// </summary>
        private void Recv7(Player p, RecievePacket r)
        {
            try
            {
                r.SetPtr(6);
                DebugSystem.Write($"[AC85] Recv7 (MAP_WARP ACK) from {p.CharName}");
                p.Send(Tools.FromFormat("bb", 20, 8)); // Mobility unfreeze
            }
            catch (Exception ex)
            {
                DebugSystem.Write(new ExceptionData(ex));
            }
        }

        /// <summary>
        /// SubCode 10: Roster inspection or Wave advance signal.
        /// </summary>
        private void Recv10(Player p, RecievePacket r)
        {
            try
            {
                r.SetPtr(6);
                var engine = ExperimentalFeaturesManager.Instance.InstanceDungeons;
                if (engine == null) return;

                var existingRoom = engine.GetPlayerRoom(p.CharID);
                if (existingRoom != null && existingRoom.IsInProgress)
                {
                    byte completedWave = r.Buffer.Length >= 7 ? r.Unpack8() : (byte)1;
                    engine.OnWaveSignal(p, completedWave, 0);
                }
                else
                {
                    engine.SendRoomList(p);
                }
            }
            catch (Exception ex)
            {
                DebugSystem.Write(new ExceptionData(ex));
            }
        }

        /// <summary>
        /// SubCode 11: Tab category navigation.
        /// </summary>
        private void Recv11(Player p, RecievePacket r)
        {
            try
            {
                r.SetPtr(6);
                byte tab = r.Buffer.Length >= 7 ? r.Unpack8() : (byte)1;
                ExperimentalFeaturesManager.Instance.InstanceDungeons?.SendTabs(p, tab);
            }
            catch (Exception ex)
            {
                DebugSystem.Write(new ExceptionData(ex));
            }
        }

        /// <summary>
        /// SubCode 12: Start / Launch instance.
        /// </summary>
        private void Recv12(Player p, RecievePacket r)
        {
            try
            {
                r.SetPtr(6);
                ExperimentalFeaturesManager.Instance.InstanceDungeons?.StartInstance(p);
            }
            catch (Exception ex)
            {
                DebugSystem.Write(new ExceptionData(ex));
            }
        }

        /// <summary>
        /// SubCode 13: Leader dismisses a party member.
        /// </summary>
        private void Recv13(Player p, RecievePacket r)
        {
            try
            {
                r.SetPtr(6);
                uint targetCharId = r.Buffer.Length >= 10 ? (uint)r.Unpack32() : 0;
                ExperimentalFeaturesManager.Instance.InstanceDungeons?.DismissMember(p, targetCharId);
            }
            catch (Exception ex)
            {
                DebugSystem.Write(new ExceptionData(ex));
            }
        }
    }
}
