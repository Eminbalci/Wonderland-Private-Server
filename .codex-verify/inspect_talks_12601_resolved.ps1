$ErrorActionPreference = 'Stop'
$repo = (Get-Location).Path
$refs = @(
    (Join-Path $repo 'RCLibrary\bin\Debug\net462\RCLibrary.dll'),
    (Join-Path $repo 'PhoenixData\bin\Debug\net462\PhoenixData.dll'),
    (Join-Path $repo 'wlo.pserver.core\bin\Debug\wlo.pserver.core.dll')
)
foreach ($a in $refs) { [void][Reflection.Assembly]::LoadFrom((Resolve-Path $a)) }

[DataFiles.TalkResolver]::Initialize((Join-Path $repo 'Data'))

$talkIds = @(27948, 27949, 27950, 27951, 27952, 27953, 27954, 27955, 11087, 27972, 27973, 27974, 27975, 27976, 27978, 27977)
foreach ($t in $talkIds) {
    $outTxt = ""
    if ([DataFiles.TalkResolver]::TryResolve([uint32]$t, [ref]$outTxt, 0, $true)) {
        Write-Host "Talk $t : $outTxt"
    } else {
        Write-Host "Talk $t : <not resolved>"
    }
}
