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

                if (p.IsMuted) {
                    p.SendSystemMessage($"[Server] You are currently muted until {p.MutedUntil.Value:yyyy-MM-dd HH:mm:ss} UTC.");
                    return;
                }

                string[] words = str.Split(' ');
                if (words.Length >= 1) {
                    string commandHeader = words[0].ToLowerInvariant();
                    bool isCommand = commandHeader.StartsWith(":") || commandHeader.StartsWith("/");
                    if (isCommand) {
                        bool isGm = Game.PlayerRelated.GmManager.IsGm(p);
                        bool isPublicCommand = commandHeader == ":help" || commandHeader == "/help" ||
                                               commandHeader == ":cmds" || commandHeader == "/cmds" ||
                                               commandHeader == ":cmd" || commandHeader == "/cmd" ||
                                               commandHeader == ":unride" || commandHeader == "/unride" ||
                                               commandHeader == ":dismount" || commandHeader == "/dismount" ||
                                               commandHeader == ":carnie" || commandHeader == "/carnie";

                        if (!isGm && !isPublicCommand) {
                            p.SendSystemMessage("[Server] You do not have GM privileges to use administrative commands.");
                            return;
                        }
                    }

                    switch (commandHeader) {
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

                                    var activePet = p.PlayerPets?.Values?.FirstOrDefault(pet => pet.IsBattle) 
                                                 ?? (p.ActivePetID > 0 ? p.PlayerPets?.Values?.FirstOrDefault(pet => pet.PetID == p.ActivePetID || pet.Slot == p.ActivePetID) : null)
                                                 ?? p.PlayerPets?.Values?.FirstOrDefault();
                                    if (activePet != null) {
                                        activePet.HP = activePet.MaxHP;
                                        activePet.SP = activePet.MaxSP;
                                        p.SendPetStat(activePet.Slot, 0x0119, (uint)activePet.HP);
                                        p.SendPetStat(activePet.Slot, 0x011A, (uint)activePet.SP);
                                    }

                                    p.SendSystemMessage($"[GM] HP/SP Restored! HP: {p.Eqs.CurHP}/{p.Eqs.FullHP}, SP: {p.Eqs.CurSP}/{p.Eqs.FullSP}");
                                } catch { }
                            }
                            break;
                        #endregion

                        #region Clear Inventory Command
                        case ":clearinv":
                        case "/clearinv":
                        case ":cleaninv":
                        case "/cleaninv": {
                                try {
                                    p.Inv.ClearInventory(true);
                                    DataBase.CharacterDataBase.GlobalInstance?.WritePlayer(p.CharID, p);
                                    p.SendSystemMessage("[GM] Inventory has been cleared!");
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

                        #region Pet EXP Command
                        case ":petexp":
                        case "/petexp": {
                                try {
                                    if (words.Length >= 2 && uint.TryParse(words[1], out uint petExpAmt)) {
                                        var activePet = p.PlayerPets?.Values?.FirstOrDefault(pet => pet.IsBattle) 
                                                     ?? p.PlayerPets?.Values?.FirstOrDefault();
                                        if (activePet != null) {
                                            p.AddPetExp(activePet, petExpAmt, false);
                                            DataBase.CharacterDataBase.GlobalInstance?.WritePlayer(p.CharID, p);
                                            p.SendSystemMessage($"[GM] Added {petExpAmt} EXP to {activePet.PetName} (Lv.{activePet.Level}, Exp: {activePet.Exp})!");
                                        } else {
                                            p.SendSystemMessage("[GM] No active companion or pet found.");
                                        }
                                    } else {
                                        p.SendSystemMessage("[GM] Usage: :petexp <amount>");
                                    }
                                } catch { }
                            }
                            break;
                        #endregion

                        #region Pet Level Command
                        case ":petlvl":
                        case ":petlevel":
                        case "/petlvl":
                        case "/petlevel": {
                                try {
                                    if (words.Length >= 2 && byte.TryParse(words[1], out byte targetLvl) && targetLvl >= 1 && targetLvl <= 199) {
                                        var activePet = p.PlayerPets?.Values?.FirstOrDefault(pet => pet.IsBattle) 
                                                     ?? p.PlayerPets?.Values?.FirstOrDefault();
                                        if (activePet != null) {
                                            activePet.Level = targetLvl;
                                            activePet.Exp = 0;
                                            activePet.MaxHP = 250 + (targetLvl - 1) * 30;
                                            activePet.HP = activePet.MaxHP;
                                            activePet.MaxSP = 100 + (targetLvl - 1) * 15;
                                            activePet.SP = activePet.MaxSP;
                                            p.SendPetStat(activePet.Slot, 0x011D, (uint)activePet.Level);
                                            p.SendPetStat(activePet.Slot, 0x0119, (uint)activePet.HP);
                                            p.SendPetStat(activePet.Slot, 0x011A, (uint)activePet.SP);
                                            p.SendPetStat(activePet.Slot, 0x011E, activePet.Exp);
                                            DataBase.CharacterDataBase.GlobalInstance?.WritePlayer(p.CharID, p);
                                            p.SendSystemMessage($"[GM] {activePet.PetName} level set to Lv.{activePet.Level}!");
                                        } else {
                                            p.SendSystemMessage("[GM] No active companion or pet found.");
                                        }
                                    } else {
                                        p.SendSystemMessage("[GM] Usage: :petlvl <1-199>");
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

                        #region warp / goto / tp
                        case ":warp":
                        case "/warp":
                        case ":goto":
                        case "/goto":
                        case ":tp":
                        case "/tp": {
                                try {
                                    if (words.Length == 2) {
                                        string targetQuery = words[1];
                                        if (ushort.TryParse(targetQuery, out ushort singleMapId)) {
                                            WarpData tmp = new WarpData {
                                                DstMap = singleMapId,
                                                DstX_Axis = 600,
                                                DstY_Axis = 600
                                            };
                                            p.CurMap?.Teleport(TeleportType.CmD, p, 0, tmp);
                                            p.SendSystemMessage($"[GM] Teleported to Map {singleMapId} (600, 600).");
                                        } else {
                                            var target = Game.PlayerRelated.GmManager.FindOnlinePlayer(targetQuery);
                                            if (target != null) {
                                                if (Game.PlayerRelated.GmManager.GotoPlayer(p, target, out string statusMsg)) {
                                                    p.SendSystemMessage($"[GM] {statusMsg}");
                                                } else {
                                                    p.SendSystemMessage($"[GM] Teleport failed: {statusMsg}");
                                                }
                                            } else {
                                                p.SendSystemMessage($"[GM] Player '{targetQuery}' not found or offline.");
                                            }
                                        }
                                    } else if (words.Length >= 4 && ushort.TryParse(words[1], out ushort dstMap) && ushort.TryParse(words[2], out ushort dstX) && ushort.TryParse(words[3], out ushort dstY)) {
                                        WarpData tmp = new WarpData {
                                            DstMap = dstMap,
                                            DstX_Axis = dstX,
                                            DstY_Axis = dstY
                                        };
                                        p.CurMap?.Teleport(TeleportType.CmD, p, 0, tmp);
                                        p.SendSystemMessage($"[GM] Teleported to Map {dstMap} ({dstX}, {dstY}).");
                                    } else {
                                        p.SendSystemMessage("[GM] Usage: :tp <playerName> OR :warp <mapID> [x] [y]");
                                    }
                                } catch (Exception ex) {
                                    p.SendSystemMessage($"[GM] Teleport error: {ex.Message}");
                                }
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
                                p.SendSystemMessage(" Dismounted from vehicle.");
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
                                p.SendSystemMessage(" Teleported to Carnie (Map 11094, 1180, 875)!");
                            }
                            break;

                        case ":pet":
                        case "/pet": {
                                try {
                                    if (words.Length >= 2 && uint.TryParse(words[1], out uint petId)) {
                                        string petName = words.Length >= 3 ? words[2] : (petId == 12178 ? "Robinson" : petId == 10727 ? "Monkey" : petId == 14161 ? "Roca" : $"Pet_{petId}");
                                        Game.QuestRelated.QuestManager.SendCompanionReward(p, petId, petName, setBattle: true);
                                        cGlobal.gCharacterDataBase?.WritePlayer(p.CharID, p);
                                        p.SendSystemMessage($" Companion '{petName}' (ID: {petId}) added and saved!");
                                    } else {
                                        p.SendSystemMessage("[GM] Usage: :pet <petId> [name]. Example: :pet 12178 Robinson, :pet 10727 Monkey, :pet 14161 Roca");
                                    }
                                } catch (Exception ex) {
                                    p.SendSystemMessage($"[GM] Error: {ex.Message}");
                                }
                            }
                            break;

                        #region Broadcast Command
                        case ":b":
                        case "/b":
                        case ":broadcast":
                        case "/broadcast":
                        case ":notice":
                        case "/notice": {
                                try {
                                    if (words.Length >= 2) {
                                        string broadcastMsg = str.Substring(words[0].Length).Trim();
                                        int count = Game.PlayerRelated.GmManager.BroadcastNotice(broadcastMsg, 4);
                                        p.SendSystemMessage($"[GM] Notice broadcasted to {count} online player(s): {broadcastMsg}");
                                    } else {
                                        p.SendSystemMessage("[GM] Usage: :broadcast <message> (or :b <message>)");
                                    }
                                } catch (Exception ex) {
                                    p.SendSystemMessage($"[GM] Error: {ex.Message}");
                                }
                            }
                            break;
                        #endregion

                        #region Kick Player Command
                        case ":kick":
                        case "/kick": {
                                try {
                                    if (words.Length >= 2) {
                                        string targetName = words[1];
                                        var target = Game.PlayerRelated.GmManager.FindOnlinePlayer(targetName);
                                        if (target != null) {
                                            string reason = words.Length >= 3 ? str.Substring(words[0].Length + words[1].Length + 2).Trim() : "Kicked by GM";
                                            Game.PlayerRelated.GmManager.KickPlayer(target, reason);
                                            p.SendSystemMessage($"[GM] Player '{target.CharName}' has been kicked from the server.");
                                        } else {
                                            p.SendSystemMessage($"[GM] Player '{targetName}' not found or offline.");
                                        }
                                    } else {
                                        p.SendSystemMessage("[GM] Usage: :kick <characterName> [reason]");
                                    }
                                } catch (Exception ex) {
                                    p.SendSystemMessage($"[GM] Error: {ex.Message}");
                                }
                            }
                            break;
                        #endregion

                        #region Summon / Bring Player Command
                        case ":summon":
                        case "/summon":
                        case ":bring":
                        case "/bring": {
                                try {
                                    if (words.Length >= 2) {
                                        string targetName = words[1];
                                        var target = Game.PlayerRelated.GmManager.FindOnlinePlayer(targetName);
                                        if (target != null) {
                                            if (Game.PlayerRelated.GmManager.SummonPlayer(p, target, out string statusMsg)) {
                                                p.SendSystemMessage($"[GM] {statusMsg}");
                                            } else {
                                                p.SendSystemMessage($"[GM] Failed to summon: {statusMsg}");
                                            }
                                        } else {
                                            p.SendSystemMessage($"[GM] Player '{targetName}' not found or offline.");
                                        }
                                    } else {
                                        p.SendSystemMessage("[GM] Usage: :summon <characterName> (or :bring <characterName>)");
                                    }
                                } catch (Exception ex) {
                                    p.SendSystemMessage($"[GM] Error: {ex.Message}");
                                }
                            }
                            break;
                        #endregion

                        #region Pet Amity Command
                        case ":amity":
                        case "/amity":
                        case ":petamity":
                        case "/petamity": {
                                try {
                                    byte amityVal = 100;
                                    if (words.Length >= 2 && byte.TryParse(words[1], out byte customAmity)) {
                                        amityVal = Math.Min((byte)100, customAmity);
                                    }
                                    if (Game.PlayerRelated.GmManager.SetPetAmity(p, amityVal, out string statusMsg)) {
                                        p.SendSystemMessage($"[GM] {statusMsg}");
                                    } else {
                                        p.SendSystemMessage($"[GM] {statusMsg}");
                                    }
                                } catch (Exception ex) {
                                    p.SendSystemMessage($"[GM] Error: {ex.Message}");
                                }
                            }
                            break;
                        #endregion

                        #region Pet Rebirth Command
                        case ":rebirth":
                        case "/rebirth":
                        case ":petrebirth":
                        case "/petrebirth": {
                                try {
                                    if (Game.PlayerRelated.GmManager.TriggerPetRebirth(p, out string statusMsg)) {
                                        p.SendSystemMessage($"[GM] {statusMsg}");
                                    } else {
                                        p.SendSystemMessage($"[GM] Failed: {statusMsg}");
                                    }
                                } catch (Exception ex) {
                                    p.SendSystemMessage($"[GM] Error: {ex.Message}");
                                }
                            }
                            break;
                        #endregion

                        #region All Skills Command
                        case ":allskills":
                        case "/allskills":
                        case ":maxskills":
                        case "/maxskills": {
                                try {
                                    byte grade = 1;
                                    if (words.Length >= 2 && byte.TryParse(words[1], out byte customGrade)) {
                                        grade = Math.Max((byte)1, Math.Min((byte)10, customGrade));
                                    }
                                    if (Game.PlayerRelated.GmManager.UnlockAllSkills(p, grade, out string statusMsg)) {
                                        p.SendSystemMessage($"[GM] {statusMsg}");
                                    } else {
                                        p.SendSystemMessage($"[GM] Failed: {statusMsg}");
                                    }
                                } catch (Exception ex) {
                                    p.SendSystemMessage($"[GM] Error: {ex.Message}");
                                }
                            }
                            break;
                        #endregion

                        #region God Mode Command
                        case ":god":
                        case "/god":
                        case ":godmode":
                        case "/godmode": {
                                try {
                                    p.Eqs.Str = 999;
                                    p.Eqs.Con = 999;
                                    p.Eqs.Int = 999;
                                    p.Eqs.Wis = 999;
                                    p.Eqs.Agi = 999;
                                    p.Eqs.CurHP = p.Eqs.FullHP;
                                    p.Eqs.CurSP = p.Eqs.FullSP;
                                    p.Eqs.Send8_1(true);
                                    p.Send_5_3();

                                    var activePet = p.PlayerPets?.Values?.FirstOrDefault(pet => pet.IsBattle) 
                                                 ?? (p.ActivePetID > 0 ? p.PlayerPets?.Values?.FirstOrDefault(pet => pet.PetID == p.ActivePetID || pet.Slot == p.ActivePetID) : null)
                                                 ?? p.PlayerPets?.Values?.FirstOrDefault();
                                    if (activePet != null) {
                                        activePet.Str = 999;
                                        activePet.Con = 999;
                                        activePet.Int = 999;
                                        activePet.Wis = 999;
                                        activePet.Agi = 999;
                                        activePet.MaxHP = 35000;
                                        activePet.HP = activePet.MaxHP;
                                        activePet.MaxSP = 20000;
                                        activePet.SP = activePet.MaxSP;
                                        p.SendPetStat(activePet.Slot, 0x0119, (uint)activePet.HP);
                                        p.SendPetStat(activePet.Slot, 0x011A, (uint)activePet.SP);
                                        p.SendPetStat(activePet.Slot, 0x0114, (uint)activePet.Str);
                                        p.SendPetStat(activePet.Slot, 0x0115, (uint)activePet.Con);
                                        p.SendPetStat(activePet.Slot, 0x0116, (uint)activePet.Int);
                                        p.SendPetStat(activePet.Slot, 0x0117, (uint)activePet.Wis);
                                        p.SendPetStat(activePet.Slot, 0x0118, (uint)activePet.Agi);
                                    }

                                    p.SendSystemMessage("[GM] God Mode Activated: All base stats boosted to 999 with maximum HP/SP!");
                                } catch (Exception ex) {
                                    p.SendSystemMessage($"[GM] Error: {ex.Message}");
                                }
                            }
                            break;
                        #endregion

                        #region Kill All / Battle Win Command
                        case ":killall":
                        case "/killall":
                        case ":killmonsters":
                        case "/killmonsters":
                        case ":winbattle":
                        case "/winbattle": {
                                try {
                                    if (Game.Battle.PvEBattleManager.ForceBattleVictory(p)) {
                                        p.SendSystemMessage("[GM] Combat ended in immediate victory!");
                                    } else {
                                        p.SendSystemMessage("[GM] You are not currently in an active combat battle.");
                                    }
                                } catch (Exception ex) {
                                    p.SendSystemMessage($"[GM] Error: {ex.Message}");
                                }
                            }
                            break;
                        #endregion

                        #region Mute / Unmute Commands
                        case ":mute":
                        case "/mute": {
                                try {
                                    if (words.Length >= 2) {
                                        string targetName = words[1];
                                        int mins = 10;
                                        if (words.Length >= 3 && int.TryParse(words[2], out int customMins)) mins = customMins;
                                        var target = Game.PlayerRelated.GmManager.FindOnlinePlayer(targetName);
                                        if (target != null) {
                                            Game.PlayerRelated.GmManager.MutePlayer(target, mins, out string statusMsg);
                                            p.SendSystemMessage($"[GM] {statusMsg}");
                                        } else {
                                            p.SendSystemMessage($"[GM] Player '{targetName}' not found or offline.");
                                        }
                                    } else {
                                        p.SendSystemMessage("[GM] Usage: :mute <characterName> [minutes]");
                                    }
                                } catch (Exception ex) {
                                    p.SendSystemMessage($"[GM] Error: {ex.Message}");
                                }
                            }
                            break;

                        case ":unmute":
                        case "/unmute": {
                                try {
                                    if (words.Length >= 2) {
                                        string targetName = words[1];
                                        var target = Game.PlayerRelated.GmManager.FindOnlinePlayer(targetName);
                                        if (target != null) {
                                            Game.PlayerRelated.GmManager.UnmutePlayer(target, out string statusMsg);
                                            p.SendSystemMessage($"[GM] {statusMsg}");
                                        } else {
                                            p.SendSystemMessage($"[GM] Player '{targetName}' not found or offline.");
                                        }
                                    } else {
                                        p.SendSystemMessage("[GM] Usage: :unmute <characterName>");
                                    }
                                } catch (Exception ex) {
                                    p.SendSystemMessage($"[GM] Error: {ex.Message}");
                                }
                            }
                            break;
                        #endregion

                        #region Online / Who Command
                        case ":online":
                        case "/online":
                        case ":who":
                        case "/who": {
                                try {
                                    var online = Game.PlayerRelated.GmManager.GetAllOnlinePlayers();
                                    p.SendSystemMessage($"[GM] Online Players ({online.Count}):");
                                    foreach (var pl in online.Take(12)) {
                                        string mapInfo = pl.CurMap != null ? $"Map {pl.CurMap.MapID} ({pl.CurX}, {pl.CurY})" : "Unknown";
                                        p.SendSystemMessage($" - {pl.CharName} (Lv.{pl.Eqs?.Level ?? 1}) @ {mapInfo}");
                                    }
                                    if (online.Count > 12) {
                                        p.SendSystemMessage($" ... and {online.Count - 12} more player(s).");
                                    }
                                } catch (Exception ex) {
                                    p.SendSystemMessage($"[GM] Error: {ex.Message}");
                                }
                            }
                            break;
                        #endregion

                        #region Tent Item Command
                        case ":tent":
                        case "/tent": {
                                try {
                                    ushort tentItemId = 34001;
                                    if (!p.Inv.ContainsItem(tentItemId)) {
                                        p.Inv.AddItem(tentItemId, 1);
                                        p.SendSystemMessage("[GM] Added Tent (Item #34001) to inventory!");
                                    } else {
                                        p.SendSystemMessage("[GM] You already have a Tent in your inventory.");
                                    }
                                } catch (Exception ex) {
                                    p.SendSystemMessage($"[GM] Error: {ex.Message}");
                                }
                            }
                            break;
                        #endregion

                        #region Dynamic Reload Command
                        case ":reload":
                        case "/reload": {
                                try {
                                    string targetType = words.Length >= 2 ? words[1].ToLowerInvariant() : "all";
                                    if (targetType == "quests" || targetType == "quest") {
                                        Game.QuestRelated.QuestManager.InitializeQuests();
                                        p.SendSystemMessage($"[GM] Quests reloaded! Total registered: {Game.QuestRelated.QuestManager.MasterCount}");
                                    } else if (targetType == "mall" || targetType == "itemmall" || targetType == "items") {
                                        Game.PlayerRelated.ItemMallManager.Initialize();
                                        p.SendSystemMessage($"[GM] Item Mall reloaded! Total catalog items: {Game.PlayerRelated.ItemMallManager.GetCatalog().Count}");
                                    } else if (targetType == "drops" || targetType == "drop") {
                                        Game.Battle.MonsterDropManager.Initialize();
                                        p.SendSystemMessage("[GM] Monster loot drop tables reloaded!");
                                    } else if (targetType == "gms" || targetType == "gm") {
                                        Game.PlayerRelated.GmManager.LoadFromDatabase();
                                        p.SendSystemMessage($"[GM] GM list reloaded! Total GMs: {Game.PlayerRelated.GmManager.GetGmList().Count}");
                                    } else {
                                        string summary = Game.PlayerRelated.GmManager.ReloadAll();
                                        p.SendSystemMessage($"[GM] Server tables reloaded: {summary}");
                                    }
                                } catch (Exception ex) {
                                    p.SendSystemMessage($"[GM] Error during reload: {ex.Message}");
                                }
                            }
                            break;
                        #endregion

                        #region Invisibility / Ghost Mode Command
                        case ":invis":
                        case "/invis":
                        case ":invisible":
                        case "/invisible":
                        case ":ghost":
                        case "/ghost":
                        case ":hide":
                        case "/hide":
                        case ":unhide":
                        case "/unhide": {
                                try {
                                    if (Game.PlayerRelated.GmManager.ToggleInvisibility(p, out string statusMsg)) {
                                        p.SendSystemMessage($"[GM] {statusMsg}");
                                    }
                                } catch (Exception ex) {
                                    p.SendSystemMessage($"[GM] Error: {ex.Message}");
                                }
                            }
                            break;
                        #endregion

                        #region Restat / Reset Stats Command
                        case ":restat":
                        case "/restat":
                        case ":resetstats":
                        case "/resetstats": {
                                try {
                                    var target = p;
                                    if (words.Length >= 2) {
                                        var found = Game.PlayerRelated.GmManager.FindOnlinePlayer(words[1]);
                                        if (found != null) target = found;
                                    }
                                    if (Game.PlayerRelated.GmManager.RestatPlayer(target, out string statusMsg)) {
                                        p.SendSystemMessage($"[GM] {statusMsg}");
                                    }
                                } catch (Exception ex) {
                                    p.SendSystemMessage($"[GM] Error: {ex.Message}");
                                }
                            }
                            break;
                        #endregion

                        #region Clear Skills Command
                        case ":clearskills":
                        case "/clearskills":
                        case ":resetskills":
                        case "/resetskills": {
                                try {
                                    var target = p;
                                    if (words.Length >= 2) {
                                        var found = Game.PlayerRelated.GmManager.FindOnlinePlayer(words[1]);
                                        if (found != null) target = found;
                                    }
                                    if (Game.PlayerRelated.GmManager.ClearSkills(target, out string statusMsg)) {
                                        p.SendSystemMessage($"[GM] {statusMsg}");
                                    }
                                } catch (Exception ex) {
                                    p.SendSystemMessage($"[GM] Error: {ex.Message}");
                                }
                            }
                            break;
                        #endregion

                        #region Repair Gear Command
                        case ":repair":
                        case "/repair":
                        case ":fixall":
                        case "/fixall": {
                                try {
                                    var target = p;
                                    if (words.Length >= 2) {
                                        var found = Game.PlayerRelated.GmManager.FindOnlinePlayer(words[1]);
                                        if (found != null) target = found;
                                    }
                                    if (Game.PlayerRelated.GmManager.RepairAllItems(target, out string statusMsg)) {
                                        p.SendSystemMessage($"[GM] {statusMsg}");
                                    }
                                } catch (Exception ex) {
                                    p.SendSystemMessage($"[GM] Error: {ex.Message}");
                                }
                            }
                            break;
                        #endregion

                        #region IM Points Command
                        case ":im":
                        case "/im":
                        case ":points_im":
                        case "/points_im":
                        case ":mallpoints":
                        case "/mallpoints": {
                                try {
                                    if (words.Length >= 2 && int.TryParse(words[1], out int pointsVal)) {
                                        var target = p;
                                        if (words.Length >= 3) {
                                            var found = Game.PlayerRelated.GmManager.FindOnlinePlayer(words[2]);
                                            if (found != null) target = found;
                                        }
                                        if (Game.PlayerRelated.GmManager.AddMallPoints(target, pointsVal, out string statusMsg)) {
                                            p.SendSystemMessage($"[GM] {statusMsg}");
                                        }
                                    } else {
                                        p.SendSystemMessage("[GM] Usage: :im <points> [characterName]");
                                    }
                                } catch (Exception ex) {
                                    p.SendSystemMessage($"[GM] Error: {ex.Message}");
                                }
                            }
                            break;
                        #endregion

                        #region Town Warp Command
                        case ":town":
                        case "/town": {
                                try {
                                    if (words.Length >= 2) {
                                        string townKey = words[1].ToLowerInvariant();
                                        if (Game.PlayerRelated.GmManager.TownDirectory.TryGetValue(townKey, out var entry)) {
                                            Game.PlayerRelated.GmManager.WarpPlayer(p, entry.MapID, entry.X, entry.Y, out string statusMsg);
                                            p.SendSystemMessage($"[GM] Warped to {entry.Name} ({statusMsg})");
                                        } else {
                                            string available = string.Join(", ", Game.PlayerRelated.GmManager.TownDirectory.Keys);
                                            p.SendSystemMessage($"[GM] Town '{townKey}' not recognized. Available: {available}");
                                        }
                                    } else {
                                        string available = string.Join(", ", Game.PlayerRelated.GmManager.TownDirectory.Keys);
                                        p.SendSystemMessage($"[GM] Usage: :town <townName>. Available: {available}");
                                    }
                                } catch (Exception ex) {
                                    p.SendSystemMessage($"[GM] Error: {ex.Message}");
                                }
                            }
                            break;
                        #endregion

                        #region Jail & Unjail Commands
                        case ":jail":
                        case "/jail": {
                                try {
                                    if (words.Length >= 2) {
                                        string targetName = words[1];
                                        int mins = 10;
                                        if (words.Length >= 3 && int.TryParse(words[2], out int customMins)) mins = customMins;
                                        var target = Game.PlayerRelated.GmManager.FindOnlinePlayer(targetName);
                                        if (target != null) {
                                            Game.PlayerRelated.GmManager.JailPlayer(target, mins, out string statusMsg);
                                            p.SendSystemMessage($"[GM] {statusMsg}");
                                        } else {
                                            p.SendSystemMessage($"[GM] Player '{targetName}' not found or offline.");
                                        }
                                    } else {
                                        p.SendSystemMessage("[GM] Usage: :jail <characterName> [minutes]");
                                    }
                                } catch (Exception ex) {
                                    p.SendSystemMessage($"[GM] Error: {ex.Message}");
                                }
                            }
                            break;

                        case ":unjail":
                        case "/unjail": {
                                try {
                                    if (words.Length >= 2) {
                                        string targetName = words[1];
                                        var target = Game.PlayerRelated.GmManager.FindOnlinePlayer(targetName);
                                        if (target != null) {
                                            Game.PlayerRelated.GmManager.UnjailPlayer(target, out string statusMsg);
                                            p.SendSystemMessage($"[GM] {statusMsg}");
                                        } else {
                                            p.SendSystemMessage($"[GM] Player '{targetName}' not found or offline.");
                                        }
                                    } else {
                                        p.SendSystemMessage("[GM] Usage: :unjail <characterName>");
                                    }
                                } catch (Exception ex) {
                                    p.SendSystemMessage($"[GM] Error: {ex.Message}");
                                }
                            }
                            break;
                        #endregion

                        #region Summon All & Kick All Commands
                        case ":summonall":
                        case "/summonall": {
                                try {
                                    int count = Game.PlayerRelated.GmManager.SummonAllPlayers(p, out string statusMsg);
                                    p.SendSystemMessage($"[GM] {statusMsg}");
                                } catch (Exception ex) {
                                    p.SendSystemMessage($"[GM] Error: {ex.Message}");
                                }
                            }
                            break;

                        case ":kickall":
                        case "/kickall": {
                                try {
                                    string reason = words.Length >= 2 ? str.Substring(words[0].Length).Trim() : "Server Maintenance";
                                    int count = Game.PlayerRelated.GmManager.KickAllPlayers(reason, out string statusMsg);
                                    p.SendSystemMessage($"[GM] {statusMsg}");
                                } catch (Exception ex) {
                                    p.SendSystemMessage($"[GM] Error: {ex.Message}");
                                }
                            }
                            break;
                        #endregion

                        #region Test Battle Command
                        case ":battle":
                        case "/battle":
                        case ":fight":
                        case "/fight": {
                                try {
                                    if (words.Length >= 2 && uint.TryParse(words[1], out uint npcId)) {
                                        p.SendSystemMessage($"[GM] Initiating test combat encounter against Mob TID {npcId}...");
                                        Game.Battle.PvEBattleManager.StartBattle(p, 1, npcId);
                                    } else {
                                        p.SendSystemMessage("[GM] Usage: :battle <npcId>");
                                    }
                                } catch (Exception ex) {
                                    p.SendSystemMessage($"[GM] Error: {ex.Message}");
                                }
                            }
                            break;
                        #endregion

                        #region Character Info Command
                        case ":info":
                        case "/info":
                        case ":whois":
                        case "/whois": {
                                try {
                                    var target = p;
                                    if (words.Length >= 2) {
                                        var found = Game.PlayerRelated.GmManager.FindOnlinePlayer(words[1]);
                                        if (found != null) target = found;
                                        else {
                                            p.SendSystemMessage($"[GM] Player '{words[1]}' not found or offline.");
                                            break;
                                        }
                                    }
                                    if (Game.PlayerRelated.GmManager.GetPlayerInfo(target, out string infoReport)) {
                                        foreach (var line in infoReport.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)) {
                                            p.SendSystemMessage(line);
                                        }
                                    }
                                } catch (Exception ex) {
                                    p.SendSystemMessage($"[GM] Error: {ex.Message}");
                                }
                            }
                            break;
                        #endregion

                        #region Drop Rate Multiplier Command
                        case ":droprate":
                        case "/droprate": {
                                try {
                                    if (words.Length >= 2 && double.TryParse(words[1], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double newRate)) {
                                        Game.Battle.MonsterDropManager.DropRateMultiplier = Math.Max(0.1, Math.Min(100.0, newRate));
                                        p.SendSystemMessage($"[GM] Global drop rate multiplier set to {Game.Battle.MonsterDropManager.DropRateMultiplier:F1}x!");
                                    } else {
                                        p.SendSystemMessage($"[GM] Current drop rate multiplier: {Game.Battle.MonsterDropManager.DropRateMultiplier:F1}x. Usage: :droprate <multiplier>");
                                    }
                                } catch (Exception ex) {
                                    p.SendSystemMessage($"[GM] Error: {ex.Message}");
                                }
                            }
                            break;
                        #endregion

                        #region Shutdown Command
                        case ":shutdown":
                        case "/shutdown": {
                                try {
                                    int seconds = 10;
                                    if (words.Length >= 2 && int.TryParse(words[1], out int customSec)) seconds = Math.Max(1, Math.Min(300, customSec));
                                    Game.PlayerRelated.GmManager.BroadcastNotice($"Server is shutting down in {seconds} seconds for maintenance!", 4);
                                    System.Threading.Tasks.Task.Run(async () => {
                                        for (int i = seconds; i > 0; i--) {
                                            if (i <= 5 || i % 10 == 0) {
                                                Game.PlayerRelated.GmManager.BroadcastNotice($"[ALERT] Server shutdown in {i} second(s)!", 4);
                                            }
                                            await System.Threading.Tasks.Task.Delay(1000);
                                        }
                                        Game.PlayerRelated.GmManager.KickAllPlayers("Scheduled Maintenance Shutdown", out _);
                                        Environment.Exit(0);
                                    });
                                    p.SendSystemMessage($"[GM] Shutdown countdown initiated ({seconds}s).");
                                } catch (Exception ex) {
                                    p.SendSystemMessage($"[GM] Error: {ex.Message}");
                                }
                            }
                            break;
                        #endregion

                        #region Help Command
                        case ":help":
                        case "/help":
                        case ":cmds":
                        case "/cmds":
                        case ":cmd":
                        case "/cmd": {
                                if (Game.PlayerRelated.GmManager.IsGm(p)) {
                                    p.SendSystemMessage("[Admin/Server] :broadcast <msg> | :reload [all|quests|mall|drops] | :droprate <rate> | :online | :shutdown [s]");
                                    p.SendSystemMessage("[Admin/Player] :tp <char> | :summon <char> | :kick <char> | :kickall | :jail <char> [m] | :unjail <char> | :mute <char> [m] | :unmute <char> | :info <char>");
                                    p.SendSystemMessage("[Player Cheat] :heal | :god | :invis | :level <1-200> | :points <n> | :restat | :gold <n> | :im <n> | :exp <n> | :allskills [gr] | :clearskills | :repair | :stat <s c i w a> | :item <id> [cnt] | :buy <id> [cnt] | :tent | :clearinv");
                                    p.SendSystemMessage("[Companion] :pet <id> [name] | :petlvl <lvl> | :petexp <n> | :amity [1-100] | :rebirth");
                                    p.SendSystemMessage("[Combat & Move] :winbattle | :battle <npcId> | :town <name> | :warp <map> [x] [y] | :summonall | :carnie | :unride");
                                } else {
                                    p.SendSystemMessage("[Player Commands] :unride (dismount vehicle/pet) | :carnie (travel to Carnie) | :help (show commands)");
                                }
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
