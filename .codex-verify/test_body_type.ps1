$repo = (Get-Location).Path
Add-Type -Path (Join-Path $repo 'RCLibrary\bin\Debug\net462\RCLibrary.dll')
Add-Type -Path (Join-Path $repo 'PhoenixData\bin\Debug\net462\PhoenixData.dll')
Add-Type -Path (Join-Path $repo 'wlo.pserver.core\bin\Debug\wlo.pserver.core.dll')
$prop = [Game.Player].GetProperty('Body')
Write-Host "Player.Body type: $($prop.PropertyType.FullName)"
$enumVals = [Enum]::GetNames($prop.PropertyType)
foreach ($e in $enumVals) {
    Write-Host "  $e = $([int][Enum]::Parse($prop.PropertyType, $e))"
}
