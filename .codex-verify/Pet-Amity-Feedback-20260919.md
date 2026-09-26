# Pet amity and equipment notice box - 2026-09-19

- Equipment stat deltas now use AC2:16 (sender DWORD 0 + raw text), not GM/system chat AC2:4. Current native client dispatch 0x2dfd44 extracts raw text and invokes the message box at global 0x4c9e40 for 2000 ms.
- Item use AC23:15 recognizes item DAT status64 and forwards the selected session pet slot. Tao Rice Ball 34014 grants5; jelly items 34096/34097/34098 grant20/26/32; 34119/34120/34121 grant10/20/40; 34126 grants10.
- PetAmityManager validates the selected pet, item lock and quantity, caps amity100, consumes only as many items as necessary, sends native AC8:2 collection4/stat64 absolute value, successful consumption sound AC23:15, and actual gain in AC2:16 box; saves the character.
- Native pet stat handler 0x416ebc dispatches stat64 to 0x41742e, which assigns the byte at pet+0x2009.
- Legacy /feed shares the same item DAT and state mutation; no longer consumes arbitrary items or treats Star30025 as Rice Ball.
- Generic system chat remains available for other callers. No character database migration and no Item Mall point refund included.

Validation: staged Compile succeeded with existing CS1998 warnings only; Test-PetAmityFeedback.ps1 passed792 inventory/pet/message checks (existing inventory coverage plus eight real amity item types, client/database slot mismatch, cap100, overrequest, full amity, wrong target, locked item, command feed, exact box packets and no equipment GM chat). Native protocol and fixture checks are not live gameplay proof; client replay pending.
