$ErrorActionPreference = "Stop"
Add-Type -Path "PhoenixData/bin/Debug/net462/PhoenixData.dll"
$talkDat = New-Object DataFiles.PhxTalkDat("Data/Talk.dat")
[DataFiles.TalkResolver]::Initialize($talkDat)

for ($id = 20035; $id -le 20060; $id++) {
    $res = [DataFiles.TalkResolver]::ResolveDetailed([uint32]$id)
    if ($res.Success) {
        Write-Host ("TalkID {0}: {1}" -f $id, $res.RawText)
    }
}
