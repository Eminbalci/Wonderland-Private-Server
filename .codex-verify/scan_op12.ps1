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
$maps = $mgr.AllMaps

Write-Host "Scanning Opcode 12..."
foreach ($mapEntry in $maps.Values) {
    if ($mapEntry.Events -ne $null) {
        foreach ($ev in $mapEntry.Events) {
            if ($ev.SubEntry -ne $null) {
                foreach ($sub in $ev.SubEntry) {
                    if ($sub.SubEntry -ne $null) {
                        foreach ($op in $sub.SubEntry) {
                            if ($op.DialogPtr -eq 12) {
                                Write-Host ("Map {0} ClickID {1} Branch {2}: d1={3} d2={4} d3={5} d4={6} dw1={7} dw2={8} dw3={9}" -f `
                                    $mapEntry.mapID, $ev.clickID, $sub.subIndex, $op.dialog1, $op.dialog2, $op.dialog3, $op.dialog4, `
                                    $op.unknowndword1, $op.unknowndword2, $op.unknowndword3)
                            }
                        }
                    }
                }
            }
        }
    }
}
