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

$w2Counts = @{}
$w2w4Samples = @{}

foreach ($k in $mgr.AllMaps.Keys) {
    $md = $mgr.AllMaps[$k]
    if ($md.PreEvents -eq $null) { continue }
    foreach ($pe in $md.PreEvents) {
        if ($pe.subentry1 -eq $null) { continue }
        foreach ($sub in $pe.subentry1) {
            if ($sub.unknown -ne $null -and $sub.unknown.Count -ge 5 -and $sub.unknown[0] -eq 5) {
                $arr = $sub.unknown.ToArray()
                $qId = [BitConverter]::ToUInt16($arr, 1)
                $w2 = [BitConverter]::ToUInt16($arr, 3)
                $w4 = [BitConverter]::ToUInt16($arr, 7)
                $w6 = [BitConverter]::ToUInt16($arr, 11)
                if (-not $w2Counts.ContainsKey($w2)) { $w2Counts[$w2] = 0 }
                $w2Counts[$w2]++
                
                $key = "w2=$w2, w4=0x$($w4.ToString('X4'))"
                if (-not $w2w4Samples.ContainsKey($key)) {
                    $w2w4Samples[$key] = "Map $k PE $($pe.clickID): Quest $qId w2=$w2 w4=0x$($w4.ToString('X4')) w6=0x$($w6.ToString('X4')) acts=$(if($sub.subentry2){$sub.subentry2.Count}else{0})"
                }
            }
        }
    }
}

Write-Host "=== w2 value counts in PreEvents case 5 ==="
foreach ($key in ($w2Counts.Keys | Sort-Object)) {
    Write-Host "w2 = $key : $($w2Counts[$key])"
}

Write-Host "`n=== Distinct w2 + w4 patterns (samples) ==="
foreach ($key in ($w2w4Samples.Keys | Sort-Object)) {
    Write-Host $w2w4Samples[$key]
}
