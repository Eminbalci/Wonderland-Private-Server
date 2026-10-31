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
        if ($ev.SubEntry -eq $null) { continue }
        foreach ($sub in $ev.SubEntry) {
            if ($sub.unknownword1 -eq 15266 -or $sub.unknownword1 -eq 15267 -or $sub.unknownword3 -eq 15266 -or $sub.unknownword3 -eq 15267) {
                Write-Host "Map $m Event $($ev.clickID) Sub $($sub.subIndex) mentions Quest 15266/7"
            }
            if ($sub.SubEntry -eq $null) { continue }
            foreach ($op in $sub.SubEntry) {
                if ($op.DialogPtr -eq 5 -and ($op.dialog1 -eq 15266 -or $op.dialog1 -eq 15267)) {
                    Write-Host "Map $m Event $($ev.clickID) Sub $($sub.subIndex): Op 5 q=$($op.dialog1) d2=$($op.dialog2) d3=$($op.dialog3) d4=$($op.dialog4)"
                }
            }
        }
    }
}
