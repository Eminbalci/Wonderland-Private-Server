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
        /// Returns the Level 1 starter elemental skill ID for a given affinity.
        /// (Advanced skills like Sword Awn Attack require STR 16 and must not be unlocked at Level 1).
        /// </summary>
        public static List<uint> GetStarterElementSkills(Affinity element)
        {
            List<uint> skills = new List<uint>();
            switch (element)
            {
                case Affinity.Earth: // 1
                    skills.Add(15085); // Rock Attack (Lv 1)
                    break;
                case Affinity.Water: // 2
                    skills.Add(15091); // Ice Attack (Lv 1)
                    break;
                case Affinity.Fire: // 3
                    skills.Add(11016); // Flame Attack (Lv 1)
                    break;
                case Affinity.Wind: // 4
                    skills.Add(11007); // Wind Attack (Lv 1)
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

            // Ensure starter stunt skill is unlocked
            uint stuntId = GetStarterStuntSkill((byte)player.Eqs.Body, player.Eqs.Head);
            if (!player.PlayerSkills.Any(s => s.SkillID == stuntId))
            {
                player.PlayerSkills.Add(new PlayerSkill(stuntId, 1, 0));
            }

            // Ensure starter elemental skills are unlocked
            var elemSkills = GetStarterElementSkills(player.Eqs.Element);
            foreach (var elemSk in elemSkills)
            {
                if (!player.PlayerSkills.Any(s => s.SkillID == elemSk))
                {
                    player.PlayerSkills.Add(new PlayerSkill(elemSk, 1, 0));
                }
            }

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

            uint stuntId = GetStarterStuntSkill((byte)player.Eqs.Body, player.Eqs.Head);
            if (!player.PlayerSkills.Any(s => s.SkillID == stuntId))
                player.PlayerSkills.Add(new PlayerSkill(stuntId, 1, 0));

            foreach (var elemSk in GetStarterElementSkills(player.Eqs.Element))
            {
                if (!player.PlayerSkills.Any(s => s.SkillID == elemSk))
                    player.PlayerSkills.Add(new PlayerSkill(elemSk, 1, 0));
            }

            // Check and include any stat progression skills qualified by current stats
            CheckAndUnlockProgressionSkillsNoSend(player);

            DebugSystem.Write($"[SkillManager] Initialized {player.PlayerSkills.Count} starter/progression skills for {player.CharName} (Stunt: {stuntId})");
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
                    if (player.Eqs.Wis >= 16) toUnlock.Add(11056); // Slowdown (WIS 16)
                    if (player.Eqs.Wis >= 26) toUnlock.Add(11002); // Poison Spell (WIS 26)
                    break;

                case Affinity.Earth: // 1
                    // Physical (STR)
                    if (player.Eqs.Str >= 16) toUnlock.Add(11017); // Earth Attack (STR 16)
                    if (player.Eqs.Str >= 26) toUnlock.Add(15056); // Rockfall Attack (STR 26)
                    if (player.Eqs.Str >= 38) toUnlock.Add(15049); // Jump Attack (STR 38)
                    // Magical (INT)
                    if (player.Eqs.Int >= 16) toUnlock.Add(11085); // Rock Hit (INT 16)
                    if (player.Eqs.Int >= 26) toUnlock.Add(15074); // Rock Beating (INT 26)
                    // Assistant (WIS / CON)
                    if (player.Eqs.Wis >= 16) toUnlock.Add(15070); // Tree Bind (WIS 16)
                    break;

                case Affinity.Water: // 2
                    // Physical (STR)
                    if (player.Eqs.Str >= 16) toUnlock.Add(15062); // Icicle Hit (STR 16)
                    if (player.Eqs.Str >= 26) toUnlock.Add(15019); // Turning Ice Attack (STR 26)
                    // Magical (INT)
                    if (player.Eqs.Int >= 16) toUnlock.Add(15092); // Ice Hit (INT 16)
                    if (player.Eqs.Int >= 26) toUnlock.Add(15093); // Ice Beating (INT 26)
                    // Healing / Assistant (WIS)
                    if (player.Eqs.Wis >= 16) toUnlock.Add(11001); // Icicle Attack / Recovery (WIS 16)
                    break;

                case Affinity.Wind: // 4
                    // Physical / Speed (AGI / STR)
                    if (player.Eqs.Agi >= 16 || player.Eqs.Str >= 16) toUnlock.Add(15079); // Air Attack (AGI 16)
                    if (player.Eqs.Agi >= 26) toUnlock.Add(15009); // Wind Cut Hit (AGI 26)
                    if (player.Eqs.Agi >= 38) toUnlock.Add(15002); // Instant Attack (AGI 38)
                    // Magical (INT)
                    if (player.Eqs.Int >= 16) toUnlock.Add(11014); // Wind Hit (INT 16)
                    if (player.Eqs.Int >= 26) toUnlock.Add(15113); // Wind Bead (INT 26)
                    break;
            }
            return toUnlock;
        }

        /// <summary>
        /// Checks player stats and unlocks any newly qualified progression skills in real-time.
        /// Dispatches AC 5:11 and AC 8:1 packets immediately.
        /// </summary>
        public static void CheckAndUnlockProgressionSkills(Player player)
        {
            if (player == null || player.Eqs == null) return;

            var qualified = GetQualifiedProgressionSkills(player);
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


