# PvE Battle System & Wild Random Encounters

## 1. Encounter Triggers
- **Click Protection (`AC 11:2`):** Clicking directly on wild roaming monsters on the map does not trigger combat.
- **Proximity Detection (`AC 06:1`):** Moving/walking within **180 distance units** of roaming wild monsters on the map automatically initiates battle with that monster group.
- **Step-Based Encounter (`AC 06:1`):** Walking in wild zones accumulates step count (18-35 steps) and triggers map-based random encounters.

## 2. Multi-Monster Battle Formations
- Encounters dynamically spawn **1 to 4 monsters** in authentic formation slots (`EnemyGridSlots`):
  - Front Center `(2,2)`
  - Front Right `(2,3)`
  - Front Left `(2,1)`
  - Front Far Right `(2,4)`
  - Back Center `(1,2)`
  - Back Right `(1,3)`
  - Back Left `(1,1)`
  - Back Far Right `(1,4)`
- Monsters within the same group spawn with authentic level variances (+/- 1) and full HP/SP scaling.
