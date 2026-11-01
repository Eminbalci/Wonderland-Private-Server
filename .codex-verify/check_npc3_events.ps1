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
$npc3 = $m.Npclist | Where-Object { $_.clickId -eq 3 }
Write-Host ("NPC 3 events count: " + $npc3.Events.Length)
for ($i = 0; $i -lt $npc3.Events.Length; $i++) {
    Write-Host ("NPC 3 Event[" + $i + "]: " + $npc3.Events[$i])
}
foreach ($pe in $m.PreEvents) {
    Write-Host ("PE ClickID: " + $pe.ClickID + " SubCount: " + $pe.SubEntries.Count)
    for ($i = 0; $i -lt $pe.SubEntries.Count; $i++) {
        $sub = $pe.SubEntries[$i]
        $acts = @()
        foreach ($act in $sub.Actions) {
            $bytes = $act.unknown
            if ($bytes.Length -ge 5 -and $bytes[0] -eq 2) {
                $tc = [BitConverter]::ToUInt16($bytes, 1)
                $type = [BitConverter]::ToUInt16($bytes, 3)
                $acts += ("actType=" + $type + " targetClick=" + $tc)
            }
        }
        $conds = @()
        foreach ($c in $sub.Conditions) {
            $conds += ("b1=" + $c.unknownbyte1 + " w1=" + $c.unknownword1 + " w2=" + $c.unknownword2 + " cmp=" + $c.comparison + " val=" + $c.value)
        }
        Write-Host ("  Sub[" + $i + "]: Conds=(" + ($conds -join "; ") + ") Acts=(" + ($acts -join "; ") + ")")
    }
}
