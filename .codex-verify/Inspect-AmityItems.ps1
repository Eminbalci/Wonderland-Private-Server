$build=Join-Path (Split-Path $PSScriptRoot -Parent) '.codex-verify/inventory-bin'
[void][Reflection.Assembly]::LoadFrom((Join-Path $build 'PhoenixData.dll'))
$dat=[DataFiles.PhxItemDat]::new(); [void]$dat.Load((Join-Path (Split-Path $PSScriptRoot -Parent) 'Data/itemDat.wpdat')).GetAwaiter().GetResult()
$list=[DataFiles.PhxItemDat].GetField('m_List',[Reflection.BindingFlags]'Instance,NonPublic').GetValue($dat)
$list | Where-Object { ([Text.Encoding]::ASCII.GetString($_.ItemName) -match 'Rice Ball|Syrup|Jelly|Amity|Loyal|Beef|Roe|Pineapple|Coconut') -or ($_.StatusType -contains 33) -or ($_.StatusType -contains 34) } | ForEach-Object { "$($_.ItemID) $([Text.Encoding]::ASCII.GetString($_.ItemName).Trim([char]0)) type=$($_.ItemType) stats=$($_.StatusType -join ',') values=$($_.StatusUp -join ',')" }
