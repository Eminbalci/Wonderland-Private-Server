# Wonderland Online Görev Oluşturma ve Mimari Kılavuzu

Sunucumuzda görevler **iki farklı yöntemle** çalışır ve oluşturulabilir:

---

### 1.1 Dinamik NPC & Görev Eşleşmesi (`QuestManager.FindQuestForPlayerNpc`)
- Tüm harita NPC'leri tıklandığında (`AC 20 Sub 1` / `AC 23 Sub 1`), `QuestManager` üzerinden otomatik olarak görev durumuna göre değerlendirilir:
  1. **Devam Eden Görevler (`InProgress`)**: Oyuncunun mevcut aşamasındaki hedef NPC (`TargetNpcPattern`, `TargetNpcTemplateID`) ile eşleşen görevler kontrol edilir.
  2. **Yeni Görevler (`NotStarted`)**: Önkoşul görev kontrolleri (`PrerequisiteQuestIDs`) tamamlanmışsa ve NPC adı/başlığı/TID'si görevle eşleşiyorsa görev oyuncuya kabul ettirilir (`AC 24 Sub 1`).
  3. **Tamamlanan Görevler (`Completed`)**: Görev tamamlandığında oyuncuya EXP, Gold ve eşyalar verilir, `charquest` tablosu güncellenir ve `AC 24 Sub 5` paketiyle F6 Görev Günlüğü güncellenir.
  4. **Köy ve Harita NPC Entegrasyonu**:
     - **Mary Lou (`TID 14013`)**: Orijinal `Talk.dat` ofseti (`0x212977`) + Görev 72 (*Pig Taking a Walk* / *Granny's Lost Pig*).
     - **Emilie (`TID 14140`)**: Görev 94 (*Emilie's Age*).
     - **Jack (`TID 14005`)**: Görev 234 (*Jack's Blue Flower*).
     - **Pigs (`TID 17400`)**: Görev 72 / 238 (*Pig's Nightmare*).
     - **Muhafızlar (`TID 14118`)**: Görev 96 (*Lost Iron Sword*).
     - **Roca (`TID 14161`)**: Görev 228 (*Undeserved Roca* / Yoldaş Alımı).dat`)**:
  - Oyuncu bir NPC'ye tıkladığında [`EveEventInterpreter.cs`](file:///d:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/Maps/Code/EveEventInterpreter.cs) devreye girer.
  - Seviye, istenen eşya, parti üyesi yoldaş gibi koşulları (`unknownbyte1 == 1..7`) değerlendirir ve `Opcode 1..12` ile ödülleri/savaşları başlatır.

---

## 2. Dinamik Veri Hattı ve Sıfır Hardcoding Mimarisi
Tüm NPC şablonları, diyalog ofsetleri ve görev dalları resmi binary veri dosyalarından dinamik olarak çözümlenir:

1. **NPC Şablon & İsim Çözümleme (`QuestManager.EnsureNpcCache`)**:
   - `Data/npc.json` (2,358 resmi NPC) ve `Data/Npc.dat` (5,000+ kayıt) belleğe dinamik olarak indekslenir (`_npcNameCache`, `_npcTidByName`).
   - `ResolveDefaultNpcTid` ve `ExtractNpcPattern` fonksiyonları tüm NPC adlarını ve Template ID'lerini bu dinamik sözlük üzerinden çözer.
   - Kod içerisinde hiçbir sabit NPC listesi veya hardcoded ID eşleşmesi bulunmaz.

2. **Dinamik 24-bit TalkID Çözümleme (`QuestNpc.ResolveTalkIdForNpc`)**:
   - `Eve.emg` dosyasındaki ilgili haritanın `Events` bytecode'u taranarak NPC'nin gerçek `DialogPtr == 2 && dialog2 == 1` opcodelarından 24-bit TalkID dinamik hesaplanır (`((uint)op.dialog1 << 16) | (uint)op.dialog3`).
   - Bu sayede oyundaki her haritadaki her NPC (Mary Lou, Emilie, Jack, Muhafızlar, Köylüler vb.) kendi özgün resmi diyaloğunu ve portresini görüntüler.

3. **Dinamik Diyalog Metni Çözümleme (`QuestNpc.ExtractDialogueFromTalkDat`)**:
   - `Data/Talk.dat` binary dosyası taranarak TalkID ofsetindeki ters çevrilmiş ASCII/Big5 dizgisi dinamik olarak çözümlenir ve sohbet penceresine yansıtılır.

---

## 3. Yöntem: C# Kod Bloğu ile Özel (Custom) Görev Oluşturma

Yeni veya özel bir görev eklemek istediğinizde [`QuestDefinition.cs`](file:///d:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/QuestRelated/QuestDefinition.cs) ve `QuestManager.RegisterQuest()` kullanılır.

### Örnek 1: Eşya Toplama Görevi (Item Collection)
```csharp
var quest = new QuestDefinition(
    questId: 5001,
    title: "Kayıp Hindistan Cevizleri",
    location: "Kelp Adası Sahili",
    type: QuestType.ItemCollection
)
{
    MapID = 10036,
    NpcNamePattern = "Robinson",
    RequiredLevel = 1,
    IntroDialogue = "Hey gezgin! Çok açım, bana sahilden 3 adet Hindistan Cevizi getirebilir misin?",
    InProgressDialogue = "Henüz 3 Hindistan Cevizini toplayamadın mı?",
    CompleteDialogue = "Harikasın! Karnımı doyurdun, al bu ödül senin!",
    AlreadyCompletedDialogue = "Yardımların için tekrar teşekkürler dostum!"
};

// İstenen Eşyalar: 3x Coconut (Item ID: 41066)
quest.RequiredItems.Add(new QuestRequirementItem(itemId: 41066, amount: 3, itemName: "Coconut"));

// Ödüller: 500 Altın, 1000 EXP, 1x Küçük Sağlık İksiri (Item ID: 10001)
quest.Reward = new QuestReward(gold: 500, exp: 1000)
    .AddItem(itemId: 10001, count: 1);

// Sunucuya Kaydet
QuestManager.RegisterQuest(quest);
```

---

### Örnek 2: Çok Aşamalı Zincir Görev (Multi-Stage Quest)
```csharp
var chainQuest = new QuestDefinition(
    questId: 5002,
    title: "Korsan Tehdidi",
    location: "Kuzey Adası",
    type: QuestType.Dialogue
);

// 1. Adım: Köy Muhtarıyla Konuş
var step1 = new QuestStep(stepIndex: 1, npcPattern: "Village Elder", stepType: QuestType.Dialogue)
{
    PromptDialogue = "Korsanlar limana yaklaşıyor, lütfen Deniz Fenerindeki Muhafız ile görüş!",
    CompleteDialogue = "Teşekkürler, hemen Deniz Fenerine git."
};
chainQuest.AddStep(step1);

// 2. Adım: Deniz Feneri Muhafızına 2x Demir Kılıç Teslim Et
var step2 = new QuestStep(stepIndex: 2, npcPattern: "Lighthouse Guard", stepType: QuestType.ItemCollection)
{
    PromptDialogue = "Korsanlarla savaşmak için 2 adet Demir Kılıca ihtiyacım var.",
    CompleteDialogue = "Silahlar harika, şimdi korsan liderini yenmeliyiz!"
};
step2.RequiredItems.Add(new QuestRequirementItem(itemId: 20001, amount: 2, itemName: "Iron Sword"));
chainQuest.AddStep(step2);

// 3. Adım: Korsan Liderini Yen (PvE Savaşı)
var step3 = new QuestStep(stepIndex: 3, npcPattern: "Pirate Captain", stepType: QuestType.MonsterDefeat)
{
    BattleMonsterID = 10050,
    BattleMonsterName = "Pirate Captain",
    PromptDialogue = "Beni asla yenemezsin velet!",
    CompleteDialogue = "Aman tanrım... Pes ediyorum!"
};
chainQuest.AddStep(step3);

// Final Ödülü: 2500 EXP, 2000 Altın ve Yoldaş Katılımı (Robinson)
chainQuest.Reward = new QuestReward(gold: 2000, exp: 2500, companionId: 12032, companionName: "Robinson");

// Sunucuya Kaydet
QuestManager.RegisterQuest(chainQuest);
```

---

## 3. Yöntem: SQLite / MySQL Veritabanından Görev Tanımlama
Görevler veritabanındaki `quest_data` tablosuna INSERT edilerek dinamik olarak sunucuya yüklenebilir:
* Tablo: `quest_data` (`id`, `map_id`, `title`, `description`, `type`, `npc_name`, `req_items`, `reward_gold`, `reward_exp`, `reward_items`, `companion_id`)
* Sunucu başladığında `QuestDataBase.LoadAllQuests()` bu kayıtları otomatik çeker.
