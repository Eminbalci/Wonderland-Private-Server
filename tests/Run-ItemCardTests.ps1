param([string]$BuildDirectory)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
if (!$BuildDirectory) { $BuildDirectory = Join-Path $root 'bin/Debug' }
$BuildDirectory = [IO.Path]::GetFullPath($BuildDirectory)
$compiler = Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
if (!(Test-Path -LiteralPath $compiler)) { throw 'Install the .NET Framework 4.x runtime/developer tools.' }
$target = Join-Path $BuildDirectory 'ItemCardTests.exe'
& $compiler /nologo /target:exe "/out:$target" "/reference:$(Join-Path $BuildDirectory 'wlo.pserver.core.dll')" "/reference:$(Join-Path $BuildDirectory 'RCLibrary.dll')" "/reference:$(Join-Path $BuildDirectory 'PhoenixData.dll')" "/reference:System.Web.Extensions.dll" (Join-Path $PSScriptRoot 'ItemCardTests.cs')
if ($LASTEXITCODE -ne 0) { throw 'ItemCard test compilation failed.' }
& $target
if ($LASTEXITCODE -ne 0) { throw 'ItemCard tests failed.' }
