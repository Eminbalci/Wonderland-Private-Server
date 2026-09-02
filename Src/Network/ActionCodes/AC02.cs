using Game;
using Game.Maps;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Network.ActionCodes {
    public class AC02 : AC {
        public override int ID { get { return 2; } }
        public override void ProcessPkt(Player r, RecievePacket p) {
            switch (p.Unpack8()) {
                case 1: Recv1(r, p); break;
                case 2: Recv2(r, p); break;
            }
        }
        void Recv1(Player p, RecievePacket r) {
            try {
                string str = r.UnpackStringN();
                DebugSystem.Write($"[Chat.Global] {p.CharName}: {str}");
            } catch { }
        }
        void Recv2(Player p, RecievePacket r) {
            try {
                string str = r.UnpackStringN();
                DebugSystem.Write($"[Chat] {p.CharName}: {str}");
                string[] words = str.Split(' ');
                if (words.Length >= 1) {
                    switch (words[0]) {
                        #region Heal / HP / SP Command
                        case ":heal":
                        case "/heal":
                        case ":hp":
                        case "/hp":
                        case ":full":
                        case "/full": {
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
                                    p.Eqs.Send8_1(false);
                                    p.Send_5_3();
                                    p.SendSystemMessage($"[GM] HP/SP Restored! HP: {p.Eqs.CurHP}/{p.Eqs.FullHP}, SP: {p.Eqs.CurSP}/{p.Eqs.FullSP}");
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
                                        DataBase.CharacterDataBase.GlobalInstance?.WritePlayer(p.CharID, p);
                                        p.SendSystemMessage($"[GM] Level updated to {p.Eqs.Level}! Available Stat Points: {p.Eqs.SkillPoints}");
                                    }
                                } catch { }
                            }
                            break;
                        #endregion

                        #region Points / SP Command
                        case ":points":
                        case ":sp":
                        case ":statpoint":
                        case ":statpoints": {
                                try {
                                    if (words.Length >= 2 && ushort.TryParse(words[1], out ushort addPts)) {
                                        p.Eqs.SkillPoints += addPts;
                                        p.Eqs.Send8_1(true);
                                        DataBase.CharacterDataBase.GlobalInstance?.WritePlayer(p.CharID, p);
                                        p.SendSystemMessage($"[GM] Added +{addPts} Stat Points! Total Available: {p.Eqs.SkillPoints}");
                                    } else {
                                        p.SendSystemMessage($"[GM] Current Available Stat Points: {p.Eqs.SkillPoints}. Usage: :points <amount>");
                                    }
                                } catch { }
                            }
                            break;
                        #endregion

                        #region Gold Command
                        case ":gold":
                        case "/gold":
                        case ":money":
                        case "/money": {
                                try {
                                    if (words.Length >= 2 && int.TryParse(words[1], out int amount)) {
                                        p.SetGold(Math.Max(0, amount));
                                        p.Send(Tools.FromFormat("bbd", 26, 4, (uint)p.Gold));
                                        DataBase.CharacterDataBase.GlobalInstance?.WritePlayer(p.CharID, p);
                                        p.SendSystemMessage($"[GM] Gold set to {p.Gold}!");
                                    }
                                } catch { }
                            }
                            break;
                        #endregion

                        #region EXP Command
                        case ":exp":
                        case "/exp": {
                                try {
                                    if (words.Length >= 2 && long.TryParse(words[1], out long expAmt)) {
                                        p.Eqs.TotalExp = Math.Max(0, expAmt);
                                        p.Eqs.Send8_1(false);
                                        DataBase.CharacterDataBase.GlobalInstance?.WritePlayer(p.CharID, p);
                                        p.SendSystemMessage($"[GM] Total EXP set to {p.Eqs.TotalExp}!");
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
                                        p.SendSystemMessage($"[GM] Stats updated: STR={strVal} CON={conVal} INT={intVal} WIS={wisVal} AGI={agiVal}");
                                    } else {
                                        p.SendSystemMessage("[GM] Usage: :stat <str> <con> <int> <wis> <agi>");
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
                                                p.SendSystemMessage("[GM] You already have a Radio Set!");
                                            } else {
                                                p.Inv.AddItem(itemid, ammt);
                                                p.SendSystemMessage($"[GM] Added Item {itemid} x{ammt} to inventory!");
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
                                        p.SendSystemMessage($"Skill {skillId} unlocked/updated to Grade {grade}!");
                                    }
                                } catch { }
                            }
                            break;
                        #endregion

                        #region Item Mall Buy Command
                        case ":buy":
                        case "/buy": {
                                try {
                                    if (words.Length >= 2) {
                                        string query = string.Join(" ", words.Skip(1)).Trim();
                                        byte quantity = 1;
                                        var lastWord = words[words.Length - 1];
                                        if (words.Length >= 3 && byte.TryParse(lastWord, out byte qVal)) {
                                            quantity = qVal;
                                            query = string.Join(" ", words.Skip(1).Take(words.Length - 2)).Trim();
                                        }

                                        var catalog = Game.PlayerRelated.ItemMallManager.GetCatalog();
                                        Game.PlayerRelated.MallItemEntry match = null;

                                        if (ushort.TryParse(query, out ushort idQuery)) {
                                            match = catalog.FirstOrDefault(i => i.ItemID == idQuery);
                                        }
                                        if (match == null) {
                                            match = catalog.FirstOrDefault(i => i.ItemName.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0);
                                        }

                                        if (match != null) {
                                            bool success = Game.PlayerRelated.ItemMallManager.PurchaseItem(p, match.ItemID, quantity);
                                            if (success) {
                                                p.SendSystemMessage($"[Item Mall] Successfully purchased {quantity}x {match.ItemName} for {match.PointCost * quantity} Points!");
                                            }
                                        } else if (ushort.TryParse(query, out ushort anyItemId) && anyItemId > 0) {
                                            // Dynamic purchase directly from ItemDat
                                            var itemInfo = cGlobal.ItemDatManager?.GetItemByID(anyItemId);
                                            string name = itemInfo != null ? System.Text.Encoding.ASCII.GetString(itemInfo.ItemName).TrimEnd('\0') : $"Item #{anyItemId}";
                                            p.Inv.AddItem(anyItemId, quantity);
                                            p.SendSystemMessage($"[Item Mall] Added {quantity}x {name} (#{anyItemId}) to inventory!");
                                        } else {
                                            p.SendSystemMessage($"[Item Mall] Item '{query}' not found. Example: :buy star, :buy jalor, :buy robot, :buy 30025, :item <id> [count]");
                                        }
                                    } else {
                                        p.SendSystemMessage("[Item Mall] Usage: :buy <item name or ID> [amount]. Example: :buy star 1, :buy jalor 1");
                                    }
                                } catch (Exception ex) {
                                    p.SendSystemMessage($"[Item Mall] Error: {ex.Message}");
                                }
                            }
                            break;
                        #endregion



                        case ":unride":
                        case "/unride":
                        case ":dismount":
                        case "/dismount": {
                                p.RideVehicle("");
                                if (p.CurMap != null && p.CurMap.MapID == 10036) {
                                    var shoreWarp = new WarpData() { DstMap = 10036, DstX_Axis = 1038, DstY_Axis = 2235 };
                                    p.CurMap.Teleport(TeleportType.CmD, p, 0, shoreWarp);
                                }
                                p.SendSystemMessage("🚶 Dismounted from vehicle.");
                            }
                            break;

                        case ":carnie":
                        case "/carnie": {
                                if (p.CurMap != null && p.CurMap.MapID != 11094) {
                                    p.CarnieReturnMap = new WarpData() {
                                        DstMap = (ushort)p.CurMap.MapID,
                                        DstX_Axis = (ushort)p.CurX,
                                        DstY_Axis = (ushort)p.CurY
                                    };
                                }
                                var carnieWarp = new WarpData() { DstMap = 11094, DstX_Axis = 1180, DstY_Axis = 875 };
                                p.CurMap?.Teleport(TeleportType.CmD, p, 0, carnieWarp);
                                p.SendSystemMessage("🎪 Teleported to Carnie (Map 11094, 1180, 875)!");
                            }
                            break;

                        case ":pet":
                        case "/pet": {
                                try {
                                    if (words.Length >= 2 && uint.TryParse(words[1], out uint petId)) {
                                        string petName = words.Length >= 3 ? words[2] : (petId == 12178 ? "Robinson" : petId == 10727 ? "Monkey" : petId == 14161 ? "Roca" : $"Pet_{petId}");
                                        Game.QuestRelated.QuestManager.SendCompanionReward(p, petId, petName, setBattle: true);
                                        cGlobal.gCharacterDataBase?.WritePlayer(p.CharID, p);
                                        p.SendSystemMessage($"🐾 Companion '{petName}' (ID: {petId}) added and saved!");
                                    } else {
                                        p.SendSystemMessage("[GM] Usage: :pet <petId> [name]. Example: :pet 12178 Robinson, :pet 10727 Monkey, :pet 14161 Roca");
                                    }
                                } catch (Exception ex) {
                                    p.SendSystemMessage($"[GM] Error: {ex.Message}");
                                }
                            }
                            break;

                        #region Help Command
                        case ":help":
                        case ":cmds":
                        case ":cmd": {
                                p.SendSystemMessage("[Player Commands] :heal | :level | :gold | :item | :pet | :skill | :warp | :unride | :carnie");
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
