# Todo — Çoklu Ajan Görev Dağılımı (Multi-Agent Task Allocation)

- [x] 1. **Ban Enforcement & Duplicate Ban Prevention (Ajan 1):**
  - [x] 1.1 `LoginServer.cs`: Add `IsIpBanned(string ip)` helper and reject banned IP connections immediately on `ListenThread()`.
  - [x] 1.2 `AC63.cs`: In `Recv4`, check banned username (`banned_users`), `users.banned == 1`, and IP (`banned_ips`), set `loginState = 5`, send `Tools.FromFormat("bb", 1, 7)` and disconnect.
  - [x] 1.3 `GmManager.cs`: Add `IsAccountBanned`, `IsIpBanned`, `BanAccount` (with duplicate check), and `UnbanAccount`.
  - [x] 1.4 `AC02.cs`: Add `:ban <username> [reason]` and `:unban <username>` GM commands with duplicate prevention.
  - [x] 1.5 `MainForm1.ExtendedTabs.cs`: Implement duplicate ban check in `BanAccountInternal` and `BanIpInternal` with warning popups.
  - [x] 1.6 Verification and test build via MSBuild / test scripts.
  - [x] 1.7 Documentation: Update `docs/81_ban_enforcement_and_duplicate_prevention.md` and `README.md`.

- [x] 2. **Dungeon Instance Creation & Map Isolation (Ajan 2):**
  - [x] 2.1 `Map.cs`: Add `TeleportToInstance(GameMap targetMap, Player sender, WarpData warp)` for chamber warp isolation.
  - [x] 2.2 `MapManager.cs`: Add isolated instance map creation (`CreateInstanceMap`) and registration (`RegisterInstanceMap`, `UnregisterInstanceMap`, `GetInstanceMap`), include in `ActiveMaps`.
  - [x] 2.3 `InstanceDungeonEngine.cs`: Overhaul room management, chamber isolation (`InstanceMap`), Subcode 0 response matching `FUN_0019da10`, participant roster synchronization, and team party warping in `StartInstance`.
  - [x] 2.4 `AC85.cs`: Correct packet reading pointers (`p.SetPtr(6)`), subcode dispatching (`SubCode 1`: Create/query/enter, `SubCode 2`: Leave/exit, `SubCode 4`: Pre-join/inspect, `SubCode 5`: Join, `SubCode 10`: Member list/sync, `SubCode 11`: Tabs, `SubCode 13`: Dismiss).
  - [x] 2.5 Build and verify compilation with 0 errors.
  - [x] 2.6 Technical documentation: Create `docs/82_dungeon_instance_creation_and_chamber_isolation.md` and update `README.md`.

- [x] 3. **World Subsystem Modernization (Ajan 3):**
  - [x] 3.1. `WorldEnums.cs`: Enums (`WorldServerState`, `WorldTickRate`, `WorldEventType`) tanımlanması.
  - [x] 3.2. `WorldAccess.cs`: `WorldServerHost` arayüzünün modern metodlarla (`GetPlayer`, `isOnline`, `DisconnectPlayer`, `Broadcast`, `GetAllPlayers`) genişletilmesi.
  - [x] 3.3. `WloWorldNode.cs`: Thread-safe `ConcurrentDictionary<uint, Player>` koleksiyonuna geçiş, `MapManager.Instance` entegrasyonu, temiz lifecycle yönetimi.
  - [x] 3.4. `WorldServer.cs`: `MapList` ile `MapManager.Instance.ActiveMaps` senkronizasyonu, `WorldServerHost` metodlarının implementasyonu, login kuyruğu ve thread döngülerinin null-safe/thread-safe hale getirilmesi.
  - [x] 3.5. Derleme ve doğrulama (0 hata).
  - [x] 3.6. Dokümantasyon (`docs/83_world_subsystem_modernization.md`, `README.md`).

- [x] 4. **PK & Settings Button Protocol (AC 33):**
  - [x] 4.1 `AC33.cs`: Correct subcode 1 and 2 handling without requiring extra setting type byte (matching client `form_module` / `FUN_003c4138`).
  - [x] 4.2 `CharacterDataBase.cs`: Implement `SaveSettings` for immediate SQLite persistence.
  - [x] 4.3 `AC33.cs`: Synchronize `p.Settings.ToArray()` back to client to update all visual button states.

- [x] 5. **Server GUI World Studio Modernization:**
  - [x] 5.1 `MainForm1.ModernUi.cs`: Link World navigation category directly to strongly-typed tab instances (`tabMapNpcStudio`, `tabPagePortals`, `tabNpcResolver`, `tabTalkResolver`).
  - [x] 5.2 `MainForm1.cs`: Overhaul `tabMapNpcStudio` into a real-time World Entity Monitor showing live players, active overworld bazaars, dynamic spawns, portals, and static Eve NPCs.
  - [x] 5.3 `MainForm1.cs`: Add World administration actions ("Warp Player Here", "Clear Drops", live entity inspector).

- [x] 6. **Derleme & Doğrulama:**
  - [x] 6.1 Tüm ajanların değişikliklerinin birleştirilmesi ve projenin MSBuild / dotnet build ile derlenmesi (0 hata, 0 uyarı).
  - [x] 6.2 PowerShell testleri ile ban, zindan, world ve PK sistemlerinin doğrulanması (`tests/Run-ComprehensiveVerification.ps1`, `tests/Run-DungeonInstanceTests.ps1`).

- [x] 7. **Dokümantasyon:**
  - [x] 7.1 `docs/80_maxstat_chat_command_and_stat_synchronization.md`
  - [x] 7.2 `docs/81_ban_enforcement_and_duplicate_prevention.md`
  - [x] 7.3 `docs/82_dungeon_instance_creation_and_chamber_isolation.md`
  - [x] 7.4 `docs/83_world_subsystem_modernization.md`
  - [x] 7.5 `docs/84_server_gui_world_studio_and_pk_button_toggle.md`
  - [x] 7.6 `README.md` ve `todo.md` güncellenmesi.
