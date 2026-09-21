using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Network;
using Game;
using Game.Maps;
using Wonderland_Private_Server.Utilities;

namespace Network.ActionCodes
{
    public class AC65 : AC
    {
        public override int ID { get { return 65; } }
        public override void ProcessPkt(Player r, RecievePacket p)
        {
            switch (p.B)
            {
                case 1: Recv1(r, p); break; // Enter tent
                case 2: Recv2(r, p); break; // Item canceled
                case 3: Recv3(r, p); break; // Exit tent
                default: Console.WriteLine("AC " + p.A + "," + p.B + " has not been coded"); break;
            }
        }
        void Recv1(Player r, RecievePacket p)
        {
            ((GameMap)r.CurMap).onEnterTent(p.Unpack32(), r);
        }
        void Recv2(Player r, RecievePacket p)
        {
            // Right-click / Pack up tent on world map
            if (r != null && r.Tent != null)
            {
                r.Tent.Close();
                DebugSystem.Write($"[AC65.Recv2] Player {r.CharName} closed / packed up tent.");
            }
        }
        void Recv3(Player r, RecievePacket p)
        {
            if (r == null) return;

            // Exit tent - warp player back to saved overworld location
            ushort dstMap = 0, dstX = 0, dstY = 0;
            if (r.TentReturnMap != null && r.TentReturnMap.DstMap > 0)
            {
                dstMap = r.TentReturnMap.DstMap;
                dstX = r.TentReturnMap.DstX_Axis;
                dstY = r.TentReturnMap.DstY_Axis;
            }
            else if (r.Tent != null && r.Tent.OwnerMap != null)
            {
                dstMap = (ushort)r.Tent.OwnerMap.MapID;
                dstX = (ushort)r.Tent.X;
                dstY = (ushort)r.Tent.Y;
            }
            else if (r.PrevMap != null && r.PrevMap.DstMap > 0)
            {
                dstMap = r.PrevMap.DstMap;
                dstX = r.PrevMap.DstX_Axis;
                dstY = r.PrevMap.DstY_Axis;
            }
            else
            {
                dstMap = 12000;
                dstX = 892;
                dstY = 734;
            }

            if (r.CurMap != null && (r.CurMap.Type == MapType.Tent || r.CurMap is Game.Code.Tent))
            {
                WarpData warp = new WarpData()
                {
                    DstMap = dstMap,
                    DstX_Axis = dstX,
                    DstY_Axis = dstY
                };
                r.CurMap.Teleport(TeleportType.CmD, r, 0, warp);
                DebugSystem.Write($"[AC65.Recv3] Player {r.CharName} exited tent to Map {dstMap} ({dstX},{dstY})");
            }
        }
    }
}
