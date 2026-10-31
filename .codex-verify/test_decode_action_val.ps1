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
$ev8 = $md.Events | Where-Object { $_.clickID -eq 8 }

for ($i = 0; $i -lt $ev8.SubEntry.Count; $i++) {
    $s = $ev8.SubEntry[$i]
    if ($s.SubEntry -ne $null -and $s.SubEntry.Count -gt 0) {
        foreach ($op in $s.SubEntry) {
            $val = ($op.dialog4 -shr 8) -bor ($op.unknowndword1 -shl 8)
            Write-Host ("Branch at Sub " + $i + ": Op " + $op.DialogPtr + " d1=" + $op.dialog1 + " d2=" + $op.dialog2 + " d3=" + $op.dialog3 + " d4=" + $op.dialog4 + " dw1=" + $op.unknowndword1 + " => DecodeActionValue=" + $val)
        }
    }
}
