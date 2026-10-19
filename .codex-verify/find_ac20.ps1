$ErrorActionPreference = "Stop"
$lines = Get-Content "decompiled/aLogin_decompiled.c"
for ($i = 0; $i -lt $lines.Count; $i++) {
    if ($lines[$i] -match "case 0x14:") {
        $context = ($lines[$i..($i+30)]) -join "`n"
        if ($context -match "0x14|0x01|Dialog|Talk") {
            Write-Host ("Found at line {0}:" -f ($i+1))
            Write-Host ($lines[$i..($i+25)] -join "`n")
            Write-Host "-------------------------------------"
        }
    }
}
