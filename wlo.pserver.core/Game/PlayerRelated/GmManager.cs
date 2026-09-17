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
        private static string ConfigPath => RCLibrary.Core.PathHelper.GetDataFilePath("gm_list.txt");

        public static event Action OnGmListChanged;

        static GmManager()
        {
            Initialize();
        }

        public static void Initialize()
        {
            LoadFromDatabase();
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

        public static void VerifyTable()
        {
            try
            {
                RCLibrary.Core.DataBase.Execute("CREATE TABLE IF NOT EXISTS gm_accounts (id INTEGER PRIMARY KEY AUTOINCREMENT, name TEXT, username TEXT, added_at TEXT, added_by TEXT);");

                var info = RCLibrary.Core.DataBase.Query("PRAGMA table_info(gm_accounts);");
                var colNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                if (info != null)
                {
                    foreach (System.Data.DataRow row in info.Rows)
                    {
                        colNames.Add(row["name"].ToString());
                    }
                }
                if (!colNames.Contains("name")) RCLibrary.Core.DataBase.Execute("ALTER TABLE gm_accounts ADD COLUMN name TEXT;");
                if (!colNames.Contains("username")) RCLibrary.Core.DataBase.Execute("ALTER TABLE gm_accounts ADD COLUMN username TEXT;");
                if (!colNames.Contains("added_at")) RCLibrary.Core.DataBase.Execute("ALTER TABLE gm_accounts ADD COLUMN added_at TEXT;");
                if (!colNames.Contains("added_by")) RCLibrary.Core.DataBase.Execute("ALTER TABLE gm_accounts ADD COLUMN added_by TEXT;");

                RCLibrary.Core.DataBase.Execute("UPDATE gm_accounts SET name = username WHERE (name IS NULL OR name = '') AND username IS NOT NULL;");
                RCLibrary.Core.DataBase.Execute("UPDATE gm_accounts SET username = name WHERE (username IS NULL OR username = '') AND name IS NOT NULL;");
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[GmManager] Error verifying gm_accounts table: {ex.Message}");
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
                    VerifyTable();
                    string now = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                    RCLibrary.Core.DataBase.Execute($"INSERT INTO gm_accounts (name, username, added_at, added_by) VALUES ('{name.Replace("'", "''")}', '{name.Replace("'", "''")}', '{now}', 'ServerAdmin');");
                    OnGmListChanged?.Invoke();
                    DebugSystem.Write($"[GmManager] Added '{name}' to GM list database.");
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
                    VerifyTable();
                    RCLibrary.Core.DataBase.Execute($"DELETE FROM gm_accounts WHERE name = '{name.Replace("'", "''")}' OR username = '{name.Replace("'", "''")}';");
                    OnGmListChanged?.Invoke();
                    DebugSystem.Write($"[GmManager] Removed '{name}' from GM list database.");
                    return true;
                }
            }
            return false;
        }

        public static void LoadFromFile() => LoadFromDatabase();
        public static void SaveToFile() { }

        public static void LoadFromDatabase()
        {
            try
            {
                VerifyTable();
                var dt = RCLibrary.Core.DataBase.Query("SELECT COALESCE(name, username) AS name FROM gm_accounts;");
                if (dt == null || dt.Rows.Count == 0)
                {
                    // Seed defaults
                    var defaults = new List<string> { "admin", "developer", "Admin", "gmone", "GM", "test" };
                    if (File.Exists(ConfigPath))
                    {
                        var lines = File.ReadAllLines(ConfigPath, Encoding.UTF8);
                        foreach (var rawLine in lines)
                        {
                            string line = rawLine.Trim();
                            if (!string.IsNullOrEmpty(line) && !line.StartsWith("#"))
                            {
                                if (!defaults.Contains(line, StringComparer.OrdinalIgnoreCase)) defaults.Add(line);
                            }
                        }
                    }

                    string now = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                    foreach (var gm in defaults)
                    {
                        RCLibrary.Core.DataBase.Execute($"INSERT INTO gm_accounts (name, username, added_at, added_by) VALUES ('{gm.Replace("'", "''")}', '{gm.Replace("'", "''")}', '{now}', 'System');");
                    }

                    dt = RCLibrary.Core.DataBase.Query("SELECT COALESCE(name, username) AS name FROM gm_accounts;");
                }

                lock (_lock)
                {
                    _gmNames.Clear();
                    if (dt != null)
                    {
                        foreach (System.Data.DataRow row in dt.Rows)
                        {
                            string n = row["name"]?.ToString()?.Trim();
                            if (!string.IsNullOrEmpty(n)) _gmNames.Add(n);
                        }
                    }
                }
                DebugSystem.Write($"[GmManager] Loaded {_gmNames.Count} GM accounts/characters from database.");
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[GmManager] Error loading GM list: {ex.Message}");
            }
        }
    }
}
