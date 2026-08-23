using System;
using Game;
using Game.Maps;
using Wonderland_Private_Server.Code.Objects;

namespace Network.ActionCodes
{
    public class AC05 : AC
    {
        public override int ID { get { return 5; } }

        public override void ProcessPkt(Player c, RecievePacket p)
        {
            switch (p.B)
            {
                case 7:
                    Recv7(c, p);
                    break;
                case 17:
                    Recv17(c, p);
                    break;
                case 4:
                    Recv4(c, p);
                    break;
                default:
                    DebugSystem.Write($"[AC5] Subcode {p.B} received from {c.CharName}");
                    break;
            }
        }

        private void Recv17(Player c, RecievePacket p)
        {
            try
            {
                if (c == null || c.CurMap == null) return;

                byte destChoice = p.Buffer.Length > 6 ? p[6] : (byte)1;
                WarpData warp = new WarpData();

                switch (destChoice)
                {
                    case 3: // Carnie (Carnival)
                        if (c.CurMap != null && c.CurMap.MapID != 11094)
                        {
                            c.CarnieReturnMap = new WarpData()
                            {
                                DstMap = (ushort)c.CurMap.MapID,
                                DstX_Axis = (ushort)c.CurX,
                                DstY_Axis = (ushort)c.CurY
                            };
                        }
                        warp.DstMap = 11094;
                        warp.DstX_Axis = 1180;
                        warp.DstY_Axis = 875;
                        DebugSystem.Write($"[AC5.Recv17] Teleporting {c.CharName} to Carnie (Map 11094, 1180, 875), saved return: Map {c.CarnieReturnMap?.DstMap} ({c.CarnieReturnMap?.DstX_Axis},{c.CarnieReturnMap?.DstY_Axis})");
                        break;

                    case 2: // Record Point
                        if (c.ReturnSpawnMap != null && c.ReturnSpawnMap.DstMap > 0)
                        {
                            warp.DstMap = c.ReturnSpawnMap.DstMap;
                            warp.DstX_Axis = c.ReturnSpawnMap.DstX_Axis;
                            warp.DstY_Axis = c.ReturnSpawnMap.DstY_Axis;
                            DebugSystem.Write($"[AC5.Recv17] Teleporting {c.CharName} to Record Point (Map {warp.DstMap})");
                        }
                        else
                        {
                            warp.DstMap = 11016;
                            warp.DstX_Axis = 1181;
                            warp.DstY_Axis = 243;
                            DebugSystem.Write($"[AC5.Recv17] Teleporting {c.CharName} to Default Beach");
                        }
                        break;

                    case 1: // Starter Beach
                    default:
                        warp.DstMap = 11016;
                        warp.DstX_Axis = 1181;
                        warp.DstY_Axis = 243;
                        DebugSystem.Write($"[AC5.Recv17] Teleporting {c.CharName} to Starter Beach (Map 11016, 1181, 243)");
                        break;
                }

                c.CurMap.Teleport(TeleportType.CmD, c, 0, warp);
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[AC5.Recv17] Error teleporting: {ex.Message}");
            }
        }

        private void Recv7(Player c, RecievePacket p)
        {
            if (c == null) return;

            // 1. Send AC 3 (Character Entity on Map)
            var ac3 = c.ToAC3Packet();
            if (ac3 != null) c.Send(ac3);

            // 2. Send AC 5:0 (Equipment visuals)
            SendPacket eq = new SendPacket();
            eq.Pack8(5);
            eq.Pack8(0);
            eq.Pack32(c.CharID);
            eq.PackArray(c.Worn_Equips);
            c.Send(eq);

            // 3. S->C AC 5:8 [5, 8, CharID(4B), 0]
            SendPacket s = new SendPacket();
            s.Pack8(5);
            s.Pack8(8);
            s.Pack32(c.CharID);
            s.Pack8(0);
            c.Send(s);

            // 4. Synchronize inventory (AC 23:5), equipment (AC 23:11), and gold (AC 26:4) upon client map ready
            if (c.Inv != null)
            {
                c.Send(new SendPacket(c.Inv.GetAC23_5()));
            }
            if (c._23_11Data != null)
            {
                c.Send(new SendPacket(c._23_11Data));
            }
            c.Send(Tools.FromFormat("bbd", 26, 4, c.Gold));

            // 5. Synchronize character & pet skills (AC 5:11/12, AC 8:2)
            Game.SkillRelated.SkillManager.SendAllSkills(c);

            // 6. Commit immediate character persistence
            c.SaveCharacterData();

            DebugSystem.Write($"[AC5.Recv7] Dispatched AC 3, AC 5:0, AC 5:8, full Inventory/Equip and Skills sync to {c.CharName}");
        }

        private void Recv4(Player c, RecievePacket p)
        {
            // Client state ping - no server echo required
        }
    }
}
