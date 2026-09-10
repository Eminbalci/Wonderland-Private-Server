# 04 - Data File Pipeline and EVE Scripting Engine

## 1. Client Data File Specifications

The server reads authentic Wonderland Online binary resource files stored in [`Data/`](file:///D:/GitHub/Wonderland-Private-Server/Data) to ensure 100% gameplay parity.

| Data File | Size (Approx) | Cipher / Format | Loaded By | Description |
| :--- | :--- | :--- | :--- | :--- |
| **`Npc.dat`** | ~680 KB | XOR Cipher (`0x5209`) | [`GameDataBase`](file:///D:/GitHub/Wonderland-Private-Server/wlo.pserver.core/DataBase/GameDataBase.cs) | Master repository of 4,928 authentic NPC and monster definitions. |
| **`Item.dat`** | ~1.2 MB | Binary Struct Array | [`ItemDatManager`](file:///D:/GitHub/Wonderland-Private-Server/Src/cGlobal.cs) | Complete item encyclopedia (equipment stats, icons, item types, buy/sell prices). |
| **`Skill.dat`** | ~450 KB | Binary Struct Array | [`SkillData`](file:///D:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/SkillData.cs) | Skill catalog, SP cost tables, elemental restrictions, animations, damage ratios. |
| **`Talk.dat`** | ~2.5 MB | Indexed String Table | [`TalkDatManager`](file:///D:/GitHub/Wonderland-Private-Server/Src/cGlobal.cs) | Dialogue trees, NPC speeches, player prompts, localized message lines. |
| **`Mark.dat`** | ~180 KB | Coordinate Vector List | [`MarkDatManager`](file:///D:/GitHub/Wonderland-Private-Server/Src/cGlobal.cs) | Mini-map markers, portal coordinates, building entry points. |
| **`Eve.emg`** | ~850 KB | Bytecode Script File | [`EveManager`](file:///D:/GitHub/Wonderland-Private-Server/wlo.pserver.core/DataFiles/EveManager.cs) | Story cutscenes, map warp triggers, interactive puzzle scripts, quest branches. |

---

## 2. `Npc.dat` XOR Cipher Decryption

Each record in `Npc.dat` is obfuscated using a 16-bit XOR arithmetic key (`0x5209`).

```csharp
public static NpcTemplateInfo DecodeNpcDatRecord(byte[] rawRecord)
{
    ushort xorKey = 0x5209;
    byte[] decoded = new byte[rawRecord.Length];
    for (int i = 0; i < rawRecord.Length; i += 2)
    {
        ushort val = BitConverter.ToUInt16(rawRecord, i);
        ushort decrypted = (ushort)(val ^ xorKey);
        Array.Copy(BitConverter.GetBytes(decrypted), 0, decoded, i, 2);
    }
    
    // Parse Decoded Struct
    ushort templateId = BitConverter.ToUInt16(decoded, 0);
    string name       = Encoding.GetEncoding(950).GetString(decoded, 2, 20).TrimEnd('\0');
    byte element      = decoded[22]; // 1=Earth, 2=Water, 3=Fire, 4=Wind
    byte level        = decoded[23];
    uint maxHp        = BitConverter.ToUInt32(decoded, 24);
    uint maxSp        = BitConverter.ToUInt32(decoded, 28);
    ushort atk        = BitConverter.ToUInt16(decoded, 32);
    ushort def        = BitConverter.ToUInt16(decoded, 34);
    ushort matk       = BitConverter.ToUInt16(decoded, 36);
    ushort mdef       = BitConverter.ToUInt16(decoded, 38);
    ushort spd        = BitConverter.ToUInt16(decoded, 40);

    return new NpcTemplateInfo(templateId, name, element, level, maxHp, maxSp, atk, def, matk, mdef, spd);
}
```

- **Parameters**: `rawRecord` (byte[]) — 64-byte encrypted binary buffer per NPC template.
- **Returns**: Populated `NpcTemplateInfo` object stored into SQLite `npc_data` table.
- **Exceptions**: Guards against out-of-bounds indices and character set fallback from Big5 to UTF-8.

---

## 3. `Eve.emg` Event Bytecode Interpreter

The server's [`EveManager`](file:///D:/GitHub/Wonderland-Private-Server/wlo.pserver.core/DataFiles/EveManager.cs) parses `.emg` script event instructions to drive interactive quests, map warp transitions, and dialogue trees.

### Primary EVE Opcode Reference

| Opcode (Hex) | Name | Operands | Functionality |
| :--- | :--- | :--- | :--- |
| `0x01` | `TALK_BUBBLE` | `talk_id (uint16)` | Dispatches AC 14 Sub 1 dialogue text to the player screen. |
| `0x02` | `CHOICE_PROMPT`| `prompt_id, options` | Shows selection modal with choices (Yes/No, branches). |
| `0x03` | `WARP_PLAYER`  | `map_id, x, y` | Teleports player and followers to target map coordinates. |
| `0x04` | `CHECK_ITEM`   | `item_id, count` | Evaluates if player inventory possesses required items. |
| `0x05` | `TAKE_ITEM`    | `item_id, count` | Consumes quest delivery items from the inventory. |
| `0x06` | `GIVE_ITEM`    | `item_id, count` | Rewards player with inventory items. |
| `0x07` | `SET_QUEST_FLAG`| `quest_id, step` | Updates `charquest` step progress in SQLite. |
| `0x08` | `START_BATTLE` | `mob_group_id` | Instantiates combat arena against predefined encounter. |
| `0x09` | `PLAY_ANIMATION`| `anim_id` | Triggers character or map prop animation sequence. |
| `0x0A` | `RECRUIT_PET`  | `npc_template_id` | Adds NPC directly to player's active companion slots. |
| `0x18` | `CUTSCENE_CUE` | `scene_id` | Initiates AC 186 cinematic sequence with camera lock. |

---

## 4. NPC Overworld Spawn Architecture

Overworld entities are linked between map geometry and NPC templates:
1. **`spawns.csv`**: Contains geometric coordinate anchors:
   - `map_id`, `click_id`, `template_id`, `npc_name`, `x`, `y`.
2. **Auto-Hydration**: On boot, [`GameDataBase.LoadSpawnsFromCsv`](file:///D:/GitHub/Wonderland-Private-Server/wlo.pserver.core/DataBase/GameDataBase.cs#L139-L175) parses records into SQLite `npcs` table.
3. **Template Resolution**: When a player enters a map, the server queries `npcs` and joins `npc_data` to broadcast AC 4 Sub 3 spawns with authentic stats, level, and element.
