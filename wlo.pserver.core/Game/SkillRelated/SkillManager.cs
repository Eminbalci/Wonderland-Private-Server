using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Game;
using Game.Code;
using Network;

namespace Game.SkillRelated
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 1)]
    public struct SkillInfoRaw
    {
        public byte SkillNameLength;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 20)]
        public byte[] SkillName;
        public byte Type;
        public ushort SkillID;
        public ushort SP;
        public byte ElementType;
        public ushort Attack;
        public byte EffectLayer;
        public byte UnknownByte1;
        public byte UnknownByte2;
        public double Decimal1;
        public double Decimal2;
        public byte UnknownByte3;
        public ushort AdditinalHarm;
        public byte NumberOfTurns;
        public byte Effect;
        public byte UnknownByte6;
        public ushort SkillID2;
        public ushort UnknownWord1;
        public ushort UnknownWord2;
        public ushort UnknownWord3;
        public ushort UnknownWord4;
        public ushort UnknownWord5;
        public byte DescriptionLength;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 30)]
        public byte[] Description;
        public ushort SkillTableOrder;
        public byte Target;
        public ushort ImageNumSmall;
        public ushort UnknownWord6;
        public byte UnknownByte7;
        public byte MaxSkillLevel;
        public byte AdditionalEffect;
        public ushort UnknownWord7;
        public byte SkillPattern1;
        public byte SkillPattern2;
        public byte SkillPattern3;
        public byte SkillPattern4;
        public byte TypeOfInjury;
        public byte UnknownByte8;
        public byte UnknownByte9;
        public byte UnknownByte10;
        public byte UnknownByte11;
        public ushort VoiceWav;
        public ushort UnknownWord8;
        public ushort UnknownWord9;
        public ushort UnknownWord10;
        public ushort UnknownWord11;
        public uint UnknownDword1;
        public uint UnknownDword2;
        public uint UnknownDword3;
        public uint UnknownDword4;
        public uint UnknownDword5;
    }

    public class SkillData
    {
        public ushort SkillId { get; set; }
        public string Name { get; set; }
        public byte Type { get; set; }
        public ushort SP { get; set; }
        public byte ElementType { get; set; }
        public ushort Attack { get; set; }
        public byte Effect { get; set; }
        public byte Target { get; set; }
        public byte AdditionalEffect { get; set; }
        public byte NumberOfTurns { get; set; }
        public byte TypeOfInjury { get; set; }
        public ushort SkillTableOrder { get; set; }

        public bool IsHeal => Target == 1 || Target == 3 || Target == 5 || (Name != null && (Name.Contains("Heal") || Name.Contains("Recover") || Name.Contains("Blessing") || Name.Contains("Cure") || Name.Contains("Rest")));
        public bool IsRevive => Name != null && (Name.Contains("Reviv") || Name.Contains("Restoration"));
        public bool IsShield => Effect == 62 || Effect == 103 || Effect == 107 || (Name != null && (Name.Contains("Shield") || Name.Contains("Barrier") || Name.Contains("Guard")));
        public bool IsHotBlooded => Effect == 73 || (Name != null && (Name.Contains("Hot-blooded") || Name.Contains("War Cry") || Name.Contains("Fiery")));
        public bool IsSpeedUp => Effect == 51 || (Name != null && Name.Contains("Speed"));
        public bool IsVanish => Effect == 53 || (Name != null && Name.Contains("Vanish"));

        public bool IsFreeze => Effect == 1 || Effect == 177 || AdditionalEffect == 3 || (Name != null && (Name.Contains("Freeze") || Name.Contains("Ice Seal") || Name.Contains("Curdle")));
        public bool IsSleep => Effect == 6 || (Name != null && (Name.Contains("Sleep") || Name.Contains("Dream")));
        public bool IsSeal => Effect == 4 || Effect == 178 || AdditionalEffect == 7 || (Name != null && (Name.Contains("Stone") || Name.Contains("Tree") || Name.Contains("Seal") || Name.Contains("Stun") || Name.Contains("Landification")));
        public bool IsConfuse => Effect == 173 || AdditionalEffect == 1 || (Name != null && (Name.Contains("Mess") || Name.Contains("Chaos") || Name.Contains("Confusion") || Name.Contains("Enchantment")));
        public bool IsPoison => Effect == 171 || (Name != null && (Name.Contains("Poison") || Name.Contains("Pollen") || Name.Contains("Miasma")));
        public bool IsParalyze => Effect == 8 || AdditionalEffect == 5 || (Name != null && (Name.Contains("Cord") || Name.Contains("Curse") || Name.Contains("Trap") || Name.Contains("Coma")));
    }

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
        private static readonly Dictionary<ushort, SkillData> _skillCatalog = new Dictionary<ushort, SkillData>();

        static SkillManager()
        {
            LoadSkillDatabase();
        }

        public static void LoadSkillDatabase()
        {
            if (_skillCatalog.Count > 0) return;
            try
            {
                string path = RCLibrary.Core.PathHelper.GetDataFilePath("Skill.dat");
                if (string.IsNullOrEmpty(path) || !File.Exists(path)) return;

                byte[] data = File.ReadAllBytes(path);
                int recordSize = 148;
                int total = data.Length / recordSize;

                for (int r = 0; r < total; r++)
                {
                    int offset = r * recordSize;
                    byte nameLen = data[offset];
                    byte[] nameBytes = new byte[20];
                    for (int i = 0; i < 20; i++) nameBytes[i] = data[offset + 1 + 19 - i];
                    string name = Encoding.ASCII.GetString(nameBytes).Trim('\0', ' ');

                    byte type = (byte)((data[offset + 21] ^ 0xFD) - 4);
                    ushort skillId = (ushort)((BitConverter.ToUInt16(data, offset + 22) ^ 0x6EA0) - 4);
                    ushort sp = (ushort)((BitConverter.ToUInt16(data, offset + 24) ^ 0x6EA0) - 4);
                    byte elem = (byte)((data[offset + 26] ^ 0xFD) - 4);
                    ushort attack = (ushort)((BitConverter.ToUInt16(data, offset + 27) ^ 0x6EA0) - 4);
                    ushort tableOrder = (ushort)((BitConverter.ToUInt16(data, offset + 97) ^ 0x6EA0) - 4);

                    if (skillId > 0 && !string.IsNullOrEmpty(name))
                    {
                        _skillCatalog[skillId] = new SkillData
                        {
                            SkillId = skillId,
                            Name = name,
                            Type = type,
                            SP = sp,
                            ElementType = elem,
                            Attack = attack,
                            SkillTableOrder = tableOrder
                        };
                    }
                }
                DebugSystem.Write($"[SkillManager] Loaded {_skillCatalog.Count} authentic skills from Skill.dat");
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[SkillManager] Error loading Skill.dat: {ex.Message}");
            }
        }

        public static SkillData GetSkill(ushort skillId)
        {
            if (_skillCatalog.Count == 0) LoadSkillDatabase();
            if (_skillCatalog.TryGetValue(skillId, out var skill)) return skill;
            return null;
        }
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
                        default: return 15003;
                    }
                case 3: // Big Male
                    switch (head)
                    {
                        case 0: return 11076; // Daniel: Combo x3 Attack
                        case 1: return 11076; // Sid: Combo x3 Attack
                        case 2: return 11183; // More: Deacon Attack
                        case 3: return 11182; // Kurogane: Ghost Hammer
                        default: return 11076;
                    }
                case 2: // Small Female
                    switch (head)
                    {
                        case 0: return 15039; // Nina: Wine Flame
                        case 1: return 12036; // Betty: Leap
                        default: return 15039;
                    }
                case 1: // Small Male
                    switch (head)
                    {
                        case 0: return 11075; // Rocco: Summon Dogs Groups
                        default: return 11075;
                    }
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

            // 1. AC 5:16 (Intro Tree Node Unlock: [5, 16, 0, (ushort)skillId, (byte)grade])
            SendPacket treePkt = new SendPacket();
            treePkt.Pack8(5);
            treePkt.Pack8(16);
            treePkt.Pack8(0);
            treePkt.Pack16((ushort)skillId);
            treePkt.Pack8(grade);
            player.Send(treePkt);

            // 2. Authentic AC 8:2 Skill Tree Unlock: [8, 2, 4, 2, 0, 0x6F, 0x01, (uint)grade, (uint)skillId]
            SendPacket ac8_2 = new SendPacket();
            ac8_2.Pack8(8);
            ac8_2.Pack8(2);
            ac8_2.Pack8(4);
            ac8_2.Pack16(2);
            ac8_2.Pack16(0x016F);
            ac8_2.Pack32((uint)grade);
            ac8_2.Pack32((uint)skillId);
            player.Send(ac8_2);

            // 3. AC 8:1 Stat 367 fallback
            player.Send(Tools.FromFormat("bbwdd", 8, 1, 0x016F, (uint)grade, (uint)skillId));

            DebugSystem.Write($"[SkillManager] Unlocked/Updated skill {skillId} (Grade {grade}, EXP {exp}) for {player.CharName}");
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
                    toUnlock.Add(11016); // Flame Attack (Starter)
                    if (player.Eqs.Str >= 16) toUnlock.Add(15101); // Sword Awn Attack (STR 16)
                    if (player.Eqs.Str >= 28) toUnlock.Add(11114); // Turning Fire Attack (STR 28)
                    if (player.Eqs.Str >= 46) toUnlock.Add(12039); // Fire Wave Attack (STR 46)
                    if (player.Eqs.Str >= 65 && player.Eqs.Wis >= 2) toUnlock.Add(15102); // Five Star Attack (STR 65, WIS 2)
                    if (player.Eqs.Str >= 85 && player.Eqs.Agi >= 4) toUnlock.Add(15044); // Hagendis Attack (STR 85, AGI 4)
                    toUnlock.Add(11166); // Blast Attack (Starter)
                    if (player.Eqs.Int >= 15 && player.Eqs.Wis >= 2) toUnlock.Add(11005); // Fire Blast Attack (INT 15, WIS 2)
                    if (player.Eqs.Int >= 27) toUnlock.Add(11034); // Fire Ball Attack (INT 27)
                    if (player.Eqs.Int >= 45) toUnlock.Add(15109); // Blaze Attack (INT 45)
                    if (player.Eqs.Int >= 66) toUnlock.Add(15111); // Fire Combo Attack (INT 66)
                    if (player.Eqs.Int >= 86 && player.Eqs.Wis >= 6) toUnlock.Add(11035); // Fire Stone Attack (INT 86, WIS 6)
                    toUnlock.Add(11056); // Slowdown (Starter)
                    if (player.Eqs.Wis >= 11) toUnlock.Add(11002); // Poison Spell (WIS 11)
                    if (player.Eqs.Wis >= 18) toUnlock.Add(11072); // Fiery Attack (WIS 18)
                    if (player.Eqs.Wis >= 29) toUnlock.Add(12045); // Free From Coma (WIS 29)
                    if (player.Eqs.Wis >= 42) toUnlock.Add(11003); // Mess Spell (WIS 42)
                    if (player.Eqs.Int >= 1 && player.Eqs.Wis >= 55) toUnlock.Add(15171); // Fire Attack (INT 1, WIS 55)
                    break;

                case Affinity.Earth: // 1
                    toUnlock.Add(15085); // Rock Attack (Starter)
                    if (player.Eqs.Str >= 16) toUnlock.Add(11017); // Earth Attack (STR 16)
                    if (player.Eqs.Str >= 28) toUnlock.Add(11087); // Rupture Attack (STR 28)
                    if (player.Eqs.Str >= 44 && player.Eqs.Wis >= 2) toUnlock.Add(15083); // Jump Attack (STR 44, WIS 2)
                    if (player.Eqs.Str >= 60 && player.Eqs.Con >= 5) toUnlock.Add(15049); // Earthquake Attack (STR 60, CON 5)
                    if (player.Eqs.Str >= 80 && player.Eqs.Agi >= 4) toUnlock.Add(15146); // Gold Attack (STR 80, AGI 4)
                    toUnlock.Add(12006); // Earth Shield (Starter)
                    if (player.Eqs.Int >= 16) toUnlock.Add(15056); // Rockfall Attack (INT 16)
                    if (player.Eqs.Int >= 28) toUnlock.Add(11019); // Rock Blast Attack (INT 28)
                    if (player.Eqs.Int >= 46) toUnlock.Add(11031); // Rock Ball Attack (INT 46)
                    if (player.Eqs.Int >= 67) toUnlock.Add(15086); // Stone Wall (INT 67)
                    if (player.Eqs.Int >= 90 && player.Eqs.Wis >= 3) toUnlock.Add(11107); // Meteorite (INT 90, WIS 3)
                    toUnlock.Add(11057); // Shield Defence (Starter)
                    if (player.Eqs.Wis >= 11) toUnlock.Add(12043); // Wake Spell (WIS 11)
                    if (player.Eqs.Wis >= 18) toUnlock.Add(12048); // Shield (WIS 18)
                    if (player.Eqs.Wis >= 29) toUnlock.Add(11055); // Rope Spell (WIS 29)
                    if (player.Eqs.Con >= 4 && player.Eqs.Wis >= 38) toUnlock.Add(15070); // Tree Bind (CON 4, WIS 38)
                    if (player.Eqs.Int >= 4 && player.Eqs.Wis >= 48) toUnlock.Add(15035); // Earth Dance (INT 4, WIS 48)
                    break;

                case Affinity.Water: // 2
                    toUnlock.Add(15091); // Ice Attack (Starter)
                    if (player.Eqs.Str >= 13) toUnlock.Add(11001); // Icicle Attack (STR 13)
                    if (player.Eqs.Str >= 19 && player.Eqs.Int >= 1) toUnlock.Add(11044); // Water Wave Attack (STR 19, INT 1)
                    if (player.Eqs.Str >= 27 && player.Eqs.Int >= 2) toUnlock.Add(15019); // Turning Ice Attack (STR 27, INT 2)
                    if (player.Eqs.Str >= 35 && player.Eqs.Int >= 3) toUnlock.Add(12007); // Ice Spike Attack (STR 35, INT 3)
                    if (player.Eqs.Str >= 58 && player.Eqs.Int >= 4) toUnlock.Add(15156); // Water Bomb (STR 58, INT 4)
                    toUnlock.Add(15097); // Ice Wall (Starter)
                    if (player.Eqs.Int >= 16) toUnlock.Add(11040); // Ice Ball Attack (INT 16)
                    if (player.Eqs.Int >= 28) toUnlock.Add(11110); // Ice Sword (INT 28)
                    if (player.Eqs.Int >= 46) toUnlock.Add(11113); // Freezing Attack (INT 46)
                    if (player.Eqs.Int >= 65 && player.Eqs.Wis >= 2) toUnlock.Add(11024); // Ice Shield (INT 65, WIS 2)
                    if (player.Eqs.Int >= 85 && player.Eqs.Wis >= 6) toUnlock.Add(15158); // Ice Dragon (INT 85, WIS 6)
                    toUnlock.Add(15100); // Detoxification (Starter)
                    if (player.Eqs.Wis >= 13) toUnlock.Add(11042); // Cure Spell (WIS 13)
                    if (player.Eqs.Wis >= 19) toUnlock.Add(15075); // Ice-out (WIS 19)
                    if (player.Eqs.Wis >= 25) toUnlock.Add(11080); // Healing Spell (WIS 25)
                    if (player.Eqs.Int >= 1 && player.Eqs.Wis >= 30) toUnlock.Add(11051); // Revival (INT 1, WIS 30)
                    if (player.Eqs.Wis >= 36) toUnlock.Add(11043); // Water Dance (WIS 36)
                    break;

                case Affinity.Wind: // 4
                    toUnlock.Add(11007); // Wind Attack (Starter)
                    if (player.Eqs.Str >= 5 && player.Eqs.Agi >= 10) toUnlock.Add(15079); // Wind Cut Attack (STR 5, AGI 10)
                    if (player.Eqs.Str >= 10 && player.Eqs.Agi >= 17) toUnlock.Add(15117); // Shadow Attack (STR 10, AGI 17)
                    if (player.Eqs.Agi >= 35) toUnlock.Add(15002); // Instant Attack (AGI 35)
                    if (player.Eqs.Str >= 20 && player.Eqs.Agi >= 46) toUnlock.Add(11046); // Dead Wind Attack (STR 20, AGI 46)
                    if (player.Eqs.Str >= 35 && player.Eqs.Agi >= 55) toUnlock.Add(15114); // Furious Wind (STR 35, AGI 55)
                    toUnlock.Add(30002); // Wind Shield (Starter)
                    if (player.Eqs.Int >= 15) toUnlock.Add(11015); // Whirlwind Attack (INT 15)
                    if (player.Eqs.Int >= 23 && player.Eqs.Wis >= 5) toUnlock.Add(15123); // Gale Attack (INT 23, WIS 5)
                    if (player.Eqs.Int >= 41) toUnlock.Add(15125); // Wind Storm Attack (INT 41)
                    if (player.Eqs.Int >= 62) toUnlock.Add(15048); // Tornado (INT 62)
                    if (player.Eqs.Int >= 86) toUnlock.Add(15161); // Hurricane (INT 86)
                    toUnlock.Add(11052); // Speed Spell (Starter)
                    if (player.Eqs.Wis >= 11) toUnlock.Add(11073); // Shield Smash (WIS 11)
                    if (player.Eqs.Wis >= 18) toUnlock.Add(12046); // Unload Wall (WIS 18)
                    if (player.Eqs.Int >= 4 && player.Eqs.Wis >= 25) toUnlock.Add(15032); // Cord Spell (INT 4, WIS 25)
                    if (player.Eqs.Wis >= 38) toUnlock.Add(15036); // Dispel (WIS 38)
                    if (player.Eqs.Int >= 7 && player.Eqs.Wis >= 49) toUnlock.Add(11026); // Wind Dance (INT 7, WIS 49)
                    break;

                case Affinity.Dark: // 5
                case Affinity.Undefined: // 7
                    if (player.Eqs.Str >= 16) toUnlock.Add(25165); // Crack Beating (STR 16)
                    if (player.Eqs.Str >= 26) toUnlock.Add(25169); // Super Crack Beating (STR 26)
                    if (player.Eqs.Str >= 38) toUnlock.Add(25175); // Furious Cyclone (STR 38)
                    if (player.Eqs.Str >= 51) toUnlock.Add(25185); // Entangled Wind (STR 51)
                    if (player.Eqs.Int >= 16) toUnlock.Add(25246); // Hellfire (INT 16)
                    if (player.Eqs.Int >= 26) toUnlock.Add(25248); // Polar Demonitis (INT 26)
                    if (player.Eqs.Int >= 38) toUnlock.Add(25275); // Icefall Explosion (INT 38)
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
                        // --- Fire Physical ---
                        case 11166: evolutions.Add(15104); break; // Blast Attack -> Blast Hit
                        case 15104: evolutions.Add(15105); break; // Blast Hit -> Blast Beating
                        case 15101: evolutions.Add(20003); break; // Sword Awn Attack -> Sword Awn Hit
                        case 20003: evolutions.Add(15089); break; // Sword Awn Hit -> Sword Awn Beating
                        case 11114: evolutions.Add(11115); break; // Turning Fire Attack -> Turning Fire Hit
                        case 11115: evolutions.Add(12022); break; // Turning Fire Hit -> Turning Fire Beating
                        case 12039: evolutions.Add(12034); break; // Fire Wave Attack -> Fire Wave Hit
                        case 12034: evolutions.Add(12038); break; // Fire Wave Hit -> Fire Wave Beating
                        case 15102: evolutions.Add(15015); break; // Five Star Attack -> Five Star Hit
                        case 15015: evolutions.Add(15103); break; // Five Star Hit -> Five Star Beating
                        case 15044: evolutions.Add(15017); break; // Hagendis Attack -> Hagendis Hit
                        case 15017: evolutions.Add(15045); break; // Hagendis Hit -> Hagendis Beating

                        // --- Fire Magical & Assistant ---
                        case 11016: evolutions.Add(11029); break; // Flame Attack -> Flame Hit
                        case 11029: evolutions.Add(11025); break; // Flame Hit -> Flame Beating
                        case 11005: evolutions.Add(30000); break; // Fire Blast Attack -> Fire Blast Hit
                        case 30000: evolutions.Add(15110); break; // Fire Blast Hit -> Fire Blast Beating
                        case 11034: evolutions.Add(11117); break; // Fire Ball Attack -> Fire Ball Hit
                        case 11117: evolutions.Add(15106); break; // Fire Ball Hit -> Fire Ball Beating
                        case 15109: evolutions.Add(15107); break; // Blaze Attack -> Blaze Hit
                        case 15107: evolutions.Add(15108); break; // Blaze Hit -> Blaze Beating
                        case 15111: evolutions.Add(12021); break; // Fire Combo Attack -> Fire Combo Hit
                        case 12021: evolutions.Add(12024); break; // Fire Combo Hit -> Fire Combo Beating
                        case 11035: evolutions.Add(15112); break; // Fire Stone Attack -> Fire Stone Hit
                        case 15112: evolutions.Add(11059); break; // Fire Stone Hit -> Fire Stone Bead
                        case 15171: evolutions.Add(11008); break; // Fire Attack -> Magma Attack
                        case 11008: evolutions.Add(12049); break; // Magma Attack -> Fire Dance

                        // --- Earth ---
                        case 11017: evolutions.Add(11086); break; // Earth Attack -> Earth Hit
                        case 11086: evolutions.Add(11065); break; // Earth Hit -> Earth Beating
                        case 11087: evolutions.Add(11071); break; // Rupture Attack -> Rupture Hit
                        case 11071: evolutions.Add(11088); break; // Rupture Hit -> Rupture Beating
                        case 15083: evolutions.Add(12025); break; // Jump Attack -> Jump Hit
                        case 12025: evolutions.Add(15084); break; // Jump Hit -> Jump Beating
                        case 15049: evolutions.Add(15020); break; // Earthquake Attack -> Earthquake Hit
                        case 15020: evolutions.Add(15043); break; // Earthquake Hit -> Earthquake Beating
                        case 15146: evolutions.Add(15147); break; // Gold Attack -> Gold Hit
                        case 15085: evolutions.Add(11085); break; // Rock Attack -> Rock Hit
                        case 11085: evolutions.Add(15074); break; // Rock Hit -> Rock Beating
                        case 15056: evolutions.Add(11091); break; // Rockfall Attack -> Rockfall Hit
                        case 11091: evolutions.Add(11093); break; // Rockfall Hit -> Rockfall Beating
                        case 11107: evolutions.Add(15152); break; // Meteorite -> Meteorite Hit
                        case 15152: evolutions.Add(15153); break; // Meteorite Hit -> Meteorite Beating

                        // --- Water ---
                        case 11001: evolutions.Add(15062); break; // Icicle Attack -> Icicle Hit
                        case 15062: evolutions.Add(15063); break; // Icicle Hit -> Icicle Beating
                        case 11044: evolutions.Add(11004); break; // Water Wave Attack -> Water Wave Hit
                        case 11004: evolutions.Add(11096); break; // Water Wave Hit -> Water Wave Beating
                        case 15019: evolutions.Add(15059); break; // Turning Ice Attack -> Turning Ice Hit
                        case 15059: evolutions.Add(15073); break; // Turning Ice Hit -> Turning Ice Beating
                        case 12007: evolutions.Add(11010); break; // Ice Spike Attack -> Ice Spike Hit
                        case 11010: evolutions.Add(21201); break; // Ice Spike Hit -> Ice Spike Beating
                        case 15156: evolutions.Add(15157); break; // Water Bomb -> Water Bomb Hit
                        case 15091: evolutions.Add(15092); break; // Ice Attack -> Ice Hit
                        case 15092: evolutions.Add(15093); break; // Ice Hit -> Ice Beating
                        case 11040: evolutions.Add(15096); break; // Ice Ball Attack -> Ice Ball Hit
                        case 15096: evolutions.Add(15095); break; // Ice Ball Hit -> Ice Ball Beating
                        case 11113: evolutions.Add(11100); break; // Freezing Attack -> Freezing Hit
                        case 11100: evolutions.Add(15098); break; // Freezing Hit -> Freezing Beating
                        case 15158: evolutions.Add(15159); break; // Ice Dragon -> Ice Dragon Hit
                        case 15159: evolutions.Add(15160); break; // Ice Dragon Hit -> Ice Dragon Beating

                        // --- Wind ---
                        case 15079: evolutions.Add(15009); break; // Wind Cut Attack -> Wind Cut Hit
                        case 15009: evolutions.Add(15010); break; // Wind Cut Hit -> Wind Cut Beating
                        case 15117: evolutions.Add(15118); break; // Shadow Attack -> Shadow Hit
                        case 15118: evolutions.Add(15119); break; // Shadow Hit -> Shadow Beating
                        case 15002: evolutions.Add(12028); break; // Instant Attack -> Instant Hit
                        case 12028: evolutions.Add(15004); break; // Instant Hit -> Instant Beating
                        case 11046: evolutions.Add(15024); break; // Dead Wind Attack -> Dead Wind Hit
                        case 15024: evolutions.Add(15025); break; // Dead Wind Hit -> Dead Wind Beating
                        case 15114: evolutions.Add(15115); break; // Furious Wind -> Furious Wind Hit
                        case 15115: evolutions.Add(15116); break; // Furious Wind Hit -> Furious Wind Beating
                        case 11007: evolutions.Add(11014); break; // Wind Attack -> Wind Hit
                        case 11014: evolutions.Add(15113); break; // Wind Hit -> Wind Bead
                        case 15123: evolutions.Add(15124); break; // Gale Attack -> Gale Hit
                        case 15124: evolutions.Add(15082); break; // Gale Hit -> Gale Beating
                        case 15125: evolutions.Add(15126); break; // Wind Storm Attack -> Wind Storm Hit
                        case 15126: evolutions.Add(15081); break; // Wind Storm Hit -> Wind Storm Beating
                        case 15048: evolutions.Add(15026); break; // Tornado -> Tornado Hit
                        case 15026: evolutions.Add(15027); break; // Tornado Hit -> Tornado Beating
                        case 15161: evolutions.Add(15162); break; // Hurricane -> Hurricane Hit
                        case 15162: evolutions.Add(15163); break; // Hurricane Hit -> Hurricane Beating
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
                    db.ExecuteNonQuery($"CREATE TABLE IF NOT EXISTS character_skills (id INTEGER PRIMARY KEY AUTOINCREMENT, charID INT NOT NULL, skillID INT NOT NULL, grade TINYINT DEFAULT 1, exp INT DEFAULT 0, UNIQUE(charID, skillID));");
                    db.ExecuteNonQuery($"INSERT OR REPLACE INTO character_skills (charID, skillID, grade, exp) VALUES ('{player.CharID}', '{skillId}', '{sk.Grade}', '{sk.Exp}');");
                }
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[SkillManager] Error updating skill EXP: {ex.Message}");
            }

            // 1. AC 5:11 (Proficiency / EXP sync: 0-10000 -> 0.00% - 100.00%)
            uint currentNeededExp = (uint)(sk.Grade * 100);
            ushort prof = (ushort)Math.Min(10000, (sk.Exp * 10000) / Math.Max(1, currentNeededExp));
            player.Send(Tools.FromFormat("bbdw", 5, 11, (uint)sk.SkillID, prof));

            // 2. AC 5:12 (Skill ID & Grade Update)
            player.Send(Tools.FromFormat("bbwb", 5, 12, (ushort)sk.SkillID, (byte)sk.Grade));

            // 3. AC 8:1 stat 367 / 0x016F (Skill Grade & Unlock Update)
            if (gradeUp)
            {
                player.Send(Tools.FromFormat("bbwdd", 8, 1, 0x016F, (uint)sk.Grade, sk.SkillID));
                DebugSystem.Write($"[SkillManager] {player.CharName}'s skill {sk.SkillID} reached Grade {sk.Grade}!");

                // Check for new evolution skill unlocks
                CheckAndUnlockProgressionSkills(player);
            }
        }

        /// <summary>
        /// Checks player stats and Grade 10 evolutions and unlocks any newly qualified skills in real-time.
        /// Dispatches authentic AC 5:12 and AC 8:1 packets immediately.
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

                    // 1. Authentic AC 8:2 Stat 110 (Skill Learned Notification & Buffer Update): [8, 2, 4, 1, 0, 110, 0, (uint)grade, (uint)skillId]
                    SendPacket learnPkt = new SendPacket();
                    learnPkt.Pack8(8);
                    learnPkt.Pack8(2);
                    learnPkt.Pack8(4);
                    learnPkt.Pack16(1);
                    learnPkt.Pack16(110);
                    learnPkt.Pack32((uint)1);
                    learnPkt.Pack32((uint)skId);
                    player.Send(learnPkt);

                    // 2. Authentic AC 8:2 Stat 367 (Skill Tree Node Unlock): [8, 2, 4, 1, 0, 0x6F, 0x01, (uint)grade, (uint)skillId]
                    SendPacket ac8_2 = new SendPacket();
                    ac8_2.Pack8(8);
                    ac8_2.Pack8(2);
                    ac8_2.Pack8(4);
                    ac8_2.Pack16(1);
                    ac8_2.Pack16(0x016F);
                    ac8_2.Pack32((uint)1);
                    ac8_2.Pack32((uint)skId);
                    player.Send(ac8_2);

                    // 3. AC 5:12 (Skill ID & Grade Update in Skill Book)
                    player.Send(Tools.FromFormat("bbwb", 5, 12, (ushort)skId, (byte)1));

                    // 4. AC 5:11 (Initial Proficiency 0.00%)
                    player.Send(Tools.FromFormat("bbdw", 5, 11, (uint)skId, (ushort)0));

                    newlyUnlocked = true;
                    DebugSystem.Write($"[SkillManager] Auto-unlocked progression skill {skId} for {player.CharName}");
                }
            }

            if (newlyUnlocked)
            {
                // Refresh full base stats and skill book in client
                player.Send_5_3();

                // Clear quickbar and refresh skill book
                for (byte a = 1; a < 11; a++)
                {
                    player.Send(Tools.FromFormat("bbbw", 5, 13, a, 0));
                }
                player.Send(Tools.FromFormat("bb", 5, 4));
                player.SaveCharacterData();
            }
        }

        /// <summary>
        /// Dispatches AC 8:2 stat 110 (Learned), stat 367 (Tree Unlock), AC 5:12 (Skill Grade) for all skills.
        /// Clears quickbar slots 1-10 (AC 5:13) and finalizes with AC 5:4.
        /// </summary>
        public static void SendAllSkills(Player player)
        {
            if (player == null) return;

            // Automatically unlock any skills qualified by player stats/level/element upon login or refresh
            CheckAndUnlockProgressionSkillsNoSend(player);

            if (player.PlayerSkills == null || player.PlayerSkills.Count == 0)
            {
                DebugSystem.Write($"[SkillManager] SendAllSkills: no skills to send for {player.CharName}");
                return;
            }

            DebugSystem.Write($"[SkillManager] Sending {player.PlayerSkills.Count} skills to {player.CharName}");
            foreach (var sk in player.PlayerSkills)
            {
                // 1. Authentic AC 8:2 Stat 110 (Skill Learned Notification & Buffer Update)
                SendPacket learnPkt = new SendPacket();
                learnPkt.Pack8(8);
                learnPkt.Pack8(2);
                learnPkt.Pack8(4);
                learnPkt.Pack16(1);
                learnPkt.Pack16(110);
                learnPkt.Pack32((uint)sk.Grade);
                learnPkt.Pack32((uint)sk.SkillID);
                player.Send(learnPkt);

                // 2. Authentic AC 8:2 Stat 367 (Skill Tree Node Unlock)
                SendPacket ac8_2 = new SendPacket();
                ac8_2.Pack8(8);
                ac8_2.Pack8(2);
                ac8_2.Pack8(4);
                ac8_2.Pack16(1);
                ac8_2.Pack16(0x016F);
                ac8_2.Pack32((uint)sk.Grade);
                ac8_2.Pack32((uint)sk.SkillID);
                player.Send(ac8_2);

                // 3. AC 5:12 (Skill ID & Grade Update in Skill Book)
                player.Send(Tools.FromFormat("bbwb", 5, 12, (ushort)sk.SkillID, (byte)sk.Grade));

                // 4. AC 5:11 (Skill Proficiency: 0-10000 -> 0.00% to 100.00%)
                uint maxExp = (uint)(sk.Grade * 100);
                ushort prof = (ushort)Math.Min(10000, (sk.Exp * 10000) / Math.Max(1, maxExp));
                player.Send(Tools.FromFormat("bbdw", 5, 11, (uint)sk.SkillID, prof));
            }

            // 5. Synchronize Companion Pet Skills
            if (player.PlayerPets != null)
            {
                foreach (var pet in player.PlayerPets.Values)
                {
                    if (pet != null && pet.PetID > 0)
                    {
                        QuestRelated.QuestManager.SendPetSkills(player, pet.PetID, pet.Slot);
                    }
                }
            }

            // 6. Finalize Skill Table Load with AC 5:4
            SendPacket fin = new SendPacket();
            fin.Pack8(5);
            fin.Pack8(4);
            player.Send(fin);
        }
    }
}


