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

$zeroActCount = 0
$totalSubCount = 0
$totalPeCount = 0
$samples = @()

foreach ($m in $mgr.AllMaps.Values) {
    if ($m.PreEvents -eq $null) { continue }
    foreach ($pe in $m.PreEvents) {
        $totalPeCount++
        if ($pe.subentry1 -eq $null) { continue }
        for ($i = 0; $i -lt $pe.subentry1.Count; $i++) {
            $totalSubCount++
            $sub = $pe.subentry1[$i]
            $hasActs = ($sub.subentry2 -ne $null -and $sub.subentry2.Count -gt 0)
            if (-not $hasActs) {
                $zeroActCount++
                if ($samples.Count -lt 15) {
                    $prevHasActs = ($i -gt 0 -and $pe.subentry1[$i-1].subentry2 -ne $null -and $pe.subentry1[$i-1].subentry2.Count -gt 0)
                    $nextHasActs = ($i + 1 -lt $pe.subentry1.Count -and $pe.subentry1[$i+1].subentry2 -ne $null -and $pe.subentry1[$i+1].subentry2.Count -gt 0)
                    $samples += [PSCustomObject]@{
                        MapID = $m.mapID
                        PE_ClickID = $pe.clickID
                        SubIdx = $i
                        TotalSubs = $pe.subentry1.Count
                        PrevHasActs = $prevHasActs
                        NextHasActs = $nextHasActs
                        Cond = [BitConverter]::ToString($sub.unknown.ToArray())
                    }
                }
            }
        }
    }
}

Write-Host "Total PreEvents: $totalPeCount, Total SubEntries: $totalSubCount, Subs with 0 actions: $zeroActCount"
$samples | Format-Table -AutoSize
