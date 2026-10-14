# 25 - Native Client Private Shop Protocol & GM Guard Relaxation

## 1. Overview & Problem Diagnosis
In the official Wonderland Online client, players open their personal market stalls via the built-in GUI ("Private Shop"). When a player drags inventory items to the vending window, sets unit gold prices, and clicks OK, the game client dispatches packet **`AC 23:30` (`0x17, 0x1E`)**.

Prior to this implementation:
1. `Src/Network/ActionCodes/AC23.cs` had no handler for `AC 23:30`, dropping the packet with `AC 23,30 has not been coded`. As a result, the client never received confirmation or state synchronization, causing shop creation to silently abort.
2. The in-game command `:spawnstall` was protected with a strict GM-only check (`!GmManager.IsGm(c)`), preventing non-GM testing accounts (e.g. `purnaci`) from spawning custom kiosks on desired maps and coordinates.
3. In `wlo.pserver.core/Game/Item.cs`, accessing `item.Name` threw `ArgumentNullException` when `Data.ItemName` was null for empty or uninitialized item slots.

---

## 2. Wire Protocol Specifications

### 2.1 Native Private Shop Setup: `AC 23:30` (Client -> Server)
- **Header:** `0xF4, 0x44, [Length (2B)]`
- **Action Code:** `0x17` (`23`)
- **Sub Action Code:** `0x1E` (`30`)
- **Payload Schema:**
  - `TitleLen` (`uint8`): Shop title length.
  - `Title` (`string`, ASCII): Stall title string (e.g. `"Private Shop"`).
  - `Count` (`uint8`): Number of stocked items (clamped to max 10).
  - Repeated per item (`6 bytes`):
    - `InvSlot` (`uint8`): 1-based inventory slot index.
    - `Quantity` (`uint8`): Stocked quantity.
    - `UnitPrice` (`uint32`): Unit sale price in gold.

### 2.2 Native Private Shop Confirmation: `AC 23:30` (Server -> Client)
Triggers client subroutine `FUN_004546e8` in `aLogin.exe`:
- **Payload Schema:**
  - `AC` (`uint8`): `23`
  - `SubAC` (`uint8`): `30`
  - `Title` (`string`): 1-byte length followed by ASCII characters.
  - `Count` (`uint8`): Item count.
  - Repeated per item (`5 bytes`):
    - `Slot` (`uint8`): Inventory slot index for price assignment (`*(actor + 0x27e8 + slot * 4)`).
    - `UnitPrice` (`uint32`): Unit sale price in gold.
- **Client Execution:**
  - Sets `*(actor + 0x2231) = 1` (Active item vending stall flag).
  - Constructs signboard banner over character head.
  - Locks shop dialog into active vending mode.

### 2.3 Native Private Shop Shutdown: `AC 23:31` (Bidirectional)
Triggers client subroutine `FUN_00454cb8` in `aLogin.exe`:
- **Payload Schema:**
  - `AC` (`uint8`): `23`
  - `SubAC` (`uint8`): `31`
  - `VendorCharID` (`uint32`): Vendor character ID.
- **Client & Server Actions:**
  - Resets `*(actor + 0x2231) = 0`.
  - Resets vendor posture to standing (`AC 32:2`, emote = 0).
  - Removes signboard banner (`AC 56:30` with empty string).
  - Closes browsing dialogs for peer players viewing the stall.

---

## 3. Structural Modifications

### 3.1 `Src/Network/ActionCodes/AC23.cs`
- Added `case 30: Recv30(r, p); break;` and `case 31: Recv31(r, p); break;`.
- Implemented `Recv30`:
  - Unpacks title, item count, and item specifications (`invSlot`, `qty`, `price`).
  - Safe item name resolution with fallback to `MonsterDropManager.ResolveItemName`.
  - Dispatches to `PlayerStallBazaarEngine.Instance.OpenStall`.
- Implemented `Recv31`:
  - Closes active personal stall via `PlayerStallBazaarEngine.Instance.ClosePersonalStall`.

### 3.2 `wlo.pserver.core/Game/Experimental/PlayerStallBazaarEngine.cs`
- Added `InvSlot` property to `StallItem`.
- Added `VendorPlayer` reference property to `PlayerStall`.
- Added `BuildStallNativeConfirmPacket(PlayerStall stall)` generating `AC 23:30` confirmation packet.
- Added `BuildStallNativeClosePacket(uint vendorCharId)` generating `AC 23:31` closure packet.
- Updated `OpenStall` to transmit `BuildStallNativeConfirmPacket`.
- Updated `CloseStall` to broadcast `BuildStallNativeClosePacket`.
- Updated `PurchaseItem` to credit seller's purse (`stall.VendorPlayer.Gold += totalCost`) and deduct items from bag (`stall.VendorPlayer.Inv.RemoveItem`).

### 3.3 `wlo.pserver.core/Game/Player.cs`
- Relaxed `:spawnstall` permission guard:
  `if (!GmManager.IsGm(c) && !ExperimentalFeaturesManager.Instance.EnablePlayerStallBazaar)`
  Enables all players to test spawning kiosks at target map coordinates when the Experimental Stall feature is active.

### 3.4 `wlo.pserver.core/Game/Item.cs`
- Null-safety hardened `Item.Name` getter:
  `public string Name { get { return Data.ItemName != null ? ASCIIEncoding.ASCII.GetString(Data.ItemName) : string.Empty; } }`
  Prevents `ArgumentNullException` when inspecting empty inventory slots.

---

## 4. Verification Evidence
Unit and protocol suite `.codex-verify/Test-StallPacket.ps1` verified:
- `AC 23:30` packet ingestion from real client log `F4 44 15 00 17 1E 0C 50 72 69 76 61 74 65 20 53 68 6F 70 01 09 01 00 00 00 00`.
- Stall registered in `PlayerStallBazaarEngine` with title `"Private Shop"` and 1 item.
- `AC 23:30` native confirmation packet dispatched to client.
- `AC 56:30` signboard banner dispatched.
- Player sitting posture applied (`Emote = 1`).
- `AC 23:31` shutdown packet closes the stall cleanly, resets posture (`Emote = 0`), and removes banner.
- Build status: 0 errors, 0 warnings.
