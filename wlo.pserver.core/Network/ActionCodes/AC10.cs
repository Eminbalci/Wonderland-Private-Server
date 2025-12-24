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
                                // Send request to target? Or directly add?
                                // WLO usually sends request. 
                                // But p.m_friendlist.AddFriend(target) seems to handle adding directly or sending generic response.
                                // Let's check logic: AddFriend calls SendPacket which sends AC 14,9 (Success?)
                                // It adds to array immediately.
                                p.m_friendlist.AddFriend(target);

                                // Also add inviter to target's list?
                                if (target.m_friendlist != null)
                                    target.m_friendlist.AddFriend(p);
                            }
                        }
                    }
                    catch { }
                    break;

                case 3: // Request Friend List
                    // Code.PlayerRelated.Friendlist has SendFriendList()
                    if (p.m_friendlist != null)
                        p.m_friendlist.SendFriendList();
                    break;

                case 4: // Delete Friend
                    try
                    {
                        uint targetID = r.Unpack32();
                        if (p.m_friendlist != null)
                            p.m_friendlist.DelFriend(targetID);
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
