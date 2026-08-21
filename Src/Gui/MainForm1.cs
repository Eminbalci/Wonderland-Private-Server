using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Game;
using Plugin;

namespace Wonderland_Private_Server
{
    public partial class Form1 : Form
    {
        bool blockclose = true;

        PluginManager phostManager;


        public Form1()
        {
            InitializeComponent();
            this.KeyPreview = true;
            LoadAllLists();
            this.Size = new System.Drawing.Size(1200, 780);
            this.MinimumSize = new System.Drawing.Size(1000, 680);
            SetupGmTab();
            SetupItemMallTab();
            SetupMonsterDropsTab();
            SetupQuestManagerTab();
            SetupServerStatusControl();
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.F5)
            {
                RunClientProgram();
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        private void RunClientProgram()
        {
            try
            {
                string[] searchPaths = new string[]
                {
                    @"D:\garipgudubetseyler\WLRI\aLogin.exe",
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "aLogin.exe"),
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "WLRI", "aLogin.exe"),
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "WLRI", "aLogin.exe")
                };

                string clientPath = searchPaths.FirstOrDefault(File.Exists);

                if (!string.IsNullOrEmpty(clientPath))
                {
                    ProcessStartInfo psi = new ProcessStartInfo
                    {
                        FileName = clientPath,
                        WorkingDirectory = Path.GetDirectoryName(clientPath),
                        UseShellExecute = true
                    };
                    Process.Start(psi);
                    DebugSystem.Write(DebugItemType.Info_Light, "[System] F5 pressed: Client process started (" + clientPath + ")");
                }
                else
                {
                    DebugSystem.Write(DebugItemType.Error, "[System] F5 pressed: Client executable (aLogin.exe) not found.");
                }
            }
            catch (Exception ex)
            {
                DebugSystem.Write(DebugItemType.Error, "[System] F5 pressed error: " + ex.Message);
            }
        }


        void DebugSystem_onNewLog(object sender, DebugItem j)
        {
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            // NPC Tabs removed as per request
            // InitializeNPCTab();

            // Load Compound Data
            try
            {
                DebugSystem.Write(DebugItemType.Info_Light, "Loading Compound/Alchemy Data...");
                cGlobal.gCompoundDat = new Wonderland_Private_Server.DataManagement.DataFiles.cCompound2Dat();
                cGlobal.gCompoundDat.Load("Data\\Compound.dat");
                cGlobal.gCompoundDat.Load("Data\\Compound2.dat", false); // append
                DebugSystem.Write(DebugItemType.Info_Light, "Compound Data Loaded.");
            }
            catch (Exception ex)
            {
                // Ensure we don't crash if files missing
                DebugSystem.Write(DebugItemType.Error, "Failed to load Compound Data: " + ex.Message);
            }

            Thread MainThread = new Thread(new ThreadStart(MainThreadWork));
            MainThread.IsBackground = true;
            MainThread.Init();

            // Auto refresh tabs and filters after server boot
            this.tabControl3.SelectedIndexChanged += (s, ev) =>
            {
                try
                {
                    if (this.tabControl3.SelectedTab != null)
                    {
                        if (this.tabControl3.SelectedTab.Text.Contains("Monster Drops"))
                        {
                            RefreshMonsterListGrid();
                        }
                        else if (this.tabControl3.SelectedTab.Text.Contains("Item Mall"))
                        {
                            RefreshMallGrid();
                        }
                    }
                }
                catch { }
            };

            Task.Run(() =>
            {
                Thread.Sleep(2000); // Wait for server initialization
                try
                {
                    this.Invoke(new Action(() =>
                    {
                        LoadCharacterFilters();
                        LoadChestDropTargets();
                        RefreshMonsterListGrid();
                        RefreshMallGrid();
                    }));
                }
                catch { }
            });
        }

        void GuiThread()
        {
            do
            {
                #region LogGUI
                #endregion
                #region TaskGui

                if (cGlobal.ApplicationTasks != null)
                    this.BeginInvoke(new Action(() => { cGlobal.ApplicationTasks.onUpdateGuiTick(); }));

                #endregion
                #region Form.System.Status gui
                try
                {
                    this.BeginInvoke(new Action(() =>
                    {
                        //thrd_label.Text = string.Format("Thread Cnt - {0}", ThreadManager.Count);
                    }));
                }
                catch { }
                #endregion
                #region Form.Update
                //try
                //{
                //    this.BeginInvoke(new Action(() =>
                //    {
                //        DateTime tmp = (cGlobal.ApplicationTasks.TaskItems.Count(c => c.TaskName == "Updating Application") > 0) ? cGlobal.ApplicationTasks.TaskItems.Single(c => c.TaskName == "Updating Application").Createdat : new DateTime();

                //        autoUpdt_label.Text = string.Format(tmp.Add(cGlobal.SrvSettings.Update.AutoUpdt_Schedule).ToString());
                //    }));
                //}
                //catch { }
                #endregion
                #region Periodic Tasks
                // Refresh online players list in cheat tab regularly (every ~1 second: 200 * 5ms = 1000ms)
                if (DateTime.Now.Millisecond % 1000 < 50)
                {
                    try
                    {
                        RefreshOnlinePlayers();
                    }
                    catch { }
                }
                #endregion
                Thread.Sleep(5);
            }
            while (cGlobal.Run);
        }


        void MainThreadWork()
        {

            cGlobal.Run = true;
            DebugSystem.Initialize(ref MainOutput, true);
            DebugSystem.VerboseLvl = 1;

            DebugSystem.Write("[Init] - Initializing DataFile Objects");
            Console.WriteLine("[Init] - Initializing DataFile Objects");
            cGlobal.ItemDatManager = new DataFiles.PhxItemDat();
            cGlobal.ItemDatManager.onDebug = (obj) => { };
            string itemDatPath = System.IO.Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "Data", "itemDat.wpdat");
            if (System.IO.File.Exists(itemDatPath))
            {
                cGlobal.ItemDatManager.Load(itemDatPath).Wait();
                DebugSystem.Write($"[Init] - Loaded {cGlobal.ItemDatManager.GetItemList().Count} items from itemDat.wpdat");
            }

            Game.Battle.MonsterDropManager.ItemNameResolver = (iid) =>
            {
                try
                {
                    var item = cGlobal.ItemDatManager?.GetItemByID(iid);
                    if (item != null && item.ItemName != null && item.ItemName.Length > 0)
                    {
                        string n = System.Text.Encoding.Default.GetString(item.ItemName).Trim('\0', ' ');
                        if (!string.IsNullOrEmpty(n)) return n;
                    }
                }
                catch { }
                return null;
            };

            string talkDatPath = System.IO.Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "Data", "Talk.dat");
            cGlobal.TalkDatManager = new DataFiles.PhxTalkDat(talkDatPath);
            DebugSystem.Write($"[Init] - Loaded {cGlobal.TalkDatManager.Count} authentic dialogues from Talk.dat");

            string markDatPath = System.IO.Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "Data", "Mark.dat");
            cGlobal.MarkDatManager = new DataFiles.PhxMarkDat(markDatPath);
            Game.QuestRelated.QuestManager.LoadAuthenticQuestsFromMarkDat(markDatPath);
            DebugSystem.Write($"[Init] - Loaded {cGlobal.MarkDatManager.Count} quest marks directly from Mark.dat (Total Quests: {Game.QuestRelated.QuestManager.AllQuests.Count})");

            string npcDatPath = System.IO.Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "Data", "Npc.dat");
            Game.Battle.MonsterDropManager.LoadFromNpcDat(npcDatPath);

            Game.PlayerRelated.GuildManager.Initialize();
            Game.PlayerRelated.MarriageManager.Initialize();
            Game.PlayerRelated.MailSystem.Initialize();
            Game.PlayerRelated.GmManager.Initialize();
            Game.PlayerRelated.ItemMallManager.Initialize();
            Game.Crafting.GatheringManager.Initialize();
            Game.Crafting.AlchemyManager.InitializeRecipes();
            Server.ServerStatusManager.OnlinePlayerCountProvider = () => cGlobal.gLoginServer?.GetAllPlayers().Count ?? 0;
            Server.ServerStatusManager.Initialize(6416);

            DebugSystem.Write("[Init] - Initializing DataBase Objects");
            cGlobal.gUserDataBase = new DataBase.UserDataBase();
            Game.PlayerRelated.ItemMallManager.OnPointsChanged = (uid, pts) => cGlobal.gUserDataBase?.SetIMPoints(uid, pts);
            cGlobal.gCharacterDataBase = new DataBase.CharacterDataBase();
            cGlobal.gCharacterDataBase.ItemDat = cGlobal.ItemDatManager;
            cGlobal.gGameDataBase = new DataBase.GameDataBase();
            cGlobal.gGameDataBase.ItemDat = cGlobal.ItemDatManager;
            cGlobal.gGameDataBase.TalkDat = cGlobal.TalkDatManager;
            cGlobal.gGameDataBase.MarkDat = cGlobal.MarkDatManager;
            cGlobal.gGameDataBase.VerifySetup(); // Create Friends table

            cGlobal.gPortalDataBase = new DataBase.PortalDataBase();
            cGlobal.gPortalDataBase.VerifySetup();
            DebugSystem.Write("[Init] - Intializing Systems Please Wait.....");
            cGlobal.ApplicationTasks = new Server.TaskManager();
            //cGlobal.Update_System = new Server.System.UpdateSystem();
            //cGlobal.Update_System.MainFrm = this;
            //cGlobal.Update_System.MapUpdtPanel = UpdtPane2;
            //cGlobal.Update_System.AppUpdtPanel = UpdatePane;
            phostManager = new PluginManager();
            phostManager.Intialize();

            cGlobal.gLoginServer = new Server.LoginServer();
            cGlobal.gWorld = new Server.WorldServer(phostManager);
            //cGlobal.gLoginServer.OnNewPlayer += (s, e) => cGlobal.gWorld.OnLogin(e);

            //cGlobal.WLO_World = new Server.WloWorldNode();
            //cGlobal.gCharacterDataBase = new DataManagement.DataBase.CharacterDataBase();
            //cGlobal.gEveManager = new DataManagement.DataFiles.EveManager();
            //cGlobal.gGameDataBase = new DataManagement.DataBase.GameDataBase();
            //cGlobal.gItemManager = new DataManagement.DataFiles.ItemManager();
            //cGlobal.gSkillManager = new DataManagement.DataFiles.SkillDataFile();
            //cGlobal.gCompoundDat = new DataManagement.DataFiles.cCompound2Dat();
            //cGlobal.gUserDataBase = new UserDataBase();
            //cGlobal.gNpcManager = new DataManagement.DataFiles.NpcDat();

            cGlobal.SrvSettings = new Server.Config.Settings();

            #region load settings file

            DebugSystem.Write("Loading Settings File");
            if (System.IO.File.Exists(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\PServer\\Config.settings.wlo"))
            {
                System.Xml.Serialization.XmlSerializer diskio = new System.Xml.Serialization.XmlSerializer(typeof(Server.Config.Settings));

                try
                {
                    using (StreamReader file = new StreamReader(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\PServer\\Config.settings.wlo"))
                        cGlobal.SrvSettings = (Server.Config.Settings)diskio.Deserialize(file);
                    DebugSystem.Write("Settings File loaded successfully");
                }
                catch { DebugSystem.Write("Settings File failed to load"); }
            }
            else
                DebugSystem.Write("Settings File not found");





            //cGlobal.gUserDataBase.TableName = cGlobal.SrvSettings.DB.TableName_Ref;
            if (!string.IsNullOrEmpty(cGlobal.SrvSettings.DB.Username_Ref))
                cGlobal.gUserDataBase.Username_Ref = cGlobal.SrvSettings.DB.Username_Ref;
            if (!string.IsNullOrEmpty(cGlobal.SrvSettings.DB.Password_Ref))
                cGlobal.gUserDataBase.Password_Ref = cGlobal.SrvSettings.DB.Password_Ref;
            if (!string.IsNullOrEmpty(cGlobal.SrvSettings.DB.UserID_Ref))
                cGlobal.gUserDataBase.DataBaseID_Ref = cGlobal.SrvSettings.DB.UserID_Ref;
            if (!string.IsNullOrEmpty(cGlobal.SrvSettings.DB.IM_Ref))
                cGlobal.gUserDataBase.IM_Ref = cGlobal.SrvSettings.DB.IM_Ref;
            if (!string.IsNullOrEmpty(cGlobal.SrvSettings.DB.CharacterID1_Ref))
                cGlobal.gUserDataBase.CharacterID1_Ref = cGlobal.SrvSettings.DB.CharacterID1_Ref;
            if (!string.IsNullOrEmpty(cGlobal.SrvSettings.DB.CharacterID2_Ref))
                cGlobal.gUserDataBase.CharacterID2_Ref = cGlobal.SrvSettings.DB.CharacterID2_Ref;
            if (!string.IsNullOrEmpty(cGlobal.SrvSettings.DB.Char_Delete_Code_Ref))
                cGlobal.gUserDataBase.Char_Delete_Code_Ref = cGlobal.SrvSettings.DB.Char_Delete_Code_Ref;
            cGlobal.gUserDataBase.PassVerification = (Game.VerifyPassType)cGlobal.SrvSettings.DB.PassVerification;
            //if (GitUptOption.SelectedIndex != (byte)cGlobal.SrvSettings.Update.UpdtControl)
            //    GitUptOption.SelectedIndex = (byte)cGlobal.SrvSettings.Update.UpdtControl;



            #endregion



            //use for testing
#if DEBUG

#endif


            #region Intialize Base Threads
            DebugSystem.Write("Intializing Gui Thread");
            Thread tmp2 = new Thread(new ThreadStart(GuiThread));
            tmp2.Name = "Gui Thread";
            tmp2.Init();

            #endregion

            #region intial check  if theres an update from github
            DebugSystem.Write("Checking For Update on Git...");

            //if (cGlobal.SrvSettings.Update.UpdtControl != Server.Config.UpdtSetting.Never && cGlobal.ApplicationTasks.TaskItems.Count(c => c.TaskName == "Updating Application") > 0)
            //    goto ShutDwn;
            #endregion


            #region Configure Form Data
            /*this.Invoke(new Action(() => {
                dataGridView1.Columns[0].DataPropertyName = "TaskName";
                dataGridView1.Columns[1].DataPropertyName = "Interval";
                dataGridView1.Columns[2].DataPropertyName = "LastExecution";
                dataGridView1.Columns[3].DataPropertyName = "NextExecution";
                dataGridView1.Columns[4].DataPropertyName = "Status";
                dataGridView1.DataSource = cGlobal.ApplicationTasks.TaskItems;
            }));

            TableName.DataBindings.Add("Text", cGlobal.SrvSettings.DB, "TableName_Ref");
            Username_Ref.DataBindings.Add("Text", cGlobal.SrvSettings.DB, "Username_Ref");
            Password_Ref.DataBindings.Add("Text", cGlobal.SrvSettings.DB, "Password_Ref");
            UserID_Ref.DataBindings.Add("Text", cGlobal.SrvSettings.DB, "UserID_Ref");
            IM_Ref.DataBindings.Add("Text", cGlobal.SrvSettings.DB, "IM_Ref");
            Char_Delete_Code_Ref.DataBindings.Add("Text", cGlobal.SrvSettings.DB, "Char_Delete_Code_Ref");
            _passVerifi.DataBindings.Add("SelectedIndex", cGlobal.SrvSettings.DB, "PassVerification");*/
            #endregion


            #region DataBase Initialization


            DebugSystem.Write("Testing Connection to UserDatabase");
            try
            {
                if (cGlobal.gUserDataBase.TestConnection())
                {
                    DebugSystem.Write("Connection Successful");
                    DebugSystem.Write("Verifying User DataBase Tables");
                    cGlobal.gUserDataBase.VerifySetup();
                }
                else
                    DebugSystem.Write("Connection not successful\r\n unable to authenticate users connecting to server");

                DebugSystem.Write("Testing Connection to Character Database");
                if (cGlobal.gCharacterDataBase.TestConnection())
                {
                    DebugSystem.Write("Connection Successful");
                    DebugSystem.Write("Verifying Character DataBase Tables");
                    cGlobal.gCharacterDataBase.VerifySetup();
                }
                else
                    DebugSystem.Write("Connection not successful\r\n unable to create neccesary tables for the server");
            }
            catch (Exception e) { DebugSystem.Write(new ExceptionData(e)); }


            #endregion

            #region Load Data Files
            //cGlobal.gItemManager.LoadItems("Data\\Item.dat");
            //cGlobal.gSkillManager.LoadSkills("Data\\Skill.dat");
            //cGlobal.gNpcManager.LoadNpc("Data\\Npc.dat");
            cGlobal.gGameDataBase.EveDat.LoadFile("Data\\eve.Emg");
            //cGlobal.gCompoundDat.Load("Data\\Compound.dat");
            //cGlobal.gCompoundDat.Load("Data\\Compound2.dat", false);

            #endregion

            DebugSystem.Write("[Init] - Intializing Server Please Wait.....");
            #region Initialize Server Components
            cGlobal.gWorld.Initialize();
            cGlobal.gLoginServer.Initialize();
            cGlobal.gItemMallServer = new Server.ItemMallServer(6416);
            cGlobal.gItemMallServer.Start();

            // Start Registration Web Server
            cGlobal.gRegistrationServer = new Server.API.RegistrationServer(8080, cGlobal.gUserDataBase);
            cGlobal.gRegistrationServer.Start();
            DebugSystem.Write("[Init] - Registration page available at: http://localhost:8080/");

            //cGlobal.WLO_World.Initialize();
            Thread.Sleep(2);
            //cGlobal.TcpListener.Initialize();
            #endregion


            do
            {
                #region TaskManager
                cGlobal.ApplicationTasks?.onUpdateTick();
                #endregion
                Thread.Sleep(10);
            }
            while (cGlobal.Run);

            PerformSafeShutdown();
        }

        private static int _isShuttingDown = 0;

        public void PerformSafeShutdown()
        {
            if (System.Threading.Interlocked.Exchange(ref _isShuttingDown, 1) != 0)
                return;

            try
            {
                cGlobal.Run = false;
                blockclose = false;

                try
                {
                    if (this.IsHandleCreated && !this.IsDisposed)
                    {
                        this.BeginInvoke(new Action(() => { this.Enabled = false; }));
                    }
                }
                catch { }

                DebugSystem.Write("[SafeShutdown] Initiating safe server shutdown sequence...");

                // 1. Broadcast warning to online players
                try
                {
                    var online = cGlobal.gCharacterDataBase?.GetOnlinePlayers();
                    if (online != null && online.Count > 0)
                    {
                        foreach (var p in online)
                        {
                            try
                            {
                                p.Send(Tools.FromFormat("bbbs", 23, 57, 0, "Server is performing a safe shutdown. Saving all character data..."));
                            }
                            catch { }
                        }
                    }
                }
                catch { }

                // 2. Save all online players & flush IM points
                int savedPlayers = 0;
                try
                {
                    var online = cGlobal.gCharacterDataBase?.GetOnlinePlayers();
                    if (online != null && online.Count > 0)
                    {
                        foreach (var p in online)
                        {
                            try
                            {
                                cGlobal.gCharacterDataBase.WritePlayer(p.CharID, p);
                                if (p.UserAccount != null && p.UserAccount.DataBaseID != 0)
                                {
                                    cGlobal.gUserDataBase?.SetIMPoints(p.UserAccount.DataBaseID, p.UserAccount.IM);
                                }
                                savedPlayers++;
                                DebugSystem.Write($"[SafeShutdown] Saved character {p.CharName} (CharID: {p.CharID})");
                            }
                            catch (Exception ex)
                            {
                                DebugSystem.Write($"[SafeShutdown] Error saving player {p.CharName}: {ex.Message}");
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    DebugSystem.Write($"[SafeShutdown] Exception saving players: {ex.Message}");
                }

                // 3. Save chest drops and server settings
                try
                {
                    Game.Maps.ChestDropManager.SaveToFile();
                    cGlobal.SrvSettings?.SaveSettings(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\PServer\\Config.settings.wlo");
                    DebugSystem.Write("[SafeShutdown] Saved server configuration and drop catalogs.");
                }
                catch (Exception ex)
                {
                    DebugSystem.Write($"[SafeShutdown] Error saving settings: {ex.Message}");
                }

                // 4. Disconnect all players
                try
                {
                    var online = cGlobal.gCharacterDataBase?.GetOnlinePlayers();
                    if (online != null && online.Count > 0)
                    {
                        foreach (var p in online)
                        {
                            try { p.Disconnect(); } catch { }
                        }
                    }
                }
                catch { }

                // 5. Terminate all network listeners cleanly
                try
                {
                    cGlobal.gRegistrationServer?.Stop();
                    cGlobal.gLoginServer?.Kill();
                    cGlobal.gItemMallServer?.Stop();
                    cGlobal.gWorld?.Kill();
                    DebugSystem.Write("[SafeShutdown] Network listeners and world threads stopped.");
                }
                catch (Exception ex)
                {
                    DebugSystem.Write($"[SafeShutdown] Exception stopping network listeners: {ex.Message}");
                }

                DebugSystem.Write($"[SafeShutdown] Safe server shutdown completed successfully ({savedPlayers} players saved).");
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[SafeShutdown] Critical shutdown error: {ex.Message}");
            }
            finally
            {
                // Force exit process cleanly without hanging or ghost background threads
                Environment.Exit(0);
            }
        }

        #region Form Events
        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (_isShuttingDown == 0)
            {
                e.Cancel = true;
                ThreadPool.QueueUserWorkItem(_ => PerformSafeShutdown());
            }
        }

        #endregion



        Dictionary<string, string> MapsData;
        Dictionary<string, string> VehiclesData;
        Dictionary<string, string> ItemsData;
        Dictionary<string, string> NpcData;

        private void LoadAllLists()
        {
            MapsData = CsvToDict("listdata\\maps.csv");
            VehiclesData = CsvToDict("listdata\\vehicles.csv");
            ItemsData = CsvToDict("listdata\\items.csv");
            NpcData = CsvToDict("listdata\\npc.csv");
            foreach (var m in MapsData) listBox_Maps.Items.Add(m.Key + " " + m.Value);
            foreach (var v in VehiclesData) listBox_Vehicles.Items.Add(v.Key + " " + v.Value);
            foreach (var i in ItemsData) listBox_Items.Items.Add(i.Key + " " + i.Value);
            foreach (var n in NpcData) listBox_NPC.Items.Add(n.Key + " " + n.Value);
            listBox_Maps.MouseDoubleClick += ListBoxMaps_MouseDoubleClick;
            listBox_Vehicles.MouseDoubleClick += ListBoxVehicles_MouseDoubleClick;
            listBox_Items.MouseDoubleClick += ListBoxItems_MouseDoubleClick;
            listBox_NPC.MouseDoubleClick += ListBoxNpc_MouseDoubleClick;
            textBox_FindMap.KeyDown += textBox_FindMap_KeyDown;
            textBox_FindVehicle.KeyDown += textBox_FindVehicle_KeyDown;
            textBox_FindItems.KeyDown += textBox_FindItems_KeyDown;
            textBox_FindNPC.KeyDown += textBox_FindNpc_KeyDown;
        }

        private void ListBoxMaps_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            int selectedIndex = listBox_Maps.SelectedIndex;
            if (selectedIndex != ListBox.NoMatches && selectedIndex < listBox_Maps.Items.Count)
            {
                string selectedMap = listBox_Maps.Items[selectedIndex].ToString();
                string selectedMapID = selectedMap.Split(' ')[0];
                GetPrivatePlayer().TeleportPlayer(selectedMapID);
            }
        }
        private void ListBoxVehicles_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            int selectedIndex = listBox_Vehicles.SelectedIndex;
            if (selectedIndex != ListBox.NoMatches && selectedIndex < listBox_Vehicles.Items.Count)
            {
                string selectedVehicle = listBox_Vehicles.Items[selectedIndex].ToString();
                string selectedVehicleID = selectedVehicle.Split(' ')[0];
                GetPrivatePlayer().UnridePet(); GetPrivatePlayer().RideVehicle(selectedVehicleID);
            }
        }
        private void ListBoxItems_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            int selectedIndex = listBox_Items.SelectedIndex;
            if (selectedIndex != ListBox.NoMatches && selectedIndex < listBox_Items.Items.Count)
            {
                string selectedItem = listBox_Items.Items[selectedIndex].ToString();
                string selectedItemID = selectedItem.Split(' ')[0];
                Player targetPlayer = GetPrivatePlayer();
                if (targetPlayer != null)
                {
                    targetPlayer.AddItemToInventory(selectedItemID);
                    DebugSystem.Write($"[Cheat] Gave item {selectedItemID} ({selectedItem}) to player {targetPlayer.CharName}");
                }
                else
                {
                    DebugSystem.Write($"[Cheat] Failed to give item: No online player found!");
                }
            }
        }
        private void ListBoxNpc_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            ProcessNpcRequest();
        }

        private void ProcessNpcRequest()
        {
            int selectedIndex = listBox_NPC.SelectedIndex;
            if (selectedIndex != ListBox.NoMatches && selectedIndex < listBox_NPC.Items.Count)
            {
                string selectedNpc = listBox_NPC.Items[selectedIndex].ToString();
                string selectedNpcID = selectedNpc.Split(' ')[0];
                Player mainPlayer = GetPrivatePlayer();
                mainPlayer.RideVehicle("");
                mainPlayer.AddPetToPartyList(selectedNpcID);
                if (radioButton_Battle.Checked) mainPlayer.PutPetToBattle(selectedNpcID);
                else if (radioButton_Ride.Checked) mainPlayer.PutPetToRide(selectedNpcID);
            }
        }

        private Dictionary<string, string> CsvToDict(string filePath)
        {
            StreamReader reader;
            if (!File.Exists(filePath)) { DebugSystem.Write("File doesn't exist--------: " + filePath); return new Dictionary<string, string>(); }
            reader = new StreamReader(File.OpenRead(filePath));
            Dictionary<string, string> dictData = new Dictionary<string, string>();
            while (!reader.EndOfStream)
            {
                var line = reader.ReadLine();
                string[] dd = line.Split(',');
                dictData.Add(dd[0], dd[1]);
            }
            return dictData;
        }

        private Player GetPrivatePlayer()
        {
            try
            {
                if (comboBox_OnlinePlayers.InvokeRequired)
                {
                    return (Player)comboBox_OnlinePlayers.Invoke(new Func<Player>(() => GetPrivatePlayer()));
                }

                if (comboBox_OnlinePlayers.SelectedItem is Player sp && sp != null)
                    return sp;

                var all = cGlobal.gLoginServer?.GetAllPlayers();
                if (all != null && all.Count > 0)
                {
                    comboBox_OnlinePlayers.SelectedItem = all[0];
                    return all[0];
                }
            }
            catch { }
            return cGlobal.gLoginServer?.privatePlayer;
        }

        private void RefreshOnlinePlayers()
        {
            if (comboBox_OnlinePlayers.InvokeRequired)
            {
                comboBox_OnlinePlayers.Invoke(new Action(RefreshOnlinePlayers));
                return;
            }

            // Save current selection
            Player selectedPlayer = null;
            if (comboBox_OnlinePlayers.SelectedItem != null)
                selectedPlayer = (Player)comboBox_OnlinePlayers.SelectedItem;

            // Get online players safely
            List<Player> onlinePlayers = new List<Player>();
            try
            {
                // Access via new GetAllPlayers method
                if (cGlobal.gLoginServer != null)
                {
                    onlinePlayers = cGlobal.gLoginServer.GetAllPlayers();
                }
            }
            catch { }

            // Update ComboBox items if list changed (simple check by count or just refresh)
            // For smoother UI, we can clear and re-add. 
            // Improve: check if list is actually different to avoid flickering? 
            // For now, just refresh every time but keep selection if valid.

            comboBox_OnlinePlayers.Items.Clear();
            foreach (var p in onlinePlayers)
            {
                comboBox_OnlinePlayers.Items.Add(p);
            }

            // Restore selection or select default
            if (selectedPlayer != null && onlinePlayers.Contains(selectedPlayer))
            {
                comboBox_OnlinePlayers.SelectedItem = selectedPlayer;
            }
            else if (onlinePlayers.Count > 0)
            {
                comboBox_OnlinePlayers.SelectedIndex = onlinePlayers.Count - 1; // Default to latest logic
            }
        }

        private void radioButton_Battle_CheckedChanged(object sender, EventArgs e)
        {
            ProcessNpcRequest();
        }

        private void button_NpcLeave_Click(object sender, EventArgs e)
        {
            GetPrivatePlayer().AddPetToPartyList("");
        }

        private void textBox_FindMap_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode != Keys.Enter) return;
            string searchKey = textBox_FindMap.Text;
            listBox_Maps.Items.Clear();
            foreach (var row in MapsData)
                if (row.Value.ToLower().Contains(searchKey.ToLower())) listBox_Maps.Items.Add(row.Key + " " + row.Value);
        }
        private void textBox_FindVehicle_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode != Keys.Enter) return;
            string searchKey = textBox_FindVehicle.Text;
            listBox_Vehicles.Items.Clear();
            foreach (var row in VehiclesData)
                if (row.Value.ToLower().Contains(searchKey.ToLower())) listBox_Vehicles.Items.Add(row.Key + " " + row.Value);
        }
        private void textBox_FindItems_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode != Keys.Enter) return;
            string searchKey = textBox_FindItems.Text;
            listBox_Items.Items.Clear();
            foreach (var row in ItemsData)
                if (row.Value.ToLower().Contains(searchKey.ToLower())) listBox_Items.Items.Add(row.Key + " " + row.Value);
        }
        private void textBox_FindNpc_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode != Keys.Enter) return;
            string searchKey = textBox_FindNPC.Text;
            listBox_NPC.Items.Clear();
            foreach (var row in NpcData)
                if (row.Value.ToLower().Contains(searchKey.ToLower())) listBox_NPC.Items.Add(row.Key + " " + row.Value);
        }
        private void button_UnrideVehicle_Click(object sender, EventArgs e)
        {
            GetPrivatePlayer().RideVehicle("");
        }

        private void btnGiveStatPoints_Click(object sender, EventArgs e)
        {
            try
            {
                Player targetPlayer = GetPrivatePlayer();
                if (targetPlayer == null)
                {
                    MessageBox.Show("Please select an online player from the dropdown first!", "No Player Selected", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                ushort ptsToAdd = (ushort)numStatPoints.Value;
                if (ptsToAdd <= 0) ptsToAdd = 1;

                targetPlayer.Eqs.SkillPoints += ptsToAdd;

                // Sync full updated stats and stat points to client status window immediately
                targetPlayer.Eqs.Send8_1(true);

                // Persist to database
                cGlobal.gCharacterDataBase?.WritePlayer(targetPlayer.CharID, targetPlayer);

                targetPlayer.SendSystemMessage($"✨ [Server GUI] You were granted +{ptsToAdd} Stat Points! Total Available: {targetPlayer.Eqs.SkillPoints}");
                DebugSystem.Write($"[GUI] Granted +{ptsToAdd} stat points to {targetPlayer.CharName}. Total Available: {targetPlayer.Eqs.SkillPoints}");

                MessageBox.Show($"Successfully added +{ptsToAdd} stat points to {targetPlayer.CharName}!\nTotal Available Points: {targetPlayer.Eqs.SkillPoints}", "Points Added", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error giving stat points: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void btnResetStats_Click(object sender, EventArgs e)
        {
            try
            {
                Player targetPlayer = GetPrivatePlayer();
                if (targetPlayer == null)
                {
                    MessageBox.Show("Please select an online player from the dropdown first!", "No Player Selected", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                var confirm = MessageBox.Show($"Are you sure you want to reset all distributed stats for '{targetPlayer.CharName}'?\nAll invested STR, CON, INT, WIS, AGI points will be refunded back to Available Stat Points.", "Confirm Stat Reset", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (confirm != DialogResult.Yes) return;

                // Baseline starting stats are 10
                ushort refund = 0;
                if (targetPlayer.baseStr > 10) { refund += (ushort)(targetPlayer.baseStr - 10); targetPlayer.baseStr = 10; }
                if (targetPlayer.baseCon > 10) { refund += (ushort)(targetPlayer.baseCon - 10); targetPlayer.baseCon = 10; }
                if (targetPlayer.baseInt > 10) { refund += (ushort)(targetPlayer.baseInt - 10); targetPlayer.baseInt = 10; }
                if (targetPlayer.baseWis > 10) { refund += (ushort)(targetPlayer.baseWis - 10); targetPlayer.baseWis = 10; }
                if (targetPlayer.baseAgi > 10) { refund += (ushort)(targetPlayer.baseAgi - 10); targetPlayer.baseAgi = 10; }

                targetPlayer.Eqs.SkillPoints += refund;
                targetPlayer.Eqs.Send8_1(true);
                cGlobal.gCharacterDataBase?.WritePlayer(targetPlayer.CharID, targetPlayer);

                targetPlayer.SendSystemMessage($"🔄 [Server GUI] All base stats have been reset to 10! +{refund} Points refunded. Total Available: {targetPlayer.Eqs.SkillPoints}");
                DebugSystem.Write($"[GUI] Reset stats for {targetPlayer.CharName}. Refunded {refund} points. Total Available: {targetPlayer.Eqs.SkillPoints}");

                MessageBox.Show($"Stats reset successfully for {targetPlayer.CharName}!\nRefunded {refund} points.\nTotal Available Points: {targetPlayer.Eqs.SkillPoints}", "Stats Reset", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error resetting stats: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        #region User Management
        private void btnRefreshUsers_Click(object sender, EventArgs e)
        {
            try
            {
                var users = cGlobal.gUserDataBase.GetAllUsers();
                if (users != null)
                {
                    dataGridViewUsers.DataSource = users;
                    dataGridViewUsers.Columns["userID"].HeaderText = "ID";
                    dataGridViewUsers.Columns["username"].HeaderText = "Username";
                    dataGridViewUsers.Columns["password"].HeaderText = "Password";
                    dataGridViewUsers.Columns["email"].HeaderText = "Email";
                    // Cipher column only shown if it exists in database
                    if (users.Columns.Contains("cipher"))
                        dataGridViewUsers.Columns["cipher"].HeaderText = "Deletion Password";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error loading users: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }




        private void btnDeleteUser_Click(object sender, EventArgs e)
        {
            if (dataGridViewUsers.SelectedRows.Count == 0)
            {
                MessageBox.Show("Please select a user to delete.", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var row = dataGridViewUsers.SelectedRows[0];
            int userId = Convert.ToInt32(row.Cells["userID"].Value);
            string username = row.Cells["username"].Value?.ToString() ?? "";

            var result = MessageBox.Show($"Are you sure you want to delete user '{username}'?",
                "Confirm Delete", MessageBoxButtons.YesNo, MessageBoxIcon.Question);

            if (result == DialogResult.Yes)
            {
                if (cGlobal.gUserDataBase.DeleteUser(userId))
                {
                    MessageBox.Show("User deleted successfully.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    btnRefreshUsers_Click(sender, e);
                }
                else
                {
                    MessageBox.Show("Failed to delete user.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void btnChangePassword_Click(object sender, EventArgs e)
        {
            if (dataGridViewUsers.SelectedRows.Count == 0)
            {
                MessageBox.Show("Please select a user to change password.", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var row = dataGridViewUsers.SelectedRows[0];
            int userId = Convert.ToInt32(row.Cells["userID"].Value);
            string username = row.Cells["username"].Value?.ToString() ?? "";

            string newPassword = ShowInputDialog($"Enter new password for '{username}':", "Change Password");

            if (!string.IsNullOrWhiteSpace(newPassword))
            {
                if (cGlobal.gUserDataBase.UpdatePassword(userId, newPassword))
                {
                    MessageBox.Show("Password changed successfully.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    btnRefreshUsers_Click(sender, e);
                }
                else
                {
                    MessageBox.Show("Failed to change password.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private string ShowInputDialog(string text, string caption, string defaultValue = "")
        {
            Form prompt = new Form() { Width = 300, Height = 150, FormBorderStyle = FormBorderStyle.FixedDialog, Text = caption, StartPosition = FormStartPosition.CenterParent };
            Label textLabel = new Label() { Left = 20, Top = 20, Width = 250, Text = text };
            TextBox inputBox = new TextBox() { Left = 20, Top = 50, Width = 240, Text = defaultValue };
            Button confirmation = new Button() { Text = "OK", Left = 170, Width = 90, Top = 80, DialogResult = DialogResult.OK };
            prompt.Controls.Add(textLabel);
            prompt.Controls.Add(inputBox);
            prompt.Controls.Add(confirmation);
            prompt.AcceptButton = confirmation;
            return prompt.ShowDialog() == DialogResult.OK ? inputBox.Text : "";
        }
        #endregion

        #region Portal Management
        private void btnRefreshPortals_Click(object sender, EventArgs e)
        {
            try
            {
                dgvPortals.DataSource = cGlobal.gPortalDataBase.GetAllPortals();
                dgvDestinations.DataSource = cGlobal.gPortalDataBase.GetAllDestinations();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error loading portal data: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void btnAddPortal_Click(object sender, EventArgs e)
        {
            string mapID = ShowInputDialog("Map ID:", "Add Portal");
            if (string.IsNullOrWhiteSpace(mapID)) return;
            string portalID = ShowInputDialog("Portal ID:", "Add Portal");
            if (string.IsNullOrWhiteSpace(portalID)) return;
            string destID = ShowInputDialog("Destination ID:", "Add Portal");
            if (string.IsNullOrWhiteSpace(destID)) return;

            if (cGlobal.gPortalDataBase.AddPortal(uint.Parse(mapID), byte.Parse(portalID), byte.Parse(destID)))
            {
                MessageBox.Show("Portal added!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                btnRefreshPortals_Click(sender, e);
            }
            else
            {
                MessageBox.Show("Failed to add portal.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void btnDeletePortal_Click(object sender, EventArgs e)
        {
            if (dgvPortals.SelectedRows.Count == 0)
            {
                MessageBox.Show("Select a portal to delete.", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            int id = Convert.ToInt32(dgvPortals.SelectedRows[0].Cells["id"].Value);
            if (cGlobal.gPortalDataBase.DeletePortal(id))
            {
                MessageBox.Show("Portal deleted!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                btnRefreshPortals_Click(sender, e);
            }
        }

        private void btnAddDestination_Click(object sender, EventArgs e)
        {
            string srcMapID = ShowInputDialog("Source Map ID:", "Add Destination");
            if (string.IsNullOrWhiteSpace(srcMapID)) return;
            string destID = ShowInputDialog("Destination ID:", "Add Destination");
            if (string.IsNullOrWhiteSpace(destID)) return;
            string dstMap = ShowInputDialog("Target Map ID:", "Add Destination");
            if (string.IsNullOrWhiteSpace(dstMap)) return;
            string dstX = ShowInputDialog("Target X:", "Add Destination");
            if (string.IsNullOrWhiteSpace(dstX)) return;
            string dstY = ShowInputDialog("Target Y:", "Add Destination");
            if (string.IsNullOrWhiteSpace(dstY)) return;

            if (cGlobal.gPortalDataBase.AddDestination(uint.Parse(srcMapID), byte.Parse(destID), ushort.Parse(dstMap), ushort.Parse(dstX), ushort.Parse(dstY)))
            {
                MessageBox.Show("Destination added!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                btnRefreshPortals_Click(sender, e);
            }
            else
            {
                MessageBox.Show("Failed to add destination.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void btnDeleteDestination_Click(object sender, EventArgs e)
        {
            if (dgvDestinations.SelectedRows.Count == 0)
            {
                MessageBox.Show("Select a destination to delete.", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            int id = Convert.ToInt32(dgvDestinations.SelectedRows[0].Cells["id"].Value);
            if (cGlobal.gPortalDataBase.DeleteDestination(id))
            {
                MessageBox.Show("Destination deleted!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                btnRefreshPortals_Click(sender, e);
            }
        }

        private void btnEditPortal_Click(object sender, EventArgs e)
        {
            if (dgvPortals.SelectedRows.Count == 0) return;
            var row = dgvPortals.SelectedRows[0];
            int id = Convert.ToInt32(row.Cells["id"].Value);

            string mapID = ShowInputDialog("Map ID:", "Edit Portal", row.Cells["mapID"].Value.ToString());
            if (string.IsNullOrWhiteSpace(mapID)) return;
            string portalID = ShowInputDialog("Portal ID:", "Edit Portal", row.Cells["portalID"].Value.ToString());
            if (string.IsNullOrWhiteSpace(portalID)) return;
            string destID = ShowInputDialog("Destination ID:", "Edit Portal", row.Cells["destID"].Value.ToString());
            if (string.IsNullOrWhiteSpace(destID)) return;

            if (cGlobal.gPortalDataBase.UpdatePortal(id, uint.Parse(mapID), byte.Parse(portalID), byte.Parse(destID)))
            {
                MessageBox.Show("Portal updated!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                btnRefreshPortals_Click(sender, e);
            }
            else
            {
                MessageBox.Show("Failed to update portal.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void btnEditDestination_Click(object sender, EventArgs e)
        {
            if (dgvDestinations.SelectedRows.Count == 0) return;
            var row = dgvDestinations.SelectedRows[0];
            int id = Convert.ToInt32(row.Cells["id"].Value);

            string mapID = ShowInputDialog("Map ID:", "Edit Destination", row.Cells["mapID"].Value.ToString());
            if (string.IsNullOrWhiteSpace(mapID)) return;
            string destID = ShowInputDialog("Destination ID:", "Edit Destination", row.Cells["destID"].Value.ToString());
            if (string.IsNullOrWhiteSpace(destID)) return;
            string dstMap = ShowInputDialog("Target Map ID:", "Edit Destination", row.Cells["dstMap"].Value.ToString());
            if (string.IsNullOrWhiteSpace(dstMap)) return;
            string dstX = ShowInputDialog("Target X:", "Edit Destination", row.Cells["dstX"].Value.ToString());
            if (string.IsNullOrWhiteSpace(dstX)) return;
            string dstY = ShowInputDialog("Target Y:", "Edit Destination", row.Cells["dstY"].Value.ToString());
            if (string.IsNullOrWhiteSpace(dstY)) return;

            if (cGlobal.gPortalDataBase.UpdateDestination(id, uint.Parse(mapID), byte.Parse(destID), ushort.Parse(dstMap), ushort.Parse(dstX), ushort.Parse(dstY)))
            {
                MessageBox.Show("Destination updated!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                btnRefreshPortals_Click(sender, e);
            }
            else
            {
                MessageBox.Show("Failed to update destination.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        #endregion

        private void btnRefreshCharacters_Click(object sender, EventArgs e)
        {
            if (cGlobal.gCharacterDataBase != null)
            {
                dgvCharacters.DataSource = cGlobal.gCharacterDataBase.GetAllCharacters();
            }
        }

        private void btnDeleteCharacter_Click(object sender, EventArgs e)
        {
            if (dgvCharacters.SelectedRows.Count > 0)
            {
                try
                {
                    uint id = Convert.ToUInt32(dgvCharacters.SelectedRows[0].Cells["charID"].Value);
                    string charName = dgvCharacters.SelectedRows[0].Cells["name"].Value?.ToString() ?? "";

                    if (MessageBox.Show($"Are you sure you want to delete character '{charName}' (ID: {id})?\n\nThis will also delete:\n- All stats\n- All inventory items\n- All friendships",
                        "Confirm Delete", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
                    {
                        // Delete related data first (cascade delete)
                        try
                        {
                            // Delete stats
                            cGlobal.gCharacterDataBase.ExecuteNonQuery($"DELETE FROM stats WHERE charID = {id}");

                            // Delete inventory
                            cGlobal.gCharacterDataBase.ExecuteNonQuery($"DELETE FROM inventory WHERE charID = {id}");

                            // Delete friendships (both directions)
                            cGlobal.gGameDataBase.ExecuteNonQuery($"DELETE FROM Friends WHERE CharID1 = {id} OR CharID2 = {id}");

                            // Delete quests if table exists
                            try { cGlobal.gCharacterDataBase.ExecuteNonQuery($"DELETE FROM charquest WHERE charID = {id}"); } catch { }

                            // Delete tent data if table exists
                            try { cGlobal.gCharacterDataBase.ExecuteNonQuery($"DELETE FROM chartent WHERE charID = {id}"); } catch { }

                            // Delete unlocks if table exists
                            try { cGlobal.gCharacterDataBase.ExecuteNonQuery($"DELETE FROM charunlocks WHERE charID = {id}"); } catch { }

                            // Delete extended data if table exists
                            try { cGlobal.gCharacterDataBase.ExecuteNonQuery($"DELETE FROM charactersextdata WHERE charID = {id}"); } catch { }
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show($"Warning: Some related data could not be deleted:\n{ex.Message}", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        }

                        // Finally delete the character
                        cGlobal.gCharacterDataBase.DeleteCharacter(id);
                        MessageBox.Show("Character and all related data deleted successfully.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        btnRefreshCharacters_Click(sender, e);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Error deleting character: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            else
            {
                MessageBox.Show("Please select a character/row to delete.");
            }
        }

        private void btnEditCharacterData_Click(object sender, EventArgs e)
        {
            OpenCharacterEditor();
        }

        private void dgvCharacters_CellDoubleClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex >= 0)
            {
                OpenCharacterEditor();
            }
        }

        private void OpenCharacterEditor()
        {
            if (dgvCharacters.SelectedRows.Count > 0)
            {
                try
                {
                    uint id = Convert.ToUInt32(dgvCharacters.SelectedRows[0].Cells["charID"].Value);
                    string charName = dgvCharacters.SelectedRows[0].Cells["name"].Value?.ToString() ?? "Unknown";

                    using (var editor = new CharacterDataEditorForm(id, charName))
                    {
                        editor.ShowDialog(this);
                    }
                    btnRefreshCharacters_Click(null, null);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error opening character editor: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            else
            {
                MessageBox.Show("Please select a character to edit.", "Notice", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        #region Player Settings Tab
        private void btnRefreshSettings_Click(object sender, EventArgs e)
        {
            try
            {
                dgvSettings.Rows.Clear();
                dgvSettings.Columns.Clear();

                // Add columns
                dgvSettings.Columns.Add("CharID", "Char ID");
                dgvSettings.Columns.Add("CharName", "Name");
                dgvSettings.Columns.Add("PKABLE", "PK Mode");
                dgvSettings.Columns.Add("JOINABLE", "Join Mode");
                dgvSettings.Columns.Add("TRADABLE", "Trade Mode");

                // Get online players
                var onlinePlayers = cGlobal.gCharacterDataBase.GetOnlinePlayers();
                if (onlinePlayers != null)
                {
                    foreach (var player in onlinePlayers)
                    {
                        if (player.Settings != null)
                        {
                            dgvSettings.Rows.Add(
                                player.CharID,
                                player.CharName,
                                player.Settings.PKABLE ? "ON" : "OFF",
                                player.Settings.JOINABLE ? "ON" : "OFF",
                                player.Settings.TRADABLE ? "ON" : "OFF"
                            );
                        }
                    }
                }

                // Make PK/Join/Trade columns editable
                dgvSettings.Columns["CharID"].ReadOnly = true;
                dgvSettings.Columns["CharName"].ReadOnly = true;
                dgvSettings.Columns["PKABLE"].ReadOnly = false;
                dgvSettings.Columns["JOINABLE"].ReadOnly = false;
                dgvSettings.Columns["TRADABLE"].ReadOnly = false;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error refreshing settings: " + ex.Message);
            }
        }

        private void btnSaveSettings_Click(object sender, EventArgs e)
        {
            try
            {
                int savedCount = 0;
                foreach (DataGridViewRow row in dgvSettings.Rows)
                {
                    if (row.Cells["CharID"].Value == null) continue;

                    uint charID = Convert.ToUInt32(row.Cells["CharID"].Value);
                    string pkStr = row.Cells["PKABLE"].Value?.ToString() ?? "OFF";
                    string joinStr = row.Cells["JOINABLE"].Value?.ToString() ?? "OFF";
                    string tradeStr = row.Cells["TRADABLE"].Value?.ToString() ?? "OFF";

                    // Find online player
                    var onlinePlayers = cGlobal.gCharacterDataBase.GetOnlinePlayers();
                    var player = onlinePlayers?.FirstOrDefault(p => p.CharID == charID);

                    if (player?.Settings != null)
                    {
                        player.Settings.PKABLE = pkStr.ToUpper() == "ON" || pkStr == "1" || pkStr.ToUpper() == "TRUE";
                        player.Settings.JOINABLE = joinStr.ToUpper() == "ON" || joinStr == "1" || joinStr.ToUpper() == "TRUE";
                        player.Settings.TRADABLE = tradeStr.ToUpper() == "ON" || tradeStr == "1" || tradeStr.ToUpper() == "TRUE";
                        savedCount++;
                    }
                }

                MessageBox.Show($"Settings saved for {savedCount} player(s).");
                btnRefreshSettings_Click(sender, e); // Refresh view
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error saving settings: " + ex.Message);
            }
        }
        #endregion

        #region Friends Management
        private void btnRefreshFriends_Click(object sender, EventArgs e)
        {
            try
            {
                // Query Friends table with character names
                var friendsData = cGlobal.gGameDataBase.GetDataTable(@"
                    SELECT 
                        f.CharID1, 
                        f.CharID2, 
                        f.AddedDate,
                        c1.name as CharName1,
                        c2.name as CharName2
                    FROM Friends f
                    LEFT JOIN characters c1 ON f.CharID1 = c1.charID
                    LEFT JOIN characters c2 ON f.CharID2 = c2.charID
                    ORDER BY f.AddedDate DESC
                ");

                if (friendsData != null)
                {
                    dgvFriends.DataSource = friendsData;
                    dgvFriends.Columns["CharID1"].HeaderText = "Char ID 1";
                    dgvFriends.Columns["CharID2"].HeaderText = "Char ID 2";
                    dgvFriends.Columns["CharName1"].HeaderText = "Character 1";
                    dgvFriends.Columns["CharName2"].HeaderText = "Character 2";
                    dgvFriends.Columns["AddedDate"].HeaderText = "Added Date";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error loading friends: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void btnDeleteFriendship_Click(object sender, EventArgs e)
        {
            if (dgvFriends.SelectedRows.Count == 0)
            {
                MessageBox.Show("Please select a friendship to delete.", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var row = dgvFriends.SelectedRows[0];
            uint charID1 = Convert.ToUInt32(row.Cells["CharID1"].Value);
            uint charID2 = Convert.ToUInt32(row.Cells["CharID2"].Value);
            string name1 = row.Cells["CharName1"].Value?.ToString() ?? "Unknown";
            string name2 = row.Cells["CharName2"].Value?.ToString() ?? "Unknown";

            var result = MessageBox.Show($"Delete friendship between '{name1}' and '{name2}'?",
                "Confirm Delete", MessageBoxButtons.YesNo, MessageBoxIcon.Question);

            if (result == DialogResult.Yes)
            {
                try
                {
                    string deleteQuery = $"DELETE FROM Friends WHERE CharID1 = {charID1} AND CharID2 = {charID2}";
                    cGlobal.gGameDataBase.ExecuteNonQuery(deleteQuery);
                    MessageBox.Show("Friendship deleted successfully.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    btnRefreshFriends_Click(sender, e);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Error deleting friendship: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }
        #endregion

        #region Inventory Management
        private void btnRefreshInventory_Click(object sender, EventArgs e)
        {
            try
            {
                // Get selected character from filter
                uint charID = 0;
                if (cmbCharacterFilter.SelectedItem != null)
                {
                    string selected = cmbCharacterFilter.SelectedItem.ToString();
                    charID = uint.Parse(selected.Split('-')[0].Trim());
                }

                if (charID == 0)
                {
                    MessageBox.Show("Please select a character first.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                // Query inventory with item names
                var inventoryData = cGlobal.gGameDataBase.GetDataTable($@"
                    SELECT 
                        i.charID,
                        c.name as CharName,
                        i.pos as Slot,
                        i.itemID,
                        i.dmg as Damage
                    FROM inventory i
                    LEFT JOIN characters c ON i.charID = c.charID
                    WHERE i.charID = {charID} AND i.storID = 1
                    ORDER BY i.pos
                ");

                if (inventoryData != null)
                {
                    dgvInventory.DataSource = inventoryData;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error loading inventory: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void btnDeleteItem_Click(object sender, EventArgs e)
        {
            if (dgvInventory.SelectedRows.Count == 0)
            {
                MessageBox.Show("Please select an item to delete.", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var row = dgvInventory.SelectedRows[0];
            uint charID = Convert.ToUInt32(row.Cells["charID"].Value);
            int slot = Convert.ToInt32(row.Cells["Slot"].Value);

            var result = MessageBox.Show($"Delete item at slot {slot}?",
                "Confirm Delete", MessageBoxButtons.YesNo, MessageBoxIcon.Question);

            if (result == DialogResult.Yes)
            {
                try
                {
                    string deleteQuery = $"DELETE FROM inventory WHERE charID = {charID} AND pos = {slot} AND storID = 1";
                    cGlobal.gGameDataBase.ExecuteNonQuery(deleteQuery);
                    MessageBox.Show("Item deleted successfully.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    btnRefreshInventory_Click(null, null);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Error deleting item: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void cmbCharacterFilter_SelectedIndexChanged(object sender, EventArgs e)
        {
            btnRefreshInventory_Click(sender, e);
        }
        #endregion

        #region Stats Management
        private void btnRefreshStats_Click(object sender, EventArgs e)
        {
            try
            {
                // Get selected character from filter
                uint charID = 0;
                if (cmbCharacterFilterStats.SelectedItem != null)
                {
                    string selected = cmbCharacterFilterStats.SelectedItem.ToString();
                    charID = uint.Parse(selected.Split('-')[0].Trim());
                }

                if (charID == 0)
                {
                    MessageBox.Show("Please select a character first.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                // Query stats
                var statsData = cGlobal.gCharacterDataBase.GetDataTable($@"
                    SELECT 
                        s.charID,
                        c.name as CharName,
                        s.statID,
                        s.StatusUp
                    FROM stats s
                    LEFT JOIN characters c ON s.charID = c.charID
                    WHERE s.charID = {charID}
                    ORDER BY s.statID
                ");

                if (statsData != null)
                {
                    dgvStats.DataSource = statsData;
                    dgvStats.Columns["StatusUp"].ReadOnly = false; // Make editable
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error loading stats: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void btnEditStat_Click(object sender, EventArgs e)
        {
            if (dgvStats.SelectedRows.Count == 0)
            {
                MessageBox.Show("Please select a stat to edit.", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var row = dgvStats.SelectedRows[0];
            uint charID = Convert.ToUInt32(row.Cells["charID"].Value);
            int statID = Convert.ToInt32(row.Cells["statID"].Value);
            int currentValue = Convert.ToInt32(row.Cells["StatusUp"].Value);

            string newValue = ShowInputDialog($"Edit Stat ID {statID}:", "Edit Stat", currentValue.ToString());

            if (!string.IsNullOrWhiteSpace(newValue))
            {
                // Validate input is numeric
                if (!int.TryParse(newValue, out int newStatValue))
                {
                    MessageBox.Show("Please enter a valid number.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                try
                {
                    string updateQuery = $"UPDATE stats SET StatusUp = {newStatValue} WHERE charID = {charID} AND statID = {statID}";
                    cGlobal.gCharacterDataBase.ExecuteNonQuery(updateQuery);

                    // If player is currently online, update memory and sync client UI immediately
                    var onlinePlayer = cGlobal.gLoginServer?.GetAllPlayers()?.FirstOrDefault(p => p.CharID == charID);
                    if (onlinePlayer != null)
                    {
                        switch (statID)
                        {
                            case 38: onlinePlayer.Eqs.SkillPoints = (ushort)newStatValue; break;
                            case 28: onlinePlayer.baseStr = (ushort)newStatValue; break;
                            case 29: onlinePlayer.baseCon = (ushort)newStatValue; break;
                            case 27: onlinePlayer.baseInt = (ushort)newStatValue; break;
                            case 33: onlinePlayer.baseWis = (ushort)newStatValue; break;
                            case 30: onlinePlayer.baseAgi = (ushort)newStatValue; break;
                        }
                        onlinePlayer.Eqs.Send8_1(true);
                        onlinePlayer.SendSystemMessage($"✨ [Server GUI] Stat ID {statID} updated to {newStatValue}!");
                    }

                    MessageBox.Show("Stat updated successfully.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    btnRefreshStats_Click(null, null);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Error updating stat: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void cmbCharacterFilterStats_SelectedIndexChanged(object sender, EventArgs e)
        {
            btnRefreshStats_Click(sender, e);
        }

        private void LoadCharacterFilters()
        {
            try
            {
                var characters = cGlobal.gCharacterDataBase.GetAllCharacters();
                if (characters != null)
                {
                    cmbCharacterFilter.Items.Clear();
                    cmbCharacterFilterStats.Items.Clear();

                    foreach (System.Data.DataRow row in characters.Rows)
                    {
                        string item = $"{row["charID"]} - {row["name"]}";
                        cmbCharacterFilter.Items.Add(item);
                        cmbCharacterFilterStats.Items.Add(item);
                    }
                }
            }
            catch { }
        }
        #endregion

        #region Chest Drops Management
        private void LoadChestDropTargets()
        {
            try
            {
                cmbChestDropTarget.Items.Clear();

                // Map pools
                foreach (var mapId in Game.Maps.ChestDropManager.MapLootTables.Keys)
                {
                    cmbChestDropTarget.Items.Add($"Map {mapId}");
                }

                // Category pools
                foreach (var cat in Game.Maps.ChestDropManager.CategoryLootTables.Keys)
                {
                    cmbChestDropTarget.Items.Add($"Category: {cat}");
                }

                if (cmbChestDropTarget.Items.Count > 0)
                {
                    cmbChestDropTarget.SelectedIndex = 0;
                }

                numRespawnSeconds.Value = Game.Maps.ChestDropManager.DefaultRespawnSeconds;
            }
            catch (Exception ex)
            {
                DebugSystem.Write(DebugItemType.Error, "Error loading Chest Drop targets: " + ex.Message);
            }
        }

        private void cmbChestDropTarget_SelectedIndexChanged(object sender, EventArgs e)
        {
            LoadChestDropsForSelectedTarget();
        }

        private void LoadChestDropsForSelectedTarget()
        {
            try
            {
                if (cmbChestDropTarget.SelectedItem == null) return;
                string selected = cmbChestDropTarget.SelectedItem.ToString();

                bool isMap = selected.StartsWith("Map ");
                string key = isMap ? selected.Substring(4).Trim() : selected.Substring("Category: ".Length).Trim();

                var drops = Game.Maps.ChestDropManager.GetLootForTarget(key, isMap);

                var dt = new System.Data.DataTable();
                dt.Columns.Add("ItemID", typeof(ushort));
                dt.Columns.Add("ItemName", typeof(string));
                dt.Columns.Add("Count", typeof(byte));
                dt.Columns.Add("Weight", typeof(int));

                foreach (var drop in drops)
                {
                    dt.Rows.Add(drop.ItemID, drop.ItemName, drop.Count, drop.Weight);
                }

                dgvChestDrops.DataSource = dt;
                dgvChestDrops.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error loading chest drops: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void txtNewItemId_TextChanged(object sender, EventArgs e)
        {
            try
            {
                if (ushort.TryParse(txtNewItemId.Text.Trim(), out ushort itemId))
                {
                    var itemInfo = cGlobal.ItemDatManager?.GetItemByID(itemId);
                    if (itemInfo != null && itemInfo.ItemName != null)
                    {
                        string name = System.Text.Encoding.Default.GetString(itemInfo.ItemName).Trim('\0', ' ');
                        if (!string.IsNullOrEmpty(name))
                        {
                            txtNewItemName.Text = name;
                        }
                    }
                }
            }
            catch { }
        }

        private void btnRefreshChestDrops_Click(object sender, EventArgs e)
        {
            LoadChestDropTargets();
            LoadChestDropsForSelectedTarget();
        }

        private void btnAddChestDrop_Click(object sender, EventArgs e)
        {
            try
            {
                if (!ushort.TryParse(txtNewItemId.Text.Trim(), out ushort itemId))
                {
                    MessageBox.Show("Please enter a valid numeric Item ID.", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                string name = txtNewItemName.Text.Trim();
                if (string.IsNullOrEmpty(name))
                {
                    name = $"Item #{itemId}";
                }

                byte count = (byte)numNewItemCount.Value;
                int weight = (int)numNewItemWeight.Value;

                if (dgvChestDrops.DataSource is System.Data.DataTable dt)
                {
                    dt.Rows.Add(itemId, name, count, weight);
                }

                txtNewItemId.Clear();
                txtNewItemName.Clear();
                numNewItemCount.Value = 1;
                numNewItemWeight.Value = 50;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error adding drop: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void btnDeleteChestDrop_Click(object sender, EventArgs e)
        {
            try
            {
                if (dgvChestDrops.SelectedRows.Count == 0)
                {
                    MessageBox.Show("Please select a drop item row to delete.", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                foreach (DataGridViewRow row in dgvChestDrops.SelectedRows)
                {
                    if (!row.IsNewRow)
                    {
                        dgvChestDrops.Rows.Remove(row);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error deleting drop: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void btnSaveChestDrops_Click(object sender, EventArgs e)
        {
            try
            {
                if (cmbChestDropTarget.SelectedItem == null) return;
                string selected = cmbChestDropTarget.SelectedItem.ToString();

                bool isMap = selected.StartsWith("Map ");
                string key = isMap ? selected.Substring(4).Trim() : selected.Substring("Category: ".Length).Trim();

                var newEntries = new List<Game.Maps.ChestLootEntry>();

                if (dgvChestDrops.DataSource is System.Data.DataTable dt)
                {
                    foreach (System.Data.DataRow row in dt.Rows)
                    {
                        ushort itemId = Convert.ToUInt16(row["ItemID"]);
                        string name = row["ItemName"]?.ToString() ?? "";
                        byte count = Convert.ToByte(row["Count"]);
                        int weight = Convert.ToInt32(row["Weight"]);

                        newEntries.Add(new Game.Maps.ChestLootEntry(itemId, name, count, weight));
                    }
                }

                Game.Maps.ChestDropManager.SetLootForTarget(key, isMap, newEntries);
                Game.Maps.ChestDropManager.DefaultRespawnSeconds = (int)numRespawnSeconds.Value;
                Game.Maps.ChestDropManager.SaveToFile();

                MessageBox.Show("Chest drop table and respawn configuration saved successfully!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error saving chest drops: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        #endregion

        #region Safe Shutdown & Data Save
        private void btnSaveAllNow_Click(object sender, EventArgs e)
        {
            try
            {
                int savedCount = 0;
                var online = cGlobal.gCharacterDataBase?.GetOnlinePlayers();
                if (online != null && online.Count > 0)
                {
                    foreach (var p in online)
                    {
                        try
                        {
                            cGlobal.gCharacterDataBase.WritePlayer(p.CharID, p);
                            savedCount++;
                        }
                        catch (Exception ex)
                        {
                            DebugSystem.Write($"[ManualSave] Error saving player {p.CharName}: {ex.Message}");
                        }
                    }
                }

                // Save drop tables and configs
                Game.Maps.ChestDropManager.SaveToFile();

                try
                {
                    cGlobal.SrvSettings?.SaveSettings(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\PServer\\Config.settings.wlo");
                }
                catch { }

                DebugSystem.Write(DebugItemType.Info_Light, $"[ManualSave] Successfully saved all server data and {savedCount} online players.");
                MessageBox.Show($"All server data and {savedCount} online characters (inventories, equipment, stats, and configs) were saved successfully!", "Save Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error performing manual save: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void btnSafeShutdown_Click(object sender, EventArgs e)
        {
            var res = MessageBox.Show(
                "Are you sure you want to perform a Safe Server Shutdown?\n\nThis will:\n1. Notify all online players.\n2. Save all inventories, equipment, stats, positions, and gold.\n3. Save all game settings and drop configs.\n4. Gracefully terminate server sockets and close the application.",
                "Confirm Safe Shutdown",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning
            );

            if (res != DialogResult.Yes) return;

            btnSafeShutdown.Enabled = false;
            btnSaveAllNow.Enabled = false;
            ThreadPool.QueueUserWorkItem(_ => PerformSafeShutdown());
        }
        #endregion

        #region GM Management Tab
        private ListBox lstGmList;
        private TextBox txtGmInput;
        private Button btnAddGm;
        private Button btnRemoveGm;

        private void SetupGmTab()
        {
            try
            {
                TabPage tabGm = new TabPage("👑 GM Management");
                tabGm.BackColor = System.Drawing.Color.White;

                Label lblHeader = new Label
                {
                    Text = "GM & Administrator Authorization List\n(Only characters and accounts listed below can use cheat/admin chat commands)",
                    Location = new System.Drawing.Point(20, 15),
                    Size = new System.Drawing.Size(600, 35),
                    Font = new System.Drawing.Font("Segoe UI", 9.5f, System.Drawing.FontStyle.Bold)
                };

                lstGmList = new ListBox
                {
                    Location = new System.Drawing.Point(20, 55),
                    Size = new System.Drawing.Size(300, 380),
                    Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left,
                    Font = new System.Drawing.Font("Segoe UI", 10f)
                };

                Label lblInput = new Label
                {
                    Text = "Character Name / Account Username:",
                    Location = new System.Drawing.Point(340, 55),
                    Size = new System.Drawing.Size(250, 20),
                    Font = new System.Drawing.Font("Segoe UI", 9f, System.Drawing.FontStyle.Regular)
                };

                txtGmInput = new TextBox
                {
                    Location = new System.Drawing.Point(340, 80),
                    Size = new System.Drawing.Size(250, 25),
                    Font = new System.Drawing.Font("Segoe UI", 10f)
                };

                btnAddGm = new Button
                {
                    Text = "➕ Add to GM List",
                    Location = new System.Drawing.Point(340, 115),
                    Size = new System.Drawing.Size(150, 35),
                    BackColor = System.Drawing.Color.LightGreen,
                    Font = new System.Drawing.Font("Segoe UI", 9.5f, System.Drawing.FontStyle.Bold)
                };
                btnAddGm.Click += (s, e) =>
                {
                    if (!string.IsNullOrWhiteSpace(txtGmInput.Text))
                    {
                        if (Game.PlayerRelated.GmManager.AddGm(txtGmInput.Text))
                        {
                            txtGmInput.Clear();
                            RefreshGmList();
                            MessageBox.Show("GM added successfully!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        }
                    }
                };

                btnRemoveGm = new Button
                {
                    Text = "➖ Remove Selected GM",
                    Location = new System.Drawing.Point(340, 160),
                    Size = new System.Drawing.Size(180, 35),
                    BackColor = System.Drawing.Color.LightCoral,
                    Font = new System.Drawing.Font("Segoe UI", 9.5f, System.Drawing.FontStyle.Bold)
                };
                btnRemoveGm.Click += (s, e) =>
                {
                    if (lstGmList.SelectedItem != null)
                    {
                        string selected = lstGmList.SelectedItem.ToString();
                        if (Game.PlayerRelated.GmManager.RemoveGm(selected))
                        {
                            RefreshGmList();
                            MessageBox.Show($"Removed '{selected}' from GM list.", "Removed", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        }
                    }
                };

                tabGm.Controls.Add(lblHeader);
                tabGm.Controls.Add(lstGmList);
                tabGm.Controls.Add(lblInput);
                tabGm.Controls.Add(txtGmInput);
                tabGm.Controls.Add(btnAddGm);
                tabGm.Controls.Add(btnRemoveGm);

                if (this.tabControl3 != null)
                {
                    this.tabControl3.TabPages.Add(tabGm);
                }

                RefreshGmList();
                Game.PlayerRelated.GmManager.OnGmListChanged += () =>
                {
                    if (this.IsHandleCreated)
                    {
                        this.BeginInvoke(new Action(RefreshGmList));
                    }
                };
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[GUI] Error creating GM Tab: {ex.Message}");
            }
        }

        private void RefreshGmList()
        {
            if (lstGmList == null) return;
            lstGmList.Items.Clear();
            foreach (var gm in Game.PlayerRelated.GmManager.GetGmList())
            {
                lstGmList.Items.Add(gm);
            }
        }
        #endregion

        #region Item Mall Management Tab
        private DataGridView dgvMallCatalog;
        private TextBox txtMallItemId;
        private TextBox txtMallItemName;
        private ComboBox cmbMallCategory;
        private NumericUpDown numMallCost;
        private NumericUpDown numMallCount;
        private Button btnAddMallItem;
        private Button btnDeleteMallItem;
        private Button btnSaveMallCatalog;
        private ComboBox cmbMallPlayers;
        private NumericUpDown numPlayerPoints;
        private Button btnAddPoints;
        private Button btnSetPoints;

        private void SetupItemMallTab()
        {
            try
            {
                TabPage tabMall = new TabPage("🛍️ Item Mall")
                {
                    BackColor = System.Drawing.Color.WhiteSmoke,
                    Padding = new Padding(6)
                };

                SplitContainer splitMall = new SplitContainer
                {
                    Dock = DockStyle.Fill,
                    Orientation = Orientation.Vertical,
                    SplitterDistance = 640,
                    SplitterWidth = 6
                };

                // === LEFT PANEL: Catalog DataGridView + Header + Buttons ===
                Panel pnlLeftTop = new Panel
                {
                    Dock = DockStyle.Top,
                    Height = 32,
                    BackColor = System.Drawing.Color.Transparent
                };

                Label lblHeader = new Label
                {
                    Text = "🛍️ Item Mall Catalog (Active in-game items)",
                    Location = new System.Drawing.Point(4, 6),
                    AutoSize = true,
                    Font = new System.Drawing.Font("Segoe UI", 10f, System.Drawing.FontStyle.Bold),
                    ForeColor = System.Drawing.Color.DarkSlateBlue
                };
                pnlLeftTop.Controls.Add(lblHeader);

                Panel pnlLeftBottom = new Panel
                {
                    Dock = DockStyle.Bottom,
                    Height = 44,
                    BackColor = System.Drawing.Color.Transparent
                };

                Button btnMoveUp = new Button
                {
                    Text = "⬆️ Move Up",
                    Location = new System.Drawing.Point(4, 6),
                    Size = new System.Drawing.Size(100, 32),
                    Font = new System.Drawing.Font("Segoe UI", 8.5f, System.Drawing.FontStyle.Bold)
                };
                btnMoveUp.Click += (s, e) =>
                {
                    if (dgvMallCatalog.SelectedRows.Count > 0)
                    {
                        int idx = dgvMallCatalog.SelectedRows[0].Index;
                        if (Game.PlayerRelated.ItemMallManager.MoveItem(idx, true))
                        {
                            RefreshMallGrid();
                            if (idx > 0 && idx - 1 < dgvMallCatalog.Rows.Count)
                                dgvMallCatalog.Rows[idx - 1].Selected = true;
                        }
                    }
                };

                Button btnMoveDown = new Button
                {
                    Text = "⬇️ Move Down",
                    Location = new System.Drawing.Point(110, 6),
                    Size = new System.Drawing.Size(100, 32),
                    Font = new System.Drawing.Font("Segoe UI", 8.5f, System.Drawing.FontStyle.Bold)
                };
                btnMoveDown.Click += (s, e) =>
                {
                    if (dgvMallCatalog.SelectedRows.Count > 0)
                    {
                        int idx = dgvMallCatalog.SelectedRows[0].Index;
                        if (Game.PlayerRelated.ItemMallManager.MoveItem(idx, false))
                        {
                            RefreshMallGrid();
                            if (idx + 1 < dgvMallCatalog.Rows.Count)
                                dgvMallCatalog.Rows[idx + 1].Selected = true;
                        }
                    }
                };

                Button btnReload = new Button
                {
                    Text = "🔄 Reload from File",
                    Location = new System.Drawing.Point(216, 6),
                    Size = new System.Drawing.Size(140, 32),
                    Font = new System.Drawing.Font("Segoe UI", 8.5f, System.Drawing.FontStyle.Bold)
                };
                btnReload.Click += (s, e) =>
                {
                    Game.PlayerRelated.ItemMallManager.LoadFromFile();
                    RefreshMallGrid();
                    MessageBox.Show("Item Mall reloaded from Data/item_mall.txt!", "Reloaded", MessageBoxButtons.OK, MessageBoxIcon.Information);
                };

                pnlLeftBottom.Controls.Add(btnMoveUp);
                pnlLeftBottom.Controls.Add(btnMoveDown);
                pnlLeftBottom.Controls.Add(btnReload);

                dgvMallCatalog = new DataGridView
                {
                    Dock = DockStyle.Fill,
                    AllowUserToAddRows = false,
                    SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                    MultiSelect = false,
                    AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                    BackgroundColor = System.Drawing.Color.WhiteSmoke
                };

                dgvMallCatalog.SelectionChanged += (s, e) =>
                {
                    if (dgvMallCatalog.SelectedRows.Count > 0)
                    {
                        var row = dgvMallCatalog.SelectedRows[0];
                        if (row != null && row.Cells["ItemID"]?.Value != null)
                        {
                            txtMallItemId.Text = row.Cells["ItemID"].Value.ToString();
                            txtMallItemName.Text = row.Cells["ItemName"]?.Value?.ToString() ?? "";
                            string cat = row.Cells["Category"]?.Value?.ToString() ?? "Hot";
                            if (cmbMallCategory.Items.Contains(cat)) cmbMallCategory.SelectedItem = cat;
                            if (decimal.TryParse(row.Cells["PointCost"]?.Value?.ToString(), out decimal cost)) numMallCost.Value = Math.Min(numMallCost.Maximum, Math.Max(numMallCost.Minimum, cost));
                            if (decimal.TryParse(row.Cells["Count"]?.Value?.ToString(), out decimal count)) numMallCount.Value = Math.Min(numMallCount.Maximum, Math.Max(numMallCount.Minimum, count));
                        }
                    }
                };

                splitMall.Panel1.Controls.Add(pnlLeftTop);
                splitMall.Panel1.Controls.Add(pnlLeftBottom);
                splitMall.Panel1.Controls.Add(dgvMallCatalog);
                dgvMallCatalog.BringToFront();

                // === RIGHT PANEL: GroupBox Add/Edit Item & Player IM Points ===
                GroupBox grpEditItem = new GroupBox
                {
                    Text = "Add / Edit Item Details",
                    Location = new System.Drawing.Point(8, 8),
                    Size = new System.Drawing.Size(340, 245),
                    Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                    Font = new System.Drawing.Font("Segoe UI", 9f)
                };

                Label lId = new Label { Text = "Item ID:", Location = new System.Drawing.Point(15, 25), Size = new System.Drawing.Size(70, 20) };
                txtMallItemId = new TextBox { Location = new System.Drawing.Point(90, 22), Size = new System.Drawing.Size(180, 23), Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
                txtMallItemId.TextChanged += (s, e) =>
                {
                    try
                    {
                        if (ushort.TryParse(txtMallItemId.Text.Trim(), out ushort queryId))
                        {
                            var itemInfo = cGlobal.ItemDatManager?.GetItemByID(queryId);
                            if (itemInfo != null && itemInfo.ItemName != null)
                            {
                                string name = System.Text.Encoding.Default.GetString(itemInfo.ItemName).Trim('\0', ' ');
                                if (!string.IsNullOrEmpty(name))
                                {
                                    txtMallItemName.Text = name;
                                }
                            }
                        }
                    }
                    catch { }
                };

                Label lName = new Label { Text = "Name:", Location = new System.Drawing.Point(15, 55), Size = new System.Drawing.Size(70, 20) };
                txtMallItemName = new TextBox { Location = new System.Drawing.Point(90, 52), Size = new System.Drawing.Size(180, 23), Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };

                Label lCat = new Label { Text = "Category:", Location = new System.Drawing.Point(15, 85), Size = new System.Drawing.Size(70, 20) };
                cmbMallCategory = new ComboBox { Location = new System.Drawing.Point(90, 82), Size = new System.Drawing.Size(180, 23), Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right, DropDownStyle = ComboBoxStyle.DropDownList };
                cmbMallCategory.Items.AddRange(new string[] { "Hot", "Grocery", "Furniture", "Armory", "Weaponry" });
                cmbMallCategory.SelectedIndex = 0;

                Label lCost = new Label { Text = "IM Points:", Location = new System.Drawing.Point(15, 115), Size = new System.Drawing.Size(70, 20) };
                numMallCost = new NumericUpDown { Location = new System.Drawing.Point(90, 112), Size = new System.Drawing.Size(180, 23), Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right, Minimum = 1, Maximum = 999999, Value = 100 };

                Label lCount = new Label { Text = "Quantity:", Location = new System.Drawing.Point(15, 145), Size = new System.Drawing.Size(70, 20) };
                numMallCount = new NumericUpDown { Location = new System.Drawing.Point(90, 142), Size = new System.Drawing.Size(180, 23), Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right, Minimum = 1, Maximum = 255, Value = 1 };

                btnAddMallItem = new Button
                {
                    Text = "➕ Add / Update",
                    Location = new System.Drawing.Point(15, 185),
                    Size = new System.Drawing.Size(130, 36),
                    BackColor = System.Drawing.Color.LightGreen,
                    Font = new System.Drawing.Font("Segoe UI", 9f, System.Drawing.FontStyle.Bold)
                };
                btnAddMallItem.Click += (s, e) =>
                {
                    if (ushort.TryParse(txtMallItemId.Text.Trim(), out ushort id) && !string.IsNullOrWhiteSpace(txtMallItemName.Text))
                    {
                        Game.PlayerRelated.ItemMallManager.AddOrUpdateItem(id, txtMallItemName.Text.Trim(), cmbMallCategory.SelectedItem.ToString(), (int)numMallCost.Value, (byte)numMallCount.Value);
                        RefreshMallGrid();
                        MessageBox.Show("Item Mall catalog updated!", "Saved", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    else
                    {
                        MessageBox.Show("Please provide a valid Item ID and Name.", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                };

                btnDeleteMallItem = new Button
                {
                    Text = "🗑️ Delete",
                    Location = new System.Drawing.Point(155, 185),
                    Size = new System.Drawing.Size(115, 36),
                    BackColor = System.Drawing.Color.LightCoral,
                    Font = new System.Drawing.Font("Segoe UI", 9f, System.Drawing.FontStyle.Bold)
                };
                btnDeleteMallItem.Click += (s, e) =>
                {
                    if (dgvMallCatalog.SelectedRows.Count > 0)
                    {
                        var row = dgvMallCatalog.SelectedRows[0];
                        ushort id = Convert.ToUInt16(row.Cells["ItemID"].Value);
                        if (Game.PlayerRelated.ItemMallManager.RemoveItem(id))
                        {
                            RefreshMallGrid();
                        }
                    }
                };

                grpEditItem.Controls.Add(lId);
                grpEditItem.Controls.Add(txtMallItemId);
                grpEditItem.Controls.Add(lName);
                grpEditItem.Controls.Add(txtMallItemName);
                grpEditItem.Controls.Add(lCat);
                grpEditItem.Controls.Add(cmbMallCategory);
                grpEditItem.Controls.Add(lCost);
                grpEditItem.Controls.Add(numMallCost);
                grpEditItem.Controls.Add(lCount);
                grpEditItem.Controls.Add(numMallCount);
                grpEditItem.Controls.Add(btnAddMallItem);
                grpEditItem.Controls.Add(btnDeleteMallItem);

                // GroupBox: Player IM Points
                GroupBox grpPoints = new GroupBox
                {
                    Text = "Player IM Points (Nakit Puan)",
                    Location = new System.Drawing.Point(8, 265),
                    Size = new System.Drawing.Size(340, 155),
                    Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                    Font = new System.Drawing.Font("Segoe UI", 9f)
                };

                Label lblTarget = new Label { Text = "Target Player / User:", Location = new System.Drawing.Point(15, 24), Size = new System.Drawing.Size(130, 18) };

                cmbMallPlayers = new ComboBox
                {
                    Location = new System.Drawing.Point(15, 44),
                    Size = new System.Drawing.Size(255, 23),
                    Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                    DropDownStyle = ComboBoxStyle.DropDown
                };
                cmbMallPlayers.DropDown += (s, e) => RefreshOnlineMallPlayers();
                cmbMallPlayers.SelectedIndexChanged += (s, e) =>
                {
                    if (cmbMallPlayers.SelectedItem is Player p)
                    {
                        numPlayerPoints.Value = Game.PlayerRelated.ItemMallManager.GetUserPoints(p);
                    }
                };

                Label lblPts = new Label { Text = "Points:", Location = new System.Drawing.Point(15, 78), Size = new System.Drawing.Size(55, 20) };
                numPlayerPoints = new NumericUpDown
                {
                    Location = new System.Drawing.Point(75, 76),
                    Size = new System.Drawing.Size(195, 23),
                    Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                    Minimum = 0,
                    Maximum = 99999999,
                    Value = 1000
                };

                btnAddPoints = new Button
                {
                    Text = "➕ Give Points",
                    Location = new System.Drawing.Point(15, 110),
                    Size = new System.Drawing.Size(120, 34),
                    BackColor = System.Drawing.Color.LightSkyBlue,
                    Font = new System.Drawing.Font("Segoe UI", 8.5f, System.Drawing.FontStyle.Bold)
                };
                btnAddPoints.Click += (s, e) =>
                {
                    string target = cmbMallPlayers.Text.Trim();
                    if (cmbMallPlayers.SelectedItem is Player p) target = p.CharName;
                    if (GivePointsToAccountOrPlayer(target, (int)numPlayerPoints.Value, true, out string msg))
                        MessageBox.Show(msg, "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    else
                        MessageBox.Show(msg, "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                };

                btnSetPoints = new Button
                {
                    Text = "💾 Set Exact",
                    Location = new System.Drawing.Point(145, 110),
                    Size = new System.Drawing.Size(125, 34),
                    BackColor = System.Drawing.Color.LightGoldenrodYellow,
                    Font = new System.Drawing.Font("Segoe UI", 8.5f, System.Drawing.FontStyle.Bold)
                };
                btnSetPoints.Click += (s, e) =>
                {
                    string target = cmbMallPlayers.Text.Trim();
                    if (cmbMallPlayers.SelectedItem is Player p) target = p.CharName;
                    if (GivePointsToAccountOrPlayer(target, (int)numPlayerPoints.Value, false, out string msg))
                        MessageBox.Show(msg, "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    else
                        MessageBox.Show(msg, "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                };

                grpPoints.Controls.Add(lblTarget);
                grpPoints.Controls.Add(cmbMallPlayers);
                grpPoints.Controls.Add(lblPts);
                grpPoints.Controls.Add(numPlayerPoints);
                grpPoints.Controls.Add(btnAddPoints);
                grpPoints.Controls.Add(btnSetPoints);

                splitMall.Panel2.Controls.Add(grpEditItem);
                splitMall.Panel2.Controls.Add(grpPoints);

                tabMall.Controls.Add(splitMall);

                if (this.tabControl3 != null)
                {
                    this.tabControl3.TabPages.Add(tabMall);
                }

                RefreshMallGrid();
                Game.PlayerRelated.ItemMallManager.OnCatalogChanged += () =>
                {
                    if (this.IsHandleCreated)
                    {
                        this.BeginInvoke(new Action(() => RefreshMallGrid()));
                    }
                };
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[GUI] Error creating Item Mall Tab: {ex.Message}");
            }
        }

        private void RefreshMallGrid()
        {
            if (dgvMallCatalog == null) return;
            var list = Game.PlayerRelated.ItemMallManager.GetCatalog();

            System.Data.DataTable dt = new System.Data.DataTable();
            dt.Columns.Add("ItemID", typeof(ushort));
            dt.Columns.Add("ItemName", typeof(string));
            dt.Columns.Add("Category", typeof(string));
            dt.Columns.Add("PointCost", typeof(int));
            dt.Columns.Add("Count", typeof(byte));

            foreach (var item in list)
            {
                dt.Rows.Add(item.ItemID, item.ItemName, item.Category, item.PointCost, item.Count);
            }

            dgvMallCatalog.DataSource = dt;
        }

        private void RefreshOnlineMallPlayers()
        {
            if (cmbMallPlayers == null) return;
            cmbMallPlayers.Items.Clear();
            var online = cGlobal.gCharacterDataBase?.GetOnlinePlayers();
            if (online != null)
            {
                foreach (var p in online)
                {
                    cmbMallPlayers.Items.Add(p);
                }
            }
            if (cmbMallPlayers.Items.Count > 0 && cmbMallPlayers.SelectedIndex == -1)
            {
                cmbMallPlayers.SelectedIndex = 0;
            }
        }

        private bool GivePointsToAccountOrPlayer(string targetNameOrId, int points, bool isAdd, out string resultMsg)
        {
            resultMsg = "";
            if (string.IsNullOrWhiteSpace(targetNameOrId))
            {
                resultMsg = "Please specify a Player Name, Username, or UserID.";
                return false;
            }

            targetNameOrId = targetNameOrId.Trim();

            // 1. Check online players
            var online = cGlobal.gCharacterDataBase?.GetOnlinePlayers();
            Player targetPlayer = null;
            if (online != null)
            {
                targetPlayer = online.FirstOrDefault(p => 
                    p.CharName.Equals(targetNameOrId, StringComparison.OrdinalIgnoreCase) ||
                    (p.UserAccount != null && p.UserAccount.UserName.Equals(targetNameOrId, StringComparison.OrdinalIgnoreCase)) ||
                    (uint.TryParse(targetNameOrId, out uint uid) && (p.UserID == uid || p.CharID == uid))
                );
            }

            if (targetPlayer != null)
            {
                if (isAdd)
                    Game.PlayerRelated.ItemMallManager.AddUserPoints(targetPlayer, points);
                else
                    Game.PlayerRelated.ItemMallManager.SetUserPoints(targetPlayer, points);

                if (cGlobal.gUserDataBase != null && targetPlayer.UserAccount != null)
                {
                    cGlobal.gUserDataBase.SetIMPoints(targetPlayer.UserAccount.DataBaseID, targetPlayer.UserAccount.IM);
                }

                targetPlayer.SendSystemMessage($"[Item Mall] Administrator updated your IM balance! Current: {targetPlayer.UserAccount.IM} Points.");
                resultMsg = $"Successfully updated {targetPlayer.CharName} ({targetPlayer.UserAccount?.UserName}) to {targetPlayer.UserAccount.IM} IM Points!";
                return true;
            }

            // 2. Offline account in UserDataBase
            if (cGlobal.gUserDataBase != null)
            {
                try
                {
                    string query = uint.TryParse(targetNameOrId, out uint uId) 
                        ? $"SELECT * FROM users WHERE userID = {uId} LIMIT 1"
                        : $"SELECT * FROM users WHERE username = '{targetNameOrId}' LIMIT 1";

                    var dt = cGlobal.gUserDataBase.GetDataTable(query);
                    if (dt != null && dt.Rows.Count > 0)
                    {
                        uint dbUserId = Convert.ToUInt32(dt.Rows[0]["userID"]);
                        string uname = dt.Rows[0]["username"].ToString();
                        int currentIm = dt.Columns.Contains("IM") && dt.Rows[0]["IM"] != DBNull.Value ? Convert.ToInt32(dt.Rows[0]["IM"]) : 0;
                        int newIm = isAdd ? currentIm + points : points;
                        if (newIm < 0) newIm = 0;

                        cGlobal.gUserDataBase.SetIMPoints(dbUserId, newIm);
                        resultMsg = $"Successfully updated offline user '{uname}' (ID: {dbUserId}) to {newIm} IM Points!";
                        return true;
                    }
                }
                catch (Exception ex)
                {
                    resultMsg = "Database error: " + ex.Message;
                    return false;
                }
            }

            resultMsg = $"Could not find player or user matching '{targetNameOrId}'.";
            return false;
        }
        #endregion

        #region Monster Drop Management Tab
        private DataGridView dgvMonsterList;
        private DataGridView dgvMonsterDrops;
        private TextBox txtMonsterSearch;
        private Label lblSelectedMonster;
        private uint selectedMonsterTid = 0;

        private TextBox txtDropItemId;
        private TextBox txtDropItemName;
        private NumericUpDown numDropMinCount;
        private NumericUpDown numDropMaxCount;
        private NumericUpDown numDropRate;
        private Button btnAddDrop;
        private Button btnDeleteDrop;
        private Button btnSaveDrops;
        private Button btnReloadDrops;

        private void SetupMonsterDropsTab()
        {
            try
            {
                TabPage tabDrops = new TabPage("🐲 Monster Drops")
                {
                    BackColor = System.Drawing.Color.WhiteSmoke,
                    Padding = new Padding(6)
                };

                SplitContainer splitDrops = new SplitContainer
                {
                    Dock = DockStyle.Fill,
                    Orientation = Orientation.Vertical,
                    SplitterDistance = 350,
                    SplitterWidth = 6
                };

                // === LEFT PANEL: Search + Monster List ===
                Panel pnlSearch = new Panel
                {
                    Dock = DockStyle.Top,
                    Height = 55,
                    BackColor = System.Drawing.Color.Transparent
                };

                Label lblSearch = new Label
                {
                    Text = "Search Monster (ID / Name):",
                    Location = new System.Drawing.Point(4, 4),
                    Size = new System.Drawing.Size(200, 18),
                    Font = new System.Drawing.Font("Segoe UI", 8.5f, System.Drawing.FontStyle.Bold)
                };

                txtMonsterSearch = new TextBox
                {
                    Location = new System.Drawing.Point(4, 24),
                    Size = new System.Drawing.Size(330, 23),
                    Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                    Font = new System.Drawing.Font("Segoe UI", 9f)
                };
                txtMonsterSearch.TextChanged += (s, e) => RefreshMonsterListGrid();

                pnlSearch.Controls.Add(lblSearch);
                pnlSearch.Controls.Add(txtMonsterSearch);

                dgvMonsterList = new DataGridView
                {
                    Dock = DockStyle.Fill,
                    AllowUserToAddRows = false,
                    SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                    MultiSelect = false,
                    AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                    BackgroundColor = System.Drawing.Color.WhiteSmoke,
                    ReadOnly = true
                };

                dgvMonsterList.SelectionChanged += (s, e) =>
                {
                    if (dgvMonsterList.SelectedRows.Count > 0)
                    {
                        var row = dgvMonsterList.SelectedRows[0];
                        if (row?.Cells["TID"]?.Value != null && uint.TryParse(row.Cells["TID"].Value.ToString(), out uint tid))
                        {
                            selectedMonsterTid = tid;
                            string mName = row.Cells["MonsterName"]?.Value?.ToString() ?? $"Monster #{tid}";
                            lblSelectedMonster.Text = $"🐲 Selected Monster: {mName} (TID: {tid})";
                            RefreshMonsterDropsGrid(tid);
                        }
                    }
                };

                splitDrops.Panel1.Controls.Add(pnlSearch);
                splitDrops.Panel1.Controls.Add(dgvMonsterList);
                dgvMonsterList.BringToFront();

                // === RIGHT PANEL: Drops Grid + Add/Edit GroupBox ===
                Panel pnlSelectedTop = new Panel
                {
                    Dock = DockStyle.Top,
                    Height = 28,
                    BackColor = System.Drawing.Color.Transparent
                };

                lblSelectedMonster = new Label
                {
                    Text = "🐲 Selected Monster: (Please select a monster from the list)",
                    Location = new System.Drawing.Point(4, 4),
                    AutoSize = true,
                    Font = new System.Drawing.Font("Segoe UI", 9.5f, System.Drawing.FontStyle.Bold),
                    ForeColor = System.Drawing.Color.DarkBlue
                };
                pnlSelectedTop.Controls.Add(lblSelectedMonster);

                dgvMonsterDrops = new DataGridView
                {
                    Dock = DockStyle.Fill,
                    AllowUserToAddRows = false,
                    SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                    MultiSelect = false,
                    AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                    BackgroundColor = System.Drawing.Color.WhiteSmoke,
                    ReadOnly = true
                };

                dgvMonsterDrops.SelectionChanged += (s, e) =>
                {
                    if (dgvMonsterDrops.SelectedRows.Count > 0)
                    {
                        var row = dgvMonsterDrops.SelectedRows[0];
                        if (row?.Cells["ItemID"]?.Value != null)
                        {
                            txtDropItemId.Text = row.Cells["ItemID"].Value.ToString();
                            txtDropItemName.Text = row.Cells["ItemName"]?.Value?.ToString() ?? "";
                            if (decimal.TryParse(row.Cells["Min"]?.Value?.ToString(), out decimal min)) numDropMinCount.Value = min;
                            if (decimal.TryParse(row.Cells["Max"]?.Value?.ToString(), out decimal max)) numDropMaxCount.Value = max;
                            if (decimal.TryParse(row.Cells["Rate%"]?.Value?.ToString(), out decimal rate)) numDropRate.Value = rate;
                        }
                    }
                };

                // Drop Item Editor GroupBox
                GroupBox grpDropEdit = new GroupBox
                {
                    Text = "Add / Edit Drop Item",
                    Dock = DockStyle.Bottom,
                    Height = 200,
                    Font = new System.Drawing.Font("Segoe UI", 8.5f)
                };

                Label lblItemId = new Label { Text = "Item ID:", Location = new System.Drawing.Point(10, 22), Size = new System.Drawing.Size(55, 18) };
                txtDropItemId = new TextBox { Location = new System.Drawing.Point(65, 20), Size = new System.Drawing.Size(70, 22) };
                txtDropItemId.TextChanged += (s, e) =>
                {
                    try
                    {
                        if (ushort.TryParse(txtDropItemId.Text.Trim(), out ushort iid))
                        {
                            var it = cGlobal.ItemDatManager?.GetItemByID(iid);
                            if (it != null && it.ItemName != null)
                            {
                                string name = System.Text.Encoding.Default.GetString(it.ItemName).Trim('\0', ' ');
                                if (!string.IsNullOrEmpty(name))
                                {
                                    txtDropItemName.Text = name;
                                }
                            }
                        }
                    }
                    catch { }
                };

                Label lblItemName = new Label { Text = "Name:", Location = new System.Drawing.Point(145, 22), Size = new System.Drawing.Size(45, 18) };
                txtDropItemName = new TextBox { Location = new System.Drawing.Point(190, 20), Size = new System.Drawing.Size(220, 22) };

                Label lblMin = new Label { Text = "Min:", Location = new System.Drawing.Point(10, 52), Size = new System.Drawing.Size(35, 18) };
                numDropMinCount = new NumericUpDown { Location = new System.Drawing.Point(45, 50), Size = new System.Drawing.Size(50, 22), Minimum = 1, Maximum = 99, Value = 1 };

                Label lblMax = new Label { Text = "Max:", Location = new System.Drawing.Point(105, 52), Size = new System.Drawing.Size(35, 18) };
                numDropMaxCount = new NumericUpDown { Location = new System.Drawing.Point(140, 50), Size = new System.Drawing.Size(50, 22), Minimum = 1, Maximum = 99, Value = 1 };

                Label lblRate = new Label { Text = "Drop Rate %:", Location = new System.Drawing.Point(200, 52), Size = new System.Drawing.Size(80, 18) };
                numDropRate = new NumericUpDown { Location = new System.Drawing.Point(280, 50), Size = new System.Drawing.Size(70, 22), Minimum = 0, Maximum = 100, DecimalPlaces = 1, Value = 50 };

                btnAddDrop = new Button
                {
                    Text = "➕ Add / Update Drop",
                    Location = new System.Drawing.Point(10, 85),
                    Size = new System.Drawing.Size(195, 32),
                    BackColor = System.Drawing.Color.LightGreen,
                    Font = new System.Drawing.Font("Segoe UI", 9f, System.Drawing.FontStyle.Bold)
                };
                btnAddDrop.Click += (s, e) =>
                {
                    if (selectedMonsterTid == 0)
                    {
                        MessageBox.Show("Please select a monster first.", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }
                    if (!ushort.TryParse(txtDropItemId.Text.Trim(), out ushort iid) || iid == 0)
                    {
                        MessageBox.Show("Please enter a valid Item ID.", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }

                    string iName = string.IsNullOrWhiteSpace(txtDropItemName.Text) ? $"Item #{iid}" : txtDropItemName.Text.Trim();
                    byte min = (byte)numDropMinCount.Value;
                    byte max = (byte)numDropMaxCount.Value;
                    if (max < min) max = min;
                    double rate = (double)numDropRate.Value;

                    Game.Battle.MonsterDropManager.AddOrUpdateDrop(selectedMonsterTid, iid, iName, min, max, rate);
                    RefreshMonsterDropsGrid(selectedMonsterTid);
                    RefreshMonsterListGrid();
                    MessageBox.Show($"Successfully saved drop: {iName} ({rate}%) for Monster TID {selectedMonsterTid}!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                };

                btnDeleteDrop = new Button
                {
                    Text = "🗑️ Delete Drop",
                    Location = new System.Drawing.Point(215, 85),
                    Size = new System.Drawing.Size(195, 32),
                    BackColor = System.Drawing.Color.LightCoral,
                    Font = new System.Drawing.Font("Segoe UI", 9f, System.Drawing.FontStyle.Bold)
                };
                btnDeleteDrop.Click += (s, e) =>
                {
                    if (selectedMonsterTid == 0 || !ushort.TryParse(txtDropItemId.Text.Trim(), out ushort iid))
                    {
                        MessageBox.Show("Please select a drop to delete.", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }

                    if (Game.Battle.MonsterDropManager.RemoveDrop(selectedMonsterTid, iid))
                    {
                        RefreshMonsterDropsGrid(selectedMonsterTid);
                        RefreshMonsterListGrid();
                        MessageBox.Show($"Removed Item #{iid} from monster drops.", "Deleted", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                };

                Button btnClearMonster = new Button
                {
                    Text = "🧹 Clear Monster's Drops",
                    Location = new System.Drawing.Point(10, 122),
                    Size = new System.Drawing.Size(195, 30),
                    BackColor = System.Drawing.Color.SandyBrown,
                    Font = new System.Drawing.Font("Segoe UI", 8.5f, System.Drawing.FontStyle.Bold)
                };
                btnClearMonster.Click += (s, e) =>
                {
                    if (selectedMonsterTid == 0)
                    {
                        MessageBox.Show("Please select a monster first.", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }

                    if (MessageBox.Show($"Are you sure you want to clear all drops for Monster TID {selectedMonsterTid}?", "Confirm Clear Monster", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                    {
                        Game.Battle.MonsterDropManager.ClearMonsterDrops(selectedMonsterTid);
                        RefreshMonsterDropsGrid(selectedMonsterTid);
                        RefreshMonsterListGrid();
                        MessageBox.Show($"Cleared all drops for Monster TID {selectedMonsterTid}.", "Cleared", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                };

                btnSaveDrops = new Button
                {
                    Text = "💾 Save All Drops to File",
                    Location = new System.Drawing.Point(215, 122),
                    Size = new System.Drawing.Size(195, 30),
                    BackColor = System.Drawing.Color.LightSkyBlue,
                    Font = new System.Drawing.Font("Segoe UI", 8.5f, System.Drawing.FontStyle.Bold)
                };
                btnSaveDrops.Click += (s, e) =>
                {
                    Game.Battle.MonsterDropManager.SaveToFile();
                    MessageBox.Show("All monster drops saved to Data/monster_drops.txt!", "Saved", MessageBoxButtons.OK, MessageBoxIcon.Information);
                };

                btnReloadDrops = new Button
                {
                    Text = "🔄 Reload from Npc.dat",
                    Location = new System.Drawing.Point(10, 156),
                    Size = new System.Drawing.Size(195, 30),
                    BackColor = System.Drawing.Color.LightYellow,
                    Font = new System.Drawing.Font("Segoe UI", 8.5f, System.Drawing.FontStyle.Bold)
                };
                btnReloadDrops.Click += (s, e) =>
                {
                    string npcDat = System.IO.Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "Data", "Npc.dat");
                    Game.Battle.MonsterDropManager.LoadFromNpcDat(npcDat);
                    RefreshMonsterListGrid();
                    if (selectedMonsterTid > 0) RefreshMonsterDropsGrid(selectedMonsterTid);
                    MessageBox.Show("Reloaded authentic drops from Npc.dat!", "Reloaded", MessageBoxButtons.OK, MessageBoxIcon.Information);
                };

                Button btnClearAllDrops = new Button
                {
                    Text = "❌ Clear ALL Drop Tables",
                    Location = new System.Drawing.Point(215, 156),
                    Size = new System.Drawing.Size(195, 30),
                    BackColor = System.Drawing.Color.Crimson,
                    ForeColor = System.Drawing.Color.White,
                    Font = new System.Drawing.Font("Segoe UI", 8.5f, System.Drawing.FontStyle.Bold)
                };
                btnClearAllDrops.Click += (s, e) =>
                {
                    var res = MessageBox.Show("Are you sure you want to completely DELETE and CLEAR ALL monster drop tables?\n\nThis action cannot be undone unless you reload from Npc.dat.", "Confirm Delete All Drop Tables", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                    if (res == DialogResult.Yes)
                    {
                        Game.Battle.MonsterDropManager.ClearAllDrops();
                        RefreshMonsterListGrid();
                        if (selectedMonsterTid > 0) RefreshMonsterDropsGrid(selectedMonsterTid);
                        MessageBox.Show("All monster drop tables have been successfully cleared!", "All Drops Cleared", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                };

                grpDropEdit.Controls.Add(lblItemId);
                grpDropEdit.Controls.Add(txtDropItemId);
                grpDropEdit.Controls.Add(lblItemName);
                grpDropEdit.Controls.Add(txtDropItemName);
                grpDropEdit.Controls.Add(lblMin);
                grpDropEdit.Controls.Add(numDropMinCount);
                grpDropEdit.Controls.Add(lblMax);
                grpDropEdit.Controls.Add(numDropMaxCount);
                grpDropEdit.Controls.Add(lblRate);
                grpDropEdit.Controls.Add(numDropRate);
                grpDropEdit.Controls.Add(btnAddDrop);
                grpDropEdit.Controls.Add(btnDeleteDrop);
                grpDropEdit.Controls.Add(btnClearMonster);
                grpDropEdit.Controls.Add(btnSaveDrops);
                grpDropEdit.Controls.Add(btnReloadDrops);
                grpDropEdit.Controls.Add(btnClearAllDrops);

                splitDrops.Panel2.Controls.Add(pnlSelectedTop);
                splitDrops.Panel2.Controls.Add(grpDropEdit);
                splitDrops.Panel2.Controls.Add(dgvMonsterDrops);
                dgvMonsterDrops.BringToFront();

                tabDrops.Controls.Add(splitDrops);

                if (this.tabControl3 != null)
                {
                    this.tabControl3.TabPages.Add(tabDrops);
                }

                RefreshMonsterListGrid();
                Game.Battle.MonsterDropManager.OnLootTablesChanged += () =>
                {
                    if (this.IsHandleCreated)
                    {
                        this.BeginInvoke(new Action(() =>
                        {
                            RefreshMonsterListGrid();
                            if (selectedMonsterTid > 0) RefreshMonsterDropsGrid(selectedMonsterTid);
                        }));
                    }
                };
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[GUI] Error creating Monster Drops Tab: {ex.Message}");
            }
        }

        private static Dictionary<int, string> _cachedNpcNames = null;

        private static string GetNpcDisplayName(uint npcId)
        {
            if (_cachedNpcNames == null)
            {
                _cachedNpcNames = new Dictionary<int, string>();
                string jsonPath = System.IO.Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "Data", "npc.json");
                if (System.IO.File.Exists(jsonPath))
                {
                    try
                    {
                        string jsonText = System.IO.File.ReadAllText(jsonPath);
                        var matches = System.Text.RegularExpressions.Regex.Matches(jsonText, @"""(\d+)""\s*:\s*""([^""]+)""");
                        foreach (System.Text.RegularExpressions.Match match in matches)
                        {
                            if (int.TryParse(match.Groups[1].Value, out int jid))
                            {
                                _cachedNpcNames[jid] = match.Groups[2].Value;
                            }
                        }
                    }
                    catch { }
                }
            }
            if (_cachedNpcNames.TryGetValue((int)npcId, out string name))
            {
                return name;
            }
            return $"Monster #{npcId}";
        }

        private void RefreshMonsterListGrid()
        {
            if (dgvMonsterList == null) return;
            string filter = txtMonsterSearch?.Text?.Trim().ToLower() ?? "";
            var drops = Game.Battle.MonsterDropManager.GetAllDrops();

            System.Data.DataTable dt = new System.Data.DataTable();
            dt.Columns.Add("TID", typeof(uint));
            dt.Columns.Add("MonsterName", typeof(string));
            dt.Columns.Add("Drops", typeof(int));

            foreach (var kvp in drops.OrderBy(k => k.Key))
            {
                string mName = GetNpcDisplayName(kvp.Key);

                if (string.IsNullOrEmpty(filter) || kvp.Key.ToString().Contains(filter) || mName.ToLower().Contains(filter))
                {
                    dt.Rows.Add(kvp.Key, mName, kvp.Value.Count);
                }
            }

            dgvMonsterList.DataSource = dt;

            // Auto select first monster if available and none selected yet
            if (dgvMonsterList.Rows.Count > 0 && selectedMonsterTid == 0)
            {
                var firstRow = dgvMonsterList.Rows[0];
                if (firstRow?.Cells["TID"]?.Value != null && uint.TryParse(firstRow.Cells["TID"].Value.ToString(), out uint tid))
                {
                    selectedMonsterTid = tid;
                    string mName = firstRow.Cells["MonsterName"]?.Value?.ToString() ?? $"Monster #{tid}";
                    if (lblSelectedMonster != null) lblSelectedMonster.Text = $"🐲 Selected Monster: {mName} (TID: {tid})";
                    RefreshMonsterDropsGrid(tid);
                }
            }
        }

        private void RefreshMonsterDropsGrid(uint monsterTid)
        {
            if (dgvMonsterDrops == null) return;
            var list = Game.Battle.MonsterDropManager.GetDrops(monsterTid);

            System.Data.DataTable dt = new System.Data.DataTable();
            dt.Columns.Add("ItemID", typeof(ushort));
            dt.Columns.Add("ItemName", typeof(string));
            dt.Columns.Add("Min", typeof(byte));
            dt.Columns.Add("Max", typeof(byte));
            dt.Columns.Add("Rate%", typeof(double));

            foreach (var d in list)
            {
                dt.Rows.Add(d.ItemID, d.ItemName, d.MinCount, d.MaxCount, d.DropRatePercent);
            }

            dgvMonsterDrops.DataSource = dt;
        }
        #endregion

        #region Quest Manager GUI Tab
        private TabPage tabQuests;
        private DataGridView dgvQuests;
        private TextBox txtQuestSearch;
        private Label lblQuestCount;

        // Editor inputs
        private TextBox txtQId, txtQName, txtQNpcPattern, txtQDesc, txtQIntro, txtQInProgress, txtQComplete, txtQAlreadyDone;
        private TextBox txtQBattleMonsterName, txtQRewardCompanionName, txtQRewardItems, txtQRequiredItems, txtQPrereqs, txtQStepsJson;
        private ComboBox cmbQType;
        private NumericUpDown numQNpcTid, numQBattleMonsterId, numQRewardGold, numQRewardExp, numQRewardCompanionId;
        private Button btnSaveQuest, btnNewQuest, btnDeleteQuest, btnReloadQuests;

        private void SetupQuestManagerTab()
        {
            try
            {
                tabQuests = new TabPage("📜 Quest DB Manager")
                {
                    BackColor = System.Drawing.Color.WhiteSmoke,
                    Padding = new Padding(6)
                };

                SplitContainer split = new SplitContainer
                {
                    Dock = DockStyle.Fill,
                    Orientation = Orientation.Vertical,
                    SplitterDistance = 420,
                    SplitterWidth = 6
                };

                // === LEFT PANEL: Search, Counter, Quest Grid ===
                Panel pnlLeftTop = new Panel
                {
                    Dock = DockStyle.Top,
                    Height = 70,
                    BackColor = System.Drawing.Color.Transparent
                };

                Label lblHeader = new Label
                {
                    Text = "📜 Quests DB Manager (Live SQLite)",
                    Font = new System.Drawing.Font("Segoe UI", 10.5f, System.Drawing.FontStyle.Bold),
                    ForeColor = System.Drawing.Color.DarkSlateBlue,
                    Location = new System.Drawing.Point(4, 4),
                    AutoSize = true
                };

                lblQuestCount = new Label
                {
                    Text = "Total Quests: 0",
                    Font = new System.Drawing.Font("Segoe UI", 8.5f, System.Drawing.FontStyle.Bold),
                    ForeColor = System.Drawing.Color.DimGray,
                    Location = new System.Drawing.Point(4, 26),
                    AutoSize = true
                };

                Label lblSearch = new Label
                {
                    Text = "🔍 Search:",
                    Font = new System.Drawing.Font("Segoe UI", 8.5f, System.Drawing.FontStyle.Bold),
                    Location = new System.Drawing.Point(4, 46),
                    AutoSize = true
                };

                txtQuestSearch = new TextBox
                {
                    Location = new System.Drawing.Point(68, 44),
                    Size = new System.Drawing.Size(250, 22),
                    Font = new System.Drawing.Font("Segoe UI", 9f)
                };
                txtQuestSearch.TextChanged += (s, e) => RefreshQuestGrid(txtQuestSearch.Text);

                btnReloadQuests = new Button
                {
                    Text = "🔄 Refresh",
                    Location = new System.Drawing.Point(325, 42),
                    Size = new System.Drawing.Size(85, 26),
                    BackColor = System.Drawing.Color.LightSkyBlue,
                    Font = new System.Drawing.Font("Segoe UI", 8.5f, System.Drawing.FontStyle.Bold)
                };
                btnReloadQuests.Click += (s, e) => RefreshQuestGrid(txtQuestSearch.Text);

                pnlLeftTop.Controls.AddRange(new Control[] { lblHeader, lblQuestCount, lblSearch, txtQuestSearch, btnReloadQuests });

                // Quest Grid
                dgvQuests = new DataGridView
                {
                    Dock = DockStyle.Fill,
                    ReadOnly = true,
                    AllowUserToAddRows = false,
                    AllowUserToDeleteRows = false,
                    SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                    MultiSelect = false,
                    BackgroundColor = System.Drawing.Color.White,
                    RowHeadersVisible = false,
                    Font = new System.Drawing.Font("Segoe UI", 8.5f)
                };
                dgvQuests.SelectionChanged += (s, e) => OnQuestGridSelectionChanged();

                split.Panel1.Controls.Add(dgvQuests);
                split.Panel1.Controls.Add(pnlLeftTop);

                // === RIGHT PANEL: Editor & Form Controls ===
                GroupBox grpQuestEdit = new GroupBox
                {
                    Text = "✏️ Quest Editor & Details",
                    Dock = DockStyle.Fill,
                    Font = new System.Drawing.Font("Segoe UI", 9f, System.Drawing.FontStyle.Bold)
                };

                // Top Toolbar for Actions
                Panel pnlActions = new Panel
                {
                    Dock = DockStyle.Top,
                    Height = 42,
                    BackColor = System.Drawing.Color.Transparent
                };

                btnSaveQuest = new Button
                {
                    Text = "💾 Save / Update Quest",
                    Location = new System.Drawing.Point(8, 6),
                    Size = new System.Drawing.Size(160, 30),
                    BackColor = System.Drawing.Color.LightGreen,
                    Font = new System.Drawing.Font("Segoe UI", 8.5f, System.Drawing.FontStyle.Bold)
                };
                btnSaveQuest.Click += (s, e) => SaveCurrentQuest();

                btnNewQuest = new Button
                {
                    Text = "➕ New Quest",
                    Location = new System.Drawing.Point(176, 6),
                    Size = new System.Drawing.Size(110, 30),
                    BackColor = System.Drawing.Color.LightYellow,
                    Font = new System.Drawing.Font("Segoe UI", 8.5f, System.Drawing.FontStyle.Bold)
                };
                btnNewQuest.Click += (s, e) => ClearQuestInputs();

                btnDeleteQuest = new Button
                {
                    Text = "🗑️ Delete Quest",
                    Location = new System.Drawing.Point(294, 6),
                    Size = new System.Drawing.Size(110, 30),
                    BackColor = System.Drawing.Color.MistyRose,
                    ForeColor = System.Drawing.Color.DarkRed,
                    Font = new System.Drawing.Font("Segoe UI", 8.5f, System.Drawing.FontStyle.Bold)
                };
                btnDeleteQuest.Click += (s, e) => DeleteCurrentQuest();

                Button btnReimportQuests = new Button
                {
                    Text = "🔄 Reset & Import quests.json",
                    Location = new System.Drawing.Point(412, 6),
                    Size = new System.Drawing.Size(190, 30),
                    BackColor = System.Drawing.Color.LightCyan,
                    Font = new System.Drawing.Font("Segoe UI", 8.5f, System.Drawing.FontStyle.Bold)
                };
                btnReimportQuests.Click += (s, e) =>
                {
                    if (MessageBox.Show("Tüm görevleri sıfırlayıp Data/quests.json dosyasındaki 2.154 resmi görevi yüklemek istiyor musunuz?", "Görevleri Yenile", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                    {
                        var db = cGlobal.gGameDataBase ?? DataBase.GameDataBase.GlobalInstance;
                        DataBase.QuestDataBase.ReimportCleanQuests(db);
                        RefreshQuestGrid(txtQuestSearch?.Text);
                        MessageBox.Show("2.154 resmi sistem görevi başarıyla yüklendi ve güncellendi!", "Başarılı", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                };

                pnlActions.Controls.AddRange(new Control[] { btnSaveQuest, btnNewQuest, btnDeleteQuest, btnReimportQuests });

                Panel pnlScroll = new Panel
                {
                    Dock = DockStyle.Fill,
                    AutoScroll = true
                };

                int py = 8;
                int lblW = 125;
                int inputW = 500;

                // Row 1: QuestID, Type, NPC Pattern, TID
                Label l1 = new Label { Text = "Quest ID:", Location = new System.Drawing.Point(8, py), AutoSize = true };
                txtQId = new TextBox { Location = new System.Drawing.Point(lblW, py), Size = new System.Drawing.Size(75, 22), Font = new System.Drawing.Font("Segoe UI", 9f) };
                
                Label l3 = new Label { Text = "Type:", Location = new System.Drawing.Point(210, py), AutoSize = true };
                cmbQType = new ComboBox { Location = new System.Drawing.Point(250, py), Size = new System.Drawing.Size(120, 22), DropDownStyle = ComboBoxStyle.DropDownList, Font = new System.Drawing.Font("Segoe UI", 8.5f) };
                cmbQType.Items.AddRange(new object[] { "0 - Dialogue", "1 - ItemCollection", "2 - MonsterBattle", "3 - CompanionRecruit" });
                cmbQType.SelectedIndex = 0;

                Label l4 = new Label { Text = "NPC Pattern:", Location = new System.Drawing.Point(380, py), AutoSize = true };
                txtQNpcPattern = new TextBox { Location = new System.Drawing.Point(460, py), Size = new System.Drawing.Size(80, 22), Font = new System.Drawing.Font("Segoe UI", 9f) };

                Label l5 = new Label { Text = "TID:", Location = new System.Drawing.Point(550, py), AutoSize = true };
                numQNpcTid = new NumericUpDown { Location = new System.Drawing.Point(580, py), Size = new System.Drawing.Size(60, 22), Maximum = 65535, Font = new System.Drawing.Font("Segoe UI", 9f) };

                pnlScroll.Controls.AddRange(new Control[] { l1, txtQId, l3, cmbQType, l4, txtQNpcPattern, l5, numQNpcTid });
                py += 30;

                // Row 2: Title / Name
                Label l2 = new Label { Text = "Title / Name:", Location = new System.Drawing.Point(8, py), AutoSize = true };
                txtQName = new TextBox { Location = new System.Drawing.Point(lblW, py), Size = new System.Drawing.Size(inputW, 22), Font = new System.Drawing.Font("Segoe UI", 9f) };
                pnlScroll.Controls.AddRange(new Control[] { l2, txtQName });
                py += 28;

                // Row 3: Description
                Label l6 = new Label { Text = "Description:", Location = new System.Drawing.Point(8, py), AutoSize = true };
                txtQDesc = new TextBox { Location = new System.Drawing.Point(lblW, py), Size = new System.Drawing.Size(inputW, 40), Multiline = true, Font = new System.Drawing.Font("Segoe UI", 8.5f), ScrollBars = ScrollBars.Vertical };
                pnlScroll.Controls.AddRange(new Control[] { l6, txtQDesc });
                py += 46;

                // Row 4: Dialogues
                Label l7 = new Label { Text = "Intro Dialogue:", Location = new System.Drawing.Point(8, py), AutoSize = true };
                txtQIntro = new TextBox { Location = new System.Drawing.Point(lblW, py), Size = new System.Drawing.Size(inputW, 35), Multiline = true, Font = new System.Drawing.Font("Segoe UI", 8.5f), ScrollBars = ScrollBars.Vertical };
                pnlScroll.Controls.AddRange(new Control[] { l7, txtQIntro });
                py += 40;

                Label l8 = new Label { Text = "In Progress:", Location = new System.Drawing.Point(8, py), AutoSize = true };
                txtQInProgress = new TextBox { Location = new System.Drawing.Point(lblW, py), Size = new System.Drawing.Size(inputW, 30), Multiline = true, Font = new System.Drawing.Font("Segoe UI", 8.5f), ScrollBars = ScrollBars.Vertical };
                pnlScroll.Controls.AddRange(new Control[] { l8, txtQInProgress });
                py += 35;

                Label l9 = new Label { Text = "Complete Dialog:", Location = new System.Drawing.Point(8, py), AutoSize = true };
                txtQComplete = new TextBox { Location = new System.Drawing.Point(lblW, py), Size = new System.Drawing.Size(inputW, 30), Multiline = true, Font = new System.Drawing.Font("Segoe UI", 8.5f), ScrollBars = ScrollBars.Vertical };
                pnlScroll.Controls.AddRange(new Control[] { l9, txtQComplete });
                py += 35;

                Label l10 = new Label { Text = "Already Done:", Location = new System.Drawing.Point(8, py), AutoSize = true };
                txtQAlreadyDone = new TextBox { Location = new System.Drawing.Point(lblW, py), Size = new System.Drawing.Size(inputW, 23), Font = new System.Drawing.Font("Segoe UI", 8.5f) };
                pnlScroll.Controls.AddRange(new Control[] { l10, txtQAlreadyDone });
                py += 28;

                // Row 5: Battle Monster
                Label l11 = new Label { Text = "Battle Monster:", Location = new System.Drawing.Point(8, py), AutoSize = true };
                numQBattleMonsterId = new NumericUpDown { Location = new System.Drawing.Point(lblW, py), Size = new System.Drawing.Size(90, 22), Maximum = 65535, Font = new System.Drawing.Font("Segoe UI", 9f) };
                Label l12 = new Label { Text = "Monster Name:", Location = new System.Drawing.Point(230, py), AutoSize = true };
                txtQBattleMonsterName = new TextBox { Location = new System.Drawing.Point(330, py), Size = new System.Drawing.Size(295, 22), Font = new System.Drawing.Font("Segoe UI", 9f) };
                pnlScroll.Controls.AddRange(new Control[] { l11, numQBattleMonsterId, l12, txtQBattleMonsterName });
                py += 28;

                // Row 6: Rewards
                Label l13 = new Label { Text = "Gold / EXP:", Location = new System.Drawing.Point(8, py), AutoSize = true };
                numQRewardGold = new NumericUpDown { Location = new System.Drawing.Point(lblW, py), Size = new System.Drawing.Size(85, 22), Maximum = 999999, Font = new System.Drawing.Font("Segoe UI", 9f) };
                numQRewardExp = new NumericUpDown { Location = new System.Drawing.Point(lblW + 92, py), Size = new System.Drawing.Size(85, 22), Maximum = 999999, Font = new System.Drawing.Font("Segoe UI", 9f) };
                
                Label l14 = new Label { Text = "Companion ID/Name:", Location = new System.Drawing.Point(315, py), AutoSize = true };
                numQRewardCompanionId = new NumericUpDown { Location = new System.Drawing.Point(450, py), Size = new System.Drawing.Size(65, 22), Maximum = 65535, Font = new System.Drawing.Font("Segoe UI", 9f) };
                txtQRewardCompanionName = new TextBox { Location = new System.Drawing.Point(520, py), Size = new System.Drawing.Size(105, 22), Font = new System.Drawing.Font("Segoe UI", 9f) };
                pnlScroll.Controls.AddRange(new Control[] { l13, numQRewardGold, numQRewardExp, l14, numQRewardCompanionId, txtQRewardCompanionName });
                py += 28;

                // Row 7: Items & Prerequisites
                Label l17 = new Label { Text = "Required Items:", Location = new System.Drawing.Point(8, py), AutoSize = true };
                txtQRequiredItems = new TextBox { Location = new System.Drawing.Point(lblW, py), Size = new System.Drawing.Size(inputW, 22), Font = new System.Drawing.Font("Segoe UI", 8.5f) };
                pnlScroll.Controls.AddRange(new Control[] { l17, txtQRequiredItems });
                py += 28;

                Label l18 = new Label { Text = "Reward Items:", Location = new System.Drawing.Point(8, py), AutoSize = true };
                txtQRewardItems = new TextBox { Location = new System.Drawing.Point(lblW, py), Size = new System.Drawing.Size(inputW, 22), Font = new System.Drawing.Font("Segoe UI", 8.5f) };
                pnlScroll.Controls.AddRange(new Control[] { l18, txtQRewardItems });
                py += 28;

                Label l16 = new Label { Text = "Prerequisites:", Location = new System.Drawing.Point(8, py), AutoSize = true };
                txtQPrereqs = new TextBox { Location = new System.Drawing.Point(lblW, py), Size = new System.Drawing.Size(inputW, 22), Font = new System.Drawing.Font("Segoe UI", 8.5f) };
                pnlScroll.Controls.AddRange(new Control[] { l16, txtQPrereqs });
                py += 28;

                Label l19 = new Label { Text = "Steps JSON:", Location = new System.Drawing.Point(8, py), AutoSize = true };
                txtQStepsJson = new TextBox { Location = new System.Drawing.Point(lblW, py), Size = new System.Drawing.Size(inputW, 55), Multiline = true, Font = new System.Drawing.Font("Segoe UI", 8.5f), ScrollBars = ScrollBars.Vertical };
                pnlScroll.Controls.AddRange(new Control[] { l19, txtQStepsJson });
                py += 65;

                grpQuestEdit.Controls.Add(pnlScroll);
                grpQuestEdit.Controls.Add(pnlActions);

                split.Panel2.Controls.Add(grpQuestEdit);

                tabQuests.Controls.Add(split);

                if (this.tabControl3 != null)
                {
                    this.tabControl3.TabPages.Add(tabQuests);
                }

                tabQuests.Enter += (s, e) => RefreshQuestGrid(txtQuestSearch?.Text);

                RefreshQuestGrid();
            }
            catch (Exception ex)
            {
                DebugSystem.Write(DebugItemType.Error, $"[MainForm1] Error setting up Quest Manager Tab: {ex.Message}");
            }
        }

        private void RefreshQuestGrid(string filter = "")
        {
            try
            {
                if (dgvQuests == null) return;

                var db = cGlobal.gGameDataBase ?? DataBase.GameDataBase.GlobalInstance;
                if (db == null)
                {
                    db = new DataBase.GameDataBase();
                    cGlobal.gGameDataBase = db;
                }

                var dt = DataBase.QuestDataBase.GetQuestsDataTable(db, filter);
                if (dt == null || dt.Rows.Count == 0)
                {
                    DataBase.QuestDataBase.Initialize(db);
                    dt = DataBase.QuestDataBase.GetQuestsDataTable(db, filter);
                }

                // Fallback: Populate directly from QuestManager.AllQuests in memory if DB table is still reading 0
                if (dt == null || dt.Rows.Count == 0)
                {
                    dt = new System.Data.DataTable();
                    dt.Columns.Add("quest_id", typeof(uint));
                    dt.Columns.Add("name", typeof(string));
                    dt.Columns.Add("type", typeof(int));
                    dt.Columns.Add("npc_name_pattern", typeof(string));
                    dt.Columns.Add("npc_template_id", typeof(int));
                    dt.Columns.Add("reward_gold", typeof(int));
                    dt.Columns.Add("reward_exp", typeof(int));
                    dt.Columns.Add("reward_companion_name", typeof(string));
                    dt.Columns.Add("reward_items", typeof(string));
                    dt.Columns.Add("required_items", typeof(string));
                    dt.Columns.Add("prerequisite_quests", typeof(string));
                    dt.Columns.Add("description", typeof(string));
                    dt.Columns.Add("intro_dialogue", typeof(string));
                    dt.Columns.Add("in_progress_dialogue", typeof(string));
                    dt.Columns.Add("complete_dialogue", typeof(string));
                    dt.Columns.Add("already_completed_dialogue", typeof(string));
                    dt.Columns.Add("battle_monster_id", typeof(int));
                    dt.Columns.Add("battle_monster_name", typeof(string));
                    dt.Columns.Add("reward_companion_id", typeof(int));
                    dt.Columns.Add("steps_json", typeof(string));

                    var quests = Game.QuestRelated.QuestManager.AllQuests.Values;
                    string filterLower = (filter ?? "").Trim().ToLower();
                    foreach (var q in quests)
                    {
                        if (!string.IsNullOrWhiteSpace(filterLower))
                        {
                            if (!q.QuestID.ToString().Contains(filterLower) &&
                                !(q.Title ?? "").ToLower().Contains(filterLower) &&
                                !(q.NpcNamePattern ?? "").ToLower().Contains(filterLower))
                                continue;
                        }
                        string reqItemsStr = q.RequiredItems != null ? string.Join(", ", q.RequiredItems.Select(i => $"{i.ItemID}x{i.Amount}")) : "";
                        string rewItemsStr = q.Reward?.Items != null ? string.Join(", ", q.Reward.Items.Select(i => $"{i.Item1}x{i.Item2}")) : "";
                        string prereqsStr = q.PrerequisiteQuestIDs != null ? string.Join(", ", q.PrerequisiteQuestIDs) : "";

                        dt.Rows.Add(q.QuestID, q.Title ?? $"Quest #{q.QuestID}", (int)q.Type, q.NpcNamePattern ?? "", (int)q.NpcTemplateID,
                            q.Reward?.Gold ?? 0, (int)(q.Reward?.Exp ?? 0), q.Reward?.CompanionName ?? "",
                            rewItemsStr, reqItemsStr, prereqsStr,
                            q.Description ?? "", q.IntroDialogue ?? "", q.InProgressDialogue ?? "",
                            q.CompleteDialogue ?? "", q.AlreadyCompletedDialogue ?? "",
                            (int)q.BattleMonsterID, q.BattleMonsterName ?? "", (int)(q.Reward?.CompanionPetID ?? 0), "");
                    }
                }

                dgvQuests.DataSource = dt;

                // Format visible columns clearly
                if (dgvQuests.Columns.Contains("quest_id")) { dgvQuests.Columns["quest_id"].HeaderText = "ID"; dgvQuests.Columns["quest_id"].Width = 55; }
                if (dgvQuests.Columns.Contains("name")) { dgvQuests.Columns["name"].HeaderText = "Title / Quest Name"; dgvQuests.Columns["name"].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill; }
                if (dgvQuests.Columns.Contains("type")) { dgvQuests.Columns["type"].HeaderText = "Type"; dgvQuests.Columns["type"].Width = 55; }
                if (dgvQuests.Columns.Contains("npc_name_pattern")) { dgvQuests.Columns["npc_name_pattern"].HeaderText = "NPC"; dgvQuests.Columns["npc_name_pattern"].Width = 80; }
                if (dgvQuests.Columns.Contains("reward_gold")) { dgvQuests.Columns["reward_gold"].HeaderText = "Gold"; dgvQuests.Columns["reward_gold"].Width = 55; }
                if (dgvQuests.Columns.Contains("reward_exp")) { dgvQuests.Columns["reward_exp"].HeaderText = "Exp"; dgvQuests.Columns["reward_exp"].Width = 55; }

                // Hide detailed non-grid columns so the grid is clean and readable
                string[] hiddenCols = new string[] {
                    "npc_template_id", "reward_companion_name", "reward_items", "required_items",
                    "prerequisite_quests", "description", "intro_dialogue", "in_progress_dialogue",
                    "complete_dialogue", "already_completed_dialogue", "battle_monster_id",
                    "battle_monster_name", "reward_companion_id", "steps_json"
                };
                foreach (var c in hiddenCols)
                {
                    if (dgvQuests.Columns.Contains(c)) dgvQuests.Columns[c].Visible = false;
                }

                if (lblQuestCount != null && dt != null)
                {
                    lblQuestCount.Text = $"Total Quests: {dt.Rows.Count}";
                }
            }
            catch (Exception ex)
            {
                DebugSystem.Write(DebugItemType.Error, $"[MainForm1] Error refreshing Quest Grid: {ex.Message}");
            }
        }

        private void OnQuestGridSelectionChanged()
        {
            try
            {
                if (dgvQuests == null || dgvQuests.SelectedRows.Count == 0) return;
                var row = dgvQuests.SelectedRows[0];

                txtQId.Text = row.Cells["quest_id"].Value?.ToString() ?? "";
                txtQName.Text = row.Cells["name"].Value?.ToString() ?? "";

                int typeVal = Convert.ToInt32(row.Cells["type"].Value ?? 0);
                if (typeVal >= 0 && typeVal < cmbQType.Items.Count) cmbQType.SelectedIndex = typeVal;

                txtQNpcPattern.Text = row.Cells["npc_name_pattern"].Value?.ToString() ?? "";
                numQNpcTid.Value = Convert.ToDecimal(row.Cells["npc_template_id"].Value ?? 0);
                txtQDesc.Text = row.Cells["description"].Value?.ToString() ?? "";
                txtQIntro.Text = row.Cells["intro_dialogue"].Value?.ToString() ?? "";
                txtQInProgress.Text = row.Cells["in_progress_dialogue"].Value?.ToString() ?? "";
                txtQComplete.Text = row.Cells["complete_dialogue"].Value?.ToString() ?? "";
                txtQAlreadyDone.Text = row.Cells["already_completed_dialogue"].Value?.ToString() ?? "";

                numQBattleMonsterId.Value = Convert.ToDecimal(row.Cells["battle_monster_id"].Value ?? 0);
                txtQBattleMonsterName.Text = row.Cells["battle_monster_name"].Value?.ToString() ?? "";

                numQRewardGold.Value = Convert.ToDecimal(row.Cells["reward_gold"].Value ?? 0);
                numQRewardExp.Value = Convert.ToDecimal(row.Cells["reward_exp"].Value ?? 0);
                numQRewardCompanionId.Value = Convert.ToDecimal(row.Cells["reward_companion_id"].Value ?? 0);
                txtQRewardCompanionName.Text = row.Cells["reward_companion_name"].Value?.ToString() ?? "";

                txtQRewardItems.Text = row.Cells["reward_items"].Value?.ToString() ?? "";
                txtQRequiredItems.Text = row.Cells["required_items"].Value?.ToString() ?? "";
                txtQPrereqs.Text = row.Cells["prerequisite_quests"].Value?.ToString() ?? "";
                txtQStepsJson.Text = row.Cells["steps_json"].Value?.ToString() ?? "";
            }
            catch (Exception ex)
            {
                DebugSystem.Write(DebugItemType.Error, $"[MainForm1] Error in OnQuestGridSelectionChanged: {ex.Message}");
            }
        }

        private void SaveCurrentQuest()
        {
            try
            {
                if (!uint.TryParse(txtQId.Text.Trim(), out uint qId) || qId == 0)
                {
                    MessageBox.Show("Please enter a valid Quest ID (greater than 0).", "Invalid ID", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                string name = txtQName.Text.Trim();
                if (string.IsNullOrEmpty(name))
                {
                    MessageBox.Show("Please enter a Quest Title / Name.", "Invalid Name", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                int type = cmbQType.SelectedIndex;
                string npcPattern = txtQNpcPattern.Text.Trim();
                int npcTid = (int)numQNpcTid.Value;
                string desc = txtQDesc.Text.Trim();
                string intro = txtQIntro.Text.Trim();
                string inProgress = txtQInProgress.Text.Trim();
                string complete = txtQComplete.Text.Trim();
                string alreadyDone = txtQAlreadyDone.Text.Trim();

                int battleMonsterId = (int)numQBattleMonsterId.Value;
                string battleMonsterName = txtQBattleMonsterName.Text.Trim();

                int rewardGold = (int)numQRewardGold.Value;
                int rewardExp = (int)numQRewardExp.Value;
                int rewardCompId = (int)numQRewardCompanionId.Value;
                string rewardCompName = txtQRewardCompanionName.Text.Trim();

                string rewardItems = txtQRewardItems.Text.Trim();
                string requiredItems = txtQRequiredItems.Text.Trim();
                string prereqs = txtQPrereqs.Text.Trim();
                string stepsJson = txtQStepsJson.Text.Trim();

                if (DataBase.QuestDataBase.SaveQuest(DataBase.GameDataBase.GlobalInstance, qId, name, npcPattern, npcTid, type,
                    desc, intro, inProgress, complete, alreadyDone, battleMonsterId, battleMonsterName,
                    rewardGold, rewardExp, rewardCompId, rewardCompName, rewardItems, requiredItems, prereqs, stepsJson))
                {
                    RefreshQuestGrid(txtQuestSearch.Text);
                    MessageBox.Show($"Quest #{qId} ('{name}') saved and updated in database successfully!", "Saved", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    MessageBox.Show("Failed to save quest to database. Check server logs.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving quest: {ex.Message}", "Exception", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void DeleteCurrentQuest()
        {
            try
            {
                if (!uint.TryParse(txtQId.Text.Trim(), out uint qId) || qId == 0)
                {
                    MessageBox.Show("Please select a quest to delete.", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                if (MessageBox.Show($"Are you sure you want to delete Quest #{qId} ('{txtQName.Text}') from the database?", "Confirm Delete", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                {
                    if (DataBase.QuestDataBase.DeleteQuest(DataBase.GameDataBase.GlobalInstance, qId))
                    {
                        RefreshQuestGrid(txtQuestSearch.Text);
                        ClearQuestInputs();
                        MessageBox.Show($"Quest #{qId} deleted successfully from database.", "Deleted", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error deleting quest: {ex.Message}", "Exception", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ClearQuestInputs()
        {
            txtQId.Text = "";
            txtQName.Text = "";
            cmbQType.SelectedIndex = 0;
            txtQNpcPattern.Text = "";
            numQNpcTid.Value = 0;
            txtQDesc.Text = "";
            txtQIntro.Text = "";
            txtQInProgress.Text = "";
            txtQComplete.Text = "";
            txtQAlreadyDone.Text = "";
            numQBattleMonsterId.Value = 0;
            txtQBattleMonsterName.Text = "";
            numQRewardGold.Value = 0;
            numQRewardExp.Value = 0;
            numQRewardCompanionId.Value = 0;
            txtQRewardCompanionName.Text = "";
            txtQRewardItems.Text = "";
            txtQRequiredItems.Text = "";
            txtQPrereqs.Text = "";
            txtQStepsJson.Text = "";
        }
        #endregion

        #region Server Status Manager (Port 6416)
        private ComboBox cmbServerStatus;
        private bool _isUpdatingServerStatusUi = false;

        private void SetupServerStatusControl()
        {
            try
            {
                if (this.tabPage7 == null) return;

                GroupBox grpServerStatus = new GroupBox
                {
                    Text = "🌐 Sunucu Listesi Trafik Işığı / Doluluk Rengi (Port 6416)",
                    Font = new System.Drawing.Font("Segoe UI", 9f, System.Drawing.FontStyle.Bold),
                    ForeColor = System.Drawing.Color.DarkSlateBlue,
                    Location = new System.Drawing.Point(6, 42),
                    Size = new System.Drawing.Size(this.tabPage7.Width - 12, 65),
                    Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
                };

                Label lblStatus = new Label
                {
                    Text = "Sunucu Durumu:",
                    Location = new System.Drawing.Point(10, 26),
                    AutoSize = true,
                    Font = new System.Drawing.Font("Segoe UI", 9f, System.Drawing.FontStyle.Bold),
                    ForeColor = System.Drawing.Color.Black
                };

                cmbServerStatus = new ComboBox
                {
                    Location = new System.Drawing.Point(125, 23),
                    Size = new System.Drawing.Size(220, 24),
                    DropDownStyle = ComboBoxStyle.DropDownList,
                    Font = new System.Drawing.Font("Segoe UI", 9f)
                };
                cmbServerStatus.Items.AddRange(new object[] {
                    "🟢 Yeşil (Boş / Akıcı)",
                    "🟡 Sarı (Kalabalık)",
                    "🔴 Kırmızı (Dolu)",
                    "⚫ Kapalı / Bakım",
                    "⚡ Otomatik (Canlı Oyuncu)"
                });
                cmbServerStatus.SelectedIndexChanged += (s, e) =>
                {
                    if (_isUpdatingServerStatusUi) return;
                    Server.ServerLoadColor color;
                    switch (cmbServerStatus.SelectedIndex)
                    {
                        case 0: color = Server.ServerLoadColor.Green; break;
                        case 1: color = Server.ServerLoadColor.Yellow; break;
                        case 2: color = Server.ServerLoadColor.Red; break;
                        case 3: color = Server.ServerLoadColor.Offline; break;
                        case 4: color = Server.ServerLoadColor.Auto; break;
                        default: color = Server.ServerLoadColor.Green; break;
                    }
                    Server.ServerStatusManager.SetMode(color);
                };

                Button btnSetGreen = new Button
                {
                    Text = "🟢 Yeşil",
                    Location = new System.Drawing.Point(355, 22),
                    Size = new System.Drawing.Size(90, 26),
                    BackColor = System.Drawing.Color.LightGreen,
                    Font = new System.Drawing.Font("Segoe UI", 8.5f, System.Drawing.FontStyle.Bold)
                };
                btnSetGreen.Click += (s, e) => { Server.ServerStatusManager.SetMode(Server.ServerLoadColor.Green); RefreshServerStatusUi(); };

                Button btnSetYellow = new Button
                {
                    Text = "🟡 Sarı",
                    Location = new System.Drawing.Point(450, 22),
                    Size = new System.Drawing.Size(90, 26),
                    BackColor = System.Drawing.Color.Khaki,
                    Font = new System.Drawing.Font("Segoe UI", 8.5f, System.Drawing.FontStyle.Bold)
                };
                btnSetYellow.Click += (s, e) => { Server.ServerStatusManager.SetMode(Server.ServerLoadColor.Yellow); RefreshServerStatusUi(); };

                Button btnSetRed = new Button
                {
                    Text = "🔴 Kırmızı",
                    Location = new System.Drawing.Point(545, 22),
                    Size = new System.Drawing.Size(95, 26),
                    BackColor = System.Drawing.Color.MistyRose,
                    ForeColor = System.Drawing.Color.DarkRed,
                    Font = new System.Drawing.Font("Segoe UI", 8.5f, System.Drawing.FontStyle.Bold)
                };
                btnSetRed.Click += (s, e) => { Server.ServerStatusManager.SetMode(Server.ServerLoadColor.Red); RefreshServerStatusUi(); };

                Button btnSetAuto = new Button
                {
                    Text = "⚡ Otomatik",
                    Location = new System.Drawing.Point(645, 22),
                    Size = new System.Drawing.Size(105, 26),
                    BackColor = System.Drawing.Color.LightCyan,
                    Font = new System.Drawing.Font("Segoe UI", 8.5f, System.Drawing.FontStyle.Bold)
                };
                btnSetAuto.Click += (s, e) => { Server.ServerStatusManager.SetMode(Server.ServerLoadColor.Auto); RefreshServerStatusUi(); };

                grpServerStatus.Controls.AddRange(new Control[] {
                    lblStatus, cmbServerStatus, btnSetGreen, btnSetYellow, btnSetRed, btnSetAuto
                });

                this.tabPage7.Controls.Add(grpServerStatus);

                // Adjust MainOutput position
                this.MainOutput.Location = new System.Drawing.Point(6, 112);
                this.MainOutput.Size = new System.Drawing.Size(this.tabPage7.Width - 12, this.tabPage7.Height - 118);

                RefreshServerStatusUi();
            }
            catch { }
        }

        private void RefreshServerStatusUi()
        {
            try
            {
                _isUpdatingServerStatusUi = true;
                if (cmbServerStatus == null) return;
                switch (Server.ServerStatusManager.CurrentMode)
                {
                    case Server.ServerLoadColor.Green: cmbServerStatus.SelectedIndex = 0; break;
                    case Server.ServerLoadColor.Yellow: cmbServerStatus.SelectedIndex = 1; break;
                    case Server.ServerLoadColor.Red: cmbServerStatus.SelectedIndex = 2; break;
                    case Server.ServerLoadColor.Offline: cmbServerStatus.SelectedIndex = 3; break;
                    case Server.ServerLoadColor.Auto: cmbServerStatus.SelectedIndex = 4; break;
                    default: cmbServerStatus.SelectedIndex = 0; break;
                }
            }
            finally
            {
                _isUpdatingServerStatusUi = false;
            }
        }
        #endregion
    }
}

