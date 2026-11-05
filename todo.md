# Todo — Envanter Stack Ayırma ve Taşıma Hatası Düzeltmesi (MoveItem Stack Splitting)

- [x] 1. Sunucu Log Analizi: Son log dosyası (`Logs/wlophoenixlogFile_20261105.txt`) incelendi, AC 23:10 paketindeki `ammt` değerinin ve sunucu yanıtının tespiti yapıldı.
- [x] 2. `wlo.pserver.core\Game\Inventory.cs` dosyasındaki `MoveItem` metodunda boş slota taşıma esnasında `ammt` yerine tüm `source.Ammt` değerini taşıyan ve stack'i parçalamayan mantıksal hata düzeltildi (`moveAmmt = Math.Min(ammt, source.Ammt)` ile kısmi ayırma ve tam taşıma ayrıştırıldı).
- [x] 3. `Wonderland Private Server.csproj` MSBuild ile derlendi ve 0 hata ile doğrulama yapıldı.
- [x] 4. Otomasyon/PowerShell testleri ile 5'li stackten 1 ve 2 adet kısmi ayırma, hedefte mevcut stack üzerine birleştirme ve tam taşıma senaryoları %100 doğrulandı.
- [x] 5. Teknik dokümantasyon (`docs/78_inventory_stack_splitting_and_slot_movement_parity.md`) oluşturuldu ve global `README.md` güncellendi.
