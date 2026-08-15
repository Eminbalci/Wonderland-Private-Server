# Client Game Systems Decompiled Specifications (Extended)

This document outlines previously undocumented client-side game system strings, state validations, and UI form references extracted from deep scanning of `aLogin.exe.1.c` (491,495 lines).

---

## 1. Stall / Vending System

The client implements a player-driven marketplace via portable stalls:

### A. State Blocking Rules
Opening a stall locks the player out of most other actions. The client enforces these blocks with the following alerts:
- `"Stall is being used"` / `"Stall is in use"` — Generic busy state.
- `"Close Stall first"` — Must close stall before initiating combat/trade/tent.
- `"Cancel Stall to fish"` — Cannot fish with an active stall.
- `"Can't PK Stall user"` — PK is blocked against stall users.
- `"Can't mount with Stall"` — Cannot ride while stall is active.
- `"Can't set more than 1 Stall"` — Only one stall per player.
- `"Unlock to set a Stall"` — Item unlock required.
- `"Owner closed Stall"` — Notifies buyer when seller closes.

### B. UI Forms
- `"Form_BuyItemStall_1"` — Item purchase panel from another player's stall.
- `"Form_BuyPet1Stall_1"` / `"Form_BuyPet2Stall_1"` / `"Form_BuyPet3Stall_1"` — Pet purchase panels (3 slots).
- `"StallUtenSil"` / `"BeStallUtenSil"` — Stall item grid identifiers.
- `"UtensilStallMemo"` / `"UtensilStalllPanel"` — Stall title/memo text fields.

---

## 2. Marriage & Divorce System

### A. Marriage Ceremony Flow
- `"Marriage matched in heaven for the lifetime."` — System toast on marriage match.
- `"Hold hands and make an oath in front of God, relatives and friends."` — Ceremony confirmation prompt.
- `"Marriage ceremony has begun"` — Ceremony starts.
- `"Bride needs to dress up"` — Bride must equip wedding dress.
- `"Have to apply in party"` — Must be in a party to marry.
- `"Marriage canceled, no ceremony in 3 days"` — Auto-cancellation if not completed within 3 days.

### B. Divorce
- `"Divorced"` / `"Divorce commited"` — Server confirmation of divorce action.
- `"Divorced less than 7 days ago"` — 7-day cooldown before remarriage.

---

## 3. Guild System

### A. Guild Management
- `"Guild: "` — Prefix label for guild name display.
- `"Disband your Guild?"` — Confirmation prompt for guild dissolution.
- `"Want to join guild"` — Join request notification.
- `"Guild is full"` / `"Too many Guilds"` — Capacity restrictions.
- `"You left Guild"` / `"Leaves Guild"` — Departure notifications.
- `"Join Guild first"` — Required for guild-only actions.
- `"Wrong Guild name"` — Invalid name input during creation.
- `"No guild allies"` / `"No guild own Castle"` — Feature unavailability alerts.
- `"Only same guild can join"` / `"Only same guilds team"` — Guild-exclusive team restrictions.
- `"Guild chat is off"` / `"(System):Not in Guild Chat anymore"` — Chat channel muting notifications.

---

## 4. Mount / Ride System

### A. Mount State Blocking
- `"Can't ride morphed"` — Cannot mount when transformed.
- `"Can't mount when in transport"` — Blocked during warp animations.
- `"Can't mount in bath"` — Hot Spring state blocks mounting.
- `"Can't mount with Stall"` — Stall state blocks mounting.
- `"Can't mount now"` / `"Can't mount"` — Generic denials.
- `"Pet is mounted!"` — Alert when pet is already riding.
- `"Mount on and off!"` — Toggle notification.
- `"Can't use mounted"` / `"Can't use when mounted"` — Action blocked while riding.

### B. Ride Position Config
- File path: `"\\Data\\AdjustRidePetPos.txt"` — External text file controlling ride sprite offsets.

---

## 5. Transform (Morph) System

- `"Transform"` / `"Transformed"` — Activation state labels.
- `"Untransform"` / `", untransform?"` — Reversal prompts.
- `"Can't transform"` — Denied due to invalid conditions.
- `"Transformation time out"` — Duration expiry alert.
- `"Don't Transform"` — Cancellation response.
- `"Turn off transform"` — Required before marriage ceremony.
- `"Cant use when transformed"` — Blocks certain actions.

---

## 6. Forge & Upgrade System

- `"Upgrade"` / `"Upgrade success"` / `"Upgrade failed"` — Equipment upgrade states.
- `"Can't upgrade pet, Player LV must be higher"` — Level gating for pet upgrades.
- `"Reached max potential"` — Cap reached.
- `"Forged items only"` / `"Can't forge this"` / `"Can't forge this Eq"` — Material restrictions.
- `"Forge at max"` — Maximum forge level reached.
- `"form_NPCUpgrade"` — NPC equipment upgrade UI form.
- Token forge prompt: `"You have %d tokens. Forge 1 time uses 1. Confirm?"`

---

## 7. Bank / ATM System

- `"BankItem"` — Bank storage item grid identifier.
- `"btn_Bank_Gold_L"` / `"btn_Bank_Gold_R"` — Gold deposit/withdraw directional buttons.
- `"icon_withDraw"` — Withdraw icon asset.
- `"btn_weaponBank"` — Weapon bank storage button.
- `"ATM deposit limit 400M"` — Maximum gold deposit cap of **400,000,000**.

---

## 8. Fishing System

- `"Currently fishing"` / `"Already fishing"` — Duplicate action prevention.
- `"Stop to start fishing"` — Must be stationary.
- `"Fishing, can't act"` / `"Fishing, can't use"` / `"Can't do while fishing"` — Generic fishing state blocks.
- `"Fishing, can't use tent"` — Cannot open tent while fishing.
- `"Cancel Stall to fish"` — Stall conflict.
- `"Fishing special event has begun!"` / `"Fishing special event has ended!"` — Seasonal event hooks.

---

## 9. Hot Spring (Bathing) System

- `"Hot Spring Pack time: "` — Timer display prefix.
- `"Hot Spring time at max!"` — Maximum bathing duration reached.
- `"Hot Spring ended"` — Session completion notification.
- `"Don't litter In Hot Springs!"` — Item drop prevention inside bath maps.

---

## 10. Tent & Storage System

- `"Tent is open"` — Tent visit notification.
- `"Tent is open to visit!"` — Public tent broadcast.
- `"No tent space"` — Storage capacity full.
- `"Added to tent storage"` — Item successfully stored.
- `"Placed in tent claim area"` — Territory placement.
- `"Can't open, too many items in inv or tent"` — Overflow prevention.
- `"Can't recycle, storage full"` — Recycle action blocked.
- `"Fishing, can't use tent"` / `"Collecting, can't use tent"` — State conflicts.

---

## 11. Friend & Blacklist System

- `"Friend List"` / `"Friends"` — UI panel labels.
- `"Delete this friend?"` — Removal confirmation dialog.
- `"Can't add to friends"` / `"Already your friend"` — Duplicate/limit prevention.
- `"left friends"` — Friend removal notification.
- `"Blacklisted by target"` / `"You're blacklisted by target player"` — Blacklist denial alerts.
- `"You've been blacklisted"` — Self notification.
- `"] to Blacklist"` / `"] unblacklisted"` — Dynamic add/remove messages.

---

## 12. Mail System

- `"form_Mail"` / `"form_Mailplus"` — Mail compose and extended mail forms.
- `"btn_sendmail_3"` — Send mail button asset.
- `"icon_mail_1"` / `"icon_mailGary_1"` — Read/unread mail icons.
- `"icon_anim_Gotmail_1"` — Animated new mail notification.
- `"Btn_ArmyMail"` — Guild/army mail button.
- `"Mailbox volume at 90%. When full, old letters get deleted"` — Storage warning.
- `"Mailbox is empty"` / `"Mailbox emptied"` — Empty state labels.
- File paths: `"\\MailBoxMsgLog"`, `"\\MsgMailBoxLog"`, `"\\MailData.dat"`, `"\\MsgFriendLog"` — Local mail cache files.

---

## 13. Seasonal Events System (Extended)

- `"Anniversary begun! Double EXP, mini-games, and Forges 50% Off!"`
- `"Happy May has begun! Double EXP, mini-games, and Forges 50% Off"`
- `"Sailing has begun! Double EXP, mini-games, and Forges 50% Off!"`
- `"National Day begun! Double EXP, mini-games, and Forges 50% Off!"`
- `"Happy Summer has begun! Forges are 50% Off!"`
- `"Forge 60 pts discount event has begun!"`
- `"Competition has ended! Don't forget to exchange rewards!"`

---

## 14. Skill Forget System

- `"You will forget learned skills. Select skill to forget:"` — Skill reset prompt.
- `"Skill to forget: "` — Selection label.
