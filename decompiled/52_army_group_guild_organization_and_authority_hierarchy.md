# 52 - Army Group (Guild Organization) and Authority Hierarchy Specification

## 1. Architectural Overview & Source Mapping

The guild and legion systems in Wonderland Online are formally designated as **Army Groups** (軍團系統). The architecture is implemented in Delphi units `se_ArmyGroup.pas` and `se_EnsignType.pas`, managing hierarchical rank permissions, member rosters, organizational bulletins, battle registrations, and custom banner emblems.

### Subroutine Inventory & Source Locations

| Virtual Address | Subroutine Symbol | File / Unit | Purpose |
| :--- | :--- | :--- | :--- |
| `0x0028815c` | `FUN_0028815c` | `se_ArmyGroup.pas` | Multi-tab UI page switcher (Info, Members, Emblem, Notice) |
| `0x002888d8` | `FUN_002888d8` | `se_ArmyGroup.pas` | Emblem raster grid pixel rendering ($10 \times 10$ block array) |
| `0x002889e8` | `FUN_002889e8` | `se_ArmyGroup.pas` | Multi-layer emblem surface blitting and color index mapping |
| `0x00292994` | `FUN_00292994` | `se_ArmyGroup.pas` | Guild bulletin board text renderer and character word wrapper |
| `0x0029b784` | `FUN_0029b784` | `se_ArmyGroup.pas` | Member rank authority checkbox toggle handler |
| `0x0029b940` | `LAB_0029b940` | `se_ArmyGroup.pas` | Authority permission commit and server update dispatcher |
| `0x0029c8e4` | `FUN_0029c8e4` | `se_ArmyGroup.pas` | Checkbox visual state synchronization (`btn_Check_1` / `btn_UnCheck_1`) |
| `0x00272168` | `FUN_0029ca90` | `se_ArmyGroup.pas` | `Form_setauthority_1` modal dialog constructor ($345 \times 239$ px) |

---

## 2. Form Topology & Multi-Page View Modes

The primary guild interface (`Form_ArmyGrounp_1`) operates as a multi-page tab container dynamically switching sub-forms based on the selector state at `*(int *)(param_1 + 0xfc)`:

```
+-------------------------------------------------------------+
| [icon] Army Group Organization               [Btn_Close_s_1]|
+-------------------------------------------------------------+
|  [Tab 1: Overview] [Tab 2: Members] [Tab 3: Emblem] [Tab 4: Notice] |
|  +-------------------------------------------------------+  |
|  | Page 1: Form_ArmyGrounpInfo_1 (Level, Chief, Assets)  |  |
|  | Page 2: Form_ArmyGrounp_1 (Roster, Ranks, Levels, Loc) |  |
|  | Page 3: Form_Emblem_1 (16x16 Pixel Art Painter)       |  |
|  | Page 4: Form_OrgNotice (Internal & Public Bulletins)  |  |
|  +-------------------------------------------------------+  |
|  [Btn_SetAuthority] [Btn_KickMember] [Btn_Invite] [Btn_Leave]|
+-------------------------------------------------------------+
```

### Page Dispatch Matrix (`FUN_0028815c`)

| Index (`+0xfc`) | Active Sub-Form | Surface Name | Functionality |
| :---: | :--- | :--- | :--- |
| `1` | `Form_ArmyGrounpInfo_1` | General Info | Guild Level, Leader, Member Count, Total Experience, Vault Balance |
| `2` | `Form_ArmyGrounp_1` | Member Roster | 50-member DataGrid with Name, Class, Level, Rank, and Status |
| `3` | `Form_Emblem_1` | Crest Painter | $16 \times 16$ pixel raster canvas with 8-color palette controls |
| `4` | `Form_OrgNotice` | Bulletin Board | Guild leader announcement broadcast editor and display |

---

## 3. Authority Hierarchy & Permissions (`Form_setauthority_1`)

Guild leadership can assign granular administrative permissions to subordinate members using the modal dialog `Form_setauthority_1` ($345 \times 239$ pixels).

```
+-------------------------------------------------------------+
| [Icon_Organ_1] Member Authority Setup        [Btn_Close_s_1]|
+-------------------------------------------------------------+
|  Target Member: [Character Name]                            |
|  Assigned Rank: [Vice-Leader / Elder / Captain / Member]    |
|                                                             |
|  Permissions:                                               |
|  [X] btn_UnCheck_1 (1): Member Recruitment / Inviting       |
|  [ ] btn_UnCheck_1 (2): Member Expulsion / Kicking          |
|  [ ] btn_UnCheck_1 (3): Bulletin / Notice Board Editing     |
|  [ ] btn_UnCheck_1 (4): Guild War / Siege Registration      |
|  [ ] btn_UnCheck_1 (5): Guild Vault / Resource Withdrawal   |
|                                                             |
|         [Btn_OK_1 (Apply)]         [Btn_Close_1 (Cancel)]   |
+-------------------------------------------------------------+
```

### Permission Bitmask Specification

Permissions are stored as a 1-byte bitfield in the member record:

| Bit Position | Value | Authority Privilege | Description |
| :---: | :---: | :--- | :--- |
| `Bit 0` | `0x01` | `AUTH_INVITE` | Can send guild invitations to unaffiliated players |
| `Bit 1` | `0x02` | `AUTH_KICK` | Can expel lower-ranked members from the guild |
| `Bit 2` | `0x04` | `AUTH_NOTICE` | Can edit and publish guild announcements (`Form_OrgNotice`) |
| `Bit 3` | `0x08` | `AUTH_WAR_REG` | Can register the guild for Friday / Saturday Castle Sieges |
| `Bit 4` | `0x10` | `AUTH_VAULT` | Can withdraw funds and items from the guild shared storage |
| `Bits 5..7` | `0xE0` | `RESERVED` | Reserved for future expansion / GM oversight |

---

## 4. Emblem Rasterization Engine (`FUN_002888d8` / `FUN_002889e8`)

Wonderland Online allows guilds to design a custom $16 \times 16$ pixel icon displayed next to member names and on flags.

### Canvas Memory & Raster Pipeline

1. **Color Palette Mapping (`param_1 + 0xb60`):**
   - The palette contains 8 indexed 16-bit RGB 565 values:
     `0`: Transparent, `1`: Black, `2`: White, `3`: Red, `4`: Green, `5`: Blue, `6`: Yellow, `7`: Purple.
2. **Grid Geometry:**
   - Canvas size: $16 \times 16$ grid blocks.
   - Display block size: $10 \times 10$ pixels per canvas block (`FUN_002888d8`: `*(int *)(local_18 + bVar2 * 8) * 10`).
   - Total canvas render footprint: $160 \times 160$ pixels.
3. **Double-Buffered Blitting:**
   - Background canvas surface: `*(undefined4 *)(param_1 + 0xb58)`.
   - Dynamic block coordinate offset: $(X = \text{param}_1 + 0xb80, Y = \text{param}_1 + 0xb84)$.

---

## 5. Wire Protocol & Network Framing

Guild operations communicate via **Action Code 31 (`0x1F`)**.

### Guild Operations Protocol Matrix

| Action Code | Sub-Opcode | Direction | Description | Payload Schema |
| :---: | :---: | :---: | :--- | :--- |
| `0x1F` | `0x01` | C $\to$ S | Create Army Group | `[Cost: 50,000g] [Name Len] [Name String]` |
| `0x1F` | `0x02` | C $\to$ S | Invite Player to Guild | `[Target CharID (4B)]` |
| `0x1F` | `0x03` | C $\to$ S | Expel Member | `[Target CharID (4B)]` |
| `0x1F` | `0x04` | C $\to$ S | Set Member Authority | `[Target CharID (4B)] [Auth Bitmask (1B)]` |
| `0x1F` | `0x05` | C $\to$ S | Update Guild Notice | `[Notice Len (2B)] [Notice String]` |
| `0x1F` | `0x06` | C $\to$ S | Upload Emblem Canvas | `[128 Bytes Packed 4-bit Nibble Pixel Data]` |
| `0x1F` | `0x07` | C $\to$ S | Leave Army Group | `[Confirm Flag (1B)]` |
| `0x1F` | `0x08` | C $\to$ S | Disband Army Group | `[Secondary Security PIN]` |
| `0x1F` | `0x10` | S $\to$ C | Synchronize Roster | `[Member Count (1B)] { [ID] [Level] [Job] [Auth] [Name] }` |

### Authority Update Wire Packet Structure (`0x1F:0x04`)

```
+---------------+---------------+-------------------------------+---------------+
| Action (0x1F) | Sub-Op (0x04) | Target Character ID (4 Bytes) | Auth Byte (1B)|
+---------------+---------------+-------------------------------+---------------+
```

---

## 6. Private Server Implementation Requirements

1. **Permission Enforcement:** The server must rigorously validate the sender's rank and `AUTH_*` bitmask before executing kick, notice, war, or vault transactions.
2. **Emblem Persistence:** Store the 128-byte packed emblem buffer in the `guilds` database table and broadcast updates to all online guild members.
3. **Cross-Map Synchronization:** Member online/offline status and level changes must be broadcast across all map channels to maintain consistent roster DataGrids.
