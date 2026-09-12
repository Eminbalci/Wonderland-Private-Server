using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Game;
using Game.PlayerRelated;

namespace Network
{
    public static class EmbeddedWebServer
    {
        private static HttpListener _listener;
        private static Thread _serverThread;
        private static bool _isRunning = false;
        private static int _activePort = 8080;

        public static int Port => _activePort;
        public static bool IsRunning => _isRunning;

        public static void Start(int preferredPort = 8080)
        {
            if (_isRunning) return;

            int[] portsToTry = new int[] { preferredPort, 8080, 8088, 8888, 80 };

            foreach (var port in portsToTry)
            {
                try
                {
                    _listener = new HttpListener();
                    _listener.Prefixes.Add($"http://127.0.0.1:{port}/");
                    _listener.Prefixes.Add($"http://localhost:{port}/");
                    _listener.Start();
                    _activePort = port;
                    _isRunning = true;
                    DebugSystem.Write($"[EmbeddedWebServer] Item Mall Web Server started successfully on port {_activePort}.");
                    break;
                }
                catch (Exception ex)
                {
                    DebugSystem.Write($"[EmbeddedWebServer] Port {port} unavailable: {ex.Message}");
                    _listener?.Close();
                    _listener = null;
                }
            }

            if (!_isRunning || _listener == null)
            {
                DebugSystem.Write("[EmbeddedWebServer] Could not bind any HTTP port for Item Mall web server.");
                return;
            }

            // Update client web0.DAT to point to our local server
            PatchClientWebDat(_activePort);

            _serverThread = new Thread(ListenLoop)
            {
                IsBackground = true,
                Name = "Embedded Web Server"
            };
            _serverThread.Start();
        }

        public static void Stop()
        {
            _isRunning = false;
            try
            {
                _listener?.Stop();
                _listener?.Close();
            }
            catch { }
        }

        private static void PatchClientWebDat(int port)
        {
            try
            {
                var candidateDirs = new System.Collections.Generic.List<string>();
                string configuredClientDir = RCLibrary.Core.PathHelper.ClientDirectory;
                if (!string.IsNullOrEmpty(configuredClientDir))
                {
                    candidateDirs.Add(configuredClientDir);
                }

                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string currentDir = Directory.GetCurrentDirectory();

                candidateDirs.Add(baseDir);
                candidateDirs.Add(Path.Combine(baseDir, "WLRI"));
                candidateDirs.Add(Path.Combine(baseDir, "Client"));
                candidateDirs.Add(Path.Combine(baseDir, "..", "WLRI"));
                candidateDirs.Add(Path.Combine(baseDir, "..", "Client"));
                candidateDirs.Add(Path.Combine(baseDir, "..", "..", "WLRI"));
                candidateDirs.Add(Path.Combine(baseDir, "..", "..", "Client"));
                candidateDirs.Add(Path.Combine(currentDir, "WLRI"));
                candidateDirs.Add(Path.Combine(currentDir, "..", "WLRI"));

                foreach (var dir in candidateDirs.Distinct())
                {
                    if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
                    {
                        string webDatPath = Path.Combine(dir, "web0.DAT");
                        if (File.Exists(webDatPath) || File.Exists(Path.Combine(dir, "aLogin.exe")))
                        {
                            string content = $"Update_Server_1_(Local)      http://127.0.0.1:{port}/WLROD/  {port}  6000 anonymous\r\nUpdate_Server_2_(Local)      http://127.0.0.1:{port}/  {port}  6000 anonymous\r\nend\r\n";
                            File.WriteAllText(webDatPath, content, Encoding.ASCII);
                            DebugSystem.Write($"[EmbeddedWebServer] Patched {webDatPath} to port {port}.");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[EmbeddedWebServer] Error patching web0.DAT: {ex.Message}");
            }
        }

        private static void ListenLoop()
        {
            while (_isRunning && _listener != null && _listener.IsListening)
            {
                try
                {
                    var ctx = _listener.GetContext();
                    ThreadPool.QueueUserWorkItem(ProcessRequest, ctx);
                }
                catch
                {
                    if (!_isRunning) break;
                }
            }
        }

        private static void ProcessRequest(object state)
        {
            var ctx = (HttpListenerContext)state;
            try
            {
                string rawUrl = ctx.Request.RawUrl ?? "/";
                string path = ctx.Request.Url.AbsolutePath.ToLowerInvariant();

                if (path.StartsWith("/api/buy"))
                {
                    HandleApiBuy(ctx);
                }
                else if (path.StartsWith("/api/catalog"))
                {
                    HandleApiCatalog(ctx);
                }
                else
                {
                    // Render Item Mall Web Interface
                    ServeItemMallPage(ctx);
                }
            }
            catch (Exception ex)
            {
                try
                {
                    ctx.Response.StatusCode = 500;
                    byte[] err = Encoding.UTF8.GetBytes("Server Error: " + ex.Message);
                    ctx.Response.OutputStream.Write(err, 0, err.Length);
                    ctx.Response.Close();
                }
                catch { }
            }
        }

        private static void HandleApiCatalog(HttpListenerContext ctx)
        {
            var catalog = ItemMallManager.GetCatalog();
            var jsonBuilder = new StringBuilder();
            jsonBuilder.Append("[");
            for (int i = 0; i < catalog.Count; i++)
            {
                var it = catalog[i];
                jsonBuilder.Append($"{{\"id\":{it.ItemID},\"name\":\"{it.ItemName}\",\"category\":\"{it.Category}\",\"cost\":{it.PointCost},\"count\":{it.Count}}}");
                if (i < catalog.Count - 1) jsonBuilder.Append(",");
            }
            jsonBuilder.Append("]");

            byte[] b = Encoding.UTF8.GetBytes(jsonBuilder.ToString());
            ctx.Response.ContentType = "application/json; charset=utf-8";
            ctx.Response.ContentLength64 = b.Length;
            ctx.Response.OutputStream.Write(b, 0, b.Length);
            ctx.Response.Close();
        }

        public static Func<List<Player>> GetOnlinePlayersHandler { get; set; }

        private static void HandleApiBuy(HttpListenerContext ctx)
        {
            string itemIdStr = ctx.Request.QueryString["item"];
            string userStr = ctx.Request.QueryString["user"];

            if (ushort.TryParse(itemIdStr, out ushort itemId))
            {
                var online = GetOnlinePlayersHandler?.Invoke();
                Player target = null;
                if (online != null && online.Count > 0)
                {
                    if (!string.IsNullOrEmpty(userStr))
                    {
                        target = online.FirstOrDefault(p => p.CharName.Equals(userStr, StringComparison.OrdinalIgnoreCase) || (p.UserAccount != null && p.UserAccount.UserName.Equals(userStr, StringComparison.OrdinalIgnoreCase)));
                    }
                    if (target == null)
                    {
                        target = online.FirstOrDefault();
                    }
                }

                if (target != null)
                {
                    bool ok = ItemMallManager.PurchaseItem(target, itemId, 1);
                    string json = $"{{\"success\":{(ok ? "true" : "false")},\"points\":{ItemMallManager.GetUserPoints(target)}}}";
                    byte[] b = Encoding.UTF8.GetBytes(json);
                    ctx.Response.ContentType = "application/json; charset=utf-8";
                    ctx.Response.ContentLength64 = b.Length;
                    ctx.Response.OutputStream.Write(b, 0, b.Length);
                    ctx.Response.Close();
                    return;
                }
            }

            byte[] fail = Encoding.UTF8.GetBytes("{\"success\":false,\"error\":\"No active player found\"}");
            ctx.Response.ContentType = "application/json; charset=utf-8";
            ctx.Response.ContentLength64 = fail.Length;
            ctx.Response.OutputStream.Write(fail, 0, fail.Length);
            ctx.Response.Close();
        }

        private static void ServeItemMallPage(HttpListenerContext ctx)
        {
            var catalog = ItemMallManager.GetCatalog();
            string userParam = ctx.Request.QueryString["user"];
            var online = GetOnlinePlayersHandler?.Invoke();
            Player player = null;
            if (online != null && online.Count > 0)
            {
                if (!string.IsNullOrEmpty(userParam))
                {
                    player = online.FirstOrDefault(p => p.CharName.Equals(userParam, StringComparison.OrdinalIgnoreCase) || (p.UserAccount != null && p.UserAccount.UserName.Equals(userParam, StringComparison.OrdinalIgnoreCase)));
                }
                if (player == null)
                {
                    player = online.FirstOrDefault();
                }
            }

            int currentPoints = player != null ? ItemMallManager.GetUserPoints(player) : 0;
            string charName = player != null ? player.CharName : (!string.IsNullOrEmpty(userParam) ? userParam : "Player");

            var sb = new StringBuilder();
            sb.AppendLine("<!DOCTYPE html>");
            sb.AppendLine("<html lang='en'>");
            sb.AppendLine("<head>");
            sb.AppendLine("<meta charset='UTF-8'>");
            sb.AppendLine("<title>Wonderland Online - Premium Item Mall</title>");
            sb.AppendLine("<style>");
            sb.AppendLine("  * { box-sizing: border-box; margin: 0; padding: 0; font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; }");
            sb.AppendLine("  body { background: linear-gradient(135deg, #0f172a 0%, #1e293b 100%); color: #f8fafc; padding: 20px; }");
            sb.AppendLine("  .header { display: flex; justify-content: space-between; align-items: center; background: rgba(30, 41, 59, 0.85); backdrop-filter: blur(10px); padding: 18px 24px; border-radius: 12px; border: 1px solid rgba(255, 255, 255, 0.1); margin-bottom: 24px; box-shadow: 0 8px 32px rgba(0,0,0,0.3); }");
            sb.AppendLine("  .title { font-size: 24px; font-weight: 700; background: linear-gradient(45deg, #38bdf8, #818cf8); -webkit-background-clip: text; -webkit-text-fill-color: transparent; }");
            sb.AppendLine("  .balance-badge { background: linear-gradient(135deg, #f59e0b, #d97706); padding: 8px 18px; border-radius: 30px; font-weight: 700; font-size: 15px; color: #fff; box-shadow: 0 4px 15px rgba(245, 158, 11, 0.4); display: flex; align-items: center; gap: 8px; }");
            sb.AppendLine("  .grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(240px, 1fr)); gap: 20px; }");
            sb.AppendLine("  .card { background: rgba(30, 41, 59, 0.7); border: 1px solid rgba(255, 255, 255, 0.08); border-radius: 14px; padding: 20px; display: flex; flex-direction: column; justify-content: space-between; transition: transform 0.2s, border-color 0.2s, box-shadow 0.2s; }");
            sb.AppendLine("  .card:hover { transform: translateY(-4px); border-color: #38bdf8; box-shadow: 0 10px 25px rgba(56, 189, 248, 0.2); }");
            sb.AppendLine("  .card-category { font-size: 11px; text-transform: uppercase; letter-spacing: 1px; color: #38bdf8; font-weight: 700; margin-bottom: 6px; }");
            sb.AppendLine("  .card-name { font-size: 16px; font-weight: 600; color: #f1f5f9; margin-bottom: 12px; }");
            sb.AppendLine("  .card-footer { display: flex; justify-content: space-between; align-items: center; margin-top: 15px; }");
            sb.AppendLine("  .card-price { font-size: 18px; font-weight: 700; color: #fbbf24; }");
            sb.AppendLine("  .buy-btn { background: linear-gradient(135deg, #3b82f6, #2563eb); color: #fff; border: none; padding: 8px 16px; border-radius: 8px; font-weight: 600; cursor: pointer; transition: background 0.2s, transform 0.1s; }");
            sb.AppendLine("  .buy-btn:hover { background: linear-gradient(135deg, #2563eb, #1d4ed8); transform: scale(1.05); }");
            sb.AppendLine("  .buy-btn:active { transform: scale(0.98); }");
            sb.AppendLine("</style>");
            sb.AppendLine("</head>");
            sb.AppendLine("<body>");
            sb.AppendLine("  <div class='header'>");
            sb.AppendLine($"    <div class='title'> Wonderland Online Item Mall</div>");
            sb.AppendLine($"    <div class='balance-badge'> {charName}: <span id='user-pts'>{currentPoints}</span> IM Points</div>");
            sb.AppendLine("  </div>");
            sb.AppendLine("  <div class='grid'>");

            foreach (var item in catalog)
            {
                sb.AppendLine("    <div class='card'>");
                sb.AppendLine("      <div>");
                sb.AppendLine($"        <div class='card-category'>{item.Category} (x{item.Count})</div>");
                sb.AppendLine($"        <div class='card-name'>{item.ItemName}</div>");
                sb.AppendLine("      </div>");
                sb.AppendLine("      <div class='card-footer'>");
                sb.AppendLine($"        <div class='card-price'>{item.PointCost} Pts</div>");
                sb.AppendLine($"        <button class='buy-btn' onclick='buyItem({item.ItemID}, \"{item.ItemName}\", {item.PointCost})'>Buy Now</button>");
                sb.AppendLine("      </div>");
                sb.AppendLine("    </div>");
            }

            sb.AppendLine("  </div>");
            sb.AppendLine("<script>");
            sb.AppendLine("  function buyItem(id, name, cost) {");
            sb.AppendLine("    if(!confirm('Purchase ' + name + ' for ' + cost + ' IM Points?')) return;");
            sb.AppendLine($"    fetch('/api/buy?item=' + id + '&user={charName}')");
            sb.AppendLine("      .then(r => r.json())");
            sb.AppendLine("      .then(d => {");
            sb.AppendLine("        if(d.success) {");
            sb.AppendLine("          alert('Purchase successful! Item delivered directly to your inventory in-game.');");
            sb.AppendLine("          document.getElementById('user-pts').innerText = d.points;");
            sb.AppendLine("        } else {");
            sb.AppendLine("          alert('Purchase failed: ' + (d.error || 'Insufficient IM Points!'));");
            sb.AppendLine("        }");
            sb.AppendLine("      })");
            sb.AppendLine("      .catch(e => alert('Network error: ' + e));");
            sb.AppendLine("  }");
            sb.AppendLine("</script>");
            sb.AppendLine("</body>");
            sb.AppendLine("</html>");

            byte[] b = Encoding.UTF8.GetBytes(sb.ToString());
            ctx.Response.ContentType = "text/html; charset=utf-8";
            ctx.Response.ContentLength64 = b.Length;
            ctx.Response.OutputStream.Write(b, 0, b.Length);
            ctx.Response.Close();
        }
    }
}
