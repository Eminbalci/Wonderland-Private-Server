$ErrorActionPreference = "Stop"
Add-Type -Path "PhoenixData/bin/Debug/net462/PhoenixData.dll"
$talkDat = New-Object DataFiles.PhxTalkDat("Data/Talk.dat")
[DataFiles.TalkResolver]::Initialize($talkDat)

for ($id = 1; $id -le 35000; $id++) {
    $res = [DataFiles.TalkResolver]::ResolveDetailed([uint32]$id)
    if ($res.Success -and ($res.RawText -match "picture|photo|photograph|portrait")) {
        Write-Host ("TalkID {0}: {1}" -f $id, $res.RawText)
    }
}
