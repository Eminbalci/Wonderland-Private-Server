$build=Join-Path (Split-Path $PSScriptRoot -Parent) '.codex-verify/inventory-bin'
[void][Reflection.Assembly]::LoadFrom((Join-Path $build 'PhoenixData.dll'))
$dat=[DataFiles.PhxItemDat]::new(); [void]$dat.Load((Join-Path (Split-Path $PSScriptRoot -Parent) 'Data/itemDat.wpdat')).GetAwaiter().GetResult()
$list=[DataFiles.PhxItemDat].GetField('m_List',[Reflection.BindingFlags]'Instance,NonPublic').GetValue($dat)
$list | Where-Object { $_.ItemType -eq 23 } | Select-Object -First 18 | ForEach-Object { "$($_.ItemID) $([Text.Encoding]::ASCII.GetString($_.ItemName).Trim([char]0)) stats=$($_.StatusType -join ',') values=$($_.StatusUp -join ',')" }
