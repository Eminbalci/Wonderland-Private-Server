param([string]$TestScript='Test-NpcVisibility.ps1',[string]$BuildDirectory=(Join-Path $PSScriptRoot 'npc-visibility-bin'))
$ErrorActionPreference='Stop'
[void][Reflection.Assembly]::LoadFrom((Join-Path $BuildDirectory 'RCLibrary.dll'))
[RCLibrary.Core.DataBase]::LoadGlobalConfig()
[RCLibrary.Core.DataBase]::DefaultServType=[RCLibrary.Core.DataBaseTypes]::Sqlite
[RCLibrary.Core.DataBase]::DefaultDBFile=Join-Path $PSScriptRoot ('npc-visibility-isolated-'+[Guid]::NewGuid().ToString('N')+'.db')
& (Join-Path $PSScriptRoot $TestScript) -BuildDirectory $BuildDirectory
