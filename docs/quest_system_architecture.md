# Wonderland Online Master Görev Mimarisi ve İkili Katman Senkronizasyonu

Bu dokümanda, sunucu görev motorunun ([`QuestManager.cs`](file:///d:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/QuestRelated/QuestManager.cs)) ve veri yapısının ([`QuestDefinition.cs`](file:///d:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/QuestRelated/QuestDefinition.cs)) Fandom Wiki standartlarına uygun olarak nasıl **Master Görevler (Ana Görevler)** halinde yapılandırıldığı açıklanmaktadır.

---

## 1. Mimari Tasarım: İkili Katman Modeli (Dual-Layer Model)

Resmi Wonderland Online oyun motorunda `Mark.dat` dosyası içerisinde **2.154 adet ham kayıt (Mark ID)** bulunur. Bunun nedeni, her görevin aşamalarının ayrı bayraklar olarak tutulmasıdır:

```
[İstemci / F6 Görev Günlüğü & GUI]
           │
           ▼
┌──────────────────────────────────────────────────────────┐
│             Master Quest (Ana Görev Yapısı)             │
│   Örn: "Undeserved Roca" (Master ID: 228, Kat: Yoldaş)   │
└────────────┬─────────────────────────────────┬───────────┘
             │                                 │
             ▼                                 ▼
   [In-Progress Mark #228]           [Completed Mark #229]
   - AC 24:1 / 24:2 gönderilir.      - AC 24:5 gönderilir.
   - PreEvent NPC görünürlüğü açılır. - Ödüller verilir (AC 15:1 / 26).
```

---

## 2. Master Görev Kategorileri

Tüm 2.154 ham kayıt analiz edilerek 5 ana kategori altında toplanmıştır:

1. **👥 Companion & Rebirth (`👥 Yoldaş & Reenkarnasyon Görevleri`)**:
   - Roca, Niss, Clive, Xaolan, Sam, Shizune, Elin, Victoria, Robinson, Angela, Suzuru, Eva vb.
   - Yoldaş gizli yetenek öğrenme (`Skill Master`) ve ölüm/dirilme görevleri.
2. **🛠️ Crafting & Vehicles (`🛠️ Üretim & Araç Görevleri`)**:
   - Sal (`Raft`), Kano (`Canoe`), Buharlı Gemi (`Ship`), Uçak (`Airplane`), Roket, UFO, Uzay Çadırı ve Simya (`Alchemy`) görevleri.
3. **🐉 Dungeons & Instances (`🐉 Zindan & Meydan Okuma Görevleri`)**:
   - 12 Burç (`12 Zodiacs`), 20/40 Round savaşları, Ejderha Sarayı (`Dragon Palace`), Hayalet Gemi (`Ghost Ship`), Ayı Mağarası (`Cave Bear`).
4. **🎯 Minigames & Challenges (`🎯 Mini-Oyunlar & Süreli Toplama Görevleri`)**:
   - Köstebek Vurmaca (`Whack-A-Mole`), Matematik Testi, Balıkçılık Yarışması, Süreli Maden ve Odun toplama meydan okumaları.
5. **🏝️ Storyline & Area (`🏝️ Bölge & Ana Hikaye Görevleri`)**:
   - North Island, South Island, Holy Village, Welling Village, Kelp Island, China, Japan, Egypt, Maya, Persia, Rome, Athens vb.

---

## 3. GUI Yönetim Arayüzü Özellikleri (`Quest DB Manager Studio`)

[`MainForm1.cs`](file:///d:/GitHub/Wonderland-Private-Server/Src/Gui/MainForm1.cs) üzerinden yöneticiler görevleri çok sekmeli (Multi-Tab) profesyonel bir stüdyo ortamında yönetir:

- **Sol Panel (Filtreleme & Liste)**:
  - **Kategori Filtresi (`📂 Cat`)**: Yalnızca seçilen kategorideki Master Görevleri (1.078 adet) veya ham kayıtları (2.154 Mark ID) listeler.
  - **Arama Kutusu (`🔍 Find`)**: Başlık, bölge, NPC veya ID'ye göre anlık arama yapar.
  - **Sayaç Bilgisi**: Toplam filtrelenen görev sayısını gösterir.

- **Sağ Panel (5 Sekmeli Görev Stüdyosu)**:
  1. **📋 Overview & Prerequisites (Genel & Ön Koşullar)**:
     - Görev açıklaması ve hikaye arka planı (`Description`).
     - Başlangıç haritası (`Start Map ID`), NPC Şablon ID'si (`TID`) ve NPC isim örüntüsü.
     - Görev türü (Diyalog, Eşya Toplama, Canavar Savaşı, Yoldaş Edinme vb.).
     - **Gereken Ön Görevler (`Prerequisite Quests`)**: Oyuncunun bu görevi alabilmesi için daha önce bitirmiş olması gereken görevlerin Mark ID listesi (Örn: `1864, 52`).
     - **Gereken İlk Eşyalar (`Required Items`)**: Göreve başlarken veya teslimde istenen eşyalar (Örn: `32005x1, 41066x2`).
  2. **👣 Multi-Stage Steps & Objectives (Görev Aşamaları & Hedefleri)**:
     - Görevin çok aşamalı ilerleme tablosu (`dgvQuestSteps`).
     - Her aşama için: Aşama türü, hedef NPC/Canavar, aşama talimat metni/diyalogu, aşamada istenen ve verilen eşyalar.
     - `➕ Add Step`, `➖ Delete Step`, `✔️ Update Step` butonları ile aşama ekleme/çıkarma.
  3. **💬 Story Dialogues & Script (Diyaloglar & Metinler)**:
     - `🟢 Accept / Intro Dialogue`: Görev kabul edilirken söylenen metin.
     - `🟡 In-Progress Dialogue`: Görev devam ederken NPC ile konuşulduğunda söylenen metin.
     - `🔵 Completion Dialogue`: Görev başarıyla teslim edildiğinde söylenen metin.
     - `⚪ Already Completed Dialogue`: Görev zaten tamamlanmışken NPC ile konuşulduğunda söylenen metin.
  4. **🎁 Rewards & Boss Battles (Ödüller & Canavarlar)**:
     - 💰 Gold & ⭐ EXP ödülleri.
     - 👥 Yoldaş kazanımı (`CompanionPetID` & `CompanionName`).
     - ⚔️ Görev sonu patron/canavar savaşı (`BattleMonsterID` & `BattleMonsterName`).
     - 🎒 Eşya ödülleri (`RewardItems`, Örn: `32001x2, 48016x1`).
  5. **🧪 Live Player Dispatcher (Canlı Oyuncu Test & Yönetim)**:
     - Çevrimiçi bağlı oyuncu seçimi (`Select Online Player`).
     - `▶️ Start Quest (AC 24:1)`: Oyuncuya görevi başlatır, In-Progress bayrağını ve harita PreEvent tetikleyicilerini açar.
     - `⏭️ Advance Step`: Oyuncuyu sonraki görev aşamasına ilerletir.
     - `🏆 Complete Quest (AC 24:5)`: Görevi tamamlar, ödülleri verir ve F6 Görev Günlüğüne yeşil onay işareti ekler.
     - `🔄 Reset Quest Flags`: Test amacıyla oyuncunun görev bayraklarını sıfırlar.
