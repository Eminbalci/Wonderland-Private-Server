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
            SetupGmTab();
            SetupItemMallTab();
            SetupMonsterDropsTab();
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

            // Load character filters and NPCs
            Task.Run(() =>
            {
                Thread.Sleep(2500); // Wait for server initialization
                try
                {
                    this.Invoke(new Action(() =>
                    {
                        LoadCharacterFilters();
                        LoadChestDropTargets();
                        // NPC tabs removed, so no refresh needed
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
            string itemDatPath = System.IO.Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "Data", "itemDat.wpdat");
            cGlobal.ItemDatManager.Load(itemDatPath).Wait();

            string talkDatPath = System.IO.Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "Data", "Talk.dat");
            cGlobal.TalkDatManager = new DataFiles.PhxTalkDat(talkDatPath);
            DebugSystem.Write($"[Init] - Loaded {cGlobal.TalkDatManager.Count} authentic dialogues from Talk.dat");

            string markDatPath = System.IO.Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "Data", "Mark.dat");
            cGlobal.MarkDatManager = new DataFiles.PhxMarkDat(markDatPath);
            DebugSystem.Write($"[Init] - Loaded {cGlobal.MarkDatManager.Count} quest marks from Mark.dat");
            Game.QuestRelated.QuestManager.LoadAuthenticQuestsFromMarkDat(markDatPath);

            string npcDatPath = System.IO.Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "Data", "Npc.dat");
            Game.Battle.MonsterDropManager.LoadFromNpcDat(npcDatPath);

            Game.PlayerRelated.GuildManager.Initialize();
            Game.PlayerRelated.MarriageManager.Initialize();
            Game.PlayerRelated.MailSystem.Initialize();
            Game.PlayerRelated.GmManager.Initialize();
            Game.PlayerRelated.ItemMallManager.Initialize();
            Game.Crafting.GatheringManager.Initialize();
            Game.Crafting.AlchemyManager.InitializeRecipes();

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
            var registrationServer = new Server.API.RegistrationServer(8080, cGlobal.gUserDataBase);
            registrationServer.Start();
            DebugSystem.Write("[Init] - Registration page available at: http://localhost:8080/");

            //cGlobal.WLO_World.Initialize();
            Thread.Sleep(2);
            //cGlobal.TcpListener.Initialize();
            #endregion


            do
            {
                #region Thread Management
                //try
                //{
                //    Thread r;
                //    foreach (var t in ThreadManager. cGlobal.ThreadManager)
                //        if (!t.Value.IsAlive)
                //            if (cGlobal.ThreadManager.TryRemove(t.Key, out r))
                //                DebugSystem.Write(t.Value.Name + " has been Terminated", Utilities.LogType.THRD);
                //}
                //catch { }
                #endregion
                #region TaskManager
                cGlobal.ApplicationTasks.onUpdateTick();
                #endregion
                Thread.Sleep(10);
            }
            while (cGlobal.Run);

            ShutDown();



        }


        public void ShutDown()
        {
            this.Invoke(new Action(() => { this.Enabled = false; }));

            UI.ShutDown_Dialog tmp = new UI.ShutDown_Dialog();
            tmp.Location = this.Location;
            tmp.Left = this.Left + 100;
            tmp.Top = this.Top + 250;

            tmp.ShowDialog();
            tmp.Dispose();

            cGlobal.Run = false;
            blockclose = false;


            this.Invoke(new Action(() => { Close(); }));
            foreach (var process in Process.GetProcessesByName("Wonderland Private Server"))
            {
                process.Kill();
            }
        }

        #region Form Events
        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (cGlobal.Run || blockclose)
                e.Cancel = true;
            cGlobal.Run = false;
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
                GetPrivatePlayer().AddItemToInventory(selectedItemID);
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
            // If a player is selected in the cheat tab combobox, use that player
            if (comboBox_OnlinePlayers.InvokeRequired)
            {
                return (Player)comboBox_OnlinePlayers.Invoke(new Func<Player>(() =>
                {
                    if (comboBox_OnlinePlayers.SelectedItem != null && comboBox_OnlinePlayers.SelectedItem is Player)
                        return (Player)comboBox_OnlinePlayers.SelectedItem;
                    return cGlobal.gLoginServer.privatePlayer;
                }));
            }
            else
            {
                if (comboBox_OnlinePlayers.SelectedItem != null && comboBox_OnlinePlayers.SelectedItem is Player)
                    return (Player)comboBox_OnlinePlayers.SelectedItem;
                return cGlobal.gLoginServer.privatePlayer;
            }
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

            try
            {
                btnSafeShutdown.Enabled = false;
                btnSaveAllNow.Enabled = false;

                // 1. Notify players in-game
                try
                {
                    var online = cGlobal.gCharacterDataBase?.GetOnlinePlayers();
                    if (online != null)
                    {
                        foreach (var p in online)
                        {
                            p.Send(Tools.FromFormat("bbbs", 23, 57, 0, "Server is performing a safe shutdown. Saving all data..."));
                        }
                    }
                }
                catch { }

                // 2. Save all online player data
                int savedCount = 0;
                try
                {
                    var online = cGlobal.gCharacterDataBase?.GetOnlinePlayers();
                    if (online != null)
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
                                DebugSystem.Write($"[SafeShutdown] Error saving player {p.CharName}: {ex.Message}");
                            }
                        }
                    }
                    DebugSystem.Write(DebugItemType.Info_Heavy, $"[SafeShutdown] Saved {savedCount} online players.");
                }
                catch (Exception ex)
                {
                    DebugSystem.Write(DebugItemType.Error, $"[SafeShutdown] Exception saving players: {ex.Message}");
                }

                // 3. Save drop configs and server settings
                try
                {
                    Game.Maps.ChestDropManager.SaveToFile();
                    cGlobal.SrvSettings?.SaveSettings(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\PServer\\Config.settings.wlo");
                }
                catch { }

                // 4. Terminate network listeners
                try
                {
                    cGlobal.gLoginServer?.Kill();
                    cGlobal.gItemMallServer?.Stop();
                    cGlobal.gWorld?.Kill();
                }
                catch { }

                // 5. Exit application cleanly
                blockclose = false;
                cGlobal.Run = false;
                Application.Exit();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error during safe shutdown: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                btnSafeShutdown.Enabled = true;
                btnSaveAllNow.Enabled = true;
            }
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
                    Size = new System.Drawing.Size(300, 320),
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
                TabPage tabMall = new TabPage("🛍️ Item Mall");
                tabMall.BackColor = System.Drawing.Color.White;

                Label lblHeader = new Label
                {
                    Text = "Item Mall Catalog & Player Points Management",
                    Location = new System.Drawing.Point(15, 10),
                    Size = new System.Drawing.Size(400, 20),
                    Font = new System.Drawing.Font("Segoe UI", 9.5f, System.Drawing.FontStyle.Bold)
                };

                // DataGridView for Catalog
                dgvMallCatalog = new DataGridView
                {
                    Location = new System.Drawing.Point(15, 35),
                    Size = new System.Drawing.Size(460, 335),
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

                // Move Up / Move Down / Reload buttons under grid
                Button btnMoveUp = new Button
                {
                    Text = "⬆️ Move Up",
                    Location = new System.Drawing.Point(15, 375),
                    Size = new System.Drawing.Size(100, 30),
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
                    Location = new System.Drawing.Point(125, 375),
                    Size = new System.Drawing.Size(100, 30),
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
                    Text = "🔄 Reload",
                    Location = new System.Drawing.Point(235, 375),
                    Size = new System.Drawing.Size(85, 30),
                    Font = new System.Drawing.Font("Segoe UI", 8.5f, System.Drawing.FontStyle.Bold)
                };
                btnReload.Click += (s, e) =>
                {
                    Game.PlayerRelated.ItemMallManager.LoadFromFile();
                    RefreshMallGrid();
                    MessageBox.Show("Item Mall reloaded from Data/item_mall.txt!", "Reloaded", MessageBoxButtons.OK, MessageBoxIcon.Information);
                };

                // GroupBox: Add/Edit Item
                GroupBox grpEditItem = new GroupBox
                {
                    Text = "Item Details",
                    Location = new System.Drawing.Point(490, 30),
                    Size = new System.Drawing.Size(260, 240)
                };

                Label lId = new Label { Text = "Item ID:", Location = new System.Drawing.Point(15, 25), Size = new System.Drawing.Size(65, 20) };
                txtMallItemId = new TextBox { Location = new System.Drawing.Point(85, 22), Size = new System.Drawing.Size(160, 22) };
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

                Label lName = new Label { Text = "Name:", Location = new System.Drawing.Point(15, 55), Size = new System.Drawing.Size(65, 20) };
                txtMallItemName = new TextBox { Location = new System.Drawing.Point(85, 52), Size = new System.Drawing.Size(160, 22) };

                Label lCat = new Label { Text = "Category:", Location = new System.Drawing.Point(15, 85), Size = new System.Drawing.Size(65, 20) };
                cmbMallCategory = new ComboBox { Location = new System.Drawing.Point(85, 82), Size = new System.Drawing.Size(160, 22), DropDownStyle = ComboBoxStyle.DropDownList };
                cmbMallCategory.Items.AddRange(new string[] { "Hot", "Grocery", "Furniture", "Armory", "Weaponry" });
                cmbMallCategory.SelectedIndex = 0;

                Label lCost = new Label { Text = "IM Points:", Location = new System.Drawing.Point(15, 115), Size = new System.Drawing.Size(65, 20) };
                numMallCost = new NumericUpDown { Location = new System.Drawing.Point(85, 112), Size = new System.Drawing.Size(160, 22), Minimum = 1, Maximum = 999999, Value = 100 };

                Label lCount = new Label { Text = "Quantity:", Location = new System.Drawing.Point(15, 145), Size = new System.Drawing.Size(65, 20) };
                numMallCount = new NumericUpDown { Location = new System.Drawing.Point(85, 142), Size = new System.Drawing.Size(160, 22), Minimum = 1, Maximum = 255, Value = 1 };

                btnAddMallItem = new Button
                {
                    Text = "➕ Add / Update",
                    Location = new System.Drawing.Point(15, 185),
                    Size = new System.Drawing.Size(110, 35),
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
                    Location = new System.Drawing.Point(135, 185),
                    Size = new System.Drawing.Size(110, 35),
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

                tabMall.Controls.Add(btnMoveUp);
                tabMall.Controls.Add(btnMoveDown);
                tabMall.Controls.Add(btnReload);

                // GroupBox: Player IM Points
                GroupBox grpPoints = new GroupBox
                {
                    Text = "Player IM Points (Nakit Puan)",
                    Location = new System.Drawing.Point(490, 275),
                    Size = new System.Drawing.Size(260, 140)
                };

                Label lblTarget = new Label { Text = "Target Player / User / ID:", Location = new System.Drawing.Point(10, 18), Size = new System.Drawing.Size(150, 15), Font = new System.Drawing.Font("Segoe UI", 7.5f) };

                cmbMallPlayers = new ComboBox
                {
                    Location = new System.Drawing.Point(10, 35),
                    Size = new System.Drawing.Size(240, 22),
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

                Label lblPts = new Label { Text = "Amount:", Location = new System.Drawing.Point(10, 62), Size = new System.Drawing.Size(55, 20) };
                numPlayerPoints = new NumericUpDown
                {
                    Location = new System.Drawing.Point(65, 60),
                    Size = new System.Drawing.Size(185, 22),
                    Minimum = 0,
                    Maximum = 99999999,
                    Value = 1000
                };

                btnAddPoints = new Button
                {
                    Text = "➕ Give Points",
                    Location = new System.Drawing.Point(10, 92),
                    Size = new System.Drawing.Size(115, 36),
                    BackColor = System.Drawing.Color.LightSkyBlue,
                    Font = new System.Drawing.Font("Segoe UI", 8.5f, System.Drawing.FontStyle.Bold)
                };
                btnAddPoints.Click += (s, e) =>
                {
                    string target = cmbMallPlayers.Text.Trim();
                    if (cmbMallPlayers.SelectedItem is Player p)
                    {
                        target = p.CharName;
                    }

                    if (GivePointsToAccountOrPlayer(target, (int)numPlayerPoints.Value, true, out string msg))
                    {
                        MessageBox.Show(msg, "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    else
                    {
                        MessageBox.Show(msg, "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                };

                btnSetPoints = new Button
                {
                    Text = "💾 Set Exact",
                    Location = new System.Drawing.Point(135, 92),
                    Size = new System.Drawing.Size(115, 36),
                    BackColor = System.Drawing.Color.LightGoldenrodYellow,
                    Font = new System.Drawing.Font("Segoe UI", 8.5f, System.Drawing.FontStyle.Bold)
                };
                btnSetPoints.Click += (s, e) =>
                {
                    string target = cmbMallPlayers.Text.Trim();
                    if (cmbMallPlayers.SelectedItem is Player p)
                    {
                        target = p.CharName;
                    }

                    if (GivePointsToAccountOrPlayer(target, (int)numPlayerPoints.Value, false, out string msg))
                    {
                        MessageBox.Show(msg, "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    else
                    {
                        MessageBox.Show(msg, "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                };

                grpPoints.Controls.Add(lblTarget);
                grpPoints.Controls.Add(cmbMallPlayers);
                grpPoints.Controls.Add(lblPts);
                grpPoints.Controls.Add(numPlayerPoints);
                grpPoints.Controls.Add(btnAddPoints);
                grpPoints.Controls.Add(btnSetPoints);

                tabMall.Controls.Add(lblHeader);
                tabMall.Controls.Add(dgvMallCatalog);
                tabMall.Controls.Add(grpEditItem);
                tabMall.Controls.Add(grpPoints);

                if (this.tabControl3 != null)
                {
                    this.tabControl3.TabPages.Add(tabMall);
                }

                RefreshMallGrid();
                Game.PlayerRelated.ItemMallManager.OnCatalogChanged += () =>
                {
                    if (this.IsHandleCreated)
                    {
                        this.BeginInvoke(new Action(RefreshMallGrid));
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
                TabPage tabDrops = new TabPage("🐲 Monster Drops");
                tabDrops.BackColor = System.Drawing.Color.White;

                Label lblHeader = new Label
                {
                    Text = "Monster Loot Drop Tables Management",
                    Location = new System.Drawing.Point(15, 10),
                    Size = new System.Drawing.Size(400, 20),
                    Font = new System.Drawing.Font("Segoe UI", 9.5f, System.Drawing.FontStyle.Bold)
                };

                // Left Panel: Search & Monster List
                Label lblSearch = new Label
                {
                    Text = "Search Monster (ID / Name):",
                    Location = new System.Drawing.Point(15, 35),
                    Size = new System.Drawing.Size(180, 18),
                    Font = new System.Drawing.Font("Segoe UI", 8.5f)
                };

                txtMonsterSearch = new TextBox
                {
                    Location = new System.Drawing.Point(15, 55),
                    Size = new System.Drawing.Size(220, 22),
                    Font = new System.Drawing.Font("Segoe UI", 9f)
                };
                txtMonsterSearch.TextChanged += (s, e) => RefreshMonsterListGrid();

                dgvMonsterList = new DataGridView
                {
                    Location = new System.Drawing.Point(15, 85),
                    Size = new System.Drawing.Size(300, 360),
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
                            lblSelectedMonster.Text = $"Selected: {mName} (TID: {tid})";
                            RefreshMonsterDropsGrid(tid);
                        }
                    }
                };

                // Right Panel: Drop Items DataGridView
                lblSelectedMonster = new Label
                {
                    Text = "Selected: (Select a monster)",
                    Location = new System.Drawing.Point(330, 35),
                    Size = new System.Drawing.Size(420, 20),
                    Font = new System.Drawing.Font("Segoe UI", 9f, System.Drawing.FontStyle.Bold),
                    ForeColor = System.Drawing.Color.DarkBlue
                };

                dgvMonsterDrops = new DataGridView
                {
                    Location = new System.Drawing.Point(330, 60),
                    Size = new System.Drawing.Size(425, 200),
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
                    Location = new System.Drawing.Point(330, 270),
                    Size = new System.Drawing.Size(425, 175),
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
                    Size = new System.Drawing.Size(195, 35),
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
                    Size = new System.Drawing.Size(195, 35),
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
                    Location = new System.Drawing.Point(10, 125),
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
                    Location = new System.Drawing.Point(215, 125),
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
                    Location = new System.Drawing.Point(10, 160),
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
                    Location = new System.Drawing.Point(215, 160),
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

                grpDropEdit.Size = new System.Drawing.Size(425, 200);

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

                tabDrops.Controls.Add(lblHeader);
                tabDrops.Controls.Add(lblSearch);
                tabDrops.Controls.Add(txtMonsterSearch);
                tabDrops.Controls.Add(dgvMonsterList);
                tabDrops.Controls.Add(lblSelectedMonster);
                tabDrops.Controls.Add(dgvMonsterDrops);
                tabDrops.Controls.Add(grpDropEdit);

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
    }
}

