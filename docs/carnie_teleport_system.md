# Carnie Island Teleportation & Return Portal Specification

## 1. Overview
Carnie Island (Map ID `11094`) serves as the central carnival and amusement hub. When players teleport to Carnie, their previous map and coordinates are remembered. Exiting through the portal on Map 11094 returns the player to their exact origin location.

## 2. Teleportation Protocol
- **Destination**: Map `11094`, Coordinates `(X: 1180, Y: 875)`.
- **Triggers**:
  - `AC 5 Sub 17` with `destChoice = 3` (Carnie option).
  - Chat Command: `:carnie` / `/carnie`.
- **Origin Tracking**:
  - `Player.CarnieReturnMap` stores `(DstMap = CurMap.MapID, DstX_Axis = CurX, DstY_Axis = CurY)`.

## 3. Exit Portal Handling ([Map.cs](file:///d:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/Maps/Map.cs))
- When `MapID == 11094` in `Teleport(TeleportType.Regular, ...)`:
  - If `sender.CarnieReturnMap != null`: Warps player back to `sender.CarnieReturnMap`.
  - Fallback (if no saved origin): Warps to starter beach `(Map 11016, X: 1181, Y: 243)`.
