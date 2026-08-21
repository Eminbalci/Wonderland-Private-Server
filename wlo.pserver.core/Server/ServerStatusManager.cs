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
        Green = 1,   // Yeşil (Boş / Smooth)
        Yellow = 2,  // Sarı (Kalabalık / Crowded)
        Red = 3,     // Kırmızı (Dolu / Full)
        Auto = 255   // Online sayısına göre otomatik
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

        public static Func<int> OnlinePlayerCountProvider { get; set; }
        public static event Action OnStatusChanged;

        private static string ConfigPath => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "server_status.txt");

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

        public static void SaveConfig()
        {
            try
            {
                string dir = Path.GetDirectoryName(ConfigPath);
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

                var sb = new StringBuilder();
                sb.AppendLine("# WLO Single Server Status Color Configuration (Port 6416)");
                sb.AppendLine("# 1=Green (Yeşil), 2=Yellow (Sarı), 3=Red (Kırmızı), 0=Offline, 255=Auto");
                sb.AppendLine($"CLUSTER={ClusterId}");
                sb.AppendLine($"SERVER_ID={ServerId}");
                sb.AppendLine($"MODE={(byte)CurrentMode}");

                File.WriteAllText(ConfigPath, sb.ToString(), Encoding.UTF8);
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[ServerStatusManager] Error saving config: {ex.Message}");
            }
        }

        public static void LoadConfig()
        {
            if (!File.Exists(ConfigPath))
            {
                SaveConfig();
                return;
            }

            try
            {
                var lines = File.ReadAllLines(ConfigPath, Encoding.UTF8);
                lock (_lock)
                {
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
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[ServerStatusManager] Error loading config: {ex.Message}");
            }
        }
    }
}
