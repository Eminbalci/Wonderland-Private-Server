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

$md = $mgr.GetMapData(12601)

# Inspect all PreEvents in 12601
for ($p = 0; $p -lt $md.PreEvents.Count; $p++) {
    $pe = $md.PreEvents[$p]
    Write-Host "PE $p (ClickID $($pe.clickID)):"
    for ($s = 0; $s -lt $pe.subentry1.Count; $s++) {
        $sub = $pe.subentry1[$s]
        $arr = $sub.unknown.ToArray()
        $q = [BitConverter]::ToUInt16($arr, 1)
        $w2 = [BitConverter]::ToUInt16($arr, 3)
        $w4 = [BitConverter]::ToUInt16($arr, 7)
        $w6 = [BitConverter]::ToUInt16($arr, 11)
        $acts = if ($sub.subentry2) { $sub.subentry2.Count } else { 0 }
        Write-Host "  Sub[$s]: b1=$($arr[0]) q=$q w2=$w2 w4=0x$($w4.ToString('X4')) w6=0x$($w6.ToString('X4')) acts=$acts"
        if ($acts -gt 0) {
            foreach ($act in $sub.subentry2) {
                $aArr = $act.unknown.ToArray()
                $tClick = [BitConverter]::ToUInt16($aArr, 1)
                $tType = [BitConverter]::ToUInt16($aArr, 3)
                Write-Host "    Action: type=$tType targetClick=$tClick"
            }
        }
    }
}
