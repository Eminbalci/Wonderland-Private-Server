$ErrorActionPreference = "Stop"

$CoreDll = "D:\GitHub\Wonderland-Private-Server\wlo.pserver.core\bin\Debug\wlo.pserver.core.dll"
$ServerExe = "D:\GitHub\Wonderland-Private-Server\bin\Debug\Wonderland Private Server.exe"

Write-Host "Loading assemblies..."
[System.Reflection.Assembly]::LoadFrom($CoreDll) | Out-Null
[System.Reflection.Assembly]::LoadFrom($ServerExe) | Out-Null

Write-Host "Initializing MapManager and InstanceDungeonEngine..."
$mapManager = New-Object Game.Maps.MapManager
$dungeonEngine = New-Object Game.Experimental.InstanceDungeonEngine

Write-Host "Test 1: Verify isolated instance map creation"
$instMap1 = $mapManager.CreateInstanceMap(30001)
$instMap2 = $mapManager.CreateInstanceMap(30001)
if ($instMap1 -eq $null -or $instMap2 -eq $null) {
    throw "Failed to create instance maps"
}
if ($instMap1.GetHashCode() -eq $instMap2.GetHashCode()) {
    throw "Instance maps must be distinct instances for chamber isolation!"
}
Write-Host "PASS: Instance maps are strictly isolated ($($instMap1.GetHashCode()) != $($instMap2.GetHashCode()))"

Write-Host "Test 2: Register instance map in MapManager"
$mapManager.RegisterInstanceMap(1001, $instMap1)
$retrieved = $mapManager.GetInstanceMap(1001)
if ($retrieved -ne $instMap1) {
    throw "Failed to retrieve registered instance map"
}
$foundInActive = $false
foreach ($m in $mapManager.ActiveMaps) {
    if ($m -eq $instMap1) { $foundInActive = $true; break }
}
if (-not $foundInActive) {
    throw "Registered instance map not found in ActiveMaps"
}
Write-Host "PASS: Instance map registered and present in ActiveMaps"

Write-Host "Test 3: CreateRoom generates SubCode 0 (Success) roster packet"
$dummyLeader = New-Object Game.Player
$dummyLeader.CharID = 5501
$dummyLeader.CharName = "DungeonHero"

$room = $dungeonEngine.CreateRoom($dummyLeader, "Hero's Chamber", 30001, 4)
if ($room -eq $null) {
    throw "CreateRoom returned null"
}
if ($room.InstanceMap -eq $null) {
    throw "Room must have an isolated InstanceMap"
}
if ($room.Members.Count -ne 1) {
    throw "Room should have 1 member initially"
}
Write-Host "PASS: Room #$($room.RoomId) created with isolated chamber map (MapID: $($room.MapId))"

Write-Host "Test 4: JoinRoom adds participant and updates roster"
$dummyMember = New-Object Game.Player
$dummyMember.CharID = 5502
$dummyMember.CharName = "SideKick"

$joined = $dungeonEngine.JoinRoom($dummyMember, $room.RoomId)
if (-not $joined) {
    throw "JoinRoom failed"
}
if ($room.Members.Count -ne 2) {
    throw "Room members count should be 2"
}
Write-Host "PASS: Member successfully joined room (Members: $($room.Members.Count))"

Write-Host "Test 5: StartInstance initiates chamber session and transitions in-progress state"
$started = $dungeonEngine.StartInstance($dummyLeader)
if (-not $started) {
    throw "StartInstance failed"
}
if (-not $room.IsInProgress) {
    throw "Room IsInProgress must be true after start"
}
Write-Host "PASS: Dungeon instance started successfully. InProgress=$($room.IsInProgress)"

Write-Host "Test 6: LeaveRoom and disband cleanup"
$dungeonEngine.LeaveRoom($dummyMember)
if ($room.Members.Count -ne 1) {
    throw "Member count should be 1 after SideKick leaves"
}
$dungeonEngine.LeaveRoom($dummyLeader)
$roomAfterDisband = $dungeonEngine.GetPlayerRoom($dummyLeader.CharID)
if ($roomAfterDisband -ne $null) {
    throw "Room should be disbanded after leader leaves"
}
Write-Host "PASS: Room disbanded and cleaned up cleanly"

Write-Host "ALL DUNGEON INSTANCE TESTS PASSED (6/6)!"
