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
[Game.DataFiles.SceneDataManager]::Initialize((Join-Path $repo 'Data'))

Write-Host "Checking Bangkok events for NPC hide/show, transformations, or battle conversions..."
for ($m = 12600; $m -le 12650; $m++) {
    $md = $mgr.GetMapData($m)
    if ($md -eq $null -or $md.Events -eq $null) { continue }
    foreach ($ev in $md.Events) {
        if ($ev.SubEntry -eq $null) { continue }
        foreach ($sub in $ev.SubEntry) {
            if ($sub.SubEntry -eq $null) { continue }
            $hasHideShow = $false
            $hasOp12 = $false
            $hasBattle = $false
            foreach ($op in $sub.SubEntry) {
                if ($op.DialogPtr -eq 12) { $hasOp12 = $true }
                if ($op.DialogPtr -eq 2 -and ($op.dialog2 -eq 2 -or $op.dialog2 -eq 3)) { $hasHideShow = $true }
                if ($op.DialogPtr -eq 4) { $hasBattle = $true }
            }
            if ($hasOp12 -or ($hasHideShow -and $hasBattle) -or ($hasHideShow -and $sub.unknownword1 -gt 0)) {
                Write-Host ("Map {0} ClickID {1} Branch {2}: Q={3} HideShow={4} Op12={5} Battle={6}" -f `
                    $m, $ev.clickID, $sub.subIndex, $sub.unknownword1, $hasHideShow, $hasOp12, $hasBattle)
                foreach ($op in $sub.SubEntry) {
                    Write-Host ("   Opcode {0}: d1={1} d2={2} d3={3} d4={4} dw1={5}" -f $op.DialogPtr, $op.dialog1, $op.dialog2, $op.dialog3, $op.dialog4, $op.unknowndword1)
                }
            }
        }
    }
}
