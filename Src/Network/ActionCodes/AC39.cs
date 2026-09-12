using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Network;
using Game;
using Game.Code;
using Game.PlayerRelated;
using wlo.pserver.core;

namespace Network.ActionCodes
{
    public class AC39 : AC
    {
        public override int ID { get { return 39; } }
        public override void ProcessPkt(Player p, RecievePacket r)
        {
            byte sub = r.B ?? 0;
            switch (sub)
            {
                case 1:
                    Game.QuestRelated.QuestManager.SendQuestJournal(p);
                    break;
                case 2: Recv2(p, r); break; // request NEW MEMBER TO GUILD / quest help
                case 3: Recv3(p, r); break; // accept request guild
                case 4: break;              // Guild EMAIL
                case 5:
                case 10:
                    p.Send(Tools.FromFormat("bbb", 39, sub, 1));
                    break;
                case 6: Recv6(p); break;    // leave guild
                case 7: Recv7(p, r); break; // Dismiss member / Abandon quest
                case 8: Recv8(p, r); break; // TAB MESSAGE
                case 9: Recv9(p, r); break; // edit rule
                case 11: Recv11(p, r); break; // Remove HOLD THE POST OF VIC ORG
                case 12:
                    p.Send(Tools.FromFormat("bbb", 39, 12, 0)); // Empty guild member list
                    break;
                case 14: Recv14(p, r); break; // HOLD THE POST OF VICE ORGLEADER
                case 16:
                case 17:
                case 19:
                case 50:
                case 51:
                    p.Send(Tools.FromFormat("bbb", 39, sub, 1));
                    break;
                case 18: Recv18(p, r); break; // change insignia guild
                default: Console.WriteLine("AC " + r.A + "," + r.B + " has not been coded"); break;
            }
        }

        void Recv2(Player p, RecievePacket r)
        {
            try
            {
                uint m = r.Unpack32(); // get request member id               

                var targetPl = ((GameMap)p.CurMap)?.PlayersList.FirstOrDefault(pl => pl.CharID == m);
                if (targetPl != null)
                {
                    SendPacket s = new SendPacket();
                    s.PackArray(new byte[] { 39, 3 });
                    s.Pack32(p.UserID);
                    targetPl.Send(s);
                }
            }
            catch (Exception t) { Console.WriteLine(t); }
        }

        void Recv3(Player p, RecievePacket r)
        {
            try
            {
                uint m = r.Unpack32(); // get request member id
                var leader = ((GameMap)p.CurMap)?.PlayersList.FirstOrDefault(pl => pl.CharID == m);
                if (leader?.Guild != null)
                {
                    leader.Guild.AddNewMemberGuild(p, m);
                }
            }
            catch (Exception t) { Console.WriteLine(t); }
        }

        void Recv6(Player p)
        {
            try
            {
                p.Guild?.LeaveGuild(p);
            }
            catch (Exception t) { Console.WriteLine(t); }
        }

        void Recv7(Player p, RecievePacket r)
        {
            uint target = r.Unpack32();
            try
            {
                if (p.Guild != null && p.Guild.Leader?.ID == p.UserID)
                {
                    p.Guild.Dismiss(target, p.UserID);
                }
            }
            catch (Exception t) { Console.WriteLine(t); }
        }

        void Recv8(Player p, RecievePacket r)
        {
            try
            {
                string msg = r.UnpackString();
                if (!string.IsNullOrEmpty(msg) && p.Guild != null)
                {
                    p.Guild.BroadCast(p.Guild.ID, msg, p.UserID);
                }
            }
            catch (Exception t) { Console.WriteLine(t); }
        }

        void Recv9(Player p, RecievePacket r)
        {
            try
            {
                if (p.Guild != null && p.Guild.Leader?.ID == p.UserID)
                {
                    p.Guild.Edit_Rule(r.UnpackStringN());
                }
            }
            catch (Exception t) { Console.WriteLine(t); }
        }

        void Recv11(Player p, RecievePacket r)
        {
            try
            {
                uint target = r.Unpack32();

                if (p.Guild != null && p.Guild.Leader?.ID == p.UserID)
                {
                    p.Guild.RemoveHoldThePostOfViceOrgleader(p, target);
                }
            }
            catch (Exception t) { Console.WriteLine(t); }
        }

        void Recv14(Player p, RecievePacket r)
        {
            try
            {
                uint target = r.Unpack32();

                if (p.Guild != null && p.Guild.Leader?.ID == p.UserID)
                {
                    p.Guild.HoldThePostOfViceOrgleader(p, target);
                }
            }
            catch (Exception t) { Console.WriteLine(t); }
        }

        void Recv16(Player p, RecievePacket r)
        {
            try
            {
                uint target = r.Unpack32();
                byte pos = r.Unpack8();

                if (p.Guild != null && p.Guild.Leader?.ID == p.UserID)
                {
                    p.Guild.Permission(target, p, pos);
                }
            }
            catch (Exception t) { Console.WriteLine(t); }
        }

        void Recv18(Player p, RecievePacket r)
        {
            try
            {
                byte[] a = new byte[4];
                for (int i = 0; i < 4; i++) a[i] = r.Unpack8();
                p.Guild?.ChangeInsigna(a);
            }
            catch (Exception t) { Console.WriteLine(t); }
        }
    }
}