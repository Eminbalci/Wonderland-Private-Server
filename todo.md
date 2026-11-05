# Todo — `:maxstat` Komutunun Eklenmesi (Max Stat & Points Reset Command)

- [x] 1. Mimari & Bağımlılık Doğrulaması: `EquipManager.cs` (`baseStr`, `baseCon`, `baseInt`, `baseWis`, `baseAgi`, `SkillPoints`, `Send8_1`), `GmManager.cs` ve `AC02.cs` komut akışının incelenmesi.
- [x] 2. `GmManager.cs` içine `MaxStatPlayer(Player player, out string statusMsg)` metodunun eklenmesi (STR, CON, INT, WIS, AGI değerlerini avatar bonusları da hesaplanarak tam 602'ye set etme, `SkillPoints` değerini 0 yapma, HP/SP yenileme, `Send8_1` senkronizasyonu ve veritabanına kaydetme).
- [x] 3. `AC02.cs` içerisine `:maxstat` ve `/maxstat` komut handler'ının eklenmesi, `isPublicCommand` listesine eklenerek oyuncu ve GM erişiminin sağlanması, `:help` / `:cmds` menüsüne eklenmesi.
- [x] 4. Projenin MSBuild ile derlenmesi (0 hata, 0 uyarı).
- [x] 5. PowerShell ile komutun fonksiyonel testinin doğrulanması (tüm statların 602 olması, puanların 0 olması, veritabanı kaydı).
- [x] 6. Teknik dokümantasyon (`docs/80_maxstat_chat_command_and_stat_synchronization.md`), global `README.md` ve `todo.md` güncellenmesi.
