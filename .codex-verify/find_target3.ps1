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
foreach ($pe in $m.PreEvents) {
    for ($i = 0; $i -lt $pe.subentry1.Count; $i++) {
        $sub = $pe.subentry1[$i]
        if ($sub.subentry2 -ne $null) {
            foreach ($act in $sub.subentry2) {
                $b = $act.unknown.ToArray()
                if ($b.Length -ge 5 -and $b[0] -eq 2) {
                    $tc = [BitConverter]::ToUInt16($b, 1)
                    $at = [BitConverter]::ToUInt16($b, 3)
                    if ($tc -eq 3) {
                        Write-Host ("PE clickID=" + $pe.clickID + " sub=" + $i + " targetClick=" + $tc + " actType=" + $at)
                    }
                }
            }
        }
    }
}
