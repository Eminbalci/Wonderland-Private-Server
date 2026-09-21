param([Parameter(Mandatory=$true)][string]$TestName,
      [string]$BuildDirectory)
$ErrorActionPreference='Stop'
if (!$BuildDirectory) { $BuildDirectory=Join-Path $PSScriptRoot 'branch1-bin' }
# Isolate default/static database operations before loading game assemblies.
[void][Reflection.Assembly]::LoadFrom((Join-Path $BuildDirectory 'RCLibrary.dll'))
[RCLibrary.Core.DataBase]::LoadGlobalConfig()
[RCLibrary.Core.DataBase]::DefaultServType=[RCLibrary.Core.DataBaseTypes]::Sqlite
$fixtureDir=Join-Path $PSScriptRoot 'branch1-fixtures'
[void][IO.Directory]::CreateDirectory($fixtureDir)
[RCLibrary.Core.DataBase]::DefaultDBFile=Join-Path $fixtureDir ($TestName + '.db')
& (Join-Path $PSScriptRoot $TestName) -BuildDirectory $BuildDirectory
if (!$?) { throw "Failed: $TestName" }
