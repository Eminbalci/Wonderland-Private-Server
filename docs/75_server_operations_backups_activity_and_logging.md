# 75 — Server operations: backups, announcements, activity log, logger fixes

## Logger (`RCLibrary/System/DebugSystem.cs`)
- File queue is now FIFO (`ConcurrentQueue`). The old `ConcurrentStack` wrote each batch in reverse order.
- No blank line after every entry (the entry no longer carries its own `\r\n`).
- Old log retention works: the 10-file limit is applied inside `Logs/` (it used to count files in the
  application folder, so nothing was ever deleted).
- Rolled-over files are named `wlophoenixlogFile_yyyyMMdd_N.txt`.
- Console: lines are queued and written by a UI timer every 150 ms, in colour runs, capped at
  `MaxConsoleLines` (5000). Network threads no longer post one `BeginInvoke` per line.
- `DebugSystem.AutoScroll` is honoured; the Dashboard "Auto-Scroll" button drives it.
- Errors are coloured red, warnings yellow, network lines green.
- `DebugSystem.LineWritten` event (used by the error counter).

## Services (`Src/Server/Services`)
| Service | What it does |
|---|---|
| `DatabaseBackupService` | SQLite online backup to `Data/Backups/ServerDataBase_yyyyMMdd_HHmmss.db`. First run 30 s after boot, then every `BackupIntervalMinutes` (default 30). Keeps `BackupKeepCount` (default 10). Also runs on Safe Shutdown. The live DB is opened read-only. Skipped for MySQL. |
| `AnnouncementScheduler` | Sends `Announcements` in rotation every `AnnouncementIntervalMinutes` to everyone online, as notification box or world chat. |
| `ActivityLog` | `Logs/Activity/sessions_yyyyMMdd.csv` (LOGIN / LOGOUT / SHUTDOWN, account, IP, duration) and `chat_yyyyMMdd.csv` (channel, message, GM command flag). Fed by `CharacterDataBase.CharacterJoined/CharacterLeft` and AC02. |
| `ServerMetrics` | Auto-save duration, daily peak players, error count, last backup. |

Auto-save: each round is timed. A round over 1 s logs a warning (at most once a minute). The interval comes from
`AutoSaveIntervalSeconds` (default 1 s = unchanged behaviour).

## Settings (`Config.settings.wlo`, all optional)
`BackupEnabled`, `BackupIntervalMinutes`, `BackupKeepCount`, `AutoSaveIntervalSeconds`,
`AnnouncementsEnabled`, `AnnouncementIntervalMinutes`, `AnnouncementTarget` (0 = notification box, 1 = world chat),
`Announcements`.

## GUI
- Server → **Activity**, **Announcements**, **Backups** pages (`Src/Gui/MainForm1.ServerTools.cs`).
- Bottom status bar on every page: peak players today, last auto-save, last backup, next announcement, error count.
  - Click the error count to jump to the first error in the console. Right-click it to reset the count.
  - Click the backup or announcement item to open its page.

## Not included
- Restoring a backup from the GUI. Stop the server and copy the file over `Data/ServerDataBase.db` by hand.
