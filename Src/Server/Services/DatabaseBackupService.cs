using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using RCLibrary.Core;

namespace Server.Services
{
    /// <summary>
    /// Periodic, consistent snapshots of the SQLite database using SQLite's online backup API,
    /// so a copy can be taken while the server keeps writing. Backups go to Data/Backups next to
    /// the database file; only the newest <c>BackupKeepCount</c> files are kept.
    /// The live database is only opened for reading here.
    /// </summary>
    public static class DatabaseBackupService
    {
        private const string FilePrefix = "ServerDataBase_";
        private static readonly object m_Lock = new object();
        private static Thread m_thread;
        private static volatile bool m_stop;

        public static bool IsRunning => m_thread != null && m_thread.IsAlive;

        /// <summary>Resolves the live SQLite file. False for MySQL/SQL Server setups.</summary>
        public static bool TryGetDatabaseFile(out string file)
        {
            file = null;
            var db = cGlobal.gCharacterDataBase;
            if (db == null || db.DatabaseType != DataBaseTypes.Sqlite) return false;

            Match m = Regex.Match(db.Connection_String ?? "", @"Data Source=([^;]+)", RegexOptions.IgnoreCase);
            if (!m.Success) return false;
            file = Path.GetFullPath(m.Groups[1].Value.Trim());
            return File.Exists(file);
        }

        public static string BackupDirectory
        {
            get
            {
                if (TryGetDatabaseFile(out string file))
                {
                    return Path.Combine(Path.GetDirectoryName(file), "Backups");
                }
                return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "Backups");
            }
        }

        public static List<FileInfo> ListBackups()
        {
            try
            {
                var dir = new DirectoryInfo(BackupDirectory);
                if (!dir.Exists) return new List<FileInfo>();
                return dir.GetFiles(FilePrefix + "*.db").OrderByDescending(f => f.CreationTime).ToList();
            }
            catch
            {
                return new List<FileInfo>();
            }
        }

        /// <summary>Takes one backup now. Returns the created file, or null (reason in <paramref name="error"/>).</summary>
        public static string BackupNow(string reason, out string error)
        {
            error = null;
            lock (m_Lock)
            {
                try
                {
                    if (!TryGetDatabaseFile(out string source))
                    {
                        error = "Backups are only available for the SQLite database.";
                        return null;
                    }

                    string dir = BackupDirectory;
                    Directory.CreateDirectory(dir);
                    string target = Path.Combine(dir, $"{FilePrefix}{DateTime.Now:yyyyMMdd_HHmmss}.db");
                    string temp = target + ".partial";

                    var started = DateTime.Now;
                    using (var src = new SQLiteConnection($"Data Source={source};Version=3;Read Only=True;"))
                    using (var dst = new SQLiteConnection($"Data Source={temp};Version=3;"))
                    {
                        src.Open();
                        dst.Open();
                        src.BackupDatabase(dst, "main", "main", -1, null, 0);
                    }
                    SQLiteConnection.ClearAllPools();

                    if (File.Exists(target)) File.Delete(target);
                    File.Move(temp, target);
                    Prune(dir);

                    long kb = new FileInfo(target).Length / 1024;
                    DebugSystem.Write($"[Backup] Database backup created ({reason}): {Path.GetFileName(target)} ({kb:N0} KB, {(DateTime.Now - started).TotalMilliseconds:N0} ms)");
                    ServerMetrics.RecordBackup(target, null);
                    return target;
                }
                catch (Exception ex)
                {
                    error = ex.Message;
                    DebugSystem.Write(DebugItemType.Error, "[Backup] Database backup failed: " + ex.Message);
                    ServerMetrics.RecordBackup(null, ex.Message);
                    return null;
                }
            }
        }

        private static void Prune(string dir)
        {
            int keep = Math.Max(1, cGlobal.SrvSettings?.BackupKeepCount ?? 10);
            try
            {
                foreach (var old in new DirectoryInfo(dir).GetFiles(FilePrefix + "*.db")
                    .OrderByDescending(f => f.CreationTime).Skip(keep))
                {
                    try { old.Delete(); } catch { }
                }
                foreach (var partial in new DirectoryInfo(dir).GetFiles("*.partial"))
                {
                    try { partial.Delete(); } catch { }
                }
            }
            catch { }
        }

        public static void Start()
        {
            if (IsRunning) return;
            m_stop = false;
            m_thread = new Thread(Loop) { IsBackground = true, Name = "Database Backup Thread" };
            m_thread.Start();
        }

        public static void Stop()
        {
            m_stop = true;
        }

        private static void Loop()
        {
            // Let the server finish booting before the first snapshot.
            if (Sleep(TimeSpan.FromSeconds(30))) return;

            DateTime next = DateTime.Now;
            while (!m_stop)
            {
                var settings = cGlobal.SrvSettings;
                bool enabled = settings == null || settings.BackupEnabled;
                int minutes = Math.Max(5, settings?.BackupIntervalMinutes ?? 30);

                if (enabled && DateTime.Now >= next)
                {
                    if (TryGetDatabaseFile(out _))
                    {
                        BackupNow("auto", out _);
                    }
                    else
                    {
                        DebugSystem.Write("[Backup] Skipped: automatic backups only support the SQLite database.");
                        return;
                    }
                    next = DateTime.Now.AddMinutes(minutes);
                }

                if (Sleep(TimeSpan.FromSeconds(15))) return;
            }
        }

        /// <summary>Sleeps in small steps; returns true when the service is stopping.</summary>
        private static bool Sleep(TimeSpan span)
        {
            var until = DateTime.Now + span;
            while (DateTime.Now < until)
            {
                if (m_stop) return true;
                Thread.Sleep(500);
            }
            return m_stop;
        }
    }
}
