using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using RCLibrary.Core;

namespace Server
{
    public enum ServerLoadColor : byte
    {
        Offline = 0,
        Green = 1,   // Green (Smooth / Empty)
        Yellow = 2,  // Yellow (Crowded)
        Red = 3,     // Red (Full)
        Auto = 255   // Automatic based on online player count
    }

    public static class ServerStatusManager
    {
        private static readonly object _lock = new object();
        private static TcpListener _listener;
        private static Thread _listenThread;
        private static bool _isRunning = false;

        public static byte ClusterId { get; set; } = 1;
        public static ushort ServerId { get; set; } = 1;
        public static ServerLoadColor CurrentMode { get; set; } = ServerLoadColor.Green;
        public static double ExpRate { get; set; } = 1.0;
        public static double DropRate { get; set; } = 1.0;
        public static double GoldRate { get; set; } = 1.0;
        public static int MaxPlayers { get; set; } = 500;

        public static Func<int> OnlinePlayerCountProvider { get; set; }
        public static event Action OnStatusChanged;

        private static string ConfigPath => RCLibrary.Core.PathHelper.GetDataFilePath("server_status.txt");

        public static void Initialize(int port = 6416)
        {
            LoadConfig();
            StartListener(port);
        }

        public static void StartListener(int port = 6416)
        {
            lock (_lock)
            {
                if (_isRunning) return;
                try
                {
                    _listener = new TcpListener(IPAddress.Any, port);
                    _listener.Start(20);
                    _isRunning = true;
                    _listenThread = new Thread(ListenLoop)
                    {
                        IsBackground = true,
                        Name = "ServerStatusListener_6416"
                    };
                    _listenThread.Start();
                    DebugSystem.Write($"[ServerStatusManager] Server List Status Service listening on Port {port}.");
                }
                catch (Exception ex)
                {
                    DebugSystem.Write($"[ServerStatusManager] Failed to start Port {port} listener: {ex.Message}");
                }
            }
        }

        public static void StopListener()
        {
            lock (_lock)
            {
                _isRunning = false;
                try { _listener?.Stop(); } catch { }
            }
        }

        private static void ListenLoop()
        {
            while (_isRunning)
            {
                try
                {
                    if (_listener == null) break;
                    var client = _listener.AcceptTcpClient();
                    ThreadPool.QueueUserWorkItem(HandleStatusRequest, client);
                }
                catch
                {
                    if (!_isRunning) break;
                    Thread.Sleep(50);
                }
            }
        }

        private static void HandleStatusRequest(object state)
        {
            var client = state as TcpClient;
            if (client == null) return;

            try
            {
                using (client)
                using (var stream = client.GetStream())
                {
                    byte[] packet = BuildStatusPacket();
                    stream.Write(packet, 0, packet.Length);
                    stream.Flush();
                }
            }
            catch { }
        }

        /// <summary>
        /// Builds the authentic 0xC9 status packet for the client server selection window.
        /// Sends status for both ServerId (1) and subserver 101/Rhodes1 aliases so any client configuration lights up properly.
        /// </summary>
        public static byte[] BuildStatusPacket()
        {
            lock (_lock)
            {
                using (var ms = new MemoryStream())
                using (var bw = new BinaryWriter(ms))
                {
                    bw.Write((byte)0xC9);      // Opcode 201
                    bw.Write((byte)0x00);      // Sub-type
                    bw.Write((byte)ClusterId); // Cluster ID (1)

                    byte colorByte;
                    if (CurrentMode == ServerLoadColor.Auto)
                    {
                        int onlineCount = OnlinePlayerCountProvider != null ? OnlinePlayerCountProvider() : 0;
                        if (onlineCount < 10) colorByte = (byte)ServerLoadColor.Green;
                        else if (onlineCount < 30) colorByte = (byte)ServerLoadColor.Yellow;
                        else colorByte = (byte)ServerLoadColor.Red;
                    }
                    else
                    {
                        colorByte = (byte)CurrentMode;
                    }

                    // Write Primary Server ID (e.g. 1)
                    bw.Write((ushort)ServerId);
                    bw.Write((byte)colorByte);

                    // Also write 101 alias if ServerId is not 101, for maximum compatibility with all client versions
                    if (ServerId != 101)
                    {
                        bw.Write((ushort)101);
                        bw.Write((byte)colorByte);
                    }

                    return ms.ToArray();
                }
            }
        }

        public static void SetMode(ServerLoadColor color)
        {
            lock (_lock)
            {
                CurrentMode = color;
            }
            SaveConfig();
            OnStatusChanged?.Invoke();
        }

        public static void VerifyTable()
        {
            try
            {
                RCLibrary.Core.DataBase.Execute("CREATE TABLE IF NOT EXISTS server_settings (key TEXT PRIMARY KEY, value TEXT);");
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[ServerStatusManager] Error verifying server_settings table: {ex.Message}");
            }
        }

        public static void SaveConfig()
        {
            try
            {
                VerifyTable();
                lock (_lock)
                {
                    RCLibrary.Core.DataBase.Execute($"INSERT OR REPLACE INTO server_settings (key, value) VALUES ('CLUSTER', '{ClusterId}');");
                    RCLibrary.Core.DataBase.Execute($"INSERT OR REPLACE INTO server_settings (key, value) VALUES ('SERVER_ID', '{ServerId}');");
                    RCLibrary.Core.DataBase.Execute($"INSERT OR REPLACE INTO server_settings (key, value) VALUES ('MODE', '{(byte)CurrentMode}');");
                    RCLibrary.Core.DataBase.Execute($"INSERT OR REPLACE INTO server_settings (key, value) VALUES ('EXP_RATE', '{ExpRate}');");
                    RCLibrary.Core.DataBase.Execute($"INSERT OR REPLACE INTO server_settings (key, value) VALUES ('DROP_RATE', '{DropRate}');");
                    RCLibrary.Core.DataBase.Execute($"INSERT OR REPLACE INTO server_settings (key, value) VALUES ('GOLD_RATE', '{GoldRate}');");
                    RCLibrary.Core.DataBase.Execute($"INSERT OR REPLACE INTO server_settings (key, value) VALUES ('MAX_PLAYERS', '{MaxPlayers}');");
                }
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[ServerStatusManager] Error saving settings to database: {ex.Message}");
            }
        }

        public static void LoadConfig()
        {
            try
            {
                VerifyTable();
                var dt = RCLibrary.Core.DataBase.Query("SELECT key, value FROM server_settings");
                if (dt == null || dt.Rows.Count == 0)
                {
                    // Fallback check server_status.txt if exists
                    if (File.Exists(ConfigPath))
                    {
                        var lines = File.ReadAllLines(ConfigPath, Encoding.UTF8);
                        foreach (var rawLine in lines)
                        {
                            string line = rawLine.Trim();
                            if (string.IsNullOrEmpty(line) || line.StartsWith("#")) continue;
                            var parts = line.Split('=');
                            if (parts.Length == 2)
                            {
                                string key = parts[0].Trim().ToUpper();
                                string val = parts[1].Trim();
                                if (key == "CLUSTER" && byte.TryParse(val, out byte cId)) ClusterId = cId;
                                else if (key == "SERVER_ID" && ushort.TryParse(val, out ushort sId)) ServerId = sId;
                                else if (key == "MODE" && byte.TryParse(val, out byte modeVal)) CurrentMode = (ServerLoadColor)modeVal;
                            }
                        }
                    }

                    // Seed database
                    SaveConfig();
                    return;
                }

                lock (_lock)
                {
                    foreach (System.Data.DataRow row in dt.Rows)
                    {
                        string key = row["key"]?.ToString()?.ToUpper();
                        string val = row["value"]?.ToString();
                        if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(val)) continue;

                        if (key == "CLUSTER" && byte.TryParse(val, out byte cId)) ClusterId = cId;
                        else if (key == "SERVER_ID" && ushort.TryParse(val, out ushort sId)) ServerId = sId;
                        else if (key == "MODE" && byte.TryParse(val, out byte modeVal)) CurrentMode = (ServerLoadColor)modeVal;
                        else if (key == "EXP_RATE" && double.TryParse(val, out double exp)) ExpRate = exp;
                        else if (key == "DROP_RATE" && double.TryParse(val, out double drop)) DropRate = drop;
                        else if (key == "GOLD_RATE" && double.TryParse(val, out double gold)) GoldRate = gold;
                        else if (key == "MAX_PLAYERS" && int.TryParse(val, out int max)) MaxPlayers = max;
                    }
                }
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[ServerStatusManager] Error loading settings from database: {ex.Message}");
            }
        }
    }
}
