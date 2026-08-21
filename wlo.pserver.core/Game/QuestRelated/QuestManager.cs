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

            if (full.Contains("sasha")) return new QuestReward(2000, 5000, 10012, "Sasha");
            if (full.Contains("roca")) return new QuestReward(1500, 4000, 10014, "Roca");
            if (full.Contains("niss")) return new QuestReward(1500, 4000, 10016, "Niss");
            if (full.Contains("clive")) return new QuestReward(3000, 8000, 10018, "Clive");
            if (full.Contains("sam")) return new QuestReward(2500, 6000, 10020, "Sam");
            if (full.Contains("elin")) return new QuestReward(3500, 10000, 10022, "Elin");
            if (full.Contains("shizune")) return new QuestReward(4000, 12000, 10024, "Shizune");
            if (full.Contains("victoria")) return new QuestReward(4500, 15000, 10026, "Victoria");
            if (full.Contains("angela")) return new QuestReward(5000, 18000, 10028, "Angela");
            if (full.Contains("eva")) return new QuestReward(5000, 20000, 10030, "Eva");
            if (full.Contains("robinson")) return new QuestReward(3000, 10000, 12032, "Robinson");

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
                return "👥 Companion & Rebirth";
            }
            if (t.Contains("raft") || t.Contains("canoe") || t.Contains("ship") ||
                t.Contains("boat") || t.Contains("airplane") || t.Contains("rocket") ||
                t.Contains("ufo") || t.Contains("tent") || t.Contains("craftsman") ||
                t.Contains("alchemy") || t.Contains("make a"))
            {
                return "🛠️ Crafting & Vehicles";
            }
            if (t.Contains("whack") || t.Contains("collect") || t.Contains("contest") ||
                t.Contains("quiz") || t.Contains("test") || t.Contains("game"))
            {
                return "🎯 Minigames & Challenges";
            }
            if (t.Contains("zodiac") || t.Contains("trial") || t.Contains("ghost") ||
                t.Contains("dragon") || t.Contains("round") || t.Contains("palace") ||
                t.Contains("tower") || t.Contains("cave") || t.Contains("pirate"))
            {
                return "🐉 Dungeons & Instances";
            }
            return "🏝️ Storyline & Area";
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
                string jsonPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "npc.json");
                if (!System.IO.File.Exists(jsonPath)) jsonPath = @"d:\GitHub\Wonderland-Private-Server\Data\npc.json";
                if (System.IO.File.Exists(jsonPath))
                {
                    string jsonText = System.IO.File.ReadAllText(jsonPath);
                    var matches = System.Text.RegularExpressions.Regex.Matches(jsonText, @"""(\d+)""\s*:\s*""([^""]+)""");
                    foreach (System.Text.RegularExpressions.Match match in matches)
                    {
                        if (uint.TryParse(match.Groups[1].Value, out uint jid))
                        {
                            string jname = match.Groups[2].Value.Trim();
                            _npcNameCache[jid] = jname;
                            if (!_npcTidByName.ContainsKey(jname))
                            {
                                _npcTidByName[jname] = jid;
                            }
                        }
                    }
                }
            }
            catch
            {
            }
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
                if (string.IsNullOrWhiteSpace(title) || title.StartsWith("Visit Mark") || title.StartsWith("Time Mark"))
                    return null;

                // 2. Extract Body from [266..525]
                string body = ExtractReversedString(data, offset + 266, 260);

                var mark = new ParsedMark
                {
                    MarkID = markId,
                    Title = CleanString(title),
                    NpcPattern = ExtractNpcPattern(title, body)
                };

                if (string.IsNullOrWhiteSpace(body) || body == "Quest Mark")
                {
                    mark.Description = mark.Title;
                    mark.IntroDialogue = mark.Title;
                    mark.InProgressDialogue = mark.Title;
                    mark.CompletedSummary = mark.Title;
                    mark.Location = ExtractAreaFromText(mark.Title, "");
                    return mark;
                }

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
            if (petId == 12178) { petId = 12032; petName = "Robinson"; }

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
                player.CurMap?.Broadcast(petPkt, "Ex", player.CharID);
                SendPetSkills(player, petId, 1);

                // 3. If battle mode enabled, set active companion on map
                if (setBattle)
                {
                    player.ActivePetID = petId;
                    // AC 19:1 Set battle companion state for owner (Frame 0958)
                    player.Send(Tools.FromFormat("bbd", 19, 1, petId));

                    // AC 19:4 Broadcast battle companion following player to all players on map
                    SendPacket followPkt = new SendPacket();
                    followPkt.Pack8(19);
                    followPkt.Pack8(4);
                    followPkt.Pack32(player.CharID);
                    followPkt.Pack32(petId);
                    player.CurMap?.Broadcast(followPkt, "Ex", player.CharID);
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

        public static List<ushort> GetDefaultPetSkills(uint petId)
        {
            List<ushort> skills = new List<ushort>();
            switch (petId)
            {
                case 12032:
                case 12178: // Robinson (Water)
                    skills.Add(15249); // Fury Strike (30 SP, Water)
                    skills.Add(15216); // Freeze Strike (77 SP, Water)
                    break;
                case 17162: // Monkey
                    skills.Add(12026); // Throw Banana Skin (12 SP)
                    break;
                case 12003: // Niss (Water)
                    skills.Add(11001); // Icicle Attack
                    break;
                case 12002: // Clive (Earth)
                    skills.Add(15001); // Exact Combo Hit
                    skills.Add(15002); // Instant Attack
                    break;
                case 12001: // Xaolan (Fire)
                    skills.Add(12001); // Fire Light
                    break;
                case 12005: // Sam (Wind)
                    skills.Add(15003); // Newbie's Stunt
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
            var skills = GetDefaultPetSkills(petId);
            foreach (var skId in skills)
            {
                SendPacket p = new SendPacket();
                p.Pack8(8);
                p.Pack8(2);
                p.Pack8(slot);
                p.Pack8(2);
                p.Pack8(0);
                p.Pack16(0x016F); // 367 = Pet Skill Unlock
                p.Pack32(0);
                p.Pack32((uint)skId);
                player.Send(p);
            }
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
                            SendQuestUpdate(player, qId, (QuestState)qPos, 1);
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
                DebugSystem.Write($"[QuestManager] Player {player.CharName} advanced Quest #{questId} to Step {pq.Step}.");
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[QuestManager] Error in AdvanceQuestStep: {ex.Message}");
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
                DebugSystem.Write($"[QuestManager] Reset Quest #{questId} for Player {player.CharName}.");
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[QuestManager] Error in ResetQuest: {ex.Message}");
            }
        }
    }
}
