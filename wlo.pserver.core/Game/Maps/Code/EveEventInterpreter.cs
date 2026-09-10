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
    public static class EveEventInterpreter
    {
        public static bool TryExecute(Player player, GameMap map, ushort clickId)
        {
            if (player == null || map == null || clickId == 0) return false;

            try
            {
                var mapData = DataBase.GameDataBase.GlobalInstance?.EveDat?.GetMapData((ushort)map.MapID);
                if (mapData == null) return false;

                var npcEntry = mapData.Npclist?.FirstOrDefault(n => n.clickId == clickId);
                var mapNpc = map.NpcList?.FirstOrDefault(n => n.CickID == clickId) as QuestNpc;

                // If NPC is a wild/roaming monster, immediately initiate PvE Battle
                if ((mapNpc != null && mapNpc.IsWildMonster()) || (npcEntry != null && npcEntry.npcId >= 17000 && npcEntry.npcId <= 17999 && (mapNpc == null || mapNpc.IsWildMonster())))
                {
                    uint tid = npcEntry != null && npcEntry.npcId > 0 ? (uint)npcEntry.npcId : (mapNpc?.TemplateID ?? 17000);
                    string mName = mapNpc?.Name;
                    if (string.IsNullOrEmpty(mName) || mName.Equals("Npc", StringComparison.OrdinalIgnoreCase) || mName.StartsWith("unknown", StringComparison.OrdinalIgnoreCase))
                    {
                        mName = Game.Battle.PvEBattleManager.ResolveMonsterName(tid);
                    }
                    int mLv = mapNpc != null && mapNpc.Level > 0 ? (int)mapNpc.Level : 5;
                    int mHp = mapNpc != null && mapNpc.HP > 0 ? (int)mapNpc.HP : 200;
                    Battle.PvEBattleManager.StartPvEBattle(player, clickId, mName, mLv, mHp, tid);
                    DebugSystem.Write($"[EveEventInterpreter] Monster encounter initiated for {player.CharName} vs {mName} (TID {tid}, ClickID {clickId})");
                    return true;
                }

                EventsinMapEntries eventEntry = null;
                EventSubEntry selectedSub = null;

                if (npcEntry != null && npcEntry.Events != null && npcEntry.Events.Count > 0)
                {
                    // Scan registered events for this NPC to find the first event with an eligible branch
                    foreach (var evId in npcEntry.Events)
                    {
                        var candidate = mapData.Events?.FirstOrDefault(e => e.clickID == evId);
                        if (candidate != null && candidate.SubEntry != null && candidate.SubEntry.Count > 0)
                        {
                            var sub = SelectMatchingBranch(player, map, clickId, candidate);
                            if (sub != null)
                            {
                                eventEntry = candidate;
                                selectedSub = sub;
                                break;
                            }
                        }
                    }

                    // Fallback to first available event if no specific state branch was matched
                    if (eventEntry == null)
                    {
                        foreach (var evId in npcEntry.Events)
                        {
                            eventEntry = mapData.Events?.FirstOrDefault(e => e.clickID == evId);
                            if (eventEntry != null && eventEntry.SubEntry != null && eventEntry.SubEntry.Count > 0)
                                break;
                        }
                    }
                }

                // If this is an interactive map prop / entity not registered in Npclist, check direct event match on clickID
                if (eventEntry == null && mapData.Events != null && (npcEntry == null || (npcEntry.Events == null || npcEntry.Events.Count == 0)))
                {
                    eventEntry = mapData.Events.FirstOrDefault(e => e.clickID == clickId);
                }

                if (eventEntry == null || eventEntry.SubEntry == null || eventEntry.SubEntry.Count == 0)
                    return false;

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
                    var monkeySteps = new List<MonkeyDialogueStep>();

                    // Authentic full dialogue sequence (Steps 1..11)
                    monkeySteps.Add(new MonkeyDialogueStep(20038, 7, (byte)clickId)); // Player: Oh? It's a Monkey!
                    monkeySteps.Add(new MonkeyDialogueStep(20039, 3, (byte)clickId)); // Monkey: Squeak~ Squeak~ Squeak~
                    monkeySteps.Add(new MonkeyDialogueStep(20040, 7, (byte)clickId)); // Player: Monkey, are you ok?
                    monkeySteps.Add(new MonkeyDialogueStep(20041, 7, (byte)clickId)); // Player: (Revive the monkey)
                    monkeySteps.Add(new MonkeyDialogueStep(20042, 3, (byte)clickId)); // Monkey: Squeak! Squeak! Squeak!
                    monkeySteps.Add(new MonkeyDialogueStep(20043, 7, (byte)clickId)); // Player: Ok, you have regained consciousness.
                    monkeySteps.Add(new MonkeyDialogueStep(20044, 7, (byte)clickId)); // Player: Be careful next time! Monkey.
                    monkeySteps.Add(new MonkeyDialogueStep(20045, 3, (byte)clickId)); // Monkey: ... ... ... ??
                    monkeySteps.Add(new MonkeyDialogueStep(20046, 7, (byte)clickId)); // Player: Oh? Why are you following me?
                    monkeySteps.Add(new MonkeyDialogueStep(20047, 7, (byte)clickId)); // Player: I don't have anything to eat, so don't follow me! Just stay back!
                    monkeySteps.Add(new MonkeyDialogueStep(20048, 3, (byte)clickId)); // Monkey: Squeak~ Squeak~ Squeak~ (crying sound)

                    if (isTeamFull)
                    {
                        monkeySteps.Add(new MonkeyDialogueStep(31146, 7, (byte)clickId)); // Player: My team is already full...
                        monkeySteps.Add(new MonkeyDialogueStep(20048, 3, (byte)clickId)); // Monkey: Squeak~ crying
                    }
                    else
                    {
                        monkeySteps.Add(new MonkeyDialogueStep(20049, 7, (byte)clickId)); // Player: Oh? It seems that you have mistaken me for your mom?
                        monkeySteps.Add(new MonkeyDialogueStep(20050, 3, (byte)clickId)); // Monkey: Squeak~ (Nods vigorously)
                        monkeySteps.Add(new MonkeyDialogueStep(20051, 7, (byte)clickId)); // Player: Do I look like Female Monkey? How annoying!
                        monkeySteps.Add(new MonkeyDialogueStep(20052, 7, (byte)clickId)); // Player: Ah! That's ok! You can accompany me! It's better to have one than none.
                        monkeySteps.Add(new MonkeyDialogueStep(20053, 3, (byte)clickId)); // Monkey: Squeak, squeak, squeak!
                    }

                    for (int i = 0; i < monkeySteps.Count; i++)
                    {
                        byte sNum = (byte)(i + 1);
                        var sInfo = monkeySteps[i];
                        SendPacket stepPkt = BuildDialoguePacket(sInfo.Speaker, sInfo.TalkId, sNum, sInfo.Portrait);
                        string dText = global::DataFiles.TalkResolver.Resolve(sInfo.TalkId, player.CharName, true);
                        if (!string.IsNullOrEmpty(dText))
                        {
                            dText = dText.Replace("\r", " ").Replace("\n", " ");
                            if (dText.Length > 60) dText = dText.Substring(0, 60) + "...";
                        }
                        if (i == 0)
                        {
                            player.Send(Tools.FromFormat("bbb", 6, 2, 1));
                            player.Send(stepPkt);
                            DebugSystem.Write($"[EveEventInterpreter] Sent S.Monkey Step 1 (TalkID: #{sInfo.TalkId}) - \"{dText}\" to {player.CharName}");
                        }
                        else
                        {
                            player.QueueData.Enqueue(stepPkt);
                            DebugSystem.Write($"[EveEventInterpreter] Enqueued S.Monkey Step {sNum} (TalkID: #{sInfo.TalkId}) - \"{dText}\" for {player.CharName}");
                        }
                    }

                    player.OnInteractionComplete = () =>
                    {
                        if (!isTeamFull)
                        {
                            // Despawn S.Monkey from map
                            player.Send(Tools.FromFormat("bbwbb", 22, 10, clickId, 0xFF, 0xFF));
                            map.Broadcast(Tools.FromFormat("bbwbb", 22, 10, clickId, 0xFF, 0xFF));

                            // Recruit S.Monkey
                            QuestManager.SendCompanionReward(player, 17162, "S.Monkey");
                            player.Send(Tools.FromFormat("bbbs", 23, 57, 0, "S.Monkey has joined your party!"));
                            DebugSystem.Write($"[EveEventInterpreter] Recruited S.Monkey (TID 17162) for {player.CharName}");

                            // Update Quests 12002 and 12003
                            if (player.Quests == null) player.Quests = new Dictionary<uint, PlayerQuest>();
                            player.Quests[12002] = new PlayerQuest(12002, QuestState.Completed, 1);
                            player.Quests[12003] = new PlayerQuest(12003, QuestState.InProgress, 1);
                            QuestManager.SavePlayerQuest(player, 12002);
                            QuestManager.SavePlayerQuest(player, 12003);
                            QuestManager.SendQuestUpdate(player, 12002, QuestState.Completed, 1);
                            QuestManager.SendQuestUpdate(player, 12003, QuestState.InProgress, 1);

                            // Fanfare SFX
                            player.Send(Tools.FromFormat("bb", 20, 10));
                        }

                        player.Send(Tools.FromFormat("bbb", 6, 2, 0));
                        player.Send(Tools.FromFormat("bb", 20, 8));
                        player.Send(Tools.FromFormat("bb", 5, 4));
                        player.SaveCharacterData();
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
                    player.Send(Tools.FromFormat("bb", 5, 4));
                    return true;
                }

                // 3. Execute opcodes with dialogue multi-step queueing
                player.OnDialogueChoice = null;
                player.OnInteractionComplete = null;
                player.QueueData.Clear();
                bool firstDialogSent = false;
                bool executedAny = false;
                bool interactiveSessionStarted = false;

                List<EventSubSubEntry> postDialogueOpcodes = new List<EventSubSubEntry>();

                void RunSubOpcodes(EventSubEntry sub)
                {
                    if (sub?.SubEntry == null) return;
                    uint lastDialogueTalkId = 0;
                    byte dialogStepCounter = 0;
                    foreach (var op in sub.SubEntry)
                    {
                        uint talkId24 = 0;
                        bool isFlagOp = (op.DialogPtr == 1 && (op.dialog1 == 1 || op.dialog1 == 2) && op.dialog3 >= 10000);
                        bool isAnimOp = (op.DialogPtr == 2 && (op.dialog2 == 5 || op.dialog2 == 2 || op.dialog2 == 3 || op.dialog2 == 4 || op.dialog2 == 8));
                        bool isChoiceOp = (op.DialogPtr == 2 && op.dialog2 == 6);
                        bool isSystemOp = (op.DialogPtr == 5 || op.DialogPtr == 6 || op.DialogPtr == 7 || op.DialogPtr == 8 || op.DialogPtr == 3);

                        if (!isFlagOp && !isAnimOp && !isSystemOp)
                        {
                            if (op.DialogPtr == 1 && op.dialog1 == 2 && op.dialog2 >= 10000)
                            {
                                // Player speech line: dialog2 is TalkID
                                talkId24 = (uint)op.dialog2 | (2u << 16);
                            }
                            else if (op.DialogPtr == 2)
                            {
                                if (op.dialog3 >= 10000 && op.dialog3 <= 55000 && (op.dialog2 == 1 || op.dialog2 == 0 || op.dialog2 == 2 || op.dialog2 == 6))
                                {
                                    uint highByte = (op.dialog1 > 0) ? (uint)op.dialog1 : ((op.dialog2 > 0 && op.dialog2 < 20) ? (uint)op.dialog2 : 3u);
                                    talkId24 = (uint)op.dialog3 | (highByte << 16);
                                }
                            }
                        }

                        if (talkId24 >= 10000)
                        {
                            lastDialogueTalkId = (talkId24 & 0xFFFF);
                        }

                        bool isDialog = !isFlagOp && !isAnimOp && !isSystemOp && (talkId24 >= 10000 || isChoiceOp);

                        if (isDialog)
                        {
                            dialogStepCounter++;
                            byte stepNum = dialogStepCounter;
                            byte portrait = 3; // Official WLO Protocol: 3 = NPC Portrait Window, 7 = Player Portrait Window
                            byte speakerClickId = (byte)clickId;

                            if (op.DialogPtr == 1)
                            {
                                if (op.dialog1 == 2)
                                {
                                    portrait = 7; // Player portrait
                                    speakerClickId = 0; // Official PCAP: 0 for player portrait
                                }
                                else if (op.dialog1 > 0)
                                {
                                    speakerClickId = (byte)op.dialog1;
                                }
                            }
                            else if (op.DialogPtr == 2)
                            {
                                if (op.dialog2 == 2)
                                {
                                    portrait = 7; // Player portrait
                                    speakerClickId = 0; // Official PCAP: 0 for player portrait
                                }
                                else
                                {
                                    portrait = 3; // NPC portrait
                                    if (op.dialog1 > 0)
                                    {
                                        speakerClickId = (byte)op.dialog1;
                                    }
                                }
                            }

                            // Choice prompt (dialog2 == 6) - dynamically resolves question TalkID from bytecode sequence
                            if (isChoiceOp)
                            {
                                uint choiceTalkId = (talkId24 >= 10000) ? (talkId24 & 0xFFFF) : 0;
                                if (choiceTalkId == 0)
                                {
                                    if (op.dialog3 >= 10000) choiceTalkId = (uint)op.dialog3;
                                    else if (op.dialog2 >= 10000) choiceTalkId = (uint)op.dialog2;
                                    else if (lastDialogueTalkId >= 10000)
                                    {
                                        choiceTalkId = lastDialogueTalkId + 1;
                                    }
                                    else
                                    {
                                        int currentSubIdx = eventEntry.SubEntry.IndexOf(sub);
                                        for (int si = currentSubIdx - 1; si >= 0; si--)
                                        {
                                            var prevSub = eventEntry.SubEntry[si];
                                            foreach (var po in prevSub.SubEntry)
                                            {
                                                if (po.DialogPtr == 1 && po.dialog2 >= 10000) choiceTalkId = po.dialog2;
                                                else if (po.DialogPtr == 2 && po.dialog3 >= 10000) choiceTalkId = po.dialog3;
                                                else if (po.DialogPtr == 2 && po.dialog2 >= 10000) choiceTalkId = po.dialog2;
                                            }
                                            if (choiceTalkId > 0)
                                            {
                                                choiceTalkId += 1;
                                                break;
                                            }
                                        }
                                    }
                                }

                                if (choiceTalkId > 0)
                                {
                                    lastDialogueTalkId = choiceTalkId;
                                }

                                SendPacket cPkt = new SendPacket();
                                cPkt.Pack8(20);                                   // [0] AC
                                cPkt.Pack8(1);                                    // [1] SubCode
                                cPkt.Pack8(0); cPkt.Pack8(0); cPkt.Pack8(0);     // [2-4] session padding
                                cPkt.Pack8(stepNum);                             // [5] step
                                cPkt.Pack8(6);                                    // [6] choice prompt flag (0x06)
                                cPkt.Pack8(portrait);                             // [7] portrait (3=NPC, 7=Player)
                                cPkt.Pack8(speakerClickId);                       // [8] speaker click id
                                cPkt.Pack8(0);                                    // [9] padding
                                cPkt.Pack8(0); cPkt.Pack8(0); cPkt.Pack8(0); cPkt.Pack8(0); // [10-13] 4-byte zero flags for choice menu
                                cPkt.Pack8(0);                                    // [14] padding
                                cPkt.Pack8((byte)(op.dialog3 & 0xFF));            // [15] Native Choice ID from eve.Emg
                                cPkt.Pack8((byte)((op.dialog3 >> 8) & 0xFF));     // [16] Native Choice ID MSB
                                cPkt.Pack8((byte)(sub != null ? sub.subIndex : 1)); // [17] SubEntry index

                                ushort curQuestionId = (ushort)op.dialog3;
                                player.OnDialogueChoice = (choice) =>
                                {
                                    DebugSystem.Write($"[EveEventInterpreter] Player {player.CharName} selected dialogue choice: 0x{choice:X} ({choice}) for Question #{curQuestionId}");

                                    int branchIdx = (choice >= 0x28) ? (choice - 0x28) : ((choice >= 0x1E) ? (choice - 0x1E) : Math.Max(0, choice - 1));
                                    ushort targetChoiceVal = (ushort)(30 + branchIdx);

                                    // Dynamically find branch matching choice ID and Question ID
                                    EventSubEntry choiceSub = eventEntry.SubEntry.FirstOrDefault(subEntry =>
                                        subEntry.unknownbyte1 == 7 &&
                                        (curQuestionId == 0 || subEntry.unknownword1 == curQuestionId || subEntry.unknownword1 == 0) &&
                                        (subEntry.unknownword2 == targetChoiceVal || subEntry.unknownword2 == choice)
                                    );

                                    // Fallback: match by relative index among choice branches (unknownbyte1 == 7)
                                    if (choiceSub == null)
                                    {
                                        var choiceSubs = eventEntry.SubEntry.Where(s => s.unknownbyte1 == 7 && (curQuestionId == 0 || s.unknownword1 == curQuestionId || s.unknownword1 == 0)).ToList();
                                        if (branchIdx >= 0 && branchIdx < choiceSubs.Count)
                                        {
                                            choiceSub = choiceSubs[branchIdx];
                                        }
                                    }

                                    if (choiceSub != null)
                                    {
                                        firstDialogSent = false;
                                        postDialogueOpcodes.Clear();
                                        RunSubOpcodes(choiceSub);

                                        // If this choice branch does not trigger another question, check for dedicated quiz outcome branch
                                        if (choiceSub.SubEntry != null && !choiceSub.SubEntry.Any(o => o.DialogPtr == 2 && o.dialog2 == 6))
                                        {
                                            bool hasQuizOutcomes = eventEntry.SubEntry.Any(s => s.unknownword4 == 773 || s.unknownword4 == 769);
                                            if (hasQuizOutcomes)
                                            {
                                                uint qId = curQuestionId;
                                                var op5 = choiceSub.SubEntry.FirstOrDefault(o => o.DialogPtr == 5);
                                                if (op5.DialogPtr == 5 && op5.dialog1 > 0) qId = (uint)op5.dialog1;

                                                EventSubEntry resultBranch = null;
                                                bool isSuccess = (targetChoiceVal == 31 || choice == 0x1F || choiceSub.SubEntry.Any(o => o.DialogPtr == 5));
                                                if (isSuccess)
                                                {
                                                    resultBranch = eventEntry.SubEntry.FirstOrDefault(s => s != choiceSub && (qId == 0 || s.unknownword1 == qId || s.unknownword1 == 0) && (s.unknownword4 == 773 || (s.unknownword4 & 0xFF) == 5));
                                                }
                                                else
                                                {
                                                    resultBranch = eventEntry.SubEntry.FirstOrDefault(s => s != choiceSub && (qId == 0 || s.unknownword1 == qId || s.unknownword1 == 0) && (s.unknownword4 == 769 || (s.unknownword4 & 0xFF) == 1));
                                                }

                                                if (resultBranch != null && resultBranch != choiceSub)
                                                {
                                                    DebugSystem.Write($"[EveEventInterpreter] Running quiz outcome branch Sub #{resultBranch.subIndex} for {player.CharName}");
                                                    RunSubOpcodes(resultBranch);
                                                }
                                            }
                                        }

                                        if (firstDialogSent && player.OnDialogueChoice == null)
                                        {
                                            player.OnInteractionComplete = () =>
                                            {
                                                foreach (var postOp in postDialogueOpcodes)
                                                {
                                                    ExecuteOpcode(player, map, clickId, eventEntry, choiceSub, postOp);
                                                }
                                                player.Send(Tools.FromFormat("bbb", 6, 2, 0));
                                                player.Send(Tools.FromFormat("bb", 20, 8));
                                                player.Send(Tools.FromFormat("bb", 5, 4));
                                                player.SaveCharacterData();
                                            };
                                        }
                                        else if (!firstDialogSent)
                                        {
                                            foreach (var postOp in postDialogueOpcodes)
                                            {
                                                ExecuteOpcode(player, map, clickId, eventEntry, choiceSub, postOp);
                                            }
                                            player.Send(Tools.FromFormat("bbb", 6, 2, 0));
                                            player.Send(Tools.FromFormat("bb", 20, 8));
                                            player.Send(Tools.FromFormat("bb", 5, 4));
                                            player.SaveCharacterData();
                                        }
                                    }
                                    else
                                    {
                                        player.Send(Tools.FromFormat("bbb", 6, 2, 0));
                                        player.Send(Tools.FromFormat("bb", 20, 8));
                                        player.Send(Tools.FromFormat("bb", 5, 4));
                                    }
                                };

                                if (!firstDialogSent)
                                {
                                    player.Send(Tools.FromFormat("bbb", 6, 2, 1));
                                    player.Send(cPkt);
                                    firstDialogSent = true;
                                    DebugSystem.Write($"[EveEventInterpreter] Sent Choice Step {stepNum} (Talk #{choiceTalkId}) to {player.CharName} for NPC #{speakerClickId} '{nName}'");
                                }
                                else
                                {
                                    player.QueueData.Enqueue(cPkt);
                                    DebugSystem.Write($"[EveEventInterpreter] Enqueued Choice Step {stepNum} (Talk #{choiceTalkId}) for {player.CharName} (NPC #{speakerClickId} '{nName}')");
                                }
                                executedAny = true;
                                continue;
                            }

                            if (talkId24 == 0) continue;

                            SendPacket dPkt = new SendPacket();
                            dPkt.Pack8(20);                                   // AC
                            dPkt.Pack8(1);                                    // SubCode
                            dPkt.Pack8(0); dPkt.Pack8(0); dPkt.Pack8(0);     // session padding
                            dPkt.Pack8(stepNum);                             // step
                            dPkt.Pack8(1);                                    // fixed
                            dPkt.Pack8(portrait);                             // portrait (3=NPC, 7=Player)
                            dPkt.Pack8(speakerClickId);                       // npc click id
                            dPkt.Pack8(0);                                    // padding
                            dPkt.Pack8(1); dPkt.Pack8(0); dPkt.Pack8(0); dPkt.Pack8(0); // flags
                            dPkt.Pack8(0);                                    // padding
                            dPkt.Pack8((byte)(talkId24 & 0xFF));             // TalkID LSB
                            dPkt.Pack8((byte)((talkId24 >> 8) & 0xFF));      // TalkID MID
                            dPkt.Pack8((byte)((talkId24 >> 16) & 0xFF));     // TalkID MSB

                            string diagText = "";
                            try
                            {
                                diagText = global::DataFiles.TalkResolver.Resolve((uint)talkId24, player.CharName, true);
                                if (!string.IsNullOrEmpty(diagText))
                                {
                                    diagText = diagText.Replace("\r", " ").Replace("\n", " ");
                                    if (diagText.Length > 60) diagText = diagText.Substring(0, 60) + "...";
                                    diagText = $" - \"{diagText}\"";
                                }
                            }
                            catch { }

                            uint cleanTalkId = (uint)(talkId24 & 0xFFFF);
                            string speakerName = (speakerClickId == clickId) ? nName : ((map.NpcList?.FirstOrDefault(n => n.CickID == speakerClickId) as Game.Maps.QuestNpc)?.Name ?? (mapData.Npclist != null ? Game.DataFiles.SceneDataManager.GetNpcName((ushort)(mapData.Npclist.FirstOrDefault(n => n.clickId == speakerClickId)?.npcId ?? 0)) : null));
                            if (string.IsNullOrEmpty(speakerName)) speakerName = nName;

                            if (!firstDialogSent)
                            {
                                player.Send(Tools.FromFormat("bbb", 6, 2, 1));
                                player.Send(dPkt);
                                firstDialogSent = true;
                                DebugSystem.Write($"[EveEventInterpreter] Sent Step {stepNum} (TalkID: #{cleanTalkId} / 0x{cleanTalkId:X4}){diagText} to {player.CharName} for NPC #{speakerClickId} '{speakerName}'");
                            }
                            else
                            {
                                player.QueueData.Enqueue(dPkt);
                                DebugSystem.Write($"[EveEventInterpreter] Enqueued Step {stepNum} (TalkID: #{cleanTalkId} / 0x{cleanTalkId:X4}){diagText} for {player.CharName} (NPC #{speakerClickId} '{speakerName}')");
                            }
                            executedAny = true;
                        }
                        else
                        {
                            if (firstDialogSent)
                            {
                                // Defer action opcode to execute after player finishes reading all dialogues
                                postDialogueOpcodes.Add(op);
                                executedAny = true;
                            }
                            else
                            {
                                bool res = ExecuteOpcode(player, map, clickId, eventEntry, sub, op);
                                executedAny |= res;
                                if (op.DialogPtr == 6 || op.DialogPtr == 7 || op.DialogPtr == 9 || op.DialogPtr == 13 || op.DialogPtr == 186)
                                {
                                    interactiveSessionStarted = true;
                                }
                            }
                        }
                    }
                }

                RunSubOpcodes(selectedSub);

                // If selectedSub was a quest flag setter with no dialogues and no minigame/battle, evaluate the newly activated dialogue branch
                if (!firstDialogSent && !interactiveSessionStarted)
                {
                    EventSubEntry nextSub = SelectMatchingBranch(player, map, clickId, eventEntry, excludeSub: selectedSub);
                    if (nextSub != null && nextSub.SubEntry != null && nextSub.SubEntry.Any(o => o.DialogPtr == 1 || o.DialogPtr == 2))
                    {
                        DebugSystem.Write($"[EveEventInterpreter] Cascading to newly activated dialogue branch Sub #{nextSub.subIndex} for Quest State");
                        RunSubOpcodes(nextSub);
                    }
                }

                if (firstDialogSent)
                {
                    int postIdx = 0;
                    Action executeRemainingOpcodes = null;

                    executeRemainingOpcodes = () =>
                    {
                        while (postIdx < postDialogueOpcodes.Count)
                        {
                            var postOp = postDialogueOpcodes[postIdx++];
                            if (postOp.DialogPtr == 8 && postOp.dialog4 == 31488)
                            {
                                // Trigger storm cutscene (AC 186:12). Client AC 186:9 will synchronize playback and transition to beach
                                ExecuteOpcode(player, map, clickId, eventEntry, selectedSub, postOp);
                                return;
                            }
                            else
                            {
                                ExecuteOpcode(player, map, clickId, eventEntry, selectedSub, postOp);
                            }
                        }

                        // Check if newly updated quest flags activate a follow-up action branch (e.g. Sub #5 companion recruitment / despawn)
                        EventSubEntry postSub = SelectMatchingBranch(player, map, clickId, eventEntry, excludeSub: selectedSub);
                        if (postSub != null && postSub != selectedSub && postSub.SubEntry != null)
                        {
                            bool isActionBranch = postSub.SubEntry.Any(o => o.DialogPtr == 3 || (o.DialogPtr == 2 && (o.dialog2 == 2 || o.dialog2 == 5)));
                            if (isActionBranch)
                            {
                                DebugSystem.Write($"[EveEventInterpreter] Executing follow-up action branch Sub #{postSub.subIndex} after dialogue completion");
                                foreach (var actOp in postSub.SubEntry)
                                {
                                    ExecuteOpcode(player, map, clickId, eventEntry, postSub, actOp);
                                }
                            }
                        }

                        if (player.CurMap != null && player.CurMap.MapID == 10035 && clickId == 1)
                        {
                            // Official PCAP Frame 2997: Robinson returns to normal standing posture (AC 22:12 [1, 1, 0, 6])
                            player.Send(Tools.FromFormat("bbbbbb", 22, 12, 1, 1, 0, 6));
                            player.CurMap?.Broadcast(Tools.FromFormat("bbbbbb", 22, 12, 1, 1, 0, 6), "Ex", player.CharID);
                        }

                        if (player.Emote == 9)
                        {
                            player.Emote = 0;
                            SendPacket eReset = new SendPacket();
                            eReset.PackArray(new byte[] { 32, 2 });
                            eReset.Pack32(player.CharID);
                            eReset.Pack8(0);
                            player.Send(eReset);
                            player.CurMap?.Broadcast(eReset, "Ex", player.CharID);
                        }

                        if (!postDialogueOpcodes.Any(o => o.DialogPtr == 6 || o.DialogPtr == 7 || o.DialogPtr == 8 || o.DialogPtr == 9 || o.DialogPtr == 13 || o.DialogPtr == 186 || (o.DialogPtr == 1 && o.dialog1 == 3)))
                        {
                            player.Send(Tools.FromFormat("bbb", 6, 2, 0)); // Restore UI & HUD
                            player.Send(Tools.FromFormat("bb", 20, 8));    // Screen unlock
                            player.Send(Tools.FromFormat("bb", 5, 4));     // Movement unlock
                        }
                    };

                    if (player.OnDialogueChoice == null)
                    {
                        player.OnInteractionComplete = executeRemainingOpcodes;
                    }
                }
                else if (!interactiveSessionStarted)
                {
                    if (player.Emote == 9)
                    {
                        player.Emote = 0;
                        SendPacket eReset = new SendPacket();
                        eReset.PackArray(new byte[] { 32, 2 });
                        eReset.Pack32(player.CharID);
                        eReset.Pack8(0);
                        player.Send(eReset);
                        player.CurMap?.Broadcast(eReset, "Ex", player.CharID);
                    }

                    // Fallback: unlock immediately if event completed with no interactive dialogues or minigames
                    player.Send(Tools.FromFormat("bbb", 6, 2, 0)); // Restore UI & HUD
                    player.Send(Tools.FromFormat("bb", 20, 8));
                    player.Send(Tools.FromFormat("bb", 5, 4));
                }

                return executedAny;
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[EveEventInterpreter] Exception executing event for ClickID {clickId}: {ex.Message}");
                player.Send(Tools.FromFormat("bb", 20, 8));
                player.Send(Tools.FromFormat("bb", 5, 4));
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
                // Exclude child slots of multi-cell items: ItemID==0 but Parent!=0 means slot is physically occupied
                if (slot == null || (slot.ItemID == 0 && slot.Parent == 0)) free++;
            }
            return free;
        }

        private static bool IsInventoryFullErrorBranch(EventSubEntry sub)
        {
            if (sub?.SubEntry == null) return false;
            return sub.SubEntry.Any(o =>
                o.dialog2 == 11087 || o.dialog3 == 11087 ||
                o.dialog2 == 50062 || o.dialog3 == 50062 ||
                o.dialog2 == 30025 || o.dialog3 == 30025);
        }

        private static bool IsTeamFullErrorBranch(EventSubEntry sub)
        {
            if (sub?.SubEntry == null) return false;
            return sub.SubEntry.Any(o =>
                o.dialog2 == 31146 || o.dialog3 == 31146 ||
                o.dialog2 == 20048 || o.dialog3 == 20048);
        }

        private static EventSubEntry GetExecutableBranch(Player player, EventsinMapEntries eventEntry, EventSubEntry sub)
        {
            if (sub.SubEntry != null && sub.SubEntry.Count > 0)
            {
                int free = GetPlayerFreeSlots(player);
                if (free >= 1 && IsInventoryFullErrorBranch(sub))
                {
                    // Ignore error branch if player has space
                }
                else if (player.PlayerPets != null && player.PlayerPets.Count < 4 && IsTeamFullErrorBranch(sub))
                {
                    // Ignore team full error branch if player has room for pets
                }
                else
                {
                    return sub;
                }
            }

            int freeSlots = GetPlayerFreeSlots(player);
            int currentIdx = eventEntry.SubEntry.IndexOf(sub);
            int nextIdx = (currentIdx >= 0) ? (currentIdx + 1) : 0;
            while (nextIdx < eventEntry.SubEntry.Count)
            {
                var nextSub = eventEntry.SubEntry[nextIdx];

                // If next sub is an inventory space condition check (CondType == 15)
                if (nextSub.unknownbyte1 == 15)
                {
                    byte reqSlots = (byte)Math.Max(1, (int)nextSub.unknownword1);
                    // w4 == 2: Inventory is full error condition
                    if (nextSub.unknownword4 == 2 && freeSlots >= reqSlots)
                    {
                        // Player HAS enough space, so skip the full inventory error branch
                        nextIdx++;
                        while (nextIdx < eventEntry.SubEntry.Count && (eventEntry.SubEntry[nextIdx].SubEntry == null || eventEntry.SubEntry[nextIdx].SubEntry.Count == 0))
                        {
                            nextIdx++;
                        }
                        nextIdx++; // Skip the error branch with opcodes
                        continue;
                    }
                }

                if (nextSub.SubEntry != null && nextSub.SubEntry.Count > 0)
                {
                    bool isInvFullError = IsInventoryFullErrorBranch(nextSub);
                    if (isInvFullError && freeSlots >= 1)
                    {
                        nextIdx++;
                        continue;
                    }

                    bool isTeamFull = IsTeamFullErrorBranch(nextSub);
                    if (isTeamFull && player.PlayerPets != null && player.PlayerPets.Count < 4)
                    {
                        nextIdx++;
                        continue;
                    }

                    return nextSub;
                }
                nextIdx++;
            }
            return null;
        }

        private static EventSubEntry GetOwningBranch(EventsinMapEntries eventEntry, EventSubEntry sub)
        {
            if (sub == null || eventEntry?.SubEntry == null) return null;
            if (sub.SubEntry != null && sub.SubEntry.Count > 0) return sub;
            int idx = eventEntry.SubEntry.IndexOf(sub);
            for (int i = idx - 1; i >= 0; i--)
            {
                if (eventEntry.SubEntry[i].SubEntry != null && eventEntry.SubEntry[i].SubEntry.Count > 0)
                {
                    return eventEntry.SubEntry[i];
                }
            }
            return null;
        }

        private static EventSubEntry SelectMatchingBranch(Player player, GameMap map, ushort clickId, EventsinMapEntries eventEntry, EventSubEntry excludeSub = null)
        {
            if (eventEntry.SubEntry == null || eventEntry.SubEntry.Count == 0)
                return null;

            if (eventEntry.SubEntry.Count == 1)
                return eventEntry.SubEntry[0];

            int playerFreeSlots = GetPlayerFreeSlots(player);

            // 0. Treasure Chests / Map Props (unknownbyte1 == 3)
            var chestSubs = eventEntry.SubEntry.Where(s => s.unknownbyte1 == 3).ToList();
            if (chestSubs.Count > 0)
            {
                uint chestKey = (uint)(map.MapID * 1000 + eventEntry.clickID);
                bool isOpened = player.Quests != null && player.Quests.TryGetValue(chestKey, out var pq) && pq.State == QuestState.Completed;

                if (isOpened)
                {
                    // Select the "already empty" branch (typically unknownword4 == 261 or dialog1 == 2)
                    var emptyBranch = chestSubs.FirstOrDefault(s => s.unknownword4 == 261 || (s.SubEntry != null && s.SubEntry.Any(o => o.DialogPtr == 1 && o.dialog1 == 2)));
                    if (emptyBranch != null) return emptyBranch;
                }
                else
                {
                    // Select the lootable branch (typically unknownword4 == 5 or dialog1 == 1)
                    var lootBranch = chestSubs.FirstOrDefault(s => s.unknownword4 == 5 || (s.SubEntry != null && s.SubEntry.Any(o => o.DialogPtr == 1 && o.dialog1 == 1)));
                    if (lootBranch != null) return lootBranch;
                }
            }

            // 0. Special Breillat (Map 10027 NPC 5) 10-talks character swap check — ONLY on Map 10027
            if (map.MapID == 10027 && (eventEntry.clickID == 4 || eventEntry.clickID == 5))
            {
                bool hasVoucher = player.Inv != null && player.Inv.ContainsItem(30002);
                if (!hasVoucher)
                {
                    player.BreillatTalkCount++;
                    DebugSystem.Write($"[EveEventInterpreter] Breillat Talk Count: {player.BreillatTalkCount}/10 for {player.CharName}");
                    if (player.BreillatTalkCount >= 10 && eventEntry.SubEntry.Count > 6)
                    {
                        return eventEntry.SubEntry[6]; // Sub #6: Breillat swap proposal
                    }
                }
            }

            // 1. Check Condition SubEntries (Levels, Items, Gold)
            foreach (var sub in eventEntry.SubEntry)
            {
                if (sub == excludeSub) continue;

                // Level Condition (unknownbyte1 == 1)
                if (sub.unknownbyte1 == 1 && sub.unknownword1 > 0)
                {
                    if (player.Level >= sub.unknownword1)
                    {
                        var target = GetExecutableBranch(player, eventEntry, sub);
                        if (target != null && target != excludeSub) return target;
                    }
                }

                // Item Condition (unknownbyte1 == 2)
                if (sub.unknownbyte1 == 2)
                {
                    ushort reqItem = (sub.unknownword1 >= 10000 && sub.unknownword1 <= 65000) ? sub.unknownword1 : sub.unknownword3;
                    if (reqItem > 0)
                    {
                        byte reqCount = (byte)Math.Max(1, (int)sub.unknownword2);
                        bool hasItem = player.Inv != null && player.Inv.ContainsItem(reqItem) && player.Inv.GetItemCount(reqItem) >= reqCount;

                        // (unknownword4 & 0x01) != 0 or unknownword4 == 2 or 5: Condition is Player MUST HAVE the required item
                        bool reqHave = (sub.unknownword4 == 2 || sub.unknownword4 == 5 || (sub.unknownword4 & 0x01) != 0);
                        if ((reqHave && hasItem) || (!reqHave && !hasItem))
                        {
                            var target = GetExecutableBranch(player, eventEntry, sub);
                            if (target != null && target != excludeSub)
                            {
                                // CRITICAL: Skip completion branch if associated quest is already completed
                                uint tQuestId = target.unknownword1;
                                if (tQuestId > 0 && player.Quests != null && player.Quests.TryGetValue(tQuestId, out var compPq) && compPq.State == QuestState.Completed)
                                {
                                    continue;
                                }
                                if (target.SubEntry != null)
                                {
                                    bool hasCompletedQuestOp = target.SubEntry.Any(o => o.DialogPtr == 5 && o.dialog1 > 0 &&
                                        player.Quests != null && player.Quests.TryGetValue(o.dialog1, out var qPq) && qPq.State == QuestState.Completed);
                                    if (hasCompletedQuestOp)
                                    {
                                        continue;
                                    }
                                }
                                return target;
                            }
                        }
                    }
                }

                // Quest State Condition (unknownbyte1 == 5)
                // w1 = questId, w2 = required state (1: InProgress, 2: NotStarted, 3: Completed), w3 = step
                if (sub.unknownbyte1 == 5 && sub.unknownword1 > 0)
                {
                    uint qId = sub.unknownword1;
                    ushort reqState = sub.unknownword2;
                    ushort reqStep = sub.unknownword3;
                    bool stateMatches = false;

                    if (player.Quests != null && player.Quests.TryGetValue(qId, out var pq))
                    {
                        if (reqState == 1 && pq.State == QuestState.InProgress && (reqStep == 0 || reqStep == pq.Step))
                            stateMatches = true;
                        else if (reqState == 2 && pq.State == QuestState.NotStarted)
                            stateMatches = true;
                        else if (reqState == 3 && pq.State == QuestState.Completed)
                            stateMatches = true;
                    }
                    else if (reqState == 2)
                    {
                        stateMatches = true;
                    }

                    if (stateMatches)
                    {
                        var target = GetExecutableBranch(player, eventEntry, sub);
                        if (target != null && target != excludeSub)
                        {
                            uint tQId = target.unknownword1;
                            if (tQId > 0 && player.Quests != null && player.Quests.TryGetValue(tQId, out var targetPq) && targetPq.State == QuestState.Completed)
                            {
                                continue;
                            }
                            return target;
                        }
                    }
                }
            }

            // 2. Battle Encounter Trigger Priority (DialogPtr == 4 || DialogPtr == 6)
            var battleSub = eventEntry.SubEntry.FirstOrDefault(s => s != excludeSub && s.SubEntry != null && s.SubEntry.Any(o => o.DialogPtr == 4 || o.DialogPtr == 6));
            if (battleSub != null)
            {
                uint bQuestId = battleSub.unknownword1;
                bool questDone = bQuestId > 0 && player.Quests != null && player.Quests.TryGetValue(bQuestId, out var bpq) && bpq.State == QuestState.Completed;
                if (!questDone)
                {
                    return battleSub;
                }
            }

            // 3. In-Progress Quest exact step match: unknownword2 == 1 (InProgress) && unknownword3 == pq.Step
            // Prioritize active question / riddle prompt branches (w4 == 261 / 0x0105 or containing dialog2 == 6)
            EventSubEntry activeBranch = null;
            foreach (var sub in eventEntry.SubEntry)
            {
                if (sub == excludeSub || sub.SubEntry == null || sub.SubEntry.Count == 0) continue;
                if (sub.unknownbyte1 == 4 || sub.unknownbyte1 == 7) continue; // Exclude battle outcome callbacks
                uint questId = sub.unknownword1;
                if (questId > 0 && player.Quests != null && player.Quests.TryGetValue(questId, out var pq))
                {
                    if (pq.State == QuestState.InProgress && sub.unknownword2 == 1 && (sub.unknownword3 == 0 || sub.unknownword3 == pq.Step))
                    {
                        if (sub.unknownword4 == 261 || (sub.unknownword4 & 0x0100) != 0 || sub.SubEntry.Any(o => o.DialogPtr == 2 && o.dialog2 == 6))
                        {
                            return sub;
                        }
                        if (activeBranch == null) activeBranch = sub;
                    }
                }
            }
            if (activeBranch != null) return activeBranch;

            // Check if this NPC is an active gathering node on the map
            var mapNpc = map?.NpcList?.FirstOrDefault(n => n.CickID == clickId) as QuestNpc;
            bool isGatheringNode = (mapNpc != null && (mapNpc.TemplateID == 19039 || (mapNpc.Name ?? "").ToLower().Contains("coconut") || (mapNpc.Name ?? "").ToLower().Contains("wood") || (mapNpc.Name ?? "").ToLower().Contains("ore")));

            // 4. New Quest / Not Started matching branch (or Respawned Gathering Node)
            var validCandidates = new List<EventSubEntry>();
            foreach (var sub in eventEntry.SubEntry)
            {
                if (sub == excludeSub || sub.SubEntry == null || sub.SubEntry.Count == 0) continue;
                if (sub.unknownbyte1 == 4 || sub.unknownbyte1 == 7) continue; // Exclude battle outcome callbacks
                uint questId = sub.unknownword1;
                if (questId > 0)
                {
                    if (isGatheringNode && !mapNpc.IsBroken)
                    {
                        if (sub.unknownword2 == 2 || sub.SubEntry.Any(o => o.DialogPtr == 1 && o.dialog3 >= 10000))
                        {
                            return sub;
                        }
                    }

                    if (player.Quests != null && player.Quests.TryGetValue(questId, out var pq) && pq.State == QuestState.Completed && !isGatheringNode)
                    {
                        // Already completed one-time quest / chest! Skip this branch
                        continue;
                    }

                    if (player.Quests == null || !player.Quests.ContainsKey(questId) || player.Quests[questId].State == QuestState.NotStarted || isGatheringNode)
                    {
                        if (sub.unknownword2 == 2)
                        {
                            validCandidates.Add(sub);
                        }
                    }
                }
            }

            if (validCandidates.Count > 0)
            {
                // Filter out error branches when player meets capacity requirements
                if (player.PlayerPets != null && player.PlayerPets.Count < 4)
                {
                    validCandidates.RemoveAll(s => IsTeamFullErrorBranch(s));
                }
                if (playerFreeSlots >= 1)
                {
                    validCandidates.RemoveAll(s => IsInventoryFullErrorBranch(s));
                }

                if (validCandidates.Count == 0)
                {
                    return null;
                }

                ushort GetBranchGrantItem(EventSubEntry s)
                {
                    if (s?.SubEntry == null) return 0;
                    var grantOp = s.SubEntry.FirstOrDefault(o => o.DialogPtr == 1 && o.dialog1 == 1 && o.dialog3 > 0 && o.dialog3 < 60000);
                    if (grantOp.dialog3 > 0) return grantOp.dialog3;
                    int subIdx = eventEntry.SubEntry.IndexOf(s);
                    if (subIdx >= 0 && subIdx + 2 < eventEntry.SubEntry.Count)
                    {
                        var itemSub = eventEntry.SubEntry[subIdx + 2];
                        if (itemSub != null && itemSub.unknownbyte1 == 2 && itemSub.unknownword3 > 0 && itemSub.unknownword3 < 60000)
                            return itemSub.unknownword3;
                    }
                    return 0;
                }

                bool HasPlayerItem(ushort iid)
                {
                    if (iid == 0) return true;
                    if (player.Inv != null && (player.Inv.ContainsItem(iid) || (iid == 36002 && player.Inv.ContainsItem(32000)))) return true;
                    return false;
                }

                // 1. First priority: branches granting items the player DOES NOT yet have (e.g. Tent 36002, Notepad 34038)
                var unobtainedItemBranch = validCandidates.OrderByDescending(s => s.subIndex).FirstOrDefault(s =>
                {
                    ushort gi = GetBranchGrantItem(s);
                    return gi > 0 && !HasPlayerItem(gi);
                });

                if (unobtainedItemBranch != null)
                {
                    return unobtainedItemBranch;
                }

                // 2. Second priority: companion recruitment branches
                var recruitBranch = validCandidates.FirstOrDefault(s => s.SubEntry != null && s.SubEntry.Any(o => o.DialogPtr == 3));
                if (recruitBranch != null)
                {
                    return recruitBranch;
                }

                // 3. Third priority: non-reward dialogue branch
                var nonItemBranch = validCandidates.FirstOrDefault(s => GetBranchGrantItem(s) == 0);
                if (nonItemBranch != null)
                {
                    return nonItemBranch;
                }

                return validCandidates[0];
            }

            // 5. Fallback: only when excludeSub == null (initial NPC click, not looking for follow-up state transitions)
            if (excludeSub == null)
            {
                bool isChestOrProp = eventEntry.SubEntry.Any(s => s.SubEntry != null && s.SubEntry.Any(o => o.DialogPtr == 2 && o.dialog2 == 5));
                if (!isChestOrProp)
                {
                    // Filter out branches whose quest is already completed!
                    var candidateSubs = eventEntry.SubEntry.Where(s =>
                        s.SubEntry != null &&
                        s.SubEntry.Any(o => o.DialogPtr == 1 || o.DialogPtr == 2 || o.DialogPtr == 4 || o.DialogPtr == 6) &&
                        s.unknownbyte1 != 15 &&
                        s.unknownbyte1 != 4 &&
                        s.unknownbyte1 != 7 &&
                        (playerFreeSlots < 1 || !IsInventoryFullErrorBranch(s))).ToList();

                    var eligibleSubs = candidateSubs.Where(s =>
                    {
                        uint qId = s.unknownword1;
                        if (qId > 0 && player.Quests != null && player.Quests.TryGetValue(qId, out var pq) && pq.State == QuestState.Completed)
                            return false;

                        if (s.SubEntry.Any(o => o.DialogPtr == 5 && o.dialog1 > 0 &&
                            player.Quests != null && player.Quests.TryGetValue(o.dialog1, out var pq2) && pq2.State == QuestState.Completed))
                            return false;

                        return true;
                    }).ToList();

                    // Prioritize idle / greeting dialogues (unknownbyte1 == 6)
                    var idleSub = eligibleSubs.FirstOrDefault(s => s.unknownbyte1 == 6);
                    if (idleSub != null) return idleSub;

                    var generalSub = eligibleSubs.FirstOrDefault();
                    if (generalSub != null) return generalSub;
                }
                else
                {
                    foreach (var sub in eventEntry.SubEntry)
                    {
                        if (sub.SubEntry == null || sub.SubEntry.Count == 0) continue;
                        if (sub.unknownbyte1 == 15 || sub.unknownbyte1 == 4 || sub.unknownbyte1 == 7) continue; // Skip inventory check gate branches & battle outcomes
                        if (playerFreeSlots >= 1 && IsInventoryFullErrorBranch(sub)) continue; // Skip inv full error when player has space
                        uint questId = sub.unknownword1;
                        if (questId > 0 && player.Quests != null && player.Quests.TryGetValue(questId, out var pq) && pq.State == QuestState.Completed)
                        {
                            continue;
                        }
                        if (sub.SubEntry.Any(o => o.DialogPtr == 1 || o.DialogPtr == 2 || o.DialogPtr == 4 || o.DialogPtr == 6))
                        {
                            return sub;
                        }
                    }
                }
            }

            return null;
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
                        // Scene / Chapter Transition in Eve: dptr=1, d1=3, d2=transitionType
                        if (op.dialog1 == 3)
                        {
                            uint transitionType = op.dialog2;
                            DebugSystem.Write($"[EveEventInterpreter] Opcode 1: Scene Transition (Type: {transitionType}) on Map #{map.MapID} for {player.CharName}");

                            // Transition 1 from Starter Ship -> Rhode Island Shipwreck Beach
                            if (map.MapID == 10017 || (map.MapID >= 10024 && map.MapID <= 10028))
                            {
                                player.OnInteractionComplete = () =>
                                {
                                    player.PendingBeachCutscene = true;
                                    var warp = new WarpData() { DstMap = 10035, DstX_Axis = 1038, DstY_Axis = 2235 };
                                    player.CurMap?.Teleport(TeleportType.CmD, player, 0, warp);
                                    DebugSystem.Write($"[EveEventInterpreter] Prologue Shipwreck Scene Transition completed. Teleported {player.CharName} to Map 10035 (1038, 2235)");
                                };
                                return true;
                            }
                            // Transition from Shipwreck Beach to South Island
                            else if (map.MapID == 10035)
                            {
                                player.OnInteractionComplete = () =>
                                {
                                    var warp = new WarpData() { DstMap = 11016, DstX_Axis = 402, DstY_Axis = 1035 };
                                    player.CurMap?.Teleport(TeleportType.CmD, player, 0, warp);
                                    DebugSystem.Write($"[EveEventInterpreter] Ocean Raft Scene Transition completed. Teleported {player.CharName} to Map 11016 (402, 1035)");
                                };
                                return true;
                            }
                            return true;
                        }

                        // Item Take/Give Opcode in Eve: dptr=1, d1=1, d2=count, d3=Item ID, d4=Mode
                        if (op.dialog1 == 1 && op.dialog3 > 0)
                        {
                            ushort itemId = op.dialog3;
                            byte count = (byte)Math.Max(1, (int)op.dialog2);

                            // dialog4 == 65280 (0xFF00) -> Take / Remove Item from player (e.g. Honeycomb #30034)
                            if (op.dialog4 == 65280 || (op.dialog4 & 0xFF00) == 0xFF00)
                            {
                                if (player.Inv != null && player.Inv.ContainsItem(itemId))
                                {
                                    player.Inv.RemoveItem(itemId, count);
                                    string remName = Game.Battle.MonsterDropManager.ResolveItemName(itemId) ?? $"Item #{itemId}";
                                    player.Send(Tools.FromFormat("bbbs", 23, 57, 0, $"Lost {remName}"));
                                    player.SaveCharacterData();
                                    DebugSystem.Write($"[EveEventInterpreter] Opcode 1: Consumed / Removed Item {remName} (#{itemId}) x{count} from {player.CharName}");
                                }
                                return true;
                            }
                            else // dialog4 == 256 (0x0100) or positive -> Give / Grant Item to player
                            {
                                int freeSlots = GetPlayerFreeSlots(player);
                                if (freeSlots < 1)
                                {
                                    player.Send(Tools.FromFormat("bbbs", 23, 57, 0, "Inventory is full!"));
                                    DebugSystem.Write($"[EveEventInterpreter] Inventory full — could not grant Item #{itemId} to {player.CharName}");
                                    return false;
                                }
                                player.Inv.AddItem(itemId, count);
                                string itemName = Game.Battle.MonsterDropManager.ResolveItemName(itemId);
                                if (string.IsNullOrEmpty(itemName) || itemName.StartsWith("Item #"))
                                {
                                    itemName = (itemId == 48010 || itemId == 48016) ? "Raft" : (itemId == 32075 ? "Space Remote" : (itemId == 36002 ? "Space Capsule" : (itemId == 34038 ? "Notebook" : $"Item #{itemId}")));
                                }
                                player.Send(Tools.FromFormat("bbbs", 23, 57, 0, $"Obtain {itemName}"));
                                player.Send(Tools.FromFormat("bb", 20, 10)); // Fanfare
                                if (sub != null && sub.unknownbyte1 == 3)
                                {
                                    uint chestKey = (uint)(map.MapID * 1000 + (ev != null ? ev.clickID : 1));
                                    if (player.Quests == null) player.Quests = new Dictionary<uint, PlayerQuest>();
                                    player.Quests[chestKey] = new PlayerQuest(chestKey, QuestState.Completed, 1);
                                    QuestManager.SavePlayerQuest(player, chestKey);
                                }
                                player.SaveCharacterData();
                                DebugSystem.Write($"[EveEventInterpreter] Opcode 1: Granted Item {itemName} (#{itemId}) x{count} to {player.CharName}");
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
                                player.Send(Tools.FromFormat("bb", 5, 4));
                                player.SaveCharacterData();
                            };

                            player.Send(Tools.FromFormat("bbb", 6, 2, 1));
                            player.Send(dPkt);
                            return true;
                        }
                        break;

                    // Opcode 2: Dialogue response line, Prop Break, or Gathering Node Despawn
                    case 2:
                        // Prop Break / Chest Open Animation: dialog2 == 5
                        if (op.dialog2 == 5)
                        {
                            ushort propClickId = (ushort)(op.dialog1 > 0 ? op.dialog1 : clickId);
                            SendPacket anim = Tools.FromFormat("bbwb", 22, 1, propClickId, (byte)1);
                            player.Send(anim);
                            map?.Broadcast(anim);

                            var qn = map?.NpcList?.FirstOrDefault(n => n.CickID == propClickId) as QuestNpc;
                            if (qn != null)
                            {
                                qn.IsBroken = true;
                                qn.RespawnTime = DateTime.Now.AddSeconds(60);
                            }

                            DebugSystem.Write($"[EveEventInterpreter] Prop Break/Open Animation (AC 22:1) for ClickID {propClickId} triggered by {player.CharName}");
                            return true;
                        }

                        // Gathering Node Despawn Animation: dialog2 == 2 (e.g. Coconut, Wood, Ore)
                        if (op.dialog2 == 2)
                        {
                            ushort propClickId = (ushort)(op.dialog1 > 0 ? op.dialog1 : clickId);
                            SendPacket anim = Tools.FromFormat("bbwbb", 22, 10, propClickId, (byte)0xFF, (byte)0xFF);
                            player.Send(anim);
                            map?.Broadcast(anim);

                            var qn = map?.NpcList?.FirstOrDefault(n => n.CickID == propClickId) as QuestNpc;
                            if (qn != null)
                            {
                                qn.IsBroken = true;
                                qn.RespawnTime = DateTime.Now.AddSeconds(60);
                            }

                            DebugSystem.Write($"[EveEventInterpreter] Gathering Node ClickID {propClickId} gathered/despawned for {player.CharName}");
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
                                player.Send(Tools.FromFormat("bb", 5, 4));
                            };

                            player.Send(Tools.FromFormat("bbb", 6, 2, 1));
                            player.Send(dPkt);
                            return true;
                        }
                        break;

                    // Opcode 3: Companion Pet Recruitment
                    case 3:
                        if (op.dialog2 > 0)
                        {
                            uint companionId = op.dialog2;
                            string petName = companionId == 12178 ? "Robinson" : (Game.Battle.PvEBattleManager.ResolveMonsterName(companionId) ?? $"Companion #{companionId}");
                            QuestManager.SendCompanionReward(player, companionId, petName);
                            player.Send(Tools.FromFormat("bbbs", 23, 57, 0, $"{petName} has joined your party!"));
                            DebugSystem.Write($"[EveEventInterpreter] Recruited Companion Pet {petName} (#{companionId}) for {player.CharName}");
                            return true;
                        }
                        break;

                    // Opcode 5: Quest Mark / Flag State Update
                    case 5:
                        if (op.dialog1 > 0)
                        {
                            uint questId = op.dialog1;
                            byte step = (byte)Math.Max(1, (int)(op.dialog3 > 0 ? op.dialog3 : (op.dialog4 >> 8 > 0 ? op.dialog4 >> 8 : 1)));
                            // In WLO eve.Emg: dialog2 == 2 (advance/complete), dialog2 == 1 (activate/start), step >= 250 (complete)
                            QuestState state = (op.dialog2 == 2 || step >= 250) ? QuestState.Completed : QuestState.InProgress;
                            if (player.Quests == null) player.Quests = new Dictionary<uint, PlayerQuest>();

                            // SAFEGUARD: Never demote an already Completed quest back to InProgress!
                            if (player.Quests.TryGetValue(questId, out var existingQ) && existingQ.State == QuestState.Completed && state == QuestState.InProgress)
                            {
                                DebugSystem.Write($"[EveEventInterpreter] Opcode 5: Prevented demoting completed Quest #{questId} back to InProgress for {player.CharName}");
                                return true;
                            }

                            if (!player.Quests.ContainsKey(questId))
                            {
                                player.Quests[questId] = new PlayerQuest(questId, state, step);
                            }
                            else
                            {
                                player.Quests[questId].Step = step;
                                player.Quests[questId].State = state;
                            }

                            if (state == QuestState.Completed)
                            {
                                player.Quests[questId].CompletedAt = DateTime.UtcNow;
                            }

                            QuestManager.SavePlayerQuest(player, questId);
                            QuestManager.SendQuestUpdate(player, questId, state, step);


                            DebugSystem.Write($"[EveEventInterpreter] Opcode 5: Updated Quest/Flag #{questId} -> Step {step} ({state}) for {player.CharName}");
                            return true;
                        }
                        break;

                    // Opcode 4 & Opcode 6: Start Battle / Mob Engagement
                    case 4:
                    case 6:
                        {
                            uint battleId = 0;
                            if (op.DialogPtr == 6)
                            {
                                battleId = (uint)(op.dialog1 > 0 ? op.dialog1 : op.dialog2);
                            }
                            else if (op.DialogPtr == 4)
                            {
                                if (op.dialog2 >= 10000) battleId = (uint)op.dialog2;
                                else if (op.dialog1 >= 10000) battleId = (uint)op.dialog1;
                                else
                                {
                                    // Check if sub had a preceding dialog opcode with an NPC clickId (e.g. d1=18)
                                    var refOp = sub.SubEntry?.FirstOrDefault(o => o.DialogPtr != op.DialogPtr && o.dialog1 > 0);
                                    ushort targetClick = (ushort)(refOp.HasValue ? refOp.Value.dialog1 : clickId);
                                    var mapNpc = map?.NpcList?.FirstOrDefault(n => n.CickID == targetClick) as QuestNpc;
                                    if (mapNpc != null && mapNpc.TemplateID >= 10000)
                                    {
                                        battleId = (uint)mapNpc.TemplateID;
                                    }
                                    else if (clickId == 17) // Trap3 / Beehive -> Baby Bee
                                    {
                                        battleId = 17064;
                                    }
                                    else
                                    {
                                        battleId = (uint)(op.dialog2 > 0 ? op.dialog2 : (op.dialog1 > 0 ? op.dialog1 : 17064));
                                    }
                                }
                            }

                            string mobName = Game.Battle.PvEBattleManager.ResolveMonsterName(battleId);
                            int mobLv = Math.Max(5, (int)op.dialog3);
                            int mobHp = (op.unknowndword1 > 0) ? (int)op.unknowndword1 : (mobLv * 35 + 200);

                            // Find victory branch and defeat branch in eventEntry
                            EventSubEntry victoryBranch = ev?.SubEntry?.FirstOrDefault(s => (s.unknownbyte1 == 7 || s.unknownbyte1 == 4) && s.unknownword2 == 1)
                                                       ?? ev?.SubEntry?.FirstOrDefault(s => s != sub && s.SubEntry != null && s.SubEntry.Any(o => o.DialogPtr == 5 || (o.DialogPtr == 1 && o.dialog4 >= 32768)));
                            EventSubEntry defeatBranch = ev?.SubEntry?.FirstOrDefault(s => (s.unknownbyte1 == 7 || s.unknownbyte1 == 4) && s.unknownword2 == 2);
                            EventSubEntry rewardBranch = ev?.SubEntry?.FirstOrDefault(s => s != sub && s.SubEntry != null && s.SubEntry.Any(o => o.DialogPtr == 1 && o.dialog1 == 1 && o.dialog3 > 0));

                            var qCtx = new Game.Battle.QuestBattleContext
                            {
                                QuestID = sub.unknownword1,
                                Step = (byte)sub.unknownword3,
                                MapID = (ushort)map.MapID,
                                ClickID = clickId,
                                OnVictory = () =>
                                {
                                    DebugSystem.Write($"[EveEventInterpreter] Quest battle victory against {mobName} (TID {battleId}) for {player.CharName}");
                                    if (victoryBranch != null && victoryBranch.SubEntry != null)
                                    {
                                        foreach (var vOp in victoryBranch.SubEntry)
                                        {
                                            ExecuteOpcode(player, map, clickId, ev, victoryBranch, vOp);
                                        }
                                    }
                                    if (rewardBranch != null && rewardBranch.SubEntry != null)
                                    {
                                        foreach (var rOp in rewardBranch.SubEntry)
                                        {
                                            ExecuteOpcode(player, map, clickId, ev, rewardBranch, rOp);
                                        }
                                    }
                                    else if (sub.unknownword1 > 0)
                                    {
                                        QuestManager.SetPlayerQuestState(player, sub.unknownword1, QuestState.Completed);
                                    }
                                    QuestManager.ReplayActorVisibility(player, map);
                                },
                                OnDefeat = () =>
                                {
                                    DebugSystem.Write($"[EveEventInterpreter] Quest battle defeat against {mobName} (TID {battleId}) for {player.CharName}");
                                    if (defeatBranch != null && defeatBranch.SubEntry != null)
                                    {
                                        foreach (var dOp in defeatBranch.SubEntry)
                                        {
                                            ExecuteOpcode(player, map, clickId, ev, defeatBranch, dOp);
                                        }
                                    }
                                }
                            };

                            player.Send(Tools.FromFormat("bb", 20, 8));
                            Battle.PvEBattleManager.StartPvEBattle(player, clickId, mobName, npcLv: mobLv, npcHp: mobHp, monsterTid: battleId, questContext: qCtx);
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
                                // Send authentic AC 20:1 Type 7 step to client so the client GUI (Storage / Shop / Bank) opens immediately!
                                SendPacket sysPkt = new SendPacket();
                                sysPkt.Pack8(20);
                                sysPkt.Pack8(1);
                                sysPkt.Pack8(0); sysPkt.Pack8(0); sysPkt.Pack8(0);
                                sysPkt.Pack8((byte)Math.Max(1, (int)op.subsubIndex)); // step
                                sysPkt.Pack8(7);                                 // Type 7: System Action / UI
                                sysPkt.Pack16(actionOrMap);                      // 1=Weapon Shop, 2=Props Shop, 3=Armor, 4=Props Keep Storage, 5=Save Point, 7=Doctor Heal, 9=Stock Keep
                                sysPkt.Pack8(0);
                                sysPkt.Pack8(0); sysPkt.Pack8(0); sysPkt.Pack8(0); sysPkt.Pack8(0);
                                player.Send(sysPkt);

                                switch (actionOrMap)
                                {
                                    // 1: Weapon Shop, 2: Props / Item Shop, 3: Armor Shop
                                    case 1:
                                    case 2:
                                    case 3:
                                        uint shopCatalogId = (actionOrMap == 1) ? 0x0001FB84u : (actionOrMap == 3 ? 0x0001FB83u : 0x0001FB85u);
                                        player.Send(Tools.FromFormat("bb", 20, 8));
                                        player.Send(Tools.FromFormat("bbdb", 35, 12, shopCatalogId, (byte)0));
                                        player.Send(Tools.FromFormat("bb", 5, 4));
                                        string shopType = (actionOrMap == 1 ? "Weapon Shop" : (actionOrMap == 2 ? "Props Shop" : "Armor Shop"));
                                        player.SendSystemMessage($"🏪 [{shopType}]: Opened shopping catalog.");
                                        DebugSystem.Write($"[EveEventInterpreter] Opened {shopType} (Code {actionOrMap}, Catalog: 0x{shopCatalogId:X}) for {player.CharName}");
                                        return true;

                                    // 4: Props Keep / Storage Bank
                                    case 4:
                                        player.OpenPropsKeeper();
                                        player.SendSystemMessage("🏦 [Props Keep]: Storage vault opened.");
                                        DebugSystem.Write($"[EveEventInterpreter] Opened Character Props Keep Storage for {player.CharName}");
                                        return true;

                                    // 5: Save Respawn / Memory Point
                                    case 5:
                                        DataBase.CharacterDataBase.GlobalInstance?.ExecuteNonQuery($"UPDATE characters SET location_map = '{map.MapID}', location_x = '{player.CurX}', location_y = '{player.CurY}' WHERE charID = '{player.CharID}';");
                                        
                                        // TalkID 0x0379B6 ("Memory point saved!")
                                        SendPacket savePkt = new SendPacket();
                                        savePkt.PackArray(new byte[] { 20, 1, 0, 0, 0, 1, 1, 3, (byte)clickId, 0, 1, 0, 0, 0, 0, 0xB6, 0x79, 0x03 });
                                        player.Send(savePkt);
                                        player.Send(Tools.FromFormat("bbb", 5, 21, (byte)1));
                                        player.Send(Tools.FromFormat("bb", 20, 10)); // Fanfare
                                        player.Send(Tools.FromFormat("bb", 20, 8));
                                        player.Send(Tools.FromFormat("bb", 5, 4));
                                        player.SendSystemMessage($"💾 Respawn point saved to Map {map.MapID} pos({player.CurX},{player.CurY})!");
                                        DebugSystem.Write($"[EveEventInterpreter] Saved spawn point for {player.CharName} at Map {map.MapID} ({player.CurX},{player.CurY})");
                                        return true;

                                    // 6, 7: Witch Doctor / Clinic Full Heal & Revive (Player + Companions)
                                    case 6:
                                    case 7:
                                        if (player.Eqs != null)
                                        {
                                            player.Eqs.CurHP = player.Eqs.FullHP;
                                            player.Eqs.CurSP = player.Eqs.FullSP;
                                            player.Eqs.Send8_1(true);
                                        }
                                        if (player.PlayerPets != null)
                                        {
                                            foreach (var pet in player.PlayerPets.Values)
                                            {
                                                if (pet != null)
                                                {
                                                    pet.HP = pet.MaxHP;
                                                    pet.SP = pet.MaxSP;
                                                }
                                            }
                                        }
                                        player.Send(Tools.FromFormat("bbd", 5, 18, (uint)player.CharID));
                                        player.Send(Tools.FromFormat("bbd", 31, 2, (uint)0xFFFFFFFF));
                                        player.Send(Tools.FromFormat("bb", 20, 9));
                                        player.Send(Tools.FromFormat("bb", 20, 8));
                                        player.Send(Tools.FromFormat("bb", 5, 4));
                                        player.SendSystemMessage($"✨ [Witch Doctor]: HP and SP fully restored! (HP: {player.Eqs?.CurHP}/{player.Eqs?.FullHP}, SP: {player.Eqs?.CurSP}/{player.Eqs?.FullSP})");
                                        DebugSystem.Write($"[EveEventInterpreter] Witch Doctor healed {player.CharName} and companions to full HP/SP.");
                                        return true;

                                    // 9: Stock Keep / Hotel / Guild Storage
                                    case 9:
                                        player.OpenPropsKeeper();
                                        player.SendSystemMessage("🏨 [Stock Keep]: Stock keeper vault opened.");
                                        DebugSystem.Write($"[EveEventInterpreter] Opened Character Stock Keep for {player.CharName}");
                                        return true;

                                    default:
                                        player.Send(Tools.FromFormat("bb", 20, 8));
                                        player.Send(Tools.FromFormat("bb", 5, 4));
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

                            // In WLO arcade machines, beating the minigame awards the Minigame Voucher (Item #30002)
                            // (op.dialog2 is the game duration/difficulty parameter 11000, not an item ID)
                            ushort rewardItemId = 30002;
                            byte rewardCount = 1;

                            player.OnMinigameWon = () =>
                            {
                                try
                                {
                                    if (player.Inv != null)
                                    {
                                        string itemName = Game.Battle.MonsterDropManager.ResolveItemName(rewardItemId) ?? "Voucher";
                                        player.Inv.AddItem(rewardItemId, rewardCount);
                                        player.Send(Tools.FromFormat("bbbs", 23, 57, 0, $"Obtained {itemName} x{rewardCount}!"));
                                        DebugSystem.Write($"[EveEventInterpreter] Granted Minigame Reward {itemName} (#{rewardItemId}) x{rewardCount} to {player.CharName}");
                                    }

                                    if (questId > 0)
                                    {
                                        if (player.Quests == null) player.Quests = new Dictionary<uint, PlayerQuest>();
                                        player.Quests[questId] = new PlayerQuest(questId, QuestState.Completed, 1);
                                        player.Quests[questId].CompletedAt = DateTime.UtcNow;
                                        QuestManager.SavePlayerQuest(player, questId);
                                        QuestManager.SendQuestUpdate(player, questId, QuestState.Completed, 1);
                                    }
                                    DebugSystem.Write($"[EveEventInterpreter] Player {player.CharName} won Minigame (Type {gameType})");
                                }
                                catch (Exception ex)
                                {
                                    DebugSystem.Write($"[EveEventInterpreter] Error in OnMinigameWon: {ex.Message}");
                                }
                            };
                            player.OnMinigameLost = () =>
                            {
                                DebugSystem.Write($"[EveEventInterpreter] Player {player.CharName} lost Minigame (Type {gameType})");
                            };

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
                            if (op.dialog4 == 31488) // 0x7B00 (Thunder / Storm Cutscene Trigger)
                            {
                                player.PlayingStormCutscene = true;

                                // 1. Official PCAP Frame 1914: CG Movie Trigger (AC 186:12 Cutscene #1)
                                SendPacket cutscenePkt = new SendPacket();
                                cutscenePkt.PackArray(new byte[] { 186, 12, 1, 0, 0, 0, 0 });
                                player.Send(cutscenePkt);

                                // 2. Official PCAP Frame 1942: Dialog Step 3 Cinematic Event Trigger
                                // [AC=20][Sub=1][Step=3 (4B)][Type=5][Target=0 (4B)][Param=2 (2B)][Sound/ID=31488 (2B)][Padding=0 (4B)]
                                player.Send(Tools.FromFormat("bbdbbwwd", 20, 1, (uint)3, (byte)5, (byte)0, (ushort)2, (ushort)31488, (uint)0));

                                DebugSystem.Write($"[EveEventInterpreter] Dispatched Authentic Storm Cutscene Step 3 (AC 186:12 & AC 20:1) for {player.CharName} (awaiting client finish AC 20:6)");
                                return true;
                            }
                            else
                            {
                                player.Send(Tools.FromFormat("bb", 20, 10));
                                DebugSystem.Write($"[EveEventInterpreter] Played Fanfare SFX (AC 20:10) for {player.CharName}");
                                return true;
                            }
                        }

                    // Opcode 10: Gold / Currency Grant
                    case 10:
                        if (op.dialog1 > 0)
                        {
                            uint gold = (uint)op.dialog1;
                            player.AddGold((int)gold);
                            player.Send(Tools.FromFormat("bbbs", 23, 57, 0, $"Obtained {gold} Gold!"));
                            DebugSystem.Write($"[EveEventInterpreter] Granted {gold} Gold to {player.CharName}");
                            return true;
                        }
                        break;

                    // Opcode 11: Experience Point (EXP) Grant
                    case 11:
                        if (op.dialog1 > 0)
                        {
                            uint exp = (uint)op.dialog1;
                            if (player.Eqs != null) player.Eqs.CurExp = (int)exp;
                            player.Send(Tools.FromFormat("bbbs", 23, 57, 0, $"Obtained {exp} EXP!"));
                            DebugSystem.Write($"[EveEventInterpreter] Granted {exp} EXP to {player.CharName}");
                            return true;
                        }
                        break;

                    // Opcode 13 / 186: Cinematic Cutscene / Event Movie Animation Trigger (e.g. Ship Storm)
                    case 13:
                    case 186:
                        {
                            SendPacket moviePkt = new SendPacket();
                            moviePkt.PackArray(new byte[] { 186, 12, 1, 0, 0, 0, 0 });
                            player.Send(moviePkt);
                            DebugSystem.Write($"[EveEventInterpreter] Triggered Dynamic Cutscene / Screen Animation (AC 186:12) for {player.CharName}");
                            return true;
                        }

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
