$repo = (Get-Location).Path
$refs = @(
    (Join-Path $repo 'RCLibrary\bin\Debug\net462\RCLibrary.dll'),
    (Join-Path $repo 'PhoenixData\bin\Debug\net462\PhoenixData.dll'),
    (Join-Path $repo 'wlo.pserver.core\bin\Debug\wlo.pserver.core.dll')
)
foreach ($a in $refs) { [void][Reflection.Assembly]::LoadFrom((Resolve-Path $a)) }
$mgr = New-Object Game.DataFiles.EveManager
$mgr.LoadFile((Join-Path $repo 'Data\eve.Emg'))
$m = $mgr.GetMapData(12600)
$pe0 = $m.PreEvents[0]
Write-Host ("PE0 ClickID=" + $pe0.clickID + " subcount=" + $pe0.subentry1.Count)
for ($i = 0; $i -lt $pe0.subentry1.Count; $i++) {
    $s = $pe0.subentry1[$i]
    $hex = ($s.unknown | ForEach-Object { $_.ToString('X2') }) -join ' '
    $actCount = if ($s.subentry2) { $s.subentry2.Count } else { 0 }
    Write-Host ("  Sub[" + $i + "] Acts=" + $actCount + " Hex=" + $hex)
    if ($actCount -gt 0) {
        foreach ($act in $s.subentry2) {
            Write-Host ("    Act: " + (($act.unknown | ForEach-Object { $_.ToString('X2') }) -join ' '))
        }
    }
}
