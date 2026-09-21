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

                // 5. Add Map 12000 & 12001 staged actors
                if (mapId == 12000)
                {
                    ushort[] map12000Actors = { 10, 14, 15, 16, 17, 18, 19, 20, 28, 29, 31, 32, 33, 34, 35, 36 };
                    foreach (var c in map12000Actors) allClickIds.Add(c);
                }
                else if (mapId == 12001)
                {
                    ushort[] map12001Actors = { 1, 2, 3 };
                    foreach (var c in map12001Actors) allClickIds.Add(c);
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
                        if (preEvent.subentry1 == null || preEvent.subentry1.Count == 0) continue;

                        var rules = GroupSubEntriesIntoRules(preEvent);
                        foreach (var rule in rules)
                        {
                            bool allMatch = true;
                            foreach (var cond in rule.Conditions)
                            {
                                if (!EvaluateConditionBlock(player, cond))
                                {
                                    allMatch = false;
                                    break;
                                }
                            }

                            if (allMatch && rule.Actions != null && rule.Actions.Count > 0)
                            {
                                foreach (var act in rule.Actions)
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
                if (player.CurMap is GameMap markerMap && markerMap.MapID == mapId)
                    Game.Maps.EveEventInterpreter.SyncQuestMinimapMarkers(player, markerMap, force);
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
                var mapNpc = player.CurMap?.MapID == mapId
                    ? (player.CurMap as GameMap)?.NpcList?.OfType<Game.Maps.QuestNpc>().FirstOrDefault(n => n.CickID == clickId)
                    : null;
                if (mapNpc != null && mapNpc.IsBroken && mapNpc.IsWildMonster()) return false;

                // Permanent guideposts (10, 31) and honeycomb (17) on Map 12000 are always visible initially
                if (mapId == 12000 && (clickId == 17 || clickId == 10 || clickId == 31))
                {
                    return true;
                }

                // Check if this map entity is a companion already recruited by the player
                var eveDat = DataBase.GameDataBase.GlobalInstance?.EveDat;
                var mapData = eveDat?.GetMapData(mapId);
                var npcDef = mapData?.Npclist?.FirstOrDefault(n => n.clickId == clickId);

                if (npcDef != null && npcDef.npcId > 0 && player.HasStoryCompanionInParty((ushort)npcDef.npcId))
                {
                    return false;
                }

                // Map 11016 (North Island Starter Beach): S. Monkey (ClickID 1, TID 17162)
                if (mapId == 11016 && clickId == 1)
                {
                    bool hasMonkey = (player.PlayerPets != null && player.PlayerPets.Values.Any(pet => pet != null && (pet.PetID == 17162 || pet.PetID == 10727))) ||
                                     player.ActivePetID == 17162 || player.ActivePetID == 10727 ||
                                     player.HasRecruitedCompanion("S.Monkey", 17162) ||
                                     (player.Quests != null && player.Quests.TryGetValue(12002, out var mq) && mq.State == QuestState.Completed);

                    if (hasMonkey) return false;
                    return true;
                }

                // Roca's house/grave actors follow native PreEvents below. Hardcoded
                // "always visible/hidden" rules bypassed her return and death stages.

                // Map 12000 (Kelan Village): Exact lifecycle visibility rules
                if (mapId == 12000)
                {
                    // Staged Cutscene Roca at village gate (ClickID 32):
                    // Ephemeral cutscene actor dynamically spawned during Event 45/49 (Quest 13098 step 3)
                    // and hidden immediately when dialogue finishes. Never visible by default on the map.
                    if (clickId == 32)
                    {
                        return false;
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

                // EVE's first NPC flag byte uses bit 0 for initial visibility.
                // A staged actor can have its own click events; duplicate-name heuristics
                // cannot determine whether it belongs to the current quest phase.
                bool visible = npcDef == null || (npcDef.unknownbyte1 & 1) != 0;
                if (mapData?.PreEvents == null) return visible;

                foreach (var preEvent in mapData.PreEvents)
                {
                    foreach (var rule in GroupSubEntriesIntoRules(preEvent))
                    {
                        if (!rule.Conditions.All(cond => EvaluateConditionBlock(player, cond))) continue;
                        if (rule.Actions != null)
                        {
                            foreach (var action in rule.Actions)
                            {
                                var data = action.unknown;
                                if (data == null || data.Count < 5 || data[0] != 2) continue;
                                var bytes = data.ToArray();
                                if (BitConverter.ToUInt16(bytes, 1) != clickId) continue;
                                ushort actionType = BitConverter.ToUInt16(bytes, 3);
                                if (actionType == 2) visible = false;
                                else if (actionType == 3) visible = true;
                            }
                        }
                        // PreEvent rules are cumulative. Apply every matching rule in
                        // file order, including hide/show pairs targeting the same actor.
                    }
                }
                // Event 60002:51 explicitly shows the standing pair when they leave
                // the party. Its high-amity branch also sets CG eligibility mark 50046,
                // which makes the map-entry PreEvent omit them. Preserve the authored
                // departure scene, then restore the shore pair after the CG while
                // the original companions are still waiting to return.
                if (mapId == 60002 && (clickId == 43 || clickId == 44) &&
                    player.Quests != null && player.Quests.TryGetValue(50042, out var spring) &&
                    spring.State == QuestState.InProgress &&
                    (spring.Step == 1 || (spring.Step > 1 && GetPlayerQuestState(player, 50047) == 1)))
                    return true;
                if (mapId == 12002 && (clickId == 2 || clickId == 7) && QuestManager.IsXaolanInFate(player))
                    return false;
                // Rescue and Unknown Fate are separate stages. The first rescue clears
                // actors 2/3 before the replacement actors 6/7 have an active story mark.
                // Keep the original farewell speakers until that next stage starts.
                if (mapId == 12002 && (clickId == 2 || clickId == 3) &&
                    player.Quests != null && player.Quests.TryGetValue(13033, out var rescue) &&
                    rescue.State == QuestState.InProgress && rescue.Step == 1 &&
                    GetPlayerQuestState(player, 13004) != 1 && GetPlayerQuestState(player, 13005) != 1)
                    return clickId != 2 || !player.HasStoryCompanionInParty(14156);
                return visible;
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[PreEventInterpreter] Error checking visibility for map {mapId}, click {clickId}: {ex.Message}");
            }

            return true;
        }

        // A PreEvent condition is one 21-byte record, not three independent chunks.
        private static bool EvaluateConditionBlock(Player player, byte[] data)
        {
            return Game.Maps.EveEventInterpreter.MatchesPreEventCondition(player, data);
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

            // Record: [ClickID:w, Frame:w (0 for props, 0x00FF for animated actors), X:w, Y:w, EntityType:b (1), Duration:d (0), StateFlag:b (0)] (14 bytes)
            SendPacket p = new SendPacket();
            p.Pack8(22);
            p.Pack8(4);
            p.Pack16(clickId);
            var npc = (player.CurMap as GameMap)?.NpcList?.OfType<Game.Maps.QuestNpc>()
                .FirstOrDefault(n => n.CickID == clickId);
            uint templateId = npc?.TemplateID ?? DataBase.GameDataBase.GlobalInstance?.EveDat?
                .GetMapData((ushort)player.MapID)?.Npclist?.FirstOrDefault(n => n.clickId == clickId)?.npcId ?? 0;
            p.Pack16(Game.Maps.QuestNpc.GetIdleAnimationFrame(templateId));
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
        // Client AC22:9 -> 0x4464dc -> 0x430f90: actor, animation, facing.
        internal static bool SendActorPose(Player player, ushort clickId, ushort animation, byte facing)
        {
            if (animation > byte.MaxValue) return false;
            player.Send(Tools.FromFormat("bbwbb", 22, 9, clickId, (byte)animation, facing));
            return true;
        }

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

                if (actionType == 7)
                {
                    // EVE action 2/7 specifies the actor animation in word 3,
                    // with facing in the packed value; it is not a prop frame.
                    SendActorPose(player, clickId, BitConverter.ToUInt16(data, 5), state1);
                }
                else if (actionType == 2)
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

        // Rebuild authored icons from current marks on every map/quest refresh.
        // Declared but inactive targets clear to zero; unrelated NPCs use the derived fallback.
        internal static Dictionary<uint, byte> GetNativeMinimapMarkers(Player player, ushort mapId)
        {
            var markers = new Dictionary<uint, byte>();
            var preEvents = DataBase.GameDataBase.GlobalInstance?.EveDat?.GetMapData(mapId)?.PreEvents;
            if (preEvents == null) return markers;
            foreach (var preEvent in preEvents)
                foreach (var sub in preEvent.subentry1)
                    foreach (var action in sub.subentry2)
                    {
                        var raw = action.unknown;
                        if (raw == null || raw.Count < 7 || raw[0] != 13) continue;
                        byte[] bytes = raw.ToArray();
                        ushort kind = BitConverter.ToUInt16(bytes, 1);
                        if (kind >= 1 && kind <= 3)
                            markers[((uint)(kind - 1) << 16) | BitConverter.ToUInt16(bytes, 3)] = 0;
                    }
            foreach (var preEvent in preEvents)
                foreach (var rule in GroupSubEntriesIntoRules(preEvent))
                {
                    if (!rule.Conditions.All(c => EvaluateConditionBlock(player, c))) continue;
                    foreach (var action in rule.Actions)
                    {
                        var raw = action.unknown;
                        if (raw == null || raw.Count < 7 || raw[0] != 13) continue;
                        byte[] bytes = raw.ToArray();
                        ushort kind = BitConverter.ToUInt16(bytes, 1);
                        ushort icon = BitConverter.ToUInt16(bytes, 5);
                        if (kind >= 1 && kind <= 3 && icon >= 1 && icon <= 256)
                            markers[((uint)(kind - 1) << 16) | BitConverter.ToUInt16(bytes, 3)] = (byte)(icon - 1);
                    }
                }
            return markers;
        }

        private class PreEventRule
        {
            public List<byte[]> Conditions { get; } = new List<byte[]>();
            public List<preEventSubSubEntry> Actions { get; set; } = new List<preEventSubSubEntry>();
            public ushort ParentClickId { get; set; }
        }

        private static List<PreEventRule> GroupSubEntriesIntoRules(preEventEntries preEvent)
        {
            var rules = new List<PreEventRule>();
            if (preEvent?.subentry1 == null || preEvent.subentry1.Count == 0) return rules;

            PreEventRule currentRule = null;
            foreach (var sub in preEvent.subentry1)
            {
                if (sub.unknown == null || sub.unknown.Count < 7) continue;

                bool hasActions = sub.subentry2 != null && sub.subentry2.Count > 0;
                if (hasActions || currentRule == null)
                {
                    currentRule = new PreEventRule
                    {
                        ParentClickId = preEvent.clickID
                    };
                    currentRule.Conditions.Add(sub.unknown.ToArray());
                    if (hasActions)
                    {
                        currentRule.Actions = sub.subentry2;
                    }
                    rules.Add(currentRule);
                }
                else
                {
                    currentRule.Conditions.Add(sub.unknown.ToArray());
                }
            }

            return rules;
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
