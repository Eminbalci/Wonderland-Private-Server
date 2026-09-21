using System;
using System.Linq;
using System.IO;
using System.Collections.Generic;
using Game.DataFiles;
using Game.QuestRelated;
using Network;

namespace Game.Maps
{
    public static partial class EveEventInterpreter
    {
        private static readonly HashSet<uint> DisabledNativeEvents = LoadDisabledNativeEvents();

        private static HashSet<uint> LoadDisabledNativeEvents()
        {
            var result = new HashSet<uint>();
            string path = RCLibrary.Core.PathHelper.GetDataFilePath("disabled_quest_events.csv");
            if (!File.Exists(path)) return result;
            foreach (string line in File.ReadLines(path).Skip(1))
            {
                string[] fields = line.Split(',');
                ushort mapId, eventId;
                if (fields.Length >= 2 && ushort.TryParse(fields[0], out mapId) && ushort.TryParse(fields[1], out eventId))
                    result.Add(((uint)mapId << 16) | eventId);
            }
            return result;
        }

        internal static bool IsNativeEventDisabled(uint mapId, ushort eventId)
        {
            return DisabledNativeEvents.Contains((mapId << 16) | eventId);
        }

        private static bool RejectDisabledNativeEvent(Player player, GameMap map, EventsinMapEntries ev)
        {
            if (!IsNativeEventDisabled(map.MapID, ev.clickID)) return false;
            player.CancelInteraction();
            player.SendHeadBanner("This quest is disabled: required game data is unavailable.");
            DebugSystem.Write("[EveEventInterpreter] Disabled content: map=" + map.MapID + " event=" + ev.clickID);
            return true;
        }
        // Native AC22:12 selects Icon_Sign_<id>; icon 7 is the question mark.
        // Resolve the same first matching NPC event as interaction, without executing it.
        public static void SyncQuestMinimapMarkers(Player player, GameMap map, bool force = false)
        {
            if (player == null || map == null || player.CurMap != map) return;
            var data = DataBase.GameDataBase.GlobalInstance?.EveDat?.GetMapData((ushort)map.MapID);
            if (data?.Npclist == null) return;
            var nativeMarkers = PreEventInterpreter.GetNativeMinimapMarkers(player, (ushort)map.MapID);
            lock (player.SentQuestMinimapMarkers)
            {
                foreach (var npc in data.Npclist)
                {
                    byte icon;
                    bool native = nativeMarkers.TryGetValue(npc.clickId, out icon);
                    nativeMarkers.Remove(npc.clickId);
                    bool disabled = npc.Events.Count > 0 && npc.Events.All(id => IsNativeEventDisabled(map.MapID, id));
                    if (player.HiddenNpcClickIDs.Contains(npc.clickId) || disabled) icon = 0;
                    else
                    {
                        foreach (byte eventId in npc.Events)
                        {
                            var ev = data.Events.FirstOrDefault(e => e.clickID == eventId);
                            var branch = FindBranch(player, map, ev);
                            if (branch == null) continue;
                            if (IsNativeEventDisabled(map.MapID, ev.clickID)) icon = 0;
                            else if (!native && !IsRoamingBattleNpc((ushort)map.MapID, npc.clickId) &&
                                IsUnfinishedQuestBranch(player, ev, branch)) icon = 7;
                            break;
                        }
                    }
                    SendMinimapMarker(player, 1, npc.clickId, icon, force);
                }
                foreach (var marker in nativeMarkers)
                    SendMinimapMarker(player, (byte)((marker.Key >> 16) + 1), (ushort)marker.Key,
                        marker.Key < 65536 && player.HiddenNpcClickIDs.Contains((ushort)marker.Key) ? (byte)0 : marker.Value, force);
            }
        }

        internal static void SendMinimapMarker(Player player, byte kind, ushort clickId, byte icon, bool force = false)
        {
            uint key = ((uint)(kind - 1) << 16) | clickId;
            lock (player.SentQuestMinimapMarkers)
            {
                byte sent;
                if (!force && player.SentQuestMinimapMarkers.TryGetValue(key, out sent) && sent == icon) return;
                player.Send(Tools.FromFormat("bbbwb", 22, 12, kind, clickId, icon));
                player.SentQuestMinimapMarkers[key] = icon;
            }
        }

        private static bool IsUnfinishedQuestBranch(Player player, EventsinMapEntries ev, EventSubEntry branch)
        {
            bool mentionsActiveMark = false, hasActiveMark = false, hasCompletionMark = false;
            int index = ev.SubEntry.IndexOf(branch);
            int count = Math.Max(1, branch.unknownword6 >> 8);
            foreach (var condition in ev.SubEntry.Skip(index).Take(count))
            {
                ushort flag;
                if (condition.unknownbyte1 != 5 ||
                    !Game.PlayerRelated.NotebookManager.TryGetCompletionFlag(condition.unknownword1, out flag)) continue;
                PlayerQuest quest;
                bool present = player.Quests != null && player.Quests.TryGetValue(condition.unknownword1, out quest) &&
                    quest.State == QuestState.InProgress && quest.Step > 0;
                if (flag == 0) { mentionsActiveMark = true; hasActiveMark |= present; }
                else hasCompletionMark |= present;
            }
            bool changesActiveMark = branch.SubEntry.Any(op =>
            {
                ushort flag;
                return op.DialogPtr == 5 && Game.PlayerRelated.NotebookManager.TryGetCompletionFlag(op.dialog1, out flag) && flag == 0;
            });
            // A lost-item replacement after completion is not an unfinished quest.
            if (hasCompletionMark && !hasActiveMark && !changesActiveMark) return false;
            return mentionsActiveMark || changesActiveMark;
        }

        public static bool HasLinkedEvent(ushort mapId, ushort clickId)
        {
            var data = DataBase.GameDataBase.GlobalInstance?.EveDat?.GetMapData(mapId);
            var actor = data?.Npclist?.FirstOrDefault(n => n.clickId == clickId);
            return actor?.Events != null && actor.Events.Any(id =>
                data.Events.Any(e => e.clickID == id && e.SubEntry.Any(s => s.SubEntry.Count > 0)));
        }

        public static bool IsRoamingBattleNpc(ushort mapId, ushort clickId)
        {
            var data = DataBase.GameDataBase.GlobalInstance?.EveDat?.GetMapData(mapId);
            var actor = data?.Npclist?.FirstOrDefault(n => n.clickId == clickId);
            // Native field actors (type 5) link to encounter events (type 10).
            // A linked battle is not automatically a quest or a dialogue NPC.
            return actor?.unknownbyte1 == 5 && actor.Events != null && actor.Events.Any(id =>
                data.Events.Any(e => e.clickID == id && e.unknownbyte1 == 10 &&
                    e.SubEntry.Any(s => s.SubEntry.Any(o => o.DialogPtr == 4 && o.dialog1 == 1))));
        }

        // AC20:8 carries the EVE entry id. Scripted entries must not fall through to
        // the map's single-exit teleport fallback when their conditions do not match.
        public static bool TryExecuteEntry(Player player, GameMap map, ushort entryId)
        {
            var data = DataBase.GameDataBase.GlobalInstance?.EveDat?.GetMapData((ushort)map.MapID);
            var entry = data?.Entry_Points?.FirstOrDefault(e => e.clickID == entryId &&
                (e.unknownbyte3 == 1 || (map.MapID == 12000 && e.clickID == 4 && e.unknownbyte3 == 2)));
            // Kelan's chief-house portal includes Roca's return-scroll choice.
            // Running the geometric warp first silently skipped that authored dialogue.
            if (entry == null) return false;
            if (player.NativeEventActive) return true;
            if (!InsideEntry(entry, player.CurX, player.CurY))
            {
                // The client waits for AC20:8 even when its entry request is stale
                // or outside the server-side region. Release click-to-move.
                player.Send(Tools.FromFormat("bb", 20, 8));
                return true;
            }
            var linked = data.Events.Where(e => entry.unknownbytearray1.Any(id => id == e.clickID)).ToList();
            if (linked.Count > 0 && linked.All(e => IsNativeEventDisabled(map.MapID, e.clickID)))
                return RejectDisabledNativeEvent(player, map, linked[0]);
            foreach (var id in entry.unknownbytearray1)
            {
                var ev = data.Events.FirstOrDefault(e => e.clickID == id);
                var branch = FindBranch(player, map, ev);
                if (branch == null) continue;
                StartSession(player, map, 0, ev, branch);
                return true;
            }
            player.Send(Tools.FromFormat("bb", 20, 8));
            return true;
        }

        public static bool TryExecuteRegion(Player player, GameMap map, ushort? previousX = null, ushort? previousY = null)
        {
            if (player.NativeEventActive) return true;
            var data = DataBase.GameDataBase.GlobalInstance?.EveDat?.GetMapData((ushort)map.MapID);
            if (data?.Entry_Points == null) return false;
            foreach (var entry in data.Entry_Points.Where(e => e.unknownbyte3 == 1))
            {
                if (!InsideEntry(entry, player.CurX, player.CurY)) continue;
                if (previousX.HasValue && previousY.HasValue && InsideEntry(entry, previousX.Value, previousY.Value)) continue;
                var linked = data.Events.Where(e => entry.unknownbytearray1.Any(id => id == e.clickID)).ToList();
                if (linked.Count > 0 && linked.All(e => IsNativeEventDisabled(map.MapID, e.clickID)))
                    return RejectDisabledNativeEvent(player, map, linked[0]);
                foreach (var id in entry.unknownbytearray1)
                {
                    var ev = data.Events.FirstOrDefault(e => e.clickID == id);
                    var branch = FindBranch(player, map, ev);
                    if (branch == null) continue;
                    StartSession(player, map, 0, ev, branch);
                    return true;
                }
            }
            return false;
        }

        // These authored map-arrival rules advance Roca's father/reunion story.
        // Visibility replay alone must not consume their movie or quest actions.
        public static bool TryExecuteStoryArrival(Player player, GameMap map)
        {
            if (player == null || map == null || player.CurMap != map) return false;
            if (player.NativeEventActive) return true;
            // A disconnect after the spider movie resumes the authored farewell, not the battle.
            if (map.MapID == 11077 && player.Quests.TryGetValue(13086, out var fate) &&
                fate.State == QuestState.InProgress && fate.Step == 2)
            {
                var farewell = DataBase.GameDataBase.GlobalInstance?.EveDat?.GetMapData(11077)?.Events.FirstOrDefault(e => e.clickID == 12);
                // Death flag is committed before closing the progress flag below.
                // Finish that cleanup if a disconnect interrupted the two writes.
                if (player.Quests.TryGetValue(13087, out var done) && done.State == QuestState.InProgress && done.Step > 0)
                {
                    var ending = farewell?.SubEntry.FirstOrDefault(b => b.subIndex == 1);
                    var complete = ending?.SubEntry.FirstOrDefault(o => o.DialogPtr == 5 && o.dialog1 == 13086 && o.dialog2 == 2);
                    if (complete.HasValue && complete.Value.DialogPtr == 5) ExecuteOpcode(player, map, 13, farewell, ending, complete.Value);
                    return false;
                }
                var branch = FindBranch(player, map, farewell);
                if (branch == null) return false;
                StartSession(player, map, 13, farewell, branch, false);
                return true;
            }
            if (map.MapID != 12055 && map.MapID != 12000) return false;
            var data = DataBase.GameDataBase.GlobalInstance?.EveDat?.GetMapData((ushort)map.MapID);
            if (data?.PreEvents == null) return false;
            foreach (var pre in data.PreEvents.Where(p => map.MapID == 12055 ? p.clickID == 1 : p.clickID == 10 || p.clickID == 12))
            {
                // PreEvents use the same 21-byte condition/action records as EVE
                // events. Keep their original condition grouping and action order.
                var ev = new EventsinMapEntries { Name = "Story arrival " + pre.clickID };
                foreach (var sub in pre.subentry1)
                {
                    if (sub.unknown == null || sub.unknown.Count < 21) return false;
                    var raw = sub.unknown.ToArray();
                    var branch = new EventSubEntry {
                        subIndex = sub.subIndex, unknownbyte1 = raw[0],
                        unknownword1 = BitConverter.ToUInt16(raw, 1), unknownword2 = BitConverter.ToUInt16(raw, 3),
                        unknownword3 = BitConverter.ToUInt16(raw, 5), unknownword4 = BitConverter.ToUInt16(raw, 7),
                        unknownword5 = BitConverter.ToUInt16(raw, 9), unknownword6 = BitConverter.ToUInt16(raw, 11),
                        unknowndword1 = BitConverter.ToUInt32(raw, 13), unknowndword2 = BitConverter.ToUInt32(raw, 17)
                    };
                    foreach (var action in sub.subentry2)
                    {
                        if (action.unknown == null || action.unknown.Count < 21) return false;
                        raw = action.unknown.ToArray();
                        branch.SubEntry.Add(new EventSubSubEntry {
                            subsubIndex = action.subIndex, DialogPtr = raw[0],
                            dialog1 = BitConverter.ToUInt16(raw, 1), dialog2 = BitConverter.ToUInt16(raw, 3),
                            dialog3 = BitConverter.ToUInt16(raw, 5), dialog4 = BitConverter.ToUInt16(raw, 7),
                            unknowndword1 = BitConverter.ToUInt32(raw, 9), unknowndword2 = BitConverter.ToUInt32(raw, 13),
                            unknowndword3 = BitConverter.ToUInt32(raw, 17)
                        });
                    }
                    ev.SubEntry.Add(branch);
                }
                var selected = FindBranch(player, map, ev);
                if (selected == null) continue;
                // The reunion cleanup is already durable after the first visit.
                if (map.MapID == 12000 && pre.clickID == 10 && player.Quests.TryGetValue(13052, out var reunion) &&
                    reunion.State == QuestState.Completed) continue;
                StartSession(player, map, 0, ev, selected, false);
                return true;
            }
            return false;
        }

        private static bool InsideEntry(Entry_Exit_Point_Entries entry, ushort x, ushort y)
        {
            return x / 20 >= entry.x && x / 20 <= entry.unknowndword1 &&
                y / 20 >= entry.y && y / 20 <= entry.unknowndword2;
        }

        private static EventSubEntry FindBranch(Player player, GameMap map, EventsinMapEntries ev,
            byte trigger = 0, ushort question = 0, ushort answer = 0, EventSubEntry exclude = null)
        {
            if (ev?.SubEntry == null) return null;
            if (map?.MapID == 12002 && (ev.clickID == 8 || ev.clickID == 9) && QuestManager.IsXaolanInFate(player)) return null;
            // A completed answer branch falls through to later result conditions.
            // Restarting at the greeting can clear accumulated quiz marks first.
            int first = exclude != null && exclude.unknownbyte1 == 7 ? ev.SubEntry.IndexOf(exclude) + 1 : 0;
            IEnumerable<int> branchOrder = Enumerable.Range(first, ev.SubEntry.Count - first);
            // This EVE revision puts Junior Alchemy's material reminder before its
            // hand-in rule. Prefer the recipe rule, while retaining all four gates;
            // otherwise the reminder permanently shadows learning the skill.
            if (trigger == 0 && map?.MapID == 10071 && ev.clickID == 6)
                branchOrder = branchOrder.OrderBy(i => ev.SubEntry[i].subIndex == 6 ? 0 : 1);
            foreach (int i in branchOrder)
            {
                var branch = ev.SubEntry[i];
                if (branch == exclude || branch.SubEntry == null || branch.SubEntry.Count == 0) continue;
                bool callback = branch.unknownbyte1 == 4 || branch.unknownbyte1 == 7 || branch.unknownbyte1 == 8;
                if (trigger == 0 ? callback : branch.unknownbyte1 != trigger) continue;
                if (trigger != 0)
                {
                    if (branch.unknownword1 != question) continue;
                    if (trigger == 8)
                    {
                        if (!CompareValue(answer, branch.unknownword4 >> 8, branch.unknownword4 & 255)) continue;
                    }
                    else if (branch.unknownword2 != answer) continue;
                }
                bool matches = trigger != 0 || MatchesCondition(player, map, ev, branch);
                // Conditions belong to the action-bearing branch BEFORE them, never the next branch.
                int count = Math.Max(1, branch.unknownword6 >> 8);
                if (i + count > ev.SubEntry.Count) continue;
                for (int j = i + 1; j < i + count; j++)
                {
                    var condition = ev.SubEntry[j];
                    if (condition.SubEntry.Count > 0 || !MatchesCondition(player, map, ev, condition))
                        matches = false;
                }
                if (matches) return branch;
            }
            return null;
        }

        private static bool CompareValue(int actual, int expected, int comparison)
        {
            switch (comparison)
            {
                case 1: return actual < expected;
                case 2: return actual > expected;
                case 3: return actual <= expected;
                case 4: return actual >= expected;
                case 5: return actual == expected;
                case 6: return actual != expected;
                default: return false;
            }
        }

        private static bool MatchesCondition(Player player, GameMap map, EventsinMapEntries ev, EventSubEntry condition)
        {
            int value = (condition.unknownword4 >> 8) | (condition.unknownword5 << 8) |
                ((condition.unknownword6 & 255) << 24);
            int comparison = condition.unknownword4 & 255;
            switch (condition.unknownbyte1)
            {
                case 0: return true;
                case 1: return condition.unknownword1 == 0 && condition.unknownword2 == 0 &&
                    condition.unknownword3 == 0 && condition.unknownword4 == 0;
                case 6: return true; // normal click / greeting
                case 5:
                    PlayerQuest quest;
                    bool active = player.Quests != null && player.Quests.TryGetValue(condition.unknownword1, out quest) &&
                        quest.State == QuestState.InProgress;
                    int mark = 0;
                    if (active) mark = player.Quests[condition.unknownword1].Step;
                    if (condition.unknownword2 == 2) return !active;
                    if (condition.unknownword2 != 1) return false;
                    return CompareValue(mark, value, comparison);
                case 2:
                    if (condition.unknownword1 == 1)
                    {
                        // Player operand: 1 = item count, 2 = gold. The operand selector
                        // is not a required quantity; the threshold is the packed value.
                        if (condition.unknownword2 == 1)
                            return CompareValue(player.Inv?.GetItemCount(condition.unknownword3) ?? 0, value, comparison);
                        if (condition.unknownword2 == 2)
                            return CompareValue((int)player.Gold, value, comparison);
                        return false;
                    }
                    if (condition.unknownword1 == 2)
                    {
                        bool present = player.PlayerPets != null && player.PlayerPets.Values.Any(p =>
                            p != null && Player.IsSamePetOrCompanion(p.PetID, condition.unknownword3));
                        switch (condition.unknownword2)
                        {
                            case 1: return present;
                            case 2: return !present;
                            case 5:
                                int pets = player.PlayerPets?.Values.Count(p => p != null) ?? 0;
                                return CompareValue(Math.Max(0, 4 - pets), value, comparison);
                            case 6:
                                // The packed operand is the active companion template, not w3.
                                // Require a real party member: a stale ActivePetID must not
                                // authorize the companion's story branch after removal.
                                if (condition.unknownword3 != 0 || value <= 0 || value > ushort.MaxValue ||
                                    (comparison != 5 && comparison != 6)) return false;
                                bool following = player.ActivePetID != 0 && player.PlayerPets != null &&
                                    player.PlayerPets.Values.Any(p => p != null &&
                                        Player.IsSamePetOrCompanion(p.PetID, player.ActivePetID) &&
                                        Player.IsSamePetOrCompanion(p.PetID, (uint)value));
                                return comparison == 5 ? following : !following;
                            case 8: // Already owns this pet (voucher dialogue 31350).
                            case 9: // May recruit it; a quest reserve is restored, not duplicated.
                                bool owned = present || (player.HotelPets != null && player.HotelPets.Values.Any(p =>
                                    p != null && Player.IsSamePetOrCompanion(p.PetID, condition.unknownword3)));
                                return condition.unknownword2 == 8 ? owned : !owned;
                            case 7: // Companion amity (e.g. Roca/Xaolan hot spring: >60).
                            case 10: // Companion level (e.g. Elin's chip: >=40).
                                var companion = player.PlayerPets?.Values.FirstOrDefault(p => p != null &&
                                    Player.IsSamePetOrCompanion(p.PetID, condition.unknownword3));
                                return companion != null && CompareValue(condition.unknownword2 == 7 ?
                                    companion.Amity : companion.Level, value, comparison);
                        }
                    }
                    return false;
                case 3:
                    if (map == null || ev == null) return false;
                    // Stewart's keyed stone door is opened by event9 but crossed by
                    // event10. Read the durable marks used by its native PreEvents,
                    // not an opened-chest key derived from the current event ID.
                    if (map.MapID == 11036 && condition.unknownword1 == 7 && condition.unknownword2 == 3)
                    {
                        bool doorOpen = player.Quests != null &&
                            ((player.Quests.TryGetValue(10024, out var keyDoor) &&
                              keyDoor.State == QuestState.InProgress && keyDoor.Step >= 1) ||
                             (player.Quests.TryGetValue(10025, out var keyDoorDone) &&
                              keyDoorDone.State == QuestState.InProgress && keyDoorDone.Step == 1));
                        return CompareValue(doorOpen ? 1 : 0, value, comparison);
                    }
                    // Stewart door actor7 is opened by Fate's statue, not a loot chest.
                    if (map.MapID == 11037 && condition.unknownword1 == 7 && condition.unknownword2 == 3)
                    {
                        bool doorOpen = player.Quests.TryGetValue(13086, out var fate) &&
                            fate.State == QuestState.InProgress && fate.Step > 0 &&
                            !(player.Quests.TryGetValue(13087, out var fateDone) && fateDone.State == QuestState.InProgress && fateDone.Step > 0);
                        return CompareValue(doorOpen ? 1 : 0, value, comparison);
                    }
                    uint key = (uint)(map.MapID * 1000 + ev.clickID);
                    PlayerQuest chest;
                    bool opened = player.Quests != null && player.Quests.TryGetValue(key, out chest) && chest.State == QuestState.Completed;
                    return CompareValue(opened ? 1 : 0, value, comparison);
                case 10:
                    // Native condition type 4: learned grade of the specified skill.
                    if (condition.unknownword2 != 1 || condition.unknownword3 != 0 ||
                        Game.SkillRelated.SkillManager.GetSkill(condition.unknownword1) == null) return false;
                    var skill = player.PlayerSkills?.FirstOrDefault(s => s.SkillID == condition.unknownword1);
                    return CompareValue(skill?.Grade ?? 0, value, comparison);
                case 14:
                    return MatchesGatheringTimer(player, condition);
                case 15:
                    return condition.unknownword1 == 1 && CompareValue(GetPlayerFreeSlots(player), value, comparison);
                default:
                    // Unsupported conditions must never authorize quest rewards.
                    return false;
            }
        }

        internal static bool MatchesPreEventCondition(Player player, byte[] data)
        {
            if (data == null || data.Length < 21) return false;
            var condition = new EventSubEntry {
                unknownbyte1 = data[0], unknownword1 = BitConverter.ToUInt16(data, 1),
                unknownword2 = BitConverter.ToUInt16(data, 3), unknownword3 = BitConverter.ToUInt16(data, 5),
                unknownword4 = BitConverter.ToUInt16(data, 7), unknownword5 = BitConverter.ToUInt16(data, 9),
                unknownword6 = BitConverter.ToUInt16(data, 11)
            };
            return MatchesCondition(player, null, null, condition);
        }

        private static uint DecodeActionValue(EventSubSubEntry op)
        {
            return (uint)(op.dialog4 >> 8) | (op.unknowndword1 << 8);
        }

        // Native client AC20:1 layout: type, subject, actor, mode, uint value,
        // ushort text/question, byte branch. Path/movie completion returns AC20:6.
        private static SendPacket BuildEventFrame(byte type, byte subject, ushort actor, byte mode,
            uint value, ushort text, byte step, byte branch)
        {
            var packet = new SendPacket();
            packet.PackArray(new byte[] { 20, 1, 0, 0, 0, step, type, subject });
            packet.Pack16(actor);
            packet.Pack8(mode);
            packet.Pack32(value);
            packet.Pack16(text);
            packet.Pack8(branch);
            return packet;
        }

        private static bool IsClientStep(EventSubSubEntry op)
        {
            return (op.DialogPtr == 1 && (op.dialog1 == 4 || (op.dialog1 == 2 && op.dialog2 >= 10000))) ||
                (op.DialogPtr == 2 && (op.dialog2 == 0 || op.dialog2 == 1 || op.dialog2 == 6 || op.dialog2 == 9)) ||
                op.DialogPtr == 4 || op.DialogPtr == 6 || op.DialogPtr == 7 || op.DialogPtr == 8 || op.DialogPtr == 9 || op.DialogPtr == 15;
        }

        private static bool IsXaolanRobberyReward(Player player, EventSubEntry branch, EventSubSubEntry op)
        {
            // This EVE revision omits the Star from the first rescue's Shale reward.
            return player.CurMap?.MapID == 12002 && branch.unknownbyte1 == 4 && branch.unknownword2 == 1 &&
                op.DialogPtr == 1 && op.dialog1 == 1 && op.dialog3 == 43002 &&
                branch.SubEntry.Any(o => o.DialogPtr == 5 && o.dialog1 == 13032 && o.dialog2 == 2);
        }

        private static bool CanDeliverPendingRewards(Player player, EventSubEntry branch, int index)
        {
            var changes = new System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<ushort, int>>();
            long gold = player.Gold;
            var party = (player.PlayerPets?.Values ?? Enumerable.Empty<Player.PlayerPetData>()).Where(p => p != null).Select(p => p.PetID).ToList();
            var reserve = (player.QuestPets?.Values ?? Enumerable.Empty<Player.PlayerPetData>()).Where(p => p != null).Select(p => p.PetID).ToList();
            int reserveSlots = player.QuestPets?.Count ?? 0;
            foreach (var op in branch.SubEntry.Skip(index).TakeWhile(o => !IsClientStep(o)))
            {
                if (UnsupportedRewardAction(op)) return false;
                if (op.DialogPtr == 3)
                {
                    uint id = op.dialog2;
                    bool present = party.Any(p => Player.IsSamePetOrCompanion(p, id));
                    if (op.dialog1 == 1 && !present)
                    {
                        if ((Player.IsSamePetOrCompanion(id, 14156) && QuestManager.IsXaolanInFate(player)) ||
                            party.Count >= 4 || SceneDataManager.GetNpcBaseStats(Player.GetCompanionBroadcastId(id)) == null ||
                            (player.HotelPets != null && player.HotelPets.Values.Any(p => p != null && Player.IsSamePetOrCompanion(p.PetID, id)))) return false;
                        party.Add(id);
                        int returning = reserve.FindIndex(p => Player.IsSamePetOrCompanion(p, id));
                        if (returning >= 0) { reserve.RemoveAt(returning); reserveSlots--; }
                    }
                    else if (op.dialog1 == 2)
                    {
                        if (present && QuestManager.IsStoryCompanion(id))
                        {
                            if (reserveSlots >= byte.MaxValue || reserve.Any(p => Player.IsSamePetOrCompanion(p, id))) return false;
                            reserve.Add(id);
                            reserveSlots++;
                        }
                        party.RemoveAll(p => Player.IsSamePetOrCompanion(p, id));
                    }
                    else if (op.dialog1 == 5 && !present) return false;
                    else if (op.dialog1 != 1 && op.dialog1 != 5) return false;
                }
                if (op.DialogPtr != 1 || op.dialog1 != 1) continue;
                int amount = unchecked((int)DecodeActionValue(op));
                if (op.dialog2 == 1 || op.dialog2 == 7)
                {
                    if (op.dialog3 == 0) return false;
                    changes.Add(new System.Collections.Generic.KeyValuePair<ushort, int>(op.dialog3, amount));
                    if (IsXaolanRobberyReward(player, branch, op))
                        changes.Add(new System.Collections.Generic.KeyValuePair<ushort, int>(30025, 1));
                }
                else if (op.dialog2 == 2 && op.dialog3 == 0)
                {
                    gold += amount;
                    if (gold < 0 || gold > 999999) return false;
                }
                else if (op.dialog2 == 5 && op.dialog3 == 1)
                {
                    if (amount < 0 || player.Eqs == null) return false;
                }
                else if (IsFullRecoveryReward(op))
                {
                    if (player.Eqs == null) return false;
                }
                else return false; // Never interpret an unknown reward type as an item ID.
            }
            return changes.Count == 0 || player.Inv.TryApplyQuestItems(changes, false);
        }

        private static bool IsFullRecoveryReward(EventSubSubEntry op)
        {
            // 成年禮的考驗: native 1/1/3 and 1/1/4, player target 1,
            // zero operand means full recovery (not setting HP/SP to zero).
            return op.DialogPtr == 1 && op.dialog1 == 1 &&
                (op.dialog2 == 3 || op.dialog2 == 4) && op.dialog3 == 1 && DecodeActionValue(op) == 0;
        }

        private static bool UnsupportedRewardAction(EventSubSubEntry op)
        {
            return op.DialogPtr == 10 ||
                (op.DialogPtr == 11 && (op.dialog2 != 1 || op.dialog3 != 0 || DecodeActionValue(op) != 1 ||
                    Game.SkillRelated.SkillManager.GetSkill(op.dialog1) == null)) ||
                (op.DialogPtr == 13 && (op.dialog1 < 1 || op.dialog1 > 3 || op.dialog3 < 1 || op.dialog3 > 256)) ||
                (op.DialogPtr == 14 && !IsWaterTimerAction(op)) || op.DialogPtr == 16 || op.DialogPtr == 17 ||
                (op.DialogPtr == 15 && (op.dialog1 != 1 || op.dialog2 != 1 || op.dialog3 != 0 ||
                    DecodeActionValue(op) == 0 || DecodeActionValue(op) > 9999)) ||
                (op.DialogPtr == 1 && op.dialog1 == 1 && !IsFullRecoveryReward(op) && !IsWaterReward(op) && op.dialog2 != 1 && op.dialog2 != 2 && op.dialog2 != 5 && op.dialog2 != 7);
        }

        // Reconstructed controller for Niss resting/returning at Welling inn only.
        // The meaning of native action 10 remains unknown; other events still reject it.
        // Keep the authored dialogue, movie, pose, quest marks and warps unchanged.
        private static EventSubEntry PrepareNissInnBranch(GameMap map, EventsinMapEntries ev, EventSubEntry branch)
        {
            if (map.MapID != 12052 || ev.clickID != 6) return branch;
            var actions = branch.SubEntry.ToList();
            if (branch.subIndex == 1 || branch.subIndex == 6)
            {
                // Save the resting checkpoint before reserving Niss. The checkpoint
                // below owns removal, so reconnecting at step 1 can safely resume it.
                actions.RemoveAll(o => o.DialogPtr == 3 && o.dialog1 == 2 && o.dialog2 == 14081);
            }
            else if (branch.subIndex == 12 || branch.subIndex == 14)
            {
                actions.RemoveAll(o => o.DialogPtr == 10 && o.dialog1 == 1 &&
                    o.dialog2 == 0 && o.dialog3 == 0 && DecodeActionValue(o) == 0);
                if (branch.subIndex == 12)
                    actions.Insert(0, new EventSubSubEntry { DialogPtr = 3, dialog1 = 2, dialog2 = 14081 });
            }
            else return branch;
            return new EventSubEntry {
                subIndex = branch.subIndex, unknownbyte1 = branch.unknownbyte1,
                unknownword1 = branch.unknownword1, unknownword2 = branch.unknownword2,
                unknownword3 = branch.unknownword3, unknownword4 = branch.unknownword4,
                unknownword5 = branch.unknownword5, unknownword6 = branch.unknownword6,
                unknowndword1 = branch.unknowndword1, unknowndword2 = branch.unknowndword2,
                SubEntry = actions
            };
        }

        // User-approved reconstruction of the Xaolan finale controller only.
        // Original actor action2/11 is unresolved; preserve dialogue/movies/marks and
        // replace its visual lifecycle with hiding the two scene actors at farewell.
        private static EventSubEntry PrepareXaolanFateBranch(GameMap map, EventsinMapEntries ev, EventSubEntry branch)
        {
            if (map.MapID != 11077 || !((ev.clickID == 7 && (branch.subIndex == 5 || branch.subIndex == 7)) ||
                (ev.clickID == 12 && branch.subIndex == 1))) return branch;
            var actions = branch.SubEntry.ToList();
            if (ev.clickID == 7 && branch.subIndex == 5)
            {
                var checkpoint = actions.First(o => o.DialogPtr == 5 && o.dialog1 == 13086);
                actions.Remove(checkpoint);
                actions.Insert(actions.FindIndex(o => o.DialogPtr == 3 && o.dialog1 == 2), checkpoint);
            }
            else
            {
                int index = actions.FindIndex(o => o.DialogPtr == 2 && o.dialog2 == 11);
                if (index >= 0)
                {
                    actions.RemoveAt(index);
                    actions.Insert(index, new EventSubSubEntry { DialogPtr = 2, dialog1 = 12, dialog2 = 2, dialog3 = 1, dialog4 = 65280, unknowndword1 = 255 });
                    actions.Insert(index + 1, new EventSubSubEntry { DialogPtr = 2, dialog1 = 13, dialog2 = 2, dialog3 = 1, dialog4 = 65280, unknowndword1 = 255 });
                }
                // Commit death while the resumable step2 still exists, then close it.
                var death = actions.First(o => o.DialogPtr == 5 && o.dialog1 == 13087);
                actions.Remove(death);
                actions.Insert(actions.FindIndex(o => o.DialogPtr == 5 && o.dialog1 == 13086), death);
            }
            return new EventSubEntry {
                subIndex = branch.subIndex, unknownbyte1 = branch.unknownbyte1,
                unknownword1 = branch.unknownword1, unknownword2 = branch.unknownword2,
                unknownword3 = branch.unknownword3, unknownword4 = branch.unknownword4,
                unknownword5 = branch.unknownword5, unknownword6 = branch.unknownword6,
                unknowndword1 = branch.unknowndword1, unknowndword2 = branch.unknowndword2, SubEntry = actions
            };
        }

        private static void StartSession(Player player, GameMap map, ushort clickId, EventsinMapEntries ev, EventSubEntry branch, bool allowTransition = true)
        {
            if (RejectDisabledNativeEvent(player, map, ev)) return;
            if (TryRunWaterGathering(player, map, ev, branch)) return;
            branch = PrepareNissInnBranch(map, ev, branch);
            branch = PrepareXaolanFateBranch(map, ev, branch);
            player.ClearInteraction();
            var token = new object();
            player.NativeEventToken = token;
            player.NativeEventActive = true;
            player.Send(Tools.FromFormat("bbb", 6, 2, 1));
            int index = 0;
            Action advance = null;
            Action finish = () => {
                if (player.NativeEventToken != token) return;
                player.CancelInteraction();
                QuestManager.SyncPerPlayerNpcVisibility(player, (ushort)map.MapID);
                player.SaveCharacterData();
            };
            // Refuse unsupported mutations before saving any marks or taking materials.
            if (branch.SubEntry.Any(UnsupportedRewardAction))
            {
                var unsupported = branch.SubEntry.First(UnsupportedRewardAction);
                DebugSystem.Write("[EveEventInterpreter] Unsupported quest action: map=" + map.MapID +
                    " event=" + ev.clickID + " branch=" + branch.subIndex + " step=" + unsupported.subsubIndex + " opcode=" + unsupported.DialogPtr);
                player.SendHeadBanner("This quest action is not supported yet. Please report this quest.");
                finish();
                return;
            }
            advance = () => {
                if (player.NativeEventToken != token) return;
                lock (player.Inv.SyncRoot)
                try
                {
                    if (!CanDeliverPendingRewards(player, branch, index))
                    {
                        player.SendHeadBanner("Cannot receive reward. Check materials, bag space, gold, party slots and Pet Hotel.");
                        finish();
                        return;
                    }
                    while (index < branch.SubEntry.Count)
                    {
                        var op = branch.SubEntry[index++];
                        byte step = op.subsubIndex;
                        bool playerChoice = op.DialogPtr == 1 && op.dialog1 == 4;
                        if (playerChoice || (op.DialogPtr == 2 && op.dialog2 == 6))
                        {
                            ushort question = playerChoice ? op.dialog2 : op.dialog3;
                            player.OnDialogueChoice = choice => {
                                if (player.NativeEventToken != token) return;
                                // Native option callbacks are 20/21 or 30..39. Close is 40,
                                // never an alias for the first option.
                                if (choice == 40) { finish(); return; }
                                ushort answer = choice;
                                var selected = FindBranch(player, map, ev, 7, question, answer);
                                if (selected == null) { finish(); return; }
                                StartSession(player, map, clickId, ev, selected);
                            };
                            player.Send(BuildEventFrame(6, (byte)(playerChoice ? 7 : 3), (ushort)(playerChoice ? 0 : op.dialog1), 0, 0, question, step, branch.subIndex));
                            return;
                        }
                        bool playerSpeech = op.DialogPtr == 1 && op.dialog1 == 2 && op.dialog2 >= 10000;
                        bool npcSpeech = op.DialogPtr == 2 && (op.dialog2 == 0 || op.dialog2 == 1) && op.dialog3 >= 10000;
                        bool path = op.DialogPtr == 2 && op.dialog2 == 9;
                        bool movie = op.DialogPtr == 8 && (op.dialog1 == 1 || op.dialog1 == 2);
                        bool music = op.DialogPtr == 15;
                        if (playerSpeech || npcSpeech || path || movie || music)
                        {
                            player.OnInteractionComplete = advance;
                            if (playerSpeech || npcSpeech)
                                player.Send(BuildEventFrame(1, (byte)(playerSpeech ? 7 : 3), (ushort)(playerSpeech ? 0 : op.dialog1),
                                    1, 0, playerSpeech ? op.dialog2 : op.dialog3, step, branch.subIndex));
                            else if (!ExecuteOpcode(player, map, clickId, ev, branch, op)) finish();
                            return;
                        }
                        // Battle/minigame results own the next branch; never execute past their start.
                        bool external = op.DialogPtr == 4 || op.DialogPtr == 6 || op.DialogPtr == 9;
                        if (!ExecuteOpcode(player, map, clickId, ev, branch, op)) { finish(); return; }
                        if (external) return;
                        if (op.DialogPtr == 7) { finish(); return; }
                        if (player.NativeEventToken != token) return; // teleport/cancellation
                        if (player.OnInteractionComplete != null) return;
                    }
                    if (map.MapID == 11077 && ev.clickID == 7 && branch.subIndex == 5)
                    {
                        var farewell = DataBase.GameDataBase.GlobalInstance.EveDat.GetMapData(11077).Events.First(e => e.clickID == 12);
                        var next = FindBranch(player, map, farewell);
                        if (next != null) { StartSession(player, map, 13, farewell, next, false); return; }
                    }
                    if (map.MapID == 12052 && ev.clickID == 6)
                    {
                        // Intro dialogue flows directly into rest; return from the
                        // rescue flows into native rejoin (including its full-party gate).
                        var nextEvent = branch.subIndex == 14
                            ? DataBase.GameDataBase.GlobalInstance.EveDat.GetMapData(12052).Events.FirstOrDefault(e => e.clickID == 9)
                            : ev;
                        if (branch.subIndex == 1 || branch.subIndex == 6 || branch.subIndex == 14)
                        {
                            var next = FindBranch(player, map, nextEvent);
                            if (next != null) { StartSession(player, map, clickId, nextEvent, next, false); return; }
                        }
                        finish();
                        return;
                    }
                    // Opening Robinson's raft chest enables his immediate reaction.
                    // Re-evaluate authored conditions after the reward; do not replay it.
                    if ((map.MapID == 10035 || map.MapID == 10039) && ev.clickID == 19 && branch.subIndex == 1)
                    {
                        var next = FindBranch(player, map, ev, exclude: branch);
                        if (next != null) { StartSession(player, map, clickId, ev, next, false); return; }
                    }
                    // A pure mark/party transition may enable the next branch in this event
                    // (Roca returning the scroll, or clearing a retryable minigame mark).
                    if (allowTransition && branch.SubEntry.All(o => o.DialogPtr == 5 || o.DialogPtr == 3))
                    {
                        var next = FindBranch(player, map, ev, exclude: branch);
                        if (next != null) { StartSession(player, map, clickId, ev, next, false); return; }
                    }
                    finish();
                }
                catch (Exception ex)
                {
                    DebugSystem.Write("[EveEventInterpreter] Event " + ev.clickID + " aborted: " + ex.Message);
                    finish();
                }
            };
            advance();
        }

        private static void RunOutcome(Player player, GameMap map, ushort clickId, EventsinMapEntries ev,
            byte trigger, ushort source, ushort result, object token)
        {
            if (token == null || player.NativeEventToken != token) return;
            player.OnMinigameWon = null;
            player.OnMinigameLost = null;
            if (player.CurMap != map) return;
            var branch = FindBranch(player, map, ev, trigger, source, result);
            if (branch != null) StartSession(player, map, clickId, ev, branch);
            else
            {
                player.CancelInteraction();
            }
        }

        private static bool ExecuteCompanionAction(Player player, GameMap map, EventSubSubEntry op)
        {
            var pet = player.PlayerPets?.Values.FirstOrDefault(p => p != null && Player.IsSamePetOrCompanion(p.PetID, op.dialog2));
            switch (op.dialog1)
            {
                case 1:
                    return QuestManager.SendCompanionReward(player, op.dialog2, QuestManager.ResolveCompanionName(op.dialog2));
                case 2:
                    if (pet == null) return true;
                    // Preserve a story companion's progress while it is absent from the party.
                    // Ordinary quest turn-in creatures are still consumed, not archived.
                    if (QuestManager.IsStoryCompanion(pet.PetID))
                    {
                        if (player.QuestPets == null) player.QuestPets = new System.Collections.Generic.Dictionary<byte, Player.PlayerPetData>();
                        if (player.QuestPets.Values.Any(p => p != null && Player.IsSamePetOrCompanion(p.PetID, pet.PetID))) return false;
                        int reserveSlot = 1;
                        while (reserveSlot <= byte.MaxValue && player.QuestPets.ContainsKey((byte)reserveSlot)) reserveSlot++;
                        if (reserveSlot > byte.MaxValue) return false;
                        player.QuestPets[(byte)reserveSlot] = pet;
                    }
                    byte partySlot = pet.Slot;
                    byte clientSlot = pet.ClientSlot;
                    bool removingMount = Player.IsSamePetOrCompanion(player.ActiveMountID, pet.PetID);
                    if (removingMount) player.UnridePet();
                    if (Player.IsSamePetOrCompanion(player.ActivePetID, pet.PetID))
                    {
                        player.ActivePetID = 0;
                        pet.IsBattle = false;
                        player.Send(Tools.FromFormat("bb", 19, 2));
                        player.CurMap?.Broadcast(Tools.FromFormat("bbd", 19, 7, player.CharID), "Ex", player.CharID);
                        // A different mounted pet owns the visible appearance while riding.
                        if (player.ActiveMountID == 0)
                        {
                            var despawn = Tools.FromFormat("bbdd", 5, 8, player.CharID, 0);
                            player.Send(despawn);
                            player.CurMap?.Broadcast(despawn, "Ex", player.CharID);
                        }
                    }
                    player.PlayerPets.Remove(partySlot);
                    pet.ClientSlot = 0;
                    pet.IsBattle = false;
                    pet.IsRide = false;
                    if (player.QuestPets != null)
                    {
                        var reserved = player.QuestPets.FirstOrDefault(p => p.Value == pet);
                        if (reserved.Value != null) pet.Slot = reserved.Key;
                    }
                    var removed = Tools.FromFormat("bbdb", 15, 2, player.CharID, clientSlot);
                    player.Send(removed);
                    player.CurMap?.Broadcast(removed, "Ex", player.CharID);
                    player.SaveCharacterData();
                    return true;
                case 5:
                    if (pet == null) return false;
                    pet.Amity = (byte)Math.Max(0, Math.Min(100, pet.Amity + (int)unchecked((sbyte)(op.dialog4 >> 8))));
                    QuestManager.SendPetAmity(player, pet);
                    player.SaveCharacterData();
                    return true;
                default: return false;
            }
        }
    }
}
