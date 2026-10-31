$repo = (Get-Location).Path
$refs = @(
    (Join-Path $repo 'RCLibrary\bin\Debug\net462\RCLibrary.dll'),
    (Join-Path $repo 'PhoenixData\bin\Debug\net462\PhoenixData.dll'),
    (Join-Path $repo 'wlo.pserver.core\bin\Debug\wlo.pserver.core.dll')
)
foreach ($a in $refs) { [void][Reflection.Assembly]::LoadFrom((Resolve-Path $a)) }
$mgr = New-Object Game.DataFiles.EveManager
$mgr.LoadFile((Join-Path $repo 'Data\eve.Emg'))
$m = $mgr.GetMapData(12601)
if ($m -ne $null) {
    Write-Host ("Map 12601 found! PreEvents count: " + $m.PreEvents.Count)
    foreach ($pe in $m.PreEvents) {
        Write-Host ("PreEvent ClickID=" + $pe.ClickID + " SubCount=" + $pe.SubEntries.Count)
        for ($i = 0; $i -lt $pe.SubEntries.Count; $i++) {
            $sub = $pe.SubEntries[$i]
            Write-Host ("  Sub[" + $i + "]: CondCount=" + $sub.Conditions.Count + " ActCount=" + $sub.Actions.Count)
            foreach ($c in $sub.Conditions) {
                Write-Host ("    Cond: b1=" + $c.unknownbyte1 + " w1=" + $c.unknownword1 + " w2=" + $c.unknownword2 + " cmp=" + $c.comparison + " val=" + $c.value)
            }
            foreach ($act in $sub.Actions) {
                Write-Host ("    Act: " + ($act.unknown -join ' '))
            }
        }
    }
} else {
    Write-Host "Map 12601 not found"
}
