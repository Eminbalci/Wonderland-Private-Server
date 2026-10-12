# Complete GM Studio and CY_GmForm Specification

## 1. Executive Summary & Binary Identification

The hidden Game Master administration suite embedded within [`aLogin.exe`](file:///D:/GitHub/ghidradecodeyapma/WLRI/aLogin.exe) is registered under unit name `CY_GmForm` and class name `TGmForm`.

* **Resource Type:** `RT_RCDATA`
* **Resource Byte Offset:** `5361508` (`0x0051ce74`)
* **Resource Size:** 12,784 Bytes
* **Published Method Table Offset:** `1206000` (`0x00126a70`)
* **Class Constructor / Destructor:** `FormCreate` (`0x00137a60`) / `FormDestroy` (`0x00137da0`)

---

## 2. Exhaustive GM Action Button Catalog (56 Buttons)

Below is the complete table mapping each button, published method pointer, original Big5 Chinese caption, English translation, and verified operational function:

| Button | Method RVA | Caption (Big5) | English Translation | Operational Description & Protocol Action |
|---|---|---|---|---|
| `Button1` | `0x00137a48` | `留言板` | Blackboard | Opens server-wide player blackboard message inspector |
| `Button2` | `0x00137a54` | `Refresh` | Refresh | Refreshes active online player grid (`StringGrid1`) |
| `Button3` | `0x00137a58` | `Delete` | Delete | Removes selected player entry from administrative watchlists |
| `Button4` | `0x00137a5c` | `禁水桶` | Forum Mute | Imposes community posting ban / timeout on target account |
| `Button5` | `0x00137a64` | `查玩家` | Inspect Player | Queries player character details (level, gold, stats, location) |
| `Button6` | `0x00137a68` | `改名` | Rename Player | Forces character rename on target account |
| `Button7` | `0x00137b24` | `全頻` | Global Broadcast | Dispatches announcement text across all server channels |
| `Button8` | `0x00137b2c` | `關人` | Imprison Player | Warps target player to GM Jail (`Scene 10001`) |
| `Button9` | `0x00137b30` | `抓玩家` | Summon Player | Pulls target player directly to GM's current spatial coordinates |
| `Button10` | `0x00137b58` | `斷線` | Kick Player | Soft-disconnects target player TCP socket session |
| `Button11` | `0x00137b5c` | `查位置` | Look up Location | Retrieves target character's map ID, X coordinate, and Y coordinate |
| `Button12` | `0x00137b60` | `依名字查ID` | Lookup ID by Name | Resolves account ID and character ID from character name string |
| `Button13` | `0x00137b64` | `放人` | Release Player | Releases target player from GM Jail to default revival spawn |
| `Button14` | `0x00137b68` | `密頻` | Whisper | Sends private GM administrative message to target player |
| `Button15` | `0x00137b6c` | `鎖單一角色` | Lock Character | Freezes target character slot without locking entire account |
| `Button16` | `0x00137b70` | `關名單` | Jailed List | Displays active registry of players currently confined to Jail |
| `Button17` | `0x00137b74` | `觀戰` | Spectate Battle | Infiltrates target's turn-based combat instance as observer |
| `Button18` | `0x00137b78` | `解單一角色` | Unlock Character | Unlocks and restores frozen character slot |
| `Button19` | `0x00137b7c` | `鎖單一名單` | Banlist | Displays list of individually banned character accounts |
| `Button20` | `0x00137b80` | `外掛場` | Botting Arena | Warps suspected automated botters to isolated holding pen |
| `Button21` | `0x00137b84` | `強斷線` | Force Kick | Executes immediate hard socket close without client handshake |
| `Button22` | `0x00137b88` | `黑名單` | Blacklist | Manages persistent server-side IP and account blacklist |
| `Button23` | `0x00137b8c` | `Photo` | Screenshot | Captures client screen buffer and writes to `Photo\` folder |
| `Button24` | `0x00137b90` | `列留言` | List Messages | Dumps recent messages from public town bulletin boards |
| `Button25` | `0x00137b94` | `廣播` | Server Notice | Broadcasts colored notice text to top banner |
| `Button26` | `0x00137b98` | `戴耳機` | Eavesdrop Mode | Toggles GM monitoring mode to read all local channel chat |
| `Button27` | `0x00137b9c` | `更換團徽` | Change Emblem | Forces replacement of organization/guild emblem bitmap |
| `Button28` | `0x00137ba0` | `解禁團徽` | Unban Emblem | Restores custom guild emblem upload privileges |
| `Button29` | `0x00137bc8` | `選擇角色` | Select Character | Selects active character slot (Character 1 / Character 2) |
| `Button30` | `0x00137bcc` | `鎖帳號` | Ban Account | Sets persistent ban flag on entire player account |
| `Button31` | `0x00137bd0` | `解帳號` | Unban Account | Removes account ban flag from persistence layer |
| `Button32` | `0x00137bd4` | `查分流` | Inspect Channel | Checks subchannel (branch ID) and server cluster status |
| `Button33` | `0x00137bd8` | `禁言` | Mute Player | Blocks player from broadcasting chat across all channels |
| `Button34` | `0x00137bdc` | `解禁言` | Unmute Player | Removes chat mute restriction from target player |
| `Button35` | `0x00137be0` | `禁名單` | Muted List | Displays table of all currently muted player IDs |
| `Button36` | `0x00137be4` | `解禁水桶` | Forum Unmute | Removes bulletin board posting restriction |
| `Button37` | `0x00137bec` | `問問題` | Anti-Bot Quiz | Dispatches modal anti-bot question prompt to player |
| `Button38` | `0x00137c14` | `到玩家` | Teleport to Player | Teleports GM directly to target player coordinates |
| `Button39` | `0x00137c18` | `信件` | Mailbox | Opens GM mail management console |
| `Button40` | `0x00137c1c` | `回信內容` | Reply Content | Composes and sends administrative in-game mail reply |
| `Button41` | `0x00137c20` | `傳送` | Send Mail | Dispatches system mail with optional item attachments |
| `Button42` | `0x00137c24` | `清除` | Clear | Clears active log memo and diagnostic text buffers |
| `Button43` | `0x00137c28` | `出生地` | Spawn Point | Modifies default character respawn location |
| `Button44` | `0x00137c2c` | `查分流` | Branch Status | Queries active cluster connection load |
| `Button45` | `0x00138174` | `查玩家IP` | Check Player IP | Displays remote client IPv4 address and ISP routing info |
| `Button46` | `0x00137c30` | `改組織名` | Rename Guild | Forces rename on organization/guild entity |
| `Button47` | `0x00137f60` | `瞬移` | Coordinate Warp | Warps GM to explicit `SceneNum`, `X`, and `Y` coordinates |
| `Button48` | `0x00137fd0` | `OK` | Confirm Warp | Commits coordinate warp execution |
| `Button49` | `0x00138064` | `Close Form` | Close Form | Closes active sub-dialog |
| `Button50` | `0x00138068` | `GM中心` | GM Center | Displays central server diagnostic overview |
| `Button51` | `0x0013806c` | `在線名單` | Online Roster | Enumerates all connected players in current cluster |
| `Button52` | `0x00138070` | `所有頻道` | All Channels | Monitors text transmission across all 6 chat channels |
| `Button53` | `0x00137c00` | `Search` | Search Player | Queries character census database |
| `Button54` | `0x00138178` | `Clear` | Clear Filters | Clears search keyword and ID filters |
| `Button55` | `0x00138180` | `Photo Dir` | Photo Directory | Opens local `Photo\` directory in Windows Explorer |
| `Button56` | `0x0013817c` | `Login Drifter` | Login Drifter | Developer authentication bypass for unregistered testing |

---

## 3. Specialized Developer Routines

### 3.1 Storage Lock Recovery (`GMGetSaveLockPwdClick` @ `0x00138190`)
* **Purpose:** Queries and decrypts the target account's secondary security lock PIN.
* **Mechanism:** Queries character record field `SaveLockPwd` from server session state, decrypting the PIN and displaying it in GM console `Edit` field.

### 3.2 Anti-Bot Quiz Keys (`Ans1` .. `Ans4` @ `0x00137c00`)
* **Purpose:** Displays the 4 potential answer choices and marks the correct answer key for the anti-bot verification challenge sent to suspected macro users.

### 3.3 Continuous Surveillance Handlers
* **`CheckBox1` (`連續觀戰`):** Automatically injects GM client into every battle instance initiated by target player.
* **`CheckBox2` (`連續跟蹤`):** Automatically transitions GM through map scene warps whenever target player moves between scenes.

---

## 4. Channel Privilege Bitmask Specification

`TGmForm` includes a bitmask editor configuring transmission channel privileges:

```
+-----------+---------------+-----------------------------------------------+
| Bit Value | Channel Name  | Scope                                         |
+-----------+---------------+-----------------------------------------------+
| 1 (0x01)  | 全 (Local)    | Map-wide local proximity chat                 |
| 2 (0x02)  | 輕 (Whisper)  | Direct private whisper messages               |
| 3 (0x04)  | 密 (Secret)   | Private encrypted channel                     |
| 4 (0x08)  | 隊 (Party)    | Active party / team chat                      |
| 5 (0x10)  | 組 (Guild)    | Organization / guild chat                     |
| 6 (0x20)  | 盟 (Alliance) | Multi-guild cooperative alliance chat         |
| 7 (0x64)  | GM (Admin)    | Privileged Game Master administrative channel |
+-----------+---------------+-----------------------------------------------+
```
