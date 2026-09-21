$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$runtime=Join-Path $root 'bin/Debug'
foreach($name in @('RCLibrary.dll','PhoenixData.dll','wlo.pserver.core.dll')){[void][Reflection.Assembly]::LoadFrom((Join-Path $runtime $name))}
Set-Location -LiteralPath $runtime
foreach($name in @('itemDat.wpdat','gacha_packs.json','mall_forging.json','critical_hits.json')){
 $path=[RCLibrary.Core.PathHelper]::GetDataFilePath($name)
 if([IO.Path]::GetFullPath($path) -ne [IO.Path]::GetFullPath((Join-Path (Join-Path $root 'Data') $name))){throw "Wrong active data path: $name -> $path"}
}
[Game.Battle.CriticalHitManager]::Load([RCLibrary.Core.PathHelper]::GetDataFilePath('critical_hits.json'))
if([Game.Battle.CriticalHitManager]::GetItemChance(11101) -ne 24){throw 'Runtime Crit load failed'}
$method=[Game.Battle.PvEBattleManager].GetMethod('GetAnimationDelayMs',[Reflection.BindingFlags]'NonPublic,Static')
if($method.Invoke($null,@([uint16]11005,'attack')) -ne 4500){throw 'Runtime spell delay not deployed'}
if($method.Invoke($null,@([uint16]10001,'attack')) -ne 2300){throw 'Runtime basic attack delay changed'}
if($method.Invoke($null,@([uint16]10008,'catch')) -ne 6000){throw 'Runtime capture completion delay changed'}
'PASS: deployed assemblies load; active config paths resolve correctly; Crit24%, spell4500ms, basic2300ms, capture6000ms.'
