# NPC Binary Template Resolver & Decoding Specification

## 1. Overview
The server dynamically parses and decodes all NPC identities, names, base statistics, and drops from `Data/Npc.dat` at startup.

## 2. Binary Layout & Cipher Specification
* **File Format:** Fixed 138-byte binary records (`Pack = 1`).
* **Header:** Record 0 is a zero-padded structural marker; valid data begins at Record 1.

### 2.1 Field Decryption Formulas
| Field | Offset in Struct | Type | Decryption Formula |
| :--- | :--- | :--- | :--- |
| **Name** | `[1..10]` (or `[0..20]`) | ASCII | Stored in reverse byte order. Trimmed of null bytes and control codes (`0xCA`, `0xC8`). |
| **Template ID (`NpcID`)** | `[12..13]` | `ushort` | `((rawId ^ 0x5209) - 1) & 0xFFFF` |
| **Level** | `[37]` | `byte` | `((rawByte ^ 0xC8) - 1) & 0xFF` |
| **Max HP** | `[38..41]` | `uint32` | `((rawDword ^ 0x0BAEB716) - 1)` |
| **Element** | `[57]` | `byte` | `((rawByte ^ 0xC8) - 1) & 0xFF` (0=None, 1=Earth, 2=Water, 3=Fire, 4=Wind) |
| **Drop Items 1..5** | `[64..73]` (2 bytes each) | `ushort` | `((rawId ^ 0x5209) - 1) & 0xFFFF` |

## 3. Map NPC Resolution Pipeline
1. **Map Loading (`Eve.emg`):** Loads native NPC instances containing Map ClickID, Coordinates $(X, Y)$, and TemplateID (`npcId`).
2. **Template Lookup (`SceneDataManager.GetNpcName`):** Resolves the authentic name from `Npc.dat` using `((rawId ^ 0x5209) - 1)`.
3. **Dialogue Dispatch (`EveEventInterpreter`):** Links NPC ClickID to `eve.Emg` events and `Talk.dat` dialogues.
