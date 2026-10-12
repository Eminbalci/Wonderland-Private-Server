# Forge Enhancement and Equipment Potential Specification

## 1. Equipment Forge & Enhancement Engine

The client provides interactive interfaces for upgrading weapons and equipment:

* **UI Form:** `"form_NPCUpgrade"` (Specialized blacksmith & gear enhancer modal).
* **Upgrade States:**
  * `"Upgrade success"` (Stat boost applied, equipment tier increased).
  * `"Upgrade failed"` (Upgrade failure; potential durability damage).
  * `"Forge at max"` (Equipment has reached maximum possible enhancement tier).
* **Material & Eligibility Constraints:**
  * Only specific equipment types are forgeable: `"Forged items only"`, `"Can't forge this"`, `"Can't forge this Eq"`.
* **Token Usage Prompt:**
  * Modal confirmation text: `"You have %d tokens. Forge 1 time uses 1. Confirm?"`.

---

## 2. Companion Pet Potential System

Companion pets feature a distinct Potential progression separate from standard character levels:

* **Level Restriction Guard:** Companion potential upgrades cannot exceed player level:
  * Error Alert: `"Can't upgrade pet, Player LV must be higher"`.
* **Potential Cap:**
  * Notification: `"Reached max potential"`.
