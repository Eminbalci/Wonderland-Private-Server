using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Game.QuestRelated;
using RCLibrary.Core;

namespace DataBase
{
    /// <summary>
    /// Database manager for loading, persisting, and synchronizing game quests directly from SQLite/MySQL.
    /// </summary>
    public static class QuestDataBase
    {
        public static void Initialize(GameDataBase db)
        {
            if (db == null) return;

            try
            {
                // 1. Create game_quests table if it doesn't exist
                string createQuestsTable = @"
                    CREATE TABLE IF NOT EXISTS game_quests (
                        quest_id INTEGER PRIMARY KEY,
                        name VARCHAR(100) NOT NULL,
                        npc_name_pattern VARCHAR(100),
                        npc_template_id INT DEFAULT 0,
                        map_id INT DEFAULT 0,
                        type INT DEFAULT 0,
                        description TEXT,
                        intro_dialogue TEXT,
                        in_progress_dialogue TEXT,
                        complete_dialogue TEXT,
                        already_completed_dialogue TEXT,
                        battle_monster_id INT DEFAULT 0,
                        battle_monster_name VARCHAR(100),
                        reward_gold INT DEFAULT 0,
                        reward_exp INT DEFAULT 0,
                        reward_companion_id INT DEFAULT 0,
                        reward_companion_name VARCHAR(100),
                        reward_items TEXT,
                        required_items TEXT,
                        prerequisite_quests TEXT,
                        steps_json TEXT
                    )";
                db.ExecuteNonQuery(createQuestsTable);
                try { db.ExecuteNonQuery("ALTER TABLE game_quests ADD COLUMN map_id INT DEFAULT 0;"); } catch { }

                // 2. Check if table is empty or has old dummy Mark.dat dumps; if so, populate from Data/Mark.dat
                var dt = db.GetDataTable("SELECT COUNT(*) as cnt FROM game_quests");
                long count = 0;
                if (dt != null && dt.Rows.Count > 0)
                {
                    count = Convert.ToInt64(dt.Rows[0]["cnt"]);
                }

                if (count == 0)
                {
                    DebugSystem.Write("[QuestDataBase] Importing clean authentic quests from Data/Mark.dat...");
                    ReimportCleanQuests(db);
                }

                // 3. Load all quests from database into QuestManager
                LoadAllQuests(db);
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[QuestDataBase] Error during Quest DB initialization: {ex.Message}");
            }
        }

        public static void ReimportCleanQuests(GameDataBase db)
        {
            if (db == null) return;
            try
            {
                db.ExecuteNonQuery("DELETE FROM game_quests");

                string markDatPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "Mark.dat");
                if (!File.Exists(markDatPath))
                {
                    markDatPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "Data", "Mark.dat");
                }

                if (File.Exists(markDatPath))
                {
                    QuestManager.LoadAuthenticQuestsFromMarkDat(markDatPath);
                    try { db.ExecuteNonQuery("BEGIN TRANSACTION;"); } catch { }
                    foreach (var q in QuestManager.MasterQuests.Values)
                    {
                        string stepsJson = "";
                        if (q.Steps != null && q.Steps.Count > 0)
                        {
                            var sList = new List<string>();
                            foreach (var st in q.Steps)
                            {
                                string pDia = (st.PromptDialogue ?? "").Replace("\"", "\\\"").Replace("\r\n", "\\n").Replace("\n", "\\n");
                                string inDia = (st.InProgressDialogue ?? "").Replace("\"", "\\\"").Replace("\r\n", "\\n").Replace("\n", "\\n");
                                string compDia = (st.CompleteDialogue ?? "").Replace("\"", "\\\"").Replace("\r\n", "\\n").Replace("\n", "\\n");
                                sList.Add($"{{\"StepIndex\":{st.StepIndex},\"StepType\":\"{st.StepType}\",\"TargetNpcPattern\":\"{st.TargetNpcPattern}\",\"TargetNpcTemplateID\":{st.TargetNpcTemplateID},\"PromptDialogue\":\"{pDia}\",\"InProgressDialogue\":\"{inDia}\",\"CompleteDialogue\":\"{compDia}\"}}");
                            }
                            stepsJson = "[" + string.Join(",", sList) + "]";
                        }

                        string query = $@"
                            INSERT OR REPLACE INTO game_quests (
                                quest_id, name, npc_name_pattern, npc_template_id, map_id, type, description,
                                intro_dialogue, in_progress_dialogue, complete_dialogue, already_completed_dialogue,
                                battle_monster_id, battle_monster_name, reward_gold, reward_exp,
                                reward_companion_id, reward_companion_name, reward_items, required_items,
                                prerequisite_quests, steps_json
                            ) VALUES (
                                {q.QuestID}, '{EscapeSql(q.Title)}', '{EscapeSql(q.NpcNamePattern)}', {q.NpcTemplateID}, {q.MapID}, {(int)q.Type}, '{EscapeSql(q.Description)}',
                                '{EscapeSql(q.IntroDialogue)}', '{EscapeSql(q.InProgressDialogue)}', '{EscapeSql(q.CompleteDialogue)}', '{EscapeSql(q.AlreadyCompletedDialogue)}',
                                {q.BattleMonsterID}, '{EscapeSql(q.BattleMonsterName)}', {q.Reward?.Gold ?? 0}, {(int)(q.Reward?.Exp ?? 0)},
                                {q.Reward?.CompanionPetID ?? 0}, '{EscapeSql(q.Reward?.CompanionName)}', '', '',
                                '', '{EscapeSql(stepsJson)}'
                            )";
                        db.ExecuteNonQuery(query);
                    }
                    try { db.ExecuteNonQuery("COMMIT;"); } catch { }
                }
                else
                {
                    ImportQuestsFromJson(db);
                }

                LoadAllQuests(db);
            }
            catch (Exception ex)
            {
                try { db.ExecuteNonQuery("ROLLBACK;"); } catch { }
                DebugSystem.Write($"[QuestDataBase] Error reimporting clean quests: {ex.Message}");
            }
        }

        public static void ImportQuestsFromJson(GameDataBase db)
        {
            string jsonPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "quests.json");
            if (!File.Exists(jsonPath))
            {
                jsonPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "Data", "quests.json");
            }
            if (!File.Exists(jsonPath)) return;

            try
            {
                string jsonText = File.ReadAllText(jsonPath, Encoding.UTF8);
                var questBlocks = Regex.Matches(jsonText, @"\{[\s\S]*?\""QuestID\""\s*:\s*(\d+)[\s\S]*?\}");
                int imported = 0;

                try { db.ExecuteNonQuery("BEGIN TRANSACTION;"); } catch { }

                foreach (Match match in questBlocks)
                {
                    string block = match.Value;
                    uint questId = uint.Parse(match.Groups[1].Value);

                    string name = GetJsonString(block, "Name") ?? $"Quest {questId}";
                    string npcPattern = GetJsonString(block, "NpcNamePattern") ?? "";
                    int npcTid = GetJsonInt(block, "NpcTemplateID");
                    int type = GetJsonInt(block, "Type");
                    string desc = GetJsonString(block, "Description") ?? "";
                    string intro = GetJsonString(block, "IntroDialogue") ?? "";
                    string inProgress = GetJsonString(block, "InProgressDialogue") ?? "";
                    string complete = GetJsonString(block, "CompleteDialogue") ?? "";
                    string alreadyDone = GetJsonString(block, "AlreadyCompletedDialogue") ?? "";
                    int battleMonsterId = GetJsonInt(block, "BattleMonsterID");
                    string battleMonsterName = GetJsonString(block, "BattleMonsterName") ?? "";
                    int rewardGold = GetJsonInt(block, "RewardGold");
                    int rewardExp = GetJsonInt(block, "RewardExp");
                    int rewardCompanionId = GetJsonInt(block, "RewardCompanionID");
                    string rewardCompanionName = GetJsonString(block, "RewardCompanionName") ?? "";
                    string rewardItems = GetJsonString(block, "RewardItems") ?? "";
                    string requiredItems = GetJsonString(block, "RequiredItems") ?? "";
                    string prereqs = GetJsonString(block, "PrerequisiteQuests") ?? "";
                    string stepsJson = "";

                    string query = $@"
                        INSERT OR REPLACE INTO game_quests (
                            quest_id, name, npc_name_pattern, npc_template_id, map_id, type, description,
                            intro_dialogue, in_progress_dialogue, complete_dialogue, already_completed_dialogue,
                            battle_monster_id, battle_monster_name, reward_gold, reward_exp,
                            reward_companion_id, reward_companion_name, reward_items, required_items,
                            prerequisite_quests, steps_json
                        ) VALUES (
                            {questId}, '{EscapeSql(name)}', '{EscapeSql(npcPattern)}', {npcTid}, 0, {type}, '{EscapeSql(desc)}',
                            '{EscapeSql(intro)}', '{EscapeSql(inProgress)}', '{EscapeSql(complete)}', '{EscapeSql(alreadyDone)}',
                            {battleMonsterId}, '{EscapeSql(battleMonsterName)}', {rewardGold}, {rewardExp},
                            {rewardCompanionId}, '{EscapeSql(rewardCompanionName)}', '{EscapeSql(rewardItems)}', '{EscapeSql(requiredItems)}',
                            '{EscapeSql(prereqs)}', '{EscapeSql(stepsJson)}'
                        )";
                    db.ExecuteNonQuery(query);
                    imported++;
                }

                try { db.ExecuteNonQuery("COMMIT;"); } catch { }
                DebugSystem.Write($"[QuestDataBase] Successfully imported {imported} quests into game_quests table.");
            }
            catch (Exception ex)
            {
                try { db.ExecuteNonQuery("ROLLBACK;"); } catch { }
                DebugSystem.Write($"[QuestDataBase] Error importing quests from JSON: {ex.Message}");
            }
        }

        public static void LoadAllQuests(GameDataBase db)
        {
            try
            {
                var dt = db.GetDataTable("SELECT * FROM game_quests ORDER BY quest_id ASC");
                if (dt == null || dt.Rows.Count == 0) return;

                int loaded = 0;
                foreach (DataRow row in dt.Rows)
                {
                    uint questId = Convert.ToUInt32(row["quest_id"]);
                    string name = row["name"].ToString();
                    string npcPattern = row["npc_name_pattern"].ToString();
                    uint npcTid = Convert.ToUInt32(row["npc_template_id"]);
                    ushort mapId = dt.Columns.Contains("map_id") && row["map_id"] != DBNull.Value ? Convert.ToUInt16(row["map_id"]) : (ushort)0;
                    QuestType type = (QuestType)Convert.ToInt32(row["type"]);

                    var quest = new QuestDefinition(questId, name, npcPattern, type)
                    {
                        NpcTemplateID = npcTid,
                        MapID = mapId,
                        Description = row["description"].ToString(),
                        IntroDialogue = row["intro_dialogue"].ToString(),
                        InProgressDialogue = row["in_progress_dialogue"].ToString(),
                        CompleteDialogue = row["complete_dialogue"].ToString(),
                        AlreadyCompletedDialogue = row["already_completed_dialogue"].ToString(),
                        BattleMonsterID = Convert.ToUInt32(row["battle_monster_id"]),
                        BattleMonsterName = row["battle_monster_name"].ToString()
                    };

                    // Rewards
                    int rGold = Convert.ToInt32(row["reward_gold"]);
                    uint rExp = Convert.ToUInt32(row["reward_exp"]);
                    uint rCompId = Convert.ToUInt32(row["reward_companion_id"]);
                    string rCompName = row["reward_companion_name"].ToString();
                    quest.Reward = new QuestReward(rGold, rExp, rCompId, rCompName);

                    // Parse Reward Items
                    string rItemsStr = row["reward_items"]?.ToString();
                    if (!string.IsNullOrWhiteSpace(rItemsStr))
                    {
                        var matches = Regex.Matches(rItemsStr, @"\""ItemId\""\s*:\s*(\d+)[\s\S]*?\""Count\""\s*:\s*(\d+)");
                        foreach (Match m in matches)
                        {
                            quest.Reward.AddItem(ushort.Parse(m.Groups[1].Value), byte.Parse(m.Groups[2].Value));
                        }
                    }

                    // Parse Required Items
                    string reqItemsStr = row["required_items"]?.ToString();
                    if (!string.IsNullOrWhiteSpace(reqItemsStr))
                    {
                        var matches = Regex.Matches(reqItemsStr, @"\""ItemId\""\s*:\s*(\d+)[\s\S]*?\""Count\""\s*:\s*(\d+)");
                        foreach (Match m in matches)
                        {
                            quest.RequiredItems.Add(new QuestRequirementItem(ushort.Parse(m.Groups[1].Value), byte.Parse(m.Groups[2].Value)));
                        }
                    }

                    // Parse Prerequisite Quests
                    string prereqsStr = row["prerequisite_quests"]?.ToString();
                    if (!string.IsNullOrWhiteSpace(prereqsStr))
                    {
                        var parts = prereqsStr.Split(new char[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries);
                        foreach (var p in parts)
                        {
                            if (uint.TryParse(p.Trim(), out uint pId))
                            {
                                quest.AddPrerequisiteQuest(pId);
                            }
                        }
                    }

                    // Parse Steps JSON
                    string stepsJson = row["steps_json"]?.ToString();
                    if (!string.IsNullOrWhiteSpace(stepsJson) && stepsJson.Length > 5)
                    {
                        var stepMatches = Regex.Matches(stepsJson, @"\{[\s\S]*?\""StepNumber\""\s*:\s*(\d+)[\s\S]*?\}");
                        foreach (Match sm in stepMatches)
                        {
                            string sBlock = sm.Value;
                            byte sNum = byte.Parse(sm.Groups[1].Value);
                            string sPattern = GetJsonString(sBlock, "TargetNpcPattern") ?? "";
                            uint sTid = (uint)GetJsonInt(sBlock, "TargetNpcTemplateID");
                            QuestType sType = (QuestType)GetJsonInt(sBlock, "StepType");

                            var step = new QuestStep(sNum, sPattern, sType)
                            {
                                TargetNpcTemplateID = sTid,
                                PromptDialogue = GetJsonString(sBlock, "PromptDialogue") ?? "",
                                InProgressDialogue = GetJsonString(sBlock, "InProgressDialogue") ?? "",
                                CompleteDialogue = GetJsonString(sBlock, "CompleteDialogue") ?? "",
                                BattleMonsterID = (uint)GetJsonInt(sBlock, "BattleMonsterID"),
                                BattleMonsterName = GetJsonString(sBlock, "BattleMonsterName") ?? ""
                            };

                            quest.AddStep(step);
                        }
                    }

                    QuestManager.RegisterQuest(quest);
                    loaded++;
                }

                DebugSystem.Write($"[QuestDataBase] Successfully loaded {loaded} quests directly from database into QuestManager.");
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[QuestDataBase] Error loading quests from database: {ex.Message}");
            }
        }

        public static DataTable GetQuestsDataTable(GameDataBase db, string filter = "")
        {
            if (db == null) return new DataTable();
            try
            {
                string query = "SELECT quest_id, name, type, npc_name_pattern, npc_template_id, map_id, reward_gold, reward_exp, reward_companion_name, reward_items, required_items, prerequisite_quests, description, intro_dialogue, in_progress_dialogue, complete_dialogue, already_completed_dialogue, battle_monster_id, battle_monster_name, reward_companion_id, steps_json FROM game_quests";
                if (!string.IsNullOrWhiteSpace(filter))
                {
                    string safeFilter = EscapeSql(filter.Trim());
                    query += $" WHERE name LIKE '%{safeFilter}%' OR npc_name_pattern LIKE '%{safeFilter}%' OR CAST(quest_id AS TEXT) LIKE '%{safeFilter}%'";
                }
                query += " ORDER BY quest_id ASC";
                return db.GetDataTable(query);
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[QuestDataBase] Error querying quests datatable: {ex.Message}");
                return new DataTable();
            }
        }

        public static bool SaveQuest(GameDataBase db, uint questId, string name, string npcPattern, int npcTid, int mapId, int type,
            string desc, string intro, string inProgress, string complete, string alreadyDone,
            int battleMonsterId, string battleMonsterName, int rewardGold, int rewardExp,
            int rewardCompanionId, string rewardCompanionName, string rewardItems, string requiredItems,
            string prereqs, string stepsJson)
        {
            if (db == null || questId == 0) return false;
            try
            {
                string query = $@"
                    INSERT OR REPLACE INTO game_quests (
                        quest_id, name, npc_name_pattern, npc_template_id, map_id, type, description,
                        intro_dialogue, in_progress_dialogue, complete_dialogue, already_completed_dialogue,
                        battle_monster_id, battle_monster_name, reward_gold, reward_exp,
                        reward_companion_id, reward_companion_name, reward_items, required_items,
                        prerequisite_quests, steps_json
                    ) VALUES (
                        {questId}, '{EscapeSql(name)}', '{EscapeSql(npcPattern)}', {npcTid}, {mapId}, {type}, '{EscapeSql(desc)}',
                        '{EscapeSql(intro)}', '{EscapeSql(inProgress)}', '{EscapeSql(complete)}', '{EscapeSql(alreadyDone)}',
                        {battleMonsterId}, '{EscapeSql(battleMonsterName)}', {rewardGold}, {rewardExp},
                        {rewardCompanionId}, '{EscapeSql(rewardCompanionName)}', '{EscapeSql(rewardItems)}', '{EscapeSql(requiredItems)}',
                        '{EscapeSql(prereqs)}', '{EscapeSql(stepsJson)}'
                    )";
                db.ExecuteNonQuery(query);

                // Reload in memory
                LoadAllQuests(db);
                return true;
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[QuestDataBase] Error saving quest {questId}: {ex.Message}");
                return false;
            }
        }

        public static bool SaveQuest(GameDataBase db, uint questId, string name, string npcPattern, int npcTid, int type,
            string desc, string intro, string inProgress, string complete, string alreadyDone,
            int battleMonsterId, string battleMonsterName, int rewardGold, int rewardExp,
            int rewardCompanionId, string rewardCompanionName, string rewardItems, string requiredItems,
            string prereqs, string stepsJson)
        {
            return SaveQuest(db, questId, name, npcPattern, npcTid, 0, type, desc, intro, inProgress, complete, alreadyDone,
                battleMonsterId, battleMonsterName, rewardGold, rewardExp, rewardCompanionId, rewardCompanionName,
                rewardItems, requiredItems, prereqs, stepsJson);
        }

        public static bool DeleteQuest(GameDataBase db, uint questId)
        {
            if (db == null || questId == 0) return false;
            try
            {
                db.ExecuteNonQuery($"DELETE FROM game_quests WHERE quest_id={questId}");
                LoadAllQuests(db);
                return true;
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[QuestDataBase] Error deleting quest {questId}: {ex.Message}");
                return false;
            }
        }

        private static string GetJsonString(string block, string key)
        {
            var match = Regex.Match(block, $@"""{key}""\s*:\s*""([^""]*)""");
            return match.Success ? match.Groups[1].Value : null;
        }

        private static int GetJsonInt(string block, string key)
        {
            var match = Regex.Match(block, $@"""{key}""\s*:\s*(\d+)");
            return match.Success ? int.Parse(match.Groups[1].Value) : 0;
        }

        private static string EscapeSql(string input)
        {
            if (string.IsNullOrEmpty(input)) return "";
            return input.Replace("'", "''");
        }
    }
}
