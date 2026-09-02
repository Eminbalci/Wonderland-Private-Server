# Character Deletion and Relational Data Cleanup Protocol

## Overview
This document specifies the database cascade cleanup protocol when deleting a character or creating a new character on a recycled slot/ID.

## Root Cause
* When a character was deleted (`AC 35:2` / `DeleteCharacter`), only `characters`, `stats`, and `inventory` rows were deleted.
* Relational tables (`character_pets`, `charquest`, `chartent`, `charunlocks`, `charactersExtData`, `Friends`) retained stale records mapped to the deleted `charID` (e.g. `10001`).
* When creating a new character that received the same `charID`, the server loaded the old character's companions and completed quest states upon login.

## Implementation Details

### 1. Atomic Cascade Deletion (`CharacterDataBase.DeleteCharacter`)
* When `DeleteCharacter(charID)` is triggered, all relational tables are wiped atomically:
  * `character_pets`
  * `charquest`
  * `chartent`
  * `charunlocks`
  * `inventory`
  * `stats`
  * `charactersExtData`
  * `Friends`
  * `characters`

### 2. New Character Safety Clean (`CharacterDataBase.WriteNewPlayer`)
* At the beginning of `WriteNewPlayer`, all child tables matching `charID` are explicitly wiped to guarantee a completely fresh character state with 0 companions, 0 quest flags, and clean base inventories.
