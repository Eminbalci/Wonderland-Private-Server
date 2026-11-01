$repo = (Get-Location).Path
$refs = @(
    (Join-Path $repo 'RCLibrary\bin\Debug\net462\RCLibrary.dll'),
    (Join-Path $repo 'PhoenixData\bin\Debug\net462\PhoenixData.dll'),
    (Join-Path $repo 'wlo.pserver.core\bin\Debug\wlo.pserver.core.dll')
)
foreach ($a in $refs) { [void][Reflection.Assembly]::LoadFrom((Resolve-Path $a)) }
$mgr = New-Object Game.DataFiles.EveManager
$mgr.LoadFile((Join-Path $repo 'Data\eve.Emg'))

$mismatchCount = 0
$totalActionSubs = 0
$multiCondRules = 0

foreach ($m in $mgr.AllMaps.Values) {
    if ($m.PreEvents -eq $null) { continue }
    foreach ($pe in $m.PreEvents) {
        if ($pe.subentry1 -eq $null) { continue }
        for ($i = 0; $i -lt $pe.subentry1.Count; $i++) {
            $sub = $pe.subentry1[$i]
            $hasActs = ($sub.subentry2 -ne $null -and $sub.subentry2.Count -gt 0)
            if ($hasActs) {
                $totalActionSubs++
                $b = $sub.unknown.ToArray()
                $w6 = [BitConverter]::ToUInt16($b, 11)
                $condCount = $w6 -shr 8
                if ($condCount -gt 1) {
                    $multiCondRules++
                    # Check how many subentries follow until next action
                    $actualFollowing = 0
                    for ($j = $i + 1; $j -lt $pe.subentry1.Count; $j++) {
                        if ($pe.subentry1[$j].subentry2 -ne $null -and $pe.subentry1[$j].subentry2.Count -gt 0) {
                            break
                        }
                        $actualFollowing++
                    }
                    if ($condCount -ne ($actualFollowing + 1)) {
                        $mismatchCount++
                        Write-Host "Map $($m.mapID) PE $($pe.clickID) Sub $i : condCount=$condCount but actualFollowing=$actualFollowing"
                    }
                }
            }
        }
    }
}

Write-Host "Total action subs: $totalActionSubs, MultiCond rules: $multiCondRules, Mismatches: $mismatchCount"
