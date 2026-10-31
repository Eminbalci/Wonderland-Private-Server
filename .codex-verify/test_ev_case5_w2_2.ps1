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

$samples = @()
foreach ($k in $mgr.AllMaps.Keys) {
    $md = $mgr.AllMaps[$k]
    if ($md.Events -eq $null) { continue }
    foreach ($ev in $md.Events) {
        if ($ev.SubEntry -eq $null) { continue }
        foreach ($sub in $ev.SubEntry) {
            if ($sub.unknownbyte1 -eq 5 -and $sub.unknownword2 -eq 2) {
                $qId = $sub.unknownword1
                $w4 = $sub.unknownword4
                $hasActs = if ($sub.SubEntry -ne $null -and $sub.SubEntry.Count -gt 0) { $true } else { $false }
                $samples += "Map $k Ev $($ev.clickID) Sub $($sub.subIndex): Quest $qId w2=2 w4=0x$($w4.ToString('X4')) hasActs=$hasActs"
                if ($samples.Count -ge 20) { break }
            }
        }
        if ($samples.Count -ge 20) { break }
    }
    if ($samples.Count -ge 20) { break }
}
foreach ($s in $samples) { Write-Host $s }
