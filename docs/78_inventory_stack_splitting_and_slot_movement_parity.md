# 78. Envanter Stack Ayırma ve Slot Taşıma Paritesi (Inventory Stack Splitting & Slot Movement Parity)

## 1. Genel Bakış ve Problem Tanımı

WLO istemcisi (`aLogin.exe` ve `wloweb` WebAssembly istemcisi), oyuncu envanterinde stacklenmiş bir eşya grubundan (örneğin 5 adet `ItemID: 43002`) belirli bir miktarı (örneğin 1 adet) boş bir slota sürükleyip bıraktığında sunucuya şu paketi gönderir:
- **Paket:** `AC 23:10` (`protocol.InventoryMove`)
- **Bayt Sırası:**
  - `Byte 0`: `23` (`0x17`, `AC`)
  - `Byte 1`: `10` (`0x0A`, `Sub`)
  - `Byte 2`: `from` (1 tabanlı kaynak slot numarası, 1..50)
  - `Byte 3`: `ammt` (taşınmak istenen adet, 1..50)
  - `Byte 4`: `to` (1 tabanlı hedef slot numarası, 1..50)

Önceki sunucu uygulamasında (`wlo.pserver.core\Game\Inventory.cs` içinde `MoveItem`):
- Hedef slot boş olduğunda (`CanFitItem` doğrulaması geçildiğinde) `ammt` parametresi göz ardı edilerek doğrudan `source.Ammt` okunuyordu (`byte moved = source.Ammt;`).
- Bu işlem sonucunda oyuncu 5 adetlik gruptan 1 tanesini ayırmak istese dahi sunucu tüm stack'i (5 adedi) hedef slota taşıyor, kaynak slotu tamamen temizliyor (`source.Clear()`) ve istemciye taşınan miktar olarak 5 gönderiyordu (`owner.Send(23, 10, from, 5, to)`).

## 2. Mimari Değişiklik ve Uygulama Detayları

[`wlo.pserver.core\Game\Inventory.cs`](file:///D:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/Inventory.cs#L1188-L1215) içerisindeki `MoveItem(byte from, byte to, byte ammt)` metodu yeniden düzenlenmiştir:

1. **Taşınacak Miktar Hesaplaması:**
   ```csharp
   byte moveAmmt = (byte)Math.Min((int)ammt, (int)source.Ammt);
   byte ignoreRoot = (moveAmmt == source.Ammt) ? from : (byte)0;
   ```
2. **2D Grid Alan Kontrolü (`CanFitItem`):**
   - Eğer tüm stack taşınıyorsa (`moveAmmt == source.Ammt`), kaynak slot boşalacağı için `ignoreRootSlot: from` kullanılarak hedef alan hesaplanır.
   - Eğer kısmi ayırma yapılıyorsa (`moveAmmt < source.Ammt`), kaynak slot dolu kalmaya devam edeceğinden `ignoreRootSlot: 0` geçilerek hedef alan bağımsız kontrol edilir.
3. **Durum Ayrımı:**
   - **Tam Taşıma (`moveAmmt == source.Ammt`):**
     - `UnlockSlaveSlots(from)` ile kaynak slot ve bağlı çoklu hücre işaretçileri kaldırılır.
     - `target.CopyFrom(source)` ile hedef slota kopyalanır.
     - `target.Ammt = moveAmmt` ve `LockSlaveSlots(to, target.ItemID)` uygulanır.
     - `source.Clear()` ile kaynak slot temizlenir.
   - **Kısmi Stack Ayırma (`moveAmmt < source.Ammt`):**
     - Kaynak slot boşaltılmaz ve kilitleri açılmaz.
     - `target.CopyFrom(source)` ile hedef slota eşya bilgileri kopyalanır.
     - `target.Parent = 0; target.Ammt = moveAmmt;`
     - `LockSlaveSlots(to, target.ItemID)` ile hedef slot rezerve edilir.
     - `source.Ammt -= moveAmmt;` ile kaynak slottaki miktar düşürülür (kalan miktar korunur).
4. **Paket Yanıtı:**
   - `owner?.Send(Tools.FromFormat("bbbbb", 23, 10, from, moveAmmt, to));`
   - İstemciye bildirilen miktar tam olarak taşınan miktar (`moveAmmt`) olur.

## 3. Parametreler, Dönüş Değerleri ve Fonksiyon İmzaları

### `MoveItem(byte from, byte to, byte ammt)`
- **`from` (`byte`)**: Kaynak slot indeksi (1..50).
- **`to` (`byte`)**: Hedef slot indeksi (1..50).
- **`ammt` (`byte`)**: Taşınması talep edilen miktar (1..50).
- **Dönüş Tipi**: `void`
- **İstisnalar & Sınır Durumları (Edge Cases)**:
  - `from < 1 || from > 50 || to < 1 || to > 50 || from == to || ammt == 0`: İşlem sessizce reddedilir.
  - Aktif binek slotu taşınamaz (`MountedVehicleSlot`).
  - Hedef dolu ve aynı stacklenebilir eşyaya sahipse: Stacking dalına girer ve `target.SpaceLeft` sınırına göre birleştirilir.
  - Hedef boşsa: `CanFitItem` doğrulanır; tam veya kısmi taşımaya göre slotlar güncellenir.
  - Hedef geçersiz veya doluysa: `DebugSystem.Write` ile red loglanır.

## 4. Test ve Doğrulama

PowerShell yansıma (reflection) destekli otomatik birim testleri ile aşağıdaki senaryolar başarıyla doğrulanmıştır:
- **Test 1 (Kısmi Ayırma - 5'ten 1 adet):** Slot 9 (5 adet) -> Slot 14 (boş, 1 adet taşındı). Sonuç: Slot 9'da 4 adet, Slot 14'te 1 adet kaldı. (PASSED)
- **Test 2 (Mevcut Stack Üzerine Ekleme):** Slot 9 (4 adet) -> Slot 14 (1 adet). 4 adet taşındı. Sonuç: Slot 9 tamamen temizlendi (0 adet), Slot 14'te 5 adet toplandı. (PASSED)
- **Test 3 (Tam Stack Taşıma):** Slot 14 (5 adet) -> Slot 9 (boş). 5 adet taşındı. Sonuç: Slot 14 temizlendi (0 adet), Slot 9'da 5 adet oldu. (PASSED)
- **Test 4 (Kısmi Ayırma - 5'ten 2 adet):** Slot 9 (5 adet) -> Slot 14 (boş, 2 adet taşındı). Sonuç: Slot 9'da 3 adet, Slot 14'te 2 adet kaldı. (PASSED)
- **Test 5 (Kısmi Ekleme):** Slot 9 (3 adet) -> Slot 14 (2 adet). 1 adet taşındı. Sonuç: Slot 9'da 2 adet, Slot 14'te 3 adet oldu. (PASSED)
