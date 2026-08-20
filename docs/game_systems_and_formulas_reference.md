# Comprehensive Reference: Game Systems, Mechanics & Formulas

## 1. Core Attribute & Derived Stat Formulas

### 1.1 Base and Maximum Vitality Pools
$$\text{Max HP} = \text{BaseHP} + (\text{CON} \times 2) + (\text{AvatarCON} \times 2) + \text{EquipHP}$$
$$\text{Max SP} = \text{BaseSP} + (\text{WIS} \times 2) + (\text{AvatarWIS} \times 2) + \text{EquipSP}$$

### 1.2 Combat Attack & Defense Ratings
$$\text{ATK (Physical Attack)} = (\text{STR} \times 2) + \text{WeaponATK} + \text{AvatarSTR}$$
$$\text{DEF (Physical Defense)} = (\text{CON} \times 2) + \text{ArmorDEF} + \text{AvatarCON}$$
$$\text{MATK (Magic Attack)} = (\text{INT} \times 2) + \text{WeaponMATK} + \text{AvatarINT}$$
$$\text{MDEF (Magic Defense)} = (\text{WIS} \times 2) + \text{ArmorMDEF} + \text{AvatarWIS}$$
$$\text{SPD (Turn Speed)} = \text{AGI} + \text{EquipSPD} + \text{AvatarAGI} + \text{SaddleBonus}$$

---

## 2. Elemental Affinity System

### 2.1 Cycle & Multipliers
* **Advantage Cycle:** Earth $\rightarrow$ Water $\rightarrow$ Fire $\rightarrow$ Wind $\rightarrow$ Earth
* **Advantage Factor ($E_{\text{mod}}$):** $+30\%$ Bonus ($\times 1.30$), $-30\%$ incoming damage ($\times 0.70$).
* **Disadvantage Factor ($E_{\text{mod}}$):** $-30\%$ Penalty ($\times 0.70$), $+30\%$ incoming damage ($\times 1.30$).
* **Neutral / Identical:** $\times 1.00$.

---

## 3. Combat & Damage Equations

### 3.1 Physical & Magical Damage
$$\text{Physical Damage} = \max\Big(1, \big((\text{ATK} \times \text{SkillMultiplier}) - \text{TargetDEF}\big) \times E_{\text{mod}} \times \text{CritMultiplier}\Big)$$
$$\text{Magical Damage} = \max\Big(1, \big((\text{MATK} \times \text{SkillMultiplier}) - \text{TargetMDEF}\big) \times E_{\text{mod}} \times \text{CritMultiplier}\Big)$$
* $\text{Critical Hit Chance} = 5\% + \max(0, (\text{AttackerAGI} - \text{TargetAGI}) \times 0.1\%)$
* $\text{Critical Hit Multiplier} = 2.0\times$

### 3.2 Turn Order & Combo Attacks
* **Turn Order:** Sorted strictly descending by total $\text{SPD}$.
* **Combo Trigger Condition:**
  1. Ally players/pets target the exact same monster.
  2. $|\text{SPD}_{\text{Ally A}} - \text{SPD}_{\text{Ally B}}| \le 25$.
  3. $\text{AveragePartySPD} \ge \text{MonsterSPD}$.
* **Combo Multiplier:** $1.2\times$ (2 players), $1.35\times$ (3 players), $1.5\times$ (4 players).

### 3.3 Status Sealing Probability
$$\text{Seal Success Rate} = \text{BaseSkillRate} + (\text{CasterWIS} - \text{TargetWIS}) \times 0.5\% \quad (\text{Clamped: } [10\%, 90\%])$$

---

## 4. Experience & Leveling Progression

### 4.1 Experience Curve
$$\text{Required EXP}(\text{Level}) = \Big\lfloor 10 \times \text{Level}^{2.8} + 50 \times \text{Level} \Big\rfloor$$

### 4.2 Monster Level Gap Penalty
* If $|\text{MonsterLv} - \text{PlayerLv}| \le 19$: $100\%$ Base EXP.
* If $\text{MonsterLv} - \text{PlayerLv} > 19$: Penalty scaled down to $10\%$.
* If $\text{PlayerLv} - \text{MonsterLv} > 19$: Penalty scaled down to $0\%$.

---

## 5. Companion & Pet Amity (Loyalty)

* **Amity Range:** $0 \dots 100$.
* **Modifiers:**
  * Level up: $+1$ Amity.
  * Tent interaction / Feeding: $+1$ to $+5$ Amity.
  * Death in combat: $-1$ to $-3$ Amity.
  * If $\text{Amity} < 20$: Pet has a chance to flee battle or refuse commands.

---

## 6. Vehicle Speeds & Movement Delays

| Vehicle ID | Name | Step Delay | Water / Air Clearance |
| :--- | :--- | :--- | :--- |
| `0` | Walking | 200 ms | Ground Only |
| `48010` / `48016` | Raft | 200 ms | Water Traversable |
| `48011` | Canoe | 160 ms | Water Traversable |
| `48012` | Sailboat | 130 ms | Water Traversable |
| `48013` | Steamship | 100 ms | Water Traversable |
| `48014` | Airship | 80 ms | All Terrains & Obstacles |

---

## 7. Alchemy & Compound Crafting

$$\text{Result Rank} = \Big\lfloor \frac{\text{Item}_1\text{ Rank} + \text{Item}_2\text{ Rank}}{2} \Big\rfloor + \text{AlchemyLevel}$$
$$\text{Success Rate} = \text{Clamp}\Big(100 - (\text{TargetRank} - \text{ResultRank}) \times 15,\; 5\%,\; 95\%\Big)$$
