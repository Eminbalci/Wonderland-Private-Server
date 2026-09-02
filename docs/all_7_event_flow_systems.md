# Wonderland Online: 7 Farklı Olay Akış (Event Flow) Sistemi

`eve.Emg`, `Talk.dat` ve `aLogin.exe` mimarisi incelendiğinde oyunda **7 farklı bağımsız olay akış sistemi** bulunmaktadır:

---

## 1. Yedi Farklı Olay Akışı ve Mekanikleri

| # | Olay Akış Türü | Tetiklenme Şekli | İlgili `.dat` / Bytecode | İşlenen Arayüz & Protokol |
| :-: | :--- | :--- | :--- | :--- |
| **1** | **NPC Diyalog & Hikaye Akışı** | NPC'ye sol tıklama (`AC 23:1`) | `eve.Emg` (`case 3 & 8 & 10`) + `Talk.dat` | Tek pencereli konuşma (`AC 20:1 Type 1`), Soru-cevap seçenek menüleri (`AC 20:1 Type 6`). |
| **2** | **Sandık & Yer Eşyaları Akışı** | Sandığa/Kutuya sol tıklama | `eve.Emg` (`case 6 Item`) | Sandık açılma animasyonu (`AC 22:1`), Tek seferlik eşya/altın ödülü verme (`Opcode 1`). |
| **3** | **Doğal Kaynak Toplama Akışı** | Maden/Odun/Balık alanına tıklama | `eve.Emg` (`case 5 Mine`) + `Item.dat` | Kazma/Olta/Elektrikli süpürge aracıyla toplama döngüsü (`AC 23:2` & `AC 22:10`). |
| **4** | **Zemin Tuzakları & Gizli Basamaklar** | Oyuncunun haritada belirli bir karoya yürümesi (`AC 6:1`) | `eve.Emg` (`case 7 Trap / InteractiveInfo`) | Gizli kapı açılması, tuzak düşmesi, lav hasarı veya mağara çöküşü tetikleyicisi. |
| **5** | **Koşullu Işınlanma Kapıları** | Kapıdan veya portaldan geçme | `eve.Emg` (`case 4 Door / WarpInfo`) | Anahtar eşya (`Item`), seviye veya görev bayrağı kontrolüyle ışınlanma (`AC 5:4`). |
| **6** | **Harita Ön-Olay Değerlendirmesi (`PreEvent`)** | Haritaya ilk giriş (`Map.Warp_In`) | `eve.Emg` (`case 8 & 10 PreEvent`) | Oyuncunun görev durumuna göre NPC'leri ve engelleri anında gizleme/gösterme (`AC 22:10`). |
| **7** | **Çadır İçi Üretim Tezgahları Akışı** | Çadır mobilyalarına tıklama | `eve.Emg` (`Opcode 17`) + `Compound.dat` / `Formula.dat` | Dikiş makinesi, marangoz masası, kimya ocağı ve mutfak tezgahı üretim pencereleri (`AC 50`). |

---

## 2. Özel Giriş Menüleri (Sayılı / Şifreli Olaylar)
NPC etkileşimlerinde diyalog haricinde istemci tarafından desteklenen 2 özel giriş penceresi:
* **Sayı Giriş Penceresi (`AC 20:1 Type 8`)**: Oyuncudan belirli bir miktar veya sayısal şifre girmesini ister.
* **Metin / Şifre Giriş Penceresi (`AC 20:1 Type 9`)**: Gizli kapı parolası veya bilmece cevabı girilmesini sağlar.
