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

                if (npcEntry != null && npcEntry.Events != null && npcEntry.Events.Count > 0)
                {
                    foreach (var evId in npcEntry.Events)
                    {
                        eventEntry = mapData.Events?.FirstOrDefault(e => e.clickID == evId);
                        if (eventEntry != null && eventEntry.SubEntry != null && eventEntry.SubEntry.Count > 0)
                            break;
                    }
                }

                // Fallback: direct match on clickID if no NPC-specific event list exists
                if (eventEntry == null && mapData.Events != null)
                {
                    eventEntry = mapData.Events.FirstOrDefault(e => e.clickID == clickId);
                }

                if (eventEntry == null || eventEntry.SubEntry == null || eventEntry.SubEntry.Count == 0)
                    return false;

                DebugSystem.Write($"[EveEventInterpreter] Executing native Event for Map {map.MapID}, NPC ClickID {clickId} -> Event {eventEntry.clickID} ('{eventEntry.Name.Trim()}'). SubEntries: {eventEntry.SubEntry.Count}");

                // 2. Select matching branch based on player quest state
                EventSubEntry selectedSub = SelectMatchingBranch(player, map, clickId, eventEntry);
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
                player.QueueData.Clear();
                bool firstDialogSent = false;
                bool executedAny = false;
                bool interactiveSessionStarted = false;

                List<EventSubSubEntry> postDialogueOpcodes = new List<EventSubSubEntry>();

                void RunSubOpcodes(EventSubEntry sub)
                {
                    if (sub?.SubEntry == null) return;
                    foreach (var op in sub.SubEntry)
                    {
                        uint talkId24 = 0;
                        bool isFlagOp = (op.DialogPtr == 1 && (op.dialog1 == 1 || op.dialog1 == 2) && op.dialog3 >= 10000);
                        bool isAnimOp = (op.DialogPtr == 2 && (op.dialog2 == 5 || op.dialog2 == 2 || op.dialog2 == 3 || op.dialog2 == 4 || op.dialog2 == 8));
                        bool isChoiceOp = (op.DialogPtr == 2 && op.dialog2 == 6 && sub.unknownbyte1 != 7);
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
                                if (op.dialog3 >= 10000 && op.dialog3 <= 65000)
                                {
                                    uint highByte = (op.dialog1 > 0) ? (uint)op.dialog1 : ((op.dialog2 > 0 && op.dialog2 < 20) ? (uint)op.dialog2 : 3u);
                                    talkId24 = (uint)op.dialog3 | (highByte << 16);
                                }
                                else if (op.dialog2 >= 10000 && op.dialog2 <= 65000)
                                {
                                    uint highByte = (op.dialog1 > 0) ? (uint)op.dialog1 : (uint)Math.Max(1, (int)op.dialog3);
                                    talkId24 = (uint)op.dialog2 | (highByte << 16);
                                }
                            }
                        }

                        bool isDialog = !isFlagOp && !isAnimOp && !isSystemOp && (talkId24 >= 10000 || isChoiceOp);

                        if (isDialog)
                        {
                            byte portrait = 3; // Official WLO Protocol: 3 = NPC Portrait Window, 7 = Player Portrait Window
                            byte speakerClickId = (byte)clickId;

                            if (op.DialogPtr == 1)
                            {
                                if (op.dialog1 == 2)
                                {
                                    portrait = 7; // Player portrait
                                    speakerClickId = 0;
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
                                    speakerClickId = 0;
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

                            byte stepNum = (byte)Math.Max(1, (int)op.subsubIndex);

                            // Choice prompt (e.g. Robinson asking 3 options, Breillat asking Yes/No: dialog2 == 6)
                            if (isChoiceOp)
                            {
                                byte choiceCount = (byte)eventEntry.SubEntry.Count(s => s.unknownbyte1 == 7);
                                if (choiceCount == 0) choiceCount = 3;

                                SendPacket cPkt = new SendPacket();
                                cPkt.Pack8(20);                                   // AC
                                cPkt.Pack8(1);                                    // SubCode
                                cPkt.Pack8(0); cPkt.Pack8(0); cPkt.Pack8(0);     // session padding
                                cPkt.Pack8(stepNum);                             // step
                                cPkt.Pack8(6);                                    // choice prompt flag (0x06)
                                cPkt.Pack8(portrait);                             // portrait
                                cPkt.Pack8(speakerClickId);                       // speaker click id
                                cPkt.Pack8(0);                                    // padding
                                cPkt.Pack8(0); cPkt.Pack8(0); cPkt.Pack8(0); cPkt.Pack8(0); // flags
                                cPkt.Pack8(0);                                    // padding
                                cPkt.Pack8(1); cPkt.Pack8(0); cPkt.Pack8(choiceCount); // option count (e.g. 0x03)

                                player.OnDialogueChoice = (choice) =>
                                {
                                    DebugSystem.Write($"[EveEventInterpreter] Player {player.CharName} selected dialogue choice: 0x{choice:X} ({choice})");

                                    // Normalize choice:
                                    // In WLO protocol:
                                    // Base 0x28 (40, 41, 42...) -> Maps to branch 30, 31, 32...
                                    // Base 0x1E (30, 31, 32...) -> Maps to branch 30, 31, 32...
                                    // Base 1 (1, 2, 3...) -> Maps to branch 30, 31, 32...
                                    int branchIdx = (choice >= 0x28) ? (choice - 0x28) : ((choice >= 0x1E) ? (choice - 0x1E) : Math.Max(0, choice - 1));
                                    ushort targetChoiceVal = (ushort)(30 + branchIdx);

                                    // Dynamically find branch matching choice ID (unknownbyte1 == 7 && (unknownword2 == targetChoiceVal || unknownword2 == choice))
                                    EventSubEntry choiceSub = eventEntry.SubEntry.FirstOrDefault(subEntry =>
                                        subEntry.unknownbyte1 == 7 && (subEntry.unknownword2 == targetChoiceVal || subEntry.unknownword2 == choice)
                                    );

                                    // Fallback: match by relative index among choice branches (unknownbyte1 == 7)
                                    if (choiceSub == null)
                                    {
                                        var choiceSubs = eventEntry.SubEntry.Where(s => s.unknownbyte1 == 7).ToList();
                                        if (branchIdx >= 0 && branchIdx < choiceSubs.Count)
                                        {
                                            choiceSub = choiceSubs[branchIdx];
                                        }
                                    }

                                    if (choiceSub != null)
                                    {
                                        // Reset firstDialogSent so the first dialogue of the selected branch is sent immediately
                                        firstDialogSent = false;
                                        RunSubOpcodes(choiceSub);
                                    }
                                    else
                                    {
                                        player.Send(Tools.FromFormat("bb", 20, 8));
                                        player.Send(Tools.FromFormat("bb", 5, 4));
                                    }
                                };

                                if (!firstDialogSent)
                                {
                                    player.Send(cPkt);
                                    firstDialogSent = true;
                                    DebugSystem.Write($"[EveEventInterpreter] Sent Choice Step {stepNum} to {player.CharName} for ClickID {speakerClickId}");
                                }
                                else
                                {
                                    player.QueueData.Enqueue(cPkt);
                                    DebugSystem.Write($"[EveEventInterpreter] Enqueued Choice Step {stepNum} for {player.CharName}");
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

                            if (!firstDialogSent)
                            {
                                player.Send(dPkt);
                                firstDialogSent = true;
                                DebugSystem.Write($"[EveEventInterpreter] Sent Step {stepNum} (TalkID: 0x{talkId24:X}) to {player.CharName} for ClickID {speakerClickId}");
                            }
                            else
                            {
                                player.QueueData.Enqueue(dPkt);
                                DebugSystem.Write($"[EveEventInterpreter] Enqueued Step {stepNum} (TalkID: 0x{talkId24:X}) for {player.CharName}");
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
                    player.OnInteractionComplete = () =>
                    {
                        foreach (var postOp in postDialogueOpcodes)
                        {
                            ExecuteOpcode(player, map, clickId, eventEntry, selectedSub, postOp);
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

                        if (!postDialogueOpcodes.Any(o => o.DialogPtr == 6 || o.DialogPtr == 7 || o.DialogPtr == 8 || o.DialogPtr == 9 || o.DialogPtr == 13 || o.DialogPtr == 186))
                        {
                            player.Send(Tools.FromFormat("bb", 20, 8));
                            player.Send(Tools.FromFormat("bb", 5, 4));
                        }
                    };
                }
                else if (!interactiveSessionStarted)
                {
                    // Fallback: unlock immediately if event completed with no interactive dialogues or minigames
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

        private static EventSubEntry GetExecutableBranch(Player player, EventsinMapEntries eventEntry, EventSubEntry sub)
        {
            if (sub.SubEntry != null && sub.SubEntry.Count > 0)
            {
                int free = GetPlayerFreeSlots(player);
                if (free >= 1 && IsInventoryFullErrorBranch(sub))
                {
                    // Ignore error branch if player has space
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
                    return nextSub;
                }
                nextIdx++;
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

            // 0. Primary Chest / Item Pickup Priority: If event has an item grant branch and player has free space, award it directly!
            if (playerFreeSlots >= 1)
            {
                var rewardBranch = eventEntry.SubEntry.FirstOrDefault(s => s != excludeSub && s.SubEntry != null && s.SubEntry.Any(o => o.DialogPtr == 5 && o.dialog2 == 1));
                if (rewardBranch != null)
                {
                    uint qId = rewardBranch.unknownword1;
                    if (qId == 0 || player.Quests == null || !player.Quests.TryGetValue(qId, out var pq) || pq.State != QuestState.Completed)
                    {
                        return rewardBranch;
                    }
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

            // 1. Condition-Specific SubEntry Evaluations (Level, Item, Quest, Pet, Gold)
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
                if (sub.unknownbyte1 == 2 && sub.unknownword3 >= 10000 && sub.unknownword3 <= 65000)
                {
                    ushort reqItem = sub.unknownword3;
                    byte reqCount = (byte)Math.Max(1, (int)sub.unknownword2);
                    if (player.Inv != null && player.Inv.ContainsItem(reqItem) && player.Inv.GetItemCount(reqItem) >= reqCount)
                    {
                        var target = GetExecutableBranch(player, eventEntry, sub);
                        if (target != null && target != excludeSub) return target;
                    }
                }

                // Companion Pet Condition (unknownbyte1 == 4)
                if (sub.unknownbyte1 == 4 && sub.unknownword1 > 0)
                {
                    if (player.PlayerPets != null && player.PlayerPets.Values.Any(pet => pet != null && pet.PetID == sub.unknownword1))
                    {
                        var target = GetExecutableBranch(player, eventEntry, sub);
                        if (target != null && target != excludeSub) return target;
                    }
                }

                // Gold Condition (unknownbyte1 == 5)
                if (sub.unknownbyte1 == 5 && sub.unknownword1 > 0)
                {
                    if (player.Gold >= sub.unknownword1)
                    {
                        var target = GetExecutableBranch(player, eventEntry, sub);
                        if (target != null && target != excludeSub) return target;
                    }
                }
            }

            // 2. In-Progress Quest exact step match: unknownword2 == 1 (InProgress) && unknownword3 == pq.Step
            foreach (var sub in eventEntry.SubEntry)
            {
                if (sub == excludeSub || sub.SubEntry == null || sub.SubEntry.Count == 0) continue;
                uint questId = sub.unknownword1;
                if (questId > 0 && player.Quests != null && player.Quests.TryGetValue(questId, out var pq))
                {
                    if (pq.State == QuestState.InProgress && sub.unknownword2 == 1 && (sub.unknownword3 == 0 || sub.unknownword3 == pq.Step))
                    {
                        return sub;
                    }
                }
            }

            // Check if this NPC is an active gathering node on the map
            var mapNpc = map?.NpcList?.FirstOrDefault(n => n.CickID == clickId) as QuestNpc;
            bool isGatheringNode = (mapNpc != null && (mapNpc.TemplateID == 19039 || (mapNpc.Name ?? "").ToLower().Contains("coconut") || (mapNpc.Name ?? "").ToLower().Contains("wood") || (mapNpc.Name ?? "").ToLower().Contains("ore")));

            // 3. New Quest / Not Started matching branch (or Respawned Gathering Node)
            foreach (var sub in eventEntry.SubEntry)
            {
                if (sub == excludeSub || sub.SubEntry == null || sub.SubEntry.Count == 0) continue;
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
                            return sub;
                        }
                    }
                }
            }

            // 3. Fallback: only when excludeSub == null (initial NPC click, not looking for follow-up state transitions)
            if (excludeSub == null)
            {
                bool isChestOrProp = eventEntry.SubEntry.Any(s => s.SubEntry != null && s.SubEntry.Any(o => o.DialogPtr == 2 && o.dialog2 == 5));
                if (!isChestOrProp)
                {
                    // Skip CondType=15 inventory gate branches and inventory-full error branches when player has space
                    var generalSub = eventEntry.SubEntry.FirstOrDefault(s =>
                        s.SubEntry != null &&
                        s.SubEntry.Any(o => o.DialogPtr == 1 || o.DialogPtr == 2) &&
                        s.unknownbyte1 != 15 &&
                        (playerFreeSlots < 1 || !IsInventoryFullErrorBranch(s)));
                    if (generalSub != null) return generalSub;
                }
                else
                {
                    foreach (var sub in eventEntry.SubEntry)
                    {
                        if (sub.SubEntry == null || sub.SubEntry.Count == 0) continue;
                        if (sub.unknownbyte1 == 15) continue; // Skip inventory check gate branches
                        if (playerFreeSlots >= 1 && IsInventoryFullErrorBranch(sub)) continue; // Skip inv full error when player has space
                        uint questId = sub.unknownword1;
                        if (questId > 0 && player.Quests != null && player.Quests.TryGetValue(questId, out var pq) && pq.State == QuestState.Completed)
                        {
                            continue;
                        }
                        if (sub.SubEntry.Any(o => o.DialogPtr == 1 || o.DialogPtr == 2))
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
                    // Opcode 1: Item Grant / Consume / Dialogue Frame
                    // Opcode 1: Set Quest Flag / Variable
                    case 1:
                        if (op.dialog3 >= 10000 && op.dialog3 <= 65000 && (op.dialog1 == 1 || op.dialog1 == 2))
                        {
                            uint flagId = op.dialog3;
                            byte step = (byte)(op.dialog4 >= 256 ? 1 : Math.Max(1, (int)op.dialog2));
                            QuestState state = (op.dialog4 >= 32768) ? QuestState.Completed : QuestState.InProgress;

                            if (player.Quests == null) player.Quests = new Dictionary<uint, PlayerQuest>();
                            if (!player.Quests.ContainsKey(flagId))
                            {
                                player.Quests[flagId] = new PlayerQuest(flagId, state, step);
                            }
                            else
                            {
                                player.Quests[flagId].Step = step;
                                player.Quests[flagId].State = state;
                                if (state == QuestState.Completed) player.Quests[flagId].CompletedAt = DateTime.UtcNow;
                            }

                            QuestManager.SavePlayerQuest(player, flagId);
                            QuestManager.SendQuestUpdate(player, flagId, state, step);
                            DebugSystem.Write($"[EveEventInterpreter] Set Quest Flag #{flagId} -> Step {step} ({state}) for {player.CharName}");
                            return true;
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
                            SendPacket dPkt = new SendPacket();
                            dPkt.Pack8(20);
                            dPkt.Pack8(1);
                            dPkt.Pack8(0); dPkt.Pack8(0); dPkt.Pack8(0);
                            dPkt.Pack8((byte)op.subsubIndex);
                            dPkt.Pack8(1);
                            dPkt.Pack8(portrait);
                            dPkt.Pack8((byte)clickId);
                            dPkt.Pack8(0);
                            dPkt.Pack8(1); dPkt.Pack8(0); dPkt.Pack8(0); dPkt.Pack8(0);
                            dPkt.Pack8(0);
                            dPkt.Pack8((byte)(talkId24 & 0xFF));
                            dPkt.Pack8((byte)((talkId24 >> 8) & 0xFF));
                            dPkt.Pack8((byte)((talkId24 >> 16) & 0xFF));

                            player.OnInteractionComplete = () =>
                            {
                                player.Send(Tools.FromFormat("bb", 20, 8));
                                player.Send(Tools.FromFormat("bb", 5, 4));
                            };

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
                                player.Send(Tools.FromFormat("bb", 20, 8));
                                player.Send(Tools.FromFormat("bb", 5, 4));
                            };

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

                    // Opcode 5: Item Grant / Item Consume & Quest State
                    case 5:
                        if (op.dialog1 > 0)
                        {
                            ushort itemId = op.dialog1;
                            byte count = (byte)Math.Max(1, (int)op.dialog3);

                            if (op.dialog2 == 2) // Item Consume
                            {
                                if (player.Inv != null && player.Inv.ContainsItem(itemId))
                                {
                                    player.Inv.RemoveItem(itemId, count);
                                    player.Send(Tools.FromFormat("bbwbbbb", 23, 7, itemId, count, 0, 0, 0));
                                    player.Send(new SendPacket(player.Inv.GetAC23_5()));
                                    player.Send(Tools.FromFormat("bb", 20, 10)); // Fanfare
                                    DebugSystem.Write($"[EveEventInterpreter] Consumed Item #{itemId} x{count} from {player.CharName}");
                                }
                            }
                            else if (op.dialog2 == 1 && op.dialog4 == 256) // Item Grant with Fanfare
                            {
                                ushort giveItemId = (itemId == 12047) ? (ushort)48010 : itemId; // Robinson grants Raft (48010) instead of Broad Spear (12047)
                                // Quest flag tracking items (12000-12999) are WLO internal markers, NOT real inventory items.
                                // Their IDs overlap with weapon DB indices causing wrong names (e.g. "War Copper Spear").
                                // Suppress from inventory but still update quest state tracker below.
                                if (giveItemId < 12000 || giveItemId >= 13000)
                                {
                                    int freeSlots = GetPlayerFreeSlots(player);
                                    if (freeSlots < 1)
                                    {
                                        player.Send(Tools.FromFormat("bbbs", 23, 57, 0, "Inventory is full!"));
                                        DebugSystem.Write($"[EveEventInterpreter] Inventory full — could not grant Item #{giveItemId} to {player.CharName}");
                                        return false;
                                    }
                                    player.Inv.AddItem(giveItemId, count);
                                    player.Send(new SendPacket(player.Inv.GetAC23_5()));
                                    string itemName = Game.Battle.MonsterDropManager.ResolveItemName(giveItemId);
                                    if (string.IsNullOrEmpty(itemName) || itemName.StartsWith("Item #"))
                                    {
                                        itemName = (giveItemId == 48010 || giveItemId == 48016) ? "Raft" : $"Item #{giveItemId}";
                                    }
                                    player.Send(Tools.FromFormat("bbbs", 23, 57, 0, $"Obtain {itemName}"));
                                    player.Send(Tools.FromFormat("bb", 20, 10)); // Fanfare
                                    DebugSystem.Write($"[EveEventInterpreter] Granted Item {itemName} (#{giveItemId}) x{count} to {player.CharName}");
                                }
                                else
                                {
                                    DebugSystem.Write($"[EveEventInterpreter] Quest flag item #{giveItemId} (12000-12999) suppressed from inventory — quest state updated only.");
                                }
                            }

                            // Update quest / flag tracker
                            uint questId = op.dialog1;
                            byte step = (byte)Math.Max(1, (int)(op.dialog3 > 0 ? op.dialog3 : (op.dialog4 >> 8 > 0 ? op.dialog4 >> 8 : 1)));
                            // Item-grant branches (dialog2==1) mark the chest quest as Completed so it cannot be looted again on re-click.
                            // Consume/check branches (dialog2==2/3) and step>=250 also mark Completed.
                            QuestState state = (op.dialog2 == 1 || op.dialog2 == 2 || op.dialog2 == 3 || step >= 250) ? QuestState.Completed : QuestState.InProgress;

                            if (player.Quests == null) player.Quests = new Dictionary<uint, PlayerQuest>();

                            if (!player.Quests.ContainsKey(questId))
                            {
                                player.Quests[questId] = new PlayerQuest(questId, state, step);
                            }
                            else
                            {
                                player.Quests[questId].Step = step;
                                player.Quests[questId].State = state;
                                if (state == QuestState.Completed) player.Quests[questId].CompletedAt = DateTime.UtcNow;
                            }

                            QuestManager.SavePlayerQuest(player, questId);
                            QuestManager.SendQuestUpdate(player, questId, state, step);
                            DebugSystem.Write($"[EveEventInterpreter] Updated Quest/Flag #{questId} -> Step {step} ({state}) for {player.CharName}");
                            return true;
                        }
                        break;

                    // Opcode 6: Start Battle (Battle Formation from ExtBattleInfo)
                    case 6:
                        if (op.dialog1 > 0)
                        {
                            uint battleId = op.dialog1;
                            player.Send(Tools.FromFormat("bb", 20, 8));
                            Battle.PvEBattleManager.StartPvEBattle(player, clickId, "Quest Battle", npcLv: 12, npcHp: 300, monsterTid: battleId);
                            DebugSystem.Write($"[EveEventInterpreter] Started Quest Battle #{battleId} for {player.CharName}");
                            return true;
                        }
                        break;

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

                    // Opcode 4: NPC Visibility / Despawn
                    case 4:
                        if (op.dialog1 > 0)
                        {
                            ushort npcClick = op.dialog1;
                            player.Send(Tools.FromFormat("bbwbb", 22, 10, npcClick, 0xFF, 0xFF));
                            DebugSystem.Write($"[EveEventInterpreter] Despawned NPC ClickID #{npcClick} for {player.CharName}");
                            return true;
                        }
                        break;

                    // Opcode 8: Sound Effect / Fanfare or Cinematic Cutscene Trigger (dialog4 == 31488)
                    case 8:
                        {
                            if (op.dialog4 == 31488 || op.dialog1 == 2 || (map.MapID == 10017 && clickId == 10))
                            {
                                SendPacket cutscenePkt = new SendPacket();
                                cutscenePkt.PackArray(new byte[] { 186, 12, 1, 0, 0, 0, 0 });
                                player.Send(cutscenePkt);
                                DebugSystem.Write($"[EveEventInterpreter] Triggered Storm Cutscene Animation (AC 186:12) for {player.CharName}");
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
    }
}
