# Wonderland Online Server Management GUI Extended Tabs & Pet Battle/Ride Mode Architecture

## 1. Overview & System Scope

This technical document covers two major subsystem enhancements delivered to the server:
1. **Pet Battle vs Ride Conflict Resolution**: Eliminating race conditions and state corruption when a player owns multiple pets and toggles one to battle mode and another to ride/mount mode.
2. **GUI Management Parity Suite (7 Extended Admin Tabs)**: Implementation of full administrative and live game operations parity with the reference server (`gui_app.py`), directly embedded into `Src/Gui/MainForm1.ExtendedTabs.cs` with seamless WinForms integration.

---

## 2. Pet Battle vs Ride Conflict Architecture

### 2.1 Problem Analysis
In earlier revisions:
- When a player owned multiple pets, selecting one pet as combat companion (`IsBattle = true`) and mounting another pet (`IsRide = true`), the server combat manager fell back to picking the last modified pet (even if it was only a mount) or overwrote `ActivePetID` during mounting.
- During battle initialization (`PvEBattleManager.cs`), the selector used an overly permissive fallback (`p.PetList.FirstOrDefault()`) if `ActivePetID` was 0, erroneously deploying riding mounts into turn-based combat.
- In `Src/Network/ActionCodes/AC19.cs`, toggling battle mode destructively dismounted the player via an unconditioned `player.UnridePet()` call.

### 2.2 Resolution & Technical Specifications
- **`wlo.pserver.core/Game/Battle/PvEBattleManager.cs` (`GetActivePet`)**:
  - Signature: `private static Player.PlayerPetData GetActivePet(Player p)`
  - Logic: Strictly queries `p.PetList.FirstOrDefault(x => x.Slot == p.ActivePetID && x.IsBattle && x.HP > 0)` or `p.PetList.FirstOrDefault(x => x.IsBattle && x.HP > 0)`.
  - Edge Case: If no pet has `IsBattle == true` and positive HP, the method returns `null`, ensuring riding mounts are never dragged into combat.
- **`wlo.pserver.core/Game/Player.cs`**:
  - `PutPetToRide(byte slot)`: Mounts the target pet without altering `ActivePetID` or resetting battle state on companion pets.
  - `AddPetToPartyList(string petID)`: Retains existing combat companions without overriding `ActivePetID`.
- **`Src/Network/ActionCodes/AC19.cs`**:
  - Removed unconditioned `UnridePet()` calls when toggling pet battle statuses.
- **`wlo.pserver.core/Game/Maps/Map.cs`**:
  - Prevented automatic battle-flag assignment when `ActivePetID == 0`.

---

## 3. Extended Admin GUI Tabs Specification

All 7 extended tabs are declared in the partial class `Form1` inside [`Src/Gui/MainForm1.ExtendedTabs.cs`](file:///d:/GitHub/Wonderland-Private-Server/Src/Gui/MainForm1.ExtendedTabs.cs) and initialized in the `Form1()` constructor.

### 3.1 Tab 1: 👥 Online Sessions (`SetupOnlineSessionsTab`)
* **Purpose**: Real-time monitoring and administrative control of active player sessions.
* **UI Controls**:
  - `ext_dgvOnlineSessions`: DataGridView listing `CharID`, `Name`, `Account`, `Level`, `Gold`, `MapID`, `X`, `Y`, `IP Address`.
  - Filter bar: Real-time search by character name, account name, or Map ID.
  - Live GM Session Tools panel:
    - `🧙 Open Deep Character Editor`: Launches modal [`CharacterDataEditorForm`](file:///d:/GitHub/Wonderland-Private-Server/Src/Gui/CharacterDataEditorForm.cs) for full inventory, stats, quest flags, companion, and visibility manipulation.
    - `💚 Heal HP/SP to 100%`: Instantly sets `Eqs.CurHP = Eqs.FullHP` and `Eqs.CurSP = Eqs.FullSP`, broadcasts `AC 8 Sub 1` packet, and alerts the player.
    - `💰 Add 100,000 Gold`: Increases player gold, sends `AC 26 Sub 4` packet, and persists to database.
    - `🛡️ Toggle Invincible God Mode`: Sets HP/SP to 99,999 with stat synchronization.
    - `🚀 Teleport Player`: Prompts for `MapID`, `X`, `Y` and teleports via `CurMap.Teleport(TeleportType.CmD, ...)`.
    - `👢 Kick Selected Player`: Disconnects the socket cleanly via `player.Disconnect()`.
    - `⛔ Ban Player Account`: Writes record to `banned_users`, updates `users SET banned = 1`, and forces disconnect.
    - `🌐 Ban Player IP`: Writes record to `banned_ips` and terminates matching connections.

### 3.2 Tab 2: 🏰 Guilds (`SetupGuildsTab`)
* **Purpose**: Inspect and manage player guilds, leadership, rosters, and announcements.
* **Backend API (`wlo.pserver.core/Game/PlayerRelated/Guild.cs`)**:
  - `GuildManager.GetAllGuilds()`: Returns `IReadOnlyDictionary<ushort, Guild>`.
  - `GuildManager.AdminUpdateRules(ushort guildId, string rules)`: Updates notice and persists to database.
  - `GuildManager.AdminChangeLeader(ushort guildId, uint newLeaderCharId)`: Promotes a member to guild master.
  - `GuildManager.AdminKickMember(ushort guildId, uint memberCharId)`: Removes a player from the guild roster.
  - `GuildManager.AdminDisbandGuild(ushort guildId)`: Disbands guild and resets member associations.
* **UI Controls**:
  - `ext_dgvGuilds`: Guild overview (ID, Name, Leader, Member count, Creation Date).
  - Member Roster (`ext_dgvGuildMembers`): Real-time member levels, ranks, elements, and online/offline statuses.
  - Notice editor with `💾 Save Notice`.
  - Action buttons: `👑 Change Leader`, `👢 Kick Member`, `🗑️ Disband Guild`.

### 3.3 Tab 3: 📬 In-Game Mail (`SetupMailTab`)
* **Purpose**: Dispatch GM mails, system announcements, item rewards, and currency attachments; inspect mailbox history.
* **Backend API (`wlo.pserver.core/Game/PlayerRelated/Mail.cs`)**:
  - `MailSystem.GetAllMails()`: Returns `List<MailMessage>`.
  - `MailSystem.AdminDispatchMail(uint targetCharId, string senderName, string subject, string content, uint gold, ushort itemId, byte count)`: Emits mail into recipient inboxes and synchronizes in-game notifications (`AC 14`).
  - `MailSystem.AdminDeleteMail(uint mailId)`: Removes mail from database and memory.
* **UI Controls**:
  - Left panel: Target selector (`Single Character`, `All Online Players`, `All Registered Characters`), recipient lookup, subject, body, gold amount, item ID (with auto-lookup name preview via `Item.dat`), and quantity.
  - Right panel: Mail records table with `🔄 Refresh` and `🗑️ Delete Mail`.

### 3.4 Tab 4: 🛡️ Security & Bans (`SetupSecurityTab`)
* **Purpose**: IP and account ban management with SQLite persistence.
* **Database Schema**:
  - `banned_ips (ip TEXT PRIMARY KEY, reason TEXT, banned_at TEXT, banned_by TEXT)`
  - `banned_users (userID INT PRIMARY KEY, username TEXT, reason TEXT, banned_at TEXT, banned_by TEXT)`
* **UI Controls**:
  - Left split: Banned IP table with `➕ Add IP Ban` and `🔓 Unban Selected IP`.
  - Right split: Banned accounts table with `⛔ Ban Account` and `🔓 Unban Selected Account`.

### 3.5 Tab 5: ⚔️ Live Battles Monitor (`SetupLiveBattlesTab`)
* **Purpose**: Live monitor for active turn-based combat encounters (PvE and PvP) with recovery tools for stuck encounters.
* **Backend API (`wlo.pserver.core/Game/Battle/PvEBattleManager.cs`)**:
  - `PvEBattleManager.ActiveBattles`: Read-only snapshot of running encounters.
  - `PvEBattleManager.ForceWinBattle(uint battleId)`: Immediately forces victory, allocates standard drops/EXP, and returns combatants to the overworld.
  - `PvEBattleManager.ForceEndBattle(uint battleId)`: Aborts combat safely, clears battle states, and releases player input lock.
* **UI Controls**:
  - `ext_dgvBattles`: Battle list with Battle ID, Type, Map ID, Turn count, Player name, Pet name, Opponents.
  - `ext_txtBattleDetails`: Deep battle state inspector (Attacker stats, Pet stats, Monster grid coordinates & HP/SP, expected action counts).
  - Action buttons: `🏆 Force Win (Victory)` and `🛑 Force End / Abort Battle`.

### 3.6 Tab 6: 💍 Marriages (`SetupMarriagesTab`)
* **Purpose**: In-game marriage registry, administrative divorce, and spouse teleportation.
* **Backend API (`wlo.pserver.core/Game/PlayerRelated/MarriageManager.cs`)**:
  - `MarriageManager.GetAllMarriages()`: Returns `List<MarriageRecord>`.
  - `MarriageManager.AdminDivorce(uint charId)`: Dissolves marriage, clears DB records, and notifies online spouses.
* **UI Controls**:
  - `ext_dgvMarriages`: Husband ID/Name, Wife ID/Name, Marriage Date, Status.
  - Action buttons: `💔 Admin Annul / Divorce` and `🚀 Teleport Spouses Together`.

### 3.7 Tab 7: 🎁 Starter Items Pack (`SetupStarterItemsTab`)
* **Purpose**: Configure beginner item packages granted to newly registered or created characters.
* **Backend API (`wlo.pserver.core/Game/PlayerRelated/StarterPackManager.cs`)**:
  - Storage: `Data/starter_items.json` serialized via `System.Web.Script.Serialization.JavaScriptSerializer`.
  - `GetItems()`, `AddItem()`, `UpdateItem()`, `DeleteItem()`, `ExportJson()`, `ImportJson()`, `DeliverToPlayer(Player p)`.
* **UI Controls**:
  - `ext_dgvStarters`: Order, Item ID, Item Name, Quantity, Description.
  - Top toolbar: `🔄 Reload Starters`, `➕ Add Starter Item`, `✏️ Edit Selected`, `🗑️ Remove Item`, `📥 Import JSON`, `📤 Export JSON`.
  - Interactive double-click to edit rows.

---

## 4. Verification & Testing

- **Compilation**: Full solution compiled with `dotnet build "Wonderland Private Server.sln"` yielding **0 Errors**.
- **Pet State Isolation**: Verified that mounting a pet leaves companion combat flags intact, and setting a battle pet does not dismount vehicles or mounts.
- **Data Persistence**: Verified SQLite read/write operations for security bans and JSON serialization for starter packs.
