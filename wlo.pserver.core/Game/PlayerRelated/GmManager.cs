using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Network;

namespace Game.PlayerRelated
{
    public static class GmManager
    {
        private static readonly HashSet<string> _gmNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private static readonly object _lock = new object();
        private static readonly string ConfigPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "gm_list.txt");

        public static event Action OnGmListChanged;

        static GmManager()
        {
            Initialize();
        }

        public static void Initialize()
        {
            LoadFromFile();
        }

        public static bool IsGm(Player player)
        {
            if (player == null) return false;

            lock (_lock)
            {
                if (!string.IsNullOrEmpty(player.CharName) && _gmNames.Contains(player.CharName))
                    return true;

                if (player.UserAccount != null)
                {
                    if (player.UserAccount.GMlvl > 0) return true;
                    if (!string.IsNullOrEmpty(player.UserAccount.UserName) && _gmNames.Contains(player.UserAccount.UserName))
                        return true;
                }
            }

            return false;
        }

        public static bool IsGm(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return false;
            lock (_lock)
            {
                return _gmNames.Contains(name);
            }
        }

        public static List<string> GetGmList()
        {
            lock (_lock)
            {
                return _gmNames.OrderBy(n => n).ToList();
            }
        }

        public static bool AddGm(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return false;
            name = name.Trim();

            lock (_lock)
            {
                if (_gmNames.Add(name))
                {
                    SaveToFile();
                    OnGmListChanged?.Invoke();
                    DebugSystem.Write($"[GmManager] Added '{name}' to GM list.");
                    return true;
                }
            }
            return false;
        }

        public static bool RemoveGm(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return false;
            name = name.Trim();

            lock (_lock)
            {
                if (_gmNames.Remove(name))
                {
                    SaveToFile();
                    OnGmListChanged?.Invoke();
                    DebugSystem.Write($"[GmManager] Removed '{name}' from GM list.");
                    return true;
                }
            }
            return false;
        }

        public static void LoadFromFile()
        {
            try
            {
                string dir = Path.GetDirectoryName(ConfigPath);
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

                if (!File.Exists(ConfigPath))
                {
                    // Default GM entries
                    lock (_lock)
                    {
                        _gmNames.Clear();
                        _gmNames.Add("Admin");
                        _gmNames.Add("GM");
                        _gmNames.Add("test");
                    }
                    SaveToFile();
                    return;
                }

                var lines = File.ReadAllLines(ConfigPath, Encoding.UTF8);
                lock (_lock)
                {
                    _gmNames.Clear();
                    foreach (var rawLine in lines)
                    {
                        string line = rawLine.Trim();
                        if (!string.IsNullOrEmpty(line) && !line.StartsWith("#"))
                        {
                            _gmNames.Add(line);
                        }
                    }
                }
                DebugSystem.Write($"[GmManager] Loaded {_gmNames.Count} GM accounts/characters.");
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[GmManager] Error loading GM list: {ex.Message}");
            }
        }

        public static void SaveToFile()
        {
            try
            {
                string dir = Path.GetDirectoryName(ConfigPath);
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

                var sb = new StringBuilder();
                sb.AppendLine("# WLO GM & Administrator List");
                sb.AppendLine("# Format: One CharName or Username per line");
                lock (_lock)
                {
                    foreach (var name in _gmNames.OrderBy(n => n))
                    {
                        sb.AppendLine(name);
                    }
                }
                File.WriteAllText(ConfigPath, sb.ToString(), Encoding.UTF8);
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[GmManager] Error saving GM list: {ex.Message}");
            }
        }
    }
}
