using System;
using System.Collections.Generic;
using System.Linq;
using Game.DataFiles;
using Network;

namespace Game.QuestRelated
{
    /// <summary>
    /// Purely data-driven runtime interpreter for official Wonderland Online Eve.emg PreEvents.
    /// Handles per-player scene isolation, NPC visibility states, and multi-stage actor toggles across all 1,119 maps.
    /// </summary>
    public static class PreEventInterpreter
    {
        /// <summary>
        /// Evaluates all PreEvents for the target map against the player's current quest marks/flags.
        /// </summary>
        public static void EvaluateMapPreEvents(Player player, ushort mapId)
        {
            if (player == null) return;

            try
            {
                var eveDat = DataBase.GameDataBase.GlobalInstance?.EveDat;
                if (eveDat == null) return;

                var mapData = eveDat.GetMapData(mapId);
                if (mapData == null) return;

                HashSet<ushort> handledNpcs = new HashSet<ushort>();

                // Hide any companions on this map that have already been recruited by this player
                if (mapData.Npclist != null && mapData.Npclist.Count > 0)
                {
                    foreach (var npc in mapData.Npclist)
                    {
                        if (player.HasRecruitedCompanion(npc.Name, (ushort)npc.npcId))
                        {
                            SendActorHide(player, (ushort)npc.clickId);
                            handledNpcs.Add((ushort)npc.clickId);
                        }
                    }
                }

                // Map 12000 (Kelan Village): Exact per-player quest lifecycle isolation matching official PCAP
                if (mapId == 12000)
                {
                    bool hasRoca = player.HasRecruitedCompanion("Roca", 14162) || player.HasRecruitedCompanion(14162);

                    // 1. Father's Statue (33) & Iron Sword (35):
                    // Staged quest props for Quest 13098 ("Remembering Father").
                    // Hidden while Quest 13098 is NotStarted (state 2).
                    if (GetPlayerQuestState(player, 13098) == 2)
                    {
                        SendPropHide(player, 33);
                        SendPropHide(player, 35);
                    }
                    handledNpcs.Add(33);
                    handledNpcs.Add(35);

                    // 2. Grave Rocas (ClickID 34 & ClickID 36):
                    // In official WLO, Roca is strictly in the village center (ClickID 32).
                    // The two grave Rocas (34 & 36) are staged cutscene actors for Quest 13052 ("Death of Roca's Father").
                    // ClickID 34 (mourning Roca) is only visible while Quest 13052 is InProgress (state 1) and not recruited.
                    // ClickID 36 (standing Roca) is hidden while Quest 13052 is NotStarted (2) or Completed (3) or recruited.
                    ushort q13052State = GetPlayerQuestState(player, 13052);
                    if (q13052State != 1 || hasRoca)
                    {
                        SendActorHide(player, 34);
                    }
                    handledNpcs.Add(34);

                    if (q13052State == 2 || q13052State == 3 || hasRoca)
                    {
                        SendActorHide(player, 36);
                    }
                    handledNpcs.Add(36);

                    if (hasRoca)
                    {
                        SendActorHide(player, 32);
                    }
                    handledNpcs.Add(32);

                    // 3. Lina's Shiba Inus:
                    // ClickID 29 is the permanent dog sitting with Lina (always visible).
                    // ClickID 28 is the missing dog next to Lina (Event 35, PreEvents #5 & #6).
                    ushort q13046State = GetPlayerQuestState(player, 13046);
                    byte q13046Step = 0;
                    if (player.Quests != null && player.Quests.TryGetValue(13046, out var pq13046)) q13046Step = (byte)pq13046.Step;
                    bool q13047Done = GetPlayerQuestState(player, 13047) == 1;

                    if (q13046State == 2 || (q13046State == 1 && q13046Step < 2 && !q13047Done))
                    {
                        SendActorHide(player, 28);
                    }
                    handledNpcs.Add(28);
                    handledNpcs.Add(29); // Permanent dog: always visible, never hide

                    // 4. Lost Shiba Inu in trees/hills (ClickID 20):
                    // Only visible during Quest 13046 Step 1 (when player has to find it). Hidden when unstarted, dog found, or quest completed.
                    if (q13046State == 2 || q13046State == 3 || (q13046State == 1 && q13046Step >= 2) || q13047Done)
                    {
                        SendActorHide(player, 20);
                    }
                    handledNpcs.Add(20);

                    // 5. Baby Bees near Honeycomb (ClickID 18 & 19):
                    // Only swarm when Honeycomb (ClickID 17) is disturbed.
                    if (GetPlayerQuestState(player, 13023) == 2)
                    {
                        SendActorHide(player, 18);
                        SendActorHide(player, 19);
                    }
                    handledNpcs.Add(18);
                    handledNpcs.Add(19);

                    // 6. Staged Quest Pigs (ClickID 14, 15, 16):
                    // Match authentic official PCAP Frame 530 and Frame 614.
                    ushort q12020State = GetPlayerQuestState(player, 12020);
                    byte q12020Stp = 0;
                    if (player.Quests != null && player.Quests.TryGetValue(12020, out var pq12020)) q12020Stp = (byte)pq12020.Step;
                    bool q12021Done = GetPlayerQuestState(player, 12021) == 1;

                    if (!((q12020State == 1 && q12020Stp >= 2) || q12020State == 3 || q12021Done))
                    {
                        SendActorHide(player, 14);
                    }
                    handledNpcs.Add(14);

                    if (!(q12020State == 1 && q12020Stp == 1))
                    {
                        SendActorHide(player, 15);
                    }
                    handledNpcs.Add(15);

                    if (GetPlayerQuestState(player, 13020) == 2 || GetPlayerQuestState(player, 13021) == 1)
                    {
                        SendActorHide(player, 16);
                    }
                    handledNpcs.Add(16);

                    // 7. Permanent guideposts & honeycomb:
                    handledNpcs.Add(17);
                    handledNpcs.Add(10);
                    handledNpcs.Add(31);
                }

                if (mapData.PreEvents == null || mapData.PreEvents.Count == 0)
                {
                    DebugSystem.Write($"[PreEvent] Map {mapId}: PreEvents=null or empty — generic bytecode loop skipped");
                    return;
                }

                DebugSystem.Write($"[PreEvent] Map {mapId}: evaluating {mapData.PreEvents.Count} PreEvents for {player.CharName}");

                foreach (var preEvent in mapData.PreEvents)
                {
                    if (preEvent.subentry1 == null || preEvent.subentry1.Count == 0) continue;

                    foreach (var sub in preEvent.subentry1)
                    {
                        if (sub.unknown == null || sub.unknown.Count < 7)
                        {
                            DebugSystem.Write($"[PreEvent] Map {mapId} ClickID={preEvent.clickID}: sub.unknown too short ({sub.unknown?.Count ?? 0} bytes), skipping");
                            continue;
                        }

                        byte[] condData = sub.unknown.ToArray();
                        bool condMet = EvaluateConditionBlock(player, condData);
                        byte opcode = condData.Length > 0 ? condData[0] : (byte)0;
                        DebugSystem.Write($"[PreEvent] Map {mapId} ClickID={preEvent.clickID}: opcode=0x{opcode:X2} condMet={condMet} sub2count={sub.subentry2?.Count ?? 0}");

                        if (condMet)
                        {
                            // Condition matched! Execute action blocks in subentry2 for unhandled NPCs
                            if (sub.subentry2 != null && sub.subentry2.Count > 0)
                            {
                                foreach (var act in sub.subentry2)
                                {
                                    if (act.unknown != null && act.unknown.Count >= 10 && act.unknown[0] == 0x02)
                                    {
                                        ushort targetClickId = BitConverter.ToUInt16(act.unknown.ToArray(), 1);
                                        byte s1 = act.unknown[8]; byte s2 = act.unknown[9];
                                        DebugSystem.Write($"[PreEvent] Map {mapId} ClickID={preEvent.clickID}: action targetClickId={targetClickId} state=[{s1:X2},{s2:X2}] handled={handledNpcs.Contains(targetClickId)}");
                                        if (!handledNpcs.Contains(targetClickId))
                                        {
                                            ExecuteActionBlock(player, mapId, act.unknown.ToArray());
                                            handledNpcs.Add(targetClickId);
                                        }
                                    }
                                    else if (act.unknown != null && act.unknown.Count >= 5)
                                    {
                                        byte aop = act.unknown[0];
                                        DebugSystem.Write($"[PreEvent] Map {mapId} ClickID={preEvent.clickID}: non-0x02 action opcode=0x{aop:X2} len={act.unknown.Count}");
                                        ExecuteActionBlock(player, mapId, act.unknown.ToArray());
                                    }
                                }
                            }
                            else if (preEvent.clickID > 0 && !handledNpcs.Contains(preEvent.clickID) && mapId != 12000)
                            {
                                // Sub matched but has no action data (sub2_count=0).
                                // The parent PreEvent's clickID is the implicit target — emit actor hide (only on maps where clickID maps to entity).
                                SendActorHide(player, preEvent.clickID);
                                handledNpcs.Add(preEvent.clickID);
                            }
                        }

                    }
                }
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[PreEventInterpreter] Error evaluating PreEvents for map {mapId}: {ex.Message}");
            }
        }


        /// <summary>
        /// Determines whether an NPC should be visible upon entering a map based on dynamic quest stage requirements.
        /// </summary>
        public static bool ShouldNpcBeVisible(Player player, ushort mapId, ushort clickId)
        {
            try
            {
                if (player == null) return true;

                // Permanent guideposts (10, 31) and honeycomb (17) on Map 12000 are always visible initially
                if (mapId == 12000 && (clickId == 17 || clickId == 10 || clickId == 31))
                {
                    return true;
                }

                // Check if this map entity is a companion already recruited by the player
                var eveDat = DataBase.GameDataBase.GlobalInstance?.EveDat;
                var mapData = eveDat?.GetMapData(mapId);
                if (mapData?.Npclist != null)
                {
                    var npcDef = mapData.Npclist.FirstOrDefault(n => n.clickId == clickId);
                    if (npcDef != null && player.HasRecruitedCompanion(npcDef.Name, (ushort)npcDef.npcId))
                    {
                        return false;
                    }
                }

                // Map 12000 (Kelan Village): Exact lifecycle visibility rules
                if (mapId == 12000)
                {
                    bool hasRoca = player != null && (player.HasRecruitedCompanion("Roca", 14162) || player.HasRecruitedCompanion(14162));

                    // Father's Statue (33) & Iron Sword (35):
                    // Staged quest props for Quest 13098 ("Remembering Father").
                    // Hidden while Quest 13098 is NotStarted (state 2).
                    if (clickId == 33 || clickId == 35)
                    {
                        if (GetPlayerQuestState(player, 13098) == 2)
                        {
                            return false;
                        }
                        return true;
                    }

                    // Grave Rocas (34 & 36) are only visible during active Quest 13052 grave cutscene
                    if (clickId == 34)
                    {
                        ushort q13052 = GetPlayerQuestState(player, 13052);
                        if (q13052 == 1 && !hasRoca)
                        {
                            return true; // Mourning at grave during Quest 13052 InProgress
                        }
                        return false;
                    }
                    if (clickId == 36)
                    {
                        return false; // Staged standing cutscene actor, triggered dynamically via Event 46
                    }

                    // Village Roca (32)
                    if (clickId == 32)
                    {
                        if (hasRoca) return false;
                        return true;
                    }

                    // Lina's Shiba Inus:
                    // ClickID 29 (X=624, Y=1259) is Lina's permanent dog sitting by her side. ALWAYS visible.
                    if (clickId == 29)
                    {
                        return true;
                    }

                    // ClickID 28 (X=592, Y=1192) is Lina's lost dog that went missing (Event 35, PreEvents #5 & #6).
                    // Hidden while Quest 13046 is NotStarted (2) OR InProgress at Step 1 (still missing in hogpen).
                    // Appears next to Lina only when Quest 13046 is found (Step 2) or Completed (3) or flag 13047 is set.
                    if (clickId == 28)
                    {
                        ushort qSt = GetPlayerQuestState(player, 13046);
                        byte qStep = 0;
                        if (player?.Quests != null && player.Quests.TryGetValue(13046, out var pq)) qStep = (byte)pq.Step;
                        bool qDone = GetPlayerQuestState(player, 13047) == 1;
                        if (qSt == 2 || (qSt == 1 && qStep < 2 && !qDone))
                        {
                            return false; // Dog is lost!
                        }
                        return true;
                    }

                    // Lost Shiba Inu in trees/hogpen (20)
                    if (clickId == 20)
                    {
                        ushort qSt = GetPlayerQuestState(player, 13046);
                        byte qStep = 0;
                        if (player?.Quests != null && player.Quests.TryGetValue(13046, out var pq)) qStep = (byte)pq.Step;
                        bool qDone = GetPlayerQuestState(player, 13047) == 1;
                        if (qSt == 1 && qStep < 2 && !qDone)
                        {
                            return true; // Only visible when Quest 13046 is InProgress at Step 1 (searching for dog)
                        }
                        return false; // Hidden when unstarted, dog found, or quest completed
                    }

                    // Baby Bees (18 & 19)
                    if (clickId == 18 || clickId == 19)
                    {
                        if (GetPlayerQuestState(player, 13023) == 2)
                        {
                            return false;
                        }
                        return true;
                    }

                    // Staged Pigs (14 & 15):
                    // PreEvents #0 & #1:
                    // Pig 15 is the stray pig outside the pen (visible during Step 1).
                    // Pig 14 is the pig returned to the pen (visible during Step 2 and Completed).
                    if (clickId == 14)
                    {
                        ushort q12020St = GetPlayerQuestState(player, 12020);
                        byte q12020Step = 0;
                        if (player?.Quests != null && player.Quests.TryGetValue(12020, out var pq12020)) q12020Step = (byte)pq12020.Step;
                        bool q12021Done = GetPlayerQuestState(player, 12021) == 1;
                        if ((q12020St == 1 && q12020Step >= 2) || q12020St == 3 || q12021Done)
                        {
                            return true;
                        }
                        return false;
                    }
                    if (clickId == 15)
                    {
                        ushort q12020St = GetPlayerQuestState(player, 12020);
                        byte q12020Step = 0;
                        if (player?.Quests != null && player.Quests.TryGetValue(12020, out var pq12020)) q12020Step = (byte)pq12020.Step;
                        if (q12020St == 1 && q12020Step == 1)
                        {
                            return true;
                        }
                        return false;
                    }

                    // Staged Pig (16)
                    if (clickId == 16)
                    {
                        if (GetPlayerQuestState(player, 13020) == 2 || GetPlayerQuestState(player, 13021) == 1)
                        {
                            return false;
                        }
                        return true;
                    }
                }

                if (eveDat == null || mapData?.PreEvents == null || mapData.PreEvents.Count == 0) return true;

                foreach (var preEvent in mapData.PreEvents)
                {
                    if (preEvent.subentry1 == null || preEvent.subentry1.Count == 0) continue;

                    foreach (var sub in preEvent.subentry1)
                    {
                        if (sub.unknown == null || sub.unknown.Count < 7) continue;

                        byte[] condData = sub.unknown.ToArray();
                        if (EvaluateConditionBlock(player, condData))
                        {
                            if (sub.subentry2 != null && sub.subentry2.Count > 0)
                            {
                                bool targetsThisNpc = false;
                                foreach (var act in sub.subentry2)
                                {
                                    if (act.unknown != null && act.unknown.Count >= 10 && act.unknown[0] == 0x02)
                                    {
                                        ushort targetClickId = BitConverter.ToUInt16(act.unknown.ToArray(), 1);
                                        if (targetClickId == clickId)
                                        {
                                            targetsThisNpc = true;
                                            byte s1 = act.unknown[8];
                                            byte s2 = act.unknown[9];

                                            if (s1 == 0xFF && s2 == 0xFF)
                                            {
                                                return false; // Action specifies complete concealment / despawn frame (FF FF)
                                            }
                                            else
                                            {
                                                return true; // Visible with custom state/animation
                                            }
                                        }
                                    }
                                }

                                if (targetsThisNpc)
                                {
                                    break; // First matching rule targeting this specific NPC applies for this PreEvent
                                }
                            }
                            else if (preEvent.clickID == clickId && mapId != 12000)
                            {
                                // Sub matched but has no action data (sub2_count=0).
                                // The parent PreEvent's clickID is the implicit hide target (only on non-12000 maps).
                                return false;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[PreEventInterpreter] Error checking visibility for map {mapId}, click {clickId}: {ex.Message}");
            }

            return true;
        }

        /// <summary>
        /// Evaluates a single bytecode condition block from eve.Emg PreEvents.
        /// In official Wonderland Online Eve files, quest conditions (op 5) define:
        /// Chunk 0: flagId (offset 1), reqState (offset 3; 1: InProgress, 2: NotStarted, 3: Completed), compType (offset 5)
        /// Chunk 7: reqStep (offset 8), stepCompType (offset 12)
        /// </summary>
        private static bool EvaluateConditionBlock(Player player, byte[] data)
        {
            if (data == null || data.Length == 0) return true;

            ushort activeFlagId = 0;

            // Iterate over all 7-byte condition chunks in the 21-byte condition buffer
            for (int offset = 0; offset + 7 <= data.Length; offset += 7)
            {
                byte op = data[offset];
                if (op == 0x00) break; // End of condition chunks

                // Opcode 0x05: Quest Mark / Flag Condition
                if (op == 0x05)
                {
                    if (offset == 0)
                    {
                        activeFlagId = BitConverter.ToUInt16(data, offset + 1);
                        ushort reqState = BitConverter.ToUInt16(data, offset + 3);
                        ushort compType = BitConverter.ToUInt16(data, offset + 5);

                        ushort playerState = GetPlayerQuestState(player, activeFlagId);

                        bool chunkMatch = false;
                        switch (compType)
                        {
                            case 1: chunkMatch = (playerState == reqState); break;
                            case 2: chunkMatch = (playerState >= reqState); break;
                            case 3: chunkMatch = (playerState <= reqState); break;
                            case 4: chunkMatch = (playerState != reqState); break;
                            default: chunkMatch = (playerState == reqState); break;
                        }

                        if (!chunkMatch) return false;
                    }
                    else if (offset == 7)
                    {
                        // Chunk 7: Step condition for InProgress quest
                        ushort reqStep = BitConverter.ToUInt16(data, offset + 1);
                        ushort stepComp = BitConverter.ToUInt16(data, offset + 5);

                        if (reqStep > 0 && activeFlagId > 0)
                        {
                            byte playerStep = 0;
                            if (player?.Quests != null && player.Quests.TryGetValue(activeFlagId, out var pq) && pq.State == QuestState.InProgress)
                            {
                                playerStep = (byte)Math.Max(1, (int)pq.Step);
                            }

                            bool stepMatch = false;
                            switch (stepComp)
                            {
                                case 1: stepMatch = (playerStep == reqStep); break;
                                case 2: stepMatch = (playerStep >= reqStep); break;
                                case 3: stepMatch = (playerStep <= reqStep); break;
                                case 4: stepMatch = (playerStep != reqStep); break;
                                default: stepMatch = (playerStep == reqStep); break;
                            }

                            if (!stepMatch) return false;
                        }
                    }
                }
                // Opcode 0x01: Unconditional / Always True
                else if (op == 0x01)
                {
                    continue;
                }
                // Opcode 0x02: Companion / Pet Recruitment Check
                else if (op == 0x02)
                {
                    ushort subType = BitConverter.ToUInt16(data, offset + 1);
                    ushort count = BitConverter.ToUInt16(data, offset + 3);
                    ushort petId = BitConverter.ToUInt16(data, offset + 5);

                    if (subType == 2 && petId > 0)
                    {
                        if (player == null) return false;
                        // Check if player has recruited this pet
                        bool hasPet = (player.PlayerPets != null && player.PlayerPets.Values.Any(p => p.PetID == petId || (petId == 12178 && p.PetID == 12032) || (petId == 12032 && p.PetID == 12178)))
                                   || player.ActivePetID == petId
                                   || (petId == 17162 && player.HasRecruitedCompanion("S.Monkey", 17162))
                                   || player.HasRecruitedCompanion(petId);

                        if (!hasPet) return false;
                    }
                }
            }

            return true;
        }

        /// <summary>
        /// Sends authentic actor hide packets (AC 22:10 actor despawn frame and AC 22:11 scene isolation frame).
        /// Note: AC 22:4 must strictly NEVER be sent dynamically at runtime, as the client replaces entity slot 0 with length/14 records.
        /// </summary>
        public static void SendActorHide(Player player, ushort clickId)
        {
            if (player == null) return;
            player.Send(Tools.FromFormat("bbwbb", 22, 10, clickId, (byte)0xFF, (byte)0xFF));
            player.Send(Tools.FromFormat("bbwbb", 22, 11, clickId, (byte)0xFF, (byte)0xFF));
        }

        /// <summary>
        /// Sends authentic actor show packets (AC 22:10 actor spawn/reveal frame and AC 22:11 scene isolation frame).
        /// Note: AC 22:4 must strictly NEVER be sent dynamically at runtime, as the client replaces entity slot 0 with length/14 records.
        /// </summary>
        public static void SendActorShow(Player player, ushort clickId)
        {
            if (player == null) return;
            player.Send(Tools.FromFormat("bbwbb", 22, 10, clickId, (byte)0x00, (byte)0xFF));
            player.Send(Tools.FromFormat("bbwbb", 22, 11, clickId, (byte)0x00, (byte)0xFF));
        }

        /// <summary>
        /// Sends authentic prop hide packet (AC 22:10 and AC 22:11 scene isolation frames).
        /// Note: AC 22:4 must strictly NEVER be sent dynamically at runtime, as the client replaces entity slot 0 with length/14 records.
        /// </summary>
        public static void SendPropHide(Player player, ushort clickId)
        {
            if (player == null) return;
            player.Send(Tools.FromFormat("bbwbb", 22, 10, clickId, (byte)0xFF, (byte)0xFF));
            player.Send(Tools.FromFormat("bbwbb", 22, 11, clickId, (byte)0xFF, (byte)0xFF));
        }

        /// <summary>
        /// Executes a single bytecode action block from eve.Emg PreEvents.
        /// </summary>
        private static void ExecuteActionBlock(Player player, ushort mapId, byte[] data)
        {
            if (player == null || data == null || data.Length < 10) return;

            byte actionOp = data[0];

            // Opcode 0x02: Actor Visibility / State Control
            if (actionOp == 0x02)
            {
                ushort clickId = BitConverter.ToUInt16(data, 1);
                ushort actionType = BitConverter.ToUInt16(data, 3);
                byte state1 = data[8];
                byte state2 = data[9];

                // Guideposts (10, 31) and Honeycomb (17) on Map 12000 are permanent scenery props
                if (mapId == 12000 && (clickId == 17 || clickId == 10 || clickId == 31))
                {
                    return;
                }

                if (state1 == 0xFF && state2 == 0xFF)
                {
                    // Action specifies complete concealment / despawn frame (FF FF)
                    SendActorHide(player, clickId);
                }
                else
                {
                    // Standard actor state / animation frame update (AC 22:10)
                    player.Send(Tools.FromFormat("bbwbb", 22, 10, clickId, state1, state2));
                }
            }
        }

        /// <summary>
        /// Retrieves the player's quest lifecycle state matching authentic WLO Eve.emg bytecode:
        /// 1 = InProgress
        /// 2 = NotStarted (default for unaccepted / unstarted quests)
        /// 3 = Completed
        /// </summary>
        private static ushort GetPlayerQuestState(Player player, ushort flagId)
        {
            if (player?.Quests == null || flagId == 0) return 2; // Default unstarted quest is 2 (NotStarted)

            if (player.Quests.TryGetValue(flagId, out var pq))
            {
                switch (pq.State)
                {
                    case QuestState.InProgress:
                        return 1;
                    case QuestState.NotStarted:
                        return 2;
                    case QuestState.Completed:
                        return 3;
                    default:
                        return 2;
                }
            }

            return 2; // Unregistered quest is NotStarted
        }
    }
}
