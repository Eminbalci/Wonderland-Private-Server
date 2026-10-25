# Tent Crafting Manufacturing Timer & AC 64 Wire Protocol Specification

## 1. Overview and Problem Statement

When crafting items at tent appliances (such as the Workbench `38049`, Stove, or Furnace), recipes in Wonderland Online have defined manufacturing times (e.g. Recipe 501 for Wooden Basin `38017` takes 1 minute). Previously, the server immediately granted the item and dispatched completion packets (`AC 64:4` and `AC 64:9`) in the exact same millisecond upon receiving the start craft request (`AC 64:1`). Furthermore, `Send64_1` packed a 4-byte duration of `0`. This resulted in instantaneous craft completion without triggering the client's progress bar countdown or hammering/sawing crafting animation.

## 2. Reverse-Engineered Client Packet Handler (`aLogin.exe` `FUN_00401808`)

Analysis of `aLogin.exe` function `FUN_00401808` revealed the exact client state machine for ActionCode 64 (`0x40`):

### 2.1 ActionCode 64 SubCode Dispatch

| SubCode | Direction | Client Handling |
| :--- | :--- | :--- |
| `0x01` | Server -> Client | Sets active manufacturing slot state: `[benchIndex (1B)] [itemId (2B LE)] [remainingDurationSecs (4B LE)] [batchCount (1B)]` (stride = 8 bytes). |
| `0x02` | Server -> Client / Client -> Server | Sets bench active flag (`param_1 + 0x1b4d + bench * 0xd = 1`), displays `\tCrafting ` + item name, triggers character hammering/sawing animation, and renders `Panel_makeThingTimeForm_1` / `Panel_makeThingTime_1` countdown progress bar. |
| `0x03` | Server -> Client / Client -> Server | Cancels manufacturing, clears bench state, displays cancellation dialog, and resets player animation. |
| `0x04` | Server -> Client | Craft completed confirmation: plays `"Confirm"` sound and message, halts timer bar, and clears bench state. |
| `0x09` | Server -> Client | Finalizes immediate non-furniture bag crafts without lingering bench animation. |

### 2.2 Client Timer Unit Calculation (`FUN_00401808` & `FUN_00479102`)

In client memory, recipe build times are extracted from `Compound2.dat`:
```c
iVar2 = (uint)*(ushort *)(... + 0x1f + recipeIndex * 0x41) * 0x3c * count;
```
Here, `0x3c` = 60 seconds (1 minute per `buildTime` unit). Therefore, each unit of `cBuildElement.buildTime` corresponds to **1 minute (60 seconds)**.

## 3. Server-Side Manufacturing Session Architecture

### 3.1 `ActiveCraftSession` Data Structure

```csharp
public class ActiveCraftSession
{
    public uint CharID { get; set; }
    public byte BenchIndex { get; set; }
    public ushort FormulaIndex { get; set; }
    public ushort ResultID { get; set; }
    public byte Count { get; set; }
    public bool IsFurniture { get; set; }
    public int TotalDurationSeconds { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public List<Tuple<ushort, int>> DeductedMaterials { get; set; } = new List<Tuple<ushort, int>>();
    public Timer Timer { get; set; }
}
```

### 3.2 Thread-Safe Session Management

Active crafting sessions are indexed in `AC64` via a thread-safe `ConcurrentDictionary<string, ActiveCraftSession>` keyed by `"{CharID}_{BenchIndex}"`:
- On `Recv1` (`AC 64:1`): Validates ingredients against `requestedBatchCount`, deducts materials, records them for rollback, computes `durationSeconds = formula.buildTime * 60 * requestedBatchCount`, stores the session, dispatches `Send64_1` and `Send64_2`, and initializes an asynchronous timer.
- On Timer Expiration: `CompleteCraft` atomically claims the session via `TryRemove`, places furniture on the tent floor or adds bag items to inventory, broadcasts golden sparkles `AC 23:122`, dispatches `Send64_4`, and saves character state.
- On `Recv2` (`AC 64:2`): Queries remaining duration. If expired, immediately triggers `CompleteCraft`; otherwise dispatches refreshed `Send64_1` and `Send64_2`.
- On `Recv3` (`AC 64:3`): Cancels active craft, disposes timer, refunds all deducted materials to the player inventory, and dispatches `Send64_3`.

## 4. Tent Re-Entry & State Synchronization

To preserve active progress bars when a player leaves and re-enters their tent:
1. `Game.Code.Tent.OnTentItemsSent` static event is triggered in [`Tent.SendTentItemsToPlayer`](file:///D:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/PlayerRelated/Tent/Tent.cs).
2. `AC64.SyncActiveCraftsToPlayer` subscribes to this event and transmits current remaining seconds for any active bench in that player's tent.
