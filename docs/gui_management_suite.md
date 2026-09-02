# Wonderland Online Sunucu Yönetici Arayüzü (GUI) & Görev / Olay Sistemleri Sekmeleri

Sunucu GUI arayüzüne ([`MainForm1.cs`](file:///d:/GitHub/Wonderland-Private-Server/Src/Gui/MainForm1.cs)), tüm görevleri ve 7 olay sistemini anlık olarak izleyip yönetebileceğiniz **iki kapsamlı yönetim sekmesi** eklenmiştir:

---

## 1. 📜 Görev Yöneticisi Sekmesi (`Quest DB Manager`)
* **Canlı Görev Arama ve Listeleme**:
  - `Mark.dat` ve SQLite veritabanındaki **2.154 resmi görevin** tamamını anlık filtreler.
  - Görev ID, başlık, bölge, NPC deseni, ödül altın/EXP, eşya ve yoldaş ödüllerini listeler.
* **Gelişmiş Görev Editörü**:
  - Çok adımlı aşamaları (`Steps JSON`), giriş, ilerleme ve bitiş diyaloglarını düzenleme imkanı.
  - Tek tıkla `💾 Save / Update Quest`, `➕ New Quest`, `🗑️ Delete Quest`.
  - `🔄 Reset & Import quests.json`: Orijinal resmi görevleri tek tıkla sıfırlayıp yeniden yükleme.

---

## 2. 🗺️ 7 Olay Sistemi ve Harita Tetikleyicileri Sekmesi (`7 Event Systems & Triggers`)
Tüm `eve.Emg` veri tablosunu (1.119 Harita, 145.254 Opcode) harita bazında incelemeyi ve canlı test etmeyi sağlar:

### 2.1 Harita Bazlı 6 Alt Izgara (Sub-Grids):
1. **🪤 Zemin Tuzakları & Basamaklar (`Floor Traps & Triggers` — 34.363 Adet)**:
   - Basılan koordinat (`X, Y`), tetikleyici genişlik/yükseklik (`W, H`) ve çalıştırılacak Olay Kimliği (`EntryID`).
2. **🎭 Ön-Olaylar & Görünürlük (`Storyline PreEvents` — 41.340 Adet)**:
   - Harita yüklendiğinde görev adımına göre NPC gizleme/gösterme bayrakları.
3. **⛏️ Maden & Doğal Kaynaklar (`Mining / Gathering Nodes` — 47.569 Adet)**:
   - Kaynak alanlarının koordinatları ve gereken alet tipleri (Kazma, Olta vb.).
4. **🚪 Işınlanma Kapıları (`Doors & Portals` — 89.724 Adet)**:
   - Çıkış kapısı koordinatları ve hedef harita/koordinat eşleşmeleri.
5. **📦 Sandıklar & Yer Eşyaları (`Chests & Ground Items` — 47.370 Adet)**:
   - Haritadaki sandıkların konumları ve içerdikleri Eşya ID'leri.
6. **👤 NPC & Canavar Listesi (`NPCs & Monsters` — 22.171 Adet)**:
   - Haritadaki tüm NPC ve yaratıkların Template ID, isim, konum ve bağlı Eve betik sayıları.

### 2.2 Canlı Test & Oyuncu Yönetim Araçları (Live Tester):
* **⚡ Canlı Olay Tetikleme (`Execute Event Now`)**:
  - Seçilen çevrimiçi oyuncuda herhangi bir Eve Olayını / NPC tıklamasını anında sunucu tarafında çalıştırır.
* **🔄 Görev Bayraklarını Senkronize Et (`Sync Quest Flags - AC 24`)**:
  - Canlı oyuncunun görev günlüğünü ve `PreEvent` tetikleyici bayraklarını client'a anında yansıtır.
* **🚀 Haritaya Işınla (`Warp to Selected Map`)**:
  - Oyuncuyu incelenen haritaya anında ışınlar.
