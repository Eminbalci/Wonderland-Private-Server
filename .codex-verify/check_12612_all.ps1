$repo = (Get-Location).Path
$refs = @(
    (Join-Path $repo 'RCLibrary\bin\Debug\net462\RCLibrary.dll'),
    (Join-Path $repo 'PhoenixData\bin\Debug\net462\PhoenixData.dll'),
    (Join-Path $repo 'wlo.pserver.core\bin\Debug\wlo.pserver.core.dll')
)
foreach ($a in $refs) { [void][Reflection.Assembly]::LoadFrom((Resolve-Path $a)) }
$mgr = New-Object Game.DataFiles.EveManager
$mgr.LoadFile((Join-Path $repo 'Data\eve.Emg'))
$m = $mgr.GetMapData(12612)
foreach ($n in $m.Npclist) {
    Write-Host ("12612 NPC: clickID=" + $n.clickId + " TID=" + $n.npcId + " Name=" + $n.name + " Pos=(" + $n.x + "," + $n.y + ") b1=" + $n.unknownbyte1)
}
foreach ($pe in $m.PreEvents) {
    Write-Host ("12612 PE: clickID=" + $pe.clickID + " Name=" + $pe.Name + " subcount=" + $pe.subentry1.Count)
}
