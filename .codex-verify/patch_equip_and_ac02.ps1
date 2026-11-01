# Patch Equip.cs
$eqPath = "wlo.pserver.core/Game/PlayerRelated/Equip.cs"
$eqContent = [System.IO.File]::ReadAllText($eqPath)

$targetEq = @"
        public void SetLevel(byte targetLvl, bool grantPoints = true)
        {
            lock (m_Lock)
            {
                byte oldLvl = Level;
                long exp = 0;
                for (int l = 1; l < targetLvl; l++)
                {
                    exp += (uint)CalcMaxExp(BitConverter.GetBytes(Reborn)[0], l);
                }
                m_totalexp = exp;
                if (grantPoints && targetLvl > oldLvl)
                {
                    SkillPoints += (ushort)((targetLvl - oldLvl) * 3);
                }
                CurHP = FullHP;
                CurSP = FullSP;
            }
        }
"@

$replacementEq = @"
        public void SetLevel(byte targetLvl, bool grantPoints = true)
        {
            lock (m_Lock)
            {
                byte oldLvl = Level;
                long exp = 0;
                for (int l = 1; l < targetLvl; l++)
                {
                    exp += (uint)CalcMaxExp(BitConverter.GetBytes(Reborn)[0], l);
                }
                m_totalexp = exp;
                if (grantPoints && targetLvl > oldLvl)
                {
                    SkillPoints += (ushort)((targetLvl - oldLvl) * 3);
                }
                CurHP = FullHP;
                CurSP = FullSP;
            }

            if (this is Player player)
            {
                SendExp();
                player.Send_5_3();
                Send8_1(grantPoints);
                player.CurMap?.Broadcast(Tools.FromFormat("bbdw", 5, 5, player.CharID, (ushort)0));
                player.SaveCharacterData();
            }
        }
"@

$normEqContent = $eqContent.Replace("`r`n", "`n")
$normTargetEq = $targetEq.Replace("`r`n", "`n")
$normReplacementEq = $replacementEq.Replace("`r`n", "`n")

if ($normEqContent.Contains($normTargetEq)) {
    $normEqContent = $normEqContent.Replace($normTargetEq, $normReplacementEq)
    [System.IO.File]::WriteAllText($eqPath, $normEqContent.Replace("`n", "`r`n"))
    Write-Output "Patched Equip.cs successfully"
} else {
    Write-Error "Target not found in Equip.cs"
}

# Patch AC02.cs
$acPath = "Src/Network/ActionCodes/AC02.cs"
$acContent = [System.IO.File]::ReadAllText($acPath)

$targetAc = @"
                                    if (words.Length >= 2 && byte.TryParse(words[1], out byte newLvl)) {
                                        byte targetLvl = Math.Max((byte)1, Math.Min((byte)200, newLvl));
                                        p.Eqs.SetLevel(targetLvl);
                                        p.Eqs.Send8_1(true);
                                        DataBase.CharacterDataBase.GlobalInstance?.WritePlayer(p.CharID, p);
                                        p.SendSystemMessage(`$"[GM] Level updated to {p.Eqs.Level}! Available Stat Points: {p.Eqs.SkillPoints}");
                                    }
"@

$replacementAc = @"
                                    if (words.Length >= 2 && byte.TryParse(words[1], out byte newLvl)) {
                                        byte targetLvl = Math.Max((byte)1, Math.Min((byte)200, newLvl));
                                        p.Eqs.SetLevel(targetLvl);
                                        p.Eqs.SendExp();
                                        p.Send_5_3();
                                        p.Eqs.Send8_1(true);
                                        p.CurMap?.Broadcast(Tools.FromFormat("bbdw", 5, 5, p.CharID, (ushort)0));
                                        DataBase.CharacterDataBase.GlobalInstance?.WritePlayer(p.CharID, p);
                                        p.SendSystemMessage(`$"[GM] Level updated to {p.Eqs.Level}! Available Stat Points: {p.Eqs.SkillPoints}");
                                    }
"@

$normAcContent = $acContent.Replace("`r`n", "`n")
$normTargetAc = $targetAc.Replace("`r`n", "`n")
$normReplacementAc = $replacementAc.Replace("`r`n", "`n")

if ($normAcContent.Contains($normTargetAc)) {
    $normAcContent = $normAcContent.Replace($normTargetAc, $normReplacementAc)
    [System.IO.File]::WriteAllText($acPath, $normAcContent.Replace("`n", "`r`n"))
    Write-Output "Patched AC02.cs successfully"
} else {
    Write-Error "Target not found in AC02.cs"
}
