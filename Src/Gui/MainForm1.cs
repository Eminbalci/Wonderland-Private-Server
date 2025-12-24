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

        // NPC Database GUI Controls
        TabPage tabPageNPC;
        DataGridView dgvNPC;
        Button btnRefreshNPC;
        Button btnSaveNPC;

        // NPC Templates GUI Controls
        TabPage tabPageNpcTemplates;
        DataGridView dgvNpcTemplates;
        Button btnRefreshNpcTemplates;
        Button btnImportNpcCsv;
        Button btnSaveNpcTemplates;


        public Form1()
        {

            InitializeComponent();
            LoadAllLists();
        }


        void DebugSystem_onNewLog(object sender, DebugItem j)
        {
            new Task(() =>
            {
                this.Invoke(new Action(() =>
                {
                    switch (j.Type)
                    {
                        //case DebugItemType.Info_Light: SystemLog.AppendText(j.Msg + "\r\n=============================\r\n"); break;
                        //case DebugItemType.Network_Light: NetWorkLog.AppendText(j.Msg + "\r\n=============================\r\n"); break;
                        //case DebugItemType.Error: errorLog.AppendText(j.Msg + "\r\n=============================\r\n"); break;
                        //case Utilities.LogType.DB: errorLog.AppendText(j.Msg + "\r\n=============================\r\n"); break;
                        //case Utilities.LogType.THRD: SystemLog.AppendText(j.Msg + "\r\n=============================\r\n"); break;
                        //case Utilities.LogType.UPDT: SystemLog.AppendText(j.Msg + "\r\n=============================\r\n"); break;
                    }
                }));
            }
            ).Start();
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
                string s = null;
                //if (!string.IsNullOrEmpty((s = DebugSystem.PullLogItem())))
                //    SystemLog.AppendText(s);
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
            cGlobal.ItemDatManager.Load(Environment.CurrentDirectory + "\\Data\\itemDat.wpdat");
            DebugSystem.Write("[Init] - Initializing DataBase Objects");
            cGlobal.gUserDataBase = new DataBase.UserDataBase();
            cGlobal.gCharacterDataBase = new DataBase.CharacterDataBase();
            cGlobal.gCharacterDataBase.ItemDat = cGlobal.ItemDatManager;
            cGlobal.gGameDataBase = new DataBase.GameDataBase();
            cGlobal.gGameDataBase.ItemDat = cGlobal.ItemDatManager;
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


        private void InitializeNPCTab()
        {
            // Tabs removed as per request
        }

        private void btnRefreshNpcTemplates_Click(object sender, EventArgs e)
        {
            try
            {
                if (cGlobal.gGameDataBase == null) return;
                var data = cGlobal.gGameDataBase.GetAllNpcTemplates();
                if (data != null) dgvNpcTemplates.DataSource = data;
            }
            catch (Exception ex) { MessageBox.Show("Error loading templates: " + ex.Message); }
        }

        private void btnImportNpcCsv_Click(object sender, EventArgs e)
        {
            MessageBox.Show("CSV Import is disabled. Npc.dat is used automatically on startup.", "Info");
            // try
            // {
            //     if (cGlobal.gGameDataBase == null) return;
            //     // Assuming listdata/npc.csv is in bin/Debug/listdata/npc.csv
            //     string path = Environment.CurrentDirectory + "\\listdata\\npc.csv";
            //     // int count = cGlobal.gGameDataBase.ImportNpcDataFromCsv(path); // Method removed
            //     // MessageBox.Show($"Imported {count} NPCs from CSV.");
            //     btnRefreshNpcTemplates_Click(null, null);
            // }
            // catch (Exception ex) { MessageBox.Show("Error importing CSV: " + ex.Message); }
        }

        private void btnSaveNpcTemplates_Click(object sender, EventArgs e)
        {
            try
            {
                int updatedCount = 0;
                int addedCount = 0;

                foreach (DataGridViewRow row in dgvNpcTemplates.Rows)
                {
                    if (row.IsNewRow) continue;

                    if (row.Cells["id"].Value == null || row.Cells["id"].Value == DBNull.Value) continue;

                    int id = Convert.ToInt32(row.Cells["id"].Value);
                    string name = row.Cells["name"].Value?.ToString() ?? "";
                    int level = row.Cells["level"].Value != null && row.Cells["level"].Value != DBNull.Value ? Convert.ToInt32(row.Cells["level"].Value) : 1;
                    int hp = row.Cells["hp"].Value != null && row.Cells["hp"].Value != DBNull.Value ? Convert.ToInt32(row.Cells["hp"].Value) : 100;
                    int element = row.Cells["element"].Value != null && row.Cells["element"].Value != DBNull.Value ? Convert.ToInt32(row.Cells["element"].Value) : 0;

                    // For now, always update since we don't track new/modified easily in grid loop without bindings
                    // But we have Duplicate Key Update in Add? No, UpdateNpcTemplate uses UPDATE.
                    // AddNpcTemplate uses INSERT.
                    // Check if exists?
                    // Simplified: Try Update first, if rows affected=0, Add?
                    // GameDataBase.UpdateNpcTemplate returns true/false but based on execution success, not rows.
                    // Actually, ExecuteNonQuery returns rows affected?
                    // My ExecuteNonQuery implementation in Database.cs?
                    // Let's just use AddNpcTemplate with "ON DUPLICATE KEY UPDATE" logic if I changed it?
                    // In Step 1489 Import uses ON DUPLICATE.
                    // Here I wrote Update and Add separately.
                    // Let's use Update. If it fails (or returns 0 rows? I can't check), assume Add?
                    // Actually, if row exists in Grid and DB, Update works.
                    // If row is NEW in Grid (added by user), it's not in DB. Update fails (0 rows).
                    // So I should try Add if Update affects 0 rows.
                    // But my wrapper doesn't return rows.
                    // I'll call AddNpcTemplate which I should modify to use UPSERT logic?
                    // Or just use `Import` logic for single row?
                    // I'll use AddNpcTemplate (Insert). If it fails (duplicate), I'll try Update.

                    if (cGlobal.gGameDataBase.AddNpcTemplate(id, name, level, hp, element))
                    {
                        addedCount++;
                    }
                    else
                    {
                        if (cGlobal.gGameDataBase.UpdateNpcTemplate(id, name, level, hp, element))
                            updatedCount++;
                    }
                }
                MessageBox.Show($"Saved Templates! Added/Updated: {addedCount + updatedCount}");
                btnRefreshNpcTemplates_Click(null, null);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error saving templates: " + ex.Message);
            }
        }

        private void btnSaveNPC_Click(object sender, EventArgs e)
        {
            try
            {
                int updatedCount = 0;
                int addedCount = 0;

                foreach (DataGridViewRow row in dgvNPC.Rows)
                {
                    if (row.IsNewRow) continue;

                    // Simple validation: check if map_id exists
                    if (row.Cells["map_id"].Value == null || row.Cells["map_id"].Value == DBNull.Value) continue;

                    int mapId = Convert.ToInt32(row.Cells["map_id"].Value);
                    int clickId = Convert.ToInt32(row.Cells["click_id"].Value);
                    string type = row.Cells["npc_type"].Value?.ToString() ?? "QuestNpc";
                    string name = row.Cells["npc_name"].Value?.ToString() ?? "";
                    int x = row.Cells["x"].Value != null && row.Cells["x"].Value != DBNull.Value ? Convert.ToInt32(row.Cells["x"].Value) : 0;
                    int y = row.Cells["y"].Value != null && row.Cells["y"].Value != DBNull.Value ? Convert.ToInt32(row.Cells["y"].Value) : 0;

                    // Check if it's an existing row (has npc_id) or new
                    if (row.Cells["npc_id"].Value != null && row.Cells["npc_id"].Value != DBNull.Value)
                    {
                        int npcId = Convert.ToInt32(row.Cells["npc_id"].Value);
                        if (cGlobal.gGameDataBase.UpdateNPC(npcId, mapId, clickId, type, name, x, y))
                            updatedCount++;
                    }
                    else
                    {
                        if (cGlobal.gGameDataBase.AddNPC(mapId, clickId, type, name, x, y))
                            addedCount++;
                    }
                }
                MessageBox.Show($"Saved! Updated: {updatedCount}, Added: {addedCount}");
                btnRefreshNPC_Click(null, null); // Refresh to get proper IDs for new rows
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error saving NPCs: " + ex.Message);
            }
        }

        private void btnRefreshNPC_Click(object sender, EventArgs e)
        {
            try
            {
                if (cGlobal.gGameDataBase == null) return;
                var npcs = cGlobal.gGameDataBase.GetAllNPCs();
                if (npcs != null)
                {
                    dgvNPC.DataSource = npcs;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error loading NPCs: " + ex.Message);
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
    }
}
