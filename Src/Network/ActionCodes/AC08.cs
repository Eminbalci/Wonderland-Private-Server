using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Wonderland_Private_Server.Code.Objects;
using Wonderland_Private_Server.Utilities;
using Network;
using Game;
using RCLibrary.Core;

namespace Network.ActionCodes
{
    public class AC08 : AC
    {
        public override int ID { get { return 8; } }

        public override void ProcessPkt(Player r, RecievePacket p)
        {
            byte sub = p.B ?? 0;
            p.SetPtr(6); // Skip AC header (byte 4) and Subcode (byte 5)

            switch (sub)
            {
                case 1: Recv_1(r, p); break;
                default: Console.WriteLine(p.A + "," + p.B + " Has not been coded"); break;
            }
        }

        void Recv_1(Player r, RecievePacket p)
        {
            if (p.Buffer.Count() - p.GetPtr() < 6) return;
            byte targetType = p.Unpack8();
            byte count = p.Unpack8();
            if (count == 0) count = 1;

            DebugSystem.Write($"[AC08] Stat allocation batch: targetType={targetType}, count={count}, currentPoints={r.SkillPoints}");

            if (targetType == 0) // Player
            {
                bool anyAllocated = false;
                for (int i = 0; i < count; i++)
                {
                    if (p.Buffer.Count() - p.GetPtr() < 5) break;
                    byte statId = p.Unpack8();
                    uint amount = p.Unpack32();

                    if (r.SkillPoints >= amount && amount > 0)
                    {
                        r.SkillPoints -= (ushort)amount;
                        switch (statId)
                        {
                            case 28: r.baseStr += (ushort)amount; break;
                            case 29: r.baseCon += (ushort)amount; break;
                            case 27: r.baseInt += (ushort)amount; break;
                            case 33: r.baseWis += (ushort)amount; break;
                            case 30: r.baseAgi += (ushort)amount; break;
                        }
                        anyAllocated = true;
                        DebugSystem.Write($"[AC08] Allocated {amount} to stat {statId}. Remaining points: {r.SkillPoints}");
                    }
                }

                if (anyAllocated)
                {
                    DebugSystem.Write($"[AC08] Batch complete. New base: STR={r.baseStr}, CON={r.baseCon}, INT={r.baseInt}, WIS={r.baseWis}, AGI={r.baseAgi}, remainingPts={r.SkillPoints}");
                    r.Send8_1(true);
                    Game.SkillRelated.SkillManager.CheckAndUnlockProgressionSkills(r);
                }
            }
        }
    }
}

