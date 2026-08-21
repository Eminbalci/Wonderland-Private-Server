using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Network;
using RCLibrary.Core;

namespace Game.QuestRelated
{
    public static class QuestManager
    {
        private static readonly Dictionary<uint, QuestDefinition> _registeredQuests = new Dictionary<uint, QuestDefinition>();
        private static readonly object _lock = new object();

        public static IReadOnlyDictionary<uint, QuestDefinition> AllQuests => _registeredQuests;
        public static int Count => _registeredQuests.Count;

        static QuestManager()
        {
            InitializeQuests();
        }

        public static void InitializeQuests()
        {
            lock (_lock)
            {
                _registeredQuests.Clear();

                // Load all quests directly from SQLite/MySQL database
                if (DataBase.GameDataBase.GlobalInstance != null)
                {
                    DataBase.QuestDataBase.LoadAllQuests(DataBase.GameDataBase.GlobalInstance);
                }

                DebugSystem.Write($"[QuestManager] Total {_registeredQuests.Count} active quests registered from database.");
            }
        }

        /// <summary>
        /// Loads all authentic quests directly from the client/server Mark.dat binary file.
        /// Extracts real titles, descriptions, and multi-step #01/#02/#99 stage progressions.
        /// </summary>
        public static void LoadAuthenticQuestsFromMarkDat(string filePath)
        {
            if (string.IsNullOrEmpty(filePath) || !System.IO.File.Exists(filePath)) return;

            try
            {
                byte[] data = System.IO.File.ReadAllBytes(filePath);
                if (data.Length < 256) return;

                int numRecords = data.Length / 553;
                int loadedCount = 0;

                lock (_lock)
                {
                    for (uint markId = 1; markId <= numRecords; markId++)
                    {
                        int offset = (int)((markId - 1) * 553);
                        var entry = ParseMarkEntry(data, offset, markId);
                        if (entry != null && !string.IsNullOrWhiteSpace(entry.Title) && !_registeredQuests.ContainsKey(markId))
                        {
                                var quest = new QuestDefinition(markId, entry.Title, entry.Location ?? entry.Title, QuestType.Dialogue)
                                {
                                    Description = entry.Description ?? entry.Title,
                                    IntroDialogue = entry.Title,
                                    InProgressDialogue = entry.Description ?? entry.Title,
                                    CompleteDialogue = entry.CompletedSummary ?? entry.Description ?? entry.Title,
                                    AlreadyCompletedDialogue = entry.CompletedSummary ?? entry.Title,
                                    Reward = new QuestReward(gold: 0, exp: 0)
                                };

                                // Extract multi-stage steps if available (#01, #02...)
                                if (entry.StepDescriptions != null && entry.StepDescriptions.Count > 0)
                                {
                                    for (int i = 0; i < entry.StepDescriptions.Count; i++)
                                    {
                                        var stepDesc = entry.StepDescriptions[i];
                                        quest.AddStep(new QuestStep(i + 1, entry.Location ?? entry.Title, QuestType.Dialogue)
                                        {
                                            PromptDialogue = stepDesc,
                                            InProgressDialogue = stepDesc,
                                            CompleteDialogue = (i == entry.StepDescriptions.Count - 1) ? entry.CompletedSummary : stepDesc
                                        });
                                    }
                                }

                                _registeredQuests[markId] = quest;
                                loadedCount++;
                            }
                        }
                    }

                DebugSystem.Write($"[QuestManager] Loaded {loadedCount} authentic quests directly from Mark.dat (Total registered: {_registeredQuests.Count}).");
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[QuestManager] Error reading Mark.dat: {ex.Message}");
            }
        }

        private class ParsedMark
        {
            public uint MarkID { get; set; }
            public string Title { get; set; }
            public string Location { get; set; }
            public string Description { get; set; }
            public string CompletedSummary { get; set; }
            public List<string> StepDescriptions { get; set; } = new List<string>();
        }

        private static ParsedMark ParseMarkEntry(byte[] data, int offset, uint markId)
        {
            try
            {
                int end = Math.Min(data.Length, offset + 512);
                List<string> extractedStrings = new List<string>();
                List<char> currChars = new List<char>();

                for (int i = offset; i < end; i++)
                {
                    byte b = data[i];
                    if (b >= 32 && b <= 126)
                    {
                        currChars.Add((char)b);
                    }
                    else
                    {
                        if (currChars.Count >= 3)
                        {
                            currChars.Reverse();
                            string s = new string(currChars.ToArray()).Trim();
                            if (!string.IsNullOrEmpty(s) && !s.Contains("'s's's") && s != "0")
                            {
                                extractedStrings.Add(s);
                            }
                        }
                        currChars.Clear();
                    }
                }

                if (currChars.Count >= 3)
                {
                    currChars.Reverse();
                    string s = new string(currChars.ToArray()).Trim();
                    if (!string.IsNullOrEmpty(s) && !s.Contains("'s's's") && s != "0")
                    {
                        extractedStrings.Add(s);
                    }
                }

                if (extractedStrings.Count == 0) return null;

                var mark = new ParsedMark { MarkID = markId };
                mark.Title = CleanString(extractedStrings[0]);
                if (string.IsNullOrWhiteSpace(mark.Title) || mark.Title.StartsWith("Visit Mark") || mark.Title.StartsWith("Time Mark"))
                    return null;

                if (extractedStrings.Count > 1) mark.Location = CleanString(extractedStrings[1]);
                if (extractedStrings.Count > 2)
                {
                    string body = string.Join(" ", extractedStrings.GetRange(2, extractedStrings.Count - 2));
                    mark.Description = body;

                    // Parse #01, #02, #99 steps
                    if (body.Contains("#01") || body.Contains("#02") || body.Contains("#99"))
                    {
                        var tokens = body.Split('#');
                        foreach (var tok in tokens)
                        {
                            string trimmed = tok.Trim();
                            if (trimmed.StartsWith("99"))
                            {
                                mark.CompletedSummary = CleanString(trimmed.Substring(2));
                            }
                            else if (trimmed.Length >= 3 && char.IsDigit(trimmed[0]) && char.IsDigit(trimmed[1]))
                            {
                                mark.StepDescriptions.Add(CleanString(trimmed.Substring(2)));
                            }
                        }
                    }
                }

                return mark;
            }
            catch
            {
                return null;
            }
        }

        private static string CleanString(string s)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;
            var sb = new System.Text.StringBuilder();
            foreach (char c in s)
            {
                if (c >= 32 && c <= 126 && c != '&' && c != '$' && c != '\'' && c != '`')
                {
                    sb.Append(c);
                }
            }
            return sb.ToString().Trim();
        }

        public static void RegisterQuest(QuestDefinition quest)
        {
            if (quest == null) return;
            lock (_lock)
            {
                _registeredQuests[quest.QuestID] = quest;
            }
        }

        public static QuestDefinition GetQuest(uint questId)
        {
            lock (_lock)
            {
                _registeredQuests.TryGetValue(questId, out var quest);
                return quest;
            }
        }

        /// <summary>
        /// Finds the active or available quest for a player interacting with an NPC.
        /// Evaluates multi-stage quest steps first, then validates prerequisite quests before starting new ones.
        /// </summary>
        public static QuestDefinition FindQuestForPlayerNpc(Player player, string npcName, uint templateId, out QuestStep matchingStep, out bool isNewQuest)
        {
            matchingStep = null;
            isNewQuest = false;
            lock (_lock)
            {
                string lower = (npcName ?? "").ToLower().Trim();
                if (string.IsNullOrEmpty(lower) && templateId == 0) return null;

                // 1. Check in-progress quests for matching current step
                if (player != null && player.Quests != null)
                {
                    foreach (var pq in player.Quests.Values)
                    {
                        if (pq.State == QuestState.InProgress && _registeredQuests.TryGetValue(pq.QuestID, out var q))
                        {
                            if (q.Steps != null && q.Steps.Count > 0)
                            {
                                int stepIdx = Math.Max(1, pq.Step);
                                if (stepIdx <= q.Steps.Count)
                                {
                                    var step = q.Steps[stepIdx - 1];
                                    if (IsNpcMatch(step.TargetNpcPattern, step.TargetNpcTemplateID, lower, templateId))
                                    {
                                        matchingStep = step;
                                        return q;
                                    }
                                }
                            }
                            else if (IsNpcMatch(q.NpcNamePattern, q.NpcTemplateID, lower, templateId))
                            {
                                return q;
                            }
                        }
                    }
                }

                // 2. Check for starting new quests (NotStarted) with Prerequisite Quest checks
                foreach (var q in _registeredQuests.Values)
                {
                    if (player != null && player.Quests != null && player.Quests.TryGetValue(q.QuestID, out var existingPq))
                    {
                        if (existingPq.State == QuestState.Completed) continue;
                    }

                    // Validate all prerequisite quests are completed
                    if (q.PrerequisiteQuestIDs != null && q.PrerequisiteQuestIDs.Count > 0)
                    {
                        bool allPrereqsMet = true;
                        foreach (var prereqId in q.PrerequisiteQuestIDs)
                        {
                            if (player == null || player.Quests == null || !player.Quests.TryGetValue(prereqId, out var prereqPq) || prereqPq.State != QuestState.Completed)
                            {
                                allPrereqsMet = false;
                                break;
                            }
                        }
                        if (!allPrereqsMet) continue; // Skip if prerequisite quests not completed
                    }

                    if (q.Steps != null && q.Steps.Count > 0)
                    {
                        var firstStep = q.Steps[0];
                        if (IsNpcMatch(firstStep.TargetNpcPattern, firstStep.TargetNpcTemplateID, lower, templateId))
                        {
                            matchingStep = firstStep;
                            isNewQuest = true;
                            return q;
                        }
                    }
                    else if (IsNpcMatch(q.NpcNamePattern, q.NpcTemplateID, lower, templateId))
                    {
                        isNewQuest = true;
                        return q;
                    }
                }

                // 3. Fallback: Check completed quests for dialogue repetition
                if (player != null && player.Quests != null)
                {
                    foreach (var pq in player.Quests.Values)
                    {
                        if (pq.State == QuestState.Completed && _registeredQuests.TryGetValue(pq.QuestID, out var q))
                        {
                            if (IsNpcMatch(q.NpcNamePattern, q.NpcTemplateID, lower, templateId))
                            {
                                return q;
                            }
                        }
                    }
                }

                return null;
            }
        }

        public static QuestDefinition FindQuestForNpc(string npcName, uint templateId)
        {
            return FindQuestForPlayerNpc(null, npcName, templateId, out _, out _);
        }

        private static bool IsNpcMatch(string pattern, uint targetTid, string currentName, uint currentTid)
        {
            if (targetTid > 0 && currentTid > 0 && targetTid == currentTid)
                return true;

            if (!string.IsNullOrEmpty(pattern) && !string.IsNullOrEmpty(currentName) && currentName.Contains(pattern.ToLower()))
                return true;

            return false;
        }

        /// <summary>
        /// Handles NPC dialogue and multi-stage quest progression when clicked.
        /// </summary>
        public static bool TryHandleNpcQuest(Player player, string npcName, uint templateId, out string dialogue)
        {
            dialogue = string.Empty;
            if (player == null) return false;

            var quest = FindQuestForPlayerNpc(player, npcName, templateId, out var currentStep, out bool isNewQuest);
            if (quest == null) return false;

            if (!player.Quests.TryGetValue(quest.QuestID, out var pq))
            {
                pq = new PlayerQuest(quest.QuestID, QuestState.NotStarted, 1);
                player.Quests[quest.QuestID] = pq;
            }

            // A. Multi-Stage Step Progression Engine
            if (quest.Steps != null && quest.Steps.Count > 0)
            {
                return HandleMultiStepQuest(player, quest, pq, currentStep, isNewQuest, out dialogue);
            }

            // B. Single-NPC Legacy Quest Engine
            switch (pq.State)
            {
                case QuestState.NotStarted:
                    if (quest.Type == QuestType.Dialogue)
                    {
                        GrantRewards(player, quest);
                        pq.State = QuestState.Completed;
                        pq.CompletedAt = DateTime.UtcNow;
                        SavePlayerQuest(player, quest.QuestID);
                        SendQuestUpdate(player, quest.QuestID, QuestState.Completed);
                        dialogue = quest.CompleteDialogue ?? quest.IntroDialogue;
                    }
                    else
                    {
                        pq.State = QuestState.InProgress;
                        pq.Step = 1;
                        SavePlayerQuest(player, quest.QuestID);
                        SendQuestUpdate(player, quest.QuestID, QuestState.InProgress, 1);
                        dialogue = quest.IntroDialogue;
                    }
                    return true;

                case QuestState.InProgress:
                    if (quest.Type == QuestType.ItemCollection)
                    {
                        if (CheckAndConsumeItems(player, quest.RequiredItems))
                        {
                            GrantRewards(player, quest);
                            pq.State = QuestState.Completed;
                            pq.CompletedAt = DateTime.UtcNow;
                            SavePlayerQuest(player, quest.QuestID);
                            SendQuestUpdate(player, quest.QuestID, QuestState.Completed);
                            dialogue = quest.CompleteDialogue;
                        }
                        else
                        {
                            dialogue = quest.InProgressDialogue;
                        }
                    }
                    else if (quest.Type == QuestType.MonsterBattle)
                    {
                        dialogue = quest.InProgressDialogue;
                    }
                    else
                    {
                        GrantRewards(player, quest);
                        pq.State = QuestState.Completed;
                        pq.CompletedAt = DateTime.UtcNow;
                        SavePlayerQuest(player, quest.QuestID);
                        SendQuestUpdate(player, quest.QuestID, QuestState.Completed);
                        dialogue = quest.CompleteDialogue;
                    }
                    return true;

                case QuestState.Completed:
                    dialogue = quest.AlreadyCompletedDialogue ?? quest.CompleteDialogue;
                    return true;

                default:
                    dialogue = quest.IntroDialogue;
                    return true;
            }
        }

        private static bool HandleMultiStepQuest(Player player, QuestDefinition quest, PlayerQuest pq, QuestStep step, bool isNewQuest, out string dialogue)
        {
            dialogue = string.Empty;
            if (step == null) step = quest.Steps[0];

            if (pq.State == QuestState.Completed)
            {
                dialogue = quest.AlreadyCompletedDialogue ?? "Thank you again for your assistance!";
                return true;
            }

            if (isNewQuest || pq.State == QuestState.NotStarted)
            {
                pq.State = QuestState.InProgress;
                pq.Step = 1;
                SavePlayerQuest(player, quest.QuestID);
                SendQuestUpdate(player, quest.QuestID, QuestState.InProgress, 1);
                dialogue = step.PromptDialogue;
                return true;
            }

            // In Progress: Process current step
            if (step.StepType == QuestType.ItemCollection)
            {
                if (CheckAndConsumeItems(player, step.RequiredItems))
                {
                    // Grant step items if any
                    if (step.GrantItemsOnStep != null)
                    {
                        foreach (var it in step.GrantItemsOnStep)
                            player.Inv.AddItem(it.Item1, (byte)it.Item2);
                    }

                    // Advance step or complete quest
                    if (pq.Step >= quest.Steps.Count)
                    {
                        GrantRewards(player, quest);
                        pq.State = QuestState.Completed;
                        pq.CompletedAt = DateTime.UtcNow;
                        SavePlayerQuest(player, quest.QuestID);
                        SendQuestUpdate(player, quest.QuestID, QuestState.Completed);
                        dialogue = step.CompleteDialogue ?? quest.CompleteDialogue;
                    }
                    else
                    {
                        pq.Step++;
                        SavePlayerQuest(player, quest.QuestID);
                        SendQuestUpdate(player, quest.QuestID, QuestState.InProgress, (byte)pq.Step);
                        dialogue = step.CompleteDialogue;
                    }
                }
                else
                {
                    dialogue = step.InProgressDialogue;
                }
            }
            else // Dialogue / Delivery / Battle Step
            {
                // Grant step items if any (e.g. Bick handing Black Medicine)
                if (step.GrantItemsOnStep != null)
                {
                    foreach (var it in step.GrantItemsOnStep)
                        player.Inv.AddItem(it.Item1, (byte)it.Item2);
                }

                if (pq.Step >= quest.Steps.Count)
                {
                    GrantRewards(player, quest);
                    pq.State = QuestState.Completed;
                    pq.CompletedAt = DateTime.UtcNow;
                    SavePlayerQuest(player, quest.QuestID);
                    SendQuestUpdate(player, quest.QuestID, QuestState.Completed);
                    dialogue = step.CompleteDialogue ?? quest.CompleteDialogue;
                }
                else
                {
                    pq.Step++;
                    SavePlayerQuest(player, quest.QuestID);
                    SendQuestUpdate(player, quest.QuestID, QuestState.InProgress, (byte)pq.Step);
                    dialogue = step.PromptDialogue ?? step.CompleteDialogue;
                }
            }

            return true;
        }

        private static bool CheckAndConsumeItems(Player player, List<QuestRequirementItem> items)
        {
            if (items == null || items.Count == 0) return true;

            // 1. Verify player has all items
            foreach (var req in items)
            {
                bool found = false;
                for (byte slot = 1; slot <= 50; slot++)
                {
                    var it = player.Inv[slot];
                    if (it != null && it.ItemID == req.ItemID && it.Ammt >= req.Amount)
                    {
                        found = true;
                        break;
                    }
                }
                if (!found) return false;
            }

            // 2. Consume items
            foreach (var req in items)
            {
                for (byte slot = 1; slot <= 50; slot++)
                {
                    var it = player.Inv[slot];
                    if (it != null && it.ItemID == req.ItemID)
                    {
                        player.Inv.RemoveItem(slot, (byte)req.Amount, senddata: true);
                        break;
                    }
                }
            }

            return true;
        }

        private static void GrantRewards(Player player, QuestDefinition quest)
        {
            if (player == null || quest == null || quest.Reward == null) return;

            // 1. Gold
            if (quest.Reward.Gold > 0)
            {
                player.Eqs.AddGold((int)quest.Reward.Gold);
                player.Send(Tools.FromFormat("bbd", 26, 4, player.Gold));
            }

            // 2. EXP
            if (quest.Reward.Exp > 0)
            {
                player.Eqs.CurExp = (int)quest.Reward.Exp;
            }

            // 3. Items
            if (quest.Reward.Items != null && quest.Reward.Items.Count > 0)
            {
                foreach (var it in quest.Reward.Items)
                {
                    player.Inv.AddItem(it.Item1, (byte)it.Item2);
                }
            }

            // 4. Companion Pet
            if (quest.Reward.CompanionPetID > 0)
            {
                SendCompanionReward(player, quest.Reward.CompanionPetID, quest.Reward.CompanionName ?? "Companion");
            }

            // 5. Dynamic NPC Despawn (AC 22:1 state 1) for this player upon quest completion
            if (quest.DespawnNpcClickIDs != null && quest.DespawnNpcClickIDs.Count > 0)
            {
                foreach (var clickId in quest.DespawnNpcClickIDs)
                {
                    SendPacket despawnPkt = new SendPacket();
                    despawnPkt.PackArray(new byte[] { 22, 1, (byte)clickId, 0, 1 });
                    player.Send(despawnPkt);
                    DebugSystem.Write($"[QuestManager] Despawned NPC (ClickID: {clickId}) via AC 22:1 for {player.CharName} following Quest '{quest.Title}' completion.");
                }
            }

            DebugSystem.Write($"[QuestManager] Granted rewards for Quest '{quest.Title}' to {player.CharName} (Gold: +{quest.Reward.Gold}, EXP: +{quest.Reward.Exp})");
        }

        /// <summary>
        /// Synchronizes personal, client-side NPC visibility for a specific player when entering a map.
        /// Ensures despawned/completed NPCs stay hidden ONLY for players who finished the quest on this specific map.
        /// </summary>
        public static void SyncPerPlayerNpcVisibility(Player player, ushort mapId)
        {
            if (player == null || player.Quests == null) return;

            try
            {
                lock (_lock)
                {
                    foreach (var pq in player.Quests.Values)
                    {
                        if (pq.State == QuestState.Completed && _registeredQuests.TryGetValue(pq.QuestID, out var quest))
                        {
                            if (quest.MapID == mapId && quest.DespawnNpcClickIDs != null && quest.DespawnNpcClickIDs.Count > 0)
                            {
                                foreach (var clickId in quest.DespawnNpcClickIDs)
                                {
                                    SendPacket despawnPkt = new SendPacket();
                                    despawnPkt.PackArray(new byte[] { 22, 1, (byte)clickId, 0, 1 });
                                    player.Send(despawnPkt);
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[QuestManager] Error syncing per-player NPC visibility: {ex.Message}");
            }
        }

        /// <summary>
        /// <summary>
        /// Sends authentic companion recruit packets (Official AC 15:1 format matching PCAP Frame 0958).
        /// </summary>
        public static void SendCompanionReward(Player player, uint petId, string petName, bool setBattle = true)
        {
            if (player == null || petId == 0) return;

            try
            {
                // 1. AC 22:10 Hide Recruited NPC from Map (PCAP Frame 1476: 16 0a 01 00 ff ff)
                ushort npcClickId = (petId == 12178) ? (ushort)1 : (petId == 10727 ? (ushort)1 : (ushort)1);
                SendPacket hidePkt = Tools.FromFormat("bbwbb", 22, 10, npcClickId, (byte)0xFF, (byte)0xFF);
                player.Send(hidePkt);
                player.CurMap?.Broadcast(hidePkt);

                var mapNpc = (player.CurMap as GameMap)?.NpcList?.FirstOrDefault(n => n.CickID == npcClickId) as Maps.QuestNpc;
                if (mapNpc != null)
                {
                    mapNpc.IsBroken = true;
                    mapNpc.RespawnTime = DateTime.MaxValue;
                }

                // 2. AC 15:1 Authentic 54-byte Pet Recruit Packet (Byte-for-byte from PCAP Frame 0958)
                SendPacket petPkt = CreatePetPacket(player, petId, 1);
                player.Send(petPkt);

                // 3. If battle mode enabled, set active companion on map
                if (setBattle)
                {
                    player.ActivePetID = petId;
                    // AC 19:1 Set battle companion state (Frame 0958)
                    player.Send(Tools.FromFormat("bbd", 19, 1, petId));
                    player.CurMap?.Broadcast(Tools.FromFormat("bbd", 19, 1, petId));
                }

                // 4. Save to player's active pet list
                if (player.PlayerPets != null)
                {
                    player.PlayerPets[1] = new Player.PlayerPetData()
                    {
                        Slot = 1,
                        PetID = petId,
                        PetName = petName,
                        Level = 1,
                        HP = 250,
                        MaxHP = 250,
                        SP = 100,
                        MaxSP = 100,
                        Amity = 60,
                        IsBattle = setBattle,
                        IsRide = false
                    };
                }

                DebugSystem.Write($"[QuestManager] Companion {petName} (ID: {petId}) successfully recruited with authentic AC 15:1 54-byte packet!");
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[QuestManager] Error sending companion reward: {ex.Message}");
            }
        }

        public static SendPacket CreatePetPacket(Player player, uint petId, byte slot = 1, int curHp = 250, int maxHp = 250, int curSp = 100, int maxSp = 100, byte amity = 60, byte level = 1)
        {
            SendPacket petPkt = new SendPacket();
            petPkt.PackArray(new byte[] { 15, 1 });
            petPkt.Pack32(player.CharID);
            petPkt.Pack32(petId);
            petPkt.Pack8(slot);

            if (petId == 12178 || petId == 12032) // Robinson (Official baseline stats)
            {
                petPkt.Pack16(7);   // STR: 7
                petPkt.Pack16(11);  // CON: 11
                petPkt.Pack16(2);   // INT: 2
                petPkt.Pack16(4);   // WIS: 4
                petPkt.Pack16(6);   // AGI: 6
                petPkt.Pack8(1);    // Water Element
                petPkt.Pack32(level > 0 ? level : (byte)1);   // Level / Potential
                petPkt.Pack32((uint)curHp); // CurHP
                petPkt.Pack32((uint)maxHp); // MaxHP
                for (int i = 0; i < 7; i++) petPkt.Pack8(0);
                petPkt.Pack8(amity > 0 ? amity : (byte)60);   // Amity
                for (int i = 0; i < 13; i++) petPkt.Pack8(0);
            }
            else // Monkey / other companions
            {
                petPkt.Pack16(5);   // STR: 5
                petPkt.Pack16(8);   // CON: 8
                petPkt.Pack16(2);   // INT: 2
                petPkt.Pack16(3);   // WIS: 3
                petPkt.Pack16(5);   // AGI: 5
                petPkt.Pack8(0);    // Earth Element
                petPkt.Pack32(level > 0 ? level : (byte)1);   // Level
                petPkt.Pack32((uint)curHp); // CurHP
                petPkt.Pack32((uint)maxHp); // MaxHP
                for (int i = 0; i < 7; i++) petPkt.Pack8(0);
                petPkt.Pack8(amity > 0 ? amity : (byte)60);   // Amity
                for (int i = 0; i < 13; i++) petPkt.Pack8(0);
            }
            return petPkt;
        }

        /// <summary>
        /// Sends the entire quest journal list to the client using authentic AC 24 Sub 4 packet.
        /// </summary>
        public static void SendQuestJournal(Player player)
        {
            if (player == null) return;
            try
            {
                if (player.Quests != null && player.Quests.Count > 0)
                {
                    SendPacket pkt = new SendPacket();
                    pkt.PackArray(new byte[] { 24, 4 });
                    pkt.Pack16((ushort)player.Quests.Count);
                    foreach (var kvp in player.Quests)
                    {
                        pkt.Pack16((ushort)kvp.Key);
                        pkt.Pack8((byte)(kvp.Value.State == QuestState.Completed ? 255 : (byte)kvp.Value.State));
                        pkt.Pack8((byte)kvp.Value.State);
                    }
                    player.Send(pkt);
                    DebugSystem.Write($"[QuestManager] Sent AC 24:4 Journal ({player.Quests.Count} quests) to {player.CharName}");
                }
                else
                {
                    player.Send(Tools.FromFormat("bbw", 24, 4, (ushort)0));
                }
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[QuestManager] Error sending quest journal: {ex.Message}");
            }
        }

        /// <summary>
        /// Synchronizes all active quest step flags and completed flags to the client for PreEvent evaluation.
        /// </summary>
        public static void SendAllQuestFlags(Player player)
        {
            if (player == null) return;
            try
            {
                // 1. Send full quest journal list
                SendQuestJournal(player);

                // 2. Dispatch AC 24:1 step flags and AC 24:5 completed flags
                if (player.Quests != null && player.Quests.Count > 0)
                {
                    foreach (var kvp in player.Quests)
                    {
                        uint qId = kvp.Key;
                        var pq = kvp.Value;
                        if (pq.State == QuestState.Completed)
                        {
                            player.Send(Tools.FromFormat("bbwb", 24, 5, (ushort)qId, (byte)1));
                        }
                        else if (pq.State == QuestState.InProgress)
                        {
                            byte step = (byte)Math.Max(1, pq.Step);
                            player.Send(Tools.FromFormat("bbwb", 24, 1, (ushort)qId, step));
                            player.Send(Tools.FromFormat("bbwb", 24, 2, (ushort)qId, step));
                        }
                    }
                    DebugSystem.Write($"[QuestManager] Synchronized {player.Quests.Count} quest flags to {player.CharName}");
                }
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[QuestManager] Error in SendAllQuestFlags for {player.CharName}: {ex.Message}");
            }
        }

        /// <summary>
        /// Sends authentic AC 24 quest state updates (Sub 1: Start, Sub 2: Progress, Sub 5: Completed, Sub 3: Abandoned).
        /// </summary>
        public static void SendQuestUpdate(Player player, uint questId, QuestState state, byte step = 1)
        {
            if (player == null) return;

            try
            {
                switch (state)
                {
                    case QuestState.InProgress:
                        // AC 24 Sub 1 (Quest Accepted/Started) + AC 24 Sub 2 (Step update)
                        player.Send(Tools.FromFormat("bbwb", 24, 1, (ushort)questId, step));
                        player.Send(Tools.FromFormat("bbwb", 24, 2, (ushort)questId, step));
                        break;

                    case QuestState.Completed:
                        // AC 24 Sub 5 (Quest Completed / Flagged)
                        player.Send(Tools.FromFormat("bbwb", 24, 5, (ushort)questId, (byte)state));
                        break;

                    case QuestState.Failed:
                        // AC 24 Sub 3 (Quest Failed / Reset)
                        player.Send(Tools.FromFormat("bbw", 24, 3, (ushort)questId));
                        break;

                    default:
                        player.Send(Tools.FromFormat("bbwb", 24, 5, (ushort)questId, (byte)state));
                        break;
                }
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[QuestManager] Error sending quest update: {ex.Message}");
            }
        }

        /// <summary>
        /// Loads player's active and completed quests from database upon login.
        /// </summary>
        public static void LoadPlayerQuests(Player player)
        {
            if (player == null) return;

            player.Quests.Clear();

            try
            {
                var db = (RCLibrary.Core.DataBase)DataBase.CharacterDataBase.GlobalInstance ?? (RCLibrary.Core.DataBase)DataBase.GameDataBase.GlobalInstance;
                if (db != null)
                {
                    var dt = db.GetDataTable($"SELECT quest_started, quest_pos FROM charquest WHERE charID={player.CharID}");
                    if (dt != null && dt.Rows.Count > 0)
                    {
                        foreach (System.Data.DataRow row in dt.Rows)
                        {
                            uint qId = Convert.ToUInt32(row["quest_started"]);
                            byte qPos = Convert.ToByte(row["quest_pos"]);
                            player.Quests[qId] = new PlayerQuest(qId, (QuestState)qPos);
                        }
                    }
                }
                DebugSystem.Write($"[QuestManager] Loaded {player.Quests.Count} quests for {player.CharName} from DB.");
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[QuestManager] Error loading quests for {player.CharName}: {ex.Message}");
            }
        }

        /// <summary>
        /// Saves or updates a specific quest state in the database.
        /// </summary>
        public static void SavePlayerQuest(Player player, uint questId)
        {
            if (player == null) return;

            try
            {
                var db = (RCLibrary.Core.DataBase)DataBase.CharacterDataBase.GlobalInstance ?? (RCLibrary.Core.DataBase)DataBase.GameDataBase.GlobalInstance;
                if (db != null && player.Quests != null && player.Quests.TryGetValue(questId, out var pq))
                {
                    var existing = db.GetDataTable($"SELECT pri_key FROM charquest WHERE charID={player.CharID} AND quest_started={questId} LIMIT 1");
                    if (existing != null && existing.Rows.Count > 0)
                    {
                        db.ExecuteNonQuery($"UPDATE charquest SET quest_pos={(byte)pq.State} WHERE charID={player.CharID} AND quest_started={questId}");
                    }
                    else
                    {
                        db.ExecuteNonQuery($"INSERT INTO charquest (charID, quest_started, quest_pos) VALUES ({player.CharID}, {questId}, {(byte)pq.State})");
                    }
                }
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[QuestManager] Error saving quest {questId} for {player.CharName}: {ex.Message}");
            }
        }
    }
}
