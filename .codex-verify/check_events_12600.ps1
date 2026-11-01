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
Write-Host ("Events count: " + $m.Events.Count)
for ($i = 0; $i -lt $m.Events.Count; $i++) {
    $e = $m.Events[$i]
    Write-Host ("Index " + $i + " clickID=" + $e.clickID + " Name=" + $e.Name + " subcount=" + $e.subentry1.Count)
}
