# Document 60: AoE Multi-Target Damage & Hit Animation Synchronization Engine

## 1. Architectural Overview & Defect Root Cause

In *Wonderland Online*, Area of Effect (AoE) offensive skills and multi-target support abilities affect multiple combatants simultaneously across the 2x4 tactical grid system. When an AoE attack is executed, every creature within the pattern geometry must simultaneously receive damage, display numerical damage indicators, and enter its corresponding hurt / damage-taking flinch animation (`TFightHum.Action = Hurt/Flinch`).

### The Defect
When players used AoE skills against multiple targets, only the primary target (`targetFighter` at index 0) visually reacted, displayed damage floating numbers, and entered the hurt animation. Secondary and tertiary monsters took damage in server-side state (`target.CurHP`), but remained visually in idle stance and suddenly vanished when the death packet (`AC 53:3`) arrived.

### Client Disassembly Analysis (`aLogin.exe`)
Inspection of the client's battle packet handler (`FUN_003abf3c`) and hurt trigger dispatcher (`FUN_003ac7ac`) revealed the exact binary wire protocol:

```c
local_1c = FUN_0001412c(local_c); // Packet length
iVar7 = 2;
do {
    FUN_00014334(local_c, iVar7, 2, &local_28);
    uVar3 = FUN_00112da4(*(undefined4 *)PTR_DAT_004c87fc, local_28); // 17 (0x11)
    local_d = *(byte *)(local_c + 1 + iVar7); // Actor GridX
    local_e = *(byte *)(local_c + 2 + iVar7); // Actor GridY
    FUN_00014334(local_c, iVar7 + 4, 2, &local_2c);
    local_14 = FUN_00112da4(*(undefined4 *)PTR_DAT_004c87fc, local_2c); // SkillId
    local_12 = *(undefined1 *)(local_c + 5 + iVar7);
    local_11 = *(undefined1 *)(local_c + 6 + iVar7); // 1
    local_f = *(byte *)(local_c + 7 + iVar7);        // Target GridX
    local_10 = *(byte *)(local_c + 8 + iVar7);       // Target GridY
    FUN_00014334(local_c, iVar7 + 8, (uVar3 & 0xffff) - 6, &local_24);
    iVar7 = iVar7 + (uVar3 & 0xffff) + 2;

    iVar4 = FUN_003b2570(DAT_0073f0b0, local_d, local_e);
    if (0 < iVar4) {
        local_20 = *(int *)(DAT_0073f0b0 + 0x1a0 + iVar4 * 4);
        ...
        FUN_003b2c48(local_20, local_14, local_11, uVar8);
        *(short *)(local_20 + 0x2210) = local_14;
        // Triggers the hurt / flinch animation on target (local_f, local_10)!
        FUN_003ac7ac(local_8,
                     *(undefined1 *)(DAT_0073f0b0 + (uint)local_d * 0x46 + 0x4c + (uint)local_e * 0xe),
                     *(undefined1 *)(DAT_0073f0b0 + (uint)local_f * 0x46 + 0x4c + (uint)local_10 * 0xe),
                     local_16, local_12, sVar2);
    }
} while (iVar7 < local_1c);
```

### Critical Discovery
1. `FUN_003ac7ac` is the routine that sets the target entity into its hurt / damage flinch animation (`FUN_00392808`).
2. `FUN_003ac7ac` is called in the **outer record loop** of `FUN_003abf3c` for each 19-byte block (`uVar3 = 17` + 2 byte length = 19 bytes).
3. `local_f` and `local_10` (the target coordinates passed to `FUN_003ac7ac`) are read strictly from `*(local_c + 7 + iVar7)` and `*(local_c + 8 + iVar7)`.
4. Therefore, when multiple targets were grouped into a single block with `targetCount > 1`, `FUN_003ac7ac` was only executed for the first target at index 0. All remaining targets never had `FUN_003ac7ac` called, completely bypassing their hurt animation!
5. In official client architecture, multi-target AoE attacks are encoded as **concatenated 19-byte records** within a single `AC 50:1` packet, one record per targeted entity.

---

## 2. Binary Wire Protocol Specification

For each targeted entity in an AoE attack, an authentic 19-byte record is packed into the single `AC 50:1` packet:

```
[Per-Target Record Layout (19 bytes)]
Offset 0x00 (2 bytes): 0x11, 0x00 (16-bit block length = 17 bytes)
Offset 0x02 (1 byte) : actor.GridX
Offset 0x03 (1 byte) : actor.GridY
Offset 0x04 (2 bytes): skillId (ushort, little-endian)
Offset 0x06 (1 byte) : 0x00
Offset 0x07 (1 byte) : 0x01 (Target Count = 1 for this block)
Offset 0x08 (1 byte) : target.GridX
Offset 0x09 (1 byte) : target.GridY
Offset 0x0A (1 byte) : 0x01
Offset 0x0B (1 byte) : isDefending (0x00 or 0x01)
Offset 0x0C (1 byte) : 0x01
Offset 0x0D (1 byte) : statType / status (0x19 for HP Damage, 0xDF Freeze, 0xDE Petrify, 0xDD Sleep, 0xE0 Poison)
Offset 0x0E (4 bytes): statValue / damage (uint32, little-endian)
Offset 0x12 (1 byte) : hitType (0x01 = normal, 0x02 = critical digits)
```

---

## 3. Implementation Details

### Attack Animation Stream (`pAttackAnim`)
Updated `PvEBattleManager.cs` Phase 4:
```csharp
foreach (var target in hitTargets)
{
    // ... Damage, elemental multiplier, and critical calculation ...

    // Authentic 19-byte record per hit target
    pAttackAnim.PackArray(new byte[] { 0x11, 0x00 });
    pAttackAnim.Pack8(actor.GridX); pAttackAnim.Pack8(actor.GridY);
    pAttackAnim.Pack16(a.SkillId > 0 ? a.SkillId : (ushort)10001);
    pAttackAnim.Pack8(0);
    pAttackAnim.Pack8(1);
    pAttackAnim.Pack8(target.GridX); pAttackAnim.Pack8(target.GridY);
    pAttackAnim.Pack8(1);
    pAttackAnim.Pack8(defendingActors.Contains((target.GridX << 8) | target.GridY) ? (byte)1 : (byte)0);
    pAttackAnim.Pack8(1);
    if (isPureDebuff)
    {
        byte debuffByte = 0;
        if (sk != null)
        {
            if (sk.IsFreeze) debuffByte = 0xDF;
            else if (sk.IsSeal) debuffByte = 0xDE;
            else if (sk.IsSleep) debuffByte = 0xDD;
            else if (sk.IsPoison) debuffByte = 0xE0;
        }
        pAttackAnim.Pack8(debuffByte);
        pAttackAnim.Pack32(0);
        pAttackAnim.Pack8(1);
    }
    else
    {
        pAttackAnim.Pack8(0x19); // HP damage
        pAttackAnim.Pack32((uint)finalDmg);
        pAttackAnim.Pack8(isCritical ? (byte)2 : (byte)1);
    }
}
```

### Support Animation Stream (`pSupport`)
Updated `PvEBattleManager.cs` Phase 2:
Emits an authentic 19-byte block for each affected ally, guaranteeing `FUN_003ac7ac` is triggered for every team member in multi-target buffs, party shields, and party heals.

---

## 4. Verification & Testing

1. **Compilation**: Solution rebuilt with `dotnet build "Wonderland Private Server.sln"`: 0 warnings, 0 errors.
2. **Animation Trigger Verification**: `FUN_003abf3c` loops through all $N$ records in the single packet, executing `FUN_003ac7ac` for each target.
3. **Synchronization**: All hit targets simultaneously enter the hurt animation, display floating damage indicators, and persist until the death packet (`AC 53:3`) is broadcast after `animationDelay`.
