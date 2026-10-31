$repoRoot = 'D:\GitHub\Wonderland-Private-Server'
Add-Type -Path "$repoRoot\PhoenixData\bin\Debug\net462\PhoenixData.dll"
$refs = @(
    (Join-Path $repoRoot 'RCLibrary\bin\Debug\net462\RCLibrary.dll'),
    (Join-Path $repoRoot 'PhoenixData\bin\Debug\net462\PhoenixData.dll'),
    (Join-Path $repoRoot 'wlo.pserver.core\bin\Debug\wlo.pserver.core.dll')
)
foreach ($a in $refs) { [void][Reflection.Assembly]::LoadFrom((Resolve-Path $a)) }

$mgr = New-Object Game.DataFiles.EveManager
$mgr.LoadFile((Join-Path $repoRoot 'Data\eve.Emg'))

$md = $mgr.GetMapData(12601)
$ev8 = $md.Events | Where-Object { $_.clickID -eq 8 }
for ($i = 0; $i -lt $ev8.SubEntry.Count; $i++) {
    $s = $ev8.SubEntry[$i]
    if ($s.unknownbyte1 -eq 5 -and $s.unknownword1 -eq 15266 -and $s.unknownword2 -eq 2) {
        Write-Host ("Ev 8 Sub " + $i + " has 15266 w2=2, w4=0x" + $s.unknownword4.ToString("X4") + ", hasActs=" + ($s.SubEntry -ne $null -and $s.SubEntry.Count -gt 0))
    }
}
