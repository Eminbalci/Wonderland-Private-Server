$repoRoot = 'D:\GitHub\Wonderland-Private-Server'
Add-Type -Path "$repoRoot\PhoenixData\bin\Debug\net462\PhoenixData.dll"
$refs = @(
    (Join-Path $repoRoot 'RCLibrary\bin\Debug\net462\RCLibrary.dll'),
    (Join-Path $repoRoot 'PhoenixData\bin\Debug\net462\PhoenixData.dll'),
    (Join-Path $repoRoot 'wlo.pserver.core\bin\Debug\wlo.pserver.core.dll')
)
foreach ($a in $refs) { [void][Reflection.Assembly]::LoadFrom((Resolve-Path $a)) }

$mgr = New-Object Game.DataFiles.EveManager
$mgr.LoadFile((Join-Path $repoRoot 'Data\eve.Emg'))

$samples = @()
foreach ($k in $mgr.AllMaps.Keys) {
    $md = $mgr.AllMaps[$k]
    if ($md.Events -eq $null) { continue }
    foreach ($ev in $md.Events) {
        if ($ev.SubEntry -eq $null) { continue }
        foreach ($s in $ev.SubEntry) {
            if ($s.unknownbyte1 -eq 2 -and $s.unknownword1 -eq 3) {
                $samples += "Map $k Ev $($ev.clickID): b1=2 w1=3 w2=$($s.unknownword2) w3=$($s.unknownword3) w4=0x$($s.unknownword4.ToString('X4'))"
                if ($samples.Count -ge 20) { break }
            }
        }
        if ($samples.Count -ge 20) { break }
    }
    if ($samples.Count -ge 20) { break }
}
foreach ($s in $samples) { Write-Host $s }
