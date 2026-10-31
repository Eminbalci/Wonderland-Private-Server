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

Write-Host "PreEvents count: $($md.PreEvents.Count)"
for ($i = 0; $i -lt $md.PreEvents.Count; $i++) {
    $pe = $md.PreEvents[$i]
    Write-Host ("PE[{0}] clickID={1} subentry1={2}" -f $i, $pe.clickID, $pe.subentry1.Count)
    foreach ($sub in $pe.subentry1) {
        $cHex = [BitConverter]::ToString($sub.unknown.ToArray())
        $acts = if ($sub.subentry2 -ne $null) { $sub.subentry2.Count } else { 0 }
        Write-Host ("   Sub: Acts={0} CondBytes={1}" -f $acts, $cHex)
        if ($sub.subentry2 -ne $null) {
            foreach ($act in $sub.subentry2) {
                Write-Host ("      Act: {0}" -f [BitConverter]::ToString($act.unknown.ToArray()))
            }
        }
    }
}
