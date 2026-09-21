$ErrorActionPreference='Stop'
$build=Join-Path (Split-Path $PSScriptRoot -Parent) '.codex-verify/inventory-bin'
foreach($a in @('RCLibrary.dll','PhoenixData.dll','wlo.pserver.core.dll')) {[void][Reflection.Assembly]::LoadFrom((Join-Path $build $a))}
$dat=[DataFiles.PhxItemDat]::new()
if (!$dat.Load((Join-Path (Split-Path $PSScriptRoot -Parent) 'Data/itemDat.wpdat')).GetAwaiter().GetResult()) {throw 'Item data load failed'}
foreach($id in @(21013,24013,28014,34026,45001,32075,43002)) {
 $i=$dat.GetItemByID($id)
 if($null -ne $i) {"$id $([Text.Encoding]::ASCII.GetString($i.ItemName)) type=$($i.ItemType) equip=$($i.Equippos) stats=$($i.StatusType -join ',') values=$($i.StatusUp -join ',')"}
}
