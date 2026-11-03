# Todo — Sunucu İyileştirmeleri (2026-11-03)

Kural: `Data/ServerDataBase.db` elle değiştirilmez/commit edilmez. Tüm testler `bin` kopyası üzerinde, izole klasörde yapılır.

## 1. Log dosyası düzeltmeleri (`RCLibrary/System/DebugSystem.cs`)
- [x] Satırlar sırayla yazılsın: `ConcurrentStack` (LIFO) yerine `ConcurrentQueue` (FIFO). Şu an aynı anda gelen satırlar ters sırada dosyaya düşüyor.
- [x] Her satırdan sonra kalan boş satırı kaldır (`\r\n` + `WriteLine` çift satır sonu).
- [x] Eski log temizliği gerçekten çalışsın: dosya sayımı `Logs/` klasöründe yapılsın, en fazla 10 dosya kalsın (şu an kök klasörde saydığı için hiç silmiyor; 17 dosya / 20 MB birikmiş).

## 2. Konsol performansı (`DebugSystem` + Dashboard)
- [x] Her log satırı için ayrı `BeginInvoke` yerine satırlar kuyruğa alınsın, arayüze 150 ms'de bir toplu yazılsın.
- [x] Konsol en fazla ~5000 satır tutsun; eskiler baştan silinsin (bellek ve hız).
- [x] "Auto-Scroll" butonu gerçekten çalışsın (şu an sadece yazısı değişiyor, kaydırmayı etkilemiyor).

## 3. Otomatik veritabanı yedeği
- [x] SQLite online backup API ile tutarlı yedek (sunucu çalışırken güvenli kopya).
- [x] Açılışta bir kez + varsayılan her 30 dakikada bir; son 10 yedek tutulur (`Data/Backups/ServerDataBase_yyyyMMdd_HHmmss.db`).
- [x] Ayarlar `Config.settings.wlo` içinde: açık/kapalı, aralık, saklanacak yedek sayısı.
- [x] Server → "Backups" sayfası: "Back Up Now", "Open Folder", yedek listesi ve son yedek bilgisi.
- [x] MySQL modunda yedeği atla ve bunu logla. `Data/Backups/` → `.gitignore`.

## 4. Auto-save ölçümü (`WorldServer.AutoSaveLoop`)
- [x] Her turun süresi ölçülsün; son kayıt zamanı, süre ve kaydedilen oyuncu sayısı tutulsun.
- [x] Bir tur 1 saniyeyi geçerse uyarı logu (en fazla dakikada bir).
- [x] Kayıt aralığı ayardan okunsun (varsayılan 1 sn — mevcut davranış korunur).

## 5. Dashboard metrikleri
- [x] Pencerenin altındaki durum çubuğunda: bugünkü en yüksek oyuncu sayısı, son auto-save (süre), son yedek, sıradaki duyuru.
- [x] Hata sayacı: oturumdaki `Error` log sayısı; tıklanınca konsolda ilk hataya gider.

## 6. Zamanlanmış duyurular
- [x] Mesaj listesi + dakika aralığı + hedef (bildirim kutusu / dünya sohbeti); sırayla döner.
- [x] Ayarlar dosyasına kaydedilir, sunucu açılınca otomatik başlar.
- [x] Server → "Announcements" sayfası: ekle / sil / sırala / şimdi gönder / aç-kapat.

## 7. Aktivite kayıtları (oturum + sohbet)
- [x] Giriş/çıkış kaydı: karakter, hesap, IP, giriş-çıkış zamanı, süre → `Logs/Activity/sessions_yyyyMMdd.csv`.
- [x] Sohbet kaydı: zaman, kanal, karakter, mesaj → `Logs/Activity/chat_yyyyMMdd.csv` (GM komutları ayrıca işaretli).
- [x] Server → "Activity" sayfası: tarih seçimi, oturum ve sohbet tabloları, metin filtresi.

## 8. Doğrulama
- [x] `MSBuild` ile 0 hata.
- [x] İzole kopyada çalıştırıp ekran görüntüleriyle kontrol (yedek dosyası oluşuyor, log sırası doğru, yeni sayfalar açılıyor).
- [x] `git status` ile `Data/ServerDataBase.db`'nin değişmediğini doğrula.
- [ ] Gerçek istemciyle giriş/çıkış ve sohbet kaydını dene (test sırasında satırlar elle yazıldı; gerçek oyuncu bağlanmadı).
