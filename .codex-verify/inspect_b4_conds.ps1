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

# Branch 4 (Sub 3):
$s3 = $ev8.SubEntry[3]
Write-Host ("Branch 4 (Sub 3): condition count = " + ($s3.unknownword6 -shr 8))
for ($k = 0; $k -lt ($s3.unknownword6 -shr 8); $k++) {
    $c = $ev8.SubEntry[3 + $k]
    $cmp = $c.unknownword4 -band 255
    $val = ($c.unknownword4 -shr 8) -bor ($c.unknownword5 -shl 8)
    Write-Host ("  Cond " + $k + ": q=" + $c.unknownword1 + " w2=" + $c.unknownword2 + " cmp=" + $cmp + " val=" + $val)
}
