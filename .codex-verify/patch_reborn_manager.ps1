$path = "wlo.pserver.core\Game\PlayerRelated\RebornManager.cs"
$content = [System.IO.File]::ReadAllText($path)

$target = @"
            // Job drives the Reborn flag (Reborn => Job != none), so set it before
            // SetLevel so level 1 is computed on the reborn EXP curve.
            player.Job = (Game.RebornJob)(byte)job;
            player.RebornJob = (byte)job;
            player.SetLevel(1);
            player.FillHP();
            player.FillSP();

            // Refresh the client's EXP/HP/SP and the appearance (reborn flag).
            player.SendStat64(36, player.TotalExp);
            player.Send8_1(true);
            player.CurMap?.Broadcast(Tools.FromFormat("bbdw", 5, 5, player.CharID, (ushort)0));
            SendPacket pVisual = new SendPacket();
            pVisual.Pack8(23);
            pVisual.Pack8(122);
            pVisual.Pack32(player.CharID);
            player.CurMap?.Broadcast(pVisual);

            player.SaveCharacterData();

            Notify(player, $"Congratulations! You are now a Reborn {job} (Lv 1). Please relog to refresh your character.");
            DebugSystem.Write($"[RebornManager] Player {player.CharName} reborn as {job}.");
            return true;
"@

$replacement = @"
            // Job drives the Reborn flag (Reborn => Job != none), so set it before
            // SetLevel so new level is computed on the reborn EXP curve.
            // Authentic level deduction: Level is reduced by 100 from current level (minimum 1).
            // Example: Lv 100 -> Lv 1, Lv 120 -> Lv 20, Lv 199 -> Lv 99.
            byte newLevel = (byte)Math.Max(1, player.Level - 100);
            player.Job = (Game.RebornJob)(byte)job;
            player.RebornJob = (byte)job;
            player.SetLevel(newLevel, grantPoints: false);
            player.FillHP();
            player.FillSP();

            // Refresh the client's state with full login/status packets (AC 5:3, AC 8:1 stats, EXP).
            player.Send_5_3();
            player.SendExp();
            player.Send8_1(false);
            player.CurMap?.Broadcast(Tools.FromFormat("bbdw", 5, 5, player.CharID, (ushort)0));
            SendPacket pVisual = new SendPacket();
            pVisual.Pack8(23);
            pVisual.Pack8(122);
            pVisual.Pack32(player.CharID);
            player.CurMap?.Broadcast(pVisual);

            player.SaveCharacterData();

            Notify(player, $"Congratulations! You are now a Reborn {job} (Lv {newLevel}). Please relog to refresh your character.");
            DebugSystem.Write($"[RebornManager] Player {player.CharName} reborn as {job} (Lv {newLevel}).");
            return true;
"@

$targetLf = $target.Replace("`r`n", "`n")
$replacementLf = $replacement.Replace("`r`n", "`n")
$contentLf = $content.Replace("`r`n", "`n")

if ($contentLf.Contains($targetLf)) {
    $contentLf = $contentLf.Replace($targetLf, $replacementLf)
    if ($content.Contains("`r`n")) {
        $contentLf = $contentLf.Replace("`n", "`r`n")
    }
    [System.IO.File]::WriteAllText($path, $contentLf, [System.Text.Encoding]::UTF8)
    Write-Host "Replaced successfully"
} else {
    Write-Host "Target not found"
}
