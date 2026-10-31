$repoRoot = 'D:\GitHub\Wonderland-Private-Server'
Add-Type -Path "$repoRoot\PhoenixData\bin\Debug\net462\PhoenixData.dll"
$refs = @(
    (Join-Path $repoRoot 'RCLibrary\bin\Debug\net462\RCLibrary.dll'),
    (Join-Path $repoRoot 'PhoenixData\bin\Debug\net462\PhoenixData.dll'),
    (Join-Path $repoRoot 'wlo.pserver.core\bin\Debug\wlo.pserver.core.dll')
)
foreach ($a in $refs) { [void][Reflection.Assembly]::LoadFrom((Resolve-Path $a)) }

$talk = New-Object DataFiles.PhxTalkDat("$repoRoot\Data\Talk.DAT")

$talkIds = @(30365, 60034, 30591, 32759, 30448, 32761, 32762, 30164, 30165, 30166, 34065, 30169, 30168)
foreach ($t in $talkIds) {
    $txt = $talk.GetDialogue([uint32]$t)
    Write-Host "TalkID $t : $txt"
}
