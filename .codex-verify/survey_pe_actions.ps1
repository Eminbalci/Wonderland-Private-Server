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

$mapIds = @(10035, 11016, 12000, 12002, 12601, 12603, 12650)
foreach ($m in $mapIds) {
    $md = $mgr.GetMapData($m)
    if ($md -eq $null -or $md.PreEvents -eq $null) { continue }
    foreach ($pe in $md.PreEvents) {
        if ($pe.subentry1 -eq $null) { continue }
        foreach ($sub in $pe.subentry1) {
            if ($sub.subentry2 -ne $null -and $sub.subentry2.Count -gt 0) {
                foreach ($act in $sub.subentry2) {
                    if ($act.unknown -ne $null -and $act.unknown.Count -ge 5 -and $act.unknown[0] -eq 2) {
                        $targetClick = [BitConverter]::ToUInt16($act.unknown.ToArray(), 1)
                        $actType = [BitConverter]::ToUInt16($act.unknown.ToArray(), 3)
                        $actTypeStr = if ($actType -eq 2) { "HIDE" } elseif ($actType -eq 3) { "SHOW" } else { "TYPE_$actType" }
                        $npc = $md.Npclist | Where-Object { $_.clickId -eq $targetClick }
                        $npcName = if ($npc) { $npc.Name } else { "UNKNOWN" }
                        $npcTid = if ($npc) { $npc.npcId } else { 0 }
                        Write-Host "Map $m PE $($pe.clickID): Action $actTypeStr ClickID $targetClick ($npcName, TID $npcTid)"
                    }
                }
            }
        }
    }
}
