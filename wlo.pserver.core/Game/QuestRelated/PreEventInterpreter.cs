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
                if (mapData?.PreEvents == null || mapData.PreEvents.Count == 0) return;

                foreach (var preEvent in mapData.PreEvents)
                {
                    if (preEvent.subentry1 == null || preEvent.subentry1.Count == 0) continue;

                    foreach (var sub in preEvent.subentry1)
                    {
                        if (sub.unknown == null || sub.unknown.Count < 7) continue;

                        byte[] condData = sub.unknown.ToArray();
                        if (EvaluateConditionBlock(player, condData))
                        {
                            // Condition matched! Execute action blocks in subentry2
                            if (sub.subentry2 != null)
                            {
                                foreach (var act in sub.subentry2)
                                {
                                    if (act.unknown != null && act.unknown.Count >= 5)
                                    {
                                        ExecuteActionBlock(player, act.unknown.ToArray());
                                    }
                                }
                            }
                            break; // First valid branch matched for this PreEvent
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
            if (player == null) return true;

            try
            {
                var eveDat = DataBase.GameDataBase.GlobalInstance?.EveDat;
                if (eveDat == null) return true;

                var mapData = eveDat.GetMapData(mapId);
                if (mapData?.PreEvents == null || mapData.PreEvents.Count == 0) return true;

                foreach (var preEvent in mapData.PreEvents)
                {
                    if (preEvent.subentry1 == null || preEvent.subentry1.Count == 0) continue;

                    foreach (var sub in preEvent.subentry1)
                    {
                        if (sub.unknown == null || sub.unknown.Count < 7) continue;

                        byte[] condData = sub.unknown.ToArray();
                        if (EvaluateConditionBlock(player, condData))
                        {
                            if (sub.subentry2 != null)
                            {
                                foreach (var act in sub.subentry2)
                                {
                                    if (act.unknown != null && act.unknown.Count >= 10 && act.unknown[0] == 0x02)
                                    {
                                        ushort targetClickId = BitConverter.ToUInt16(act.unknown.ToArray(), 1);
                                        byte state1 = act.unknown[8];
                                        byte state2 = act.unknown[9];

                                        if (targetClickId == clickId && state1 == 0xFF && state2 == 0xFF)
                                        {
                                            return false; // Dynamic PreEvent dictates NPC is hidden
                                        }
                                    }
                                }
                            }
                            break; // First matching branch for this PreEvent applies
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
        /// Executes a single bytecode action block from eve.Emg PreEvents.
        /// </summary>
        private static void ExecuteActionBlock(Player player, byte[] data)
        {
            if (player == null || data == null || data.Length < 10) return;

            byte actionOp = data[0];

            // Opcode 0x02: Actor Visibility / State Control
            if (actionOp == 0x02)
            {
                ushort clickId = BitConverter.ToUInt16(data, 1);
                ushort p1 = BitConverter.ToUInt16(data, 3);
                byte state1 = data[8];
                byte state2 = data[9];

                // Send AC 22:10 (standard actor state update)
                SendPacket actPkt10 = Tools.FromFormat("bbwbb", 22, 10, clickId, state1, state2);
                player.Send(actPkt10);

                // Send AC 22:11 (authentic PreEvent scene isolate hide packet)
                if (p1 == 3 || (state1 == 0xFF && state2 == 0xFF))
                {
                    SendPacket actPkt11 = Tools.FromFormat("bbwbb", 22, 11, clickId, state1, state2);
                    player.Send(actPkt11);
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
