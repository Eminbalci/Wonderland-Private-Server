# Technical Specification: Experimental Stall & Player Null-Safety Hotfix

## Overview
This document details the defect analysis, architectural root cause, and systemic resolution for the unhandled exception triggered when clicking the "Spawn Test Vendor Stall" (`btnSimStall`) and associated experimental simulator actions within `MainForm1.ExperimentalTab.cs`.

---

## 1. Defect & Root Cause Analysis

### 1.1 Unhandled `NullReferenceException` in `Player.get_CharID()`
* **Symptom:** Clicking `btnSimStall` threw an unhandled `System.NullReferenceException` at runtime.
* **Mechanism:**
  ```csharp
  // MainForm1.ExperimentalTab.cs
  var dummy = new Player { CharID = 4401, CharName = "Traveling Merchant" };
  ExperimentalFeaturesManager.Instance.PlayerStallBazaar.OpenStall(dummy, "Rare Alchemy Supplies", items);
  ```
  Inside `PlayerStall..ctor`:
  ```csharp
  VendorCharId = vendor?.CharID ?? 0;
  ```
  Inside `Player.cs`:
  ```csharp
  public override uint CharID
  {
      get
      {
          return (Slot == 1) ? UserAcc.Character1ID : UserAcc.Character2ID;
      }
      set
      {
          base.CharID = value;
      }
  }
  ```
  1. The parameterless constructor `public Player() : base()` did not initialize `m_useracc`, leaving `UserAcc` as `null`.
  2. Calling `vendor.CharID` evaluated `UserAcc.Character2ID`, throwing `NullReferenceException`.
  3. Setting `CharID = 4401` stored the value in `base.CharID`, but the getter completely ignored `base.CharID`.

### 1.2 `Tools.FromFormat` and Packet Construction Vulnerabilities
* `Tools.FromFormat("bbdb", 5, 16, vendor.CharID, (byte)1)` invoked format specifier `'d'`, executing `Convert.ToInt32((uint)vendor.CharID)`. If `CharID` exceeded signed 32-bit integer limits (`int.MaxValue`), an `OverflowException` occurred.
* Replaced reflection-heavy LINQ formatting with native `SendPacket.Pack32(vendor.CharID)` byte streams.

### 1.3 Null Bag References on Disconnected / Synthetic Players
* `AquacultureFishingEngine.CatchFish` attempted `player.Inv.AddItem(...)` directly without null checking. In headless or GUI simulation instances without an active client socket, `player.Inv` was null.
* Replaced with null-conditional propagation: `player.Inv?.AddItem(...)`.

---

## 2. Implemented Resolutions

### 2.1 Player Constructor & Property Hardening (`Player.cs`)
* Initialized `m_useracc = new User()` and `m_teammembers = new List<Player>()` within the default parameterless constructor.
* Enhanced `Player.CharID` getter with strict fallback hierarchy:
  ```csharp
  public override uint CharID
  {
      get
      {
          if (base.CharID != 0) return base.CharID;
          if (UserAcc != null)
          {
              return (Slot == 1) ? UserAcc.Character1ID : UserAcc.Character2ID;
          }
          return 0;
      }
      set
      {
          base.CharID = value;
      }
  }
  ```

### 2.2 PlayerStallBazaarEngine Hardening (`PlayerStallBazaarEngine.cs`)
* Wrapped `OpenStall`, `PurchaseItem`, and `CloseStall` within isolated `try-catch` blocks and `DebugSystem.Write` logging.
* Switched broadcast packets from `Tools.FromFormat("bbdb", ...)` to zero-allocation native `SendPacket` calls packing unsigned 32-bit integers (`Pack32`).
* Safely referenced coordinates via `vendor?.CurX ?? 0` and `vendor?.CurY ?? 0`.

### 2.3 Comprehensive UI Safety (`MainForm1.ExperimentalTab.cs`)
* Wrapped all test simulation button triggers (`btnSimStall`, `btnTestArcade`, `btnTestDonation`, `btnTestMatch`, `btnTestCatch`, `btnTestBather`) in `try-catch` structures with informative user modals (`MessageBoxButtons.OK`).
* Prevented silent failure or crash loops when operating the server cockpit interactively.

---

## 3. Verification & Empirical Proof
* Executed end-to-end PowerShell integration suite against newly compiled binaries:
  1. **Instance Dungeon (`btnCreateTest`):** Room #1001 created successfully.
  2. **Church Charity (`btnTestDonation`):** 5,000G donated, Karma awarded, Tithe Vault updated.
  3. **Gobang Match (`btnTestMatch`):** Match #7001 started between 8801 and 8802 with 5,000G ante.
  4. **Aquaculture Fishing (`btnTestCatch`):** Fish rolled, weight calculated, item credited without bag null exception.
  5. **Hot Spring Bathing (`btnTestBather`):** Bather registered on map 10000, rest pulse applied (+50 HP / +25 SP).
  6. **Arcade Minigame (`btnTestArcade`):** Session #8001 created, score 450 recorded.
  7. **Player Stall Bazaar (`btnSimStall`):** Shop 'Rare Alchemy Supplies' opened, 2 items stocked, purchased by buyer, kiosk appearance broadcasted, shop closed cleanly.
* Solution build verification: `0 Warnings`, `0 Errors`.
