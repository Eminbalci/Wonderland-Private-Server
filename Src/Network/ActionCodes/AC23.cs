using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Network;
using Game;
using Game.Code;
using wlo.pserver.core.Game;

namespace Network.ActionCodes
{
    public class AC23 : AC
    {
        public override int ID { get { return 23; } }
        public override void ProcessPkt(Player r, RecievePacket p)
        {
            switch (p.B)
            {
                // case 1: Recv1(ref r, p); break;
                case 2: Recv2(r, p); break; // Get item ground
                case 3: Recv3(r, p); break; // drop item g round
                case 10: Recv10(r, p); break;//move item inv
                case 11: Recv11(r, p); break;//item selected to wear in inv
                case 12: Recv12(r, p); break; //item selected to remove
                case 15: Recv15(r, p); break; // OPen tent
                case 124: Recv124(r, p); break; //confirm destroy 
                default: Console.WriteLine("AC " + p.A + "," + p.B + " has not been coded"); break;
            }
        }
        void Recv1(Player p, RecievePacket r)
        {
            try
            {

            }
            catch (Exception t) { Console.WriteLine(t); }
        }
        void Recv2(Player p, RecievePacket r)
        {
            try
            {
                byte pos = r.Unpack8();
                ((GameMap)p.CurMap).onItemPickup(p, pos);

            }
            catch (Exception t) { Console.WriteLine(t); }
        }
        void Recv3(Player p, RecievePacket r)
        {
            try
            {
                byte pos = r.Unpack8();
                byte qnt = r.Unpack8();
                byte ukn = r.Unpack8();
                var item = p.Inv[pos];

                if (item != null)
                {

                    if (item.Dropable)
                    {
                        ((GameMap)p.CurMap).onItemDrop(p, pos, qnt);
                    }
                    else
                    {
                        // test need ASK destroy
                        SendPacket s = new SendPacket();
                        s.PackArray(new byte[] { 23, 212, 255 });
                        s.Pack8(pos);
                        s.Pack16(item.ItemID);
                        s.Pack8(qnt);
                        p.Send(s);
                    }
                }
            }
            catch (Exception t) { Console.WriteLine(t); }
        }
        void Recv10(Player p, RecievePacket r) // move item inventory
        {
            try
            {
                byte src = r[2];
                byte ammt = r[3];
                byte dst = r[4];

                if (((src > 0) && (src < 51)) && ((dst > 0) && (dst < 51)) && ((ammt > 0) && (ammt < 51)))
                    p.Inv.MoveItem(src, dst, ammt);

            }
            catch (Exception t) { Console.WriteLine(t); }
        }
        void Recv11(Player p, RecievePacket r)
        {
            try
            {

                byte loc = r[2];
                if ((loc > 0) && (loc < 51))
                {
                    //TODO do any checks here to make sure we can wear item or in Equipment class
                    p.WearEQ(loc);
                }

            }
            catch (Exception t) { Console.WriteLine(t); }
        }
        void Recv12(Player p, RecievePacket r) //item selected to remove
        {
            try
            {
                byte loc = r[2];
                byte dst = r[3];
                if ((loc > 0) && (loc < 7) && (dst > 0) && (dst < 51))
                {
                    p.unWearEQ(loc, dst);
                }
            }
            catch (Exception t) { Console.WriteLine(t); }
        }
        void Recv54(Player p, RecievePacket r)
        {
            try
            {

            }
            catch (Exception t) { Console.WriteLine(t); }
        }
        void Recv15(Player p, RecievePacket r)
        {
            try
            {
                byte pos = r.Unpack8();
                if (p.Inv[pos].ItemID == 36002)
                {
                    p.Tent.Open();
                }
            }
            catch (Exception t) { Console.WriteLine(t); }
        }
        void Recv124(Player p, RecievePacket r)
        {

            try
            {
                byte pos = r.Unpack8();
                byte qnt = r.Unpack8();
                byte ukn = r.Unpack8(); //??
                var item = p.Inv[pos];
                if (item != null)
                {
                    // test confirm destroy item
                    SendPacket s = new SendPacket();
                    s.PackArray(new byte[] { 23, 26 });
                    s.Pack16(item.ItemID);
                    s.Pack8(qnt);
                    p.Send(s);
                    p.Inv.RemoveItem(pos, qnt);
                }

            }
            catch (Exception t) { Console.WriteLine(t); }
        }
    }
}