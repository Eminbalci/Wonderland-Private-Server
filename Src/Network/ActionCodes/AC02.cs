using Game;
using Game.Maps;
using System;

namespace Network.ActionCodes {
    public class AC02 : AC {
        public override int ID { get { return 2; } }
        public override void ProcessPkt(Player r, RecievePacket p) {
            switch (p.Unpack8()) {
                // case 1: Recv1(ref r, p); break;
                case 2: Recv2(r, p); break;
            }
        }
        void Recv1(Player p, RecievePacket r) {

        }
        void Recv2(Player p, RecievePacket r) {
            try {
                string str = r.UnpackStringN();
                string[] words = str.Split(' ');
                if (words.Length >= 1) {
                    switch (words[0]) {
                        #region Heal / HP / SP Command
                        case ":heal":
                        case ":hp":
                        case ":full": {
                                try {
                                    if (words.Length >= 3 && int.TryParse(words[1], out int customHp) && int.TryParse(words[2], out int customSp)) {
                                        p.Eqs.CurHP = Math.Min(p.Eqs.FullHP, customHp);
                                        p.Eqs.CurSP = Math.Min(p.Eqs.FullSP, customSp);
                                    } else if (words.Length >= 2 && int.TryParse(words[1], out int customHpOnly)) {
                                        p.Eqs.CurHP = Math.Min(p.Eqs.FullHP, customHpOnly);
                                        p.Eqs.CurSP = p.Eqs.FullSP;
                                    } else {
                                        p.Eqs.CurHP = p.Eqs.FullHP;
                                        p.Eqs.CurSP = p.Eqs.FullSP;
                                    }
                                    p.Eqs.SendStat(25, p.Eqs.CurHP);
                                    p.Eqs.SendStat(26, p.Eqs.CurSP);
                                    p.Send(Tools.FromFormat("bbbs", 23, 57, 0, $"[GM] HP/SP Restored! HP: {p.Eqs.CurHP}/{p.Eqs.FullHP}, SP: {p.Eqs.CurSP}/{p.Eqs.FullSP}"));
                                } catch { }
                            }
                            break;
                        #endregion

                        #region Level Command
                        case ":level":
                        case ":lvl": {
                                try {
                                    if (words.Length >= 2 && byte.TryParse(words[1], out byte newLvl)) {
                                        byte targetLvl = Math.Max((byte)1, Math.Min((byte)200, newLvl));
                                        p.Eqs.SetLevel(targetLvl);
                                        p.Eqs.Send8_1(true);
                                        p.Send(Tools.FromFormat("bbbs", 23, 57, 0, $"[GM] Level updated to {p.Eqs.Level}!"));
                                    }
                                } catch { }
                            }
                            break;
                        #endregion

                        #region Gold Command
                        case ":gold":
                        case ":money": {
                                try {
                                    if (words.Length >= 2 && int.TryParse(words[1], out int amount)) {
                                        p.Eqs.Gold = (uint)Math.Max(0, amount);
                                        p.Eqs.SendStat(39, (int)p.Eqs.Gold);
                                        p.Send(Tools.FromFormat("bbbs", 23, 57, 0, $"[GM] Gold set to {p.Eqs.Gold}!"));
                                    }
                                } catch { }
                            }
                            break;
                        #endregion

                        #region Stat Command
                        case ":stats":
                        case ":stat": {
                                try {
                                    if (words.Length >= 6 &&
                                        ushort.TryParse(words[1], out ushort strVal) &&
                                        ushort.TryParse(words[2], out ushort conVal) &&
                                        ushort.TryParse(words[3], out ushort intVal) &&
                                        ushort.TryParse(words[4], out ushort wisVal) &&
                                        ushort.TryParse(words[5], out ushort agiVal)) {
                                        p.Eqs.Str = strVal;
                                        p.Eqs.Con = conVal;
                                        p.Eqs.Int = intVal;
                                        p.Eqs.Wis = wisVal;
                                        p.Eqs.Agi = agiVal;
                                        p.Eqs.CurHP = p.Eqs.FullHP;
                                        p.Eqs.CurSP = p.Eqs.FullSP;
                                        p.Eqs.Send8_1(true);
                                        p.Send(Tools.FromFormat("bbbs", 23, 57, 0, $"[GM] Stats updated: STR={strVal} CON={conVal} INT={intVal} WIS={wisVal} AGI={agiVal}"));
                                    } else {
                                        p.Send(Tools.FromFormat("bbbs", 23, 57, 0, "[GM] Usage: :stat <str> <con> <int> <wis> <agi>"));
                                    }
                                } catch { }
                            }
                            break;
                        #endregion

                        #region item 
                        case ":item": {
                                try {
                                    ushort itemid = 0;
                                    byte ammt = 1;
                                    if (words.Length >= 2) {
                                        if (words[1].Equals("add", StringComparison.OrdinalIgnoreCase)) {
                                            if (words.Length >= 3) ushort.TryParse(words[2], out itemid);
                                            if (words.Length >= 4) byte.TryParse(words[3], out ammt);
                                        } else {
                                            ushort.TryParse(words[1], out itemid);
                                            if (words.Length >= 3) byte.TryParse(words[2], out ammt);
                                        }
                                        if (itemid > 0) {
                                            ammt = Math.Max((byte)1, ammt);
                                            if (itemid == 34076 && (p.Inv.ContainsItem(34076) || p.Eqs.IsEquipped(34076))) {
                                                p.Send(Tools.FromFormat("bbbs", 23, 57, 0, "[GM] You already have a Radio Set!"));
                                            } else {
                                                p.Inv.AddItem(itemid, ammt);
                                                p.Send(Tools.FromFormat("bbbs", 23, 57, 0, $"[GM] Added Item {itemid} x{ammt} to inventory!"));
                                            }
                                        }
                                    }
                                } catch { }
                            }
                            break;
                        #endregion

                        #region warp
                        case ":warp":
                        case ":goto": {
                                try {
                                    WarpData tmp = new WarpData();
                                    tmp.DstMap = ushort.Parse(words[1]);
                                    tmp.DstX_Axis = ushort.Parse(words[2]);
                                    tmp.DstY_Axis = ushort.Parse(words[3]);
                                    p.CurMap.Teleport(TeleportType.CmD, p, (byte)0, tmp);
                                } catch { }
                            }
                            break;
                        #endregion

                        #region Skill Command
                        case ":skill": {
                                try {
                                    if (words.Length >= 2 && uint.TryParse(words[1], out uint skillId)) {
                                        byte grade = 1;
                                        if (words.Length >= 3) byte.TryParse(words[2], out grade);
                                        Game.SkillRelated.SkillManager.UnlockSkill(p, skillId, grade);
                                        p.Send(Tools.FromFormat("bbbs", 23, 57, 0, $"Skill {skillId} unlocked/updated to Grade {grade}!"));
                                    }
                                } catch { }
                            }
                            break;
                        #endregion

                        #region Help Command
                        case ":help":
                        case ":cmds":
                        case ":cmd": {
                                p.Send(Tools.FromFormat("bbbs", 23, 57, 0, "[GM Commands] :heal [hp] [sp] | :level <1-200> | :gold <amount> | :item <id> [amt] | :stat <str> <con> <int> <wis> <agi> | :skill <id> [grade] | :warp <map> <x> <y>"));
                            }
                            break;
                        #endregion

                        #region Default
                        default: {
                                RCLibrary.Core.Networking.PacketBuilder tmp = new RCLibrary.Core.Networking.PacketBuilder();
                                tmp.Begin();
                                tmp.Add((byte)2);
                                tmp.Add((byte)2);
                                tmp.Add(p.CharID);
                                tmp.Add(str, true);

                                p.CurMap.Broadcast(new SendPacket(tmp.End()), "Ex", p.CharID);
                            }
                            break;
                        #endregion
                    }
                }
            } catch (Exception t) { Console.WriteLine(t.Message, t); }
        }
    }
}
