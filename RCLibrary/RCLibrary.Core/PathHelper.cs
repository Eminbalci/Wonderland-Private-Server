using System;
using System.IO;
using System.Linq;

namespace RCLibrary.Core
{
    /// <summary>
    /// Provides dynamic path resolution for game assets, databases, and client executables
    /// across different host machines, directories, and execution environments.
    /// </summary>
    public static class PathHelper
    {
        private static string _cachedDataDir = null;
        private static string _cachedClientDir = null;
        private static readonly object _lock = new object();

        /// <summary>
        /// Gets the detected project root directory (where Wonderland Private Server.sln or Data/ resides).
        /// </summary>
        public static string AppRootDirectory
        {
            get
            {
                string current = AppDomain.CurrentDomain.BaseDirectory;
                for (int i = 0; i < 5; i++)
                {
                    if (string.IsNullOrEmpty(current) || !Directory.Exists(current)) break;
                    if (File.Exists(Path.Combine(current, "Wonderland Private Server.sln")) ||
                        Directory.Exists(Path.Combine(current, "Data")))
                    {
                        return Path.GetFullPath(current);
                    }
                    var parent = Directory.GetParent(current);
                    if (parent == null) break;
                    current = parent.FullName;
                }

                current = Directory.GetCurrentDirectory();
                for (int i = 0; i < 5; i++)
                {
                    if (string.IsNullOrEmpty(current) || !Directory.Exists(current)) break;
                    if (File.Exists(Path.Combine(current, "Wonderland Private Server.sln")) ||
                        Directory.Exists(Path.Combine(current, "Data")))
                    {
                        return Path.GetFullPath(current);
                    }
                    var parent = Directory.GetParent(current);
                    if (parent == null) break;
                    current = parent.FullName;
                }

                return AppDomain.CurrentDomain.BaseDirectory;
            }
        }

        /// <summary>
        /// Dynamically locates the active Data directory containing server DAT files.
        /// Searches BaseDirectory, parent folders, current working directory, and known targets.
        /// </summary>
        public static string DataDirectory
        {
            get
            {
                lock (_lock)
                {
                    if (!string.IsNullOrEmpty(_cachedDataDir) && Directory.Exists(_cachedDataDir))
                        return _cachedDataDir;

                    // 1. Check custom override file if exists (data_path.txt)
                    string overrideFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data_path.txt");
                    if (!File.Exists(overrideFile))
                    {
                        overrideFile = Path.Combine(AppRootDirectory, "data_path.txt");
                    }
                    if (File.Exists(overrideFile))
                    {
                        try
                        {
                            string line = File.ReadAllText(overrideFile).Trim();
                            if (!string.IsNullOrEmpty(line))
                            {
                                string resolved = Path.IsPathRooted(line) ? line : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, line);
                                if (Directory.Exists(resolved))
                                {
                                    _cachedDataDir = Path.GetFullPath(resolved);
                                    return _cachedDataDir;
                                }
                            }
                        }
                        catch { }
                    }

                    // 2. Scan candidate locations
                    string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                    string currentDir = Directory.GetCurrentDirectory();

                    string[] candidates = new string[]
                    {
                        Path.Combine(baseDir, "Data"),
                        Path.Combine(baseDir, "..", "Data"),
                        Path.Combine(baseDir, "..", "..", "Data"),
                        Path.Combine(baseDir, "..", "..", "..", "Data"),
                        Path.Combine(currentDir, "Data"),
                        Path.Combine(currentDir, "..", "Data"),
                        Path.Combine(currentDir, "..", "..", "Data"),
                        Path.Combine(baseDir, "bin", "Debug", "Data"),
                        Path.Combine(baseDir, "bin", "Release", "Data")
                    };

                    foreach (var path in candidates)
                    {
                        try
                        {
                            string full = Path.GetFullPath(path);
                            if (Directory.Exists(full))
                            {
                                // Verify this folder contains essential DAT files
                                if (File.Exists(Path.Combine(full, "Npc.dat")) ||
                                    File.Exists(Path.Combine(full, "Talk.dat")) ||
                                    File.Exists(Path.Combine(full, "Item.dat")) ||
                                    File.Exists(Path.Combine(full, "eve.Emg")) ||
                                    File.Exists(Path.Combine(full, "Ground.MMG")))
                                {
                                    _cachedDataDir = full;
                                    return _cachedDataDir;
                                }
                            }
                        }
                        catch { }
                    }

                    // Fallback to BaseDirectory/Data
                    _cachedDataDir = Path.GetFullPath(Path.Combine(baseDir, "Data"));
                    return _cachedDataDir;
                }
            }
        }

        /// <summary>
        /// Resolves the absolute path for a specific data file within the detected Data directory
        /// or from dynamic fallback paths.
        /// </summary>
        public static string GetDataFilePath(string fileName)
        {
            if (string.IsNullOrEmpty(fileName)) return fileName;

            // Direct check in primary DataDirectory
            string primary = Path.Combine(DataDirectory, fileName);
            if (File.Exists(primary)) return primary;

            // Secondary check across candidate directories
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string currentDir = Directory.GetCurrentDirectory();

            string[] candidates = new string[]
            {
                primary,
                Path.Combine(baseDir, fileName),
                Path.Combine(baseDir, "Data", fileName),
                Path.Combine(baseDir, "..", "..", "Data", fileName),
                Path.Combine(baseDir, "..", "Data", fileName),
                Path.Combine(currentDir, fileName),
                Path.Combine(currentDir, "Data", fileName),
                Path.Combine(currentDir, "..", "..", "Data", fileName),
                Path.Combine(baseDir, "bin", "Debug", "Data", fileName),
                Path.Combine(baseDir, "bin", "Release", "Data", fileName)
            };

            foreach (var candidate in candidates)
            {
                try
                {
                    if (File.Exists(candidate))
                        return Path.GetFullPath(candidate);
                }
                catch { }
            }

            return primary;
        }

        /// <summary>
        /// Dynamically locates the SQLite database file (ServerDataBase.db), prioritizing genuine populated databases.
        /// </summary>
        public static string ResolveDatabaseFile(string defaultFileName = "ServerDataBase.db")
        {
            // 1. Check database.override.txt if custom File is provided
            string configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "database.override.txt");
            if (!File.Exists(configPath))
            {
                configPath = Path.Combine(AppRootDirectory, "database.override.txt");
            }
            if (File.Exists(configPath))
            {
                try
                {
                    foreach (var line in File.ReadAllLines(configPath))
                    {
                        if (string.IsNullOrWhiteSpace(line) || !line.Contains("|")) continue;
                        var parts = line.Split('|');
                        if (parts[0].Trim().Equals("File", StringComparison.OrdinalIgnoreCase) && parts.Length > 1)
                        {
                            string customFile = parts[1].Trim();
                            return Path.IsPathRooted(customFile) ? customFile : Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, customFile));
                        }
                    }
                }
                catch { }
            }

            // 2. Search candidate locations
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string currentDir = Directory.GetCurrentDirectory();
            string dataDir = DataDirectory;

            string[] candidates = new string[]
            {
                Path.Combine(baseDir, defaultFileName),
                Path.Combine(dataDir, defaultFileName),
                Path.Combine(baseDir, "..", "..", defaultFileName),
                Path.Combine(baseDir, "..", "..", "Data", defaultFileName),
                Path.Combine(baseDir, "bin", "Debug", defaultFileName),
                Path.Combine(baseDir, "bin", "Debug", "Data", defaultFileName),
                Path.Combine(currentDir, defaultFileName),
                Path.Combine(currentDir, "Data", defaultFileName)
            };

            // Look for existing database with non-trivial size (>10KB)
            foreach (var candidate in candidates)
            {
                try
                {
                    if (File.Exists(candidate))
                    {
                        var fi = new FileInfo(candidate);
                        if (fi.Length > 10000)
                        {
                            return Path.GetFullPath(candidate);
                        }
                    }
                }
                catch { }
            }

            // Fallback: any existing candidate
            foreach (var candidate in candidates)
            {
                try
                {
                    if (File.Exists(candidate))
                        return Path.GetFullPath(candidate);
                }
                catch { }
            }

            return Path.Combine(baseDir, defaultFileName);
        }

        /// <summary>
        /// Gets or sets the configured client directory (WLRI / Wonderland Online client).
        /// Persists configuration to client_path.txt when set.
        /// </summary>
        public static string ClientDirectory
        {
            get
            {
                lock (_lock)
                {
                    if (!string.IsNullOrEmpty(_cachedClientDir) && Directory.Exists(_cachedClientDir))
                        return _cachedClientDir;

                    // 1. Check persistent config file: client_path.txt
                    string configPath = GetClientConfigPath();
                    if (File.Exists(configPath))
                    {
                        try
                        {
                            string saved = File.ReadAllText(configPath).Trim();
                            if (!string.IsNullOrEmpty(saved))
                            {
                                string resolved = Path.IsPathRooted(saved) ? saved : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, saved);
                                if (File.Exists(resolved) && Path.GetFileName(resolved).Equals("aLogin.exe", StringComparison.OrdinalIgnoreCase))
                                {
                                    resolved = Path.GetDirectoryName(resolved);
                                }
                                if (Directory.Exists(resolved))
                                {
                                    _cachedClientDir = Path.GetFullPath(resolved);
                                    return _cachedClientDir;
                                }
                            }
                        }
                        catch { }
                    }

                    // 2. Search dynamic relative candidates
                    string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                    string currentDir = Directory.GetCurrentDirectory();

                    string[] relativeCandidates = new string[]
                    {
                        Path.Combine(baseDir, "WLRI"),
                        Path.Combine(baseDir, "Client"),
                        Path.Combine(baseDir, "..", "WLRI"),
                        Path.Combine(baseDir, "..", "Client"),
                        Path.Combine(baseDir, "..", "..", "WLRI"),
                        Path.Combine(baseDir, "..", "..", "Client"),
                        Path.Combine(baseDir, "..", "..", "..", "WLRI"),
                        Path.Combine(baseDir, "..", "..", "..", "Client"),
                        Path.Combine(currentDir, "WLRI"),
                        Path.Combine(currentDir, "Client"),
                        Path.Combine(currentDir, "..", "WLRI"),
                        Path.Combine(currentDir, "..", "Client"),
                        @"C:\Games\WLRI",
                        @"D:\Games\WLRI",
                        @"E:\Games\WLRI",
                        @"C:\WLRI",
                        @"D:\WLRI",
                        @"E:\WLRI",
                        @"C:\Program Files\Wonderland Online",
                        @"C:\Program Files (x86)\Wonderland Online"
                    };

                    foreach (var candidate in relativeCandidates)
                    {
                        try
                        {
                            if (Directory.Exists(candidate) && File.Exists(Path.Combine(candidate, "aLogin.exe")))
                            {
                                _cachedClientDir = Path.GetFullPath(candidate);
                                SaveClientDirectory(_cachedClientDir);
                                return _cachedClientDir;
                            }
                        }
                        catch { }
                    }

                    // Check if aLogin.exe exists directly in BaseDirectory
                    if (File.Exists(Path.Combine(baseDir, "aLogin.exe")))
                    {
                        _cachedClientDir = Path.GetFullPath(baseDir);
                        return _cachedClientDir;
                    }

                    return null;
                }
            }
            set
            {
                lock (_lock)
                {
                    if (string.IsNullOrEmpty(value))
                    {
                        _cachedClientDir = null;
                        return;
                    }
                    string dir = value;
                    if (File.Exists(dir) && Path.GetFileName(dir).Equals("aLogin.exe", StringComparison.OrdinalIgnoreCase))
                    {
                        dir = Path.GetDirectoryName(dir);
                    }
                    if (Directory.Exists(dir))
                    {
                        _cachedClientDir = Path.GetFullPath(dir);
                        SaveClientDirectory(_cachedClientDir);
                    }
                }
            }
        }

        /// <summary>
        /// Gets the absolute path to aLogin.exe if available.
        /// </summary>
        public static string GetClientExecutablePath()
        {
            string dir = ClientDirectory;
            if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
            {
                string exe = Path.Combine(dir, "aLogin.exe");
                if (File.Exists(exe)) return exe;
            }
            return null;
        }

        private static string GetClientConfigPath()
        {
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "client_path.txt");
        }

        private static void SaveClientDirectory(string dir)
        {
            try
            {
                string configPath = GetClientConfigPath();
                File.WriteAllText(configPath, dir);
            }
            catch { }
        }
    }
}
