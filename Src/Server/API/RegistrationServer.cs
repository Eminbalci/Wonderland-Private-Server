using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using DataBase;
using Game.Code;

namespace Server.API
{
    /// <summary>
    /// Simple HTTP server for handling web registration requests
    /// </summary>
    public class RegistrationServer
    {
        private HttpListener _listener;
        private Thread _listenerThread;
        private bool _isRunning;
        private readonly int _port;
        private readonly UserDataBase _userDb;

        public RegistrationServer(int port, UserDataBase userDb)
        {
            _port = port;
            _userDb = userDb;
        }

        public void Start()
        {
            if (_isRunning) return;

            _listener = new HttpListener();

            // Try http://+:port first (allows external access, requires admin)
            // Fall back to localhost if admin rights not available
            bool useExternalBinding = true;
            try
            {
                var testListener = new HttpListener();
                testListener.Prefixes.Add($"http://+:{_port}/");
                testListener.Start();
                testListener.Stop();
            }
            catch
            {
                useExternalBinding = false;
            }

            _listener.Prefixes.Add(useExternalBinding ? $"http://+:{_port}/" : $"http://localhost:{_port}/");

            try
            {
                _listener.Start();
                _isRunning = true;
                _listenerThread = new Thread(Listen);
                _listenerThread.IsBackground = true;
                _listenerThread.Start();
                DebugSystem.Write(useExternalBinding
                    ? $"[Registration] Started on http://*:{_port}/ (external access enabled)"
                    : $"[Registration] Started on http://localhost:{_port}/ (run as admin for external access)");
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"Failed to start registration server: {ex.Message}");
            }
        }

        public void Stop()
        {
            _isRunning = false;
            _listener?.Stop();
        }

        private void Listen()
        {
            while (_isRunning)
            {
                try
                {
                    var context = _listener.GetContext();
                    ThreadPool.QueueUserWorkItem(o => HandleRequest(context));
                }
                catch (HttpListenerException)
                {
                    // Listener was stopped
                    break;
                }
            }
        }

        private void HandleRequest(HttpListenerContext context)
        {
            var request = context.Request;
            var response = context.Response;

            // Enable CORS
            response.Headers.Add("Access-Control-Allow-Origin", "*");
            response.Headers.Add("Access-Control-Allow-Methods", "POST, GET, OPTIONS");
            response.Headers.Add("Access-Control-Allow-Headers", "Content-Type");

            if (request.HttpMethod == "OPTIONS")
            {
                response.StatusCode = 200;
                response.Close();
                return;
            }

            string responseString = "";
            response.ContentType = "application/json";

            try
            {
                if (request.Url.AbsolutePath == "/register" && request.HttpMethod == "POST")
                {
                    responseString = HandleRegister(request);
                }
                else if (request.Url.AbsolutePath == "/" || request.Url.AbsolutePath == "/register.html")
                {
                    response.ContentType = "text/html";
                    responseString = GetRegistrationPage();
                }
                else
                {
                    response.StatusCode = 404;
                    responseString = "{\"success\":false,\"message\":\"Not found\"}";
                }
            }
            catch (Exception ex)
            {
                response.StatusCode = 500;
                responseString = $"{{\"success\":false,\"message\":\"{ex.Message}\"}}";
            }

            byte[] buffer = Encoding.UTF8.GetBytes(responseString);
            response.ContentLength64 = buffer.Length;
            response.OutputStream.Write(buffer, 0, buffer.Length);
            response.Close();
        }

        private string HandleRegister(HttpListenerRequest request)
        {
            using (var reader = new StreamReader(request.InputStream, request.ContentEncoding))
            {
                string body = reader.ReadToEnd();

                // Parse form data or JSON
                string username = "", password = "", email = "";

                if (request.ContentType?.Contains("application/x-www-form-urlencoded") == true)
                {
                    // Manual query string parsing
                    var pairs = body.Split('&');
                    foreach (var pair in pairs)
                    {
                        var parts = pair.Split(new[] { '=' }, 2);
                        if (parts.Length == 2)
                        {
                            var key = Uri.UnescapeDataString(parts[0]);
                            var value = Uri.UnescapeDataString(parts[1]);
                            if (key == "username") username = value;
                            else if (key == "password") password = value;
                            else if (key == "email") email = value;
                        }
                    }
                }
                else
                {
                    // Simple JSON parsing
                    username = ExtractJsonValue(body, "username");
                    password = ExtractJsonValue(body, "password");
                    email = ExtractJsonValue(body, "email");
                }

                // Validate input
                if (string.IsNullOrWhiteSpace(username) || username.Length < 3)
                {
                    return "{\"success\":false,\"message\":\"Username must be at least 3 characters\"}";
                }
                if (string.IsNullOrWhiteSpace(password) || password.Length < 4)
                {
                    return "{\"success\":false,\"message\":\"Password must be at least 4 characters\"}";
                }
                if (string.IsNullOrWhiteSpace(email) || !email.Contains("@"))
                {
                    return "{\"success\":false,\"message\":\"Please enter a valid email\"}";
                }

                // Register user
                string errorMessage;
                bool success = _userDb.RegisterUser(username, password, email, out errorMessage);

                if (success)
                {
                    return "{\"success\":true,\"message\":\"Registration successful! You can now login.\"}";
                }
                else
                {
                    return $"{{\"success\":false,\"message\":\"{errorMessage}\"}}";
                }
            }
        }

        private string ExtractJsonValue(string json, string key)
        {
            try
            {
                int keyIndex = json.IndexOf($"\"{key}\"");
                if (keyIndex < 0) return "";
                int colonIndex = json.IndexOf(":", keyIndex);
                int valueStart = json.IndexOf("\"", colonIndex) + 1;
                int valueEnd = json.IndexOf("\"", valueStart);
                return json.Substring(valueStart, valueEnd - valueStart);
            }
            catch
            {
                return "";
            }
        }

        private string GetRegistrationPage()
        {
            return @"<!DOCTYPE html>
<html lang='en'>
<head>
    <meta charset='UTF-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <title>Wonderland - Register</title>
    <style>
        * { margin: 0; padding: 0; box-sizing: border-box; }
        body {
            font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
            min-height: 100vh;
            display: flex;
            align-items: center;
            justify-content: center;
            background: linear-gradient(135deg, #1a1a2e 0%, #16213e 50%, #0f3460 100%);
            padding: 20px;
        }
        .container {
            background: rgba(255, 255, 255, 0.05);
            backdrop-filter: blur(10px);
            border-radius: 20px;
            padding: 40px;
            width: 100%;
            max-width: 420px;
            box-shadow: 0 25px 50px rgba(0, 0, 0, 0.3);
            border: 1px solid rgba(255, 255, 255, 0.1);
        }
        h1 {
            color: #fff;
            text-align: center;
            margin-bottom: 10px;
            font-size: 2rem;
            background: linear-gradient(90deg, #e94560, #ff6b6b);
            -webkit-background-clip: text;
            -webkit-text-fill-color: transparent;
        }
        .subtitle {
            color: rgba(255, 255, 255, 0.6);
            text-align: center;
            margin-bottom: 30px;
            font-size: 0.9rem;
        }
        .form-group {
            margin-bottom: 20px;
        }
        label {
            display: block;
            color: rgba(255, 255, 255, 0.8);
            margin-bottom: 8px;
            font-size: 0.9rem;
        }
        input {
            width: 100%;
            padding: 14px 16px;
            border: 2px solid rgba(255, 255, 255, 0.1);
            border-radius: 10px;
            background: rgba(255, 255, 255, 0.05);
            color: #fff;
            font-size: 1rem;
            transition: all 0.3s ease;
        }
        input:focus {
            outline: none;
            border-color: #e94560;
            background: rgba(255, 255, 255, 0.1);
        }
        input::placeholder {
            color: rgba(255, 255, 255, 0.3);
        }
        button {
            width: 100%;
            padding: 14px;
            border: none;
            border-radius: 10px;
            background: linear-gradient(90deg, #e94560, #ff6b6b);
            color: #fff;
            font-size: 1rem;
            font-weight: 600;
            cursor: pointer;
            transition: transform 0.2s, box-shadow 0.2s;
            margin-top: 10px;
        }
        button:hover {
            transform: translateY(-2px);
            box-shadow: 0 10px 30px rgba(233, 69, 96, 0.4);
        }
        button:active {
            transform: translateY(0);
        }
        .message {
            padding: 12px;
            border-radius: 8px;
            margin-top: 20px;
            text-align: center;
            font-size: 0.9rem;
            display: none;
        }
        .message.success {
            background: rgba(46, 213, 115, 0.2);
            color: #2ed573;
            border: 1px solid rgba(46, 213, 115, 0.3);
        }
        .message.error {
            background: rgba(255, 71, 87, 0.2);
            color: #ff4757;
            border: 1px solid rgba(255, 71, 87, 0.3);
        }
        .logo {
            text-align: center;
            margin-bottom: 20px;
            font-size: 3rem;
        }
    </style>
</head>
<body>
    <div class='container'>
        <div class='logo'>🌟</div>
        <h1>Wonderland</h1>
        <p class='subtitle'>Create your account</p>
        
        <form id='registerForm'>
            <div class='form-group'>
                <label for='username'>Username</label>
                <input type='text' id='username' name='username' placeholder='Enter username' required minlength='3'>
            </div>
            <div class='form-group'>
                <label for='email'>Email</label>
                <input type='email' id='email' name='email' placeholder='Enter email' required>
            </div>
            <div class='form-group'>
                <label for='password'>Password</label>
                <input type='password' id='password' name='password' placeholder='Enter password' required minlength='4'>
            </div>
            <div class='form-group'>
                <label for='confirmPassword'>Confirm Password</label>
                <input type='password' id='confirmPassword' placeholder='Confirm password' required>
            </div>
            <button type='submit'>Register</button>
        </form>
        
        <div id='message' class='message'></div>
    </div>

    <script>
        document.getElementById('registerForm').addEventListener('submit', async function(e) {
            e.preventDefault();
            
            const password = document.getElementById('password').value;
            const confirmPassword = document.getElementById('confirmPassword').value;
            const messageDiv = document.getElementById('message');
            
            if (password !== confirmPassword) {
                messageDiv.textContent = 'Passwords do not match';
                messageDiv.className = 'message error';
                messageDiv.style.display = 'block';
                return;
            }
            
            const data = {
                username: document.getElementById('username').value,
                email: document.getElementById('email').value,
                password: password
            };
            
            try {
                const response = await fetch('/register', {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify(data)
                });
                
                const result = await response.json();
                
                messageDiv.textContent = result.message;
                messageDiv.className = 'message ' + (result.success ? 'success' : 'error');
                messageDiv.style.display = 'block';
                
                if (result.success) {
                    document.getElementById('registerForm').reset();
                }
            }
            catch (error) {
                messageDiv.textContent = 'Connection error. Please try again.';
                messageDiv.className = 'message error';
                messageDiv.style.display = 'block';
            }
        });
    </script>
</body>
</html>";
        }
    }
}
