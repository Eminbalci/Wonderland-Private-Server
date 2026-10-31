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

for ($m = 0; $m -lt 1119; $m++) {
    $md = $mgr.GetMapData($m)
    if ($md -eq $null -or $md.Events -eq $null) { continue }
    foreach ($ev in $md.Events) {
        if ($m -eq 12601 -and $ev.clickID -eq 8) { continue }
        if ($ev.SubEntry -eq $null) { continue }
        foreach ($sub in $ev.SubEntry) {
            # check branch conditions
            if ($sub.unknownword1 -eq 15266 -or $sub.unknownword1 -eq 15267 -or $sub.unknownword3 -eq 15266 -or $sub.unknownword3 -eq 15267) {
                Write-Host "Map $m Ev $($ev.clickID) Sub $($sub.subIndex): Condition mentions 15266/7! b1=$($sub.unknownbyte1) w1=$($sub.unknownword1) w2=$($sub.unknownword2) w3=$($sub.unknownword3)"
            }
            if ($sub.SubEntry -eq $null) { continue }
            foreach ($op in $sub.SubEntry) {
                if ($op.DialogPtr -eq 5 -and ($op.dialog1 -eq 15266 -or $op.dialog1 -eq 15267)) {
                    Write-Host "Map $m Ev $($ev.clickID) Sub $($sub.subIndex): Op 5: q=$($op.dialog1) d2=$($op.dialog2)"
                }
            }
        }
    }
}
