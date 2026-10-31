$ErrorActionPreference = 'Stop'
$repo = (Get-Location).Path
$refs = @(
    (Join-Path $repo 'RCLibrary\bin\Debug\net462\RCLibrary.dll'),
    (Join-Path $repo 'PhoenixData\bin\Debug\net462\PhoenixData.dll'),
    (Join-Path $repo 'wlo.pserver.core\bin\Debug\wlo.pserver.core.dll')
)
foreach ($a in $refs) { [void][Reflection.Assembly]::LoadFrom((Resolve-Path $a)) }

$mgr = New-Object Game.DataFiles.EveManager
$mgr.LoadFile((Join-Path $repo 'Data\eve.Emg'))
$md = $mgr.GetMapData(12601)

for ($i = 0; $i -lt $md.PreEvents.Count; $i++) {
    $pe = $md.PreEvents[$i]
    Write-Host "PreEvent #$i (ClickID $($pe.clickID)): SubEntries=$($pe.subentry1.Count)"
    for ($j = 0; $j -lt $pe.subentry1.Count; $j++) {
        $sub = $pe.subentry1[$j]
        $acts = if ($sub.subentry2) { $sub.subentry2.Count } else { 0 }
        $hex = ($sub.unknown | ForEach-Object { $_.ToString("X2") }) -join ' '
        Write-Host "  Sub[$j]: Acts=$acts, Hex: $hex"
        if ($acts -gt 0) {
            foreach ($act in $sub.subentry2) {
                $actHex = ($act.unknown | ForEach-Object { $_.ToString("X2") }) -join ' '
                Write-Host "    Action: $actHex"
            }
        }
    }
}
