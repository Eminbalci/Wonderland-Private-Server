# Client Trade, Battle, Map & Server Decompiled Specifications

This document covers trade mechanics, battle system, map/minimap infrastructure, weather rendering, server/channel architecture, login/character management, chat channels, GM tools, and anti-cheat strings extracted from `aLogin.exe.1.c`.

---

## 1. Trade System

### A. Trade Flow
- `"Trade"` — Trade panel tab label.
- `"Trade complete"` — Successful trade confirmation.
- `"Trade canceled"` / `"Other player canceled trade"` — Cancellation alerts.
- `"Trade failed"` — Server-side trade failure.
- `"Trade is locked"` — Trade lock (security feature).
- `"Unlock to trade items"` — Trade unlock requirement.

### B. Trade Restrictions
- `"Can't trade now"` / `"Unable to trade"` / `"Can't trade here"` — State/location blocks.
- `"Can't have few trades"` — Concurrent trade limit.
- `"Can't trade, Amity is 0"` — Pet amity requirement for pet trades.
- `"Can't trade, the same pet in possession"` / `"Can't trade when possess the same pets"` — Duplicate pet prevention.
- `"Can't trade Mercenaries"` — Mercenary NPCs are untradeable.
- `"Use Pet Cage to trade"` — Pets require cage item for trading.
- `"Pet food has been used. Can't trade"` — Fed pets become non-tradeable.
- `"Item [%s] is non-tradeable. Can't be used"` — Dynamic non-tradeable item alert.
- `"<Non-tradeable>"` — Non-tradeable label tag on items.
- `"Can't be traded or gifted"` — Absolute trade lock.
- `"Item won't be tradeable"` — Post-forge tradeability warning.
- `"Pet will be non-tradeable. Confirm?"` / `"Reward will be non-tradeable. Continue?"` — Confirmation dialogs.
- `"Players on diff maps"` — Map proximity requirement for trade.
- `"Player disconnected"` — Trade aborted due to disconnect.

### C. Safe Trade & Exchange UI
- `"In a Secure Trade"` — Secure trade mode active.
- `"TradeLeftItem"` — Player's trade slot grid identifier.
- `"OtherSafeTradeItem"` / `"MySafeTradeItem"` — Safe trade item grids.
- `"Form_ExchangeLockChang"` — Exchange lock settings form.
- `"Btn_ExchangeLockSet"` / `"Btn_Exchange_1"` / `"Btn_Exchange_2"` — Exchange UI buttons.
- `"Form_ActivityExchange"` — Activity/event exchange form.
- `"Coins exchanged"` — Currency exchange confirmation.
- `"Exchange doll for?"` — Doll item exchange prompt.
- `"Props\nTransfer\nExchange"` — Transfer/exchange action menu options.

---

## 2. Battle System

### A. Battle States
- `"Can't in battle"` / `"Can't use in battle"` / `"Can't do in battle!"` — Generic battle-state action blocks.
- `"Can't fix in battle"` / `"Can't remove in battle"` / `"Can't be set in battle"` — Specific in-battle restrictions.
- `"Cant use in fights"` — Alt phrasing for fight state block.
- `"Target is in battle"` — Target player is currently fighting.
- `"Pet in battle!"` / `"Pet in Battle state"` — Pet combat state indicators.
- `"Battle is over"` — Fight completed.
- `"Battle time run out!"` — Turn timer expired.
- `"Battle not cleared, HP is 0"` — Unresolved battle with dead character.
- `"Max battles per event"` — Event battle count limit reached.

### B. Battle Skills & Win Plate
- `"Form_BattleSkill_1"` — Battle skill selection panel.
- `"btn_AssignFight_1"` — Fight assignment button.
- `"Skill only for battle"` / `"Can use only in battle"` — Battle-only skill restrictions.
- `"Use Win Plate to win this battle?"` — Win Plate (instant-win item) confirmation dialog.
- `"Escape"` — Flee/escape battle action label.

### C. Battle Data Parsing
- `"Read Battle."` — Battle data file loader.
- `"Read Battle.MemberAry[Left]"` — Left side (player team) member array parser.
- `"Read Battle.MemberAry[Right]"` — Right side (enemy team) member array parser.
- `"Error: FightManage.SupportRoleInfo"` — Battle support role error handler.

### D. Battle Events
- `"Battle Royale has begun. Players LV10+ can go to Capitol Building 4F in Welling Village"` — Battle Royale start.
- `"Battle Royale will end in 10 minutes!"` — 10-min warning.
- `"Battle Royale has ended."` — Event end.
- `"Battle Royale: Tuesday 7 PM to 8 PM"` — Weekly schedule.
- `"Battle Royale suspended"` — Temporary suspension.
- `"Trojan War has begun. Go to the Castle and join the battle!"` — Castle siege event.

### E. Battle Configuration
- `"BattleSwitch"` — Config key toggling battle mode (read/write via `FUN_000230f0` / `FUN_000237b8`).

---

## 3. Map & Minimap System

### A. Minimap Assets
- `"MiniMap_1"` through `"MiniMap_5"` — 5 minimap tile/layer assets.
- `"MiniMapForm"` — Minimap display form (dimensions: 0x90 × 0xE2 = 144 × 226 px).
- `"Expand Map"` — Map expansion toggle button.
- `"Reload Mini-map"` — Minimap refresh button.

### B. Map Data Files
- Directory: `"user\\Map\\"` — Local map data cache directory.
- Extension: `".MapData"` — Map data file extension.
- `"Enter Map ID"` — GM/debug map jump prompt.
- `"Map is full, try later"` — Map capacity reached.
- `"Too many players on map, animations reduced"` — Performance throttling when map is crowded.

---

## 4. Weather System

### A. Weather Effect Assets
- `"icon_Snow1"` / `"icon_Snow2"` / `"icon_Snow3"` — 3 snow particle layers.
- `"icon_Rains"` — Rain particle effect asset.

---

## 5. Server & Channel Architecture

### A. Server Config
- `"SERVER.INI"` — Server configuration file read at startup.
- `"Unnamed Server"` — Default/fallback server name.
- `"Server is busy"` — Server overload alert.
- `"Server will shutdown in 5 minutes"` — Pre-shutdown warning broadcast.
- `"Version= "` — Client version display string.
- `"Mainupdate.exe"` / `"Update.EXE"` — Patcher/updater executables.

### B. Channel System
- `"Channel: "` — Channel display label.
- `"btn_channel_"` — Dynamic channel selection button prefix.
- `"icon_channel"` — Channel icon asset prefix with appended index.
- `"Both must be on same server channel"` — Same-channel requirement for certain interactions.

### C. Channel Color Configuration
5 configurable channel color slots, each read/written via config:
- `"ChannelColor1"` through `"ChannelColor5"` — RGB color values per chat channel.
- `"MsgChannel"` — Message channel identifier in config.

---

## 6. Login & Character Management

### A. Login UI
- `"Btn_Login_L"` — Login button asset.
- `"Icon_LoginLogo_1"` — Login screen logo.
- `"Icon_RecordAccount"` — Remember account checkbox/icon.
- `"Login/Pwd error"` — Authentication failure.
- `"Can't reach login server"` — Connection failure.
- `"Security Lock will be active on next login"` — Security lock activation notice.

### B. Character Creation & Deletion
- `"Btn_CreateCharacter"` — Character creation button.
- `"Btn_DeleteCharacter_1"` — Character deletion button.
- `"Delete character?"` — Character deletion confirmation dialog.
- `"Max 10 characters"` — Maximum character slot limit: **10**.
- `"Can't delete char on 4-PVP event server"` — Deletion blocked during PVP events.
- `"Can't use second character on 4-PVP event server!"` — Single-character restriction on event servers.

### C. Logout
- `"Btn_Logout_1"` — Logout button asset.
- `"Player disconnected"` — Disconnect notification.

---

## 7. GM / Admin Tools

- `"GM Chat"` — GM chat channel label.
- `"GM banned you from Local for 8s"` — GM-issued 8-second local chat mute.
- `"GM closed chat"` / `"GM Chat disabled"` — GM chat shutdown.
- `"GM can't log 2nd char"` — GMs restricted to single character.

---

## 8. Chat Channels (Extended)

- `"World"` — World chat channel label.
- `"World channel is Off"` — World channel disabled notification.
- `"Local channel is Off"` — Local channel disabled.
- `"Whisper channel is Off"` — Whisper channel disabled.
- `"Team channel is Off"` — Team channel disabled.
- `"(System):World Channel requires Radio Set"` — World chat requires Radio Set item.

---

## 9. Anti-Cheat & Moderation

- `"Don't use Illegal words"` — Profanity/banned word filter alert (appears 3+ times).
- `"Name contains illegal words"` — Character/pet name filter.
- `"Forbidden: "` — Forbidden action prefix.
- `"Forbidden before LV25"` — Level gate for certain features.
- `"Icon_Forbid_2"` — Forbidden action icon asset.
- `"House is forbidden to enter!"` — Private tent/house access denied.
- `"panel_restrainINN_2"` — Restriction panel for INN (possible bot/cheat prevention).

---

## 10. Repair System

- `"Repairs cost: "` — Repair cost display.
- `"Vehicle repaired"` — Vehicle repair success.
- `"Repairs failed"` — Repair failure.
- `"Can't be repaired"` — Item unrepairable.
- `"Doesn't need repair"` — Item at full durability.
- `"Item selected for repair"` — Repair target confirmation.

---

## 11. Interserver PVP (Extended Detail)

Full lifecycle of cross-server PVP events:
1. `"Interserver PVP begins in 5 minutes. Players LV30+ can go to..."` — Pre-event announcement.
2. `"Interserver PVP has begun!"` — Event start (multiple phrasing variants).
3. `"Interserver PVP will end in 5 minutes. Last chance to score!"` — Pre-end warning.
4. `"Interserver PVP has ended."` — Event conclusion.
5. `"Interserver PVP has ended. Log out and return to your server for the results"` — Post-event instruction.
6. `"Interserver PVP Ranks updated! You can check leaderboard"` — Rank sync.
7. `"Return to your server"` — Post-event return prompt.
8. `"No teams in Interserver PVP"` — No team formation.
9. `"Interserver PVP resumed"` — Event resume after pause.
10. `"Unavailable at Interserver"` / `"Can't use at Interserver"` — Feature restrictions during interserver.

---

## 12. Miscellaneous

- `"Bottle full"` — Collectible bottle item at capacity.
- `"In troubled world, act anonymously"` — Anonymous mode tooltip.
- `"Events Updated"` — Server event list refresh.
- `"Mall updated"` — Item mall catalog refresh.
- `"Item list updated"` — Generic item list refresh.
- `"No pet to train"` / `"No training object"` — Pet training prerequisites.
- `"Can't replace, pet in training"` — Pet swap blocked during training.
- `"JXAN version mismatch (need Ver=2 for JmpWhole)"` — Animation file version validator.
