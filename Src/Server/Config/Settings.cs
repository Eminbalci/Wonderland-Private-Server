using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using System.Xml.Serialization;
using System.Xml;

namespace Server.Config
{

    [Serializable]
    public class SettingObj 
    {
    }

    public class Settings
    {
        [XmlIgnore]
        readonly XmlSerializer diskio;
        [XmlIgnore]
        readonly object m_Lock = new object();

        public UpdateSetting Update;
        public DataBaseConfig DB;
        public string ServerName = "Wonderland";
        public string WelcomeMessage = "Welcome to the WLO Community Server! Enjoy!";
        public List<string> WelcomeMessages = new List<string>();
        public bool EnableLoginEventPrompts = false;

        public List<string> GetAllWelcomeMessages()
        {
            var list = new List<string>();
            if (WelcomeMessages != null && WelcomeMessages.Count > 0)
            {
                foreach (var msg in WelcomeMessages)
                {
                    if (!string.IsNullOrWhiteSpace(msg) && !list.Contains(msg.Trim()))
                        list.Add(msg.Trim());
                }
            }
            if (!string.IsNullOrWhiteSpace(WelcomeMessage))
            {
                var split = WelcomeMessage.Split(new[] { "\r\n", "\n", "|", "||" }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var s in split)
                {
                    string trimmed = s.Trim();
                    if (!string.IsNullOrWhiteSpace(trimmed) && !list.Contains(trimmed))
                        list.Add(trimmed);
                }
            }
            if (list.Count == 0)
                list.Add("Welcome to the WLO Community Server! Enjoy!");
            return list;
        }
        
        public Settings()
        {
            Update = new UpdateSetting();
            DB = new DataBaseConfig();
            diskio = new XmlSerializer(this.GetType());
            
        }


        public void SaveSettings(string location)
        {
            try
            {
                lock (m_Lock)
                {
                    DebugSystem.Write("Saving Settings");
                    string dir = Path.GetDirectoryName(location);
                    if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                        Directory.CreateDirectory(dir);

                    using (StreamWriter file = new StreamWriter(location, false, Encoding.UTF8))
                        diskio.Serialize(file, this);

                    // Backup copy to local Data directory
                    try
                    {
                        string localDataDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data");
                        if (!Directory.Exists(localDataDir)) Directory.CreateDirectory(localDataDir);
                        string localPath = Path.Combine(localDataDir, "Config.settings.wlo");
                        using (StreamWriter localFile = new StreamWriter(localPath, false, Encoding.UTF8))
                            diskio.Serialize(localFile, this);
                    }
                    catch { }
                }
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[Settings] Error saving settings: {ex.Message}");
            }
        }
    }
}
