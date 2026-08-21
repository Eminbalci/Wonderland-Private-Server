using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Game;
using Game.Code;
using Network;

namespace Game.SkillRelated
{
    public class PlayerSkill
    {
        public uint SkillID { get; set; }
        public byte Grade { get; set; } = 1;
        public uint Exp { get; set; } = 0;

        public PlayerSkill() { }
        public PlayerSkill(uint skillId, byte grade = 1, uint exp = 0)
        {
            SkillID = skillId;
            Grade = grade;
            Exp = exp;
        }
    }

    public static class SkillManager
    {
        /// <summary>
        /// Gets the starter stunt skill ID based on character Body and Head selection.
        /// Replicates Python server gameserver.py get_starter_skill_id
        /// </summary>
        public static uint GetStarterStuntSkill(byte body, byte head)
        {
            switch (body)
            {
                case 4: // Big Female
                    switch (head)
                    {
                        case 0: return 15041; // Iris: Love Wish
                        case 1: return 12053; // Lique: Gallop
                        case 2: return 15003; // Vanessa: Newbie's Stunt
                        case 3: return 15060; // Breillat: Throw Dish
                        case 4: return 12051; // Jessica: Note
                        case 5: return 12049; // Konno Tsuruko: Fire Dance
                        case 6: return 11077; // Maria: Cure 2 Players
                        case 7: return 15040; // Karin: Palm
                    }
                    break;
                case 3: // Big Male
                    switch (head)
                    {
                        case 0: return 11076; // Combo x3 Attack
                        case 1: return 11076; // Combo x3 Attack
                        case 2: return 11183; // More: Deacon Attack
                        case 3: return 11182; // Kurogane: Ghost Hammer
                    }
                    break;
                case 2: // Small Female
                    switch (head)
                    {
                        case 0: return 15039; // Nina: Wine Flame
                        case 1: return 12036; // Betty: Leap
                    }
                    break;
                case 1: // Small Male
                    switch (head)
                    {
                        case 0: return 11075; // Rocco: Summon Dogs Groups
                    }
                    break;
            }
            return 15003; // Default fallback: Newbie's Stunt
        }

        /// <summary>
        /// Returns the Level 1 starter elemental skill IDs for a given affinity (Physical, Magical, Assistant branches).
        /// </summary>
        public static List<uint> GetStarterElementSkills(Affinity element)
        {
            List<uint> skills = new List<uint>();
            switch (element)
            {
                case Affinity.Fire: // 3
                    skills.Add(11016); // Flame Attack (Magical Lv 1)
                    skills.Add(11166); // Blast Attack (Physical Lv 1)
                    skills.Add(11056); // Slowdown (Assistant Lv 1)
                    break;
                case Affinity.Earth: // 1
                    skills.Add(15085); // Rock Attack (Magical Lv 1)
                    skills.Add(11017); // Earth Attack (Physical Lv 1)
                    skills.Add(11057); // Shield Defence (Assistant Lv 1)
                    break;
                case Affinity.Water: // 2
                    skills.Add(15091); // Ice Attack (Magical Lv 1)
                    skills.Add(11001); // Icicle Attack (Physical Lv 1)
                    skills.Add(15100); // Detoxification (Assistant Lv 1)
                    break;
                case Affinity.Wind: // 4
                    skills.Add(11007); // Wind Attack (Magical Lv 1)
                    skills.Add(15079); // Air Attack (Physical Lv 1)
                    skills.Add(11052); // Speed Up (Assistant Lv 1)
                    break;
                case Affinity.Dark: // 5
                case Affinity.Undefined: // 7
                    skills.Add(25115); // Fiery Wave / Dark Wave (Magical Lv 1)
                    skills.Add(25116); // Deadly Wind / Dark Strike (Physical Lv 1)
                    skills.Add(25110); // Poisonous Chill / Chaos (Assistant Lv 1)
                    break;
            }
            return skills;
        }

        /// <summary>
        /// Unlocks or updates a skill on the player and dispatches the proper AC 5:11 and AC 8:1 packets.
        /// </summary>
        public static void UnlockSkill(Player player, uint skillId, byte grade = 1, uint exp = 0)
        {
            if (player == null || skillId == 0) return;

            var existing = player.PlayerSkills.FirstOrDefault(s => s.SkillID == skillId);
            if (existing == null)
            {
                existing = new PlayerSkill(skillId, grade, exp);
                player.PlayerSkills.Add(existing);
            }
            else
            {
                existing.Grade = grade;
                existing.Exp = exp;
            }

            // Persist to database
            try
            {
                var db = DataBase.CharacterDataBase.GlobalInstance;
                if (db != null && player.CharID > 0)
                {
                    db.ExecuteNonQuery("CREATE TABLE IF NOT EXISTS character_skills (id INTEGER PRIMARY KEY AUTOINCREMENT, charID INT NOT NULL, skillID INT NOT NULL, grade TINYINT DEFAULT 1, exp INT DEFAULT 0, UNIQUE(charID, skillID));");
                    db.ExecuteNonQuery($"INSERT INTO character_skills (charID, skillID, grade, exp) VALUES ('{player.CharID}', '{skillId}', '{grade}', '{exp}') ON CONFLICT(charID, skillID) DO UPDATE SET grade = '{grade}', exp = '{exp}';");
                }
            }
            catch (Exception dbEx)
            {
                DebugSystem.Write($"[SkillManager] Error persisting skill {skillId} for {player.CharName}: {dbEx.Message}");
            }

            // 1. AC 5:11 (Unlock / Set Skill EXP)
            player.Send(Tools.FromFormat("bbdd", 5, 11, skillId, exp));

            // 2. AC 8:1 stat 110 (Skill Grade Update)
            player.Send(Tools.FromFormat("bbbbdd", 8, 1, 110, 1, (uint)grade, skillId));

            // 3. Dispatch skill slots and finalize packet
            SendSkillSlots(player);

            DebugSystem.Write($"[SkillManager] Unlocked/Updated skill {skillId} (Grade {grade}, EXP {exp}) for {player.CharName}");
        }

        /// <summary>
        /// Sends AC 5:13 skill slot mappings and AC 5:4 finalize packet.
        /// </summary>
        public static void SendSkillSlots(Player player)
        {
            if (player == null || player.PlayerSkills == null) return;

            byte slot = 1;
            foreach (var sk in player.PlayerSkills)
            {
                player.Send(Tools.FromFormat("bbbw", 5, 13, slot++, (ushort)sk.SkillID));
            }

            // AC 5:4 (Skill finalize packet)
            player.Send(Tools.FromFormat("bb", 5, 4));
        }

        /// <summary>
        /// Initializes player skills on login or creation and dispatches packets.
        /// </summary>
        public static void InitializePlayerSkills(Player player)
        {
            if (player == null) return;

            InitializePlayerSkillsNoSend(player);

            // Send all learned skills to client
            SendAllSkills(player);
        }

        /// <summary>
        /// Populates PlayerSkills list in-memory without sending any packets.
        /// Use this BEFORE AC 5:3 is sent — call SendAllSkills separately after AC 5:3.
        /// </summary>
        public static void InitializePlayerSkillsNoSend(Player player)
        {
            if (player == null) return;

            player.PlayerSkills.Clear();

            byte bodyVal = (byte)player.Body;
            if (bodyVal == 0 && player.Eqs != null) bodyVal = (byte)player.Eqs.Body;

            byte headVal = (byte)player.Head;
            if (headVal == 0 && player.Eqs != null) headVal = (byte)player.Eqs.Head;

            uint stuntId = GetStarterStuntSkill(bodyVal, headVal);
            if (!player.PlayerSkills.Any(s => s.SkillID == stuntId))
                player.PlayerSkills.Add(new PlayerSkill(stuntId, 1, 0));

            var elem = player.Element != Affinity.Normal ? player.Element : (player.Eqs != null ? player.Eqs.Element : Affinity.Fire);
            foreach (var elemSk in GetStarterElementSkills(elem))
            {
                if (!player.PlayerSkills.Any(s => s.SkillID == elemSk))
                    player.PlayerSkills.Add(new PlayerSkill(elemSk, 1, 0));
            }

            // Check and include any stat progression skills qualified by current stats
            CheckAndUnlockProgressionSkillsNoSend(player);

            // Load saved skills, grades, and exp from character_skills database table
            try
            {
                var db = DataBase.CharacterDataBase.GlobalInstance;
                if (db != null && player.CharID > 0)
                {
                    db.ExecuteNonQuery("CREATE TABLE IF NOT EXISTS character_skills (id INTEGER PRIMARY KEY AUTOINCREMENT, charID INT NOT NULL, skillID INT NOT NULL, grade TINYINT DEFAULT 1, exp INT DEFAULT 0, UNIQUE(charID, skillID));");
                    var dbSkills = db.GetDataTable($"SELECT skillID, grade, exp FROM character_skills WHERE charID = '{player.CharID}';");
                    if (dbSkills != null && dbSkills.Rows.Count > 0)
                    {
                        foreach (System.Data.DataRow r in dbSkills.Rows)
                        {
                            uint sId = Convert.ToUInt32(r["skillID"]);
                            byte grade = Convert.ToByte(r["grade"]);
                            uint exp = Convert.ToUInt32(r["exp"]);

                            var existing = player.PlayerSkills.FirstOrDefault(s => s.SkillID == sId);
                            if (existing != null)
                            {
                                existing.Grade = grade;
                                existing.Exp = exp;
                            }
                            else
                            {
                                player.PlayerSkills.Add(new PlayerSkill(sId, grade, exp));
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[SkillManager] Error loading character_skills for {player.CharName}: {ex.Message}");
            }

            DebugSystem.Write($"[SkillManager] Initialized {player.PlayerSkills.Count} starter/progression skills for {player.CharName} (Body: {bodyVal}, Head: {headVal}, Stunt: {stuntId}, Element: {elem})");
        }

        /// <summary>
        /// Populates qualified skill tree progression skills into PlayerSkills without sending packets.
        /// </summary>
        public static void CheckAndUnlockProgressionSkillsNoSend(Player player)
        {
            if (player == null || player.Eqs == null) return;

            var qualified = GetQualifiedProgressionSkills(player);
            foreach (var skId in qualified)
            {
                if (!player.PlayerSkills.Any(s => s.SkillID == skId))
                {
                    player.PlayerSkills.Add(new PlayerSkill(skId, 1, 0));
                }
            }
        }

        /// <summary>
        /// Returns list of skill IDs qualified by the player's current stats and element.
        /// </summary>
        public static List<uint> GetQualifiedProgressionSkills(Player player)
        {
            List<uint> toUnlock = new List<uint>();
            if (player == null || player.Eqs == null) return toUnlock;

            switch (player.Eqs.Element)
            {
                case Affinity.Fire: // 3
                    // Physical (STR)
                    if (player.Eqs.Str >= 16) toUnlock.Add(15101); // Sword Awn Attack (STR 16)
                    if (player.Eqs.Str >= 26) toUnlock.Add(11114); // Turning Fire Attack (STR 26)
                    if (player.Eqs.Str >= 38) toUnlock.Add(12039); // Fire Wave Attack (STR 38)
                    if (player.Eqs.Str >= 51) toUnlock.Add(15015); // Five Star Hit (STR 51)
                    if (player.Eqs.Str >= 66) toUnlock.Add(15044); // Hagendis Attack (STR 66)
                    // Magical (INT)
                    if (player.Eqs.Int >= 16) toUnlock.Add(11029); // Flame Hit / Fireball (INT 16)
                    if (player.Eqs.Int >= 26) toUnlock.Add(11025); // Flame Beating (INT 26)
                    if (player.Eqs.Int >= 38) toUnlock.Add(11034); // Fire Ball Attack (INT 38)
                    // Assistant (WIS / CON)
                    if (player.Eqs.Wis >= 16) toUnlock.Add(11002); // Poison Spell (WIS 16)
                    if (player.Eqs.Wis >= 26) toUnlock.Add(11072); // Fiery Attack (WIS 26)
                    if (player.Eqs.Wis >= 38) toUnlock.Add(11003); // Mess Spell (WIS 38)
                    break;

                case Affinity.Earth: // 1
                    // Physical (STR)
                    if (player.Eqs.Str >= 16) toUnlock.Add(15056); // Rockfall Attack (STR 16)
                    if (player.Eqs.Str >= 26) toUnlock.Add(11019); // Rock Blast Attack (STR 26)
                    if (player.Eqs.Str >= 38) toUnlock.Add(15049); // Jump Attack (STR 38)
                    // Magical (INT)
                    if (player.Eqs.Int >= 16) toUnlock.Add(11085); // Rock Hit (INT 16)
                    if (player.Eqs.Int >= 26) toUnlock.Add(15074); // Rock Beating (INT 26)
                    if (player.Eqs.Int >= 38) toUnlock.Add(11031); // Rock Ball Attack (INT 38)
                    // Assistant (WIS / CON)
                    if (player.Eqs.Wis >= 16) toUnlock.Add(15070); // Tree Bind (WIS 16)
                    if (player.Eqs.Wis >= 26) toUnlock.Add(12043); // Wake Spell (WIS 26)
                    break;

                case Affinity.Water: // 2
                    // Physical (STR)
                    if (player.Eqs.Str >= 16) toUnlock.Add(15062); // Icicle Hit (STR 16)
                    if (player.Eqs.Str >= 26) toUnlock.Add(15019); // Turning Ice Attack (STR 26)
                    // Magical (INT)
                    if (player.Eqs.Int >= 16) toUnlock.Add(15092); // Ice Hit (INT 16)
                    if (player.Eqs.Int >= 26) toUnlock.Add(15093); // Ice Beating (INT 26)
                    if (player.Eqs.Int >= 38) toUnlock.Add(11110); // Ice Ball Attack (INT 38)
                    // Healing / Assistant (WIS)
                    if (player.Eqs.Wis >= 16) toUnlock.Add(11042); // Cure Spell (WIS 16)
                    if (player.Eqs.Wis >= 26) toUnlock.Add(12048); // Ice-out (WIS 26)
                    break;

                case Affinity.Wind: // 4
                    // Physical / Speed (AGI / STR)
                    if (player.Eqs.Agi >= 16 || player.Eqs.Str >= 16) toUnlock.Add(15009); // Wind Cut Hit (AGI 16)
                    if (player.Eqs.Agi >= 26) toUnlock.Add(15002); // Instant Attack (AGI 26)
                    if (player.Eqs.Agi >= 38) toUnlock.Add(15114); // Dead Wind Attack (AGI 38)
                    // Magical (INT)
                    if (player.Eqs.Int >= 16) toUnlock.Add(11014); // Wind Hit (INT 16)
                    if (player.Eqs.Int >= 26) toUnlock.Add(15113); // Wind Bead (INT 26)
                    if (player.Eqs.Int >= 38) toUnlock.Add(15123); // Whirlwind Attack (INT 38)
                    // Assistant (WIS / CON)
                    if (player.Eqs.Wis >= 16) toUnlock.Add(11073); // Shield Smash (WIS 16)
                    if (player.Eqs.Wis >= 26) toUnlock.Add(12046); // Unload Wall (WIS 26)
                    if (player.Eqs.Wis >= 38) toUnlock.Add(15032); // Cord Spell (WIS 38)
                    break;

                case Affinity.Dark: // 5
                case Affinity.Undefined: // 7
                    // Physical (STR / AGI)
                    if (player.Eqs.Str >= 16) toUnlock.Add(25165); // Crack Beating (STR 16)
                    if (player.Eqs.Str >= 26) toUnlock.Add(25169); // Super Crack Beating (STR 26)
                    if (player.Eqs.Str >= 38) toUnlock.Add(25175); // Furious Cyclone (STR 38)
                    if (player.Eqs.Str >= 51) toUnlock.Add(25185); // Entangled Wind (STR 51)
                    // Magical (INT)
                    if (player.Eqs.Int >= 16) toUnlock.Add(25246); // Hellfire (INT 16)
                    if (player.Eqs.Int >= 26) toUnlock.Add(25248); // Polar Demonitis (INT 26)
                    if (player.Eqs.Int >= 38) toUnlock.Add(25275); // Icefall Explosion (INT 38)
                    // Assistant (WIS)
                    if (player.Eqs.Wis >= 16) toUnlock.Add(25167); // Chaos Curse (WIS 16)
                    if (player.Eqs.Wis >= 26) toUnlock.Add(25168); // Entangled Curse (WIS 26)
                    if (player.Eqs.Wis >= 38) toUnlock.Add(25470); // Summon Death (WIS 38)
                    break;
            }
            return toUnlock;
        }

        /// <summary>
        /// Returns skill evolutions (e.g. Attack -> Hit -> Beating) unlocked when a preceding skill reaches Grade 10.
        /// </summary>
        public static List<uint> GetQualifiedEvolutionSkills(Player player)
        {
            List<uint> evolutions = new List<uint>();
            if (player == null || player.PlayerSkills == null) return evolutions;

            foreach (var sk in player.PlayerSkills.ToList())
            {
                if (sk.Grade >= 10)
                {
                    switch (sk.SkillID)
                    {
                        // --- Fire ---
                        case 11016: evolutions.Add(11029); break; // Flame Attack (Grade 10) -> Flame Hit
                        case 11029: evolutions.Add(11025); break; // Flame Hit (Grade 10) -> Flame Beating
                        case 11025: evolutions.Add(11034); break; // Flame Beating (Grade 10) -> Fire Ball Attack
                        case 11166: evolutions.Add(15101); evolutions.Add(15104); break; // Blast Attack (Grade 10) -> Sword Awn Attack & Blast Hit
                        case 15101: evolutions.Add(11114); break; // Sword Awn Attack (Grade 10) -> Turning Fire Attack
                        case 11114: evolutions.Add(12039); break; // Turning Fire Attack (Grade 10) -> Fire Wave Attack
                        case 12039: evolutions.Add(15015); break; // Fire Wave Attack (Grade 10) -> Five Star Hit
                        case 15015: evolutions.Add(15044); break; // Five Star Hit (Grade 10) -> Hagendis Attack
                        case 11056: evolutions.Add(11002); break; // Slowdown (Grade 10) -> Poison Spell
                        case 11002: evolutions.Add(11072); break; // Poison Spell (Grade 10) -> Fiery Attack
                        case 11072: evolutions.Add(11003); break; // Fiery Attack (Grade 10) -> Mess Spell

                        // --- Earth ---
                        case 15085: evolutions.Add(11085); break; // Rock Attack (Grade 10) -> Rock Hit
                        case 11085: evolutions.Add(15074); break; // Rock Hit (Grade 10) -> Rock Beating
                        case 15074: evolutions.Add(11031); break; // Rock Beating (Grade 10) -> Rock Ball Attack
                        case 11017: evolutions.Add(15056); break; // Earth Attack (Grade 10) -> Rockfall Attack
                        case 15056: evolutions.Add(11019); break; // Rockfall Attack (Grade 10) -> Rock Blast Attack
                        case 11019: evolutions.Add(15049); break; // Rock Blast Attack (Grade 10) -> Jump Attack
                        case 11057: evolutions.Add(15070); break; // Shield Defence (Grade 10) -> Tree Bind
                        case 15070: evolutions.Add(12043); break; // Tree Bind (Grade 10) -> Wake Spell

                        // --- Water ---
                        case 15091: evolutions.Add(15092); break; // Ice Attack (Grade 10) -> Ice Hit
                        case 15092: evolutions.Add(15093); break; // Ice Hit (Grade 10) -> Ice Beating
                        case 15093: evolutions.Add(11110); break; // Ice Beating (Grade 10) -> Ice Ball Attack
                        case 11001: evolutions.Add(15062); break; // Icicle Attack (Grade 10) -> Icicle Hit
                        case 15062: evolutions.Add(15019); break; // Icicle Hit (Grade 10) -> Turning Ice Attack
                        case 15100: evolutions.Add(11042); break; // Detoxification (Grade 10) -> Cure Spell
                        case 11042: evolutions.Add(12048); break; // Cure Spell (Grade 10) -> Ice-out

                        // --- Wind ---
                        case 11007: evolutions.Add(11014); break; // Wind Attack (Grade 10) -> Wind Hit
                        case 11014: evolutions.Add(15113); break; // Wind Hit (Grade 10) -> Wind Bead
                        case 15113: evolutions.Add(15123); break; // Wind Bead (Grade 10) -> Whirlwind Attack
                        case 15079: evolutions.Add(15009); break; // Air Attack (Grade 10) -> Wind Cut Hit
                        case 15009: evolutions.Add(15002); break; // Wind Cut Hit (Grade 10) -> Instant Attack
                        case 15002: evolutions.Add(15114); break; // Instant Attack (Grade 10) -> Dead Wind Attack
                        case 11052: evolutions.Add(11073); break; // Speed Up (Grade 10) -> Shield Smash
                        case 11073: evolutions.Add(12046); break; // Shield Smash (Grade 10) -> Unload Wall
                        case 12046: evolutions.Add(15032); break; // Unload Wall (Grade 10) -> Cord Spell

                        // --- Dark / Undefined ---
                        case 25110: evolutions.Add(25113); break; // Poisonous Chill (Grade 10) -> Poisonous Wave
                        case 25115: evolutions.Add(25246); break; // Fiery Wave / Dark Wave (Grade 10) -> Hellfire
                        case 25246: evolutions.Add(25248); break; // Hellfire (Grade 10) -> Polar Demonitis
                        case 25116: evolutions.Add(25165); break; // Deadly Wind (Grade 10) -> Crack Beating
                        case 25165: evolutions.Add(25169); break; // Crack Beating (Grade 10) -> Super Crack Beating
                        case 25167: evolutions.Add(25168); break; // Chaos Curse (Grade 10) -> Entangled Curse
                        case 25168: evolutions.Add(25470); break; // Entangled Curse (Grade 10) -> Summon Death
                    }
                }
            }

            return evolutions;
        }

        /// <summary>
        /// Adds skill EXP to a learned skill, handles Grade level up (1-10),
        /// persists changes to database, dispatches AC 5:11 / AC 8:1 packets,
        /// and unlocks evolved skill versions if Grade 10 is attained.
        /// </summary>
        public static void AddSkillExp(Player player, uint skillId, uint expGain)
        {
            if (player == null || skillId == 0) return;

            var sk = player.PlayerSkills.FirstOrDefault(s => s.SkillID == skillId);
            if (sk == null)
            {
                sk = new PlayerSkill(skillId, 1, 0);
                player.PlayerSkills.Add(sk);
            }

            if (sk.Grade >= 10) return; // Max Grade

            sk.Exp += expGain;

            // Standard WLO Grade Exp Threshold: Grade * 100 EXP
            uint neededExp = (uint)(sk.Grade * 100);
            bool gradeUp = false;
            while (sk.Exp >= neededExp && sk.Grade < 10)
            {
                sk.Exp -= neededExp;
                sk.Grade++;
                neededExp = (uint)(sk.Grade * 100);
                gradeUp = true;
            }

            // Persist to database
            try
            {
                var db = DataBase.CharacterDataBase.GlobalInstance;
                if (db != null && player.CharID > 0)
                {
                    db.ExecuteNonQuery($"INSERT INTO character_skills (charID, skillID, grade, exp) VALUES ('{player.CharID}', '{skillId}', '{sk.Grade}', '{sk.Exp}') ON CONFLICT(charID, skillID) DO UPDATE SET grade = '{sk.Grade}', exp = '{sk.Exp}';");
                }
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[SkillManager] Error updating skill EXP: {ex.Message}");
            }

            // 1. AC 5:11 (EXP update)
            player.Send(Tools.FromFormat("bbdd", 5, 11, sk.SkillID, sk.Exp));

            // 2. AC 8:1 stat 110 (Grade update if leveled up)
            if (gradeUp)
            {
                player.Send(Tools.FromFormat("bbbbdd", 8, 1, 110, 1, (uint)sk.Grade, sk.SkillID));
                DebugSystem.Write($"[SkillManager] {player.CharName}'s skill {sk.SkillID} reached Grade {sk.Grade}!");

                // Check for new evolution skill unlocks
                CheckAndUnlockProgressionSkills(player);
            }
        }

        /// <summary>
        /// Checks player stats and Grade 10 evolutions and unlocks any newly qualified skills in real-time.
        /// Dispatches AC 5:11 and AC 8:1 packets immediately.
        /// </summary>
        public static void CheckAndUnlockProgressionSkills(Player player)
        {
            if (player == null || player.Eqs == null) return;

            var qualified = GetQualifiedProgressionSkills(player);
            var evolutions = GetQualifiedEvolutionSkills(player);
            foreach (var evo in evolutions)
            {
                if (!qualified.Contains(evo)) qualified.Add(evo);
            }

            bool newlyUnlocked = false;

            foreach (var skId in qualified)
            {
                if (!player.PlayerSkills.Any(s => s.SkillID == skId))
                {
                    player.PlayerSkills.Add(new PlayerSkill(skId, 1, 0));

                    // 1. AC 5:11 (Unlock Skill + EXP)
                    player.Send(Tools.FromFormat("bbdd", 5, 11, skId, 0));

                    // 2. AC 8:1 stat 110 (Skill Grade Update)
                    player.Send(Tools.FromFormat("bbbbdd", 8, 1, 110, 1, 1, skId));

                    newlyUnlocked = true;
                    DebugSystem.Write($"[SkillManager] Auto-unlocked progression skill {skId} for {player.CharName}");
                }
            }

            if (newlyUnlocked)
            {
                // Clear quickbar and refresh skill book
                for (byte a = 1; a < 11; a++)
                {
                    player.Send(Tools.FromFormat("bbbw", 5, 13, a, 0));
                }
                player.Send(Tools.FromFormat("bb", 5, 4));
            }
        }

        /// <summary>
        /// Dispatches AC 5:11 (Unlock Skill) and AC 8:1 stat 110 (Skill Grade) for all skills.
        /// Clears quickbar slots 1-10 (AC 5:13) and finalizes with AC 5:4.
        /// </summary>
        public static void SendAllSkills(Player player)
        {
            if (player == null || player.PlayerSkills == null || player.PlayerSkills.Count == 0)
            {
                DebugSystem.Write($"[SkillManager] SendAllSkills: no skills to send for {player?.CharName}");
                return;
            }

            DebugSystem.Write($"[SkillManager] Sending {player.PlayerSkills.Count} skills to {player.CharName}");
            foreach (var sk in player.PlayerSkills)
            {
                // 1. AC 5:11 (Unlock Skill + EXP)
                player.Send(Tools.FromFormat("bbdd", 5, 11, sk.SkillID, sk.Exp));

                // 2. AC 8:1 stat 110 (Skill Grade Update)
                player.Send(Tools.FromFormat("bbbbdd", 8, 1, 110, 1, (uint)sk.Grade, sk.SkillID));
            }

            // 3. Clear quickbar slots 1-10 to prevent duplicate ghost pins
            for (byte a = 1; a < 11; a++)
            {
                player.Send(Tools.FromFormat("bbbw", 5, 13, a, 0));
            }

            // 4. AC 5:4 (Skill finalize / refresh packet)
            player.Send(Tools.FromFormat("bb", 5, 4));
        }
    }
}


