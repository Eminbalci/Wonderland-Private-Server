using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Game;
using Wonderland_Private_Server.Code.Objects;

namespace Wonderland_Private_Server.ActionCodes
{
    public class AC10 : AC
    {
        public override int ID { get { return 10; } }

        public override void ProcessPkt(ref Player p, RecvPacket r)
        {
            // Friend Actions
            // Structure assumptions based on common WLO protocols
            // 1: Add Friend Request
            // 2: Accept/Decline?
            // 3: Request List?
            // 4: Delete Friend?

            switch (r.B)
            {
                case 1: // Add Friend Request
                    try
                    {
                        uint targetID = r.Unpack32();
                        if (p.CurMap != null)
                        {
                            var target = p.CurMap.PlayersList.FirstOrDefault(x => x.CharID == targetID);
                            if (target != null)
                            {
                                p.MyFriends?.AddFriend(target);
                                target.MyFriends?.AddFriend(p);
                            }
                        }
                    }
                    catch { }
                    break;

                case 3: // Request Friend List
                    p.MyFriends?.SendFriendList();
                    break;

                case 4: // Delete Friend
                    try
                    {
                        uint targetID = r.Unpack32();
                        p.MyFriends?.DelFriend(targetID);
                    }
                    catch { }
                    break;

                default:
                    Console.WriteLine($"[AC10] Unknown SubAction: {r.B}");
                    break;
            }
        }
    }
}
