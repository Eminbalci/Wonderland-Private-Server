$eqPath = "wlo.pserver.core/Game/PlayerRelated/Equip.cs"
$eqContent = [System.IO.File]::ReadAllText($eqPath)

$target = @"
        public void SendStat(byte statId, int val)
        {
            Send(Tools.FromFormat("bbbbdd", 8, 1, statId, 1, val, 0));
        }

        public void SendStat64(byte statId, long val)
        {
            Send(Tools.FromFormat("bbbbl", 8, 1, statId, 1, val));
        }
"@

$replacement = @"
        public void SendStat(byte statId, int val)
        {
            Send?.Invoke(Tools.FromFormat("bbbbdd", 8, 1, statId, 1, val, 0));
        }

        public void SendStat64(byte statId, long val)
        {
            Send?.Invoke(Tools.FromFormat("bbbbl", 8, 1, statId, 1, val));
        }
"@

$target2 = @"
        public void SendExp()
        {
            SendPacket p = new SendPacket();
            p.PackArray(new byte[] { 8, 1 });
            p.Pack8(36);
            p.Pack8(1);
            p.Pack64((ulong)TotalExp);
            Send(p);
        }
"@

$replacement2 = @"
        public void SendExp()
        {
            SendPacket p = new SendPacket();
            p.PackArray(new byte[] { 8, 1 });
            p.Pack8(36);
            p.Pack8(1);
            p.Pack64((ulong)TotalExp);
            Send?.Invoke(p);
        }
"@

$normContent = $eqContent.Replace("`r`n", "`n")
$normTarget = $target.Replace("`r`n", "`n")
$normReplacement = $replacement.Replace("`r`n", "`n")
$normTarget2 = $target2.Replace("`r`n", "`n")
$normReplacement2 = $replacement2.Replace("`r`n", "`n")

if ($normContent.Contains($normTarget) -and $normContent.Contains($normTarget2)) {
    $normContent = $normContent.Replace($normTarget, $normReplacement)
    $normContent = $normContent.Replace($normTarget2, $normReplacement2)
    [System.IO.File]::WriteAllText($eqPath, $normContent.Replace("`n", "`r`n"))
    Write-Output "Patched Send?.Invoke in Equip.cs"
} else {
    Write-Error "Target not found in Equip.cs"
}
