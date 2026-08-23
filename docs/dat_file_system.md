# .dat Dosya Sistemi — WLO Private Server

## Genel Mimari

```
Data/
├── Npc.dat       → Binary NPC şablonları (138 byte/kayıt, XOR şifrelenmiş)
├── Talk.dat      → Diyalog metinleri (byte offset indexleme, metinler tersine yazılmış)
├── Item.dat      → Item şablonları (binary struct)
├── Mark.dat      → Quest verileri (binary struct, PhxMarkDat ile parse)
├── Skill.dat     → Skill verileri
├── SceneData.dat → Map sahne bilgileri
├── eve.Emg       → Harita NPC spawn/event verileri (custom binary)
└── npc.json      → NPC isim override tablosu (server-side enrichment)
```

---

## 1. Npc.dat — NPC Şablon Veritabanı

### Binary Format

- **Kayıt boyutu:** 138 byte (sabit, `StructLayout Sequential Pack=1`)
- **XOR Şifreleme:** Tüm alanlar encode edilmiş

```
struct PhoneixNpc (138 bytes, Pack=1):
  [0]     NpcNameLength  : byte
  [1-20]  NpcName        : byte[20]  (reversed ASCII)
  [21]    Type           : byte
  [22-23] NpcID          : ushort    decode: (val ^ 0x5209) - 9
  [24-25] ImageNum       : ushort
  [26-27] ImageNumSmall  : ushort
  [28-43] ColorCodes     : uint[4]
  [44]    Catchable      : byte
  [45-46] UnknownBytes   : byte[2]
  [37]    Level          : byte      decode: (val ^ 0xC8) - 1
  [38-41] HP             : uint      decode: (val ^ 0x0BAEB716) - 1
  [42-45] SP             : uint
  [46-55] Stats          : ushort[5] (STR,CON,INT,WIS,AGI)
  [56]    ImageNumEnlarge: byte
  [57]    element        : byte      decode: (val ^ 0xC8) - 1
  [58-67] Skills         : ushort[5] (3 skills + padding)
  [68-77] ItemDrops      : ushort[5] (ItemID1-5, native drop table)
  [78]    TalkImage      : ushort    (portrait ID for dialogue)
  [109]   NPCQuestID     : byte
  [111]   HumanNPC       : byte      (1=human NPC, 0=monster)
```

### Pipeline: Npc.dat → SceneDataManager → ResolveNpcInfo → NPC Name/Stats

```
Server Boot:
  GameDataBase.ImportNpcDat(path)
    → XOR-decode Template ID (rawId ^ 0x520E)
    → ASCII name from dat (reversed bytes)
    → INSERT INTO npc_data (id, name, level, hp, element)

Runtime Interaction Logs:
  AC20.Recv1:
    → Player clicked NPC #{clickID} '{npcName}' (TID: {templateId}) on Map #{mapId} ({mapName})
  ProcessInteraction:
    → Found NPC #{clickID} '{npcName}' (TID: {templateId}), calling Interact for player {playerName}
  EveEventInterpreter:
    → Executing native Event for Map #{mapId} ({mapName}), NPC #{clickId} '{npcName}' (TID: {npcTid}) -> Event #{eventId} ('{eventName}')
    → Sent Step {stepNum} (TalkID: 0x{talkId:X} #{talkId}) - "{dialoguePreview}" to {playerName} for NPC #{clickId} '{npcName}'
```

NPC Spawn (Map.cs LoadNPCs):
  EveManager.LoadNpcEntries(mapData)
    → Exact binary layout (13-byte header + 92-byte walk pattern + 8-byte tail, 1119/1119 map verified)
    → entry.npcId (raw TemplateID)
  GameDataBase.ResolveNpcInfo(mapId, clickId, templateId)
    → XOR decode candidates (templateId ^ 0x5209 - 9, etc.)
    → npc_data SQLite lookup
    → Returns: Name, Level, HP, Element
```

### Decode Formulas (GameDataBase.ImportNpcDat)

```csharp
int id      = (ushort)((rawId  ^ 0x5209)   - 1);  // offset in file: rec*138 + 12
int level   = (byte) ((rawLvl ^ 0xC8)     - 1);   // offset in file: rec*138 + 37
int hp      = (int)  ((rawHp  ^ 0x0BAEB716) - 1); // offset in file: rec*138 + 38
int element = (byte) ((rawElem ^ 0xC8)     - 1);   // offset in file: rec*138 + 57
```

---

## 2. Talk.dat — Diyalog Metinleri

### Yapısı (292 Baytlık Sabit Kayıtlar)

`Talk.dat` dosyası (5,108,248 bayt) tam olarak **17,494 kayıttan** oluşur (`5108248 / 292 = 17494`). Her 292 baytlık bloğun yerleşimi:

- `Bayt 0-1`: **`UInt16 TalkID`** — `Eve.emg` bytecode opcodelarında doğrudan referans verilen resmi TalkID numarası (Örn: `20304`, `20305`, `52220` vb.).
- `Bayt 2`: **`Byte TextLength`** — Metnin bayt uzunluğu (1..250).
- `Bayt 3`: `Byte Flags/Padding`.
- `Bayt [recOffset + 292 - 35 - len] .. [recOffset + 292 - 35]`: **Tersine Yazılmış Metin (Reversed Text)**.
- `Son 35 Bayt`: `fffff` kontrol dizgisi ve 30 baytlık dahili istemci metadata kuyruğu.
  - `bytes [292-35 .. 292-30]`: `fffff` Metin Formatlama Ön-Eki (5 byte).
  - `bytes [292-30 .. 292]`: Kuyruk Metadata Bloğu (`0xEF 0xEC` deseni + `0x35 0x0B 0xE0 0x07` sabitleri, 30 byte).
- **Evrensel Bölüm / Ofset Çözümleme Formülleri (Zero-Hardcode Mathematical Mapping):**
  1. **Sistem ve Etkileşimli Nesneler (`11000..11100`)**: `RecordIndex = TalkID - 11000` (Örn: #11005 Sandık $\rightarrow$ Rec #5, #11070 Kükreme $\rightarrow$ Rec #70/77).
  2. **Hikaye, Başlangıç ve Yoldaş Görevleri (`20000..29999`)**: `RecordIndex = TalkID - 18904` (Örn: #20304 Robinson Başlangıç $\rightarrow$ Rec #1400, #20326 Sal Sandığı $\rightarrow$ Rec #1422).
  3. **Dünya, Şehir, Ada ve Köy Diyalogları (`30000..49999`)**: `RecordIndex = TalkID - 23105` (Örn: #30004 Welling Köyü $\rightarrow$ Rec #6899, #30724 Kelan Köylüsü $\rightarrow$ Rec #7619).

```
Kayıt Okuma Algoritması:
  recOffset = recordIndex * 292
  len = data[recOffset + 2]
  textStart = recOffset + 292 - 35 - len
  raw_bytes = data[textStart .. textStart + len]
  text = Reverse(raw_bytes) -> ASCII/Big5 Decode
  text = text.TrimStart('fffff').Trim()
```

### İstemci-Sunucu İlişkisi

- Server **sadece talkId'yi** paket içinde gönderir (AC20 Sub1, bytes [15-17], 3-byte LE)
- Client **Talk.dat'ı kendi okur** ve metni gösterir
- Server içi `PhxTalkDat` motoru hem 1-tabanlı indeksleri hem de mutlak ofsetleri O(1) hızında çözümler.

### AC20 Sub1 Paket Yapısı (BuildDialogueStep)

```
[0]  = 0x14 (AC 20)
[1]  = 0x01 (Sub 1)
[2-4]= 00 00 00 (padding)
[5]  = step   (diyalog adım no)
[6]  = 0x01   (fixed)
[7]  = portraitType  (3=NPC, 7=Player/pet)
[8]  = clickId (NPC ClickID)
[9]  = 0x00   (padding)
[10-13] = 01 00 00 00  (flags)
[14] = 0x00   (padding)
[15] = talkId & 0xFF         (LSB)
[16] = (talkId >> 8) & 0xFF  (MID)
[17] = (talkId >> 16) & 0xFF (MSB)
```

### Doğru TalkID Haritası (Talk.dat'tan)

#### Map 10036 - Robinson Beach

```
Robinson (ClickID 1):
  0x063ED2  "This is a deserted island. How did you end up here?"
  0x0640DB  "I think you must have been shipwrecked and drifted to this island..."
  0x06423D  "It has been 28 years since I drifted to this island."
  0x0408DC  "The ship is very close to the Philippines, would you like to search..."
```

#### Map 10017 - Ship Deck

```
Persian Cat (ClickID 8):
  Spesifik kedi diyalogu Talk.dat'ta bulunamadı
  (kedi muhtemelen sessiz, sadece animasyon tetikler)
  Mevcut 0x0175BD YANLIŞ: "5. Greece was completely conquered by which group?"
```

#### Map 10027 - Ship Cabin

```
Drunk Man (ClickID 1):
  0x050247  "Money is not the most important in the world. For I have wine..."
  0x041B30  "Don't worry! I've got [item] with me..."

Congratulations (Game Machine win):
  0x088106  "Congratulations on your finishing! This award is for you..."
```

---

## 3. eve.Emg — Harita NPC/Event Veri Dosyası

### Format (EveLoader.cs ile parse edilir)

- **Header:** 8 byte magic, 4 byte entry count
- **Map entries:** `(mapId:u16, sceneId:u16, dataOffset:u32, dataLen:u16)` × N
- **NPC entries per map:**
  ```
  elen: u16        (NPC sayısı)
  foreach NPC:
    clickId: u16
    name: 20 bytes (1 byte length + chars)
    unknownbyte1: byte
    x: u32
    y: u32
    events: byte[] (length-prefixed)
    unknowns: byte[]
    npcId: u32     (TemplateID — RAW, XOR decode gerekli değil)
    rotation: byte
    walkBehavior: byte
    walksteps: []  (length-prefixed, 12 bytes each)
    walkpatterns: [] (complex)
    unknownwords: u16[4]
  ```

### Kritik NPC Tablosu (eve.Emg'den parse edildi)

| Map   | ClickID | TemplateID | İsim          |
| ----- | ------- | ---------- | -------------- |
| 10017 | 8       | 11000      | Persian Cat    |
| 10017 | 7       | 14065      | Girl           |
| 10017 | 4,11    | 14001      | Crew on Deck   |
| 10017 | 3,9     | 14002      | Crew           |
| 10017 | 10      | 14003      | Captain        |
| 10027 | 1       | 14042      | Drunk Man      |
| 10027 | 2       | 14146      | Peter          |
| 10027 | 3       | 14145      | John           |
| 10027 | 4       | 14303      | Natasha        |
| 10027 | 5       | 10000      | Breilliat      |
| 10027 | 6       | 18000      | Game Machine a |
| 10027 | 7       | 18001      | Game Machine b |
| 10036 | 1       | 12032      | Robinson       |
| 10036 | 2,3     | 19038      | Chest          |
| 10036 | 4       | 19037      | Crate          |
| 10036 | 5       | 19034      | Coconut Tree   |
| 10036 | 6       | 19039      | Coconut        |
| 10036 | 7       | 19035      | Raft Chest     |
| 10036 | 8       | 11019      | Burke (Tiger)  |

---

## 4. Mark.dat — Quest Log & Görev Günlüğü

- `QuestManager.LoadAuthenticQuestsFromMarkDat(path)` ve `PhxMarkDat` ile yüklenir
- 1.191.715 byte sabit boyut (2155 kayıt × 553 byte)
- Ters sıralı (reversed) ASCII karakter kodlaması
- İstemci görev günlüğü metinlerini ve `#01`, `#02`, `#99` aşama açıklamalarını içerir
- `AC24 Sub5` paketi ile istemcinin görev penceresini günceller

---

## 4.1 Talk.dat & NPC Diyalog Sistemi (AC20 Sub1)

- 24-bit (3-byte) doğrudan adresleme ile diyalog pencerelerini besler
- **Map 10017 (Ship Deck) Doğrulanmış TalkID Haritası:**
  - `0x0175AC` / `0x0175AD`: Captain Cook (Step 1 & 2)
  - `0x0175BA`: Girl
  - `0x0175B9`: Persian Cat
  - `0x0175BD`: Crew (Sailor)
  - `0x0175B3`: Crew on Deck
  - `0x0175A8` / `0x0175B2`: Visitors & Jack
  - `0x0175AE`: Ross

---

## 5. Npc.dat → Drop Sistemi

```csharp
MonsterDropManager.LoadFromNpcDat(path):
  foreach record in Npc.dat:
    npcId = decode(rawId)
    drops = [ItemID1..ItemID5] (decoded)
    MonsterLootTables[npcId] = drops
```

Native drop table'ları NPC record'unun `ItemID1-5` alanlarından gelir.

---

## 6. Direct Binary Npc.dat Decoding

- NPC ve yaratık isimleri doğrudan `Data/Npc.dat` dosyasından 138-byte ikili struct parse edilerek okunur.
- Kayıt başındaki ters çevrilmiş bayt dizisi (`offset + 10` ile `offset + 1`) çözülerek ASCII / Big5 kodlamasıyla orijinal oyun ismi elde edilir.
- `npc.json` veya harici override sözlükleri devreden çıkarılmış olup tüm veriler %100 doğrudan `.dat` dosyasından yüklenir.
