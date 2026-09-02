# GM & Administrator Authorization System

## 1. Overview
The GM Authorization System restricts all cheat and administrative in-game chat commands (`T>`, `I>`, `P>`, `PR>`, `R>`, `/reborn`, `/palace`) exclusively to accounts and characters authorized in the GM list.

---

## 2. Server GUI Management ([MainForm1.cs](file:///d:/GitHub/Wonderland-Private-Server/Src/Gui/MainForm1.cs))
- **"👑 GM Management" Tab:**
  - Visual List of all authorized GMs.
  - Text input to add new GM character names or account usernames (`➕ Add to GM List`).
  - Removal button to revoke GM permissions (`➖ Remove Selected GM`).
  - Real-time updates without restarting the server (`OnGmListChanged` event).

---

## 3. Data Architecture ([GmManager.cs](file:///d:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/PlayerRelated/GmManager.cs))
- **`GmManager`**: In-memory `HashSet<string>` with case-insensitive validation.
- **`Data/gm_list.txt`**: Persists GM accounts and character names across restarts.
- **Permission Enforcement ([Player.cs](file:///d:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/Player.cs)):**
  - Non-GM players attempting admin commands receive `"You do not have GM privileges to execute cheat commands."` and execution is blocked.
