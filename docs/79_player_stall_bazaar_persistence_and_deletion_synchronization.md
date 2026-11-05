# 79. Oyuncu Pazar Kioskları ve Özel Stall Silme Senkronizasyonu (Player Stall Bazaar Persistence & Deletion Synchronization)

## 1. Genel Bakış ve Problem Tanımı

Sunucu üzerindeki oyuncu ve özel tezgah (bazaar/stall) yönetiminde, yönetici veya oyuncu pazarları sildiğinde (`:removestall`, `:clearstalls`, `:stall remove` veya `MainForm1.ExperimentalTab` arayüzündeki "Delete Selected" / "Clear All Stalls" butonları ile):
1. **Veritabanı Yolu Ayrışması (Dual-DB Desynchronization):**
   - `RCLibrary.Core.PathHelper.ResolveDatabaseFile` aday listesinde `baseDir\Data\ServerDataBase.db` bulunmadığından, derlenmiş `bin\Debug` ortamında çalışan sunucu `Data\ServerDataBase.db` dosyasına yönelirken, `Phoenix.Core.DataBase` kütüphanesi doğrudan `bin\Debug\Data\ServerDataBase.db` dosyasını referans alıyordu.
   - Bu durum iki veritabanı kopyası arasında desenkronizasyona neden oluyor, bir kopyadan silinen veri diğer kopyadan veya MSBuild kopyalama işleminden sonra yeniden yükleniyordu.
2. **Hafıza-Veritabanı Koşullu Silme Boşluğu:**
   - `PlayerStallBazaarEngine.RemoveCustomStall(vendorCharId)` metodu, `!m_stalls.TryRemove(...)` başarısız olduğunda (örneğin tezgah haritada henüz spawn olmamışsa veya hafızada bulunmuyorsa) doğrudan `return false;` dönüyor ve `DeleteStallFromDatabase` metodunu çağırmıyordu.
3. **Mükerrer Sütun Göçü Hata Logları:**
   - `EnsureDatabaseTable()` her çağrıldığında `ALTER TABLE custom_stalls ADD COLUMN stall_type INT DEFAULT 1;` komutunu doğrudan çalıştırmayı deniyor ve veritabanı loglarına `duplicate column name: stall_type` istisnaları basıyordu.

## 2. Mimari Değişiklik ve Uygulama Detayları

### 2.1 Tüm Veritabanı Adaylarında Atomik Yürütme (`ExecuteOnAllDatabases`)
[`PlayerStallBazaarEngine.cs`](file:///D:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/Experimental/PlayerStallBazaarEngine.cs) içerisine `ExecuteOnAllDatabases(string sql, params DbParam[] parameters)` metodu eklendi:
- Birincil veritabanı işlemi `RCLibrary.Core.DataBase.Execute(sql, parameters)` ile yürütülür.
- Eşzamanlı olarak diskte mevcut bulunan tüm bağımsız aday SQLite veritabanı dosyaları (`bin\Debug\Data\ServerDataBase.db`, kök `Data\ServerDataBase.db` ve `baseDir\ServerDataBase.db`) taranarak sorgu atomik olarak hepsine uygulanır.
- `SaveStallToDatabase`, `DeleteStallFromDatabase` ve `ClearAllStallsFromDatabase` metotları bu altyapıya geçirildi.

### 2.2 Dayanıklı Silme Mantığı (`RemoveCustomStall`)
- `m_stalls.TryRemove(vendorCharId, out var stall)` sonucu ne olursa olsun (tezgah hafızada olsun veya olmasın), `DeleteStallFromDatabase(vendorCharId)` çağrılması garanti altına alındı.
- Eğer tezgah hafızada ve haritada mevcutsa, bağlı harita NPC'si (`stall.ClickId`) kaldırılır ve haritadaki tüm oyunculara silinme ve despawn paketleri (`BuildNpcDespawnPacket`, `BuildStallDespawnPacket`, `BuildStallSignClearPacket`, `BuildKioskStatePacket`) anons edilir.

### 2.3 `PathHelper` Paritesi
[`RCLibrary/RCLibrary.Core/PathHelper.cs`](file:///D:/GitHub/Wonderland-Private-Server/RCLibrary/RCLibrary.Core/PathHelper.cs) içerisindeki `candidates` dizisine `Path.Combine(baseDir, "Data", defaultFileName)` eklenerek `Phoenix.Core.DataBase.ResolveDbPath` ile %100 yol çözümleme paritesi sağlandı.

### 2.4 DDL Şema Denetimi
- `EnsureDatabaseTable()` içinde `ALTER TABLE` çalıştırmadan önce `RCLibrary.Core.DataBase.StaticHasColumn("custom_stalls", "stall_type")` kontrolü eklendi; gereksiz hata logları tamamen engellendi.

## 3. Parametreler, Dönüş Tipleri ve Edge Case Analizi

### `RemoveCustomStall(uint vendorCharId)`
- **`vendorCharId` (`uint`)**: Silinecek stall veya sentetik vendor kimliği.
- **Dönüş Değeri**: `bool` (İşlem veritabanından başarıyla silindiğinde `true`, istisna durumunda `false`).
- **Edge Cases**:
  - `vendorCharId` hafızada yoksa bile veritabanı satırı silinir ve `true` döner.
  - Harita null veya oyuncu yoksa despawn paketleri atlanır, veritabanı temizliği devam eder.

### `ClearAllStalls()` / `ClearAllStallsFromDatabase()`
- **Dönüş Değeri**: `void`
- **Etki**: Aktif haritalardaki tüm tezgah NPC'lerini ve görsel aktörleri despawn eder, hafıza koleksiyonunu boşaltır, `custom_stalls` tablosundaki tüm kayıtları tüm veritabanı yollarında temizler.

## 4. Test ve Doğrulama

PowerShell ve `System.Data.SQLite` / `sqlite3` destekli test paketi ile aşağıdaki senaryolar doğrulanmıştır:
1. **Boş Veritabanı Başlangıcı:** `LoadStallsFromDatabase()` 0 tezgah okur, hata fırlatmaz. (PASSED)
2. **Kayıt ve Kalıcılık:** `SpawnCustomStall(persistToDb: true)` ile oluşturulan tezgah tüm aday veritabanı dosyalarına yazılır. (PASSED)
3. **Tekil Silme ve Kalıcılık:** `RemoveCustomStall(vendorId)` çağrıldığında hafızadan ve tüm veritabanı dosyalarından silinir; yeni nesne oluşturulup DB'den okunduğunda 0 tezgah döner (resurrection engellendi). (PASSED)
4. **Toplu Temizleme:** Çoklu tezgah açıldıktan sonra `ClearAllStalls()` çağrıldığında hem haritadan hem veritabanlarından tamamen temizlenir. (PASSED)
