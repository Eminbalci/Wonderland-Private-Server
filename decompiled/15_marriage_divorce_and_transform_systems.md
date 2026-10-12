# Marriage, Divorce, and Transform Systems Specification

## 1. Marriage & Divorce System

The client provides dedicated ceremony and legal state handling for in-game marriages:

### 1.1 Marriage Ceremony Lifecycle
* **Eligibility Checks:**
  * Must apply while in a 2-player party: `"Have to apply in party"`.
  * Bride must equip formal wedding attire: `"Bride needs to dress up"`.
  * Any active morph must be cleared: `"Turn off transform"`.
* **Ceremony Initiation:**
  * System Toast: `"Marriage matched in heaven for the lifetime."`
  * Ceremony Oath: `"Hold hands and make an oath in front of God, relatives and friends."`
  * Banner Alert: `"Marriage ceremony has begun"`.
* **Expiration Timeout:**
  * Ceremony must be held within 3 days of booking: `"Marriage canceled, no ceremony in 3 days"`.

### 1.2 Divorce Restrictions & Cooldowns
* **Execution:** System confirmation `"Divorced"` / `"Divorce commited"`.
* **Remarriage Cooldown:** Enforces a **7-day cooldown period** before either party can initiate a new marriage:
  * Error Alert: `"Divorced less than 7 days ago"`.

---

## 2. Character Morph & Transformation System

The client supports temporary character shape-shifting / transformations:

* **State Indicators:** `"Transform"`, `"Transformed"`, `"Untransform"`, `", untransform?"`.
* **Duration Expiry:** Automatic state reversion on timeout: `"Transformation time out"`.
* **Action Blocking Rules:**
  * Cannot ride mounts or vehicles while transformed: `"Can't ride morphed"`.
  * Cannot enter marriage ceremony while morphed: `"Turn off transform"`.
  * General item and skill restrictions: `"Cant use when transformed"`.
