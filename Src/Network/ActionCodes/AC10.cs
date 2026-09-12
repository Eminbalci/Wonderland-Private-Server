using System;
using System.Linq;
using Game;
using Game.PlayerRelated;
using Network;

namespace Network.ActionCodes
{
    /// <summary>
    /// AC 10: Social Relations & Friend List Operations Protocol.
    /// Handles Add Friend Request, Response (Accept/Decline), List Query, and Friend Deletion.
    /// </summary>
    public class AC10 : AC
    {
        public override int ID => 10;

        public override void ProcessPkt(Player c, RecievePacket p)
        {
            if (c == null || p == null) return;

            byte subAction = p.B ?? 1;
            DebugSystem.Write($"[AC10] Player {c.CharName} sent Friend Action SubCode={subAction}");

            switch (subAction)
            {
                case 1: // Add Friend Request
                    HandleAddFriend(c, p);
                    break;
                case 2: // Accept / Decline Friend Invitation
                    HandleFriendResponse(c, p);
                    break;
                case 3: // Request Friend List
                    HandleRequestList(c);
                    break;
                case 4: // Delete Friend
                    HandleDeleteFriend(c, p);
                    break;
                case 6: // Query Online Status
                    HandleQueryStatus(c, p);
                    break;
                default:
                    DebugSystem.Write($"[AC10] Unhandled Friend SubAction: {subAction}");
                    break;
            }
        }

        private void HandleAddFriend(Player c, RecievePacket p)
        {
            try
            {
                uint targetID = p.Unpack32();
                if (c.CurMap is GameMap curMap)
                {
                    Player target = curMap.PlayersList.FirstOrDefault(x => x.CharID == targetID);
                    if (target != null)
                    {
                        c.MyFriends?.AddFriend(target);
                        target.MyFriends?.AddFriend(c);

                        // Notify both players
                        SendFriendStatus(c, target, 1);
                        SendFriendStatus(target, c, 1);
                        DebugSystem.Write($"[AC10.AddFriend] {c.CharName} added {target.CharName}");
                    }
                }
            }
            catch (Exception ex)
            {
                DebugSystem.Write(new ExceptionData(ex));
            }
        }

        private void HandleFriendResponse(Player c, RecievePacket p)
        {
            try
            {
                uint targetID = p.Unpack32();
                byte accept = p.Unpack8();
                if (accept == 1 && c.CurMap is GameMap curMap)
                {
                    Player target = curMap.PlayersList.FirstOrDefault(x => x.CharID == targetID);
                    if (target != null)
                    {
                        c.MyFriends?.AddFriend(target);
                        target.MyFriends?.AddFriend(c);
                    }
                }
            }
            catch (Exception ex)
            {
                DebugSystem.Write(new ExceptionData(ex));
            }
        }

        private void HandleRequestList(Player c)
        {
            try
            {
                c.MyFriends?.SendFriendList();
                AC14.SendFriendList(c);
            }
            catch (Exception ex)
            {
                DebugSystem.Write(new ExceptionData(ex));
            }
        }

        private void HandleDeleteFriend(Player c, RecievePacket p)
        {
            try
            {
                uint targetID = p.Unpack32();
                c.MyFriends?.DelFriend(targetID);

                SendPacket resp = new SendPacket();
                resp.Pack8((byte)ID);
                resp.Pack8(4);
                resp.Pack32(targetID);
                c.Send(resp);
                DebugSystem.Write($"[AC10.DeleteFriend] {c.CharName} deleted friend ID={targetID}");
            }
            catch (Exception ex)
            {
                DebugSystem.Write(new ExceptionData(ex));
            }
        }

        private void HandleQueryStatus(Player c, RecievePacket p)
        {
            try
            {
                c.MyFriends?.SendFriendList();
                AC14.SendFriendList(c);
            }
            catch (Exception ex)
            {
                DebugSystem.Write(new ExceptionData(ex));
            }
        }

        private void SendFriendStatus(Player to, Player target, byte status)
        {
            try
            {
                SendPacket sp = new SendPacket();
                sp.Pack8((byte)ID);
                sp.Pack8(1); // Status update
                sp.Pack32(target.CharID);
                sp.Pack8(status); // 1 = Online, 0 = Offline
                sp.PackString(target.CharName ?? "Friend");
                to.Send(sp);
            }
            catch (Exception ex)
            {
                DebugSystem.Write(new ExceptionData(ex));
            }
        }
    }
}
