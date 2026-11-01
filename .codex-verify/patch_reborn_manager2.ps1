$rmPath = "wlo.pserver.core/Game/PlayerRelated/RebornManager.cs"
$rmContent = [System.IO.File]::ReadAllText($rmPath)

$target1 = @"
            // Authentic level deduction: Level is reduced by 100 from current level (minimum 1).
            // Example: Lv 100 -> Lv 1, Lv 120 -> Lv 20, Lv 199 -> Lv 99.
            byte newLevel = (byte)Math.Max(1, player.Level - 100);
            player.Job = (Game.RebornJob)(byte)job;
            player.RebornJob = (byte)job;
            player.SetLevel(newLevel, grantPoints: false);
            player.FillHP();
            player.FillSP();
"@

$replacement1 = @"
            // Authentic level deduction: Level is reduced by 99 from current level (minimum 1).
            // Example: Lv 100 -> Lv 1, Lv 120 -> Lv 21, Lv 130 -> Lv 31, Lv 199 -> Lv 100.
            byte newLevel = (byte)Math.Max(1, player.Level - 99);
            player.Job = (Game.RebornJob)(byte)job;
            player.RebornJob = (byte)job;
            player.SetLevel(newLevel, grantPoints: false);
            player.FillHP();
            player.FillSP();

            // Grant authentic Reborn Job Skill corresponding to chosen class
            ushort jobSkillId = SkillRelated.SkillManager.GetJobSkillId((Game.RebornJob)(byte)job);
            if (jobSkillId > 0)
            {
                SkillRelated.SkillManager.LearnSkill(player, jobSkillId, 1, 0);
            }
"@

$target2 = @"
            player.Job = (Game.RebornJob)(byte)job;
            player.RebornJob = (byte)job;
            player.Send8_1(false);
"@

$replacement2 = @"
            player.Job = (Game.RebornJob)(byte)job;
            player.RebornJob = (byte)job;

            // Grant authentic Reborn Job Skill for the new class
            ushort newJobSkillId = SkillRelated.SkillManager.GetJobSkillId((Game.RebornJob)(byte)job);
            if (newJobSkillId > 0)
            {
                SkillRelated.SkillManager.LearnSkill(player, newJobSkillId, 1, 0);
            }

            player.Send8_1(false);
"@

$normContent = $rmContent.Replace("`r`n", "`n")
$normTarget1 = $target1.Replace("`r`n", "`n")
$normReplacement1 = $replacement1.Replace("`r`n", "`n")
$normTarget2 = $target2.Replace("`r`n", "`n")
$normReplacement2 = $replacement2.Replace("`r`n", "`n")

if ($normContent.Contains($normTarget1) -and $normContent.Contains($normTarget2)) {
    $normContent = $normContent.Replace($normTarget1, $normReplacement1)
    $normContent = $normContent.Replace($normTarget2, $normReplacement2)
    [System.IO.File]::WriteAllText($rmPath, $normContent.Replace("`n", "`r`n"))
    Write-Output "Patched RebornManager.cs successfully"
} else {
    Write-Error "Target not found in RebornManager.cs"
}
