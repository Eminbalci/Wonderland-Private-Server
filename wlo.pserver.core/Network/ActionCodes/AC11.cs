using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Game;
using Game.Battle;

namespace Network.ActionCodes
{
    public class AC11 : AC
    {
        public override int ID { get { return 11; } }

        public override void ProcessPkt(Player p, RecievePacket r)
        {
            if (p == null) return;

            byte subAction = r.B ?? 0;
            DebugSystem.Write($"[AC11] Player {p.CharName} sent PvP SubAction {subAction}");

            switch (subAction)
            {
                case 1: // Request Duel with Target
                    try
                    {
                        uint targetCharId = r.Unpack32();
                        Player target = null;
                        if (p.CurMap is GameMap curMap)
                        {
                            target = curMap.PlayersList.FirstOrDefault(x => x.CharID == targetCharId);
                        }

                        if (target != null)
                        {
                            PvPManager.RequestDuel(p, target);
                        }
                    }
                    catch (Exception ex)
                    {
                        DebugSystem.Write($"[AC11:1] Exception requesting duel: {ex.Message}");
                    }
                    break;

                case 2: // Duel Response
                    try
                    {
                        byte response = r.Unpack8();
                        if (response == 1)
                        {
                            PvPManager.AcceptDuel(p);
                        }
                        else
                        {
                            PvPManager.DeclineDuel(p);
                        }
                    }
                    catch (Exception ex)
                    {
                        DebugSystem.Write($"[AC11:2] Exception responding to duel: {ex.Message}");
                    }
                    break;

                default:
                    DebugSystem.Write($"[AC11] Unhandled PvP subaction {subAction}");
                    break;
            }
        }
    }
}
