# Todo — Bazaarları Silince Kayıt Olmaması Sorununun Düzeltilmesi (Stall Deletion Persistence Fix)

- [x] 1. Hata Tespiti & Analizi: `PlayerStallBazaarEngine.cs` içindeki `RemoveCustomStall`, `DeleteStallFromDatabase` ve `ClearAllStalls` metodlarının incelemesi; hafıza (`m_stalls`) ile veritabanı senkronizasyonunun ve iki farklı veritabanı yolu (`Data\ServerDataBase.db` ve `bin\Debug\Data\ServerDataBase.db`) arasındaki tutarsızlığın tespiti.
- [x] 2. `PlayerStallBazaarEngine.cs` dosyasında `RemoveCustomStall` metodunun hafızada olmasa dahi veritabanından silmeyi garanti edecek şekilde güncellenmesi.
- [x] 3. `DeleteStallFromDatabase` ve `ClearAllStallsFromDatabase` metodlarının aktif ve çıktı veritabanı yollarının tümünde (`ExecuteOnAllDatabases`) silme işlemini atomik olarak işlemesinin sağlanması.
- [x] 4. `EnsureDatabaseTable()` içindeki gereksiz `ALTER TABLE` hata loglarının `StaticAddColumnIfNotExists` / `StaticHasColumn` kontrolüyle temizlenmesi.
- [x] 5. `RCLibrary/RCLibrary.Core/PathHelper.cs` içindeki `ResolveDatabaseFile` adaylarına `baseDir\Data\ServerDataBase.db` eklenerek `Phoenix.Core` ile tam parite sağlanması.
- [x] 6. Kullanıcının sildiği stall'un (Vendor ID: 8900001) veritabanlarından temizlenmesi (count: 0).
- [x] 7. MSBuild ile projenin derlenmesi (0 hata, 0 uyarı).
- [x] 8. PowerShell test paketi ile stall ekleme, tekil silme, toplu temizleme ve sunucu yeniden başlatma senaryolarının %100 doğrulanması.
- [x] 9. Teknik dokümantasyon (`docs/79_player_stall_bazaar_persistence_and_deletion_synchronization.md`), global `README.md` ve `todo.md` güncellenmesi.
