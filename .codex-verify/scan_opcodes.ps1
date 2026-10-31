$ErrorActionPreference = 'Stop'
$repo = (Get-Location).Path
$refs = @(
    (Join-Path $repo 'RCLibrary\bin\Debug\net462\RCLibrary.dll'),
    (Join-Path $repo 'PhoenixData\bin\Debug\net462\PhoenixData.dll'),
    (Join-Path $repo 'wlo.pserver.core\bin\Debug\wlo.pserver.core.dll')
)
foreach ($a in $refs) { [void][Reflection.Assembly]::LoadFrom((Resolve-Path $a)) }

$mgr = New-Object Game.DataFiles.EveManager
$res = $mgr.LoadFile((Join-Path $repo 'Data\eve.Emg'))
Write-Host "Loaded Eve.Emg: $res"

$maps = $mgr.AllMaps
Write-Host "Total maps: $($maps.Count)"

$opcodes = @{}
foreach ($mapEntry in $maps.Values) {
    if ($mapEntry.Events -ne $null) {
        foreach ($ev in $mapEntry.Events) {
            if ($ev.SubEntry -ne $null) {
                foreach ($sub in $ev.SubEntry) {
                    if ($sub.SubEntry -ne $null) {
                        foreach ($op in $sub.SubEntry) {
                            $ptr = $op.DialogPtr
                            if (-not $opcodes.ContainsKey($ptr)) { $opcodes[$ptr] = 0 }
                            $opcodes[$ptr]++
                        }
                    }
                }
            }
        }
    }
}

foreach ($k in ($opcodes.Keys | Sort-Object)) {
    Write-Host ("Opcode {0}: {1} occurrences" -f $k, $opcodes[$k])
}
