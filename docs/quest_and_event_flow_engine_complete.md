# Wonderland Online Tam Entegre Görev & Olay Akış Motoru

## 1. Genel Bakış
Sunucu motoru, resmi `eve.Emg` (145,254 bytecode opcode'u), `Talk.dat` (13,763 diyalog), `Mark.dat` (2,154 görev), `Npc.dat` ve `Item.dat` veri kaynaklarını temel alan **otonom, dinamik bir olay ve görev yürütme motoru** haline getirilmiştir.

---

## 2. 7 Temel Olay Akışının Entegrasyonu

1. **NPC Tıklama & Diyalog Akışı (`AC 20:1 Type 1 & Type 6`)**:
   - `EveEventInterpreter.cs` ve `QuestNpc.cs` üzerinden NPC tıklandığında TalkID ve portreler yüklenir. Soru-cevap seçenekleri `unknownbyte1 == 7` dallarına dinamik olarak yönlendirilir.
2. **Yer Sandıkları & Gizli Eşyalar (`Item / ItemsinMap` — 47,370 Adet)**:
   - Sandık tıklamasında `AC 22:1` açılma animasyonu oynatılır ve `Opcode 1` ile eşya/altın verilir.
3. **Maden & Doğal Kaynak Toplama (`Mine / MiningAreas` — 47,569 Adet)**:
   - Madencilik, odunculuk ve balıkçılık alanları araç gereç döngüsüyle (`AC 23:2`) işletilir.
4. **Zemin Tuzakları & Gizli Basamaklar (`Trap / InteractiveInfo` — 34,363 Adet)**:
   - [`AC06.cs`](file:///d:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC06.cs) hareket motoru, oyuncunun haritada bastığı koordinatı `InteractiveInfo` zemin tetikleyicileriyle eşleştirir ve `EveEventInterpreter.TryExecute` ile tetikler.
5. **Koşullu Işınlanma Kapıları (`Door / WarpInfo` — 89,724 Adet)**:
   - Harita portalları geometrik ters eşleşme ve anahtar/görev kontrolleriyle geçiş sağlar.
6. **Harita Ön-Olay Değerlendirmesi (`PreEvent` — 41,340 Adet)**:
   - Haritaya girildiğinde (`Map.Warp_In`) `QuestManager.SendAllQuestFlags` ve `SyncPerPlayerNpcVisibility` ile oyuncunun hikaye ilerlemesine göre NPC'ler anında gizlenir veya gösterilir.
7. **Çadır İçi Mobilya & Üretim İstasyonları (`Opcode 17` / `AC 50`)**:
   - Marangoz masası, dikiş makinesi, kimya ocağı ve mutfak tezgahı arayüzleri tetiklenir.
