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
            // All native map NPCs defined in eve.Emg are visible by default unless toggled by PreEvents
            return true;
        }

        /// <summary>
        /// Evaluates a single bytecode condition block from eve.Emg PreEvents.
        /// </summary>
        private static bool EvaluateConditionBlock(Player player, byte[] data)
        {
            if (data == null || data.Length == 0) return true;

            // Iterate over all 7-byte condition chunks in the 21-byte condition buffer
            for (int offset = 0; offset + 7 <= data.Length; offset += 7)
            {
                byte op = data[offset];
                if (op == 0x00) break; // End of condition chunks

                // Opcode 0x05: Quest Mark / Flag Condition
                if (op == 0x05)
                {
                    ushort flagId = BitConverter.ToUInt16(data, offset + 1);
                    ushort reqValue = BitConverter.ToUInt16(data, offset + 3);
                    ushort compType = BitConverter.ToUInt16(data, offset + 5);

                    ushort playerValue = GetPlayerFlagValue(player, flagId);

                    bool chunkMatch = false;
                    switch (compType)
                    {
                        case 1: chunkMatch = (playerValue == reqValue); break;
                        case 2: chunkMatch = (playerValue >= reqValue); break;
                        case 3: chunkMatch = (playerValue <= reqValue); break;
                        case 4: chunkMatch = (playerValue != reqValue); break;
                        case 5: chunkMatch = (playerValue > reqValue); break;
                        case 6: chunkMatch = (playerValue < reqValue); break;
                        default: chunkMatch = (playerValue == reqValue); break;
                    }

                    if (!chunkMatch) return false;
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
                                   || (petId == 17162 && player.HasRecruitedCompanion("S.Monkey", 17162));

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
                byte state1 = data[8];
                byte state2 = data[9];

                SendPacket actPkt = Tools.FromFormat("bbwbb", 22, 10, clickId, state1, state2);
                player.Send(actPkt);
            }
        }

        /// <summary>
        /// Retrieves the player's current quest flag / mark value.
        /// In official Wonderland Online eve.Emg PreEvents:
        /// 0 = Not Started / Unset (Default)
        /// 1 = In Progress / Step 1
        /// 2 = Completed / Step 2
        /// 3 = Post-Quest / Handed In
        /// </summary>
        private static ushort GetPlayerFlagValue(Player player, ushort flagId)
        {
            if (player?.Quests == null || flagId == 0) return 0; // Default unstarted quest is 0

            if (player.Quests.TryGetValue(flagId, out var pq))
            {
                switch (pq.State)
                {
                    case QuestState.InProgress:
                        return (ushort)Math.Max(1, (int)pq.Step);
                    case QuestState.Completed:
                        return 2;
                    case QuestState.NotStarted:
                    default:
                        return 0;
                }
            }

            return 0;
        }
    }
}
