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
                case 1: Recv1(r, p); break;  // Update list instances
                case 2: Recv2(r, p); break;  // Tab list instances
                case 3: Recv3(r, p); break;  // Create instance
                case 4: Recv4(r, p); break;  // Pre-join
                case 5: Recv5(r, p); break;  // Join
                case 6: Recv6(r, p); break;  // Exit instance
                case 7: Recv7(r, p); break;  // 0x07: Opcode 0x0755 (MAP_WARP / EVENT_SCRIPT ACK)
                case 10: Recv10(r, p); break;// Check members
                case 11: Recv11(r, p); break;// Move tab members
                case 13: Recv13(r, p); break;// Dismiss member
                default:
                    DebugSystem.Write($"[AC85] Unhandled sub-opcode: AC 85,{p.B} from {r.CharName}");
                    break;
            }
        }

        private void Recv1(Player p, RecievePacket r)
        {
            try
            {
                ExperimentalFeaturesManager.Instance.InstanceDungeons.SendRoomList(p);
            }
            catch (Exception ex)
            {
                DebugSystem.Write(new ExceptionData(ex));
            }
        }

        private void Recv2(Player p, RecievePacket r)
        {
            try
            {
                byte tab = r.Buffer.Length >= 7 ? r.Unpack8() : (byte)1;
                ExperimentalFeaturesManager.Instance.InstanceDungeons.SendTabs(p, tab);
            }
            catch (Exception ex)
            {
                DebugSystem.Write(new ExceptionData(ex));
            }
        }

        private void Recv3(Player p, RecievePacket r)
        {
            try
            {
                ushort mapId = r.Buffer.Length >= 8 ? (ushort)r.Unpack16() : (ushort)30001;
                byte maxMembers = r.Buffer.Length >= 9 ? r.Unpack8() : (byte)4;
                ExperimentalFeaturesManager.Instance.InstanceDungeons.CreateRoom(p, $"{p.CharName}'s Trial", mapId, maxMembers);
            }
            catch (Exception ex)
            {
                DebugSystem.Write(new ExceptionData(ex));
            }
        }

        private void Recv4(Player p, RecievePacket r)
        {
            try
            {
                uint roomId = r.Buffer.Length >= 8 ? (uint)r.Unpack16() : 1001;
                // Pre-join ACK
                SendPacket ack = new SendPacket();
                ack.Pack8(85);
                ack.Pack8(4);
                ack.Pack32(roomId);
                ack.Pack8(1);
                p.Send(ack);
            }
            catch (Exception ex)
            {
                DebugSystem.Write(new ExceptionData(ex));
            }
        }

        private void Recv5(Player p, RecievePacket r)
        {
            try
            {
                uint roomId = r.Buffer.Length >= 8 ? (uint)r.Unpack16() : 1001;
                ExperimentalFeaturesManager.Instance.InstanceDungeons.JoinRoom(p, roomId);
            }
            catch (Exception ex)
            {
                DebugSystem.Write(new ExceptionData(ex));
            }
        }

        private void Recv6(Player p, RecievePacket r)
        {
            try
            {
                ExperimentalFeaturesManager.Instance.InstanceDungeons.LeaveRoom(p);
            }
            catch (Exception ex)
            {
                DebugSystem.Write(new ExceptionData(ex));
            }
        }

        private void Recv10(Player p, RecievePacket r)
        {
            try
            {
                ExperimentalFeaturesManager.Instance.InstanceDungeons.SendRoomList(p);
            }
            catch (Exception ex)
            {
                DebugSystem.Write(new ExceptionData(ex));
            }
        }

        private void Recv11(Player p, RecievePacket r)
        {
            try
            {
                byte tab = r.Buffer.Length >= 7 ? r.Unpack8() : (byte)1;
                ExperimentalFeaturesManager.Instance.InstanceDungeons.SendTabs(p, tab);
            }
            catch (Exception ex)
            {
                DebugSystem.Write(new ExceptionData(ex));
            }
        }

        private void Recv13(Player p, RecievePacket r)
        {
            try
            {
                uint targetCharId = r.Buffer.Length >= 10 ? (uint)r.Unpack32() : 0;
                ExperimentalFeaturesManager.Instance.InstanceDungeons.DismissMember(p, targetCharId);
            }
            catch (Exception ex)
            {
                DebugSystem.Write(new ExceptionData(ex));
            }
        }
            private void Recv7(Player p, RecievePacket r)
        {
            try
            {
                DebugSystem.Write($"[AC85] Recv7 (MAP_WARP ACK) from {p.CharName}");
                p.Send(Tools.FromFormat("bb", 20, 8)); // Mobility unfreeze
            }
            catch (Exception ex)
            {
                DebugSystem.Write(new ExceptionData(ex));
            }
        }
}
}