$ErrorActionPreference='Stop'
$build=Join-Path $PSScriptRoot 'inventory-dispatch-bin'
[void][Reflection.Assembly]::LoadFrom((Join-Path $build 'PhoenixData.dll'))
$dat=[DataFiles.PhxItemDat]::new();[void]$dat.Load((Join-Path (Split-Path $PSScriptRoot -Parent) 'Data/itemDat.wpdat')).GetAwaiter().GetResult()
$dat.GetItemList() | ForEach-Object {$name=[Text.Encoding]::ASCII.GetString($_.ItemName).Trim([char]0);if($name -match 'Apple|Pineapple|Fresh Fruit|Coconut|Soybean'){[pscustomobject]@{ID=$_.ItemID;Name=$name;Type=$_.ItemType;HP_SP=($_.StatusType -join ',');Values=($_.StatusUp -join ',')}}} | Format-Table -AutoSize
