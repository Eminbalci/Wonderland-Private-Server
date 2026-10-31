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

for ($m = 0; $m -lt 1119; $m++) {
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
                        # Find the NPC on this map
                        $npc = $md.Npclist | Where-Object { $_.clickId -eq $targetClick }
                        if ($npc -ne $null -and $actType -eq 3) { # show
                            # Find if there is another NPC at same coordinates
                            $samePos = $md.Npclist | Where-Object { $_.clickId -ne $targetClick -and [Math]::Abs($_.x - $npc.x) -le 30 -and [Math]::Abs($_.y - $npc.y) -le 30 }
                            if ($samePos -ne $null) {
                                foreach ($sp in $samePos) {
                                    Write-Host ("Map " + $m + " PE " + $pe.clickID + ": Show NPC " + $targetClick + " (TID " + $npc.npcId + " '" + $npc.Name + "') at (" + $npc.x + "," + $npc.y + ") has companion NPC " + $sp.clickId + " (TID " + $sp.npcId + " '" + $sp.Name + "') at (" + $sp.x + "," + $sp.y + ")")
                                }
                            }
                        }
                    }
                }
            }
        }
    }
}
