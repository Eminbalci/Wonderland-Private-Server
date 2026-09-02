# Friend List Online Status and Companion Pet Synchronization Fix

## 1. Summary of Changes
1. **Friend List Formatting (`AC 14:5`)**:
   - The official client already includes Cupid (`GM中心`) as a hardcoded default assistant in slot 0 on the client side. The server must NOT pack Cupid into the packet payload.
   - Restored the clean authentic 15-field structure:
     `[CharID (32), CharName (Pascal), Level (8), Reborn (8), Job (8), Element (8), Body (8), Head (8), HairColor (16), SkinColor (16), ClothingColor (16), EyeColor (16), NickName (Pascal), GuildName (Pascal), isOnline (8)]`
   - Fixed online status detection: Registered player into `cGlobal.gCharacterDataBase.OnCharacterJoin(src)` at the very beginning of `CommenceLogin` so friends queries and status notifications always reflect the current online state accurately.

2. **Robinson Companion Template ID**:
   - Robinson's authentic NPC template ID in `Npc.dat` is `12032` (not the script event ID `12178`).
   - All references normalized to `12032` so the client can load the sprite from `Npc.dat`.

---

## 2. Verification
- Solution compiled cleanly via `dotnet build "Wonderland Private Server.sln"` (0 Errors).
