# 85 - Dungeon Instance Creation Deadlock Elimination & Talk ID Dialogue Resolution

## Executive Summary

This engineering specification resolves two interrelated core subsystems in the server runtime:
1. **Dungeon Instance Creation Deadlock:** A wire protocol synchronization deadlock in [`AC85.cs`](file:///D:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC85.cs) and [`InstanceDungeonEngine.cs`](file:///D:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/Experimental/InstanceDungeonEngine.cs) that prevented players from successfully creating or launching Duplicate Mission instance chambers (`Form_CreateDupMis` / `MZ_DupMis.pas`).
2. **Talk ID Dialogue Resolution:** An indexing defect in [`TalkResolver.cs`](file:///D:/GitHub/Wonderland-Private-Server/PhoenixData/DataFiles/TalkResolver.cs) where raw dialogue queries (such as Talk ID #18) failed to resolve against `Talk.dat`, throwing `"Could not resolve Talk ID #18 into any valid Talk.dat dialogue string."`.
3. **Map 30000+ Instance Event Whitelisting:** Whitelisted native interaction and combat trigger events inside isolated instance chambers (Maps 30000+) in [`EveEventRuntime.cs`](file:///D:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/Maps/Code/EveEventRuntime.cs).

---

## 1. Root Cause Analysis

### 1.1 The Dungeon Instance Creation Deadlock Loop
In Delphi client unit `MZ_DupMis.pas`, clicking the instance creation button `Btn_Create` (`FUN_0019bb9c`) opens the confirmation dialog and, upon confirmation (`FUN_0019f374`), transmits:
```
Action Code 0x55 (85), SubCode 0x01: [0x55, 0x01, 0x00] (7 bytes total wire frame)
```
In [`AC85.cs`](file:///D:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC85.cs):
```csharp
if (r.Buffer.Length >= 8)
{
    // Created room only if payload had explicit 2-byte MapID (Length >= 8)
}
else
{
    // Length was 7; if existingRoom was null, called engine.SendRoomList(p);
}
```
`engine.SendRoomList(p)` sent wire packet `AC 85, SubCode 0` with an empty roster. In the Delphi client (`FUN_001a10c4`), handling `SubCode 0` immediately triggered a re-transmission of `AC 85:1`:
```c
case 0:
    FUN_002d6ad8(*(undefined4 *)PTR_DAT_004c87fc, 0x55, 1, 0);
    break;
```
This created an infinite ping-pong loop where `existingRoom` remained null and the instance chamber was never created or launched.

### 1.2 SubCode 5 Wire Packet Alignment Mismatch
In Delphi client unit `MZ_DupMis.pas` (`FUN_001a1d9c`), incoming status sync packets (`Action Code 85, SubCode 5`) were parsed with the following offsets:
```c
local_14 = *(byte *)(local_c + 1); // SubCode: 5
local_10 = *(byte *)(local_c + 2); // CurrentWave (1..20)
// offset 4, length 4: RoomID (uint32)
// offset 8, length 2: MapID (uint16)
```
The server previously serialized `startPkt` with `RoomId` packed immediately at byte 2 and `CurrentWave` packed at byte 8, causing a severe field mismatch that resulted in invalid room validation in the client.

### 1.3 Talk ID #18 Resolution Failure
`Talk.dat` stores 17,494 fixed-size 292-byte records. Record #18 is decoded as:
- Encrypted header XOR with `0xECEA - 5` produces Talk ID `10018`.
- Record Index is `18`.
- Dialogue Text is `"Not opened this week."` (used by instance dungeon NPCs when a weekly instance is locked).

[`TalkResolver.cs`](file:///D:/GitHub/Wonderland-Private-Server/PhoenixData/DataFiles/TalkResolver.cs) previously only queried `_talkById` for direct header matches. When queried for `18`, it failed because `18 != 10018`. It also did not check category offsets (`10000 + 18`) or record indices (`dat.TryGetByRecordIndex(18)`), failing with `"Could not resolve Talk ID #18 into any valid Talk.dat dialogue string."`.

---

## 2. Technical Implementation Details

### 2.1 Robust AC85 Room Creation & Instant Launch
In [`Src/Network/ActionCodes/AC85.cs`](file:///D:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC85.cs), `Recv1` was refactored:
```csharp
private void Recv1(Player p, RecievePacket r)
{
    try
    {
        r.SetPtr(6);
        var engine = ExperimentalFeaturesManager.Instance.InstanceDungeons;
        if (engine == null) return;

        ushort mapId = 30001;
        byte maxMembers = 4;

        if (r.Buffer.Length >= 8)
        {
            ushort parsed = (ushort)r.Unpack16();
            if (parsed > 0) mapId = parsed;
            if (r.Buffer.Length >= 9) maxMembers = r.Unpack8();
        }

        var existingRoom = engine.GetPlayerRoom(p.CharID);
        if (existingRoom == null)
        {
            existingRoom = engine.CreateRoom(p, $"{p.CharName}'s Trial", mapId, maxMembers);
        }

        if (existingRoom != null)
        {
            if (existingRoom.LeaderCharId == p.CharID && !existingRoom.IsInProgress)
            {
                engine.StartInstance(p);
            }
            else if (existingRoom.IsInProgress)
            {
                engine.WarpPlayerToChamber(existingRoom, p);
            }
            else
            {
                engine.SendRosterUpdate(existingRoom, p);
            }
        }
    }
    catch (Exception ex)
    {
        DebugSystem.Write(new ExceptionData(ex));
    }
}
```

### 2.2 Wire Schema Alignment for SubCode 5 & Mobility Unfreeze
In [`wlo.pserver.core/Game/Experimental/InstanceDungeonEngine.cs`](file:///D:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/Experimental/InstanceDungeonEngine.cs):
1. **Party Ingestion:** Automatically includes existing party team members (`leader.m_teammembers`) in `room.Members` upon room creation.
2. **Chamber Teleportation & Wire Alignment:**
```csharp
public void WarpPlayerToChamber(InstanceRoom room, Player player)
{
    if (room == null || player == null) return;

    ushort spawnX = room.SpawnX > 0 ? room.SpawnX : (ushort)2869;
    ushort spawnY = room.SpawnY > 0 ? room.SpawnY : (ushort)1472;
    WarpData dungeonWarp = new WarpData { DstMap = room.MapId, DstX_Axis = spawnX, DstY_Axis = spawnY };

    if (player.CurMap != null && player.CurMap != room.InstanceMap)
    {
        player.PrevMap = new WarpData
        {
            DstMap = (ushort)player.CurMap.MapID,
            DstX_Axis = player.CurX,
            DstY_Axis = player.CurY
        };
    }

    if (player.CurMap is GameMap currentGm && room.InstanceMap != null)
    {
        currentGm.TeleportToInstance(room.InstanceMap, player, dungeonWarp);
    }
    else
    {
        player.CurMap?.Teleport(TeleportType.CmD, player, 0, dungeonWarp);
    }

    player.Send(Tools.FromFormat("bb", 20, 8)); // mobility unfreeze

    // Send status packet (AC 85:5) strictly aligned with Delphi FUN_001a1d9c wire schema
    SendPacket startPkt = new SendPacket();
    startPkt.Pack8(85);
    startPkt.Pack8(5); // SubCode 5 (byte 1)
    startPkt.Pack8((byte)room.CurrentWave); // CurrentWave (byte 2)
    startPkt.Pack8(0); // padding (byte 3)
    startPkt.Pack32(room.RoomId); // RoomID (bytes 4..7)
    startPkt.Pack16(room.MapId);  // MapID (bytes 8..9)
    player.Send(startPkt);
}
```

### 2.3 Comprehensive Talk ID Fallback Hierarchy
In [`PhoenixData/DataFiles/TalkResolver.cs`](file:///D:/GitHub/Wonderland-Private-Server/PhoenixData/DataFiles/TalkResolver.cs):
```csharp
// 1. Direct decrypted Talk ID lookup
if (dat.TryGetById(lookupId, out rawText))
{
    recordIdx = dat.GetOffset(lookupId) / 292;
    return BuildResult(result, rawText, recordIdx, "HeaderTalkId", playerName);
}

// 2. Category offset lookup (e.g. Talk ID 18 -> 10018)
if (lookupId < 10000 && dat.TryGetById(10000 + lookupId, out rawText))
{
    recordIdx = dat.GetOffset(10000 + lookupId) / 292;
    return BuildResult(result, rawText, recordIdx, "CategoryIdOffset", playerName);
}

// 3. 0-based Record Index lookup (e.g. Record 18)
if (dat.TryGetByRecordIndex(lookupId, out rawText))
{
    return BuildResult(result, rawText, (int)lookupId, "RecordIndex", playerName);
}

// 4. 1-based Record Index lookup (e.g. ID 18 -> Record 17)
if (lookupId > 0 && dat.TryGetByRecordIndex(lookupId - 1, out rawText))
{
    return BuildResult(result, rawText, (int)(lookupId - 1), "OneBasedRecordIndex", playerName);
}

// 5. Direct Byte Offset lookup (e.g. 0x063ED2)
if (rawTalkId > ushort.MaxValue && dat.TryGetByOffset(rawTalkId, out rawText))
{
    return BuildResult(result, rawText, -1, "DirectByteOffset", playerName);
}
```

### 2.4 Event Whitelisting for Instance Maps
In [`wlo.pserver.core/Game/Maps/Code/EveEventRuntime.cs`](file:///D:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/Maps/Code/EveEventRuntime.cs):
```csharp
private static bool RejectDisabledNativeEvent(Player player, GameMap map, EventsinMapEntries ev)
{
    // Allow native events inside active instance chambers (Maps 30000+) when experimental instance engine is enabled
    if (map != null && map.MapID >= 30000 && Game.Experimental.ExperimentalFeaturesManager.Instance.EnableInstanceDungeons)
        return false;

    if (!IsNativeEventDisabled(map.MapID, ev.clickID)) return false;
    player.CancelInteraction();
    player.SendHeadBanner("This quest is disabled: required game data is unavailable.");
    return true;
}
```

---

## 3. Verification & Empirical Results

The verification script [`tests/Test-InstanceAndTalkResolution.ps1`](file:///D:/GitHub/Wonderland-Private-Server/tests/Test-InstanceAndTalkResolution.ps1) confirmed:
- **Talk ID #18 Resolution:**
  - Success: `True`
  - Method: `CategoryIdOffset`
  - Formatted Text: `"Not opened this week."`
- **Instance Creation & Launch:**
  - Simulated 7-byte wire packet `AC 85:1:0`.
  - Room created: `Room #1001` on Map `30001`.
  - Wire packet `AC=85, Sub=0` (roster) emitted.
  - Wire packet `AC=85, Sub=5` emitted: `F4 44 0A 00 55 05 01 00 E9 03 00 00 31 75` (CurrentWave=1, RoomId=1001, MapId=30001).
  - Unfreeze `AC=20, Sub=8` dispatched.
  - `InProgress = True`.
- **Comprehensive Regression Suite:**
  - `tests/Run-ComprehensiveVerification.ps1`: All 3 tests passed with 0 errors.
  - Solution build: 0 warnings, 0 errors.
