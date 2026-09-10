using System;
using Game;
using Network;

namespace Network.ActionCodes
{
    /// <summary>
    /// AC 26: Team Matchmaking & Party Recruitment Board Protocol.
    /// Handles listing party vacancies, browsing active team recruitments, and applying to open slots.
    /// </summary>
    public class AC26 : AC
    {
        public override int ID => 26;

        public override void ProcessPkt(Player c, RecievePacket p)
        {
            if (c == null || p == null) return;

            byte subCode = p.B ?? 1;
            DebugSystem.Write($"[AC26] Party Recruitment action from {c.CharName}: SubCode={subCode}");

            try
            {
                switch (subCode)
                {
                    case 1: // Browse Recruitment Listings
                        SendRecruitmentList(c);
                        break;
                    case 2: // Post Party Vacancy Notice
                        SendPacket resp2 = new SendPacket();
                        resp2.Pack8((byte)ID);
                        resp2.Pack8(2);
                        resp2.Pack8(1); // 1 = Posted
                        c.Send(resp2);
                        break;
                    case 3: // Apply to Join Recruited Party
                        SendPacket resp3 = new SendPacket();
                        resp3.Pack8((byte)ID);
                        resp3.Pack8(3);
                        resp3.Pack8(1); // 1 = Application Sent
                        c.Send(resp3);
                        break;
                    case 4: // Cancel Party Recruitment
                        SendPacket resp4 = new SendPacket();
                        resp4.Pack8((byte)ID);
                        resp4.Pack8(4);
                        resp4.Pack8(1);
                        c.Send(resp4);
                        break;
                }
            }
            catch (Exception ex)
            {
                DebugSystem.Write(new ExceptionData(ex));
            }
        }

        private void SendRecruitmentList(Player c)
        {
            SendPacket sp = new SendPacket();
            sp.Pack8((byte)ID);
            sp.Pack8(1); // List response
            sp.Pack8(0); // Count = 0 (or populate active teams)
            c.Send(sp);
        }
    }
}
