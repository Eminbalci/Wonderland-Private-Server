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
        private static readonly Dictionary<uint, QuestDefinition> _masterQuests = new Dictionary<uint, QuestDefinition>();
        private static readonly object _lock = new object();

        public static IReadOnlyDictionary<uint, QuestDefinition> AllQuests => _registeredQuests;
        public static IReadOnlyDictionary<uint, QuestDefinition> MasterQuests => _masterQuests;
        public static int Count => _registeredQuests.Count;
        public static int MasterCount => _masterQuests.Count;

        static QuestManager()
        {
            InitializeQuests();
        }

        public static void InitializeQuests()
        {
            lock (_lock)
            {
                _registeredQuests.Clear();
                _masterQuests.Clear();

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
        /// Extracts real titles, descriptions, multi-step #01/#02/#99 stage progressions,
        /// and organizes raw 2,154 Mark entries into structured Master Quests with categories and paired In-Progress/Completed flags.
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
                    var parsedList = new List<ParsedMark>();

                    for (uint markId = 1; markId <= numRecords; markId++)
                    {
                        int offset = (int)((markId - 1) * 553);
                        var entry = ParseMarkEntry(data, offset, markId);
                        if (entry != null && !string.IsNullOrWhiteSpace(entry.Title))
                        {
                            parsedList.Add(entry);

                            var quest = new QuestDefinition(markId, entry.Title, entry.NpcPattern ?? entry.Title, QuestType.Dialogue)
                            {
                                MapID = ResolveDefaultMapId(entry.Location, entry.Title, entry.Description),
                                NpcTemplateID = ResolveDefaultNpcTid(entry.NpcPattern, entry.Title, entry.Location),
                                Description = entry.Description ?? entry.Title,
                                IntroDialogue = entry.IntroDialogue ?? entry.Description ?? entry.Title,
                                InProgressDialogue = entry.InProgressDialogue ?? entry.Description ?? entry.Title,
                                CompleteDialogue = entry.CompletedSummary ?? entry.Description ?? entry.Title,
                                AlreadyCompletedDialogue = entry.CompletedSummary ?? entry.Description ?? entry.Title,
                                Category = DetermineQuestCategory(entry.Title, entry.Location),
                                AreaName = entry.Location ?? "Unknown",
                                InProgressMarkID = markId,
                                CompletedMarkID = markId,
                                Reward = GenerateDefaultReward(entry.Title, entry.NpcPattern)
                            };

                            // Populate multi-stage steps (#01, #02, #99)
                            if (entry.StepDescriptions != null && entry.StepDescriptions.Count > 0)
                            {
                                for (int i = 0; i < entry.StepDescriptions.Count; i++)
                                {
                                    var stepDesc = entry.StepDescriptions[i];
                                    var sType = DetermineStepType(stepDesc);
                                    var sTarget = DetermineStepTarget(stepDesc, quest.NpcNamePattern);

                                    quest.AddStep(new QuestStep(i + 1, sTarget, sType)
                                    {
                                        TargetNpcTemplateID = quest.NpcTemplateID,
                                        PromptDialogue = stepDesc,
                                        InProgressDialogue = stepDesc,
                                        CompleteDialogue = (i == entry.StepDescriptions.Count - 1 && !string.IsNullOrEmpty(entry.CompletedSummary)) ? entry.CompletedSummary : stepDesc
                                    });
                                }
                            }

                            _registeredQuests[markId] = quest;
                            loadedCount++;
                        }
                    }

                    // Build structured Master Quests by grouping paired in-progress and completed marks
                    BuildMasterQuests(parsedList);
                }

                DebugSystem.Write($"[QuestManager] Loaded {loadedCount} authentic Mark.dat entries into {_masterQuests.Count} structured Master Quests.");
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[QuestManager] Error reading Mark.dat: {ex.Message}");
            }
        }

        public static QuestReward GenerateDefaultReward(string title, string npcPattern)
        {
            string full = (title + " " + npcPattern).ToLower();

            if (full.Contains("sasha")) return new QuestReward(2000, 5000);
            if (full.Contains("roca")) return new QuestReward(1500, 4000);
            if (full.Contains("niss")) return new QuestReward(1500, 4000);
            if (full.Contains("clive")) return new QuestReward(3000, 8000);
            if (full.Contains("sam")) return new QuestReward(2500, 6000);
            if (full.Contains("elin")) return new QuestReward(3500, 10000);
            if (full.Contains("shizune")) return new QuestReward(4000, 12000);
            if (full.Contains("victoria")) return new QuestReward(4500, 15000);
            if (full.Contains("angela")) return new QuestReward(5000, 18000);
            if (full.Contains("eva")) return new QuestReward(5000, 20000);
            if (full.Contains("robinson")) return new QuestReward(3000, 10000);

            return new QuestReward(500, 1000);
        }

        public static QuestType DetermineStepType(string text)
        {
            string t = (text ?? "").ToLower();
            if (t.Contains("defeat") || t.Contains("kill") || t.Contains("monster") || (t.Contains("guard") && t.Contains("save")) || t.Contains("battle"))
                return QuestType.MonsterBattle;
            if (t.Contains("give") || t.Contains("bring") || t.Contains("collect") || t.Contains("water") || t.Contains("item") || t.Contains("wine") || t.Contains("egg"))
                return QuestType.ItemCollection;
            if (t.Contains("door") || t.Contains("maze") || t.Contains("cave") || t.Contains("find") || t.Contains("go ahead") || t.Contains("leave here") || t.Contains("drift ashore") || t.Contains("grovel") || t.Contains("reach") || t.Contains("nobody there"))
                return QuestType.Exploration;
            return QuestType.Dialogue;
        }

        public static string DetermineStepTarget(string text, string defaultNpc)
        {
            string t = (text ?? "").ToLower();
            if (t.Contains("stone door") || t.Contains("secret door") || t.Contains("door")) return "Stone Door / Entrance";
            if (t.Contains("alien base") || t.Contains("mayan cave") || t.Contains("cave")) return "Secret Cave Passage";
            if (t.Contains("astrologer")) return "Astrologer";
            if (t.Contains("matchstick girl")) return "Matchstick Girl";
            if (t.Contains("father")) return "Father";
            if (t.Contains("roca")) return "Roca";
            if (t.Contains("sasha")) return "Sasha";
            if (t.Contains("monkey")) return "Little Monkey";
            if (t.Contains("priest")) return "Priest";
            if (t.Contains("guard")) return "Guard";
            if (t.Contains("leader") || t.Contains("chief")) return "Village Leader";
            if (t.Contains("dentist")) return "Dentist";
            if (t.Contains("zhuang zhi")) return "Zhuang Zhi";
            if (t.Contains("granny")) return "Granny";
            if (t.Contains("villager")) return "Villager";
            return string.IsNullOrEmpty(defaultNpc) ? "Quest NPC" : defaultNpc;
        }

        public static string DetermineQuestCategory(string title, string area)
        {
            string t = (title ?? "").ToLower();
            string a = (area ?? "").ToLower();

            if (t.Contains("roca") || t.Contains("niss") || t.Contains("clive") || t.Contains("sasha") ||
                t.Contains("xaolan") || t.Contains("sam") || t.Contains("shizune") ||
                t.Contains("elin") || t.Contains("victoria") || t.Contains("angela") ||
                t.Contains("suzuru") || t.Contains("eva") || t.Contains("robinson") ||
                t.Contains("fred") || t.Contains("magellan") || t.Contains("kanako") ||
                t.Contains("charlotte") || t.Contains("rebirth") || t.Contains("reincarnation") || t.Contains("skill master"))
            {
                return " Companion & Rebirth";
            }
            if (t.Contains("raft") || t.Contains("canoe") || t.Contains("ship") ||
                t.Contains("boat") || t.Contains("airplane") || t.Contains("rocket") ||
                t.Contains("ufo") || t.Contains("tent") || t.Contains("craftsman") ||
                t.Contains("alchemy") || t.Contains("make a"))
            {
                return " Crafting & Vehicles";
            }
            if (t.Contains("whack") || t.Contains("collect") || t.Contains("contest") ||
                t.Contains("quiz") || t.Contains("test") || t.Contains("game"))
            {
                return " Minigames & Challenges";
            }
            if (t.Contains("zodiac") || t.Contains("trial") || t.Contains("ghost") ||
                t.Contains("dragon") || t.Contains("round") || t.Contains("palace") ||
                t.Contains("tower") || t.Contains("cave") || t.Contains("pirate"))
            {
                return " Dungeons & Instances";
            }
            return " Storyline & Area";
        }

        public static ushort ResolveDefaultMapId(string area, string title, string description)
        {
            string full = (area + " " + title + " " + description).ToLower();

            if (full.Contains("cathedral") || full.Contains("church")) return 10017;
            if (full.Contains("kelan")) return 10001;
            if (full.Contains("holy village")) return 10010;
            if (full.Contains("weiling") || full.Contains("welling")) return 10020;
            if (full.Contains("south pole") || full.Contains("iceberg") || full.Contains("matchstick")) return 11001;
            if (full.Contains("kelp")) return 12001;
            if (full.Contains("japan") || full.Contains("kyoto") || full.Contains("edo")) return 13001;
            if (full.Contains("china") || full.Contains("chang'an") || full.Contains("great wall")) return 14001;
            if (full.Contains("egypt") || full.Contains("pyramid") || full.Contains("nile")) return 15001;
            if (full.Contains("maya")) return 16001;
            if (full.Contains("persia")) return 17001;
            if (full.Contains("rome") || full.Contains("colosseum")) return 18001;
            if (full.Contains("athens") || full.Contains("greece") || full.Contains("athenian")) return 19001;
            if (full.Contains("dragon palace") || full.Contains("dragon ball")) return 21001;
            if (full.Contains("ghost ship") || full.Contains("pirate")) return 22001;
            if (full.Contains("bangkok") || full.Contains("thailand") || full.Contains("siam")) return 23001;
            if (full.Contains("india") || full.Contains("taj mahal")) return 24001;
            if (full.Contains("australia") || full.Contains("sydney")) return 25001;
            if (full.Contains("hawaii") || full.Contains("honolulu")) return 26001;
            if (full.Contains("korea") || full.Contains("seoul")) return 27001;
            if (full.Contains("cornwell") || full.Contains("cornwall")) return 28001;
            if (full.Contains("south island")) return 11000;

            return 10000; // North Island
        }

        private static Dictionary<uint, string> _npcNameCache = null;
        private static Dictionary<string, uint> _npcTidByName = null;

        private static void EnsureNpcCache()
        {
            if (_npcNameCache != null) return;
            _npcNameCache = new Dictionary<uint, string>();
            _npcTidByName = new Dictionary<string, uint>(StringComparer.OrdinalIgnoreCase);

            try
            {
                var allNames = Game.DataFiles.SceneDataManager.GetAllNpcNames();
                if (allNames != null)
                {
                    foreach (var kvp in allNames)
                    {
                        _npcNameCache[kvp.Key] = kvp.Value;
                        if (!_npcTidByName.ContainsKey(kvp.Value))
                        {
                            _npcTidByName[kvp.Value] = kvp.Key;
                        }
                    }
                }
            }
            catch
            {
            }
        }

        public static string GetNpcName(uint tid)
        {
            EnsureNpcCache();
            if (_npcNameCache != null && _npcNameCache.TryGetValue(tid, out string name) && !string.IsNullOrWhiteSpace(name))
                return name;
            return $"Companion #{tid}";
        }

        public static uint ResolveDefaultNpcTid(string pattern, string title, string area)
        {
            if (string.IsNullOrWhiteSpace(pattern) && string.IsNullOrWhiteSpace(title))
                return 0;

            EnsureNpcCache();

            if (!string.IsNullOrWhiteSpace(pattern))
            {
                string p = pattern.Trim();
                if (_npcTidByName.TryGetValue(p, out uint tid)) return tid;

                string pLower = p.ToLower();
                foreach (var kvp in _npcTidByName)
                {
                    string nLower = kvp.Key.ToLower();
                    if (nLower == pLower || nLower.Contains(pLower) || pLower.Contains(nLower))
                    {
                        return kvp.Value;
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(title))
            {
                string tLower = title.ToLower().Trim();
                foreach (var kvp in _npcTidByName)
                {
                    if (kvp.Key.Length < 3) continue;
                    string nLower = kvp.Key.ToLower();
                    if (tLower.Contains(nLower))
                    {
                        return kvp.Value;
                    }
                }
            }

            return 0;
        }

        public static string ExtractNpcPattern(string title, string body)
        {
            EnsureNpcCache();
            string full = ((title ?? "") + " " + (body ?? "")).ToLower().Trim();

            foreach (var kvp in _npcTidByName)
            {
                if (kvp.Key.Length < 3) continue;
                if (full.Contains(kvp.Key.ToLower()))
                {
                    return kvp.Key;
                }
            }

            string cleanedTitle = CleanString(title);
            cleanedTitle = System.Text.RegularExpressions.Regex.Replace(cleanedTitle, @"^(Don't Leave!|Death of|Save|Help|Find|The|A)\s*", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase).Trim('!', '?', '.', ' ');
            return string.IsNullOrEmpty(cleanedTitle) ? CleanString(title) : cleanedTitle;
        }

        public static string ExtractAreaFromText(string title, string body)
        {
            string full = (title + " " + body).ToLower();

            if (full.Contains("south pole") || full.Contains("iceberg") || full.Contains("glacier")) return "South Pole";
            if (full.Contains("mayan") || full.Contains("alien base") || full.Contains("stone door") || full.Contains("dentist")) return "Maya";
            if (full.Contains("kelan") || full.Contains("kelan village") || full.Contains("kelan shore")) return "Kelan Village";
            if (full.Contains("weiling") || full.Contains("welling")) return "Welling Village";
            if (full.Contains("holy village") || full.Contains("cathedral") || full.Contains("church")) return "Holy Village";
            if (full.Contains("north island")) return "North Island";
            if (full.Contains("south island")) return "South Island";
            if (full.Contains("kelp") || full.Contains("kelp island")) return "Kelp Island";
            if (full.Contains("japan") || full.Contains("kyoto") || full.Contains("edo")) return "Japan";
            if (full.Contains("china") || full.Contains("chang'an") || full.Contains("great wall")) return "China";
            if (full.Contains("egypt") || full.Contains("pyramid") || full.Contains("nile")) return "Egypt";
            if (full.Contains("maya")) return "Maya";
            if (full.Contains("persia") || full.Contains("persian")) return "Persia";
            if (full.Contains("rome") || full.Contains("roman") || full.Contains("colosseum")) return "Rome";
            if (full.Contains("athens") || full.Contains("greece") || full.Contains("athenian")) return "Athens";
            if (full.Contains("dragon palace") || full.Contains("dragon ball")) return "Dragon Palace";
            if (full.Contains("ghost ship") || full.Contains("pirate ship")) return "Ghost Ship";
            if (full.Contains("bangkok") || full.Contains("thailand") || full.Contains("siam")) return "Bangkok";
            if (full.Contains("india") || full.Contains("taj mahal")) return "India";
            if (full.Contains("australia") || full.Contains("sydney")) return "Australia";
            if (full.Contains("hawaii") || full.Contains("honolulu")) return "Hawaii";
            if (full.Contains("korea") || full.Contains("seoul")) return "Korea";
            if (full.Contains("cornwell") || full.Contains("cornwall")) return "Cornwell";

            return "North Island";
        }

        private static void BuildMasterQuests(List<ParsedMark> marks)
        {
            _masterQuests.Clear();
            var processed = new HashSet<uint>();

            for (int i = 0; i < marks.Count; i++)
            {
                var m = marks[i];
                if (processed.Contains(m.MarkID)) continue;

                var master = new QuestDefinition(m.MarkID, m.Title, m.NpcPattern ?? m.Title, QuestType.Dialogue)
                {
                    MapID = ResolveDefaultMapId(m.Location, m.Title, m.Description),
                    NpcTemplateID = ResolveDefaultNpcTid(m.NpcPattern, m.Title, m.Location),
                    Description = m.Description ?? m.Title,
                    IntroDialogue = m.IntroDialogue ?? m.Description ?? m.Title,
                    InProgressDialogue = m.InProgressDialogue ?? m.Description ?? m.Title,
                    CompleteDialogue = m.CompletedSummary ?? m.Description ?? m.Title,
                    AlreadyCompletedDialogue = m.CompletedSummary ?? m.Description ?? m.Title,
                    Category = DetermineQuestCategory(m.Title, m.Location),
                    AreaName = m.Location ?? "Unknown",
                    InProgressMarkID = m.MarkID,
                    CompletedMarkID = m.MarkID,
                    Reward = GenerateDefaultReward(m.Title, m.NpcPattern)
                };

                // Add multi-stage steps to master
                if (m.StepDescriptions != null && m.StepDescriptions.Count > 0)
                {
                    for (int s = 0; s < m.StepDescriptions.Count; s++)
                    {
                        var sText = m.StepDescriptions[s];
                        var sType = DetermineStepType(sText);
                        var sTarget = DetermineStepTarget(sText, master.NpcNamePattern);

                        master.AddStep(new QuestStep(s + 1, sTarget, sType)
                        {
                            TargetNpcTemplateID = master.NpcTemplateID,
                            PromptDialogue = sText,
                            InProgressDialogue = sText,
                            CompleteDialogue = (s == m.StepDescriptions.Count - 1 && !string.IsNullOrEmpty(m.CompletedSummary)) ? m.CompletedSummary : sText
                        });
                    }
                }

                master.AllLinkedMarkIDs.Add(m.MarkID);
                processed.Add(m.MarkID);

                // Check next entry for paired completion mark
                if (i + 1 < marks.Count)
                {
                    var nextM = marks[i + 1];
                    string nextTitle = nextM.Title?.Trim() ?? "";
                    if (nextTitle.Equals(m.Title, StringComparison.OrdinalIgnoreCase) ||
                        nextTitle.StartsWith(m.Title, StringComparison.OrdinalIgnoreCase) ||
                        m.Title.StartsWith(nextTitle, StringComparison.OrdinalIgnoreCase))
                    {
                        master.CompletedMarkID = nextM.MarkID;
                        master.AllLinkedMarkIDs.Add(nextM.MarkID);
                        if (!string.IsNullOrEmpty(nextM.CompletedSummary))
                        {
                            master.CompleteDialogue = nextM.CompletedSummary;
                            master.AlreadyCompletedDialogue = nextM.CompletedSummary;
                        }
                        processed.Add(nextM.MarkID);
                        i++; // merged pair
                    }
                }

                _masterQuests[master.QuestID] = master;
            }
        }

        private class ParsedMark
        {
            public uint MarkID { get; set; }
            public string Title { get; set; }
            public string Location { get; set; }
            public string Description { get; set; }
            public string IntroDialogue { get; set; }
            public string InProgressDialogue { get; set; }
            public string CompletedSummary { get; set; }
            public string NpcPattern { get; set; }
            public List<string> StepDescriptions { get; set; } = new List<string>();
        }

        private static ParsedMark ParseMarkEntry(byte[] data, int offset, uint markId)
        {
            try
            {
                // 1. Extract Title from [200..265]
                string title = ExtractReversedString(data, offset + 200, 65);
                if (string.IsNullOrWhiteSpace(title) || 
                    title.StartsWith("Visit Mark", StringComparison.OrdinalIgnoreCase) || 
                    title.StartsWith("Time Mark", StringComparison.OrdinalIgnoreCase) ||
                    title.StartsWith("Quest Mark", StringComparison.OrdinalIgnoreCase))
                    return null;

                // 2. Extract Body from [266..525]
                string body = ExtractReversedString(data, offset + 266, 260);
                if (string.IsNullOrWhiteSpace(body) || 
                    body.StartsWith("Visit Mark", StringComparison.OrdinalIgnoreCase) || 
                    body.StartsWith("Time Mark", StringComparison.OrdinalIgnoreCase) ||
                    body.StartsWith("Visit Mar", StringComparison.OrdinalIgnoreCase) ||
                    body.StartsWith("Time Mar", StringComparison.OrdinalIgnoreCase) ||
                    body == "Quest Mark" ||
                    (body == title && (title == "North Island" || title == "South Island" || title == "Maya" || title == "Japan" || title == "China" || title == "Egypt")))
                {
                    // Internal GPS coordinate marker or sightseeing flag in Mark.dat -> filter out
                    return null;
                }

                var mark = new ParsedMark
                {
                    MarkID = markId,
                    Title = CleanString(title),
                    NpcPattern = ExtractNpcPattern(title, body)
                };

                mark.Location = ExtractAreaFromText(mark.Title, body);

                // 3. Parse #01, #02, #03, #99 steps
                if (body.Contains("#"))
                {
                    var matches = System.Text.RegularExpressions.Regex.Matches(body, @"#(\d{2})([^#]*)");
                    foreach (System.Text.RegularExpressions.Match m in matches)
                    {
                        string stepNum = m.Groups[1].Value;
                        string stepText = CleanString(m.Groups[2].Value);
                        if (string.IsNullOrEmpty(stepText) || stepText == "s'" || stepText == "'s") continue;

                        if (stepNum == "99")
                        {
                            mark.CompletedSummary = stepText;
                        }
                        else
                        {
                            mark.StepDescriptions.Add(stepText);
                        }
                    }

                    if (mark.StepDescriptions.Count > 0)
                    {
                        mark.IntroDialogue = mark.StepDescriptions[0];
                    }
                    if (mark.StepDescriptions.Count > 1)
                    {
                        mark.InProgressDialogue = string.Join(" ", mark.StepDescriptions.GetRange(1, Math.Min(2, mark.StepDescriptions.Count - 1)));
                    }
                    else if (mark.StepDescriptions.Count > 0)
                    {
                        mark.InProgressDialogue = mark.StepDescriptions[0];
                    }

                    if (!string.IsNullOrEmpty(mark.CompletedSummary))
                    {
                        mark.Description = mark.CompletedSummary;
                    }
                    else if (mark.StepDescriptions.Count > 0)
                    {
                        mark.Description = mark.StepDescriptions[0];
                    }
                    else
                    {
                        mark.Description = CleanString(body);
                    }
                }
                else
                {
                    mark.Description = CleanString(body);
                    mark.IntroDialogue = mark.Description;
                    mark.InProgressDialogue = mark.Description;
                    mark.CompletedSummary = mark.Description;
                }

                return mark;
            }
            catch
            {
                return null;
            }
        }

        private static string ExtractReversedString(byte[] data, int start, int maxLen)
        {
            List<char> chars = new List<char>();
            int end = Math.Min(data.Length, start + maxLen);
            for (int i = start; i < end; i++)
            {
                byte b = data[i];
                if (b >= 32 && b <= 126)
                {
                    chars.Add((char)b);
                }
                else
                {
                    if (chars.Count >= 3)
                    {
                        chars.Reverse();
                        string s = new string(chars.ToArray()).Trim();
                        if (!s.Contains("'s's's") && s != "0" && s != "s'" && s != "'s") return s;
                    }
                    chars.Clear();
                }
            }
            if (chars.Count >= 3)
            {
                chars.Reverse();
                string s = new string(chars.ToArray()).Trim();
                if (!s.Contains("'s's's") && s != "0" && s != "s'" && s != "'s") return s;
            }
            return "";
        }

        private static string CleanString(string s)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;
            string cleaned = s.Replace("s's's's's'", "").Replace("'s's's's's'", "").Replace("s's's", "").Trim();
            if (cleaned == "s'" || cleaned == "'s" || cleaned == "0") return string.Empty;

            var sb = new System.Text.StringBuilder();
            foreach (char c in cleaned)
            {
                if (c >= 32 && c <= 126 && c != '&' && c != '$' && c != '`')
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
                                    if (IsNpcMatch(step.TargetNpcPattern, step.TargetNpcTemplateID, lower, templateId) ||
                                        IsNpcMatch(q.NpcNamePattern, q.NpcTemplateID, lower, templateId) ||
                                        IsNpcMatch(q.Title, q.NpcTemplateID, lower, templateId))
                                    {
                                        matchingStep = step;
                                        return q;
                                    }
                                }
                            }
                            else if (IsNpcMatch(q.NpcNamePattern, q.NpcTemplateID, lower, templateId) ||
                                     IsNpcMatch(q.Title, q.NpcTemplateID, lower, templateId))
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
                        if (IsNpcMatch(firstStep.TargetNpcPattern, firstStep.TargetNpcTemplateID, lower, templateId) ||
                            IsNpcMatch(q.NpcNamePattern, q.NpcTemplateID, lower, templateId) ||
                            IsNpcMatch(q.Title, q.NpcTemplateID, lower, templateId))
                        {
                            matchingStep = firstStep;
                            isNewQuest = true;
                            return q;
                        }
                    }
                    else if (IsNpcMatch(q.NpcNamePattern, q.NpcTemplateID, lower, templateId) ||
                             IsNpcMatch(q.Title, q.NpcTemplateID, lower, templateId))
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
                            if (IsNpcMatch(q.NpcNamePattern, q.NpcTemplateID, lower, templateId) ||
                                IsNpcMatch(q.Title, q.NpcTemplateID, lower, templateId))
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

            if (!string.IsNullOrEmpty(pattern) && !string.IsNullOrEmpty(currentName))
            {
                string p = pattern.ToLower().Trim();
                string c = currentName.ToLower().Trim();
                if (c == p || c.Contains(p) || p.Contains(c))
                    return true;
            }

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
            if (quest.Reward != null && quest.Reward.CompanionPetID != 0 &&
                !CanRecruitCompanion(player, quest.Reward.CompanionPetID))
            {
                dialogue = "Cannot recruit companion. Check free party slots and Pet Hotel.";
                return true;
            }

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
                        SyncPerPlayerNpcVisibility(player, (ushort)player.MapID);
                        dialogue = quest.CompleteDialogue ?? quest.IntroDialogue;
                    }
                    else
                    {
                        pq.State = QuestState.InProgress;
                        pq.Step = 1;
                        SavePlayerQuest(player, quest.QuestID);
                        SendQuestUpdate(player, quest.QuestID, QuestState.InProgress, 1);
                        SyncPerPlayerNpcVisibility(player, (ushort)player.MapID);
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
                            SyncPerPlayerNpcVisibility(player, (ushort)player.MapID);
                            dialogue = quest.CompleteDialogue;
                        }
                        else
                        {
                            dialogue = quest.InProgressDialogue;
                        }
                    }
                    else if (quest.Type == QuestType.MonsterBattle)
                    {
                        int reqKills = quest.RequiredKillCount > 0 ? quest.RequiredKillCount : 1;
                        if (pq.CurrentKillCount >= reqKills)
                        {
                            GrantRewards(player, quest);
                            pq.State = QuestState.Completed;
                            pq.CompletedAt = DateTime.UtcNow;
                            SavePlayerQuest(player, quest.QuestID);
                            SendQuestUpdate(player, quest.QuestID, QuestState.Completed);
                            SyncPerPlayerNpcVisibility(player, (ushort)player.MapID);
                            dialogue = quest.CompleteDialogue;
                        }
                        else
                        {
                            dialogue = quest.InProgressDialogue + $" ({pq.CurrentKillCount}/{reqKills})";
                        }
                    }
                    else
                    {
                        GrantRewards(player, quest);
                        pq.State = QuestState.Completed;
                        pq.CompletedAt = DateTime.UtcNow;
                        SavePlayerQuest(player, quest.QuestID);
                        SendQuestUpdate(player, quest.QuestID, QuestState.Completed);
                        SyncPerPlayerNpcVisibility(player, (ushort)player.MapID);
                        dialogue = quest.CompleteDialogue;
                    }
                    return true;

                case QuestState.Completed:
                    if (quest.IsRepeatable || quest.IsDaily)
                    {
                        bool canRepeat = false;
                        if (quest.IsDaily && pq.CompletedAt.HasValue && pq.CompletedAt.Value.Date < DateTime.UtcNow.Date)
                        {
                            canRepeat = true;
                        }
                        else if (quest.IsRepeatable && (!pq.CompletedAt.HasValue || quest.CooldownMinutes == 0 || DateTime.UtcNow >= pq.CompletedAt.Value.AddMinutes(quest.CooldownMinutes)))
                        {
                            canRepeat = true;
                        }

                        if (canRepeat)
                        {
                            pq.State = QuestState.InProgress;
                            pq.Step = 1;
                            pq.CurrentKillCount = 0;
                            pq.StartedAt = DateTime.UtcNow;
                            pq.CompletedAt = null;
                            SavePlayerQuest(player, quest.QuestID);
                            SendQuestUpdate(player, quest.QuestID, QuestState.InProgress, 1);
                            SyncPerPlayerNpcVisibility(player, (ushort)player.MapID);
                            dialogue = quest.IntroDialogue;
                            return true;
                        }
                    }
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
                if (quest.IsRepeatable || quest.IsDaily)
                {
                    bool canRepeat = false;
                    if (quest.IsDaily && pq.CompletedAt.HasValue && pq.CompletedAt.Value.Date < DateTime.UtcNow.Date)
                    {
                        canRepeat = true;
                    }
                    else if (quest.IsRepeatable && (!pq.CompletedAt.HasValue || quest.CooldownMinutes == 0 || DateTime.UtcNow >= pq.CompletedAt.Value.AddMinutes(quest.CooldownMinutes)))
                    {
                        canRepeat = true;
                    }

                    if (canRepeat)
                    {
                        pq.State = QuestState.InProgress;
                        pq.Step = 1;
                        pq.CurrentKillCount = 0;
                        pq.StartedAt = DateTime.UtcNow;
                        pq.CompletedAt = null;
                        SavePlayerQuest(player, quest.QuestID);
                        SendQuestUpdate(player, quest.QuestID, QuestState.InProgress, 1);
                        SyncPerPlayerNpcVisibility(player, (ushort)player.MapID);
                        dialogue = step.PromptDialogue ?? quest.IntroDialogue;
                        return true;
                    }
                }
                dialogue = quest.AlreadyCompletedDialogue ?? "Thank you again for your assistance!";
                return true;
            }

            if (isNewQuest || pq.State == QuestState.NotStarted)
            {
                pq.State = QuestState.InProgress;
                pq.Step = 1;
                SavePlayerQuest(player, quest.QuestID);
                SendQuestUpdate(player, quest.QuestID, QuestState.InProgress, 1);
                SyncPerPlayerNpcVisibility(player, (ushort)player.MapID);
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
                            GrantItemReward(player, it.Item1, it.Item2);
                    }

                    // Advance step or complete quest
                    if (pq.Step >= quest.Steps.Count)
                    {
                        GrantRewards(player, quest);
                        pq.State = QuestState.Completed;
                        pq.CompletedAt = DateTime.UtcNow;
                        SavePlayerQuest(player, quest.QuestID);
                        SendQuestUpdate(player, quest.QuestID, QuestState.Completed);
                        SyncPerPlayerNpcVisibility(player, (ushort)player.MapID);
                        dialogue = step.CompleteDialogue ?? quest.CompleteDialogue;
                    }
                    else
                    {
                        pq.Step++;
                        pq.CurrentKillCount = 0;
                        SavePlayerQuest(player, quest.QuestID);
                        SendQuestUpdate(player, quest.QuestID, QuestState.InProgress, (byte)pq.Step);
                        SyncPerPlayerNpcVisibility(player, (ushort)player.MapID);
                        dialogue = step.CompleteDialogue;
                    }
                }
                else
                {
                    dialogue = step.InProgressDialogue;
                }
            }
            else if (step.StepType == QuestType.MonsterBattle)
            {
                int reqKills = step.RequiredKillCount > 0 ? step.RequiredKillCount : 1;
                if (pq.CurrentKillCount >= reqKills)
                {
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
                        SyncPerPlayerNpcVisibility(player, (ushort)player.MapID);
                        dialogue = step.CompleteDialogue ?? quest.CompleteDialogue;
                    }
                    else
                    {
                        pq.Step++;
                        pq.CurrentKillCount = 0;
                        SavePlayerQuest(player, quest.QuestID);
                        SendQuestUpdate(player, quest.QuestID, QuestState.InProgress, (byte)pq.Step);
                        SyncPerPlayerNpcVisibility(player, (ushort)player.MapID);
                        dialogue = step.CompleteDialogue;
                    }
                }
                else
                {
                    dialogue = step.InProgressDialogue + $" ({pq.CurrentKillCount}/{reqKills})";
                }
            }
            else // Dialogue / Delivery Step
            {
                // Grant step items if any (e.g. Bick handing Black Medicine)
                if (step.GrantItemsOnStep != null)
                {
                    foreach (var it in step.GrantItemsOnStep)
                        GrantItemReward(player, it.Item1, it.Item2);
                }

                if (pq.Step >= quest.Steps.Count)
                {
                    GrantRewards(player, quest);
                    pq.State = QuestState.Completed;
                    pq.CompletedAt = DateTime.UtcNow;
                    SavePlayerQuest(player, quest.QuestID);
                    SendQuestUpdate(player, quest.QuestID, QuestState.Completed);
                    SyncPerPlayerNpcVisibility(player, (ushort)player.MapID);
                    dialogue = step.CompleteDialogue ?? quest.CompleteDialogue;
                }
                else
                {
                    pq.Step++;
                    SavePlayerQuest(player, quest.QuestID);
                    SendQuestUpdate(player, quest.QuestID, QuestState.InProgress, (byte)pq.Step);
                    SyncPerPlayerNpcVisibility(player, (ushort)player.MapID);
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

        // Announce only committed rewards; the banner must not add the item a second time.
        public static bool GrantItemReward(Player player, ushort itemId, int count)
        {
            if (player?.Inv == null || count <= 0) return false;
            if (!player.Inv.TryAddItems(new Dictionary<ushort, int> { { itemId, count } }))
            {
                player.SendHeadBanner("Cannot receive reward. Check inventory space and item data.");
                return false;
            }
            string name = Game.Battle.MonsterDropManager.ResolveItemName(itemId) ?? $"Item #{itemId}";
            player.SendHeadBanner($"Obtain {name} x{count}");
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
                player.Eqs.AddExp((long)quest.Reward.Exp, true);
            }

            // 3. Items
            if (quest.Reward.Items != null && quest.Reward.Items.Count > 0)
            {
                foreach (var it in quest.Reward.Items)
                {
                    GrantItemReward(player, it.Item1, it.Item2);
                }
            }

            // 4. Companion Pet
            if (quest.Reward.CompanionPetID > 0)
            {
                SendCompanionReward(player, quest.Reward.CompanionPetID, quest.Reward.CompanionName ?? "Companion");
            }

            // 5. Dynamic NPC Despawn (AC 22:4) for this player upon quest completion
            if (quest.DespawnNpcClickIDs != null && quest.DespawnNpcClickIDs.Count > 0)
            {
                foreach (var clickId in quest.DespawnNpcClickIDs)
                {
                    PreEventInterpreter.SendActorHide(player, (ushort)clickId);
                    DebugSystem.Write($"[QuestManager] Despawned NPC (ClickID: {clickId}) via AC 22:4 for {player.CharName} following Quest '{quest.Title}' completion.");
                }
            }

            DebugSystem.Write($"[QuestManager] Granted rewards for Quest '{quest.Title}' to {player.CharName} (Gold: +{quest.Reward.Gold}, EXP: +{quest.Reward.Exp})");
        }

        /// <summary>
        /// Synchronizes personal client-side NPC visibility based on dynamic Eve.emg PreEvents and quest state.
        /// Evaluates both spawning (reveal) and despawning (conceal) symmetrically across all map entities.
        /// </summary>
        public static void SyncPerPlayerNpcVisibility(Player player, ushort mapId)
        {
            if (player == null) return;

            try
            {
                // 1. Replay actor visibility (despawn recruited companions and completed events/chests)
                if (player.CurMap is GameMap gmap)
                {
                    ReplayActorVisibility(player, gmap);
                }

                // 2. Symmetrically evaluate dynamic Eve.emg PreEvents and quest stage conditions
                PreEventInterpreter.EvaluateMapPreEvents(player, mapId);
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[QuestManager] Error syncing per-player NPC visibility: {ex.Message}");
            }
        }

        /// <summary>
        /// Sends authentic companion recruit packets (Official AC 15:1 format matching PCAP Frame 0958).
        /// </summary>
        public static string ResolveCompanionName(uint petId, string storedName = null)
        {
            string name = storedName?.Trim();
            if (!string.IsNullOrEmpty(name) && name != "Wild Monster" && name != "Wild Monst" &&
                name != "Companion" && !name.StartsWith("Companion #") && !name.StartsWith("Template #"))
                return name;
            return GetNpcName(petId);
        }

        // Xaolan is unavailable after assimilation, including the saved farewell checkpoint.
        // Keep the original companion in QuestPets; normal recruitment is not resurrection.
        public static bool IsXaolanInFate(Player player)
        {
            if (player?.Quests == null) return false;
            return (player.Quests.TryGetValue(13087, out var done) && done.State == QuestState.InProgress && done.Step > 0) ||
                (player.Quests.TryGetValue(13086, out var fate) && fate.State == QuestState.InProgress && fate.Step >= 2);
        }

        public static void SendStoryConstellations(Player player, bool showXaolan = false)
        {
            bool cygnus = player.Quests != null && player.Quests.TryGetValue(13087, out var done) &&
                done.State == QuestState.InProgress && done.Step > 0;
            // Current client AC15:19 reads count followed by byte IDs; Star_1 is Cygnus.
            player.Send(cygnus ? Tools.FromFormat("bbbb", 15, 19, 1, 1) : Tools.FromFormat("bbb", 15, 19, 0));
            // AC15:20 reads star ID and display mode. Mode0 previews the acquired image.
            if (showXaolan && cygnus) player.Send(Tools.FromFormat("bbbb", 15, 20, 1, 0));
        }

        public static bool CanRecruitCompanion(Player player, uint petId)
        {
            if (player == null || petId == 0 || Game.DataFiles.SceneDataManager.GetNpcBaseStats(Player.GetCompanionBroadcastId(petId)) == null) return false;
            if (Player.IsSamePetOrCompanion(petId, 14156) && IsXaolanInFate(player)) return false;
            if (player.HasRecruitedCompanion((ushort)petId)) return true;
            if (player.HotelPets != null && player.HotelPets.Values.Any(p => p != null && Player.IsSamePetOrCompanion(p.PetID, petId))) return false;
            return (player.PlayerPets?.Count ?? 0) < 4;
        }

        public static bool SendCompanionReward(Player player, uint petId, string petName, bool setBattle = false)
        {
            if (player == null || petId == 0 || Game.DataFiles.SceneDataManager.GetNpcBaseStats(Player.GetCompanionBroadcastId(petId)) == null) return false;
            if (Player.IsSamePetOrCompanion(petId, 14156) && IsXaolanInFate(player)) return false;
            if (petId == 12178) petId = 12032;
            petName = ResolveCompanionName(petId, petName);

            try
            {
                if (player.PlayerPets == null)
                    player.PlayerPets = new Dictionary<byte, Player.PlayerPetData>();

                Player.PlayerPetData petData = player.PlayerPets.Values
                    .FirstOrDefault(pet => pet != null && Player.IsSamePetOrCompanion(pet.PetID, petId));

                if (petData == null && player.HotelPets != null && player.HotelPets.Values.Any(p => p != null && Player.IsSamePetOrCompanion(p.PetID, petId)))
                {
                    player.SendHeadBanner("This companion is in Pet Hotel. Retrieve it before continuing.");
                    return false;
                }
                if (petData != null && !setBattle) return true;

                byte petSlot;
                bool isNewPet = petData == null;
                bool isFirstRecruit = false;
                if (isNewPet)
                {
                    var existingPets = player.PlayerPets.Values
                        .Where(pet => pet != null && pet.PetID > 0)
                        .OrderBy(pet => pet.Slot)
                        .ToList();
                    if (existingPets.Count >= 4)
                    {
                        player.SendHeadBanner("No free companion slot available.");
                        DebugSystem.Write($"[QuestManager] Could not recruit {petName} for {player.CharName}: companion slots are full.");
                        return false;
                    }

                    petSlot = 1;
                    while (player.PlayerPets.ContainsKey(petSlot)) petSlot++;
                    var returning = player.QuestPets?.Values.FirstOrDefault(p => p != null && Player.IsSamePetOrCompanion(p.PetID, petId));
                    if (returning != null)
                    {
                        player.QuestPets.Remove(returning.Slot);
                        petData = returning;
                        petData.Slot = petSlot;
                        petData.ClientSlot = 0;
                    }
                    else
                    {
                        isFirstRecruit = true;
                        petData = new Player.PlayerPetData
                        {
                            Slot = petSlot,
                            PetID = petId,
                            PetName = petName,
                            Level = 1,
                            Amity = 60,
                            IsBattle = false,
                            IsRide = false
                        };
                        // Starter equipment belongs to a new quest companion only.
                        // Replaying the quest must never regenerate removed equipment.
                        if ((petId == 14161 || petId == 14162) && player.Inv.HasItemDefinition(10063))
                            petData.Eq_Weapon = 10063; // Roca: Machete
                        else if (petId == 14156 && player.Inv.HasItemDefinition(25028))
                            petData.Eq_Special = 25028; // Xaolan's Jade
                        petData.InitializeBaseStats();
                        petData.NormalizeClientStats(true);
                    }
                    player.PlayerPets[petSlot] = petData;
                }
                else
                {
                    petSlot = petData.Slot;
                    petData.PetName = ResolveCompanionName(petData.PetID, petData.PetName);
                }

                // A returning alias must retain the saved identity in every packet.
                petId = petData.PetID;

                // 1. AC 22:10 & AC 22:11 Hide Recruited NPC from Current Map for all matching ClickIDs
                if (player.CurMap is GameMap gmap && gmap.NpcList != null)
                {
                    foreach (var mapNpc in gmap.NpcList.OfType<Maps.QuestNpc>())
                    {
                        if (player.HasStoryCompanionInParty((ushort)mapNpc.TemplateID))
                        {
                            PreEventInterpreter.SendActorHide(player, (ushort)mapNpc.CickID);
                            DebugSystem.Write($"[QuestManager] Despawned recruited NPC {mapNpc.Name} (ClickID {mapNpc.CickID}, TID {mapNpc.TemplateID}) for {player.CharName}");
                        }
                    }
                }

                // Recruit once. The client allocates the first free slot and rejects duplicate IDs.
                SendPacket petPkt = CreatePetPacket(player, petId, petSlot,
                    petData.HP, petData.MaxHP, petData.SP, petData.MaxSP, petData.Amity, petData.Level,
                    petData.Str, petData.Con, petData.Int, petData.Wis, petData.Agi,
                    petData.Exp, petData.Reborn, petData.Job);
                if (player.RegisterClientPet(petData)) player.Send(petPkt);

                SendPetProgression(player, petData);
                SendPacket petNamePacket = CreatePetNamePacket(player, petData);
                if (petNamePacket != null) player.Send(petNamePacket);

                // AC15:1 recruits without equipment fields; AC15:8 restores the
                // equipped starter item without repeating the join notification.
                if (isNewPet && (!isFirstRecruit || petData.Eq_Weapon != 0 || petData.Eq_Special != 0))
                    player.Send(CreatePetListPacket(player));

                // 4. If battle mode enabled, set active companion on map and broadcast appearance
                if (setBattle)
                {
                    foreach (var pet in player.PlayerPets.Values)
                        pet.IsBattle = pet == petData;

                    uint broadcastPetId = Player.GetCompanionBroadcastId(petId);
                    player.ActivePetID = broadcastPetId;
                    player.Send(Tools.FromFormat("bbd", 19, 1, broadcastPetId));
                    player.BroadcastPetAppearance(broadcastPetId, petData.PetName);
                }

                player.SaveCharacterData();
                DebugSystem.Write($"[QuestManager] Companion {petData.PetName} (ID: {petId}, Slot: {petSlot}, New: {isNewPet}, Battle: {setBattle}) synchronized.");
                return true;
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[QuestManager] Error sending companion reward: {ex.Message}");
                return false;
            }
        }

        public static SendPacket CreatePetPacket(Player player, uint petId, byte slot = 1, int curHp = 250, int maxHp = 250, int curSp = 100, int maxSp = 100, byte amity = 60, byte level = 1, ushort str = 0, ushort con = 0, ushort int_ = 0, ushort wis = 0, ushort agi = 0, uint exp = 0, bool reborn = false, byte job = 0)
        {
            SendPacket petPkt = new SendPacket();
            petPkt.PackArray(new byte[] { 15, 1 });
            petPkt.Pack32(player.CharID);
            uint pktPetId = Player.GetCompanionBroadcastId(petId);
            petPkt.Pack32(pktPetId);
            // aLogin.exe 0x409820: this is ownership/type, NOT a roster slot.
            petPkt.Pack8(1);
            petPkt.Pack16(str);
            petPkt.Pack16(con);
            petPkt.Pack16(int_);
            petPkt.Pack16(wis);
            petPkt.Pack16(agi);
            petPkt.Pack8(level > 0 ? level : (byte)1);
            petPkt.Pack32(Player.PlayerPetData.GetClientTotalExp(level, exp));
            // Three (byte, dword) skill-progress records. HP/SP are not in AC 15:1;
            // the client initializes them from stats, then AC 8:2 supplies current values.
            Player.PlayerPetData pet;
            if (!player.PlayerPets.TryGetValue(slot, out pet) || !Player.IsSamePetOrCompanion(pet.PetID, petId))
                pet = new Player.PlayerPetData { PetID = petId };
            pet.PackSkills(petPkt);
            petPkt.Pack8(amity);
            petPkt.Pack16(0);
            petPkt.Pack8(0);
            petPkt.Pack8(reborn ? (byte)1 : (byte)0);
            petPkt.Pack8(job);
            petPkt.Pack8(0);
            petPkt.Pack8(0);
            petPkt.Pack8(0);
            petPkt.Pack16(0);
            petPkt.Pack16(0);
            return petPkt;
        }

        /// <summary>
        /// Builds the owner's complete pet roster. AC 15:8 is the login/status-window
        /// packet; AC 15:1 is only the one-time "pet recruited" notification.
        /// </summary>
        public static SendPacket CreatePetListPacket(Player player)
        {
            if (player?.PlayerPets == null) return null;
            var pets = player.PlayerPets.Values.Where(p => p != null && p.PetID > 0 && p.ClientSlot > 0)
                .OrderBy(p => p.ClientSlot).ToList();
            if (pets.Count == 0) return null;

            // Native RevInitFNpc (0x40b1e0): variable records, 182 bytes + name.
            // Unlike AC15:1, this restores existing pets without a joined-party banner.
            SendPacket packet = new SendPacket();
            packet.PackArray(new byte[] { 15, 8 });
            foreach (var pet in pets)
            {
                pet.NormalizeExpForLevel();
                pet.NormalizeClientStats(inventory: player.Inv);
                uint clientId = Player.GetCompanionBroadcastId(pet.PetID);
                packet.Pack8(pet.ClientSlot);
                packet.Pack16((ushort)clientId);
                packet.Pack32(pet.ClientTotalExp);
                packet.Pack8(pet.Level);
                packet.Pack32((uint)Math.Max(0, pet.HP));
                packet.Pack16((ushort)Math.Max(0, Math.Min(ushort.MaxValue, pet.SP)));
                packet.Pack16(pet.Int);
                packet.Pack16(pet.Str);
                packet.Pack16(pet.Con);
                packet.Pack16(pet.Agi);
                packet.Pack16(pet.Wis);
                packet.Pack8(0);
                packet.Pack8(pet.Amity);
                packet.Pack8(1); // ownership
                packet.Pack16(pet.SkillPoints);
                byte[] name = Encoding.ASCII.GetBytes(ResolveCompanionName(pet.PetID, pet.PetName));
                packet.Pack8((byte)Math.Min(16, name.Length));
                packet.PackArray(name.Take(16).ToArray());

                // Restore existing skill levels directly, without AC8:2 stat110 (Learned).
                // Slots follow Npc.dat, not the order in the server's skill list.
                pet.PackSkills(packet);

                // Six native equipped-item records: ID plus 19 metadata bytes.
                for (byte slot = 1; slot <= 6; slot++)
                {
                    packet.Pack16(pet.GetEquipmentId(slot));
                    var metadata = new byte[19];
                    metadata[0] = pet.EquipmentMetadata[(slot - 1) * 2];
                    metadata[13] = pet.EquipmentMetadata[(slot - 1) * 2 + 1];
                    packet.PackArray(metadata);
                }
                packet.Pack8(0);
                packet.Pack8(0);
                packet.Pack8(pet.Reborn ? (byte)1 : (byte)0);
                packet.Pack8(pet.Job);
                packet.Pack8(0);
                packet.Pack8(0);
                packet.Pack8(0);
                packet.Pack16(0);
                packet.Pack16(0);
            }
            return packet;
        }

        public static SendPacket CreatePetNamePacket(Player player, Player.PlayerPetData pet)
        {
            if (player == null || pet == null || pet.ClientSlot == 0) return null;
            string petName = string.IsNullOrWhiteSpace(pet.PetName)
                ? (GetNpcName(pet.PetID) ?? "Pet")
                : pet.PetName.Trim();

            SendPacket packet = new SendPacket();
            // Native rename notification is 15:9, owner DWORD, internal slot, raw name.
            packet.PackArray(new byte[] { 15, 9 });
            packet.Pack32(player.CharID);
            packet.Pack8(pet.ClientSlot);
            packet.PackArray(Encoding.ASCII.GetBytes(petName));
            return packet;
        }

        public static void SendPetProgression(Player player, Player.PlayerPetData pet)
        {
            if (player == null || pet == null || pet.ClientSlot == 0) return;

            pet.NormalizeExpForLevel();
            pet.NormalizeClientStats(inventory: player.Inv);
            SendPetStat(player, pet.ClientSlot, 35, pet.Level);
            SendPetStat(player, pet.ClientSlot, 37, (uint)Math.Max(0, pet.Level - 1));
            SendPetStat(player, pet.ClientSlot, 38, pet.SkillPoints);
            SendPetStat(player, pet.ClientSlot, 36, pet.ClientTotalExp);
            SendPetStat(player, pet.ClientSlot, 28, pet.Str);
            SendPetStat(player, pet.ClientSlot, 29, pet.Con);
            SendPetStat(player, pet.ClientSlot, 30, pet.Agi);
            SendPetStat(player, pet.ClientSlot, 27, pet.Int);
            SendPetStat(player, pet.ClientSlot, 33, pet.Wis);
            SendPetEquipmentStats(player, pet);
        }

        public static void SendPetEquipmentStats(Player player, Player.PlayerPetData pet)
        {
            if (player == null || pet == null || pet.ClientSlot == 0) return;
            pet.NormalizeClientStats(inventory: player.Inv);
            // Native equipment modifiers, not base STR/CON/INT/WIS/AGI or totals.
            SendPetStat(player, pet.ClientSlot, 210, player.Inv.GetPetEquipmentBonus(pet, eq => eq.ATK));
            SendPetStat(player, pet.ClientSlot, 211, player.Inv.GetPetEquipmentBonus(pet, eq => eq.DEF));
            SendPetStat(player, pet.ClientSlot, 215, player.Inv.GetPetEquipmentBonus(pet, eq => eq.MAT));
            SendPetStat(player, pet.ClientSlot, 216, player.Inv.GetPetEquipmentBonus(pet, eq => eq.MDF));
            SendPetStat(player, pet.ClientSlot, 214, player.Inv.GetPetEquipmentBonus(pet, eq => eq.SPD));
            // The status panel reads separate absolute totals (native fields 0x1ffc..0x2004).
            // Equipment modifiers above do not update these display fields.
            var totals = player.PetEquipmentStats(pet);
            for (byte stat = 41; stat <= 45; stat++)
                SendPetStat(player, pet.ClientSlot, stat, Math.Min(ushort.MaxValue, totals[stat - 39]));
            // Recalculate maxima before synchronizing clamped current vitals.
            SendPetStat(player, pet.ClientSlot, 207, player.Inv.GetPetEquipmentBonus(pet, eq => eq.HP));
            SendPetStat(player, pet.ClientSlot, 208, player.Inv.GetPetEquipmentBonus(pet, eq => eq.SP));
            SendPetStat(player, pet.ClientSlot, 25, pet.HP);
            SendPetStat(player, pet.ClientSlot, 26, pet.SP);
        }

        public static void SendPetAmity(Player player, Player.PlayerPetData pet)
        {
            if (player == null || pet == null || pet.ClientSlot == 0) return;
            // Native stat 64 assigns the pet's amity; use the session slot, not its DB slot.
            SendPetStat(player, pet.ClientSlot, 64, pet.Amity);
        }

        private static void SendPetStat(Player player, byte petSlot, byte stat, long value)
        {
            // AC 8:2 is not the same layout as the player's AC 8:1 update.
            // 4 selects the pet collection, followed by a ushort internal slot,
            // stat id, positive sign, DWORD value and DWORD skill ID (unused here).
            SendPacket packet = new SendPacket();
            packet.PackArray(new byte[] { 8, 2, 4, petSlot, 0, stat, (byte)(value < 0 ? 2 : 1) });
            packet.Pack32((uint)Math.Abs(value));
            packet.Pack32(0);
            player.Send(packet);
        }

        public static bool IsStoryCompanion(uint petId)
        {
            var npc = Game.DataFiles.SceneDataManager.GetNpcBaseStats(Player.GetCompanionBroadcastId(petId));
            if (npc == null) return false;
            if (npc.Type == 4) return true;
            // Native story companions also include type 2 NPCs and type 7 fairies/beasts.
            switch (petId)
            {
                case 12068: case 12081: case 12095: case 12132: case 12147:
                case 12148: case 12149: case 17914: case 31036: case 25020:
                case 17162: case 17454: return true;
                default: return false;
            }
        }

        public static List<ushort> GetDefaultPetSkills(uint petId)
        {
            // Every owned pet uses the native skill slots for its client template,
            // including captured monsters and voucher creatures, not just story pets.
            // Missing/empty saved skills are initialized by PlayerPetData.EnsureSkills.
            var npc = Game.DataFiles.SceneDataManager.GetNpcBaseStats(Player.GetCompanionBroadcastId(petId));
            if (npc != null)
                return (npc.Skills ?? new ushort[0]).Where(id => id != 0 &&
                    SkillRelated.SkillManager.GetSkill(id) != null).Distinct().ToList();

            List<ushort> skills = new List<ushort>();
            switch (petId)
            {
                case 12032:
                case 12178: // Robinson (Water)
                    skills.Add(25221); // Fury Strike (Water)
                    skills.Add(12046); // Freeze Strike (Water)
                    break;
                case 17162: // Monkey
                    skills.Add(12026); // Throw Banana Skin (12 SP)
                    skills.Add(12027); // Monkey Trick
                    break;
                case 12003: // Niss (Water)
                    skills.Add(11001); // Icicle Attack
                    break;
                case 12002: // Clive (Earth)
                    skills.Add(15001); // Exact Combo Hit
                    skills.Add(15002); // Instant Attack
                    break;
                case 12001: // Xaolan (Fire)
                    skills.Add(11100); // Fire Light
                    break;
                case 12005: // Sam (Wind)
                    skills.Add(12025); // Newbie's Stunt
                    skills.Add(11057); // Shield Defence
                    break;
                case 12015: // Shizune (Fire)
                    skills.Add(25436); // Random Sword Slash
                    skills.Add(25437); // Fire Dragon Chopper
                    break;
            }
            return skills;
        }

        public static void SendPetSkills(Player player, uint petId, byte slot = 1)
        {
            if (player == null || petId == 0) return;
            if (player.PlayerPets == null || !player.PlayerPets.TryGetValue(slot, out var pet) || pet.ClientSlot == 0) return;
            // Snapshot refresh must never replay Learned or reset a trained skill.
            if (!Player.IsSamePetOrCompanion(pet.PetID, petId)) return;
            var packet = CreatePetListPacket(player);
            if (packet != null) player.Send(packet);
        }

        public static void SendQuestJournal(Player player)
        {
            Game.PlayerRelated.NotebookManager.SendQuestJournal(player);
        }

        public static void SendAllQuestFlags(Player player)
        {
            SendQuestJournal(player);
        }

        /// <summary>
        /// Rebuilds the correct visible quest phase and actor states for the player on map entry:
        /// - Despawns recruited companion NPCs (e.g. Robinson, Clive, Niss, Roca, Sam, Fred)
        /// - Despawns/updates completed one-time quest NPCs and chests
        /// </summary>
        public static void ReplayActorVisibility(Player player, GameMap map)
        {
            if (player == null || map == null) return;
            try
            {
                // 1. Despawn recruited companions from map if in player party/pets or recruited
                if (map.NpcList != null && map.NpcList.Count > 0)
                {
                    foreach (var qn in map.NpcList.OfType<Game.Maps.QuestNpc>())
                    {
                        if (qn == null) continue;
                        if (player.HasStoryCompanionInParty((ushort)qn.TemplateID))
                        {
                            PreEventInterpreter.SendActorHide(player, (ushort)qn.CickID);
                            DebugSystem.Write($"[ActorVisibility] Replayed despawn for companion NPC {qn.Name} (ClickID {qn.CickID}, TID {qn.TemplateID}) for {player.CharName}");
                        }
                    }
                }

                // 2. Despawn or show opened state for completed one-time events/chests
                var mapData = DataBase.GameDataBase.GlobalInstance?.EveDat?.GetMapData((ushort)map.MapID);
                if (mapData?.Events != null && player.Quests != null && player.Quests.Count > 0)
                {
                    foreach (var ev in mapData.Events)
                    {
                        if (ev.SubEntry == null) continue;
                        foreach (var sub in ev.SubEntry)
                        {
                            uint qId = sub.unknownword1;
                            if (qId > 0 && player.Quests.TryGetValue(qId, out var pq) && pq.State == QuestState.Completed)
                            {
                                bool isChest = sub.SubEntry != null && sub.SubEntry.Any(o => o.DialogPtr == 2 && o.dialog2 == 5);
                                bool isDespawn = sub.SubEntry != null && sub.SubEntry.Any(o => o.DialogPtr == 2 && o.dialog2 == 2);
                                if (isChest)
                                {
                                    var chestOp = sub.SubEntry?.FirstOrDefault(o => o.DialogPtr == 2 && o.dialog2 == 5);
                                    ushort targetClickId = (ushort)(chestOp.HasValue && chestOp.Value.dialog1 > 0 ? chestOp.Value.dialog1 : ev.clickID);
                                    player.Send(Tools.FromFormat("bbwb", 22, 1, targetClickId, (byte)1));
                                }
                                else if (isDespawn)
                                {
                                    var despawnOp = sub.SubEntry?.FirstOrDefault(o => o.DialogPtr == 2 && o.dialog2 == 2);
                                    ushort targetClickId = (ushort)(despawnOp.HasValue && despawnOp.Value.dialog1 > 0 ? despawnOp.Value.dialog1 : ev.clickID);
                                    PreEventInterpreter.SendActorHide(player, (ushort)targetClickId);
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[ActorVisibility] Error in ReplayActorVisibility: {ex.Message}");
            }
        }

        // EVE stores native marks, not the client completion-bit indices.
        public static void SendQuestUpdate(Player player, uint questId, QuestState state, byte step = 1)
        {
            Game.PlayerRelated.NotebookManager.SendQuestUpdate(player, questId, state, step);
        }

        /// <summary>
        /// Handles monster defeat callbacks from combat engine.
        /// Increments kill counts on active bounty / battle quests for the player and advances quest stages.
        /// </summary>
        public static void OnMonsterDefeated(Player player, uint monsterId, string monsterName)
        {
            if (player == null || player.Quests == null || player.Quests.Count == 0) return;

            try
            {
                string mName = (monsterName ?? "").ToLower().Trim();

                foreach (var kvp in player.Quests.ToList())
                {
                    var pq = kvp.Value;
                    if (pq.State != QuestState.InProgress) continue;

                    if (!_registeredQuests.TryGetValue(pq.QuestID, out var quest) && !_masterQuests.TryGetValue(pq.QuestID, out quest))
                    {
                        continue;
                    }

                    bool matched = false;
                    int reqKills = 1;

                    if (quest.Steps != null && pq.Step > 0 && quest.Steps.Count >= pq.Step)
                    {
                        var step = quest.Steps[pq.Step - 1];
                        if (step.StepType == QuestType.MonsterBattle)
                        {
                            reqKills = step.RequiredKillCount > 0 ? step.RequiredKillCount : 1;
                            if (step.BattleMonsterID > 0 && step.BattleMonsterID == monsterId) matched = true;
                            else if (!string.IsNullOrEmpty(mName) && !string.IsNullOrEmpty(step.BattleMonsterName) && (step.BattleMonsterName.ToLower().Contains(mName) || mName.Contains(step.BattleMonsterName.ToLower()))) matched = true;
                            else if (step.BattleMonsterID == 0 && string.IsNullOrEmpty(step.BattleMonsterName)) matched = true;
                        }
                    }
                    else if (quest.Type == QuestType.MonsterBattle)
                    {
                        reqKills = quest.RequiredKillCount > 0 ? quest.RequiredKillCount : 1;
                        if (quest.BattleMonsterID > 0 && quest.BattleMonsterID == monsterId) matched = true;
                        else if (!string.IsNullOrEmpty(mName) && !string.IsNullOrEmpty(quest.BattleMonsterName) && (quest.BattleMonsterName.ToLower().Contains(mName) || mName.Contains(quest.BattleMonsterName.ToLower()))) matched = true;
                        else if (quest.BattleMonsterID == 0 && string.IsNullOrEmpty(quest.BattleMonsterName)) matched = true;
                    }

                    if (matched)
                    {
                        pq.CurrentKillCount++;
                        SavePlayerQuest(player, quest.QuestID);
                        player.Send(Tools.FromFormat("bbbs", 23, 57, 0, $"[Quest] {quest.Title}: {pq.CurrentKillCount}/{reqKills} defeated."));

                        if (pq.CurrentKillCount >= reqKills)
                        {
                            player.Send(Tools.FromFormat("bbbs", 23, 57, 0, $"[Quest Objective Completed] {quest.Title}"));
                            SendQuestUpdate(player, quest.QuestID, QuestState.InProgress, (byte)pq.Step);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[QuestManager] Error in OnMonsterDefeated for {player?.CharName}: {ex.Message}");
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
                    try { db.ExecuteNonQuery("ALTER TABLE charquest ADD COLUMN kill_count INT DEFAULT 0;"); } catch { }
                    try { db.ExecuteNonQuery("ALTER TABLE charquest ADD COLUMN completed_at TEXT;"); } catch { }

                    var dt = db.GetDataTable($"SELECT * FROM charquest WHERE charID={player.CharID}");
                    if (dt != null && dt.Rows.Count > 0)
                    {
                        foreach (System.Data.DataRow row in dt.Rows)
                        {
                            uint qId = Convert.ToUInt32(row["quest_started"]);
                            byte qPos = Convert.ToByte(row["quest_pos"]);
                            byte step = 1;
                            if (dt.Columns.Contains("step") && row["step"] != DBNull.Value)
                            {
                                step = Convert.ToByte(row["step"]);
                            }
                            int killCount = 0;
                            if (dt.Columns.Contains("kill_count") && row["kill_count"] != DBNull.Value)
                            {
                                killCount = Convert.ToInt32(row["kill_count"]);
                            }
                            DateTime startedAt = DateTime.UtcNow;
                            DateTime? completedAt = null;
                            if (dt.Columns.Contains("completed_at") && row["completed_at"] != DBNull.Value && DateTime.TryParse(Convert.ToString(row["completed_at"]), out var dtVal))
                            {
                                completedAt = dtVal;
                            }

                            player.Quests[qId] = new PlayerQuest(qId, (QuestState)qPos, step, killCount)
                            {
                                StartedAt = startedAt,
                                CompletedAt = completedAt
                            };
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
                    try { db.ExecuteNonQuery("ALTER TABLE charquest ADD COLUMN kill_count INT DEFAULT 0;"); } catch { }
                    try { db.ExecuteNonQuery("ALTER TABLE charquest ADD COLUMN completed_at TEXT;"); } catch { }

                    string completedAtStr = pq.CompletedAt.HasValue ? $"'{pq.CompletedAt.Value:yyyy-MM-dd HH:mm:ss}'" : "NULL";

                    var existing = db.GetDataTable($"SELECT pri_key FROM charquest WHERE charID={player.CharID} AND quest_started={questId} LIMIT 1");
                    if (existing != null && existing.Rows.Count > 0)
                    {
                        db.ExecuteNonQuery($"UPDATE charquest SET quest_pos={(byte)pq.State}, step={pq.Step}, kill_count={pq.CurrentKillCount}, completed_at={completedAtStr} WHERE charID={player.CharID} AND quest_started={questId}");
                    }
                    else
                    {
                        db.ExecuteNonQuery($"INSERT INTO charquest (charID, quest_started, quest_pos, step, kill_count, completed_at) VALUES ({player.CharID}, {questId}, {(byte)pq.State}, {pq.Step}, {pq.CurrentKillCount}, {completedAtStr})");
                    }
                }
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[QuestManager] Error saving quest {questId} for {player.CharName}: {ex.Message}");
            }
        }

        /// <summary>
        /// Starts and accepts a quest for a player, sending authentic AC 24:1 packet and saving state.
        /// </summary>
        public static void AcceptQuest(Player player, uint questId)
        {
            if (player == null) return;
            try
            {
                player.Quests[questId] = new PlayerQuest(questId, QuestState.InProgress) { Step = 1 };
                SendQuestUpdate(player, questId, QuestState.InProgress, 1);
                SavePlayerQuest(player, questId);
                SyncPerPlayerNpcVisibility(player, (ushort)player.MapID);
                DebugSystem.Write($"[QuestManager] Player {player.CharName} accepted Quest #{questId}.");
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[QuestManager] Error in AcceptQuest: {ex.Message}");
            }
        }

        /// <summary>
        /// Advances a player's quest to the next stage step.
        /// </summary>
        public static void AdvanceQuestStep(Player player, uint questId)
        {
            if (player == null) return;
            try
            {
                if (!player.Quests.TryGetValue(questId, out var pq))
                {
                    pq = new PlayerQuest(questId, QuestState.InProgress) { Step = 1 };
                    player.Quests[questId] = pq;
                }
                pq.Step++;
                SendQuestUpdate(player, questId, QuestState.InProgress, (byte)pq.Step);
                SavePlayerQuest(player, questId);
                SyncPerPlayerNpcVisibility(player, (ushort)player.MapID);
                DebugSystem.Write($"[QuestManager] Player {player.CharName} advanced Quest #{questId} to Step {pq.Step}.");
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[QuestManager] Error in AdvanceQuestStep: {ex.Message}");
            }
        }

        /// <summary>
        /// Explicitly sets and saves the quest state and step for a player.
        /// </summary>
        public static void SetPlayerQuestState(Player player, uint questId, QuestState state, byte step = 1)
        {
            if (player == null || questId == 0) return;
            try
            {
                if (player.Quests == null) player.Quests = new Dictionary<uint, PlayerQuest>();
                if (!player.Quests.TryGetValue(questId, out var pq))
                {
                    pq = new PlayerQuest(questId, state, step);
                    player.Quests[questId] = pq;
                }
                else
                {
                    pq.State = state;
                    pq.Step = step;
                }
                if (state == QuestState.Completed) pq.CompletedAt = DateTime.UtcNow;

                SendQuestUpdate(player, questId, state, step);
                SavePlayerQuest(player, questId);
                SyncPerPlayerNpcVisibility(player, (ushort)player.MapID);
                DebugSystem.Write($"[QuestManager] Set Player {player.CharName} Quest #{questId} -> {state} (Step {step})");
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[QuestManager] Error in SetPlayerQuestState: {ex.Message}");
            }
        }

        /// <summary>
        /// Completes a quest, awards EXP/Gold/Items, updates F6 Quest Log, and sends AC 24:5 packet.
        /// </summary>
        public static void CompleteQuest(Player player, uint questId)
        {
            if (player == null) return;
            try
            {
                if (!player.Quests.TryGetValue(questId, out var pq))
                {
                    pq = new PlayerQuest(questId, QuestState.Completed);
                    player.Quests[questId] = pq;
                }
                else
                {
                    pq.State = QuestState.Completed;
                }

                // Award rewards if defined
                if (_masterQuests.TryGetValue(questId, out var qDef) || _registeredQuests.TryGetValue(questId, out qDef))
                {
                    if (qDef.Reward != null)
                    {
                        if (qDef.Reward.Gold > 0)
                        {
                            player.Gold += (uint)qDef.Reward.Gold;
                            player.Send(Tools.FromFormat("bbd", 23, 7, player.Gold));
                        }
                        if (qDef.Reward.Exp > 0)
                        {
                            player.TotalExp += (long)qDef.Reward.Exp;
                            player.Send(Tools.FromFormat("bbd", 23, 8, (uint)player.TotalExp));
                        }
                    }
                }

                SendQuestUpdate(player, questId, QuestState.Completed);
                SavePlayerQuest(player, questId);
                SyncPerPlayerNpcVisibility(player, (ushort)player.MapID);
                DebugSystem.Write($"[QuestManager] Player {player.CharName} completed Quest #{questId} successfully.");
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[QuestManager] Error in CompleteQuest: {ex.Message}");
            }
        }

        /// <summary>
        /// Resets/abandons a quest for a player, sending authentic AC 24:3 packet and clearing from DB.
        /// </summary>
        public static void ResetQuest(Player player, uint questId)
        {
            if (player == null) return;
            try
            {
                player.Quests.Remove(questId);
                SendQuestUpdate(player, questId, QuestState.Failed);

                var db = (RCLibrary.Core.DataBase)DataBase.CharacterDataBase.GlobalInstance ?? (RCLibrary.Core.DataBase)DataBase.GameDataBase.GlobalInstance;
                if (db != null)
                {
                    db.ExecuteNonQuery($"DELETE FROM charquest WHERE charID={player.CharID} AND quest_started={questId}");
                }
                SyncPerPlayerNpcVisibility(player, (ushort)player.MapID);
                DebugSystem.Write($"[QuestManager] Reset Quest #{questId} for Player {player.CharName}.");
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[QuestManager] Error in ResetQuest: {ex.Message}");
            }
        }
    }
}
