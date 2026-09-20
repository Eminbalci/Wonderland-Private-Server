param([string]$BuildDirectory)
$ErrorActionPreference='Stop'
if(!$BuildDirectory){$BuildDirectory=Join-Path $PSScriptRoot 'branch1-bin'}
$runtimeTests=@('Test-Branch1.ps1','Test-PetNativeProtocol.ps1','Test-PetProgression.ps1','Test-InventoryLogin.ps1','Test-InventorySocket.ps1','Test-InventoryProtocol.ps1','Test-StorageTransfers.ps1','Test-MallCategory.ps1','Test-ClientGacha.ps1','Test-MallForging.ps1','Test-StatAllocation.ps1','Test-HudVitals.ps1','Test-CombatVitals.ps1','Test-CriticalHits.ps1','Test-NoGmMessages.ps1','Test-CombatSpeed.ps1','Test-DefenseCapture.ps1')
$sourceTests=@('Test-PetBattleRoster.ps1','Test-CombatPacing.ps1','Test-CaptureCompletion.ps1','Test-CombatActionSelection.ps1')
$results=@()
foreach($test in $runtimeTests+$sourceTests){
 $log=Join-Path $PSScriptRoot ('branch1-'+$test+'.log')
 if($test -in $runtimeTests){
  & powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot 'Run-Branch1Check.ps1') -TestName $test -BuildDirectory $BuildDirectory *> $log
 }else{
  & pwsh -NoProfile -File (Join-Path $PSScriptRoot $test) *> $log
 }
 $passed=$LASTEXITCODE -eq 0
 $results += [pscustomobject]@{test=$test;passed=$passed;log=$log}
 Write-Output ($test+': '+$passed)
 if(!$passed){Get-Content $log -Tail 10}
}
$results|ConvertTo-Json|Set-Content (Join-Path $PSScriptRoot 'branch1-final-results.json')
if($results.passed -contains $false){throw 'Branch1 regression suite failed'}
Write-Output ('PASS: '+$results.Count+' suites')
