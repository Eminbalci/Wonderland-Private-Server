# Update SkillManager.cs
$smPath = "wlo.pserver.core/Game/SkillRelated/SkillManager.cs"
$smContent = [System.IO.File]::ReadAllText($smPath)

$targetSm = @"
                    if (player.Eqs.Wis >= 38) toUnlock.Add(25470); // Summon Death (WIS 38)
                    break;
            }
            return toUnlock;
        }
"@

$replacementSm = @"
                    if (player.Eqs.Wis >= 38) toUnlock.Add(25470); // Summon Death (WIS 38)
                    break;
            }

            if (player.Job != RebornJob.none)
            {
                ushort jobSkillId = GetJobSkillId(player.Job);
                if (jobSkillId > 0 && !toUnlock.Contains(jobSkillId))
                {
                    toUnlock.Add(jobSkillId);
                }
            }

            return toUnlock;
        }

        /// <summary>
        /// Returns the authentic Reborn Job Skill ID for a given RebornJob class.
        /// Killer -> Murder (15241), Warrior -> Holy Shield (15240), Knight -> Knight's Spirit (15242),
        /// Wit -> Curse (15243), Priest -> Rest (15244), Seer -> Foretell (15245).
        /// </summary>
        public static ushort GetJobSkillId(RebornJob job)
        {
            switch (job)
            {
                case RebornJob.Killer: return 15241;  // Murder
                case RebornJob.Warrior: return 15240; // Holy Shield
                case RebornJob.Knight: return 15242;  // Knight's Spirit
                case RebornJob.Wit: return 15243;     // Curse / Cuss
                case RebornJob.Priest: return 15244;  // Rest
                case RebornJob.Seer: return 15245;    // Foretell
                default: return 0;
            }
        }

        /// <summary>
        /// Teaches a skill directly to the player, updating memory list, client packets, and database.
        /// Dispatches AC 8:1 (stat 110), AC 5:12 (skill book grade), AC 5:11 (proficiency), and AC 5:4 (refresh).
        /// </summary>
        public static bool LearnSkill(Player player, ushort skillId, byte grade = 1, uint exp = 0)
        {
            if (player == null || skillId == 0) return false;
            if (player.PlayerSkills == null) player.PlayerSkills = new List<PlayerSkill>();

            var existing = player.PlayerSkills.FirstOrDefault(s => s.SkillID == skillId);
            if (existing != null)
            {
                if (grade > existing.Grade) existing.Grade = grade;
                existing.Exp = exp;
            }
            else
            {
                player.PlayerSkills.Add(new PlayerSkill(skillId, grade, exp));
            }

            uint clientSkillId = GetClientSkillId(player, skillId);
            player.Send(Tools.FromFormat("bbbbdd", 8, 1, 110, 1, (uint)grade, clientSkillId));
            player.Send(Tools.FromFormat("bbwb", 5, 12, (ushort)clientSkillId, grade));
            uint maxExp = (uint)(grade * 100);
            ushort prof = (ushort)Math.Min(10000, (exp * 10000) / Math.Max(1, maxExp));
            player.Send(Tools.FromFormat("bbdw", 5, 11, clientSkillId, prof));
            player.Send(Tools.FromFormat("bb", 5, 4));

            player.SaveCharacterData();
            return true;
        }
"@

if ($smContent.Contains("GetJobSkillId")) {
    Write-Output "SkillManager already has GetJobSkillId"
} else {
    $normSmContent = $smContent.Replace("`r`n", "`n")
    $normTargetSm = $targetSm.Replace("`r`n", "`n")
    $normReplacementSm = $replacementSm.Replace("`r`n", "`n")
    if ($normSmContent.Contains($normTargetSm)) {
        $normSmContent = $normSmContent.Replace($normTargetSm, $normReplacementSm)
        [System.IO.File]::WriteAllText($smPath, $normSmContent.Replace("`n", "`r`n"))
        Write-Output "Patched SkillManager.cs successfully"
    } else {
        Write-Error "Target not found in SkillManager.cs"
    }
}
