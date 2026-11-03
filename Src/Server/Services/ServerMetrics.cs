using System;
using System.Threading;

namespace Server.Services
{
    /// <summary>
    /// Lightweight runtime counters shown on the dashboard: auto-save timing,
    /// daily player peak, error count and last backup.
    /// </summary>
    public static class ServerMetrics
    {
        private static readonly object m_Lock = new object();
        private static int m_errorCount;
        private static DateTime m_peakDay = DateTime.Today;
        private static bool m_initialized;

        public static DateTime StartedAt { get; } = DateTime.Now;

        // Auto-save
        public static DateTime? LastAutoSave { get; private set; }
        public static double LastAutoSaveMs { get; private set; }
        public static int LastAutoSavePlayers { get; private set; }
        public static double SlowestAutoSaveMs { get; private set; }

        // Players
        public static int PeakPlayersToday { get; private set; }
        public static DateTime? PeakPlayersAt { get; private set; }

        // Errors
        public static int ErrorCount => m_errorCount;
        public static string FirstErrorText { get; private set; }

        // Backups
        public static DateTime? LastBackup { get; private set; }
        public static string LastBackupFile { get; private set; }
        public static string LastBackupError { get; private set; }

        public static void Initialize()
        {
            if (m_initialized) return;
            m_initialized = true;
            DebugSystem.LineWritten += OnLogLine;
        }

        private static void OnLogLine(DebugItemType type, string msg)
        {
            if (!IsErrorLine(type, msg)) return;
            if (Interlocked.Increment(ref m_errorCount) == 1)
            {
                FirstErrorText = msg;
            }
        }

        /// <summary>True for lines logged as errors, or plain lines that clearly report one.</summary>
        public static bool IsErrorLine(DebugItemType type, string msg)
        {
            if (type == DebugItemType.Error) return true;
            if (string.IsNullOrEmpty(msg)) return false;
            return msg.IndexOf("[ERROR]", StringComparison.OrdinalIgnoreCase) >= 0
                || msg.IndexOf("Error:", StringComparison.Ordinal) >= 0
                || msg.IndexOf("Exception", StringComparison.Ordinal) >= 0;
        }

        public static void ResetErrors()
        {
            Interlocked.Exchange(ref m_errorCount, 0);
            FirstErrorText = null;
        }

        public static void RecordAutoSave(int players, double elapsedMs)
        {
            lock (m_Lock)
            {
                LastAutoSave = DateTime.Now;
                LastAutoSaveMs = elapsedMs;
                LastAutoSavePlayers = players;
                if (elapsedMs > SlowestAutoSaveMs) SlowestAutoSaveMs = elapsedMs;
            }
        }

        public static void ObserveOnlinePlayers(int count)
        {
            lock (m_Lock)
            {
                if (DateTime.Today != m_peakDay)
                {
                    m_peakDay = DateTime.Today;
                    PeakPlayersToday = 0;
                    PeakPlayersAt = null;
                }
                if (count > PeakPlayersToday)
                {
                    PeakPlayersToday = count;
                    PeakPlayersAt = DateTime.Now;
                }
            }
        }

        public static void RecordBackup(string file, string error)
        {
            lock (m_Lock)
            {
                if (error == null)
                {
                    LastBackup = DateTime.Now;
                    LastBackupFile = file;
                    LastBackupError = null;
                }
                else
                {
                    LastBackupError = error;
                }
            }
        }
    }
}
