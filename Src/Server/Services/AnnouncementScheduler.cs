using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Server.Services
{
    /// <summary>
    /// Sends the configured announcement messages to everyone online, one at a time in rotation,
    /// every <c>AnnouncementIntervalMinutes</c>. Settings are re-read on every tick, so edits in the
    /// GUI apply without a restart.
    /// </summary>
    public static class AnnouncementScheduler
    {
        private static Timer m_timer;
        private static int m_nextIndex;
        private static DateTime m_lastSent = DateTime.Now;

        public static DateTime? LastSentAt { get; private set; }
        public static string LastSentText { get; private set; }

        public static DateTime? NextDueAt
        {
            get
            {
                var s = cGlobal.SrvSettings;
                if (s == null || !s.AnnouncementsEnabled || s.Announcements == null || s.Announcements.Count == 0) return null;
                return m_lastSent.AddMinutes(Math.Max(1, s.AnnouncementIntervalMinutes));
            }
        }

        public static void Start()
        {
            if (m_timer != null) return;
            m_lastSent = DateTime.Now;
            m_timer = new Timer(_ => Tick(), null, TimeSpan.FromSeconds(20), TimeSpan.FromSeconds(20));
        }

        public static void Stop()
        {
            m_timer?.Dispose();
            m_timer = null;
        }

        /// <summary>Restart the interval countdown, e.g. after the settings were changed.</summary>
        public static void ResetCountdown()
        {
            m_lastSent = DateTime.Now;
        }

        private static void Tick()
        {
            try
            {
                DateTime? due = NextDueAt;
                if (due == null || DateTime.Now < due.Value) return;

                var messages = cGlobal.SrvSettings.Announcements.Where(m => !string.IsNullOrWhiteSpace(m)).ToList();
                m_lastSent = DateTime.Now;
                if (messages.Count == 0) return;

                if (m_nextIndex >= messages.Count) m_nextIndex = 0;
                string text = messages[m_nextIndex++];
                int sent = Send(text, cGlobal.SrvSettings.AnnouncementTarget);
                if (sent > 0)
                {
                    DebugSystem.Write($"[Announce] Scheduled announcement sent to {sent} player(s): {text}");
                }
            }
            catch (Exception ex)
            {
                DebugSystem.Write(DebugItemType.Error, "[Announce] Scheduled announcement failed: " + ex.Message);
            }
        }

        /// <summary>Sends <paramref name="text"/> to all online players. Returns how many received it.</summary>
        public static int Send(string text, int target)
        {
            if (string.IsNullOrWhiteSpace(text)) return 0;
            List<Game.Player> online = cGlobal.gCharacterDataBase?.GetOnlinePlayers() ?? new List<Game.Player>();
            int sent = 0;
            foreach (var p in online)
            {
                try
                {
                    if (target == 1) WorldServer.SendChatMessage(p, 1, text);
                    else p.SendHeadBanner(text);
                    sent++;
                }
                catch { }
            }
            if (sent > 0)
            {
                LastSentAt = DateTime.Now;
                LastSentText = text;
            }
            return sent;
        }
    }
}
