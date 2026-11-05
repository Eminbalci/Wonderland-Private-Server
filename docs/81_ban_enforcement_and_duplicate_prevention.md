# 81 - Ban Enforcement and Duplicate Ban Prevention Specification

## 1. Executive Summary & Problem Analysis

In the previous server implementation:
1. Banned accounts and IP addresses were able to connect to the socket listener and authenticate through `LoginServer.cs` and `AC63.cs` without restriction because `LoginServer.ListenThread` did not verify IP against the `banned_ips` table, and `AC63.Recv4` did not evaluate `banned_users` or `users.banned == 1`.
2. The administration GUI and command system allowed duplicate bans of already-banned players or IP addresses without warning or prevention, triggering redundant database writes and potential operator confusion.

This document outlines the security architecture implemented to enforce immediate rejection of banned IPs at the transport level, packet-level rejection (`bb 1 7`) of banned user accounts, and strict duplicate ban prevention across all GUI and chat administration entry points.

---

## 2. Architecture & Implementation Details

### 2.1 Transport-Level IP Rejection (`LoginServer.cs`)

When any TCP socket connection arrives on port 6414:
- The server extracts the client remote endpoint IP using `client.IPAddr` or `client.SockAddress()`.
- The helper `public bool IsIpBanned(string ip)` performs a query against `banned_ips` in `cGlobal.gUserDataBase` and `cGlobal.gGameDataBase`:
  - Strips any port information (handling both raw IP and `IP:Port` formats).
  - Sanitizes SQL string literals (`ip.Replace("'", "''")`).
  - Queries exact match: `SELECT 1 FROM banned_ips WHERE ip = '{safeIp}' LIMIT 1;`
  - Queries prefix match: `SELECT 1 FROM banned_ips WHERE ip LIKE '{safeIp}:%' LIMIT 1;`
- If banned, `client.Disconnect(); continue;` is immediately executed. The socket is terminated before the client is attached to `ClientList` or forwarded to `OnNewPlayer`.

### 2.2 Protocol Authentication Ban Verification (`AC63.cs`)

In `AC63.Recv4(ref Player p, RecievePacket r)`:
1. **IP Verification**: Client remote IP is tested against `LoginServer.IsIpBanned(clientIp)` and `GmManager.IsIpBanned(clientIp)`. If banned, `loginState = 5`.
2. **Account Verification**: Prior to and immediately after credential lookup:
   - `GmManager.IsAccountBanned(name)` verifies if `name` exists in `banned_users` or has `users.banned == 1`.
   - After `GetUserData`, `GmManager.IsAccountBanned(name, dbid)` re-validates by both username and resolved numeric `userID`. If banned, `loginState = 5`.
3. **Client Packet & Disconnect Dispatch (`case 5:`)**:
   - Dispatches native client "Account Banned" packet: `p.Send(Tools.FromFormat("bb", 1, 7));`
   - Immediately invokes `p.Disconnect();` inside a protected try-catch block.
   - Prevents banned players from ever receiving character list packets or entering the game world.

### 2.3 Centralized Security API & Duplicate Prevention (`GmManager.cs`)

Four core security management methods are introduced to `Game.PlayerRelated.GmManager`:

1. `public static bool IsIpBanned(string ip)`
   - Checks `banned_ips` table in SQLite/MySQL.
   - Returns `true` if banned, `false` otherwise.
2. `public static bool IsAccountBanned(string username, uint userId = 0)`
   - Checks `users` table for `banned == 1`.
   - Checks `banned_users` table by username or userID.
   - Returns `true` if banned, `false` otherwise.
3. `public static bool BanAccount(string username, string reason, out string statusMsg)`
   - Validates username and resolves `userId`.
   - **Duplicate Check**: Invokes `IsAccountBanned(username, userId)`. If already banned, returns `false` with `statusMsg = "Account '<user>' is already banned!"`.
   - Inserts into `banned_users (userID, username, reason, banned_at, banned_by)`.
   - Updates `users SET banned = 1`.
   - Broadcasts `Tools.FromFormat("bb", 1, 7)` to any active online sessions matching username or userId, then disconnects them.
4. `public static bool UnbanAccount(string username, out string statusMsg)`
   - Validates username.
   - Checks if currently banned. If not banned, returns `false` with `statusMsg = "Account '<user>' is not banned."`.
   - Removes entry from `banned_users` and sets `users.banned = 0`.

### 2.4 In-Game GM Chat Commands (`AC02.cs`)

Under Action Code 2 (Chat & GM Commands):
- `:ban <usernameOrCharacterName> [reason]` (aliases `/ban`):
  - Validates GM privileges (`GmManager.IsGm(p)`).
  - Calls `GmManager.BanAccount`.
  - Disconnects online player if logged in, delivers official client banned notification, and sends confirmation or rejection message to GM.
- `:unban <username>` (aliases `/unban`):
  - Validates GM privileges.
  - Calls `GmManager.UnbanAccount`.
  - Restores account access and notifies GM.
- Added to `:help` and `:cmds` administrative command output.

### 2.5 Administration GUI Duplicate Prevention (`MainForm1.ExtendedTabs.cs`)

- `BanAccountInternal(string username, uint userId, string reason)`:
  - Queries `banned_users` and `users.banned == 1`.
  - If already banned: displays warning popup `MessageBox.Show($"Account '{username}' is already banned!", "Notice", MessageBoxButtons.OK, MessageBoxIcon.Warning);` and immediately aborts re-banning.
- `BanIpInternal(string ip, string reason)`:
  - Cleans input IP (strips port).
  - Queries `banned_ips`.
  - If already banned: displays warning popup `MessageBox.Show($"IP '{ip}' is already banned!", "Notice", MessageBoxButtons.OK, MessageBoxIcon.Warning);` and immediately aborts re-banning.
- Clean IP extraction updated in `btnBanIp.Click`: extracts `ext_selectedOnlinePlayer.IPAddr?.ToString() ?? ext_selectedOnlinePlayer.SockAddress()?.Split(':')[0]`.

---

## 3. Database Schema

Tables created automatically upon server startup in `UserDataBase.VerifySetup()` and GUI tab initialization:

```sql
CREATE TABLE IF NOT EXISTS banned_ips (
    ip TEXT PRIMARY KEY,
    reason TEXT,
    banned_at TEXT,
    banned_by TEXT
);

CREATE TABLE IF NOT EXISTS banned_users (
    userID INT PRIMARY KEY,
    username TEXT,
    reason TEXT,
    banned_at TEXT,
    banned_by TEXT
);

ALTER TABLE users ADD COLUMN banned INTEGER DEFAULT 0;
```

---

## 4. Verification Evidence

PowerShell reflection tests verified:
- `IsIpBanned("192.168.1.100")` -> initially `False`, after insert `True`.
- `IsIpBanned("192.168.1.100:54321")` (with port) -> `True`.
- `BanAccount("testbanneduser", "Test Violation")` -> `True` (`statusMsg = "Account 'testbanneduser' (ID: 99999) has been banned"`).
- `IsAccountBanned("testbanneduser", 99999)` -> `True`.
- Duplicate Ban attempt: `BanAccount("testbanneduser", "Second Violation Attempt")` -> `False` (`statusMsg = "Account 'testbanneduser' is already banned!"`).
- `UnbanAccount("testbanneduser")` -> `True`.
- `IsAccountBanned` after unban -> `False`.
- Solution compilation: 0 errors, 0 warnings across all projects.
