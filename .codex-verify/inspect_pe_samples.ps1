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

$samples = @(
    @{ Map = 10002; Click = 1 },
    @{ Map = 10003; Click = 4 },
    @{ Map = 10003; Click = 5 },
    @{ Map = 12601; Click = 3 }
)

foreach ($s in $samples) {
    $md = $mgr.GetMapData($s.Map)
    $pe = $md.PreEvents | Where-Object { $_.clickID -eq $s.Click }
    Write-Host ("`n================ Map {0} PreEvent ClickID {1} ================" -f $s.Map, $s.Click)
    for ($i = 0; $i -lt $pe.subentry1.Count; $i++) {
        $sub = $pe.subentry1[$i]
        $acts = if ($sub.subentry2 -ne $null) { $sub.subentry2.Count } else { 0 }
        $c = $sub.unknown
        $op = $c[0]
        $w1 = [BitConverter]::ToUInt16($c.ToArray(), 1)
        $w2 = [BitConverter]::ToUInt16($c.ToArray(), 3)
        $w3 = [BitConverter]::ToUInt16($c.ToArray(), 5)
        $w4 = [BitConverter]::ToUInt16($c.ToArray(), 7)
        $w5 = [BitConverter]::ToUInt16($c.ToArray(), 9)
        $w6 = [BitConverter]::ToUInt16($c.ToArray(), 11)
        Write-Host ("  Sub[{0}]: Acts={1} | op={2} w1={3} w2={4} w3={5} w4={6} w5={7} w6={8}" -f `
            $i, $acts, $op, $w1, $w2, $w3, $w4, $w5, $w6)
        if ($acts -gt 0) {
            foreach ($act in $sub.subentry2) {
                $a = $act.unknown
                $targetClick = [BitConverter]::ToUInt16($a.ToArray(), 1)
                $actType = [BitConverter]::ToUInt16($a.ToArray(), 3)
                Write-Host ("     Act -> targetClick={0} actType={1}" -f $targetClick, $actType)
            }
        }
    }
}
