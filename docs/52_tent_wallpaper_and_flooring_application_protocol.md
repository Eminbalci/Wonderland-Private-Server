# Tent Wallpaper & Flooring Application Protocol

## 1. Executive Summary
This document specifies the reverse-engineered wire protocol, item classification, dynamic client rendering pipeline, and server-side persistence architecture for applying wallpapers and flooring textures within player tents.

Previously, crafting a wallpaper item (such as Item ID 39041) caused the server to classify it as floor furniture due to `itemData.cellheight == 2`, invoking `PlaceFurnitureInTent` and placing the wallpaper as an invalid physical object on the tent ground grid. The tent walls were never textured, `AC 62:15` was never dispatched, and `chartent.floor1wallpaper` was never updated.

With this specification, crafting or deploying any wallpaper or flooring item dynamically transforms the tent walls or floors in real time across all occupants, broadcasts authentic `AC 62:14` / `AC 62:15` network packets, and commits the texture permanently to the `chartent` SQLite database table.

---

## 2. Reverse-Engineered Client Architecture (`aLogin.exe`)

### 2.1 Subcode 15: Wallpaper Dispatch & Texture Binding
Disassembly of `aLogin.exe` at virtual address `0x42ea88` and `0x433904`:
```asm
; --- aLogin.exe: Subcode 15 Handler (0x42ea88) ---
0x42ea88: lea eax, [ebp - 0x944]
0x42ea8f: mov ecx, 2               ; Length = 2 Bytes (UInt16 LE)
0x42ea94: mov edx, 2               ; Packet Offset 2 (Payload)
0x42ea99: mov eax, edi             ; Packet Buffer
0x42ea9b: call 0x14334             ; Unpack16 -> wallpaperId
0x42eaa0: mov edx, [ebp - 0x944]
0x42eaa6: mov eax, [0x4c9904]
0x42eaad: call 0x113660
0x42eab4: mov eax, ebx             ; Tent Renderer Object
0x42eab6: call 0x433904             ; SetWallpaper(wallpaperId)
0x42eabb: jmp 0x430b58

; --- SetWallpaper Implementation (0x433904) ---
0x433930: mov esi, edx             ; esi = wallpaperId
0x433932: mov word ptr [ebx + 0x1a68], si ; Store wallpaper ID in client memory
0x433939: test si, si
0x43393c: jne 0x433960             ; If wallpaperId == 0, reset to default (0x9897)
0x43393e: mov dword ptr [ebx + 0x1a70], 0xffffffff
0x433948: mov dword ptr [ebx + 0x1a6c], 0xffffffff
0x433952: mov word ptr [ebx + 0x1a6a], 0x9897
0x43395b: jmp 0x433d42
0x433960: mov edx, esi             ; Query Item Record from Item.dat
0x43396f: call 0x418c1c             ; GetItemRecord(wallpaperId)
0x433974: movzx eax, word ptr [ebp - 0x2f] ; Texture ID for Wall 1
0x43398a: call 0x45f558             ; LoadTexture(TextureID)
0x43398f: mov dword ptr [ebx + 0x1a70], eax ; Wall 1 Texture Pointer
0x4339a2: mov dx, word ptr [ebx + 0x1a68]  ; Query Wall 2 Texture ID
0x4339a9: call 0x418c1c             ; GetItemRecord(wallpaperId)
0x4339ae: movzx eax, word ptr [ebp - 0x2d] ; Texture ID for Wall 2
0x4339ca: call 0x45f558             ; LoadTexture(TextureID)
0x4339cf: mov dword ptr [ebx + 0x1a6c], eax ; Wall 2 Texture Pointer
```

### 2.2 Subcode 14: Floor Color Dispatch (`0x42ea50`)
- Packet structure: `[Header: 4B] [62] [14] [FloorColorID: 2B LE]` (Length: 8 bytes).
- Client calls `SetFloorColor(floorColorId)` at `0x433864`.
- Default floor color is `0x9896` (39062 decimal, "Wood Floor").
- Stores active floor texture pointer in `[ebx + 0x1a78]`.

### 2.3 Wall Rendering Check (`0x433493`)
During frame rendering, `aLogin.exe` checks:
```asm
0x433493: cmp word ptr [ebp + 0x1a68], 0
0x43349b: jbe 0x433857             ; Skip wallpaper rendering if 0
0x4334a1: mov eax, [ebp + 0x1a70]  ; Wall 1 Texture
0x4334b0: cmp [ebp + 0x1a6c], -1  ; Wall 2 Texture
0x4334c6: call 0x45f6f0             ; Render textured polygon mesh
```

---

## 3. Network Wire Format

### 3.1 Wallpaper Update (`AC 62:15`)
| Offset | Field | Type | Description |
|---|---|---|---|
| `0..3` | Header | UInt32 LE | Packet sync prefix (`F4 44 04 00`) |
| `4` | ActionCode | UInt8 | `62` (`0x3E`) |
| `5` | SubCode | UInt8 | `15` (`0x0F`) |
| `6..7` | WallpaperID | UInt16 LE | Item ID of the wallpaper (e.g., `39041`) |

### 3.2 Floor Texture Update (`AC 62:14`)
| Offset | Field | Type | Description |
|---|---|---|---|
| `0..3` | Header | UInt32 LE | Packet sync prefix (`F4 44 04 00`) |
| `4` | ActionCode | UInt8 | `62` (`0x3E`) |
| `5` | SubCode | UInt8 | `14` (`0x0E`) |
| `6..7` | FloorColorID | UInt16 LE | Item ID of the floor tile/wood (e.g., `39062`) |

---

## 4. Implementation Details

### 4.1 Item Classification (`Tent.cs`)
```csharp
public static bool IsWallpaperItem(PhxItemInfo itemData, ushort itemId)
{
    if (itemId >= 39041 && itemId <= 39044) return true;
    if (itemId == 39064 || itemId == 39128 || itemId == 39130 || itemId == 39136 || itemId == 39138 || 
        itemId == 39140 || itemId == 39142 || itemId == 39144 || itemId == 39169 || itemId == 39171 || 
        itemId == 39174 || itemId == 39176 || itemId == 39181 || itemId == 39193 || itemId == 39195 || 
        itemId == 39197 || itemId == 39199 || itemId == 39213 || itemId == 39297 || itemId == 39311 || 
        itemId == 39312 || itemId == 39326 || itemId == 39327 || itemId == 39336 || itemId == 39361 || 
        itemId == 39370 || itemId == 39441 || itemId == 39451 || itemId == 39484 || itemId == 39486 || 
        itemId == 39507 || itemId == 39524 || itemId == 39562 || itemId == 39595 || itemId == 39690)
    {
        return true;
    }
    if (itemData != null && itemData.ItemName != null && itemData.ItemName.Length > 0)
    {
        string name = Encoding.ASCII.GetString(itemData.ItemName).ToLowerInvariant();
        if (name.Contains("wall") && !name.Contains("lamp") && !name.Contains("light") && 
            !name.Contains("furnace") && !name.Contains("painting") && !name.Contains("clock"))
        {
            return true;
        }
    }
    return false;
}

public static bool IsFloorItem(PhxItemInfo itemData, ushort itemId)
{
    if ((itemId >= 39033 && itemId <= 39036) || itemId == 39062 || itemId == 39067 || itemId == 39068 || 
        itemId == 39131 || itemId == 39177 || (itemId >= 39189 && itemId <= 39191) || itemId == 39214 || 
        itemId == 39310 || itemId == 39335 || itemId == 39362 || itemId == 39372 || itemId == 39462 || 
        itemId == 39506 || itemId == 39561 || itemId == 39584)
    {
        return true;
    }
    if (itemData != null && itemData.ItemName != null && itemData.ItemName.Length > 0)
    {
        string name = Encoding.ASCII.GetString(itemData.ItemName).ToLowerInvariant();
        if ((name.Contains("floor") || name.Contains("tile")) && !name.Contains("lamp") && 
            !name.Contains("light") && !name.Contains("gift"))
        {
            return true;
        }
    }
    return false;
}
```

### 4.2 Application Methods (`Tent.cs`)
```csharp
public void SetWallpaper(ushort wallpaperId, int floor = 0)
{
    if (floor == 1) _wall2color = wallpaperId;
    else _wallcolor = wallpaperId;
    IsDirty = true;
    SendPacket pkt = Tools.FromFormat("bbw", 62, 15, wallpaperId);
    Broadcast(pkt);
    if (_owner != null && !m_playerlist.Contains(_owner)) _owner.Send(pkt);
    DebugSystem.Write(DebugItemType.Error, $"[Tent] SetWallpaper: Applied wallpaper #{wallpaperId} to floor {floor} for {_owner?.CharName}");
}

public void SetFloorColor(ushort floorColorId, int floor = 0)
{
    if (floor == 1) _floor2color = floorColorId;
    else _floorcolor = floorColorId;
    IsDirty = true;
    SendPacket pkt = Tools.FromFormat("bbw", 62, 14, floorColorId);
    Broadcast(pkt);
    if (_owner != null && !m_playerlist.Contains(_owner)) _owner.Send(pkt);
    DebugSystem.Write(DebugItemType.Error, $"[Tent] SetFloorColor: Applied floor color #{floorColorId} to floor {floor} for {_owner?.CharName}");
}
```

### 4.3 Crafting Pipeline Integration (`AC64.cs`)
1. **Craft Initiation (`Recv1`):**
   - Evaluates `isWallpaper` and `isFloor`.
   - Prevents `isFurniture` from being flagged for wallpaper/flooring items.
   - For 0-duration recipes: calls `r.Tent.SetWallpaper(outId)` or `r.Tent.SetFloorColor(outId)` immediately, saves `chartent`, and updates UI.
2. **Manufacturing Completion (`CompleteCraft`):**
   - For timed crafts: when the timer expires, checks `session.IsWallpaper` or `session.IsFloor`.
   - Invokes `p.Tent.SetWallpaper(session.ResultID)` or `p.Tent.SetFloorColor(session.ResultID)`.
   - Extra batch quantities (`session.Count > 1`) are safely deposited into player inventory bag.
   - Commits changes via `CharacterDataBase.SaveTentData(p, force: true)`.

### 4.4 Inventory Deployment Integration (`AC62.cs`)
- When a player drags a wallpaper or flooring item from their inventory into the tent (`AC62:1`):
  - Detects `Tent.IsWallpaperItem` or `Tent.IsFloorItem`.
  - Removes 1 unit from the inventory slot.
  - Applies texture to walls or floor via `SetWallpaper` / `SetFloorColor`.
  - Saves tent state and sends user notification.

---

## 5. Verification Checklist
- [x] Wallpapers (e.g., Item ID 39041) apply directly to walls upon crafting completion.
- [x] Floor tiles (e.g., Item ID 39062, 39506) apply directly to tent floor upon crafting completion.
- [x] `AC 62:15` is dispatched to all occupants to dynamically re-render walls in real time.
- [x] `AC 62:14` is dispatched to all occupants to dynamically re-render floor in real time.
- [x] SQLite database table `chartent` preserves `floor1wallpaper` and `floor1Color` across disconnects, map transitions, and server restarts.
- [x] Solution compiles cleanly with 0 Errors and 0 Warnings.
