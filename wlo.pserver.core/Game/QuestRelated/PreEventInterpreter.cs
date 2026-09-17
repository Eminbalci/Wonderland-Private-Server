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
        /// Evaluates all PreEvents and quest stage conditions for the target map against the player's current marks/flags.
        /// Symmetrically dispatches SendActorShow or SendActorHide based on ShouldNpcBeVisible.
        /// </summary>
        public static void EvaluateMapPreEvents(Player player, ushort mapId, bool force = false)
        {
            if (player == null) return;

            try
            {
                var eveDat = DataBase.GameDataBase.GlobalInstance?.EveDat;
                if (eveDat == null) return;

                var mapData = eveDat.GetMapData(mapId);
                if (mapData == null) return;

                HashSet<ushort> allClickIds = new HashSet<ushort>();

                // 1. Gather all click IDs defined in mapData.Npclist
                if (mapData.Npclist != null)
                {
                    foreach (var npc in mapData.Npclist)
                    {
                        allClickIds.Add((ushort)npc.clickId);
                    }
                }

                // 2. Gather all click IDs from player's current map entities if available
                if (player.CurMap is GameMap gmap && gmap.NpcList != null)
                {
                    foreach (var mapNpc in gmap.NpcList)
                    {
                        allClickIds.Add((ushort)mapNpc.CickID);
                    }
                }

                // 3. Gather all target click IDs referenced in PreEvents
                if (mapData.PreEvents != null)
                {
                    foreach (var pe in mapData.PreEvents)
                    {
                        if (pe.clickID > 0) allClickIds.Add(pe.clickID);
                        if (pe.subentry1 != null)
                        {
                            foreach (var sub in pe.subentry1)
                            {
                                if (sub.subentry2 != null)
                                {
                                    foreach (var act in sub.subentry2)
                                    {
                                        if (act.unknown != null && act.unknown.Count >= 3 && act.unknown[0] == 0x02)
                                        {
                                            ushort tClick = BitConverter.ToUInt16(act.unknown.ToArray(), 1);
                                            if (tClick > 0) allClickIds.Add(tClick);
                                        }
                                    }
                                }
                            }
                        }
                    }
                }

                // 4. Gather any currently hidden NPCs for this player
                if (player.HiddenNpcClickIDs != null)
                {
                    foreach (var hid in player.HiddenNpcClickIDs)
                    {
                        allClickIds.Add(hid);
                    }
                }

                // 5. Add Map 12000 staged actors
                if (mapId == 12000)
                {
                    ushort[] map12000Actors = { 10, 14, 15, 16, 17, 18, 19, 20, 28, 29, 31, 32, 33, 34, 35, 36 };
                    foreach (var c in map12000Actors) allClickIds.Add(c);
                }

                // 6. Add any registered quest spawn/despawn click IDs for this map
                if (QuestManager.AllQuests != null)
                {
                    foreach (var q in QuestManager.AllQuests.Values)
                    {
                        if (q.MapID == mapId)
                        {
                            if (q.DespawnNpcClickIDs != null)
                                foreach (var c in q.DespawnNpcClickIDs) allClickIds.Add(c);
                            if (q.SpawnNpcClickIDs != null)
                                foreach (var c in q.SpawnNpcClickIDs) allClickIds.Add(c);
                            if (q.Steps != null)
                            {
                                foreach (var st in q.Steps)
                                {
                                    if (st.DespawnNpcClickIDs != null)
                                        foreach (var c in st.DespawnNpcClickIDs) allClickIds.Add(c);
                                    if (st.SpawnNpcClickIDs != null)
                                        foreach (var c in st.SpawnNpcClickIDs) allClickIds.Add(c);
                                }
                            }
                        }
                    }
                }

                // Symmetrically evaluate and synchronize visibility for every entity
                foreach (var clickId in allClickIds)
                {
                    bool shouldBeVisible = ShouldNpcBeVisible(player, mapId, clickId);
                    bool isHidden = player.HiddenNpcClickIDs.Contains(clickId);

                    if (!shouldBeVisible)
                    {
                        if (force || !isHidden)
                        {
                            SendActorHide(player, clickId);
                        }
                    }
                    else
                    {
                        if (force || isHidden)
                        {
                            SendActorShow(player, clickId);
                        }
                    }
                }

                // Execute custom prop states (actionType 5 or non-FF animation frames)
                if (mapData.PreEvents != null)
                {
                    foreach (var preEvent in mapData.PreEvents)
                    {
                        if (preEvent.subentry1 == null) continue;
                        foreach (var sub in preEvent.subentry1)
                        {
                            if (sub.unknown == null || sub.unknown.Count < 7) continue;
                            if (EvaluateConditionBlock(player, sub.unknown.ToArray()))
                            {
                                if (sub.subentry2 != null)
                                {
                                    foreach (var act in sub.subentry2)
                                    {
                                        if (act.unknown != null && act.unknown.Count >= 10 && act.unknown[0] == 0x02)
                                        {
                                            ushort actType = BitConverter.ToUInt16(act.unknown.ToArray(), 3);
                                            byte s1 = act.unknown[8];
                                            byte s2 = act.unknown[9];
                                            if (actType == 5 || (s1 != 0xFF && s2 != 0xFF && actType != 2 && actType != 3))
                                            {
                                                ExecuteActionBlock(player, mapId, act.unknown.ToArray());
                                            }
                                        }
                                    }
                                }
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

                // Check registered QuestDefinition / QuestStep spawn and despawn lists
                if (QuestManager.AllQuests != null)
                {
                    foreach (var qDef in QuestManager.AllQuests.Values)
                    {
                        if (qDef.MapID != mapId) continue;

                        if (qDef.DespawnNpcClickIDs != null && qDef.DespawnNpcClickIDs.Contains(clickId))
                        {
                            if (player.Quests != null && player.Quests.TryGetValue(qDef.QuestID, out var pq) && pq.State == QuestState.Completed)
                            {
                                return false;
                            }
                        }

                        if (qDef.SpawnNpcClickIDs != null && qDef.SpawnNpcClickIDs.Contains(clickId))
                        {
                            bool questCompleted = player.Quests != null && player.Quests.TryGetValue(qDef.QuestID, out var pq) && pq.State == QuestState.Completed;
                            if (!questCompleted) return false;
                            return true;
                        }

                        if (qDef.Steps != null)
                        {
                            foreach (var step in qDef.Steps)
                            {
                                if (step.DespawnNpcClickIDs != null && step.DespawnNpcClickIDs.Contains(clickId))
                                {
                                    if (player.Quests != null && player.Quests.TryGetValue(qDef.QuestID, out var pq) && pq.State == QuestState.InProgress && pq.Step == step.StepIndex)
                                    {
                                        return false;
                                    }
                                }
                                if (step.SpawnNpcClickIDs != null && step.SpawnNpcClickIDs.Contains(clickId))
                                {
                                    bool stepActive = player.Quests != null && player.Quests.TryGetValue(qDef.QuestID, out var pq) && pq.State == QuestState.InProgress && pq.Step == step.StepIndex;
                                    if (!stepActive) return false;
                                    return true;
                                }
                            }
                        }
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
                                            ushort actionType = BitConverter.ToUInt16(act.unknown.ToArray(), 3);
                                            byte s1 = act.unknown[8];
                                            byte s2 = act.unknown[9];

                                            if (actionType == 2)
                                            {
                                                return false; // ActionType 2 is strictly Conceal / Hide
                                            }
                                            else if (actionType == 3)
                                            {
                                                return true; // ActionType 3 is strictly Reveal / Show
                                            }
                                            else if (s1 == 0xFF && s2 == 0xFF)
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
                // Opcode 0x03: Quest Step Condition (at offset 7) OR Inventory Item Check
                else if (op == 0x03)
                {
                    if (offset == 7 && activeFlagId > 0)
                    {
                        ushort reqStep = BitConverter.ToUInt16(data, offset + 1);
                        ushort stepComp = BitConverter.ToUInt16(data, offset + 5);

                        if (reqStep > 0)
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
                    else
                    {
                        ushort itemId = BitConverter.ToUInt16(data, offset + 1);
                        ushort count = BitConverter.ToUInt16(data, offset + 3);
                        ushort comp = BitConverter.ToUInt16(data, offset + 5);

                        if (itemId >= 10000)
                        {
                            int hasCount = player?.Inv?.GetItemCount(itemId) ?? 0;
                            bool itemMatch = false;
                            switch (comp)
                            {
                                case 1: itemMatch = (hasCount == count); break;
                                case 2: itemMatch = (hasCount >= count); break;
                                case 3: itemMatch = (hasCount <= count); break;
                                case 4: itemMatch = (hasCount != count); break;
                                default: itemMatch = (hasCount >= (count > 0 ? count : 1)); break;
                            }
                            if (!itemMatch) return false;
                        }
                    }
                }
            }

            return true;
        }

        private static void GetNpcCoordinates(Player player, ushort clickId, out ushort x, out ushort y)
        {
            x = 0;
            y = 0;
            if (player?.CurMap is GameMap gmap && gmap.NpcList != null)
            {
                var target = gmap.NpcList.FirstOrDefault(n => n.CickID == clickId);
                if (target != null)
                {
                    x = target.X;
                    y = target.Y;
                    return;
                }
            }

            var mapData = DataBase.GameDataBase.GlobalInstance?.EveDat?.GetMapData((ushort)(player?.MapID ?? 0));
            var def = mapData?.Npclist?.FirstOrDefault(n => n.clickId == clickId);
            if (def != null)
            {
                x = (ushort)def.x;
                y = (ushort)def.y;
            }
        }

        /// <summary>
        /// Sends authentic actor hide packets (AC 22:4 concealment frame with 0x03E7FC18 despawn code).
        /// Sets actor visibility field *(actor + 0x1eec) = 2 and clears map collision grid via FUN_0043d390.
        /// </summary>
        public static void SendActorHide(Player player, ushort clickId)
        {
            if (player == null) return;
            player.HiddenNpcClickIDs.Add(clickId);

            GetNpcCoordinates(player, clickId, out ushort x, out ushort y);

            // Record: [ClickID:w, State:w (0xFFFF), X:w, Y:w, EntityType:b (2), Duration:d (0x03E7FC18), StateFlag:b (0)] (14 bytes)
            SendPacket p = new SendPacket();
            p.Pack8(22);
            p.Pack8(4);
            p.Pack16(clickId);
            p.Pack16(0xFFFF);
            p.Pack16(x);
            p.Pack16(y);
            p.Pack8(2); // 2 = Hidden
            p.Pack32(0x03E7FC18); // Despawn & clear collision
            p.Pack8(0);
            player.Send(p);

            DebugSystem.Write($"[ActorVisibility] Sent SendActorHide (AC 22:4 despawn) for ClickID {clickId} at {x},{y} to {player.CharName}");
        }

        /// <summary>
        /// Sends authentic actor show packets (AC 22:4 reveal frame).
        /// Sets actor visibility field *(actor + 0x1eec) = 1 (visible) and updates world coordinates.
        /// </summary>
        public static void SendActorShow(Player player, ushort clickId)
        {
            if (player == null) return;
            player.HiddenNpcClickIDs.Remove(clickId);

            GetNpcCoordinates(player, clickId, out ushort x, out ushort y);

            // Record: [ClickID:w, State:w (0x00FF), X:w, Y:w, EntityType:b (1), Duration:d (0), StateFlag:b (0)] (14 bytes)
            SendPacket p = new SendPacket();
            p.Pack8(22);
            p.Pack8(4);
            p.Pack16(clickId);
            p.Pack16(0x00FF);
            p.Pack16(x);
            p.Pack16(y);
            p.Pack8(1); // 1 = Visible
            p.Pack32(0);
            p.Pack8(0);
            player.Send(p);

            DebugSystem.Write($"[ActorVisibility] Sent SendActorShow (AC 22:4 spawn) for ClickID {clickId} at {x},{y} to {player.CharName}");
        }

        /// <summary>
        /// Sends authentic prop hide packet (AC 22:4 concealment frame with 0x03E7FC18 despawn code).
        /// </summary>
        public static void SendPropHide(Player player, ushort clickId)
        {
            SendActorHide(player, clickId);
        }

        /// <summary>
        /// Sends authentic prop show packet (AC 22:4 reveal frame).
        /// </summary>
        public static void SendPropShow(Player player, ushort clickId)
        {
            SendActorShow(player, clickId);
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

                if (actionType == 2)
                {
                    SendActorHide(player, clickId);
                }
                else if (actionType == 3)
                {
                    SendActorShow(player, clickId);
                }
                else if (state1 == 0xFF && state2 == 0xFF)
                {
                    // Action specifies complete concealment / despawn frame (FF FF)
                    SendActorHide(player, clickId);
                }
                else
                {
                    // Standard actor state / animation frame update (AC 22:4)
                    GetNpcCoordinates(player, clickId, out ushort x, out ushort y);
                    SendPacket p = new SendPacket();
                    p.Pack8(22);
                    p.Pack8(4);
                    p.Pack16(clickId);
                    p.Pack8(state1);
                    p.Pack8(state2);
                    p.Pack16(x);
                    p.Pack16(y);
                    p.Pack8(1);
                    p.Pack32(0);
                    p.Pack8(0);
                    player.Send(p);
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
