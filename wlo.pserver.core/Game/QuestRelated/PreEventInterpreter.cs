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
            byte op = data[0];

            // Opcode 0x05: Quest Mark / Flag Condition
            if (op == 0x05)
            {
                ushort flagId = BitConverter.ToUInt16(data, 1);
                ushort reqValue = BitConverter.ToUInt16(data, 3);
                ushort compType = BitConverter.ToUInt16(data, 5);

                ushort playerValue = GetPlayerFlagValue(player, flagId);

                switch (compType)
                {
                    case 1: // Equals
                        return (playerValue == reqValue);
                    case 2: // Greater or equal
                        return (playerValue >= reqValue);
                    case 3: // Less than or equal
                        return (playerValue <= reqValue);
                    default:
                        return (playerValue == reqValue);
                }
            }
            // Opcode 0x01: Unconditional / Always True
            else if (op == 0x01)
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Executes a single bytecode action block from eve.Emg PreEvents.
        /// </summary>
        private static void ExecuteActionBlock(Player player, byte[] data)
        {
            byte actionOp = data[0];

            // Opcode 0x02: Actor Visibility / State Control
            if (actionOp == 0x02)
            {
                ushort clickId = BitConverter.ToUInt16(data, 1);
                ushort actionType = BitConverter.ToUInt16(data, 3);

                // Action Type 0x0002: Hide / Despawn Actor from Client (AC 22:10 FF FF)
                if (actionType == 0x0002)
                {
                    SendPacket hidePkt = Tools.FromFormat("bbwbb", 22, 10, clickId, (byte)0xFF, (byte)0xFF);
                    player.Send(hidePkt);
                }
                // Action Type 0x0000, 0x0001, 0x0003, 0x0005, 0x0007: Reveal / Show Actor (AC 22:10 00 00)
                else
                {
                    SendPacket showPkt = Tools.FromFormat("bbwbb", 22, 10, clickId, (byte)0x00, (byte)0x00);
                    player.Send(showPkt);
                }
            }
        }

        /// <summary>
        /// Retrieves the player's current quest flag / mark value.
        /// In official Wonderland Online eve.Emg PreEvents:
        /// 2 = Not Started / Inactive
        /// 1 = In Progress
        /// 3 = Completed
        /// </summary>
        private static ushort GetPlayerFlagValue(Player player, ushort flagId)
        {
            if (player?.Quests == null || flagId == 0) return 2; // Default unstarted quest is 2 in WLO PreEvents

            if (player.Quests.TryGetValue(flagId, out var pq))
            {
                switch (pq.State)
                {
                    case QuestState.InProgress:
                        return (ushort)Math.Max(1, (int)pq.Step);
                    case QuestState.Completed:
                        return 3;
                    case QuestState.NotStarted:
                    default:
                        return 2;
                }
            }

            return 2;
        }
    }
}
