$repoRoot = 'D:\GitHub\Wonderland-Private-Server'
Add-Type -Path "$repoRoot\PhoenixData\bin\Debug\net462\PhoenixData.dll"
$refs = @(
    (Join-Path $repoRoot 'RCLibrary\bin\Debug\net462\RCLibrary.dll'),
    (Join-Path $repoRoot 'PhoenixData\bin\Debug\net462\PhoenixData.dll'),
    (Join-Path $repoRoot 'wlo.pserver.core\bin\Debug\wlo.pserver.core.dll')
)
foreach ($a in $refs) { [void][Reflection.Assembly]::LoadFrom((Resolve-Path $a)) }

$talk = New-Object DataFiles.PhxTalkDat("$repoRoot\Data\Talk.DAT")

# Let's search talk for merchant and antidote in Bangkok
$mgr = New-Object Game.DataFiles.EveManager
$mgr.LoadFile((Join-Path $repoRoot 'Data\eve.Emg'))

$bangkokMaps = @(12600, 12601, 12602, 12603, 12604, 12605, 12606, 12607, 12608, 12609, 12610, 12611, 12612)
foreach ($bm in $bangkokMaps) {
    $md = $mgr.GetMapData($bm)
    if ($md -eq $null -or $md.Events -eq $null) { continue }
    foreach ($ev in $md.Events) {
        if ($ev.SubEntry -eq $null) { continue }
        foreach ($sub in $ev.SubEntry) {
            if ($sub.SubEntry -eq $null) { continue }
            foreach ($op in $sub.SubEntry) {
                $tid = 0
                if ($op.dialog3 -gt 10000 -and $op.dialog3 -lt 65000) { $tid = $op.dialog3 }
                elseif ($op.dialog2 -gt 10000 -and $op.dialog2 -lt 65000) { $tid = $op.dialog2 }
                if ($tid -gt 0) {
                    $t = $talk.GetDialogue([uint32]$tid)
                    if ($t -match 'antidote' -or $t -match 'merchant' -or $t -match 'Sajit' -or $t -match 'flu' -or $t -match 'drug') {
                        Write-Host "Map $bm Ev $($ev.clickID) Sub $($sub.subIndex): Talk $tid : $t"
                    }
                }
            }
        }
    }
}
