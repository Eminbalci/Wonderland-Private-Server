param([Parameter(Mandatory=$true)][string]$TestName,[Parameter(Mandatory=$true)][string]$BuildDirectory)
$ErrorActionPreference='Stop'
[void][Reflection.Assembly]::LoadFrom((Join-Path $BuildDirectory 'RCLibrary.dll'))
[RCLibrary.Core.DataBase]::LoadGlobalConfig()
[RCLibrary.Core.DataBase]::DefaultServType=[RCLibrary.Core.DataBaseTypes]::Sqlite
$fixtureDir=Join-Path $PSScriptRoot 'upstream-merge-fixtures'
[void][IO.Directory]::CreateDirectory($fixtureDir)
[RCLibrary.Core.DataBase]::DefaultDBFile=Join-Path $fixtureDir ($TestName+'-'+[guid]::NewGuid().ToString('N')+'.db')
& (Join-Path $PSScriptRoot $TestName) -BuildDirectory $BuildDirectory
if (!$?) { throw "Failed: $TestName" }
