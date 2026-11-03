using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Server.Services
{
    /// <summary>
    /// Writes player sessions (login/logout) and chat lines to daily CSV files under
    /// Logs/Activity so admins can look up who was online and what was said.
    /// </summary>
    public static class ActivityLog
    {
        private class OpenSession
        {
            public DateTime LoginAt;
            public string Character;
            public string Account;
            public string Ip;
        }

        public class Row
        {
            public string[] Cells;
        }

        public static readonly string[] SessionColumns = { "Time", "Event", "CharID", "Character", "Account", "IP", "Duration" };
        public static readonly string[] ChatColumns = { "Time", "Channel", "CharID", "Character", "Message", "Command" };

        private static readonly object m_FileLock = new object();
        private static readonly ConcurrentDictionary<uint, OpenSession> m_open = new ConcurrentDictionary<uint, OpenSession>();
        private static bool m_initialized;
        private static volatile bool m_closed;

        public static string ActivityDirectory => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs", "Activity");

        public static void Initialize()
        {
            if (m_initialized) return;
            m_initialized = true;
            DataBase.CharacterDataBase.CharacterJoined += OnJoined;
            DataBase.CharacterDataBase.CharacterLeft += OnLeft;
        }

        private static string FileFor(string kind, DateTime day) => Path.Combine(ActivityDirectory, $"{kind}_{day:yyyyMMdd}.csv");

        private static void OnJoined(Game.Player p)
        {
            try
            {
                if (p == null) return;
                ServerMetrics.ObserveOnlinePlayers(cGlobal.gCharacterDataBase?.GetOnlinePlayers()?.Count ?? 0);

                var session = new OpenSession
                {
                    LoginAt = DateTime.Now,
                    Character = p.CharName ?? "",
                    Account = p.UserAccount?.UserName ?? "",
                    Ip = SafeIp(p)
                };
                m_open[p.CharID] = session;
                Append("sessions", SessionColumns, session.LoginAt.ToString("yyyy-MM-dd HH:mm:ss"), "LOGIN", p.CharID.ToString(),
                    session.Character, session.Account, session.Ip, "");
            }
            catch { }
        }

        private static void OnLeft(Game.Player p)
        {
            try
            {
                if (p == null) return;
                DateTime now = DateTime.Now;
                m_open.TryRemove(p.CharID, out OpenSession session);
                if (session == null && m_closed) return; // already written as SHUTDOWN
                string duration = session != null ? FormatDuration(now - session.LoginAt) : "";
                Append("sessions", SessionColumns, now.ToString("yyyy-MM-dd HH:mm:ss"), "LOGOUT", p.CharID.ToString(),
                    session?.Character ?? p.CharName ?? "", session?.Account ?? p.UserAccount?.UserName ?? "",
                    session?.Ip ?? "", duration);
            }
            catch { }
        }

        public static void RecordChat(Game.Player p, string channel, string message)
        {
            try
            {
                if (p == null || message == null) return;
                string trimmed = message.Trim();
                bool isCommand = trimmed.StartsWith(":") || trimmed.StartsWith("/");
                Append("chat", ChatColumns, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"), channel, p.CharID.ToString(),
                    p.CharName ?? "", message, isCommand ? "yes" : "");
            }
            catch { }
        }

        /// <summary>Closes still-open sessions (server shutdown) so durations are not lost.</summary>
        public static void CloseAll()
        {
            m_closed = true;
            DateTime now = DateTime.Now;
            foreach (var kv in m_open.ToArray())
            {
                if (!m_open.TryRemove(kv.Key, out OpenSession s)) continue;
                Append("sessions", SessionColumns, now.ToString("yyyy-MM-dd HH:mm:ss"), "SHUTDOWN", kv.Key.ToString(),
                    s.Character, s.Account, s.Ip, FormatDuration(now - s.LoginAt));
            }
        }

        public static int OpenSessionCount => m_open.Count;

        private static string SafeIp(Game.Player p)
        {
            try
            {
                string addr = p.SockAddress() ?? "";
                int colon = addr.LastIndexOf(':');
                return colon > 0 && addr.IndexOf(':') == colon ? addr.Substring(0, colon) : addr;
            }
            catch
            {
                return "";
            }
        }

        public static string FormatDuration(TimeSpan t)
        {
            if (t < TimeSpan.Zero) t = TimeSpan.Zero;
            return t.TotalHours >= 1 ? $"{(int)t.TotalHours}h {t.Minutes:D2}m" : $"{t.Minutes}m {t.Seconds:D2}s";
        }

        #region CSV

        private static void Append(string kind, string[] header, params string[] cells)
        {
            lock (m_FileLock)
            {
                global::System.IO.Directory.CreateDirectory(ActivityDirectory);
                string file = FileFor(kind, DateTime.Now);
                bool isNew = !File.Exists(file);
                using (var w = new StreamWriter(file, append: true, encoding: new UTF8Encoding(true)))
                {
                    if (isNew) w.WriteLine(string.Join(",", header.Select(Escape)));
                    w.WriteLine(string.Join(",", cells.Select(Escape)));
                }
            }
        }

        private static string Escape(string value)
        {
            value = value ?? "";
            if (value.IndexOfAny(new[] { ',', '"', '\r', '\n' }) < 0) return value;
            return "\"" + value.Replace("\"", "\"\"").Replace("\r", " ").Replace("\n", " ") + "\"";
        }

        /// <summary>Reads one day's file ("sessions" or "chat"); newest rows first.</summary>
        public static List<string[]> Read(string kind, DateTime day)
        {
            var rows = new List<string[]>();
            string file = FileFor(kind, day);
            if (!File.Exists(file)) return rows;

            string[] lines;
            lock (m_FileLock)
            {
                using (var fs = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var r = new StreamReader(fs, Encoding.UTF8))
                {
                    lines = r.ReadToEnd().Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
                }
            }
            foreach (string line in lines.Skip(1)) rows.Add(ParseLine(line));
            rows.Reverse();
            return rows;
        }

        /// <summary>Days that have any activity file, newest first.</summary>
        public static List<DateTime> AvailableDays()
        {
            var days = new HashSet<DateTime>();
            try
            {
                if (global::System.IO.Directory.Exists(ActivityDirectory))
                {
                    foreach (string f in global::System.IO.Directory.GetFiles(ActivityDirectory, "*.csv"))
                    {
                        string name = Path.GetFileNameWithoutExtension(f);
                        int us = name.LastIndexOf('_');
                        if (us > 0 && DateTime.TryParseExact(name.Substring(us + 1), "yyyyMMdd", null,
                            global::System.Globalization.DateTimeStyles.None, out DateTime d))
                        {
                            days.Add(d);
                        }
                    }
                }
            }
            catch { }
            days.Add(DateTime.Today);
            return days.OrderByDescending(d => d).ToList();
        }

        private static string[] ParseLine(string line)
        {
            var cells = new List<string>();
            var sb = new StringBuilder();
            bool quoted = false;
            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (quoted)
                {
                    if (c == '"' && i + 1 < line.Length && line[i + 1] == '"') { sb.Append('"'); i++; }
                    else if (c == '"') quoted = false;
                    else sb.Append(c);
                }
                else if (c == '"') quoted = true;
                else if (c == ',') { cells.Add(sb.ToString()); sb.Clear(); }
                else sb.Append(c);
            }
            cells.Add(sb.ToString());
            return cells.ToArray();
        }

        #endregion
    }
}
