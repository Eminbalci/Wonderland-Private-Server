# 77. Guidepost ve Cansız Aktörlerin Dönmesini Engelleme (TalkMode Parity)

## 1. Mimari Genel Bakış
Wonderland Online (WLO) ve `wloweb` mimarisinde haritadaki aktörlerle (NPC'ler) etkileşime girildiğinde (AC 20:1 / talkFrame), canlı karakterlerin oyuncuya dönmesi (turn towards player), cansız nesnelerin (Guidepost/yol tabelası, anıt, heykel, taş, sandık, fıçı) ise authored/sabit yönlerini korumaları gerekir.

## 2. Ağ Protokolü ve TalkMode Analizi
WLO AC 20:1 çerçeve yapısı:
```
Byte 0: Command (20)
Byte 1: SubCode (1)
Byte 2-4: Session Padding (0, 0, 0)
Byte 5: Step / EventFrameKind
Byte 6: Type / EventKindTalk (1)
Byte 7: Subject / Speaker (3 = NPC, 7 = Player)
Byte 8-9: Actor ClickID (uint16 LE)
Byte 10: Mode / Flags (1 = npcTalkTurn, 2 = preserve facing direction)
Byte 11-13: Padding / Value (0, 0, 0)
Byte 14-16: 24-bit Talk ID (Little Endian: LSB, MID, MSB)
```

- **Mode 1 (`npcTalkTurn`):** İstemci, konuşan aktörü oyuncunun baktığı koordinatlara doğru döndürür (`FaceConversation`).
- **Mode 2 (Preserve Facing):** İstemci oyuncuyu aktöre doğru döndürür fakat aktörün yönünü değiştirmez.

## 3. C# Sunucu Tarafı Değişiklikleri (`wlo.pserver.core`)
### `EveEventInterpreter.cs` ve `EveEventRuntime.cs`
- `IsNonRotatingActor(GameMap map, ushort clickId)` tespit fonksiyonu entegre edildi:
  - Otantik Guidepost şablonları: 19020..19024
  - Cansız prop/nesne şablonları: 19025 (Scroll), 19034 (Cask), 19035 / 16006 (Treasure Chest), 19037 / 19038 (Springboard), 19039 (Coconut Node)
  - Npc.dat Type 6 (Prop), Type 8 (Inanimate/Guidepost), Type 9 (Object), Type 10 (Object)
  - İsim filtreleri: `guidepost`, `signpost`, `tabela`, `levha`, `road sign`, `notice board`, `monument`, `statue`, `cask`, `chest`, `barrel`, `board`
- Opcode 1 ve Opcode 2 diyalog paketlerinde `talkMode = IsNonRotatingActor(...) ? (byte)2 : (byte)1` kuralı uygulandı.
- `BuildEventFrame` ve `BuildDialoguePacket` fonksiyonlarında `talkMode` dinamik hale getirildi.

## 4. Web İstemcisi Değişiklikleri (`Wonderland-Gonline` / `wloweb`)
### `client/wlo/world/npcs.go`
- `FaceConversation` metodu güncellendi:
  - `kindProp` (6) yanında `kindUnnamedA` (8 - Guidepost) ve Template 19020..19024 nesnelerinin konuşma esnasında yön değiştirmesi kütüphane seviyesinde de engellendi.
  - Web client WebAssembly ikili dosyası (`D:\GitHub\wloweb\site\client.wasm`) güncellendi.
