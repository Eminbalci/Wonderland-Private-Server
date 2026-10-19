using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Game.DataFiles;
using Game.QuestRelated;
using Network;
using RCLibrary.Core;

namespace Game.Maps
{
    /// <summary>
    /// Automated Event Script Interpreter for Wonderland Online.
    /// Directly parses and executes native event opcodes from eve.Emg, eliminating manual quest hardcoding.
    /// </summary>
    public static partial class EveEventInterpreter
    {
        public static bool TryExecute(Player player, GameMap map, ushort clickId)
        {
            if (player == null || map == null || clickId == 0) return false;
            if (player.NativeEventActive) return true;

            try
            {
                var mapData = DataBase.GameDataBase.GlobalInstance?.EveDat?.GetMapData((ushort)map.MapID);
                if (mapData == null) return false;

                // The battle shows actor4 only transiently. At the saved offer
                // checkpoint it must resume event2, not the completed rejoin event8.
                if (map.MapID == 11040 && clickId == 4 && player.Quests != null &&
                    player.Quests.TryGetValue(13102, out var cliveOffer) &&
                    cliveOffer.State == QuestState.InProgress && cliveOffer.Step == 4 &&
                    !(player.Quests.TryGetValue(13103, out var cliveDone) &&
                      cliveDone.State == QuestState.InProgress && cliveDone.Step > 0))
                {
                    var offerEvent = mapData.Events.FirstOrDefault(eventData => eventData.clickID == 2);
                    var offer = FindBranch(player, map, offerEvent);
                    if (offer != null) { StartSession(player, map, clickId, offerEvent, offer); return true; }
                }

                // After the paired spring CG, the shore actors offer the authored
                // two-companion return event, also used by the nearby exit region.
                if (map.MapID == 60002 && (clickId == 43 || clickId == 44) &&
                    player.Quests != null && player.Quests.TryGetValue(50042, out var spring) &&
                    spring.State == QuestState.InProgress && spring.Step > 1 &&
                    player.Quests.TryGetValue(50047, out var springCg) &&
                    springCg.State == QuestState.InProgress && springCg.Step > 0)
                {
                    var returnEvent = mapData.Events?.FirstOrDefault(e => e.clickID == 51);
                    var returnBranch = FindBranch(player, map, returnEvent);
                    if (returnBranch != null) StartSession(player, map, clickId, returnEvent, returnBranch);
                    else
                    {
                        player.SendHeadBanner("Please leave two free companion slots for Roca and Xaolan.");
                        player.Send(Tools.FromFormat("bb", 20, 8));
                    }
                    return true;
                }

                var npcEntry = mapData.Npclist?.FirstOrDefault(n => n.clickId == clickId);
                var mapNpc = map.NpcList?.FirstOrDefault(n => n.CickID == clickId) as QuestNpc;

                EventsinMapEntries eventEntry = null;
                EventSubEntry selectedSub = null;

                List<EventsinMapEntries> candidates = new List<EventsinMapEntries>();
                if (mapData.Events != null)
                {
                    // 1. Explicit linked events assigned to this NPC in Eve.emg take absolute priority
                    if (npcEntry?.Events != null && npcEntry.Events.Count > 0)
                    {
                        foreach (var evId in npcEntry.Events)
                        {
                            var linked = mapData.Events.FirstOrDefault(e => e.clickID == evId);
                            if (linked != null && linked.SubEntry != null && linked.SubEntry.Count > 0 && !candidates.Contains(linked))
                            {
                                candidates.Add(linked);
                            }
                        }
                    }

                    // 2. Fallback to direct event matching clickID ONLY if no linked events exist for this NPC
                    if (candidates.Count == 0)
                    {
                        var direct = mapData.Events.FirstOrDefault(e => e.clickID == clickId);
                        if (direct != null && direct.SubEntry != null && direct.SubEntry.Count > 0)
                        {
                            candidates.Add(direct);
                        }
                    }

                    // Robinson rescue fallback: if chest dialogue finished (Quest 12047 active)
                    // but recruitment was not finalized, interacting with Robinson directly resumes Event 19.
                    if ((map.MapID == 10035 || map.MapID == 10039) && clickId == 1 &&
                        player.Quests != null && player.Quests.TryGetValue(12047, out var robinsonQuest) &&
                        robinsonQuest.State == QuestState.InProgress &&
                        !player.HasRecruitedCompanion(12178))
                    {
                        var ev19 = mapData.Events.FirstOrDefault(e => e.clickID == 19);
                        if (ev19 != null && !candidates.Contains(ev19))
                        {
                            candidates.Insert(0, ev19);
                        }
                    }
                }

                // No native event: allow QuestNpc to handle storage, shops and other services.
                if (candidates.Count == 0) return false;
                if (candidates.All(e => IsNativeEventDisabled(map.MapID, e.clickID)))
                    return RejectDisabledNativeEvent(player, map, candidates[0]);

                // Older saves can retain the completed age quiz while Dinner Incident
                // has reverted to step 1. Restore the delivery stage; native EVE then
                // reissues a missing pepper, with its normal item and bag-space checks.
                if (map.MapID == 12000 && npcEntry?.npcId == 14140 && player.Quests != null &&
                    player.Quests.TryGetValue(12018, out var dinner) && dinner.State == QuestState.InProgress && dinner.Step == 1 &&
                    player.Quests.TryGetValue(12043, out var ageQuiz) && ageQuiz.State == QuestState.InProgress && ageQuiz.Step == 1 &&
                    (!player.Quests.TryGetValue(12019, out var dinnerDone) || dinnerDone.State != QuestState.InProgress || dinnerDone.Step == 0))
                {
                    dinner.Step = 2;
                    QuestManager.SavePlayerQuest(player, 12018);
                    QuestManager.SendQuestUpdate(player, 12018, dinner.State, 2);
                }

                // Scan all candidate events for this NPC to find the first event with an eligible branch
                foreach (var candidate in candidates)
                {
                    var sub = SelectMatchingBranch(player, map, clickId, candidate);
                    if (sub != null)
                    {
                        eventEntry = candidate;
                        selectedSub = sub;
                        break;
                    }
                }

                // If no candidate event had an eligible branch, gracefully unfreeze player and exit
                if (eventEntry == null || selectedSub == null)
                {
                    player.Send(Tools.FromFormat("bb", 20, 8));
                    return true;
                }

                if (RejectDisabledNativeEvent(player, map, eventEntry)) return true;

                var qNpc = map.NpcList?.FirstOrDefault(n => n.CickID == clickId) as Game.Maps.QuestNpc;
                ushort npcTid = qNpc != null ? (ushort)qNpc.TemplateID : (ushort)(npcEntry?.npcId ?? 0);
                string nName = qNpc != null ? qNpc.Name : (npcTid > 0 ? Game.DataFiles.SceneDataManager.GetNpcName(npcTid) : (npcEntry?.Name ?? $"NPC_{clickId}"));
                string mMapName = Game.DataFiles.SceneDataManager.GetMapName((ushort)map.MapID);

                DebugSystem.Write($"[EveEventInterpreter] Executing native Event for Map #{map.MapID} ({mMapName}), NPC #{clickId} '{nName}' (TID: {npcTid}) -> Event #{eventEntry.clickID} ('{eventEntry.Name.Trim()}'). SubEntries: {eventEntry.SubEntry.Count}");

                // Special handling for S.Monkey (TID 17162 / Map 11016 Event 1):
                // Enforces authentic 16-step dialogue sequence (TalkIDs 20038..20053), pet recruitment, and strict warp isolation
                if (npcTid == 17162 || (map.MapID == 11016 && clickId == 1))
                {
                    bool hasMonkey = (player.PlayerPets != null && player.PlayerPets.Values.Any(pet => pet != null && pet.PetID == 17162)) ||
                                     (player.Quests != null && player.Quests.TryGetValue(12002, out var mq) && mq.State == QuestState.Completed);

                    if (hasMonkey)
                    {
                        // Already recruited: short squeak dialogue (TalkID 20042)
                        SendPacket squeakPkt = BuildDialoguePacket((byte)clickId, 20042, 1, 3);
                        player.OnInteractionComplete = () =>
                        {
                            player.Send(Tools.FromFormat("bbb", 6, 2, 0));
                            player.Send(Tools.FromFormat("bb", 20, 8));
                            player.Send(Tools.FromFormat("bb", 5, 4));
                        };
                        player.Send(Tools.FromFormat("bbb", 6, 2, 1));
                        player.Send(squeakPkt);
                        DebugSystem.Write($"[EveEventInterpreter] Sent S.Monkey already-recruited dialogue (TalkID 20042) to {player.CharName}");
                        return true;
                    }

                    bool isTeamFull = player.PlayerPets != null && player.PlayerPets.Count >= 4;

                    player.QueueData.Clear();

                    // 1. Dispatch authentic in-engine Cinematic Cutscene frame AC 20:1 Type 5 Mode 1 (Cutscene ID 12000)
                    SendPacket animPkt = new SendPacket();
                    animPkt.PackArray(new byte[] { 20, 1, 0, 0, 0, 1, 5, 0 });
                    animPkt.Pack16(0);
                    animPkt.Pack8(1); // Mode 1: Play in-engine cutscene and send AC 20:6 when finished
                    animPkt.Pack32(12000); // Cutscene ID 12000 (12000.sty)
                    animPkt.Pack16(0);
                    animPkt.Pack8(1); // Branch 1
                    player.Send(animPkt);
                    player.Send(Tools.FromFormat("bbw", 186, 12, 12000));

                    // 2. Freeze player posture and immobilize during cutscene playback
                    player.Send(Tools.FromFormat("bbb", 6, 2, 1));
                    player.Send(Tools.FromFormat("bbbdb", 5, 30, 0, player.CharID, (byte)1));

                    DebugSystem.Write($"[EveEventInterpreter] [S.Monkey Cutscene] Dispatched authentic CPR resuscitation Cutscene 12000 (12000.sty) to {player.CharName} (isTeamFull={isTeamFull})");

                    // 3. Await client AC 20:6 completion ACK before executing post-cutscene dialogue and recruitment
                    player.OnInteractionComplete = () =>
                    {
                        DebugSystem.Write($"[EveEventInterpreter] [S.Monkey Cutscene] Client completed Cutscene 12000. Starting dialogue for {player.CharName} (isTeamFull={isTeamFull})");

                        if (isTeamFull)
                        {
                            SendPacket step1 = BuildDialoguePacket((byte)clickId, 31146, 1, 7);
                            SendPacket step2 = BuildDialoguePacket((byte)clickId, 20048, 2, 3);
                            player.QueueData.Clear();
                            player.QueueData.Enqueue(step2);
                            player.OnInteractionComplete = () =>
                            {
                                player.Send(Tools.FromFormat("bbbdb", 5, 30, 0, player.CharID, (byte)0));
                                player.Send(Tools.FromFormat("bbb", 6, 2, 0));
                                player.Send(Tools.FromFormat("bb", 20, 8));
                                player.Send(Tools.FromFormat("bb", 5, 4));
                                player.SaveCharacterData();
                                DebugSystem.Write($"[EveEventInterpreter] [S.Monkey Dialogue] Dialogue completed for {player.CharName} (Team full, could not recruit)");
                            };
                            player.Send(Tools.FromFormat("bbb", 6, 2, 1));
                            player.Send(step1);
                            return;
                        }

                        // Authentic follow-up: Story Cutscene 11011 (11011.sty) displaying JRole_5_3.jpg CG photo and dialogues 20048..20053
                        player.QueueData.Clear();

                        // Dispatch authentic Cutscene 11011 frame AC 20:1 Type 5 Mode 1 (Cutscene ID 11011)
                        SendPacket photoPkt = new SendPacket();
                        photoPkt.PackArray(new byte[] { 20, 1, 0, 0, 0, 2, 5, 0 });
                        photoPkt.Pack16(0);
                        photoPkt.Pack8(1); // Mode 1: Play cutscene and send AC 20:6 when finished
                        photoPkt.Pack32(11011); // Cutscene ID 11011 (11011.sty with JRole_5_3.jpg CG photo)
                        photoPkt.Pack16(0);
                        photoPkt.Pack8(1); // Branch 1
                        player.Send(photoPkt);

                        DebugSystem.Write($"[EveEventInterpreter] [S.Monkey CG Photo] Dispatched authentic Story Cutscene 11011 (11011.sty / JRole_5_3.jpg) to {player.CharName}");

                        // Await client AC 20:6 completion ACK for Cutscene 11011 before completing recruitment
                        player.OnInteractionComplete = () =>
                        {
                            // Fanfare SFX (BGM 11011 / AC 20:10)
                            player.Send(Tools.FromFormat("bb", 20, 10));

                            // Despawn S.Monkey from map (AC 22:10)
                            player.Send(Tools.FromFormat("bbwbb", 22, 10, clickId, 0xFF, 0xFF));

                            // Recruit S.Monkey
                            QuestManager.SendCompanionReward(player, 17162, "S.Monkey");
                            player.Send(Tools.FromFormat("bbbs", 23, 57, 0, "S.Monkey has joined your party!"));
                            DebugSystem.Write($"[EveEventInterpreter] [S.Monkey Recruited] Cutscene 11011 finished. Recruited S.Monkey (TID 17162) for {player.CharName}. Quests 12002 (Completed) & 12003 (InProgress) updated.");

                            // Update Quests 12002 and 12003
                            if (player.Quests == null) player.Quests = new Dictionary<uint, PlayerQuest>();
                            player.Quests[12002] = new PlayerQuest(12002, QuestState.Completed, 1);
                            player.Quests[12003] = new PlayerQuest(12003, QuestState.InProgress, 1);
                            QuestManager.SavePlayerQuest(player, 12002);
                            QuestManager.SavePlayerQuest(player, 12003);
                            QuestManager.SendQuestUpdate(player, 12002, QuestState.Completed, 1);
                            QuestManager.SendQuestUpdate(player, 12003, QuestState.InProgress, 1);

                            // Mobilize player and restore scene gate & HUD
                            player.Send(Tools.FromFormat("bbbdb", 5, 30, 0, player.CharID, (byte)0));
                            player.Send(Tools.FromFormat("bbb", 6, 2, 0));
                            player.Send(Tools.FromFormat("bb", 20, 8));
                            player.Send(Tools.FromFormat("bb", 5, 4));
                            player.SaveCharacterData();
                        };
                    };

                    return true;
                }

                // 2. Select matching branch based on player quest state (if not already resolved from candidate events)
                if (selectedSub == null)
                {
                    selectedSub = SelectMatchingBranch(player, map, clickId, eventEntry);
                }
                if (selectedSub == null || selectedSub.SubEntry == null || selectedSub.SubEntry.Count == 0)
                {
                    // If chest or prop is already opened / completed
                    if (eventEntry.SubEntry.Any(s => s.SubEntry != null && s.SubEntry.Any(o => o.DialogPtr == 2 && o.dialog2 == 5)))
                    {
                        player.Send(Tools.FromFormat("bbbs", 23, 57, 0, "Empty..."));
                    }
                    player.Send(Tools.FromFormat("bb", 20, 8));
                    return true;
                }

                StartSession(player, map, clickId, eventEntry, selectedSub);
                return true;
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[EveEventInterpreter] Exception executing event for ClickID {clickId}: {ex.Message}");
                player.Send(Tools.FromFormat("bb", 20, 8));
                return false;
            }
        }

        private static int GetPlayerFreeSlots(Player player)
        {
            if (player?.Inv == null) return 0;
            int free = 0;
            for (byte s = 1; s <= 50; s++)
            {
                var slot = player.Inv[s];
                if (slot == null || slot.ItemID == 0) free++;
            }
            return free;
        }

        private static EventSubEntry SelectMatchingBranch(Player player, GameMap map, ushort clickId, EventsinMapEntries eventEntry, EventSubEntry excludeSub = null)
        {
            return FindBranch(player, map, eventEntry, 0, 0, 0, excludeSub);
        }

        private static bool HasReplacementQuestItem(Player player, EventsinMapEntries ev, EventSubEntry sub, ushort itemId)
        {
            if (player.Inv == null || ev?.SubEntry == null || sub == null) return false;
            // EVE stores additional conditions AFTER their action-bearing branch.
            // Native inventory == 0 authorizes replacement of a lost quest item.
            // If another callback already delivered it, do not deliver a second copy.
            int index = ev.SubEntry.IndexOf(sub);
            if (index < 0) return false;
            for (int i = index + 1; i < ev.SubEntry.Count; i++)
            {
                var condition = ev.SubEntry[i];
                if (condition.SubEntry != null && condition.SubEntry.Count > 0) break;
                if (condition.unknownbyte1 == 2 && condition.unknownword1 == 1 && condition.unknownword2 == 1 &&
                    condition.unknownword3 == itemId && condition.unknownword4 == 5 &&
                    condition.unknownword5 == 0 && (condition.unknownword6 & 255) == 0 &&
                    player.Inv.GetItemCount(itemId) > 0)
                    return true;
            }
            return false;
        }

        private static bool ExecuteOpcode(Player player, GameMap map, ushort clickId, EventsinMapEntries ev, EventSubEntry sub, EventSubSubEntry op)
        {
            try
            {
                DebugSystem.Write($"[EveEventInterpreter] Opcode: {op.DialogPtr}, Dialogs: ({op.dialog1}, {op.dialog2}, {op.dialog3}, {op.dialog4}), DW: ({op.unknowndword1}, {op.unknowndword2}, {op.unknowndword3})");

                switch (op.DialogPtr)
                {
                    // Opcode 1: Item Grant / Item Consume / Scene Transition / Dialogue Frame
                    case 1:
                        // Native converter FUN_0049c76c: player actions 6/7 are
                        // client event types 14/9, not dialogue text IDs.
                        if (op.dialog1 == 6 || op.dialog1 == 7)
                        {
                            player.Send(op.dialog1 == 6
                                ? BuildEventFrame(14, (byte)op.dialog2, op.dialog3, 0, 0, 0, op.subsubIndex, sub.subIndex)
                                : BuildEventFrame(9, 1, 0, 1, op.dialog2, op.dialog3, op.subsubIndex, sub.subIndex));
                            return true;
                        }
                        // Player action 5 records the return point; dialog2 identifies the Record site, not the action.
                        if (op.dialog1 == 5)
                        {
                            var point = new WarpData { DstMap = (ushort)map.MapID, DstX_Axis = player.CurX, DstY_Axis = player.CurY };
                            if (DataBase.CharacterDataBase.GlobalInstance?.SaveRecordPoint(player.CharID, point) == false) return false;
                            player.ReturnSpawnMap = point;
                            player.SendRecordPointStatus();
                            return true;
                        }

                        // Scene / Chapter Transition in Eve: dptr=1, d1=3, d2=transitionType
                        if (op.dialog1 == 3)
                        {
                            uint transitionType = op.dialog2;
                            DebugSystem.Write($"[EveEventInterpreter] Opcode 1: Scene Transition (Type: {transitionType}) on Map #{map.MapID} for {player.CharName}");

                            // Authentic Starter Ships (10014..10018): Transition Type 1 is the Shipwreck Beach sequence (Captain dialogue event)
                            if (transitionType == 1 && (map.MapID >= 10014 && map.MapID <= 10018))
                            {
                                player.PendingBeachCutscene = true;
                                var warp = new WarpData { DstMap = 10035, DstX_Axis = 1038, DstY_Axis = 2235 };
                                player.CurMap?.Teleport(TeleportType.CmD, player, 0, warp);
                                return true;
                            }
                            else
                            {
                                var warpEntry = (map.mapData ?? DataBase.GameDataBase.GlobalInstance?.EveDat?.GetMapData((ushort)map.MapID))?.WarpLoc?.FirstOrDefault(w => w.clickID == transitionType);
                                if (warpEntry != null && warpEntry.mapID > 0)
                                {
                                    var warp = new WarpData { DstMap = warpEntry.mapID, DstX_Axis = (ushort)warpEntry.x, DstY_Axis = (ushort)warpEntry.y };
                                    player.CurMap?.Teleport(TeleportType.CmD, player, 0, warp);
                                    return true;
                                }
                                else
                                {
                                    DebugSystem.Write($"[EveEventInterpreter] Warning: No WarpLoc found for transitionType {transitionType} on Map #{map.MapID} for {player.CharName}");
                                    player.Send(Tools.FromFormat("bb", 20, 8));
                                }
                            }
                            return true;
                        }

                        // Player reward kind is dialog2; its amount spans four packed bytes.
                        if (op.dialog1 == 1 && op.dialog2 == 2 && op.dialog3 == 0)
                        {
                            long balance = (long)player.Gold + unchecked((int)DecodeActionValue(op));
                            if (balance < 0 || balance > 999999) return false;
                            player.SetGold((int)balance);
                            player.Send(Tools.FromFormat("bbd", 26, 4, player.Gold));
                            player.SaveCharacterData();
                            return true;
                        }
                        if (op.dialog1 == 1 && op.dialog2 == 5 && op.dialog3 == 1)
                        {
                            int experience = unchecked((int)DecodeActionValue(op));
                            if (experience < 0 || player.Eqs == null) return false;
                            player.Eqs.CurExp = experience;
                            player.SaveCharacterData();
                            return true;
                        }
                        if (IsFullRecoveryReward(op))
                        {
                            if (player.Eqs == null) return false;
                            if (op.dialog2 == 3) player.Eqs.CurHP = player.Eqs.FullHP;
                            else player.Eqs.CurSP = player.Eqs.FullSP;
                            player.Eqs.Send8_1();
                            player.SaveCharacterData();
                            return true;
                        }
                        if (op.dialog1 == 1 && op.dialog2 != 1 && op.dialog2 != 7) return false;
                        if (op.dialog1 == 1 && op.dialog3 > 0)
                        {
                            ushort itemId = op.dialog3;
                            int amount = unchecked((int)DecodeActionValue(op));
                            if (amount == 0) return true;
                            if (amount < 0)
                            {
                                if (!player.Inv.TryApplyQuestItems(new[] { new KeyValuePair<ushort, int>(itemId, amount) }, true)) return false;
                                player.SendHeadBanner("Lost " + Game.Battle.MonsterDropManager.ResolveItemName(itemId) + " x" + -(long)amount);
                                player.SaveCharacterData();
                                return true;
                            }
                            else // Positive -> Give / Grant Item to player (authentic quantity in dialog4 >> 8)
                            {
                                uint chestKey = (uint)(map.MapID * 1000 + (ev != null ? ev.clickID : clickId));
                                if (!player.NativeEventActive && sub != null && sub.unknownbyte1 == 3 && player.Quests != null && player.Quests.ContainsKey(chestKey) && player.Quests[chestKey].State == QuestState.Completed)
                                {
                                    ushort chestClickId = (ushort)(ev != null ? ev.clickID : clickId);
                                    player.Send(Tools.FromFormat("bbwb", 22, 1, chestClickId, (byte)1));
                                    player.Send(Tools.FromFormat("bbbs", 23, 57, 0, "The chest is empty."));
                                    player.Send(Tools.FromFormat("bb", 20, 8));
                                    DebugSystem.Write($"[EveEventInterpreter] Chest {chestKey} is already empty for {player.CharName}");
                                    return true;
                                }

                                int giveCount = amount;
                                lock (player.Inv.SyncRoot)
                                {
                                    if (HasReplacementQuestItem(player, ev, sub, itemId)) return true;
                                    if (IsXaolanRobberyReward(player, sub, op))
                                    {
                                        PlayerQuest completed;
                                        if (player.Quests.TryGetValue(13032, out completed) && completed.State == QuestState.Completed) return true;
                                        if (!player.Inv.TryAddItems(new Dictionary<ushort, int> { { itemId, giveCount }, { 30025, 1 } })) return false;
                                        player.SendHeadBanner("Received Shale x2 and Star x1");
                                    }
                                    else if (!QuestManager.GrantItemReward(player, itemId, giveCount)) return false;
                                }
                                string itemName = Game.Battle.MonsterDropManager.ResolveItemName(itemId);

                                player.Send(Tools.FromFormat("bb", 20, 10)); // Fanfare / Action advance per PCAP Frame 164
                                if (sub != null && sub.unknownbyte1 == 3)
                                {
                                    ushort chestClickId = (ushort)(ev != null ? ev.clickID : clickId);
                                    SendPacket chestAnim = Tools.FromFormat("bbwb", 22, 1, chestClickId, (byte)1);
                                    player.Send(chestAnim);


                                    if (player.Quests == null) player.Quests = new Dictionary<uint, PlayerQuest>();
                                    player.Quests[chestKey] = new PlayerQuest(chestKey, QuestState.Completed, 1);
                                    QuestManager.SavePlayerQuest(player, chestKey);
                                }
                                player.SaveCharacterData();
                                DebugSystem.Write($"[EveEventInterpreter] Opcode 1: Granted Item {itemName} (#{itemId}) x{giveCount} to {player.CharName}");
                                return true;
                            }
                        }
                        // Dialogue Sequence fallback
                        if (op.dialog2 > 0)
                        {
                            uint talkId24 = (uint)op.dialog2 | ((uint)op.dialog3 << 16);
                            if ((talkId24 == 11087 || talkId24 == 50062 || talkId24 == 30025) && GetPlayerFreeSlots(player) >= 1)
                            {
                                return true; // Suppress false-positive inventory full dialog
                            }

                            byte portrait = (byte)(op.dialog1 > 0 ? op.dialog1 : 3);
                            byte speakerClickId = (byte)(portrait == 7 ? 0 : clickId);
                            SendPacket dPkt = new SendPacket();
                            dPkt.Pack8(20);
                            dPkt.Pack8(1);
                            dPkt.Pack8(0); dPkt.Pack8(0); dPkt.Pack8(0);
                            dPkt.Pack8((byte)op.subsubIndex);
                            dPkt.Pack8(1);
                            dPkt.Pack8(portrait);
                            dPkt.Pack8(speakerClickId);
                            dPkt.Pack8(0);
                            dPkt.Pack8(1); dPkt.Pack8(0); dPkt.Pack8(0); dPkt.Pack8(0);
                            dPkt.Pack8(0);
                            dPkt.Pack8((byte)(talkId24 & 0xFF));
                            dPkt.Pack8((byte)((talkId24 >> 8) & 0xFF));
                            dPkt.Pack8((byte)((talkId24 >> 16) & 0xFF));

                            player.OnInteractionComplete = () =>
                            {
                                player.Send(Tools.FromFormat("bbb", 6, 2, 0));
                                player.Send(Tools.FromFormat("bb", 20, 8));
                                player.SaveCharacterData();
                            };

                            player.Send(Tools.FromFormat("bbb", 6, 2, 1));
                            player.Send(dPkt);
                            return true;
                        }
                        break;

                    // Opcode 2: Dialogue response line, Prop Break, or Gathering Node Despawn
                    case 2:
                        if (IsCliveActorAction(map, op))
                        {
                            if (op.dialog2 == 4 || op.dialog2 == 8)
                            {
                                if (op.dialog3 > byte.MaxValue) return false;
                                // Native converter type9 modes 2/1 map to AC22:8
                                // animation and AC22:7 expression (client 0x443868/0x4437d0).
                                player.Send(op.dialog2 == 4
                                    ? Tools.FromFormat("bbwbb", 22, 8, op.dialog1, (byte)op.dialog3, (byte)op.unknowndword2)
                                    : Tools.FromFormat("bbwb", 22, 7, op.dialog1, (byte)op.dialog3));
                            }
                            else if (op.dialog2 == 10)
                            {
                                // Native type11 resets/waits on an authored actor path.
                                player.Send(BuildEventFrame(11, (byte)op.dialog1, op.dialog2,
                                    (byte)op.dialog3, op.unknowndword2, 0, op.subsubIndex, sub.subIndex));
                            }
                            else
                            {
                                // EVE 2/11 carries the star ID (Bootes20, Niss6 or Roca8), not a fade duration.
                                // AC15:20's last byte is the scene actor. Client 0x311656
                                // uses its position and holds event ACK while +0x732a is set.
                                byte star = (byte)DecodeActionValue(op);
                                player.Send(Tools.FromFormat("bbbb", 15, 20, star, op.dialog1));
                                player.Send(BuildEventFrame(13, (byte)op.dialog3, op.dialog1,
                                    (byte)op.unknowndword2, star, 0, op.subsubIndex, sub.subIndex));
                            }
                            return true;
                        }
                        if (op.dialog2 == 7)
                        {
                            return PreEventInterpreter.SendActorPose(player, op.dialog1, op.dialog3,
                                (byte)DecodeActionValue(op));
                        }
                        if (op.dialog2 == 9)
                        {
                            player.Send(BuildEventFrame(4, 3, op.dialog1, (byte)op.dialog3,
                                DecodeActionValue(op), 0, op.subsubIndex, sub.subIndex));
                            return true;
                        }
                        // Prop Break / Chest Open Animation: dialog2 == 5
                        if (op.dialog2 == 5)
                        {
                            ushort propClickId = (ushort)(op.dialog1 > 0 ? op.dialog1 : clickId);
                            int propState = UsesNativeMechanisms(map.MapID) ? unchecked((int)DecodeActionValue(op)) : 1;
                            if (UsesNativeMechanisms(map.MapID))
                            {
                                if (op.dialog3 != 1 || propState < 0 || propState > 1) return false;
                                player.NativePropStates[propClickId] = propState;
                            }
                            SendPacket anim = Tools.FromFormat("bbwb", 22, 1, propClickId, (byte)propState);
                            player.Send(anim);

                            // Determine if this prop is a one-time per-player quest container or a renewable gathering node
                            bool isQuestProp = (sub != null && sub.unknownword1 > 0) ||
                                               (ev != null && ev.SubEntry != null && ev.SubEntry.Any(s => s.unknownword1 > 0));

                            if (!isQuestProp)
                            {
                                map?.Broadcast(anim);

                                var qn = map?.NpcList?.FirstOrDefault(n => n.CickID == propClickId) as QuestNpc;
                                if (qn != null)
                                {
                                    qn.IsBroken = true;
                                    qn.RespawnTime = DateTime.Now.AddSeconds(60);
                                }
                            }

                            DebugSystem.Write($"[EveEventInterpreter] Prop Break/Open Animation (AC 22:1) for ClickID {propClickId} triggered by {player.CharName} (isQuestProp={isQuestProp})");
                            return true;
                        }

                        // Dynamic Actor Visibility / Despawn / Gathering Node (dialog2 == 2 or dialog2 == 3)
                        if (op.dialog2 == 2 || op.dialog2 == 3)
                        {
                            ushort targetClickId = (ushort)(op.dialog1 > 0 ? op.dialog1 : clickId);
                            if (UsesNativeMechanisms(map.MapID))
                                player.NativeActorVisibility[targetClickId] = op.dialog2 == 3;
                            if (op.dialog2 == 3)
                            {
                                PreEventInterpreter.SendActorShow(player, targetClickId);
                            }
                            else
                            {
                                PreEventInterpreter.SendActorHide(player, targetClickId);
                            }

                            bool isQuestEntity = (sub != null && sub.unknownword1 > 0) ||
                                                 (ev != null && ev.SubEntry != null && ev.SubEntry.Any(s => s.unknownword1 > 0));

                            if (!isQuestEntity)
                            {
                                var qn = map?.NpcList?.FirstOrDefault(n => n.CickID == targetClickId) as QuestNpc;
                                if (qn != null && qn.IsStaticNpc())
                                {
                                    qn.IsBroken = (op.dialog2 == 2);
                                    if (qn.IsBroken)
                                    {
                                        qn.RespawnTime = DateTime.Now.AddSeconds(60);
                                    }
                                    foreach (var viewer in map.PlayersList.ToArray())
                                    {
                                        if (viewer == player) continue;
                                        if (op.dialog2 == 3)
                                            PreEventInterpreter.SendActorShow(viewer, targetClickId);
                                        else
                                            PreEventInterpreter.SendActorHide(viewer, targetClickId);
                                    }
                                }
                            }

                            DebugSystem.Write($"[EveEventInterpreter] Dynamic Actor State (AC 22:4) for ClickID {targetClickId} -> (dialog2={op.dialog2}) for {player.CharName} (isQuestEntity={isQuestEntity})");
                            return true;
                        }

                        if (op.dialog2 > 0 || op.dialog3 > 0)
                        {
                            uint talkId24 = 0;
                            if (op.dialog3 >= 10000 && op.dialog3 <= 65000)
                                talkId24 = (uint)op.dialog3 | ((uint)op.dialog2 << 16);
                            else if (op.dialog2 >= 10000 && op.dialog2 <= 65000)
                                talkId24 = (uint)op.dialog2;

                            if (talkId24 < 10000) return true; // Not a real text dialogue, avoid sending blank dialogs!

                            if ((talkId24 == 11087 || talkId24 == 50062 || talkId24 == 30025) && GetPlayerFreeSlots(player) >= 1)
                            {
                                return true; // Suppress false-positive inventory full dialog
                            }

                            byte portrait = 3;
                            byte speakerClickId = (byte)clickId;
                            if (op.dialog2 == 2)
                            {
                                portrait = 7;
                                speakerClickId = 0;
                            }
                            else
                            {
                                portrait = 3;
                                if (op.dialog1 > 0)
                                {
                                    speakerClickId = (byte)op.dialog1;
                                }
                            }

                            SendPacket dPkt = new SendPacket();
                            dPkt.Pack8(20);
                            dPkt.Pack8(1);
                            dPkt.Pack8(0); dPkt.Pack8(0); dPkt.Pack8(0);
                            dPkt.Pack8((byte)op.subsubIndex);                 // step
                            dPkt.Pack8(1);
                            dPkt.Pack8(portrait);
                            dPkt.Pack8(speakerClickId);
                            dPkt.Pack8(0);
                            dPkt.Pack8(1); dPkt.Pack8(0); dPkt.Pack8(0); dPkt.Pack8(0);
                            dPkt.Pack8(0);
                            dPkt.Pack8((byte)(talkId24 & 0xFF));
                            dPkt.Pack8((byte)((talkId24 >> 8) & 0xFF));
                            dPkt.Pack8((byte)((talkId24 >> 16) & 0xFF));

                            player.OnInteractionComplete = () =>
                            {
                                player.Send(Tools.FromFormat("bbb", 6, 2, 0));
                                player.Send(Tools.FromFormat("bb", 20, 8));
                            };

                            player.Send(Tools.FromFormat("bbb", 6, 2, 1));
                            player.Send(dPkt);
                            return true;
                        }
                        break;

                    // Opcode 3: Companion Pet Recruitment
                    case 3:
                        return ExecuteCompanionAction(player, map, op);

                    // Opcode 5: Quest Mark / Flag State Update
                    case 5:
                        if (op.dialog1 > 0)
                        {
                            uint questId = op.dialog1;
                            if (op.dialog2 != 1 && op.dialog2 != 2) return false;
                            if (player.Quests == null) player.Quests = new Dictionary<uint, PlayerQuest>();
                            PlayerQuest previous;
                            int current = player.Quests.TryGetValue(questId, out previous) && previous.State == QuestState.InProgress
                                ? previous.Step : 0;
                            int amount = (int)DecodeActionValue(op);
                            int next = op.dialog2 == 2 ? current : current + amount;
                            if (next < 0 || next > byte.MaxValue) return false;
                            byte step = (byte)next;
                            QuestState state = op.dialog2 == 2 ? QuestState.Completed : QuestState.InProgress;
                            player.Quests[questId] = new PlayerQuest(questId, state, step);

                            if (state == QuestState.Completed)
                            {
                                player.Quests[questId].CompletedAt = DateTime.UtcNow;
                            }

                            QuestManager.SavePlayerQuest(player, questId);
                            QuestManager.SendQuestUpdate(player, questId, state, step);
                            if (map.MapID == 11077 && questId == 13087 && current == 0 && next == 1 && state == QuestState.InProgress)
                                QuestManager.SendStoryConstellations(player, true);
                            if (((map.MapID == 12211 && questId == 13151) ||
                                 (map.MapID == 11149 && questId == 13173) ||
                                 (map.MapID == 11185 && questId == 13203)) && state == QuestState.InProgress)
                                QuestManager.SendStoryConstellations(player);

                            if (map != null && !player.NativeEventActive)
                            {
                                QuestManager.SyncPerPlayerNpcVisibility(player, (ushort)map.MapID);
                            }

                            DebugSystem.Write($"[EveEventInterpreter] Opcode 5: Updated Quest/Flag #{questId} -> Step {step} ({state}) for {player.CharName}");
                            return true;
                        }
                        break;

                    // Opcode 4 & Opcode 6: Start Battle / Mob Engagement
                    case 4:
                    case 6:
                        {
                            var nativeMap = DataBase.GameDataBase.GlobalInstance?.EveDat?.GetMapData((ushort)map.MapID);
                            // Opcode 4 references a formation, not a speaker/ClickID.
                            var formation = nativeMap?.ExtBattleInfo?.FirstOrDefault(f => f.entryID == op.dialog2);
                            uint[] enemyIds = op.DialogPtr == 4 && formation != null
                                ? formation.subentry1.Select(e => (uint)(e.unknownbyte1 | (e.unknownbyte2 << 8))).Where(id => id >= 10000).ToArray()
                                : new uint[0];
                            uint battleId = enemyIds.Length > 0 ? enemyIds[0] :
                                (uint)(op.dialog2 >= 10000 ? op.dialog2 : op.dialog1 >= 10000 ? op.dialog1 : 0);
                            if (battleId == 0)
                            {
                                DebugSystem.Write("[EveEventInterpreter] Missing native battle formation; refusing to attack a dialogue speaker.");
                                return false;
                            }
                            string mobName = Game.Battle.PvEBattleManager.ResolveMonsterName(battleId);
                            int mobLv = Math.Max(5, (int)op.dialog3);
                            int mobHp = (op.unknowndword1 > 0) ? (int)op.unknowndword1 : (mobLv * 35 + 200);

                            var outcomeToken = player.NativeEventToken;
                            var qCtx = new Game.Battle.QuestBattleContext
                            {
                                QuestID = sub.unknownword1,
                                Step = (byte)sub.unknownword3,
                                MapID = (ushort)map.MapID,
                                ClickID = clickId,
                                OnVictory = () =>
                                {
                                    DebugSystem.Write($"[EveEventInterpreter] Quest battle victory against {mobName} (TID {battleId}) for {player.CharName}");
                                    RunOutcome(player, map, clickId, ev, 4, op.dialog1, 1, outcomeToken, sub);
                                },
                                OnDefeat = () =>
                                {
                                    RunOutcome(player, map, clickId, ev, 4, op.dialog1, 2, outcomeToken, sub);
                                },
                                // Niss/Xaolan story battles must release their scene on flee.
                                // Their authored defeat branch supplies any retry dialogue.
                                OnFlee = (map.MapID == 12050 && (ev.clickID == 39 || ev.clickID == 16)) ||
                                    (map.MapID == 12002 && (ev.clickID == 5 || ev.clickID == 7)) ||
                                    (map.MapID == 60001 && (ev.clickID == 44 || ev.clickID == 48 || ev.clickID == 83)) ||
                                    (map.MapID == 11077 && ev.clickID == 7) ||
                                    (map.MapID == 12523 && (ev.clickID == 2 || ev.clickID == 12))
                                    ? new Action(() => RunOutcome(player, map, clickId, ev, 4, op.dialog1, 2, outcomeToken, sub))
                                    : null
                            };

                            player.Send(Tools.FromFormat("bb", 20, 8));
                            Battle.PvEBattleManager.StartPvEBattle(player, clickId, mobName, npcLv: mobLv, npcHp: mobHp, monsterTid: battleId, questContext: qCtx, enemyTemplates: enemyIds);
                            DebugSystem.Write($"[EveEventInterpreter] Started Quest Battle #{battleId} ({mobName}, Lv.{mobLv}) for {player.CharName}");
                            return true;
                        }

                    // Opcode 7: System Action Trigger / UI / Real Map Teleport
                    case 7:
                        if (op.dialog1 > 0)
                        {
                            ushort actionOrMap = op.dialog1;

                            // If actionOrMap < 1000, this is a System Action Code (Shop, Storage, Heal, Spawn Point, etc.)
                            if (actionOrMap < 1000)
                            {
                                switch (actionOrMap)
                                {
                                    // 1: Weapon Shop, 2: Props / Item Shop, 3: Armor Shop
                                    case 1:
                                    case 2:
                                    case 3:
                                        uint catalogId = (actionOrMap == 1) ? 0x0001FB84u : ((actionOrMap == 2) ? 0x0001FB85u : 0x0001FB83u);
                                        player.CurrentShopCatalogId = catalogId;
                                        player.Send(Tools.FromFormat("bbd", 35, 12, catalogId));
                                        player.Send(Tools.FromFormat("bb", 20, 8));
                                        return true;

                                    // 4: Props Keep / Storage Bank
                                    case 4:
                                        player.OpenPropsKeeper();
                                        DebugSystem.Write($"[EveEventInterpreter] Opened Character Props Keep Storage for {player.CharName}");
                                        return true;

                                    // 5: Pet Hotel (the doctor's third option).
                                    case 5:
                                        player.OpenPetHotel();
                                        return true;

                                    // 6, 7: Witch Doctor / Clinic Full Heal & Revive (Player + Companions)
                                    case 6:
                                    case 7:
                                        player.BeginNpcRest();
                                        return true;

                                    // 9: Stock Keep / Hotel / Guild Storage
                                    case 9:
                                        player.OpenPropsKeeper();
                                        DebugSystem.Write($"[EveEventInterpreter] Opened Character Stock Keep for {player.CharName}");
                                        return true;

                                    default:
                                        player.Send(Tools.FromFormat("bb", 20, 8));
                                        DebugSystem.Write($"[EveEventInterpreter] Executed System Action Code #{actionOrMap} for {player.CharName}");
                                        return true;
                                }
                            }
                            else
                            {
                                // Actual Map Teleport (Maps in WLO are >= 10000)
                                ushort targetMap = actionOrMap;
                                ushort tx = op.dialog2 > 0 ? op.dialog2 : (ushort)500;
                                ushort ty = op.dialog3 > 0 ? op.dialog3 : (ushort)500;
                                var warp = new WarpData() { DstMap = targetMap, DstX_Axis = tx, DstY_Axis = ty };
                                map.Teleport(TeleportType.CmD, player, 0, warp);
                                DebugSystem.Write($"[EveEventInterpreter] Warped {player.CharName} to Map {targetMap} ({tx},{ty})");
                                return true;
                            }
                        }
                        break;

                    // Opcode 9: Minigame Trigger (Whack-a-mole, Target Shooting, Woodcutting, etc.)
                    case 9:
                        if (op.dialog1 > 0)
                        {
                            byte gameType = (byte)op.dialog1;
                            uint seed = op.dialog2 > 0 ? (uint)(op.dialog2 | (1 << 16)) : 0x012AF8;
                            uint questId = sub.unknownword1;

                            var outcomeToken = player.NativeEventToken;
                            player.OnMinigameWon = () => RunOutcome(player, map, clickId, ev, 8, op.dialog1, 1, outcomeToken);
                            player.OnMinigameLost = () => RunOutcome(player, map, clickId, ev, 8, op.dialog1, 0, outcomeToken);

                            // Send AC 57 Sub 1 (Start Minigame) + AC 20 Sub 9 (Minigame Mode Lock)
                            SendPacket startPkt = new SendPacket();
                            startPkt.PackArray(new byte[] { 57, 1, gameType });
                            startPkt.Pack8((byte)(seed & 0xFF));
                            startPkt.Pack8((byte)((seed >> 8) & 0xFF));
                            startPkt.Pack8((byte)((seed >> 16) & 0xFF));
                            player.Send(startPkt);

                            SendPacket lockPkt = new SendPacket();
                            lockPkt.PackArray(new byte[] { 20, 9 });
                            player.Send(lockPkt);

                            DebugSystem.Write($"[EveEventInterpreter] Started Minigame (Type: {gameType}, Seed: 0x{seed:X}, QuestID: {questId}) for {player.CharName}");
                            return true;
                        }
                        break;


                    // Opcode 8: Sound Effect / Fanfare / CG Trigger
                    case 8:
                        {
                            if ((op.dialog1 == 1 || op.dialog1 == 2) && op.dialog4 != 31488)
                            {
                                player.Send(BuildEventFrame(5, 0, 0, 2, DecodeActionValue(op), 0, op.subsubIndex, sub.subIndex));
                                return true;
                            }
                            if (op.dialog4 == 31488) // 0x7B00 (Thunder / Storm Cutscene Trigger)
                            {
                                player.PlayingStormCutscene = true;
                                player.StormCutsceneAckReceived = false;
                                player.StormCutsceneStartTime = DateTime.UtcNow;

                                // 1. Official PCAP Frame 0318: Dialog Step 3 Cinematic Event Trigger (Exact 18 bytes)
                                SendPacket step3Pkt = new SendPacket();
                                step3Pkt.PackArray(new byte[] { 20, 1, 0, 0, 0, 3, 5, 0, 0, 0, 2, 0x7B, 0, 0, 0, 0, 0, 0 });
                                player.Send(step3Pkt);

                                // 2. Official PCAP Frame 0321: CG Movie Trigger (AC 186:12 Cutscene #1)
                                SendPacket cutscenePkt = new SendPacket();
                                cutscenePkt.PackArray(new byte[] { 186, 12, 1, 0, 0, 0, 0 });
                                player.Send(cutscenePkt);

                                DebugSystem.Write($"[EveEventInterpreter] Dispatched Authentic Storm Cutscene Step 3 (AC 20:1 & AC 186:12) for {player.CharName} (awaiting client AC 186:9 and AC 20:6)");
                                return true;
                            }
                            else
                            {
                                player.Send(Tools.FromFormat("bb", 20, 10));
                                DebugSystem.Write($"[EveEventInterpreter] Played Fanfare SFX (AC 20:10) for {player.CharName}");
                                return true;
                            }
                        }

                    // These opcodes are not currency/EXP amounts. Keep unknown actions explicit;
                    // scalar player rewards are encoded by player action 1/1 above.
                    case 10:
                        DebugSystem.Write("[EveEventInterpreter] Unsupported native action " + op.DialogPtr);
                        return false;

                    // EVE converter 0x49c76c maps opcode 11 to subject 4 (skill),
                    // target=d1, mode=1, grade=packed value. All authored grants teach grade 1.
                    case 11:
                        if (UnsupportedRewardAction(op)) return false;
                        var learned = player.PlayerSkills.FirstOrDefault(s => s.SkillID == op.dialog1);
                        // Replayed recruitment/learning dialogue must preserve trained proficiency.
                        if (learned != null && learned.Grade >= 1) return true;
                        Game.SkillRelated.SkillManager.UnlockSkill(player, op.dialog1, 1);
                        return true;

                    // Native EVE converter 0x49c76c: subject 13, target=d2,
                    // kind=d1, icon=d3-1. AC22:12 consumes the same zero-based icon.
                    case 13:
                        if (op.dialog1 < 1 || op.dialog1 > 3 || op.dialog3 < 1 || op.dialog3 > 256) return false;
                        SendMinimapMarker(player, (byte)op.dialog1, op.dialog2, (byte)(op.dialog3 - 1));
                        return true;

                    // Native converter -> AC20:1 type 15; client 0x307032 plays
                    // Sound/BGM####.wav and 0x3074af sends AC20:6 when complete.
                    case 15:
                        if (UnsupportedRewardAction(op)) return false;
                        player.Send(BuildEventFrame(15, (byte)op.dialog2, 0, 0,
                            DecodeActionValue(op), 0, op.subsubIndex, sub.subIndex));
                        return true;

                    // Opcode 12: Player Character Transformation (e.g. Swapping places with Breillat TemplateID 13)
                    case 12:
                        if (op.dialog2 > 0)
                        {
                            ushort targetTemplateId = op.dialog2;
                            if (targetTemplateId == 13) // Breillat
                            {
                                player.Body = BodyStyle.Big_Female;
                                player.Head = (byte)HairStyle_BigF.Breillat;

                                // Despawn NPC ClickID 5 (Breillat)
                                player.Send(Tools.FromFormat("bbwbb", 22, 10, clickId, 0xFF, 0xFF));

                                // Visual Model Transformation to Breillat (AC 5:12)
                                player.Send(Tools.FromFormat("bbdb", 5, 12, player.CharID, (byte)targetTemplateId));

                                // Equip Breillat's Maid Outfit automatically
                                if (player.Eqs != null)
                                {
                                    player.Eqs.SetBreillatOutfit();
                                }

                                // Visual Maid Dress appearance update (AC 5:2)
                                SendPacket vDress = Tools.FromFormat("bbdw", 5, 2, player.CharID, (ushort)21991);
                                player.Send(vDress);
                                player.CurMap?.Broadcast(vDress, "Ex", player.CharID);

                                // Play Fanfare
                                player.Send(Tools.FromFormat("bb", 20, 10));

                                DebugSystem.Write($"[EveEventInterpreter] Player {player.CharName} transformed into Breillat (TemplateID {targetTemplateId}) and equipped Maid Outfit!");
                                return true;
                            }
                        }
                        break;

                    // Opcode 16: Map Mechanisms, Levers, and Puzzle Door Switches (38 Occurrences)
                    case 16:
                        if (Game.Experimental.ExperimentalFeaturesManager.Instance.EnableMapMechanisms)
                        {
                            return Game.Experimental.ExperimentalFeaturesManager.Instance.MapMechanisms.ToggleMechanism(
                                (ushort)(player.CurMap?.MapID ?? 0), (byte)op.dialog1, player);
                        }
                        player.Send(Tools.FromFormat("bb", 20, 8));
                        player.Send(Tools.FromFormat("bb", 5, 4));
                        return true;

                    // Opcode 17: Instance Dungeon Wave & Floor Advancement Signals (934 Occurrences)
                    case 17:
                        if (Game.Experimental.ExperimentalFeaturesManager.Instance.EnableInstanceDungeons)
                        {
                            return Game.Experimental.ExperimentalFeaturesManager.Instance.InstanceDungeons.OnWaveSignal(
                                player, op.dialog1, op.dialog2);
                        }
                        return true;

                    default:
                        DebugSystem.Write($"[EveEventInterpreter] Unhandled Opcode: {op.DialogPtr}");
                        break;
                }
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[EveEventInterpreter] Error executing opcode {op.DialogPtr}: {ex.Message}");
            }
            return false;
        }

        private static SendPacket BuildDialoguePacket(byte speakerClickId, uint talkId, byte stepNum, byte portrait = 3, byte subIndex = 1)
        {
            SendPacket dPkt = new SendPacket();
            dPkt.Pack8(20);                                   // AC
            dPkt.Pack8(1);                                    // SubCode
            dPkt.Pack8(0); dPkt.Pack8(0); dPkt.Pack8(0);     // session padding
            dPkt.Pack8(stepNum);                             // step
            dPkt.Pack8(1);                                    // fixed
            dPkt.Pack8(portrait);                             // portrait (3=NPC, 7=Player)
            dPkt.Pack8((byte)(portrait == 7 ? 0 : speakerClickId)); // npc click id (0 for player portrait)
            dPkt.Pack8(0);                                    // padding
            dPkt.Pack8(1); dPkt.Pack8(0); dPkt.Pack8(0); dPkt.Pack8(0); // flags
            dPkt.Pack8(0);                                    // padding
            dPkt.Pack8((byte)(talkId & 0xFF));                // TalkID LSB
            dPkt.Pack8((byte)((talkId >> 8) & 0xFF));         // TalkID MID
            dPkt.Pack8((byte)((talkId >> 16) & 0xFF));        // TalkID MSB
            return dPkt;
        }

        private struct MonkeyDialogueStep
        {
            public uint TalkId;
            public byte Portrait;
            public byte Speaker;

            public MonkeyDialogueStep(uint talkId, byte portrait, byte speaker)
            {
                TalkId = talkId;
                Portrait = portrait;
                Speaker = speaker;
            }
        }
    }
}
