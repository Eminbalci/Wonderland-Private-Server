$phoenixDll = "D:\GitHub\Wonderland-Private-Server\PhoenixData\bin\Debug\net462\PhoenixData.dll"
$rcLibDll = "D:\GitHub\Wonderland-Private-Server\RCLibrary\bin\Debug\net462\RCLibrary.dll"
$coreDll = "D:\GitHub\Wonderland-Private-Server\wlo.pserver.core\bin\Debug\wlo.pserver.core.dll"
$exePath = "D:\GitHub\Wonderland-Private-Server\bin\Debug\Wonderland Private Server.exe"

[System.Reflection.Assembly]::LoadFrom($rcLibDll) | Out-Null
[System.Reflection.Assembly]::LoadFrom($phoenixDll) | Out-Null
[System.Reflection.Assembly]::LoadFrom($coreDll) | Out-Null
[System.Reflection.Assembly]::LoadFrom($exePath) | Out-Null

Write-Host "=== TEST 1: TALK ID #18 RESOLUTION ==="
$dat = New-Object DataFiles.PhxTalkDat("Data\Talk.dat")
$res18 = [DataFiles.TalkResolver]::ResolveDetailed([uint32]18)
Write-Host "Talk ID #18 Success:" $res18.Success
Write-Host "Resolution Method:" $res18.ResolutionMethod
Write-Host "Formatted Text:" $res18.FormattedText
if ($res18.Success -and $res18.FormattedText -eq "Not opened this week.") {
    Write-Host "[PASS] Talk ID #18 resolved cleanly to 'Not opened this week.'"
} else {
    Write-Error "[FAIL] Talk ID #18 resolution failed!"
    exit 1
}

Write-Host "`n=== TEST 2: DUNGEON INSTANCE CREATION & CHAMBER ISOLATION ==="
$dungeonEngine = [Game.Experimental.ExperimentalFeaturesManager]::Instance.InstanceDungeons

# Create dummy leader player
$dummyLeader = New-Object Game.Player
$dummyLeader.CharID = [uint32]99991
$dummyLeader.CharName = "ChamberHero"

# Test AC85 Recv1 with empty payload (7 bytes total: 4B header + 0x55 + 0x01 + 0x00)
$pktBytes = [byte[]](0, 0, 0, 7, 85, 1, 0)
$recvPkt = New-Object Network.RecievePacket($pktBytes, 7)

$ac85 = New-Object Network.ActionCodes.AC85
$ac85.ProcessPkt($dummyLeader, $recvPkt)

$room = $dungeonEngine.GetPlayerRoom([uint32]99991)
Write-Host "Player Room Created:" ($room -ne $null)
if ($room -ne $null) {
    Write-Host ("Room ID: #{0}, Map: {1}, Leader: {2}, InProgress: {3}" -f $room.RoomId, $room.MapId, $room.LeaderName, $room.IsInProgress)
}

if ($room -eq $null -or $room.MapId -ne 30001) {
    Write-Error "[FAIL] Instance room creation failed!"
    exit 1
}
Write-Host "[PASS] Instance room created and active on chamber 30001!"

Write-Host "`n=== ALL TESTS PASSED SUCCESSFULLY ==="
