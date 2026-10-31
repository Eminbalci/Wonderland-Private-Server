$repoRoot = 'D:\GitHub\Wonderland-Private-Server'
Add-Type -Path "$repoRoot\PhoenixData\bin\Debug\net462\PhoenixData.dll"
$talk = New-Object DataFiles.PhxTalkDat("$repoRoot\Data\Talk.DAT")

$talkIds = @(27948, 27949, 27950, 27951, 27952, 27953, 27954, 27955, 11087, 27972, 27973, 27974, 27975, 27976, 27978, 27977)
foreach ($t in $talkIds) {
    $text = $talk.GetDialogue([uint32]$t)
    Write-Host "TalkID $t : $text"
}
