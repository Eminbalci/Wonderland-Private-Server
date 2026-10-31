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
$talk = New-Object DataFiles.PhxTalkDat("$repoRoot\Data\Talk.DAT")

$samples = @()
foreach ($k in $mgr.AllMaps.Keys) {
    $md = $mgr.AllMaps[$k]
    if ($md.Events -eq $null) { continue }
    foreach ($ev in $md.Events) {
        if ($ev.SubEntry -eq $null) { continue }
        for ($i = 0; $i -lt $ev.SubEntry.Count; $i++) {
            $s = $ev.SubEntry[$i]
            if ($s.unknownbyte1 -eq 5 -and $s.unknownword2 -eq 2 -and ($s.unknownword4 -band 255) -eq 0 -and $s.SubEntry -ne $null -and $s.SubEntry.Count -gt 0) {
                $q = $s.unknownword1
                $t = ""
                foreach ($op in $s.SubEntry) {
                    if ($op.dialog3 -gt 10000) { $t = $talk.GetDialogue([uint32]$op.dialog3); break }
                    elseif ($op.dialog2 -gt 10000) { $t = $talk.GetDialogue([uint32]$op.dialog2); break }
                }
                $samples += ("Map " + $k + " Ev " + $ev.clickID + " Sub " + $i + ": Quest " + $q + " :: " + $t)
                if ($samples.Count -ge 15) { break }
            }
        }
        if ($samples.Count -ge 15) { break }
    }
    if ($samples.Count -ge 15) { break }
}
foreach ($s in $samples) { Write-Host $s }
