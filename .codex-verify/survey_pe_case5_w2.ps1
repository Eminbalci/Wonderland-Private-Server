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

$counts = @{}
for ($m = 0; $m -lt 1119; $m++) {
    $md = $mgr.GetMapData($m)
    if ($md -eq $null -or $md.PreEvents -eq $null) { continue }
    foreach ($pe in $md.PreEvents) {
        if ($pe.subentry1 -eq $null) { continue }
        foreach ($sub in $pe.subentry1) {
            if ($sub.unknown -ne $null -and $sub.unknown.Count -ge 5 -and $sub.unknown[0] -eq 5) {
                $w2 = [BitConverter]::ToUInt16($sub.unknown.ToArray(), 3)
                $key = "w2=$w2"
                if (-not $counts.ContainsKey($key)) { $counts[$key] = 0 }
                $counts[$key]++
            }
        }
    }
}
foreach ($k in $counts.Keys) {
    Write-Host "$k : $($counts[$k])"
}
