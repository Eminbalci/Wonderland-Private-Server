$repoRoot = 'D:\GitHub\Wonderland-Private-Server'
Add-Type -Path "$repoRoot\PhoenixData\bin\Debug\net462\PhoenixData.dll"
$talk = New-Object DataFiles.PhxTalkDat("$repoRoot\Data\Talk.DAT")
for ($id = 20038; $id -le 20055; $id++) {
    $text = $talk.GetDialogue([uint32]$id)
    Write-Host "TalkID $id : $text"
}
