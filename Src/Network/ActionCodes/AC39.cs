using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Wonderland_Private_Server.Code.Objects;
using Network;
using Game;
using Game.Code;
using wlo.pserver.core;

namespace Network.ActionCodes
{
    public class AC39 : AC
    {
        public override int ID { get { return 39; } }
        public override void ProcessPkt(Player p, RecievePacket r)
        {

            switch (r.B)
            {
                // case 1: Recv1(ref r, p); break;
                case 2: Recv2(p, r); break;// request NEW MEMBER TO GUILD
                case 3: Recv3(p, r); break; // acept resquest guild
                case 4: Recv4(p, r); break; // Guild EMAIL
                case 6: Recv6(p, r); break;// leave guild
                case 7: Recv7(p, r); break; // Demiss member
                case 8: Recv8(p, r); break; // TAB MESSAGE
                case 9: Recv9(p, r); break; // edit rule
                case 11: Recv11(p, r); break;//Remove HOLD THE POST OF VIC ORG
                case 14: Recv14(p, r); break;//HOLD THE POST OF VICE ORGLEADER
                case 16: Recv16(p, r); break;//PERMISSION
                case 18: Recv18(p, r); break; // change insigna guild
                default: Console.WriteLine("AC " + r.A + "," + r.B + " has not been coded"); break;
            }
        }
        void Recv2(Player p, RecievePacket r)
        {
            try
            {
                uint m = r.Unpack32(); // get request member id               

                if (((GameMap)p.CurMap).PlayersList.Any(pl => pl.CharID == m))
                {
                    //  ((GameMap)p.CurMap).Players[m].GuildID = ((dynamic)p.CurGuild).GuildID;

                    SendPacket s = new SendPacket();
                    s.PackArray(new byte[] { 39, 3 });
                    s.Pack32(p.UserID);
                    cGlobal.WLO_World.BroadcastTo(s, directTo: m);
                }
            }
            catch (Exception t) { Console.WriteLine(t); }
        }
        void Recv3(Player p, RecievePacket r)
        {
            try
            {
                uint m = r.Unpack32(); // get request member id
                if (((GameMap)p.CurMap).PlayersList.Any(pl => pl.CharID == m))
                {
                    ((dynamic)((GameMap)p.CurMap).PlayersList.First(pl => pl.CharID == m).CurGuild).AddNewMemberGuild(p, m);
                }

            }
            catch (Exception t) { Console.WriteLine(t); }
        }
        void Recv4(Player p, RecievePacket r)
        {
            try
            {
                //uint dst = r.Unpack32(3); // get member id
                //string text = r.UnpackNChar(7);
                //((dynamic)p.CurGuild).GuilMail(p.UserID,dst, text);
            }
            catch (Exception t) { Console.WriteLine(t); }
        }
        void Recv6(Player p, RecievePacket r)
        {
            try
            {
                ((dynamic)p.CurGuild).LeaveGuild(p);
            }
            catch (Exception t) { Console.WriteLine(t); }
        }
        void Recv7(Player p, RecievePacket r)
        {
            uint target = r.Unpack32();
            try
            {
                if (((dynamic)p.CurGuild).Leader.ID == p.UserID)
                {
                    ((dynamic)p.CurGuild).Dismiss(target, p.UserID);
                }
            }
            catch (Exception t) { Console.WriteLine(t); }
        }
        void Recv8(Player p, RecievePacket r)
        {
            try
            {
                string msg = r.UnpackString();
                if (msg.Length > 0)
                {
                    if (((dynamic)p.Guild) != null)
                        ((dynamic)p.Guild).BroadCast(((dynamic)p.Guild).ID, msg, p.UserID);
                }
            }
            catch (Exception t) { Console.WriteLine(t); }
        }

        void Recv9(Player p, RecievePacket r)
        {
            try
            {
                if (((dynamic)p.CurGuild).Leader.ID == p.UserID)
                {

                    ((dynamic)p.CurGuild).Edit_Rule(r.UnpackStringN());
                }
            }
            catch (Exception t) { Console.WriteLine(t); }
        }
        void Recv11(Player p, RecievePacket r)
        {
            try
            {
                uint target = r.Unpack32();

                if (((dynamic)p.CurGuild).Leader.ID == p.UserID)
                {
                    ((dynamic)p.CurGuild).RemoveHoldThePostOfViceOrgleader(p, target);
                }
            }
            catch (Exception t) { Console.WriteLine(t); }
        }
        void Recv14(Player p, RecievePacket r)
        {
            try
            {
                uint target = r.Unpack32();

                if (((dynamic)p.CurGuild).Leader.ID == p.UserID)
                {
                    ((dynamic)p.CurGuild).HoldThePostOfViceOrgleader(p, target);
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

                if (((dynamic)p.CurGuild).Leader.ID == p.UserID)
                {
                    ((dynamic)p.CurGuild).Permission(target, p, pos);
                }
            }
            catch (Exception t) { Console.WriteLine(t); }
        }
        void Recv18(Player p, RecievePacket r)
        {
            try
            {
                // byte[] a = r.UnpackArray(4);
                byte[] a = new byte[4];
                for (int i = 0; i < 4; i++) a[i] = r.Unpack8();
                ((dynamic)p.CurGuild).ChangeInsigna(a);
            }
            catch (Exception t) { Console.WriteLine(t); }
        }
    }
}