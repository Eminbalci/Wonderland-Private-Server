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
            if (this.tabControl3 != null)
            {
                this.tabControl3.Multiline = true;
            }
            SetupGmTab();
            SetupItemMallTab();
            SetupMonsterDropsTab();
            SetupQuestManagerTab();
            SetupEventSystemsTab();
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
                    @"C:\Games\WLRI\aLogin.exe",
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

            Game.SkillRelated.SkillManager.LoadSkillDatabase();
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
                targetPlayer.Send_5_3();
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
                targetPlayer.Send_5_3();
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
        private ComboBox cmbQuestCategoryFilter;

        // Top Summary & Meta controls
        private TextBox txtQId, txtQName, txtQArea;
        private ComboBox cmbQCategory, cmbQType;
        private NumericUpDown numQInProgMark, numQCompMark, numQReqLevel;
        private Button btnSaveQuest, btnNewQuest, btnDeleteQuest, btnReloadQuests;

        // Tab 1: General & Prerequisites
        private TextBox txtQDesc, txtQNpcPattern, txtQPrereqs, txtQRequiredItems;
        private NumericUpDown numQNpcTid, numQStartMapId;

        // Tab 2: Multi-Stage Steps
        private DataGridView dgvQuestSteps;
        private System.Data.DataTable dtQuestSteps;
        private ComboBox cmbStepType;
        private TextBox txtStepNpc, txtStepPrompt, txtStepReqItems, txtStepGrantItems;
        private NumericUpDown numStepNpcTid, numStepMonsterId;
        private Button btnAddStep, btnDeleteStep, btnApplyStepEdit;

        // Tab 3: Dialogues
        private TextBox txtQIntro, txtQInProgress, txtQComplete, txtQAlreadyDone;

        // Tab 4: Rewards & Boss Battle
        private NumericUpDown numQRewardGold, numQRewardExp, numQRewardCompanionId, numQBattleMonsterId;
        private TextBox txtQRewardCompanionName, txtQRewardItems, txtQBattleMonsterName;

        // Tab 5: Live Player Dispatcher
        private ComboBox cmbQuestPlayersLive;
        private Button btnLiveStartQuest, btnLiveAdvanceStep, btnLiveCompleteQuest, btnLiveResetQuest;

        private void SetupQuestManagerTab()
        {
            try
            {
                tabQuests = new TabPage("📜 Quest DB Manager")
                {
                    BackColor = System.Drawing.Color.WhiteSmoke,
                    Padding = new Padding(6)
                };

                if (this.tabControl3 != null && !this.tabControl3.TabPages.Contains(tabQuests))
                {
                    this.tabControl3.TabPages.Add(tabQuests);
                }

                SplitContainer split = new SplitContainer
                {
                    Dock = DockStyle.Fill,
                    Orientation = Orientation.Vertical,
                    SplitterWidth = 6
                };

                tabQuests.SizeChanged += (s, e) =>
                {
                    try
                    {
                        if (split.Width > 500)
                            split.SplitterDistance = Math.Max(220, Math.Min(480, (int)(split.Width * 0.40)));
                    }
                    catch { }
                };

                // === LEFT PANEL: Search, Category Filter, Counter, Quest Grid ===
                Panel pnlLeftTop = new Panel
                {
                    Dock = DockStyle.Top,
                    Height = 96,
                    BackColor = System.Drawing.Color.Transparent
                };

                Label lblHeader = new Label
                {
                    Text = "📜 Quests DB Manager (Structured Master Quests)",
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

                Label lblCategory = new Label
                {
                    Text = "📂 Cat:",
                    Font = new System.Drawing.Font("Segoe UI", 8.5f, System.Drawing.FontStyle.Bold),
                    Location = new System.Drawing.Point(4, 46),
                    AutoSize = true
                };

                cmbQuestCategoryFilter = new ComboBox
                {
                    Location = new System.Drawing.Point(52, 44),
                    Size = new System.Drawing.Size(266, 22),
                    Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                    DropDownStyle = ComboBoxStyle.DropDownList,
                    Font = new System.Drawing.Font("Segoe UI", 8.5f)
                };
                cmbQuestCategoryFilter.Items.AddRange(new object[] {
                    "All Categories (Master Quests)",
                    "🏝️ Storyline & Area",
                    "👥 Companion & Rebirth",
                    "🛠️ Crafting & Vehicles",
                    "🐉 Dungeons & Instances",
                    "🎯 Minigames & Challenges",
                    "📜 All Raw Mark Entries (2,154 IDs)"
                });
                cmbQuestCategoryFilter.SelectedIndex = 0;
                cmbQuestCategoryFilter.SelectedIndexChanged += (s, e) => RefreshQuestGrid(txtQuestSearch?.Text, cmbQuestCategoryFilter.SelectedItem?.ToString());

                Label lblSearch = new Label
                {
                    Text = "🔍 Find:",
                    Font = new System.Drawing.Font("Segoe UI", 8.5f, System.Drawing.FontStyle.Bold),
                    Location = new System.Drawing.Point(4, 72),
                    AutoSize = true
                };

                txtQuestSearch = new TextBox
                {
                    Location = new System.Drawing.Point(52, 70),
                    Size = new System.Drawing.Size(266, 22),
                    Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                    Font = new System.Drawing.Font("Segoe UI", 9f)
                };
                txtQuestSearch.TextChanged += (s, e) => RefreshQuestGrid(txtQuestSearch.Text, cmbQuestCategoryFilter.SelectedItem?.ToString());

                btnReloadQuests = new Button
                {
                    Text = "🔄 Refresh",
                    Location = new System.Drawing.Point(325, 43),
                    Size = new System.Drawing.Size(85, 49),
                    Anchor = AnchorStyles.Top | AnchorStyles.Right,
                    BackColor = System.Drawing.Color.LightSkyBlue,
                    Font = new System.Drawing.Font("Segoe UI", 8.5f, System.Drawing.FontStyle.Bold)
                };
                btnReloadQuests.Click += (s, e) => RefreshQuestGrid(txtQuestSearch.Text, cmbQuestCategoryFilter.SelectedItem?.ToString());

                pnlLeftTop.Controls.AddRange(new Control[] { lblHeader, lblQuestCount, lblCategory, cmbQuestCategoryFilter, lblSearch, txtQuestSearch, btnReloadQuests });

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
                dgvQuests.BringToFront();
                pnlLeftTop.SendToBack();

                // === RIGHT PANEL: Modern Structured Quest Editor ===
                GroupBox grpQuestEdit = new GroupBox
                {
                    Text = "✏️ Quest Editor & Multi-Stage Architecture Studio",
                    Dock = DockStyle.Fill,
                    Font = new System.Drawing.Font("Segoe UI", 9f, System.Drawing.FontStyle.Bold)
                };

                // Top Toolbar for Actions
                Panel pnlActions = new Panel
                {
                    Dock = DockStyle.Top,
                    Height = 38,
                    BackColor = System.Drawing.Color.Transparent
                };

                btnSaveQuest = new Button
                {
                    Text = "💾 Save / Update Quest",
                    Location = new System.Drawing.Point(6, 4),
                    Size = new System.Drawing.Size(160, 28),
                    BackColor = System.Drawing.Color.LightGreen,
                    Font = new System.Drawing.Font("Segoe UI", 8.5f, System.Drawing.FontStyle.Bold)
                };
                btnSaveQuest.Click += (s, e) => SaveCurrentQuest();

                btnNewQuest = new Button
                {
                    Text = "➕ New Quest",
                    Location = new System.Drawing.Point(172, 4),
                    Size = new System.Drawing.Size(100, 28),
                    BackColor = System.Drawing.Color.LightYellow,
                    Font = new System.Drawing.Font("Segoe UI", 8.5f, System.Drawing.FontStyle.Bold)
                };
                btnNewQuest.Click += (s, e) => ClearQuestInputs();

                btnDeleteQuest = new Button
                {
                    Text = "🗑️ Delete Quest",
                    Location = new System.Drawing.Point(278, 4),
                    Size = new System.Drawing.Size(110, 28),
                    BackColor = System.Drawing.Color.MistyRose,
                    ForeColor = System.Drawing.Color.DarkRed,
                    Font = new System.Drawing.Font("Segoe UI", 8.5f, System.Drawing.FontStyle.Bold)
                };
                btnDeleteQuest.Click += (s, e) => DeleteCurrentQuest();

                Button btnReimportQuests = new Button
                {
                    Text = "🔄 Reset & Import Mark.dat",
                    Location = new System.Drawing.Point(394, 4),
                    Size = new System.Drawing.Size(185, 28),
                    BackColor = System.Drawing.Color.LightCyan,
                    Font = new System.Drawing.Font("Segoe UI", 8.5f, System.Drawing.FontStyle.Bold)
                };
                btnReimportQuests.Click += (s, e) =>
                {
                    if (MessageBox.Show("Tüm görevleri sıfırlayıp Data/Mark.dat dosyasındaki 2.154 resmi görevi ve çok aşamalı adımları yüklemek istiyor musunuz?", "Görevleri Yenile", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                    {
                        var db = cGlobal.gGameDataBase ?? DataBase.GameDataBase.GlobalInstance;
                        DataBase.QuestDataBase.ReimportCleanQuests(db);
                        RefreshQuestGrid(txtQuestSearch?.Text, cmbQuestCategoryFilter?.SelectedItem?.ToString());
                        MessageBox.Show("2.154 resmi sistem görevi ve çok aşamalı adımları başarıyla yüklendi ve güncellendi!", "Başarılı", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                };

                pnlActions.Controls.AddRange(new Control[] { btnSaveQuest, btnNewQuest, btnDeleteQuest, btnReimportQuests });

                // Top Header Summary Panel
                Panel pnlSummary = new Panel
                {
                    Dock = DockStyle.Top,
                    Height = 62,
                    BackColor = System.Drawing.Color.FromArgb(245, 247, 250),
                    BorderStyle = BorderStyle.FixedSingle
                };

                Label lm1 = new Label { Text = "ID:", Location = new System.Drawing.Point(6, 6), AutoSize = true, Font = new System.Drawing.Font("Segoe UI", 8.5f, System.Drawing.FontStyle.Bold) };
                txtQId = new TextBox { Location = new System.Drawing.Point(30, 4), Size = new System.Drawing.Size(60, 22), Font = new System.Drawing.Font("Segoe UI", 9f, System.Drawing.FontStyle.Bold) };

                Label lm2 = new Label { Text = "Title:", Location = new System.Drawing.Point(96, 6), AutoSize = true, Font = new System.Drawing.Font("Segoe UI", 8.5f, System.Drawing.FontStyle.Bold) };
                txtQName = new TextBox { Location = new System.Drawing.Point(135, 4), Size = new System.Drawing.Size(260, 22), Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right, Font = new System.Drawing.Font("Segoe UI", 9f, System.Drawing.FontStyle.Bold) };

                Label lm3 = new Label { Text = "Category:", Location = new System.Drawing.Point(402, 6), AutoSize = true, Anchor = AnchorStyles.Top | AnchorStyles.Right, Font = new System.Drawing.Font("Segoe UI", 8.5f, System.Drawing.FontStyle.Bold) };
                cmbQCategory = new ComboBox { Location = new System.Drawing.Point(468, 4), Size = new System.Drawing.Size(165, 22), Anchor = AnchorStyles.Top | AnchorStyles.Right, DropDownStyle = ComboBoxStyle.DropDownList, Font = new System.Drawing.Font("Segoe UI", 8.5f) };
                cmbQCategory.Items.AddRange(new object[] {
                    "🏝️ Storyline & Area",
                    "👥 Companion & Rebirth",
                    "🛠️ Crafting & Vehicles",
                    "🐉 Dungeons & Instances",
                    "🎯 Minigames & Challenges"
                });
                cmbQCategory.SelectedIndex = 0;

                Label lm4 = new Label { Text = "Area:", Location = new System.Drawing.Point(6, 34), AutoSize = true, Font = new System.Drawing.Font("Segoe UI", 8.5f) };
                txtQArea = new TextBox { Location = new System.Drawing.Point(42, 32), Size = new System.Drawing.Size(130, 22), Font = new System.Drawing.Font("Segoe UI", 8.5f) };

                Label lm5 = new Label { Text = "In-Prog Mark:", Location = new System.Drawing.Point(178, 34), AutoSize = true, Font = new System.Drawing.Font("Segoe UI", 8.5f) };
                numQInProgMark = new NumericUpDown { Location = new System.Drawing.Point(260, 32), Size = new System.Drawing.Size(65, 22), Maximum = 65535, Font = new System.Drawing.Font("Segoe UI", 8.5f) };

                Label lm6 = new Label { Text = "Comp Mark:", Location = new System.Drawing.Point(332, 34), AutoSize = true, Font = new System.Drawing.Font("Segoe UI", 8.5f) };
                numQCompMark = new NumericUpDown { Location = new System.Drawing.Point(408, 32), Size = new System.Drawing.Size(65, 22), Maximum = 65535, Font = new System.Drawing.Font("Segoe UI", 8.5f) };

                Label lm7 = new Label { Text = "Req Lvl:", Location = new System.Drawing.Point(480, 34), AutoSize = true, Font = new System.Drawing.Font("Segoe UI", 8.5f) };
                numQReqLevel = new NumericUpDown { Location = new System.Drawing.Point(532, 32), Size = new System.Drawing.Size(55, 22), Maximum = 199, Font = new System.Drawing.Font("Segoe UI", 8.5f) };

                pnlSummary.Controls.AddRange(new Control[] { lm1, txtQId, lm2, txtQName, lm3, cmbQCategory, lm4, txtQArea, lm5, numQInProgMark, lm6, numQCompMark, lm7, numQReqLevel });

                // Multi-Tab Container for Structured Quest Details
                TabControl tabQuestSections = new TabControl
                {
                    Dock = DockStyle.Fill,
                    Font = new System.Drawing.Font("Segoe UI", 8.5f)
                };

                #region Tab 1: Overview & Prerequisites
                TabPage pageGeneral = new TabPage("📋 Overview & Prerequisites");
                Panel pnlGenScroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(8) };

                int gy = 8;
                Label lg1 = new Label { Text = "Storyline & Lore Description:", Location = new System.Drawing.Point(8, gy), AutoSize = true, Font = new System.Drawing.Font("Segoe UI", 8.5f, System.Drawing.FontStyle.Bold) };
                pnlGenScroll.Controls.Add(lg1);
                gy += 20;

                txtQDesc = new TextBox { Location = new System.Drawing.Point(8, gy), Size = new System.Drawing.Size(580, 55), Multiline = true, Font = new System.Drawing.Font("Segoe UI", 9f), ScrollBars = ScrollBars.Vertical, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
                pnlGenScroll.Controls.Add(txtQDesc);
                gy += 62;

                Label lg2 = new Label { Text = "Quest Type:", Location = new System.Drawing.Point(8, gy), AutoSize = true, Font = new System.Drawing.Font("Segoe UI", 8.5f, System.Drawing.FontStyle.Bold) };
                cmbQType = new ComboBox { Location = new System.Drawing.Point(85, gy - 2), Size = new System.Drawing.Size(150, 22), DropDownStyle = ComboBoxStyle.DropDownList, Font = new System.Drawing.Font("Segoe UI", 8.5f) };
                cmbQType.Items.AddRange(new object[] { "0 - Dialogue", "1 - ItemCollection", "2 - MonsterBattle", "3 - CompanionRecruit", "4 - MapTrigger", "5 - Minigame" });
                cmbQType.SelectedIndex = 0;

                Label lg3 = new Label { Text = "Start Map ID:", Location = new System.Drawing.Point(245, gy), AutoSize = true };
                numQStartMapId = new NumericUpDown { Location = new System.Drawing.Point(325, gy - 2), Size = new System.Drawing.Size(70, 22), Maximum = 65535, Font = new System.Drawing.Font("Segoe UI", 8.5f) };

                Label lg4 = new Label { Text = "Start NPC TID:", Location = new System.Drawing.Point(405, gy), AutoSize = true };
                numQNpcTid = new NumericUpDown { Location = new System.Drawing.Point(490, gy - 2), Size = new System.Drawing.Size(65, 22), Maximum = 65535, Font = new System.Drawing.Font("Segoe UI", 8.5f) };

                pnlGenScroll.Controls.AddRange(new Control[] { lg2, cmbQType, lg3, numQStartMapId, lg4, numQNpcTid });
                gy += 30;

                Label lg5 = new Label { Text = "Start NPC Pattern:", Location = new System.Drawing.Point(8, gy), AutoSize = true };
                txtQNpcPattern = new TextBox { Location = new System.Drawing.Point(125, gy - 2), Size = new System.Drawing.Size(200, 22), Font = new System.Drawing.Font("Segoe UI", 8.5f) };
                pnlGenScroll.Controls.AddRange(new Control[] { lg5, txtQNpcPattern });
                gy += 32;

                Label lg6 = new Label { Text = "Prerequisite Quest IDs (e.g. 1864, 52):", Location = new System.Drawing.Point(8, gy), AutoSize = true, Font = new System.Drawing.Font("Segoe UI", 8.5f, System.Drawing.FontStyle.Bold) };
                pnlGenScroll.Controls.Add(lg6);
                gy += 20;

                txtQPrereqs = new TextBox { Location = new System.Drawing.Point(8, gy), Size = new System.Drawing.Size(580, 22), Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right, Font = new System.Drawing.Font("Segoe UI", 8.5f) };
                pnlGenScroll.Controls.Add(txtQPrereqs);
                gy += 28;

                Label lg7 = new Label { Text = "Required Initial Items (e.g. 32005x1, 41066x2):", Location = new System.Drawing.Point(8, gy), AutoSize = true, Font = new System.Drawing.Font("Segoe UI", 8.5f, System.Drawing.FontStyle.Bold) };
                pnlGenScroll.Controls.Add(lg7);
                gy += 20;

                txtQRequiredItems = new TextBox { Location = new System.Drawing.Point(8, gy), Size = new System.Drawing.Size(580, 22), Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right, Font = new System.Drawing.Font("Segoe UI", 8.5f) };
                pnlGenScroll.Controls.Add(txtQRequiredItems);

                pageGeneral.Controls.Add(pnlGenScroll);
                #endregion

                #region Tab 2: Multi-Stage Steps & Objectives
                TabPage pageSteps = new TabPage("👣 Multi-Stage Steps & Objectives");
                
                SplitContainer splitSteps = new SplitContainer
                {
                    Dock = DockStyle.Fill,
                    Orientation = Orientation.Horizontal,
                    SplitterDistance = 140,
                    SplitterWidth = 4
                };

                dtQuestSteps = new System.Data.DataTable();
                dtQuestSteps.Columns.Add("Step", typeof(int));
                dtQuestSteps.Columns.Add("Type", typeof(string));
                dtQuestSteps.Columns.Add("Target NPC", typeof(string));
                dtQuestSteps.Columns.Add("TID", typeof(uint));
                dtQuestSteps.Columns.Add("Prompt / Objective", typeof(string));
                dtQuestSteps.Columns.Add("Required Items", typeof(string));
                dtQuestSteps.Columns.Add("Grant Items", typeof(string));
                dtQuestSteps.Columns.Add("Monster Target", typeof(string));

                dgvQuestSteps = new DataGridView
                {
                    Dock = DockStyle.Fill,
                    DataSource = dtQuestSteps,
                    ReadOnly = true,
                    AllowUserToAddRows = false,
                    AllowUserToDeleteRows = false,
                    SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                    MultiSelect = false,
                    BackgroundColor = System.Drawing.Color.White,
                    RowHeadersVisible = false,
                    Font = new System.Drawing.Font("Segoe UI", 8.5f),
                    AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
                };
                dgvQuestSteps.SelectionChanged += (s, e) => LoadSelectedStepToEditor();

                Panel pnlStepTools = new Panel { Dock = DockStyle.Top, Height = 32, BackColor = System.Drawing.Color.FromArgb(240, 242, 245) };
                btnAddStep = new Button { Text = "➕ Add Step", Location = new System.Drawing.Point(4, 3), Size = new System.Drawing.Size(90, 25), BackColor = System.Drawing.Color.Honeydew, Font = new System.Drawing.Font("Segoe UI", 8.5f, System.Drawing.FontStyle.Bold) };
                btnDeleteStep = new Button { Text = "➖ Delete Step", Location = new System.Drawing.Point(100, 3), Size = new System.Drawing.Size(100, 25), BackColor = System.Drawing.Color.MistyRose, Font = new System.Drawing.Font("Segoe UI", 8.5f, System.Drawing.FontStyle.Bold) };
                btnAddStep.Click += (s, e) => AddNewStepRow();
                btnDeleteStep.Click += (s, e) => DeleteSelectedStepRow();
                pnlStepTools.Controls.AddRange(new Control[] { btnAddStep, btnDeleteStep });

                splitSteps.Panel1.Controls.Add(dgvQuestSteps);
                splitSteps.Panel1.Controls.Add(pnlStepTools);
                dgvQuestSteps.BringToFront();
                pnlStepTools.SendToBack();

                // Step Editor Sub-Panel
                Panel pnlStepEditor = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = System.Drawing.Color.WhiteSmoke, Padding = new Padding(6) };
                
                int sy = 6;
                Label ls1 = new Label { Text = "Step Type:", Location = new System.Drawing.Point(6, sy), AutoSize = true, Font = new System.Drawing.Font("Segoe UI", 8.5f, System.Drawing.FontStyle.Bold) };
                cmbStepType = new ComboBox { Location = new System.Drawing.Point(75, sy - 2), Size = new System.Drawing.Size(130, 22), DropDownStyle = ComboBoxStyle.DropDownList, Font = new System.Drawing.Font("Segoe UI", 8.5f) };
                cmbStepType.Items.AddRange(new object[] { "Dialogue", "ItemCollection", "MonsterBattle", "CompanionRecruit", "MapTrigger" });
                cmbStepType.SelectedIndex = 0;

                Label ls2 = new Label { Text = "Target NPC:", Location = new System.Drawing.Point(215, sy), AutoSize = true };
                txtStepNpc = new TextBox { Location = new System.Drawing.Point(290, sy - 2), Size = new System.Drawing.Size(120, 22), Font = new System.Drawing.Font("Segoe UI", 8.5f) };

                Label ls3 = new Label { Text = "TID:", Location = new System.Drawing.Point(420, sy), AutoSize = true };
                numStepNpcTid = new NumericUpDown { Location = new System.Drawing.Point(450, sy - 2), Size = new System.Drawing.Size(60, 22), Maximum = 65535, Font = new System.Drawing.Font("Segoe UI", 8.5f) };

                btnApplyStepEdit = new Button { Text = "✔️ Update Step", Location = new System.Drawing.Point(520, sy - 3), Size = new System.Drawing.Size(95, 26), BackColor = System.Drawing.Color.LightSkyBlue, Font = new System.Drawing.Font("Segoe UI", 8.5f, System.Drawing.FontStyle.Bold) };
                btnApplyStepEdit.Click += (s, e) => ApplyCurrentStepEdit();

                pnlStepEditor.Controls.AddRange(new Control[] { ls1, cmbStepType, ls2, txtStepNpc, ls3, numStepNpcTid, btnApplyStepEdit });
                sy += 28;

                Label ls4 = new Label { Text = "Step Dialogue / Prompt Instructions:", Location = new System.Drawing.Point(6, sy), AutoSize = true, Font = new System.Drawing.Font("Segoe UI", 8.5f, System.Drawing.FontStyle.Bold) };
                pnlStepEditor.Controls.Add(ls4);
                sy += 18;

                txtStepPrompt = new TextBox { Location = new System.Drawing.Point(6, sy), Size = new System.Drawing.Size(600, 38), Multiline = true, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right, Font = new System.Drawing.Font("Segoe UI", 8.5f), ScrollBars = ScrollBars.Vertical };
                pnlStepEditor.Controls.Add(txtStepPrompt);
                sy += 42;

                Label ls5 = new Label { Text = "Required Items:", Location = new System.Drawing.Point(6, sy), AutoSize = true };
                txtStepReqItems = new TextBox { Location = new System.Drawing.Point(100, sy - 2), Size = new System.Drawing.Size(180, 22), Font = new System.Drawing.Font("Segoe UI", 8.5f) };

                Label ls6 = new Label { Text = "Grant Items:", Location = new System.Drawing.Point(290, sy), AutoSize = true };
                txtStepGrantItems = new TextBox { Location = new System.Drawing.Point(370, sy - 2), Size = new System.Drawing.Size(150, 22), Font = new System.Drawing.Font("Segoe UI", 8.5f) };

                Label ls7 = new Label { Text = "Monster ID:", Location = new System.Drawing.Point(525, sy), AutoSize = true };
                numStepMonsterId = new NumericUpDown { Location = new System.Drawing.Point(595, sy - 2), Size = new System.Drawing.Size(60, 22), Maximum = 65535, Font = new System.Drawing.Font("Segoe UI", 8.5f) };

                pnlStepEditor.Controls.AddRange(new Control[] { ls5, txtStepReqItems, ls6, txtStepGrantItems, ls7, numStepMonsterId });

                splitSteps.Panel2.Controls.Add(pnlStepEditor);
                pageSteps.Controls.Add(splitSteps);
                #endregion

                #region Tab 3: Story Dialogues & Script
                TabPage pageDialogues = new TabPage("💬 Story Dialogues & Script");
                Panel pnlDiaScroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(8) };

                int dy = 8;
                Label ld1 = new Label { Text = "🟢 Accept / Intro Dialogue (Görevi Verirken):", Location = new System.Drawing.Point(8, dy), AutoSize = true, Font = new System.Drawing.Font("Segoe UI", 8.5f, System.Drawing.FontStyle.Bold), ForeColor = System.Drawing.Color.DarkGreen };
                pnlDiaScroll.Controls.Add(ld1);
                dy += 20;

                txtQIntro = new TextBox { Location = new System.Drawing.Point(8, dy), Size = new System.Drawing.Size(580, 42), Multiline = true, Font = new System.Drawing.Font("Segoe UI", 8.5f), ScrollBars = ScrollBars.Vertical, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
                pnlDiaScroll.Controls.Add(txtQIntro);
                dy += 48;

                Label ld2 = new Label { Text = "🟡 In-Progress Dialogue (Görev Sürerken Konuşulduğunda):", Location = new System.Drawing.Point(8, dy), AutoSize = true, Font = new System.Drawing.Font("Segoe UI", 8.5f, System.Drawing.FontStyle.Bold), ForeColor = System.Drawing.Color.DarkGoldenrod };
                pnlDiaScroll.Controls.Add(ld2);
                dy += 20;

                txtQInProgress = new TextBox { Location = new System.Drawing.Point(8, dy), Size = new System.Drawing.Size(580, 36), Multiline = true, Font = new System.Drawing.Font("Segoe UI", 8.5f), ScrollBars = ScrollBars.Vertical, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
                pnlDiaScroll.Controls.Add(txtQInProgress);
                dy += 42;

                Label ld3 = new Label { Text = "🔵 Completion Dialogue (Görev Teslim Edildiğinde):", Location = new System.Drawing.Point(8, dy), AutoSize = true, Font = new System.Drawing.Font("Segoe UI", 8.5f, System.Drawing.FontStyle.Bold), ForeColor = System.Drawing.Color.DarkBlue };
                pnlDiaScroll.Controls.Add(ld3);
                dy += 20;

                txtQComplete = new TextBox { Location = new System.Drawing.Point(8, dy), Size = new System.Drawing.Size(580, 36), Multiline = true, Font = new System.Drawing.Font("Segoe UI", 8.5f), ScrollBars = ScrollBars.Vertical, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
                pnlDiaScroll.Controls.Add(txtQComplete);
                dy += 42;

                Label ld4 = new Label { Text = "⚪ Already Completed Dialogue (Zaten Bitmişken Konuşulduğunda):", Location = new System.Drawing.Point(8, dy), AutoSize = true, Font = new System.Drawing.Font("Segoe UI", 8.5f, System.Drawing.FontStyle.Bold), ForeColor = System.Drawing.Color.DimGray };
                pnlDiaScroll.Controls.Add(ld4);
                dy += 20;

                txtQAlreadyDone = new TextBox { Location = new System.Drawing.Point(8, dy), Size = new System.Drawing.Size(580, 32), Multiline = true, Font = new System.Drawing.Font("Segoe UI", 8.5f), ScrollBars = ScrollBars.Vertical, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
                pnlDiaScroll.Controls.Add(txtQAlreadyDone);

                pageDialogues.Controls.Add(pnlDiaScroll);
                #endregion

                #region Tab 4: Rewards & Boss Battles
                TabPage pageRewards = new TabPage("🎁 Rewards & Boss Battles");
                Panel pnlRewScroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(8) };

                int ry = 8;
                Label lr1 = new Label { Text = "💰 Gold Reward:", Location = new System.Drawing.Point(8, ry), AutoSize = true, Font = new System.Drawing.Font("Segoe UI", 8.5f, System.Drawing.FontStyle.Bold) };
                numQRewardGold = new NumericUpDown { Location = new System.Drawing.Point(120, ry - 2), Size = new System.Drawing.Size(100, 22), Maximum = 9999999, Font = new System.Drawing.Font("Segoe UI", 8.5f) };

                Label lr2 = new Label { Text = "⭐ EXP Reward:", Location = new System.Drawing.Point(240, ry), AutoSize = true, Font = new System.Drawing.Font("Segoe UI", 8.5f, System.Drawing.FontStyle.Bold) };
                numQRewardExp = new NumericUpDown { Location = new System.Drawing.Point(340, ry - 2), Size = new System.Drawing.Size(100, 22), Maximum = 9999999, Font = new System.Drawing.Font("Segoe UI", 8.5f) };

                pnlRewScroll.Controls.AddRange(new Control[] { lr1, numQRewardGold, lr2, numQRewardExp });
                ry += 32;

                Label lr3 = new Label { Text = "👥 Companion Recruit:", Location = new System.Drawing.Point(8, ry), AutoSize = true, Font = new System.Drawing.Font("Segoe UI", 8.5f, System.Drawing.FontStyle.Bold) };
                numQRewardCompanionId = new NumericUpDown { Location = new System.Drawing.Point(150, ry - 2), Size = new System.Drawing.Size(70, 22), Maximum = 65535, Font = new System.Drawing.Font("Segoe UI", 8.5f) };
                txtQRewardCompanionName = new TextBox { Location = new System.Drawing.Point(230, ry - 2), Size = new System.Drawing.Size(150, 22), Font = new System.Drawing.Font("Segoe UI", 8.5f) };

                pnlRewScroll.Controls.AddRange(new Control[] { lr3, numQRewardCompanionId, txtQRewardCompanionName });
                ry += 32;

                Label lr4 = new Label { Text = "⚔️ Quest Boss / Battle:", Location = new System.Drawing.Point(8, ry), AutoSize = true, Font = new System.Drawing.Font("Segoe UI", 8.5f, System.Drawing.FontStyle.Bold) };
                numQBattleMonsterId = new NumericUpDown { Location = new System.Drawing.Point(150, ry - 2), Size = new System.Drawing.Size(70, 22), Maximum = 65535, Font = new System.Drawing.Font("Segoe UI", 8.5f) };
                txtQBattleMonsterName = new TextBox { Location = new System.Drawing.Point(230, ry - 2), Size = new System.Drawing.Size(150, 22), Font = new System.Drawing.Font("Segoe UI", 8.5f) };

                pnlRewScroll.Controls.AddRange(new Control[] { lr4, numQBattleMonsterId, txtQBattleMonsterName });
                ry += 32;

                Label lr5 = new Label { Text = "🎒 Item Rewards (Format: ItemIDxCount, e.g. 32001x2, 48016x1):", Location = new System.Drawing.Point(8, ry), AutoSize = true, Font = new System.Drawing.Font("Segoe UI", 8.5f, System.Drawing.FontStyle.Bold) };
                pnlRewScroll.Controls.Add(lr5);
                ry += 20;

                txtQRewardItems = new TextBox { Location = new System.Drawing.Point(8, ry), Size = new System.Drawing.Size(580, 24), Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right, Font = new System.Drawing.Font("Segoe UI", 9f) };
                pnlRewScroll.Controls.Add(txtQRewardItems);

                pageRewards.Controls.Add(pnlRewScroll);
                #endregion

                #region Tab 5: Live Player Quest Dispatcher
                TabPage pageLive = new TabPage("🧪 Live Player Dispatcher");
                Panel pnlLive = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12), BackColor = System.Drawing.Color.WhiteSmoke };

                Label ll1 = new Label { Text = "Select Online Player:", Location = new System.Drawing.Point(12, 16), AutoSize = true, Font = new System.Drawing.Font("Segoe UI", 9f, System.Drawing.FontStyle.Bold) };
                cmbQuestPlayersLive = new ComboBox { Location = new System.Drawing.Point(160, 14), Size = new System.Drawing.Size(220, 24), DropDownStyle = ComboBoxStyle.DropDownList, Font = new System.Drawing.Font("Segoe UI", 9f) };

                btnLiveStartQuest = new Button
                {
                    Text = "▶️ Start Quest (AC 24:1)",
                    Location = new System.Drawing.Point(12, 55),
                    Size = new System.Drawing.Size(190, 34),
                    BackColor = System.Drawing.Color.PaleGreen,
                    Font = new System.Drawing.Font("Segoe UI", 8.5f, System.Drawing.FontStyle.Bold)
                };
                btnLiveStartQuest.Click += (s, e) => DispatchLiveQuestAction(1);

                btnLiveAdvanceStep = new Button
                {
                    Text = "⏭️ Advance Step",
                    Location = new System.Drawing.Point(210, 55),
                    Size = new System.Drawing.Size(150, 34),
                    BackColor = System.Drawing.Color.LightSkyBlue,
                    Font = new System.Drawing.Font("Segoe UI", 8.5f, System.Drawing.FontStyle.Bold)
                };
                btnLiveAdvanceStep.Click += (s, e) => DispatchLiveQuestAction(2);

                btnLiveCompleteQuest = new Button
                {
                    Text = "🏆 Complete Quest (AC 24:5)",
                    Location = new System.Drawing.Point(370, 55),
                    Size = new System.Drawing.Size(200, 34),
                    BackColor = System.Drawing.Color.Gold,
                    Font = new System.Drawing.Font("Segoe UI", 8.5f, System.Drawing.FontStyle.Bold)
                };
                btnLiveCompleteQuest.Click += (s, e) => DispatchLiveQuestAction(5);

                btnLiveResetQuest = new Button
                {
                    Text = "🔄 Reset Quest Flags",
                    Location = new System.Drawing.Point(12, 100),
                    Size = new System.Drawing.Size(190, 32),
                    BackColor = System.Drawing.Color.MistyRose,
                    ForeColor = System.Drawing.Color.DarkRed,
                    Font = new System.Drawing.Font("Segoe UI", 8.5f, System.Drawing.FontStyle.Bold)
                };
                btnLiveResetQuest.Click += (s, e) => DispatchLiveQuestAction(3);

                Label ll2 = new Label
                {
                    Text = "ℹ️ Live Quest Actions allow testing quest progression in real time on active connected game clients.\n- Start Quest: Sets In-Progress Mark, shows quest in F6 Quest Log, activates map PreEvents.\n- Complete Quest: Sets Completed Mark, grants Gold, EXP, Items, and Companion Pet, and updates Quest Log.",
                    Location = new System.Drawing.Point(12, 145),
                    Size = new System.Drawing.Size(560, 60),
                    ForeColor = System.Drawing.Color.DimGray,
                    Font = new System.Drawing.Font("Segoe UI", 8.5f)
                };

                pnlLive.Controls.AddRange(new Control[] { ll1, cmbQuestPlayersLive, btnLiveStartQuest, btnLiveAdvanceStep, btnLiveCompleteQuest, btnLiveResetQuest, ll2 });
                pageLive.Controls.Add(pnlLive);
                #endregion

                tabQuestSections.TabPages.AddRange(new TabPage[] {
                    pageGeneral, pageSteps, pageDialogues, pageRewards, pageLive
                });

                grpQuestEdit.Controls.Add(tabQuestSections);
                grpQuestEdit.Controls.Add(pnlSummary);
                grpQuestEdit.Controls.Add(pnlActions);
                tabQuestSections.BringToFront();
                pnlSummary.SendToBack();
                pnlActions.SendToBack();

                split.Panel2.Controls.Add(grpQuestEdit);
                tabQuests.Controls.Add(split);

                tabQuests.Enter += (s, e) =>
                {
                    RefreshQuestGrid(txtQuestSearch?.Text, cmbQuestCategoryFilter?.SelectedItem?.ToString());
                    RefreshLiveQuestPlayers();
                };

                RefreshQuestGrid();
            }
            catch (Exception ex)
            {
                DebugSystem.Write(DebugItemType.Error, $"[MainForm1] Error setting up Quest Manager Tab: {ex.Message}");
            }
        }

        private void RefreshLiveQuestPlayers()
        {
            try
            {
                if (cmbQuestPlayersLive == null) return;
                cmbQuestPlayersLive.Items.Clear();
                var players = cGlobal.gCharacterDataBase?.GetOnlinePlayers();
                if (players != null)
                {
                    foreach (var p in players)
                    {
                        if (p != null && !string.IsNullOrEmpty(p.CharName))
                        {
                            cmbQuestPlayersLive.Items.Add(p.CharName);
                        }
                    }
                }
                if (cmbQuestPlayersLive.Items.Count > 0) cmbQuestPlayersLive.SelectedIndex = 0;
            }
            catch { }
        }

        private void DispatchLiveQuestAction(byte actionType)
        {
            try
            {
                if (!uint.TryParse(txtQId.Text.Trim(), out uint qId) || qId == 0)
                {
                    MessageBox.Show("Please select a valid Quest first.", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                string pName = cmbQuestPlayersLive?.SelectedItem?.ToString();
                if (string.IsNullOrEmpty(pName))
                {
                    MessageBox.Show("No active online player selected.", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                var player = cGlobal.gCharacterDataBase?.GetOnlinePlayers()?.FirstOrDefault(p => p != null && p.CharName == pName);
                if (player == null)
                {
                    MessageBox.Show($"Player '{pName}' is no longer online.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                uint inProgMark = (uint)numQInProgMark.Value;
                uint compMark = (uint)numQCompMark.Value;
                if (inProgMark == 0) inProgMark = qId;
                if (compMark == 0) compMark = qId + 1;

                switch (actionType)
                {
                    case 1: // Start Quest
                        Game.QuestRelated.QuestManager.AcceptQuest(player, qId);
                        MessageBox.Show($"Quest #{qId} started for {pName} (In-Progress Mark #{inProgMark})!", "Quest Started", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        break;
                    case 2: // Advance Step
                        Game.QuestRelated.QuestManager.AdvanceQuestStep(player, qId);
                        MessageBox.Show($"Quest #{qId} advanced to next step for {pName}!", "Step Advanced", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        break;
                    case 5: // Complete Quest
                        Game.QuestRelated.QuestManager.CompleteQuest(player, qId);
                        MessageBox.Show($"Quest #{qId} completed for {pName} (Completed Mark #{compMark}) & rewards awarded!", "Quest Completed", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        break;
                    case 3: // Reset Quest
                        Game.QuestRelated.QuestManager.ResetQuest(player, qId);
                        MessageBox.Show($"Quest #{qId} reset for {pName}.", "Quest Reset", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        break;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error executing live quest action: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void AddNewStepRow()
        {
            if (dtQuestSteps == null) return;
            int nextStep = dtQuestSteps.Rows.Count + 1;
            dtQuestSteps.Rows.Add(nextStep, "Dialogue", "NPC Target", 0, "Talk to NPC to proceed with this objective.", "", "", "");
        }

        private void DeleteSelectedStepRow()
        {
            if (dgvQuestSteps == null || dgvQuestSteps.SelectedRows.Count == 0 || dtQuestSteps == null) return;
            int idx = dgvQuestSteps.SelectedRows[0].Index;
            if (idx >= 0 && idx < dtQuestSteps.Rows.Count)
            {
                dtQuestSteps.Rows.RemoveAt(idx);
                // Re-index remaining steps
                for (int i = 0; i < dtQuestSteps.Rows.Count; i++)
                {
                    dtQuestSteps.Rows[i]["Step"] = i + 1;
                }
            }
        }

        private void LoadSelectedStepToEditor()
        {
            try
            {
                if (dgvQuestSteps == null || dgvQuestSteps.SelectedRows.Count == 0) return;
                var row = dgvQuestSteps.SelectedRows[0];

                string type = row.Cells["Type"].Value?.ToString() ?? "Dialogue";
                int tIdx = cmbStepType.Items.IndexOf(type);
                if (tIdx >= 0) cmbStepType.SelectedIndex = tIdx;

                txtStepNpc.Text = row.Cells["Target NPC"].Value?.ToString() ?? "";
                numStepNpcTid.Value = Convert.ToDecimal(row.Cells["TID"].Value ?? 0);
                txtStepPrompt.Text = row.Cells["Prompt / Objective"].Value?.ToString() ?? "";
                txtStepReqItems.Text = row.Cells["Required Items"].Value?.ToString() ?? "";
                txtStepGrantItems.Text = row.Cells["Grant Items"].Value?.ToString() ?? "";
            }
            catch { }
        }

        private void ApplyCurrentStepEdit()
        {
            try
            {
                if (dgvQuestSteps == null || dgvQuestSteps.SelectedRows.Count == 0) return;
                var row = dgvQuestSteps.SelectedRows[0];

                row.Cells["Type"].Value = cmbStepType.SelectedItem?.ToString() ?? "Dialogue";
                row.Cells["Target NPC"].Value = txtStepNpc.Text.Trim();
                row.Cells["TID"].Value = (uint)numStepNpcTid.Value;
                row.Cells["Prompt / Objective"].Value = txtStepPrompt.Text.Trim();
                row.Cells["Required Items"].Value = txtStepReqItems.Text.Trim();
                row.Cells["Grant Items"].Value = txtStepGrantItems.Text.Trim();
            }
            catch { }
        }

        private string SerializeStepsToJson()
        {
            if (dtQuestSteps == null || dtQuestSteps.Rows.Count == 0) return "[]";
            var list = new List<string>();
            foreach (System.Data.DataRow row in dtQuestSteps.Rows)
            {
                int sIdx = Convert.ToInt32(row["Step"]);
                string type = row["Type"]?.ToString() ?? "Dialogue";
                string npc = (row["Target NPC"]?.ToString() ?? "").Replace("\"", "\\\"");
                uint tid = Convert.ToUInt32(row["TID"]);
                string prompt = (row["Prompt / Objective"]?.ToString() ?? "").Replace("\"", "\\\"").Replace("\r\n", "\\n").Replace("\n", "\\n");
                string req = (row["Required Items"]?.ToString() ?? "").Replace("\"", "\\\"");
                string grant = (row["Grant Items"]?.ToString() ?? "").Replace("\"", "\\\"");

                list.Add($"{{\"StepIndex\":{sIdx},\"StepType\":\"{type}\",\"TargetNpcPattern\":\"{npc}\",\"TargetNpcTemplateID\":{tid},\"PromptDialogue\":\"{prompt}\",\"RequiredItems\":\"{req}\",\"GrantItems\":\"{grant}\"}}");
            }
            return "[" + string.Join(",", list) + "]";
        }

        private void PopulateStepsFromData(string stepsJson, string intro, string inProgress, string complete, string npcPattern, uint npcTid)
        {
            if (dtQuestSteps == null) return;
            dtQuestSteps.Rows.Clear();

            bool loaded = false;
            if (!string.IsNullOrWhiteSpace(stepsJson) && stepsJson.Trim() != "[]")
            {
                try
                {
                    var matches = System.Text.RegularExpressions.Regex.Matches(stepsJson, @"\{[\s\S]*?\}");
                    int idx = 1;
                    foreach (System.Text.RegularExpressions.Match m in matches)
                    {
                        string block = m.Value;
                        string type = System.Text.RegularExpressions.Regex.Match(block, @"\""StepType\""\s*:\s*\""([^\""]+)\""").Groups[1].Value;
                        string npc = System.Text.RegularExpressions.Regex.Match(block, @"\""TargetNpcPattern\""\s*:\s*\""([^\""]*)\""").Groups[1].Value;
                        string prompt = System.Text.RegularExpressions.Regex.Match(block, @"\""PromptDialogue\""\s*:\s*\""([^\""]*)\""").Groups[1].Value;
                        string req = System.Text.RegularExpressions.Regex.Match(block, @"\""RequiredItems\""\s*:\s*\""([^\""]*)\""").Groups[1].Value;
                        string grant = System.Text.RegularExpressions.Regex.Match(block, @"\""GrantItems\""\s*:\s*\""([^\""]*)\""").Groups[1].Value;

                        dtQuestSteps.Rows.Add(idx++, string.IsNullOrEmpty(type) ? "Dialogue" : type, npc, npcTid, prompt, req, grant, "");
                    }
                    if (dtQuestSteps.Rows.Count > 0) loaded = true;
                }
                catch { }
            }

            if (!loaded)
            {
                // Generate natural authentic step stages based on quest dialogues
                dtQuestSteps.Rows.Add(1, "Dialogue", npcPattern, npcTid, string.IsNullOrEmpty(intro) ? "Talk to Quest NPC to start objective." : intro, "", "", "");
                if (!string.IsNullOrEmpty(inProgress))
                {
                    dtQuestSteps.Rows.Add(2, "ItemCollection", npcPattern, npcTid, inProgress, "", "", "");
                }
                dtQuestSteps.Rows.Add(dtQuestSteps.Rows.Count + 1, "Dialogue", npcPattern, npcTid, string.IsNullOrEmpty(complete) ? "Deliver quest and claim rewards." : complete, "", "", "");
            }

            if (dgvQuestSteps.Rows.Count > 0)
            {
                dgvQuestSteps.Rows[0].Selected = true;
                LoadSelectedStepToEditor();
            }
        }
        private void RefreshQuestGrid(string filter = "", string categoryFilter = "")
        {
            try
            {
                if (dgvQuests == null) return;

                bool showRaw = categoryFilter != null && categoryFilter.Contains("Raw Mark Entries");
                var sourceQuests = showRaw 
                    ? Game.QuestRelated.QuestManager.AllQuests.Values 
                    : Game.QuestRelated.QuestManager.MasterQuests.Values;

                // Fallback: If MasterQuests is empty, reload authentic Mark.dat
                if (!sourceQuests.Any())
                {
                    string markDatPath = System.IO.Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "Data", "Mark.dat");
                    Game.QuestRelated.QuestManager.LoadAuthenticQuestsFromMarkDat(markDatPath);
                    sourceQuests = showRaw 
                        ? Game.QuestRelated.QuestManager.AllQuests.Values 
                        : Game.QuestRelated.QuestManager.MasterQuests.Values;
                }

                System.Data.DataTable dt = new System.Data.DataTable();
                dt.Columns.Add("quest_id", typeof(uint));
                dt.Columns.Add("name", typeof(string));
                dt.Columns.Add("category", typeof(string));
                dt.Columns.Add("area", typeof(string));
                dt.Columns.Add("map_id", typeof(int));
                dt.Columns.Add("in_prog_mark", typeof(uint));
                dt.Columns.Add("comp_mark", typeof(uint));
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

                string filterLower = (filter ?? "").Trim().ToLower();
                string catClean = (categoryFilter ?? "").Replace("🏝️", "").Replace("👥", "").Replace("🛠️", "").Replace("🐉", "").Replace("🎯", "").Replace("📜", "").Trim().ToLower();
                if (catClean.Contains("all categories") || string.IsNullOrEmpty(categoryFilter)) catClean = "";

                foreach (var q in sourceQuests)
                {
                    if (!string.IsNullOrEmpty(catClean) && !showRaw)
                    {
                        string qCat = (q.Category ?? "").ToLower();
                        if (!qCat.Contains(catClean)) continue;
                    }

                    if (!string.IsNullOrWhiteSpace(filterLower))
                    {
                        if (!q.QuestID.ToString().Contains(filterLower) &&
                            !(q.Title ?? "").ToLower().Contains(filterLower) &&
                            !(q.Category ?? "").ToLower().Contains(filterLower) &&
                            !(q.AreaName ?? "").ToLower().Contains(filterLower) &&
                            !(q.NpcNamePattern ?? "").ToLower().Contains(filterLower))
                            continue;
                    }

                    string reqItemsStr = q.RequiredItems != null ? string.Join(", ", q.RequiredItems.Select(i => $"{i.ItemID}x{i.Amount}")) : "";
                    string rewItemsStr = q.Reward?.Items != null ? string.Join(", ", q.Reward.Items.Select(i => $"{i.Item1}x{i.Item2}")) : "";
                    string prereqsStr = q.PrerequisiteQuestIDs != null ? string.Join(", ", q.PrerequisiteQuestIDs) : "";

                    string stepsJson = "";
                    if (q.Steps != null && q.Steps.Count > 0)
                    {
                        var sList = new List<string>();
                        foreach (var st in q.Steps)
                        {
                            string pDia = (st.PromptDialogue ?? "").Replace("\"", "\\\"").Replace("\r\n", "\\n").Replace("\n", "\\n");
                            string inDia = (st.InProgressDialogue ?? "").Replace("\"", "\\\"").Replace("\r\n", "\\n").Replace("\n", "\\n");
                            string compDia = (st.CompleteDialogue ?? "").Replace("\"", "\\\"").Replace("\r\n", "\\n").Replace("\n", "\\n");
                            sList.Add($"{{\"StepIndex\":{st.StepIndex},\"StepType\":\"{st.StepType}\",\"TargetNpcPattern\":\"{st.TargetNpcPattern}\",\"TargetNpcTemplateID\":{st.TargetNpcTemplateID},\"PromptDialogue\":\"{pDia}\",\"InProgressDialogue\":\"{inDia}\",\"CompleteDialogue\":\"{compDia}\"}}");
                        }
                        stepsJson = "[" + string.Join(",", sList) + "]";
                    }

                    dt.Rows.Add(q.QuestID, q.Title ?? $"Quest #{q.QuestID}", q.Category ?? "Storyline", q.AreaName ?? "Unknown", (int)q.MapID,
                        q.InProgressMarkID, q.CompletedMarkID, (int)q.Type, q.NpcNamePattern ?? "", (int)q.NpcTemplateID,
                        q.Reward?.Gold ?? 0, (int)(q.Reward?.Exp ?? 0), q.Reward?.CompanionName ?? "",
                        rewItemsStr, reqItemsStr, prereqsStr,
                        q.Description ?? "", q.IntroDialogue ?? "", q.InProgressDialogue ?? "",
                        q.CompleteDialogue ?? "", q.AlreadyCompletedDialogue ?? "",
                        (int)q.BattleMonsterID, q.BattleMonsterName ?? "", (int)(q.Reward?.CompanionPetID ?? 0), stepsJson);
                }

                dgvQuests.DataSource = dt;

                // Format visible columns clearly with manual resizing and flexible minimum widths
                dgvQuests.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
                dgvQuests.AllowUserToResizeColumns = true;

                if (dgvQuests.Columns.Contains("quest_id")) { 
                    dgvQuests.Columns["quest_id"].HeaderText = "ID"; 
                    dgvQuests.Columns["quest_id"].Width = 48; 
                    dgvQuests.Columns["quest_id"].MinimumWidth = 40;
                    dgvQuests.Columns["quest_id"].AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
                }
                if (dgvQuests.Columns.Contains("name")) { 
                    dgvQuests.Columns["name"].HeaderText = "Title / Quest Name"; 
                    dgvQuests.Columns["name"].Width = 240; 
                    dgvQuests.Columns["name"].MinimumWidth = 60;
                    dgvQuests.Columns["name"].AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
                }
                if (dgvQuests.Columns.Contains("category")) { 
                    dgvQuests.Columns["category"].HeaderText = "Category"; 
                    dgvQuests.Columns["category"].Width = 145; 
                    dgvQuests.Columns["category"].MinimumWidth = 80;
                    dgvQuests.Columns["category"].AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
                }
                if (dgvQuests.Columns.Contains("in_prog_mark")) { 
                    dgvQuests.Columns["in_prog_mark"].HeaderText = "In-Prog"; 
                    dgvQuests.Columns["in_prog_mark"].Width = 55; 
                    dgvQuests.Columns["in_prog_mark"].MinimumWidth = 45;
                    dgvQuests.Columns["in_prog_mark"].AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
                }
                if (dgvQuests.Columns.Contains("comp_mark")) { 
                    dgvQuests.Columns["comp_mark"].HeaderText = "Comp"; 
                    dgvQuests.Columns["comp_mark"].Width = 55; 
                    dgvQuests.Columns["comp_mark"].MinimumWidth = 45;
                    dgvQuests.Columns["comp_mark"].AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
                }

                // Hide detailed non-grid columns so the grid is clean and readable
                string[] hiddenCols = new string[] {
                    "area", "map_id", "type", "npc_name_pattern", "reward_gold", "reward_exp",
                    "npc_template_id", "reward_companion_name", "reward_items", "required_items",
                    "prerequisite_quests", "description", "intro_dialogue", "in_progress_dialogue",
                    "complete_dialogue", "already_completed_dialogue", "battle_monster_id",
                    "battle_monster_name", "reward_companion_id", "steps_json"
                };
                foreach (var c in hiddenCols)
                {
                    if (dgvQuests.Columns.Contains(c)) dgvQuests.Columns[c].Visible = false;
                }

                if (lblQuestCount != null)
                {
                    lblQuestCount.Text = showRaw 
                        ? $"Showing {dt.Rows.Count} / {Game.QuestRelated.QuestManager.Count} Raw Mark IDs"
                        : $"Showing {dt.Rows.Count} / {Game.QuestRelated.QuestManager.MasterCount} Master Quests";
                }

                if (dgvQuests.Rows.Count > 0)
                {
                    dgvQuests.Rows[0].Selected = true;
                    OnQuestGridSelectionChanged();
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

                string cat = row.Cells["category"].Value?.ToString() ?? "";
                for (int i = 0; i < cmbQCategory.Items.Count; i++)
                {
                    if (cmbQCategory.Items[i].ToString().Contains(cat) || cat.Contains(cmbQCategory.Items[i].ToString()))
                    {
                        cmbQCategory.SelectedIndex = i;
                        break;
                    }
                }

                txtQArea.Text = row.Cells["area"].Value?.ToString() ?? "";
                numQStartMapId.Value = Convert.ToDecimal(row.Cells["map_id"].Value ?? 0);
                numQInProgMark.Value = Convert.ToDecimal(row.Cells["in_prog_mark"].Value ?? 0);
                numQCompMark.Value = Convert.ToDecimal(row.Cells["comp_mark"].Value ?? 0);

                int typeVal = Convert.ToInt32(row.Cells["type"].Value ?? 0);
                if (typeVal >= 0 && typeVal < cmbQType.Items.Count) cmbQType.SelectedIndex = typeVal;

                txtQNpcPattern.Text = row.Cells["npc_name_pattern"].Value?.ToString() ?? "";
                uint npcTid = Convert.ToUInt32(row.Cells["npc_template_id"].Value ?? 0);
                numQNpcTid.Value = npcTid;
                txtQDesc.Text = row.Cells["description"].Value?.ToString() ?? "";

                string intro = row.Cells["intro_dialogue"].Value?.ToString() ?? "";
                string inProgress = row.Cells["in_progress_dialogue"].Value?.ToString() ?? "";
                string complete = row.Cells["complete_dialogue"].Value?.ToString() ?? "";
                string alreadyDone = row.Cells["already_completed_dialogue"].Value?.ToString() ?? "";

                txtQIntro.Text = intro;
                txtQInProgress.Text = inProgress;
                txtQComplete.Text = complete;
                txtQAlreadyDone.Text = alreadyDone;

                numQBattleMonsterId.Value = Convert.ToDecimal(row.Cells["battle_monster_id"].Value ?? 0);
                txtQBattleMonsterName.Text = row.Cells["battle_monster_name"].Value?.ToString() ?? "";

                numQRewardGold.Value = Convert.ToDecimal(row.Cells["reward_gold"].Value ?? 0);
                numQRewardExp.Value = Convert.ToDecimal(row.Cells["reward_exp"].Value ?? 0);
                numQRewardCompanionId.Value = Convert.ToDecimal(row.Cells["reward_companion_id"].Value ?? 0);
                txtQRewardCompanionName.Text = row.Cells["reward_companion_name"].Value?.ToString() ?? "";

                txtQRewardItems.Text = row.Cells["reward_items"].Value?.ToString() ?? "";
                txtQRequiredItems.Text = row.Cells["required_items"].Value?.ToString() ?? "";
                txtQPrereqs.Text = row.Cells["prerequisite_quests"].Value?.ToString() ?? "";

                string stepsJson = row.Cells["steps_json"].Value?.ToString() ?? "";
                PopulateStepsFromData(stepsJson, intro, inProgress, complete, txtQNpcPattern.Text, npcTid);
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
                string stepsJson = SerializeStepsToJson();
                int mapId = (int)numQStartMapId.Value;

                if (DataBase.QuestDataBase.SaveQuest(DataBase.GameDataBase.GlobalInstance, qId, name, npcPattern, npcTid, mapId, type,
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
            txtQArea.Text = "";
            numQInProgMark.Value = 0;
            numQCompMark.Value = 0;
            numQReqLevel.Value = 0;
            cmbQType.SelectedIndex = 0;
            txtQNpcPattern.Text = "";
            numQNpcTid.Value = 0;
            numQStartMapId.Value = 0;
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
            dtQuestSteps?.Rows.Clear();
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

        #region 7 Event Systems & Map Interactive Entities GUI Tab
        private TabPage tabEventSystems;
        private ComboBox cmbEventMapsList;
        private TextBox txtEventMapSearch;
        private Label lblEventMapStats;
        private Label lblEventMapTotals;
        private TabControl tabControlMapEntities;
        private DataGridView dgvMapTraps;
        private DataGridView dgvMapPreEvents;
        private DataGridView dgvMapMining;
        private DataGridView dgvMapWarps;
        private DataGridView dgvMapChests;
        private DataGridView dgvMapNpcs;
        private ComboBox cmbEventTesterPlayer;
        private NumericUpDown numTestEventId;
        private ushort _selectedEventMapId = 10036;

        private void SetupEventSystemsTab()
        {
            try
            {
                tabEventSystems = new TabPage("🗺️ 7 Event Systems & Triggers")
                {
                    BackColor = System.Drawing.Color.WhiteSmoke,
                    Padding = new Padding(6)
                };

                // === TOP HEADER & MAP SELECTION PANEL ===
                Panel pnlTop = new Panel
                {
                    Dock = DockStyle.Top,
                    Height = 85,
                    BackColor = System.Drawing.Color.Transparent
                };

                Label lblHeader = new Label
                {
                    Text = "🗺️ 7 Event Systems & Map Interactive Entities (Eve.Emg Engine)",
                    Font = new System.Drawing.Font("Segoe UI", 10.5f, System.Drawing.FontStyle.Bold),
                    ForeColor = System.Drawing.Color.DarkSlateBlue,
                    Location = new System.Drawing.Point(4, 4),
                    AutoSize = true
                };

                lblEventMapTotals = new Label
                {
                    Text = "🌐 Dataset: 1,119 Maps | 22,171 NPCs | 89,724 Doors | 34,363 Floor Traps | 47,569 Mining Nodes | 47,370 Chests | 41,340 PreEvents",
                    Font = new System.Drawing.Font("Segoe UI", 8.5f, System.Drawing.FontStyle.Bold),
                    ForeColor = System.Drawing.Color.DimGray,
                    Location = new System.Drawing.Point(4, 26),
                    AutoSize = true
                };

                Label lblSelectMap = new Label
                {
                    Text = "📍 Select Map:",
                    Font = new System.Drawing.Font("Segoe UI", 8.5f, System.Drawing.FontStyle.Bold),
                    Location = new System.Drawing.Point(4, 52),
                    AutoSize = true
                };

                txtEventMapSearch = new TextBox
                {
                    Location = new System.Drawing.Point(90, 50),
                    Size = new System.Drawing.Size(120, 23),
                    Font = new System.Drawing.Font("Segoe UI", 9f)
                };
                txtEventMapSearch.TextChanged += (s, e) => FilterEventMapsDropdown(txtEventMapSearch.Text);

                cmbEventMapsList = new ComboBox
                {
                    Location = new System.Drawing.Point(216, 49),
                    Size = new System.Drawing.Size(280, 24),
                    DropDownStyle = ComboBoxStyle.DropDownList,
                    Font = new System.Drawing.Font("Segoe UI", 9f)
                };
                cmbEventMapsList.SelectedIndexChanged += (s, e) =>
                {
                    if (cmbEventMapsList.SelectedItem is EventMapComboItem item)
                    {
                        _selectedEventMapId = item.MapID;
                        LoadSelectedMapEntities(item.MapID);
                    }
                };

                Button btnReloadEve = new Button
                {
                    Text = "🔄 Reload Eve.Emg",
                    Location = new System.Drawing.Point(504, 48),
                    Size = new System.Drawing.Size(130, 26),
                    BackColor = System.Drawing.Color.LightSkyBlue,
                    Font = new System.Drawing.Font("Segoe UI", 8.5f, System.Drawing.FontStyle.Bold)
                };
                btnReloadEve.Click += (s, e) =>
                {
                    cGlobal.gGameDataBase?.EveDat?.LoadFile("Data\\eve.Emg");
                    PopulateEventMapsDropdown();
                    LoadSelectedMapEntities(_selectedEventMapId);
                    MessageBox.Show("Eve.Emg reloaded successfully!", "Reloaded", MessageBoxButtons.OK, MessageBoxIcon.Information);
                };

                lblEventMapStats = new Label
                {
                    Text = "Map Entities: 0",
                    Font = new System.Drawing.Font("Segoe UI", 8.5f, System.Drawing.FontStyle.Bold),
                    ForeColor = System.Drawing.Color.DarkGreen,
                    Location = new System.Drawing.Point(645, 52),
                    AutoSize = true
                };

                pnlTop.Controls.AddRange(new Control[] {
                    lblHeader, lblEventMapTotals, lblSelectMap, txtEventMapSearch, cmbEventMapsList, btnReloadEve, lblEventMapStats
                });

                // === BOTTOM LIVE TESTER & DISPATCHER PANEL ===
                GroupBox grpTester = new GroupBox
                {
                    Text = "⚡ Live Event Tester & Player Quest Dispatcher",
                    Dock = DockStyle.Bottom,
                    Height = 65,
                    Font = new System.Drawing.Font("Segoe UI", 8.5f, System.Drawing.FontStyle.Bold),
                    ForeColor = System.Drawing.Color.DarkSlateBlue
                };

                Label lblPlayer = new Label { Text = "Online Player:", Location = new System.Drawing.Point(10, 26), AutoSize = true, ForeColor = System.Drawing.Color.Black };
                cmbEventTesterPlayer = new ComboBox { Location = new System.Drawing.Point(100, 23), Size = new System.Drawing.Size(160, 22), DropDownStyle = ComboBoxStyle.DropDownList, Font = new System.Drawing.Font("Segoe UI", 8.5f) };

                Label lblEventId = new Label { Text = "Event/Click ID:", Location = new System.Drawing.Point(270, 26), AutoSize = true, ForeColor = System.Drawing.Color.Black };
                numTestEventId = new NumericUpDown { Location = new System.Drawing.Point(365, 23), Size = new System.Drawing.Size(80, 22), Maximum = 65535, Value = 1, Font = new System.Drawing.Font("Segoe UI", 8.5f) };

                Button btnExecuteLive = new Button
                {
                    Text = "⚡ Execute Event Now",
                    Location = new System.Drawing.Point(455, 21),
                    Size = new System.Drawing.Size(155, 27),
                    BackColor = System.Drawing.Color.LightGreen,
                    ForeColor = System.Drawing.Color.Black,
                    Font = new System.Drawing.Font("Segoe UI", 8.5f, System.Drawing.FontStyle.Bold)
                };
                btnExecuteLive.Click += (s, e) =>
                {
                    Player target = GetSelectedLivePlayer();
                    if (target == null)
                    {
                        MessageBox.Show("Please select an online player first.", "No Player", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }

                    ushort evId = (ushort)numTestEventId.Value;
                    if (target.CurMap is GameMap gmap)
                    {
                        bool ok = Game.Maps.EveEventInterpreter.TryExecute(target, gmap, evId);
                        MessageBox.Show(ok ? $"Event #{evId} executed successfully for {target.CharName}!" : $"Event #{evId} had no matching conditions on map {gmap.MapID}.", "Result", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                };

                Button btnSyncFlags = new Button
                {
                    Text = "🔄 Sync Quest Flags (AC 24)",
                    Location = new System.Drawing.Point(618, 21),
                    Size = new System.Drawing.Size(175, 27),
                    BackColor = System.Drawing.Color.LightCyan,
                    ForeColor = System.Drawing.Color.Black,
                    Font = new System.Drawing.Font("Segoe UI", 8.5f, System.Drawing.FontStyle.Bold)
                };
                btnSyncFlags.Click += (s, e) =>
                {
                    Player target = GetSelectedLivePlayer();
                    if (target != null)
                    {
                        Game.QuestRelated.QuestManager.SendAllQuestFlags(target);
                        MessageBox.Show($"Synchronized all quest flags to live player {target.CharName}!", "Synchronized", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                };

                Button btnTeleportToMap = new Button
                {
                    Text = "🚀 Warp to Selected Map",
                    Location = new System.Drawing.Point(800, 21),
                    Size = new System.Drawing.Size(165, 27),
                    BackColor = System.Drawing.Color.LightGoldenrodYellow,
                    ForeColor = System.Drawing.Color.Black,
                    Font = new System.Drawing.Font("Segoe UI", 8.5f, System.Drawing.FontStyle.Bold)
                };
                btnTeleportToMap.Click += (s, e) =>
                {
                    Player target = GetSelectedLivePlayer();
                    if (target != null && target.CurMap != null)
                    {
                        var warp = new Game.Maps.WarpData() { DstMap = _selectedEventMapId, DstX_Axis = 400, DstY_Axis = 400 };
                        target.CurMap.Teleport(TeleportType.CmD, target, 0, warp);
                        MessageBox.Show($"Warped {target.CharName} to Map {_selectedEventMapId}!", "Teleported", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                };

                grpTester.Controls.AddRange(new Control[] {
                    lblPlayer, cmbEventTesterPlayer, lblEventId, numTestEventId, btnExecuteLive, btnSyncFlags, btnTeleportToMap
                });

                // === CENTER TABCONTROL: 6 SUB-GRIDS FOR ALL MAP ENTITIES ===
                tabControlMapEntities = new TabControl
                {
                    Dock = DockStyle.Fill,
                    Font = new System.Drawing.Font("Segoe UI", 8.5f)
                };

                // Tab 1: Traps & Step Triggers
                TabPage pageTraps = new TabPage("🪤 Floor Traps & Triggers");
                dgvMapTraps = CreateEntityGrid();
                pageTraps.Controls.Add(dgvMapTraps);

                // Tab 2: PreEvents
                TabPage pagePreEvents = new TabPage("🎭 Storyline PreEvents");
                dgvMapPreEvents = CreateEntityGrid();
                pagePreEvents.Controls.Add(dgvMapPreEvents);

                // Tab 3: Mining & Gathering
                TabPage pageMining = new TabPage("⛏️ Mining / Gathering Nodes");
                dgvMapMining = CreateEntityGrid();
                pageMining.Controls.Add(dgvMapMining);

                // Tab 4: Warp Doors
                TabPage pageWarps = new TabPage("🚪 Doors & Portals");
                dgvMapWarps = CreateEntityGrid();
                pageWarps.Controls.Add(dgvMapWarps);

                // Tab 5: Ground Chests
                TabPage pageChests = new TabPage("📦 Chests & Ground Items");
                dgvMapChests = CreateEntityGrid();
                pageChests.Controls.Add(dgvMapChests);

                // Tab 6: Map NPCs
                TabPage pageNpcs = new TabPage("👤 NPCs & Monsters");
                dgvMapNpcs = CreateEntityGrid();
                pageNpcs.Controls.Add(dgvMapNpcs);

                tabControlMapEntities.TabPages.AddRange(new TabPage[] {
                    pageTraps, pagePreEvents, pageMining, pageWarps, pageChests, pageNpcs
                });

                tabEventSystems.Controls.Add(tabControlMapEntities);
                tabEventSystems.Controls.Add(grpTester);
                tabEventSystems.Controls.Add(pnlTop);

                if (this.tabControl3 != null)
                {
                    this.tabControl3.TabPages.Add(tabEventSystems);
                }

                tabEventSystems.Enter += (s, e) =>
                {
                    RefreshEventTesterPlayers();
                    if (cmbEventMapsList.Items.Count == 0) PopulateEventMapsDropdown();
                };

                PopulateEventMapsDropdown();
                LoadSelectedMapEntities(10036);
            }
            catch (Exception ex)
            {
                DebugSystem.Write(DebugItemType.Error, $"[MainForm1] Error setting up Event Systems Tab: {ex.Message}");
            }
        }

        private DataGridView CreateEntityGrid()
        {
            return new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                BackgroundColor = System.Drawing.Color.White,
                RowHeadersVisible = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                Font = new System.Drawing.Font("Segoe UI", 8.5f)
            };
        }

        private class EventMapComboItem
        {
            public ushort MapID { get; set; }
            public string Name { get; set; }
            public override string ToString() => $"[{MapID}] {Name}";
        }

        private List<EventMapComboItem> _allEventMaps = new List<EventMapComboItem>();

        private static readonly Dictionary<ushort, string> _officialMapNames = new Dictionary<ushort, string>()
        {
            { 10000, "North Island (Mainland)" },
            { 10001, "Astrologer's Cave" },
            { 10002, "North Island Cave 1" },
            { 10003, "North Island Cave 2" },
            { 10004, "North Island Peak" },
            { 10005, "North Island Forest Path" },
            { 10010, "North Island Underground Pool" },
            { 10017, "Cruise Ship (Upper Deck)" },
            { 10018, "Cruise Ship (Captain's Room)" },
            { 10026, "Cruise Ship (Guest Cabins)" },
            { 10027, "Cruise Ship (Bar & Lounge)" },
            { 10028, "Cruise Ship (Engine Room)" },
            { 10035, "Rhodes Island (Beach Coast)" },
            { 10036, "Robinson Beach (Kelan Shore)" },
            { 11000, "World Ocean (South Pacific)" },
            { 11001, "World Ocean (Kelp Island Waters)" },
            { 11002, "World Ocean (Japan Waters)" },
            { 11003, "World Ocean (China Waters)" },
            { 11004, "World Ocean (Maya Waters)" },
            { 11005, "World Ocean (Egypt Waters)" },
            { 11006, "World Ocean (Rome Waters)" },
            { 11016, "World Ocean (Open Sea)" },
            { 12000, "South Island (Mainland)" },
            { 12001, "Welling Village" },
            { 12002, "Holy Village" },
            { 12003, "South Island Cave 1" },
            { 12004, "South Island Cave 2" },
            { 12005, "South Island Cave 3" },
            { 12006, "Pass of South Island" },
            { 12007, "South Island Waterfall" },
            { 12008, "South Island Pine Forest" },
            { 12010, "Welling Village Chief's House" },
            { 12011, "Welling Weapon Shop" },
            { 12012, "Welling Item Shop" },
            { 12020, "Holy Village Church" },
            { 12021, "Holy Village Chief's House" },
            { 12022, "Holy Village Weapon Shop" },
            { 12023, "Holy Village Item Shop" },
            { 13000, "Kelp Island (Coast)" },
            { 13001, "Kelp Island (Cave)" },
            { 13002, "Kelp Island (Forest)" },
            { 14000, "Maya (Jungle Coast)" },
            { 14001, "Maya (Tribal Village)" },
            { 14002, "Maya (Pyramid Sacred Altar)" },
            { 14003, "Maya (Underground Cave)" },
            { 15000, "Egypt (Nile Coast)" },
            { 15001, "Egypt (Cairo City)" },
            { 15002, "Egypt (Pyramid Interior)" },
            { 15003, "Egypt (Sphinx Underground)" },
            { 16000, "Japan (Kyoto Coast)" },
            { 16001, "Japan (Kyoto City)" },
            { 16002, "Japan (Ninja Village)" },
            { 16003, "Japan (Shinto Shrine)" },
            { 17000, "China (Great Wall Coast)" },
            { 17001, "China (Chang'an City)" },
            { 17002, "China (Forbidden Palace)" },
            { 17003, "China (Great Wall Pass)" },
            { 17004, "China (Taoist Cave)" },
            { 18000, "Rome (Imperial Harbor)" },
            { 18001, "Rome (Colosseum & City)" },
            { 18002, "Athens (Acropolis)" },
            { 18003, "Rome (Caesar's Palace)" },
            { 19000, "Persia (Desert Coast)" },
            { 19001, "Persia (Palace & Bazaar)" },
            { 19002, "Persia (Desert Oasis)" },
            { 19003, "India (Taj Mahal Area)" },
            { 20000, "Bangkok (Floating Market)" },
            { 21000, "Hawaii (Volcanic Beach)" },
            { 22000, "Cornwall / England" },
            { 23000, "Antarctica (Ice Shelf)" },
            { 24000, "Inca / Amazon Jungle" },
            { 25000, "Ghost Ship Island" },
            { 26000, "Treasure Island" },
            { 60001, "Player Space Tent (Floor 1)" },
            { 60002, "Player Space Tent (Floor 2)" }
        };

        public static string GetMapDisplayName(ushort mapId)
        {
            if (_officialMapNames.TryGetValue(mapId, out string name)) return name;
            return $"Map #{mapId}";
        }

        public static string GetItemDisplayName(uint itemId)
        {
            if (itemId == 0) return "Empty (0)";
            try
            {
                var it = cGlobal.ItemDatManager?.GetItemByID((ushort)itemId);
                if (it != null && it.ItemName != null && it.ItemName.Length > 0)
                {
                    string n = System.Text.Encoding.Default.GetString(it.ItemName).Trim('\0', ' ');
                    if (!string.IsNullOrEmpty(n)) return $"{n} ({itemId})";
                }
            }
            catch { }
            return $"Item #{itemId}";
        }

        private void PopulateEventMapsDropdown()
        {
            try
            {
                _allEventMaps.Clear();
                var eve = cGlobal.gGameDataBase?.EveDat;
                if (eve == null) return;

                for (int m = 10000; m <= 65000; m++)
                {
                    var mapData = eve.GetMapData((ushort)m);
                    if (mapData != null)
                    {
                        string mName = GetMapDisplayName((ushort)m);
                        _allEventMaps.Add(new EventMapComboItem { MapID = (ushort)m, Name = mName });
                    }
                }

                FilterEventMapsDropdown(txtEventMapSearch?.Text ?? "");
            }
            catch { }
        }

        private void FilterEventMapsDropdown(string search)
        {
            if (cmbEventMapsList == null) return;
            string s = (search ?? "").Trim().ToLower();
            cmbEventMapsList.Items.Clear();

            foreach (var item in _allEventMaps)
            {
                if (string.IsNullOrEmpty(s) || item.MapID.ToString().Contains(s) || item.Name.ToLower().Contains(s))
                {
                    cmbEventMapsList.Items.Add(item);
                }
            }

            if (cmbEventMapsList.Items.Count > 0)
            {
                var match = cmbEventMapsList.Items.OfType<EventMapComboItem>().FirstOrDefault(i => i.MapID == _selectedEventMapId);
                cmbEventMapsList.SelectedItem = match ?? cmbEventMapsList.Items[0];
            }
        }

        private void LoadSelectedMapEntities(ushort mapId)
        {
            try
            {
                var eve = cGlobal.gGameDataBase?.EveDat;
                if (eve == null) return;

                var mapData = eve.GetMapData(mapId);
                if (mapData == null) return;

                // 1. Traps & Step Triggers
                System.Data.DataTable dtTraps = new System.Data.DataTable();
                dtTraps.Columns.Add("EntryID", typeof(ushort));
                dtTraps.Columns.Add("Description", typeof(string));
                dtTraps.Columns.Add("Tile (X, Y)", typeof(string));
                dtTraps.Columns.Add("Pixel Position", typeof(string));
                dtTraps.Columns.Add("Area Size", typeof(string));
                dtTraps.Columns.Add("Sub-Triggers", typeof(int));

                if (mapData.InteractiveInfo != null)
                {
                    foreach (var trap in mapData.InteractiveInfo)
                    {
                        if (trap.subentry != null && trap.subentry.Count > 0)
                        {
                            var s = trap.subentry[0];
                            int tx = s.unknownbyte1;
                            int ty = s.unknownbyte2;
                            int px = tx * 20;
                            int py = ty * 20;
                            int w = Math.Max(20, (int)s.unknownbyte3 * 20);
                            int h = Math.Max(20, (int)s.unknownbyte4 * 20);

                            dtTraps.Rows.Add(trap.entryID, $"Step Trigger / Trap #{trap.entryID}", $"Tile ({tx}, {ty})", $"Pixel ({px}, {py})", $"{w} x {h} px", trap.subentry.Count);
                        }
                        else
                        {
                            dtTraps.Rows.Add(trap.entryID, $"Step Trigger #{trap.entryID}", "Tile (0, 0)", "Pixel (0, 0)", "20 x 20 px", 0);
                        }
                    }
                }
                dgvMapTraps.DataSource = dtTraps;

                // 2. PreEvents
                System.Data.DataTable dtPre = new System.Data.DataTable();
                dtPre.Columns.Add("Index", typeof(int));
                dtPre.Columns.Add("Click ID", typeof(ushort));
                dtPre.Columns.Add("Event Name / Flag", typeof(string));
                dtPre.Columns.Add("Type Code", typeof(byte));
                dtPre.Columns.Add("Sub-Conditions", typeof(int));

                if (mapData.PreEvents != null)
                {
                    for (int i = 0; i < mapData.PreEvents.Count; i++)
                    {
                        var pe = mapData.PreEvents[i];
                        dtPre.Rows.Add(i + 1, pe.clickID, $"PreEvent Action #{pe.clickID}", pe.unknownbyte1, pe.subentry1?.Count ?? 0);
                    }
                }
                dgvMapPreEvents.DataSource = dtPre;

                // 3. Mining & Gathering
                System.Data.DataTable dtMine = new System.Data.DataTable();
                dtMine.Columns.Add("Click ID", typeof(ushort));
                dtMine.Columns.Add("Node Name", typeof(string));
                dtMine.Columns.Add("Pixel Position", typeof(string));
                dtMine.Columns.Add("Tool Required", typeof(string));

                if (mapData.MiningAreas != null)
                {
                    foreach (var m in mapData.MiningAreas)
                    {
                        string tool;
                        switch (m.unknownbyte1)
                        {
                            case 1: tool = "⛏️ Pickaxe (Mining)"; break;
                            case 2: tool = "🎣 Fishing Rod (Fishing)"; break;
                            case 3: tool = "🪓 Wood Axe (Logging)"; break;
                            default: tool = $"Tool Type #{m.unknownbyte1}"; break;
                        }
                        dtMine.Rows.Add(m.clickID, $"Resource Gathering Node #{m.clickID}", $"({m.x}, {m.y})", tool);
                    }
                }
                dgvMapMining.DataSource = dtMine;

                // 4. Warp Doors & Portals
                System.Data.DataTable dtWarp = new System.Data.DataTable();
                dtWarp.Columns.Add("Portal ID", typeof(ushort));
                dtWarp.Columns.Add("Source Position", typeof(string));
                dtWarp.Columns.Add("Destination Map", typeof(string));
                dtWarp.Columns.Add("Destination Position", typeof(string));
                dtWarp.Columns.Add("Pass Condition", typeof(string));

                if (mapData.WarpLoc != null)
                {
                    foreach (var w in mapData.WarpLoc)
                    {
                        string dstMapStr = $"[{w.mapID}] {GetMapDisplayName(w.mapID)}";
                        string passStr = w.neededtopass > 0 ? $"Requires Event/Quest #{w.neededtopass}" : "Open";
                        dtWarp.Rows.Add(w.clickID, $"({w.x}, {w.y})", dstMapStr, $"({w.x}, {w.y})", passStr);
                    }
                }
                dgvMapWarps.DataSource = dtWarp;

                // 5. Chests & Ground Items
                System.Data.DataTable dtChest = new System.Data.DataTable();
                dtChest.Columns.Add("Click ID", typeof(ushort));
                dtChest.Columns.Add("Item Name / ID", typeof(string));
                dtChest.Columns.Add("Pixel Position", typeof(string));

                if (mapData.ItemAreas != null)
                {
                    foreach (var it in mapData.ItemAreas)
                    {
                        string itName = GetItemDisplayName(it.itemID);
                        dtChest.Rows.Add(it.clickID, itName, $"({it.x}, {it.y})");
                    }
                }
                dgvMapChests.DataSource = dtChest;

                // 6. NPCs & Monsters
                System.Data.DataTable dtNpc = new System.Data.DataTable();
                dtNpc.Columns.Add("Click ID", typeof(ushort));
                dtNpc.Columns.Add("NPC Name", typeof(string));
                dtNpc.Columns.Add("Template ID", typeof(uint));
                dtNpc.Columns.Add("Pixel Position", typeof(string));
                dtNpc.Columns.Add("Interactive Script", typeof(string));

                if (mapData.Npclist != null)
                {
                    foreach (var npc in mapData.Npclist)
                    {
                        string nName = GetNpcDisplayName(npc.npcId);
                        string script = npc.Events?.Count > 0 ? $"⚡ Interactive ({npc.Events.Count} script bytes)" : "Static NPC";
                        dtNpc.Rows.Add(npc.clickId, nName, npc.npcId, $"({npc.x}, {npc.y})", script);
                    }
                }
                dgvMapNpcs.DataSource = dtNpc;

                if (lblEventMapStats != null)
                {
                    lblEventMapStats.Text = $"Map #{mapId} ({GetMapDisplayName(mapId)}): {dtTraps.Rows.Count} Traps | {dtPre.Rows.Count} PreEvents | {dtMine.Rows.Count} Mining | {dtWarp.Rows.Count} Warps | {dtChest.Rows.Count} Chests | {dtNpc.Rows.Count} NPCs";
                }
            }
            catch (Exception ex)
            {
                DebugSystem.Write(DebugItemType.Error, $"[MainForm1] Error loading map entities for {mapId}: {ex.Message}");
            }
        }

        private void RefreshEventTesterPlayers()
        {
            if (cmbEventTesterPlayer == null) return;
            cmbEventTesterPlayer.Items.Clear();
            var online = cGlobal.gCharacterDataBase?.GetOnlinePlayers();
            if (online != null)
            {
                foreach (var p in online)
                {
                    cmbEventTesterPlayer.Items.Add(p);
                }
            }
            if (cmbEventTesterPlayer.Items.Count > 0) cmbEventTesterPlayer.SelectedIndex = 0;
        }

        private Player GetSelectedLivePlayer()
        {
            if (cmbEventTesterPlayer?.SelectedItem is Player p) return p;
            var online = cGlobal.gCharacterDataBase?.GetOnlinePlayers();
            return online?.FirstOrDefault();
        }
        #endregion
    }
}


