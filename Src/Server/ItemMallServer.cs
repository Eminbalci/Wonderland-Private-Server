using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Game;
using Game.PlayerRelated;

namespace Server
{
    /// <summary>
    /// ItemMallServer: Dedicated TCP Server listening on Port 6416 (WLO Item Mall Catalog Service)
    /// Dispatches authentic binary item catalog expected by aLogin.exe (FUN_0025a684)
    /// </summary>
    public class ItemMallServer
    {
        private TcpListener _listener;
        private Thread _listenThread;
        private bool _isRunning;
        private readonly int _port;

        public ItemMallServer(int port = 6416)
        {
            _port = port;
        }

        public void Start()
        {
            if (_isRunning) return;
            try
            {
                _listener = new TcpListener(IPAddress.Any, _port);
                _listener.Start(20);
                _isRunning = true;
                DebugSystem.Write($"[ItemMallServer] Listening on Port {_port} for client Item Mall queries.");

                _listenThread = new Thread(ListenLoop)
                {
                    IsBackground = true,
                    Name = "Item Mall Port 6416 Listener"
                };
                _listenThread.Start();
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[ItemMallServer] Warning: Could not bind Port {_port}: {ex.Message}");
            }
        }

        public void Stop()
        {
            _isRunning = false;
            try
            {
                _listener?.Stop();
            }
            catch { }
        }

        private void ListenLoop()
        {
            while (_isRunning)
            {
                try
                {
                    TcpClient client = _listener.AcceptTcpClient();
                    ThreadPool.QueueUserWorkItem(HandleClient, client);
                }
                catch
                {
                    if (!_isRunning) break;
                }
            }
        }

        public static byte[] BuildCatalogPayload()
        {
            List<MallItemEntry> catalog = ItemMallManager.GetCatalog();
            using (MemoryStream ms = new MemoryStream())
            using (BinaryWriter bw = new BinaryWriter(ms))
            {
                // Authentic format confirmed by yeniitemmall.pcapng analysis:
                // [0xC9, 0x00, count(1B), ...items(3B each)]
                // Each item: [ItemID_lo, ItemID_hi, val]
                // val: 3=standard, 2=special/hot
                bw.Write((byte)0xC9);   // opcode
                bw.Write((byte)0x00);   // header byte 0 (always 0)
                bw.Write((byte)0x01);   // header byte 1 (always 1 - NOT item count, confirmed by pcap)

                foreach (MallItemEntry item in catalog)
                {
                    bw.Write((ushort)item.ItemID); // ItemID (LE ushort)
                    byte val = (item.IsHot > 0 || item.Badge == 2 || (item.Category ?? "").ToLowerInvariant().Contains("hot") || (item.Category ?? "").ToLowerInvariant().Contains("special")) ? (byte)2 : (byte)3;
                    bw.Write(val);
                }

                return ms.ToArray();
            }
        }

        private void HandleClient(object state)
        {
            TcpClient client = (TcpClient)state;
            try
            {
                using (client)
                using (NetworkStream stream = client.GetStream())
                {
                    stream.ReadTimeout = 5000;
                    stream.WriteTimeout = 5000;

                    // Authentic behavior (confirmed by yeniitemmall.pcapng):
                    // Server writes the raw binary catalog and immediately closes the connection.
                    // The client reads until EOF/FIN to finalize catalog parsing.
                    byte[] catalog = BuildCatalogPayload();
                    stream.Write(catalog, 0, catalog.Length);
                    stream.Flush();

                    try
                    {
                        client.Client.Shutdown(SocketShutdown.Both);
                    }
                    catch { }

                    DebugSystem.Write($"[ItemMallServer] Catalog dispatched ({catalog.Length}B, {ItemMallManager.GetCatalog().Count} items) and socket closed for {client.Client.RemoteEndPoint}.");
                }
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[ItemMallServer] Client error: {ex.Message}");
            }
        }
    }
}
