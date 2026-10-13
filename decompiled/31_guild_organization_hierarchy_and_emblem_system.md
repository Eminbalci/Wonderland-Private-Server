# Guild Organization Hierarchy and Emblem System Specification

## 1. Executive Summary & Guild Architecture

Wonderland Online features a robust Guild (Organization / Army Group - 組織 / 幫派) subsystem enabling players to form alliances, manage member hierarchies, paint custom pixel emblems, maintain guild treasuries, and participate in weekly Fortress Siege Wars (Trojan War).

Extracted client source units identified via REA:
- `CY_Organ.pas`: Organization logic, member roster, rank authorities, and treasury transactions.
- `se_ArmyGroup.pas`: Army Group war state machine and guild battle coordination.
- `kAC_Mark.pas`: Binary deserializer for guild crests stored in `WLRI\data\Mark.dat`.
- In-game forms: `Form_ArmyGrounp_1`, `Form_ArmyGrounpInfo_1`, `Form_Emblem_1`, `Form_OrgNotice`, `Form_setauthority_1`, `Form_EditRule_1`.

---

## 2. In-Game Guild Interface Hierarchy

The guild user experience is orchestrated through specialized VCL forms dispatched based on sub-state `*(param_1 + 0xFC)`:

| Sub-State (`+0xFC`) | Form Identifier | Functional Purpose | Architectural Operations |
|---|---|---|---|
| `2` | `Form_ArmyGrounp_1` | Master Organization Dashboard | Displays guild name, leader, level, member count, treasury gold balance, and recruitment status |
| `3` | `Form_Emblem_1` | Guild Crest & Emblem Painter | 16x16 pixel bitmap editor allowing leaders to draw custom guild emblems saved into `Mark.dat` |
| `4` | `Form_EditRule_1` | Guild Bylaws & Rules Editor | Rich-text memo editor for internal organization bylaws, code of conduct, and recruitment guidelines |
| `N/A` | `Form_ArmyGrounpInfo_1` | Member Details & Census | Roster view tracking member level, elemental attribute, rebirth class, contribution points, and last login |
| `N/A` | `Form_OrgNotice` | Organization Announcement (MOTD) | Server-wide login bulletin broadcast to all online guild members |
| `N/A` | `Form_setauthority_1` | Rank Permission Bitmask Editor | Granular permission assignment for Officers, Elders, and standard Members |

---

## 3. Guild Rank Hierarchy & Authority Bitmasks (`Form_setauthority_1`)

Guild leadership assigns operational privileges using a dedicated authority bitmask structure:

| Bit Position | Authority Name (EN) | Authority Name (ZH) | Operational Privileges Granted |
|---|---|---|---|
| `Bit 0` (`0x01`) | Recruit Members | 招收成員 | Approve applications and send guild recruitment invitations |
| `Bit 1` (`0x02`) | Expel Members | 開除成員 | Kick lower-ranked members from the organization |
| `Bit 2` (`0x04`) | Edit Notice | 修改公告 | Update daily MOTD broadcast in `Form_OrgNotice` |
| `Bit 3` (`0x08`) | Edit Rules | 修改幫規 | Modify organization rules in `Form_EditRule_1` |
| `Bit 4` (`0x10`) | Promote / Demote | 職位升降 | Assign member ranks (Deputy Leader, Councilor, Member) |
| `Bit 5` (`0x20`) | Treasury Access | 存取幫費 | Withdraw funds from the guild bank treasury |
| `Bit 6` (`0x40`) | Declare War | 宣戰權限 | Initiate Guild Wars and register for Trojan War siege |
| `Bit 7` (`0x80`) | Emblem Modification | 修改會徽 | Repaint or update guild emblem in `Form_Emblem_1` |

### Default Rank Tiers:
1. **Guild Leader (幫主 / 會長):** All bits enabled (`0xFF`). Cannot be expelled.
2. **Deputy Leader (副幫主):** Bits `0x7F` (All administrative rights except guild disbanding).
3. **Councilor / Elder (長老):** Bits `0x0F` (Recruit, expel, notice, rules).
4. **Member (幫眾):** Bits `0x00` (Basic member privileges, chat, contribution).

---

## 4. Guild Crest & Emblem Binary Format (`Mark.dat` & `kAC_Mark.pas`)

Emblems drawn in `Form_Emblem_1` are serialized into the client database `data\Mark.dat` (1.19 MB):
- **Image Dimensions:** $16 \times 16$ pixels.
- **Color Depth:** 16-bit RGB 565 or 24-bit RGB (526 bytes per emblem record).
- **Emblem Record Layout:**
  - Bytes `0..3`: Guild Unique ID (`OrgID` - UInt32).
  - Bytes `4..7`: Emblem Version Timestamp.
  - Bytes `8..519`: Raw 512-byte pixel bitmap buffer ($16 \times 16 \times 2$ bytes).
  - Bytes `520..525`: Checksum / Trailing delimiter.
- **Rendering:** DirectDraw overlays the $16 \times 16$ guild emblem above player head sprites alongside character title text.

---

## 5. Network Protocol Integration (Action Code 40)

All guild operations are transmitted through **Action Code 40 (`0x28`)**:
- `AC 40:1`: Guild Member Roster Request / Sync
- `AC 40:2`: Guild Invitation Send (`[TargetCharID: UInt32]`)
- `AC 40:3`: Guild Invitation Response (`[Accept: Byte]`)
- `AC 40:4`: Member Expulsion / Kick
- `AC 40:5`: Member Rank & Authority Update (`[TargetCharID, NewRank, AuthorityMask]`)
- `AC 40:6`: Treasury Deposit / Withdrawal (`[Amount: UInt32]`)
- `AC 40:7`: Emblem Upload (`[OrgID: UInt32, 512B Bitmap Payload]`)
- `AC 40:8`: MOTD Notice Broadcast Update
