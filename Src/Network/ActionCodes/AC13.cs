using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Game;
using Game.Code; // For DebugSystem
using Game.Maps; // For GameMap
using Network;
using RCLibrary.Core.Networking;

namespace Network.ActionCodes
{
    public class AC13 : AC
    {
        public override int ID { get { return 13; } }

        public override void ProcessPkt(Player p, RecievePacket r)
        {
            r.SetPtr(6);
            switch (r.B)
            {
                case 1: Recv1(ref p, r); break; // Invite Request / Join Request
                case 2: Recv2(ref p, r); break; // Invite Reply
                case 3: Recv3(ref p, r); break; // Invite Response / Accept
                case 4: Recv4(ref p, r); break; // Leave Team
                case 9: Recv9(ref p, r); break; // Kick from Party
                case 10: Recv10(ref p, r); break; // Transfer Leadership
                case 238: Recv238(ref p, r); break; // Item Mall query (authentic itemmall.pcapng)
                default:
                    DebugSystem.Write(DebugItemType.Error, $"[DEBUG] AC13 Unknown SubAction: {r.B}");
                    break;
            }
        }

        void Recv238(ref Player p, RecievePacket r)
        {
            try
            {
                // Authentic Item Mall Confirmation (Pkt #109, #113 in itemmall.pcapng)
                SendPacket s = new SendPacket();
                s.Pack8(13);
                s.Pack8(42);
                s.Pack32(p.CharID);
                p.Send(s);

                // Open Web Item Mall Portal with Point Balance & Shop
                Game.PlayerRelated.ItemMallManager.SendPointBalance(p);

                DebugSystem.Write(DebugItemType.Error, $"[AC13.Recv238] Item Mall query confirmed (13:42) and Web Shop opened for {p.CharName}.");
            }
            catch (Exception ex)
            {
                DebugSystem.Write(DebugItemType.Error, $"[AC13.Recv238] Error: {ex.Message}");
            }
        }

        void Recv1(ref Player p, RecievePacket r)
        {
            // Invite Request / Join Request (AC 13 Sub 1: 0d 01 <charId>)
            try
            {
                uint targetID = r.Unpack32();
                Player target = null;
                GameMap map = p.CurMap as GameMap;

                if (map != null)
                {
                    target = map.PlayersList.FirstOrDefault(x => x.CharID == targetID)
                             ?? map.PlayersList.FirstOrDefault(x => x.CharID == (targetID >> 8));
                }

                if (target != null)
                {
                    DebugSystem.Write(DebugItemType.Error, $"[DEBUG] AC13 Recv1 Target Found: {target.CharName} (ID: {target.CharID}). Forwarding request...");

                    SendPacket s = new SendPacket();
                    s.PackArray(new byte[] { 13, 1 });
                    s.Pack32(p.CharID);
                    s.PackString(p.CharName);
                    target.Send(s);

                    DebugSystem.Write(DebugItemType.Error, $"[DEBUG] Request packet sent to {target.CharName}");
                }
                else
                {
                    DebugSystem.Write(DebugItemType.Error, $"[DEBUG] Target {targetID} NOT found in map.");
                }
            }
            catch (Exception ex)
            {
                DebugSystem.Write(DebugItemType.Error, $"[DEBUG] AC13 Recv1 Exception: {ex.Message}");
            }
        }

        void Recv2(ref Player p, RecievePacket r)
        {
            DebugSystem.Write(DebugItemType.Error, $"[DEBUG] AC13 Recv2 called (UNEXPECTED).");
        }

        void Recv3(ref Player p, RecievePacket r)
        {
            // Invite Response / Join Team (AC 13 Sub 3: 0d 03 <reply> <requesterId>)
            try
            {
                byte reply = r.Unpack8();
                uint requesterID = r.Unpack32();

                DebugSystem.Write(DebugItemType.Error, $"[DEBUG] AC13 Recv3 (Join). Reply: {reply}, RequesterID: {requesterID}");

                if (reply == 1 || reply == 3)
                {
                    Player requester = null;
                    GameMap map = p.CurMap as GameMap;

                    if (map != null)
                    {
                        requester = map.PlayersList.FirstOrDefault(x => x.CharID == requesterID)
                                    ?? map.PlayersList.FirstOrDefault(x => x.CharID == (requesterID >> 8));
                    }

                    if (requester != null)
                    {
                        DebugSystem.Write(DebugItemType.Error, $"[DEBUG] Requester {requester.CharName} found. Adding to {p.CharName}'s team...");
                        requester.JoinParty(p);
                        DebugSystem.Write(DebugItemType.Error, $"[DEBUG] Requester {requester.CharName} joined {p.CharName}.");
                    }
                    else
                    {
                        DebugSystem.Write(DebugItemType.Error, $"[DEBUG] Requester {requesterID} NOT found in map.");
                    }
                }
                else
                {
                    DebugSystem.Write(DebugItemType.Error, $"[DEBUG] AC13 Recv3: Reply was {reply} (Declined)");
                }
            }
            catch (Exception ex)
            {
                DebugSystem.Write(DebugItemType.Error, $"[DEBUG] AC13 Recv3 Exception: {ex.Message}");
            }
        }

        void Recv4(ref Player p, RecievePacket r)
        {
            // Leave Team
            try
            {
                DebugSystem.Write(DebugItemType.Error, $"[DEBUG] AC13 Recv4 Leave Team Request from {p.CharName}");
                p.LeaveParty();
            }
            catch (Exception ex)
            {
                DebugSystem.Write(DebugItemType.Error, $"[DEBUG] AC13 Recv4 Exception: {ex.Message}");
            }
        }

        void Recv9(ref Player p, RecievePacket r)
        {
            // Kick from Party (AC 13 Sub 9: 0d 09 <targetId>)
            try
            {
                uint targetID = r.Unpack32();
                if (p.m_teammembers != null && !p.m_teammembers.Any(x => x.CharID == targetID))
                {
                    targetID = targetID >> 8;
                }

                DebugSystem.Write(DebugItemType.Error, $"[DEBUG] AC13 Recv9 Kick. Leader: {p.CharName}, Target: {targetID}");
                p.KickPartyMember(targetID);
            }
            catch (Exception ex)
            {
                DebugSystem.Write(DebugItemType.Error, $"[DEBUG] AC13 Recv9 Exception: {ex.Message}");
            }
        }

        void Recv10(ref Player p, RecievePacket r)
        {
            // Transfer Leadership (AC 13 Sub 10: 0d 0a <newLeaderId>)
            try
            {
                uint targetID = r.Unpack32();
                uint newLeaderID = targetID;

                if (p.m_teammembers != null)
                {
                    Player newLeader = p.m_teammembers.FirstOrDefault(x => x.CharID == newLeaderID)
                                       ?? p.m_teammembers.FirstOrDefault(x => x.CharID == (targetID >> 8));

                    if (newLeader != null)
                    {
                        p.TransferLeadership(newLeader);
                        DebugSystem.Write(DebugItemType.Error, $"[DEBUG] Leadership transferred to {newLeader.CharName}");
                    }
                    else
                    {
                        DebugSystem.Write(DebugItemType.Error, $"[DEBUG] New leader {newLeaderID} not found in party. Party size: {p.m_teammembers.Count}");
                    }
                }
                else
                {
                    DebugSystem.Write(DebugItemType.Error, $"[DEBUG] Player {p.CharName} has no party!");
                }
            }
            catch (Exception ex)
            {
                DebugSystem.Write(DebugItemType.Error, $"[DEBUG] AC13 Recv10 Exception: {ex.Message}");
            }
        }
    }
}
