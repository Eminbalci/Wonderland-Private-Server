using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Wonderland_Private_Server.Utilities;
using wlo.pserver.core;
using Network;
using Game;
using Game.Battle;

namespace Network.ActionCodes
{
    public class AC50 : AC
    {
        public override int ID { get { return 50; } }
        public override void ProcessPkt(Player r, RecievePacket p)
        {
            if (Game.Battle.PvEBattleManager.IsInBattle(r))
            {
                p.SetPtr(6);
                Game.Battle.PvEBattleManager.HandleBattleAction(r, (byte)p.B, p);
                return;
            }

            switch (p.B)
            {
                case 1: Recv_1(r, p); break;
                default: Console.WriteLine(p.A + "," + p.B + " Has not been coded"); break;
            }
        }
        void Recv_1(Player r, RecievePacket p) //recieve an attack command
        {
            if (r.BattleScene != null && r.BattleScene.RoundState == eBattleRoundState.ReadyState)
            {
                BattleAction tmp = new BattleAction();
                tmp.src = r.BattleScene.FindFighter(p.Unpack8(), p.Unpack8());
                tmp.dst = r.BattleScene.FindFighter(p.Unpack8(), p.Unpack8());
                //tmp.skill = new Wonderland_Private_Server.DataManagement.DataFiles.Skill();  // Commented: cross-project dependency
                //var c1 = cGlobal.gSkillManager.Get_Skill((ushort)p.Unpack16());
                //var c2 = cGlobal.gSkillManager.Get_Skill((ushort)p.Unpack32(6));
                //{
                //    tmp.skill = new Wonderland_Private_Server.DataManagement.DataFiles.Skill(c1.GetData());
                //    tmp.skill.Grade = c1.Grade;
                //    //tmp.skill.Proficiency = c1.Proficiency;
                //}
                //else
                //tmp.skill = new DataManagement.DataFiles.Skill(c2.GetData());

                tmp.unknownbyte = p.Unpack8();
                tmp.unknownbyte2 = p.Unpack8();
                r.BattleScene.PLayer_BattleAction(tmp);
            }
        }
    }
}
