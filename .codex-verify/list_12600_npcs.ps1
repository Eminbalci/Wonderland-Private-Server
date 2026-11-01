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
foreach ($n in $m.Npclist) {
    Write-Host ("ClickID=" + $n.clickId + " TID=" + $n.npcId + " Name=" + $n.name + " Pos=(" + $n.x + "," + $n.y + ") b1=" + $n.unknownbyte1 + " EvCount=" + $n.eventCount)
}
