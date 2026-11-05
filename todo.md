# Todo — Guidepost ve Cansız Nesnelerin Dönmesini Engelleme (Non-Rotating Actors)

- [x] 1. C# Sunucusu (`wlo.pserver.core\Game\Maps\Code\EveEventInterpreter.cs`):
  - `IsNonRotatingActor(GameMap map, ushort clickId)` tespit yardımcısı eklendi (Guidepost 19020..19024, tabelalar, heykeller, yazıtlar, sandıklar, prop ve nesne türleri Type 6, 8, 9, 10).
  - Opcode 1, Opcode 2 ve `BuildDialoguePacket` içerisinde cansız nesneler için `talkMode = 2` (preserves facing direction) olarak paketlendi (canlı NPC'ler için `1`).
  - `EveEventRuntime.cs` diyalog akışında (`BuildEventFrame`) `talkMode = 2` entegre edildi.
- [x] 2. Web İstemcisi (`D:\GitHub\wloweb\Wonderland-Gonline\client\wlo\world\npcs.go`):
  - `FaceConversation` fonksiyonunda `kindUnnamedA` (8 - Guidepost / Inanimate), `kindObject` (9), `kindObjectB` (10) ve Template 19020..19024 nesnelerinin konuşmada yön değiştirmesi engellendi.
  - Web client wasm (`D:\GitHub\wloweb\site\client.wasm`) yeniden derlendi.
- [x] 3. C# Çözümü MSBuild ile derlendi (0 hata, 0 uyarı).
- [x] 4. PowerShell üzerinden `IsNonRotatingActor` doğrulaması başarıyla tamamlandı (Guidepost 19020 -> True, Scroll 19025 -> True, Breillat 10000 -> False).
- [x] 5. Teknik dokümantasyon (`docs/77_guidepost_and_inanimate_actor_rotation_prevention.md`) ve `README.md` güncellendi.
