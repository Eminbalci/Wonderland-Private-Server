$repoRoot = 'D:\GitHub\Wonderland-Private-Server'
Add-Type -Path "$repoRoot\PhoenixData\bin\Debug\net462\PhoenixData.dll"
$talk = New-Object DataFiles.PhxTalkDat("$repoRoot\Data\Talk.DAT")

for ($id = 30400; $id -le 30420; $id++) {
    $text = $talk.GetDialogue([uint32]$id)
    if ($text) {
        Write-Host "TalkID $id : $text"
    }
}
