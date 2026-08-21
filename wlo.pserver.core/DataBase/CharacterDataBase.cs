using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Game;
using Game.Bots;
using MySql.Data.MySqlClient;
using Network;
using RCLibrary.Core;
using RCLibrary.Core.Networking;


namespace DataBase
{

    struct CharacterDataRequest
    {
        public uint ID;
        public DateTime RequestedAt;
        public bool isOld { get { return ((DateTime.Now - RequestedAt) > new TimeSpan(0, 0, 30)); } }
        public Character Data;
    }


    public sealed class CharacterDataBase : RCLibrary.Core.DataBase
    {
        ConcurrentDictionary<int, Character> Characters_Online;
        List<CharacterDataRequest> CacheCharacters;

        const string DBServer = "CharacterDataBase";
        //DBConnector.DBOAuth DBAssist;

        public DataFiles.PhxItemDat ItemDat { private get; set; }

        List<string> client_requested_names = new List<string>();


        /// <summary>
        /// saves a cache of a character
        /// </summary>
        ConcurrentDictionary<int, Character> Cache = new ConcurrentDictionary<int, Character>();

        Dictionary<string, uint> CharNames = new Dictionary<string, uint>();

        public static CharacterDataBase GlobalInstance { get; set; }

        public CharacterDataBase()
        {
            GlobalInstance = this;
            //DBAssist = new DBConnector.DBOAuth();
            Characters_Online = new ConcurrentDictionary<int, Character>();
            CacheCharacters = new List<CharacterDataRequest>();
            DebugSystem.Write("[Init] - Configuring restricted Names");
            Setup();
        }

        void Setup()
        {
            #region Restricted Name
            CharNames.Add("tatsuya", 3322);//Jay
            CharNames.Add("compass", 3322);//Compass
            CharNames.Add("sharky", 3322);//Sharky
            CharNames.Add("rcharnel", 3322);//rcharnel
            CharNames.Add("Petron", 3322);//petron
            CharNames.Add("nipple", 3322);//Johnathan
            CharNames.Add("dragon", 3322);//Dragon
            CharNames.Add("maganda", 3322);//Maganda
            CharNames.Add("flurrih", 3132);//Flurrih
            #endregion
        }

        public void VerifySetup()
        {

            #region characters Columns
            Dictionary<string, string> col = new Dictionary<string, string>();
            col.Add("charID", "int/NN/PK");
            col.Add("slot", "int/NN");
            col.Add("head", "int/NN");
            col.Add("body", "int/NN");
            col.Add("name", "text");
            col.Add("name_clean", "text");
            col.Add("nickname", "text");
            col.Add("location_map", "int/NN");
            col.Add("location_x", "int/NN");
            col.Add("location_y", "int/NN");
            col.Add("haircolor", "int/NN");
            col.Add("skincolor", "int/NN");
            col.Add("clothingcolor", "int/NN");
            col.Add("eyecolor", "int/NN");
            col.Add("gold", "int/NN");
            col.Add("element", "int/NN");
            col.Add("rebirth", "int/NN");
            col.Add("job", "int/NN");
            col.Add("online", "int/NN");
            col.Add("cipher", "text");

            #endregion

            #region charextdata Columns
            Dictionary<string, string> extdtcol = new Dictionary<string, string>();
            extdtcol.Add("charID", "int/NN/PK");
            extdtcol.Add("Settings", "text");
            extdtcol.Add("Friends", "text");
            extdtcol.Add("Guild", "text");
            extdtcol.Add("Mail", "text");
            #endregion

            #region chartent Columns
            Dictionary<string, string> chartent = new Dictionary<string, string>();
            chartent.Add("charID", "int/NN/PK");
            chartent.Add("locked", "int");
            chartent.Add("enlarged", "int");
            chartent.Add("tenttype", "int");
            chartent.Add("floor1Color", "int");
            chartent.Add("floor1wallpaper", "int");
            chartent.Add("floor2Color", "int");
            chartent.Add("floor2wallpaperr", "int");
            #endregion

            #region charquest Columns
            Dictionary<string, string> charquest = new Dictionary<string, string>();
            charquest.Add("pri_key", "int/NN/AI/PK");
            charquest.Add("charID", "int/NN");
            charquest.Add("quest_started", "int");
            charquest.Add("quest_pos", "int");
            #endregion

            #region charunlocks Columns
            Dictionary<string, string> charunlocks = new Dictionary<string, string>();
            charunlocks.Add("pri_key", "int/NN/AI/PK");
            charunlocks.Add("charID", "int/NN");
            charunlocks.Add("maploc", "int");
            charunlocks.Add("clickID", "int");
            #endregion

            #region inv
            Dictionary<string, string> inv = new Dictionary<string, string>();
            inv.Add("pri_key", "int/NN/AI/PK");
            inv.Add("invIdx", "int/NN");
            inv.Add("charID", "int");
            inv.Add("storID", "int");
            inv.Add("itemID", "int");
            inv.Add("dmg", "int");
            inv.Add("qty", "int");
            inv.Add("pos", "int");
            inv.Add("socketID", "int");
            inv.Add("bombID", "int");
            inv.Add("sewID", "int");
            inv.Add("forge", "int");
            #endregion

            #region stats
            Dictionary<string, string> stats = new Dictionary<string, string>();
            stats.Add("pri_key", "int/NN/AI/PK");
            stats.Add("statIdx", "int/NN");
            stats.Add("charID", "int");
            stats.Add("statID", "int");
            stats.Add("StatusUp", "int");
            stats.Add("potential", "int");
            #endregion

            #region characters table Verification
            DebugSystem.Write("Checking for characters table");
        retry:

            if (GetDataTable("SELECT * FROM characters") != null) goto exist;

            DebugSystem.Write("Setuping up characters table");

            string nonsqlite_prikey = "";
            string cmstr = "create table characters (";

            foreach (var t in col)
            {
                var str = "";
                var att = t.Value.Split('/');

                switch (ServType)
                {
                    #region Mysql
                    case RCLibrary.Core.DataBaseTypes.MySQl:
                        {
                            foreach (var a in att)
                                switch (a)
                                {
                                    case "text": str += "text "; break;
                                    case "int": str += "int(11) "; break;
                                    case "NN": str += "NOT NULL "; break;
                                    case "AI": str += "AUTO_INCREMENT "; break;
                                    case "PK": nonsqlite_prikey = "PRIMARY KEY (" + t.Key + ")"; break;
                                }
                        }
                        break;
                    #endregion
                    #region Sqlite
                    case RCLibrary.Core.DataBaseTypes.Sqlite:
                        {
                            if (att.Count(c => c == "pk") > 0 && att.Count(c => c == "NN") > 0)
                                att = att.Where(c => c != "NN").ToArray();

                            foreach (var a in att)
                                switch (a)
                                {
                                    case "text": str += "TEXT "; break;
                                    case "int": str += "INTEGER "; break;
                                    case "PK": str += "PRIMARY KEY "; break;
                                }
                        }
                        break;
                        #endregion
                }

                cmstr += string.Format("{0} {1},", t.Key, str);
            }

            if (nonsqlite_prikey != "")
                cmstr += string.Format("{0},", nonsqlite_prikey);

            cmstr = cmstr.Substring(0, cmstr.Length - 1);

            if (ServType == RCLibrary.Core.DataBaseTypes.MySQl)
                cmstr += ") ENGINE=InnoDB DEFAULT CHARSET=utf8;";
            else if (ServType == RCLibrary.Core.DataBaseTypes.Sqlite)
                cmstr += ");";


            ExecuteNonQuery(cmstr);

        exist:
            DebugSystem.Write("Found characters table");
            DebugSystem.Write("Verifying characters columns");
            //table exists verify columns  
            //table exists verify columns  
            foreach (string h in col.Keys)
            {
                if (GetDataTable("select " + h + " from characters") == null)
                {
                    DebugSystem.Write("Recreating characters table");

                    ExecuteNonQuery("drop table if exists characters");
                    goto retry;
                }
            }
            #endregion

            #region charactersextdata Verification
            DebugSystem.Write("Checking for charactersextdata table");

        retry2:

            if (GetDataTable("SELECT * FROM charactersextdata") != null) goto exist2;

            DebugSystem.Write("Setuping up charactersextdata table");

            nonsqlite_prikey = "";
            cmstr = "create table " + "charactersextdata" + " (";

            foreach (var t in extdtcol)
            {
                var str = "";
                var att = t.Value.Split('/');

                switch (ServType)
                {
                    #region Mysql
                    case RCLibrary.Core.DataBaseTypes.MySQl:
                        {
                            foreach (var a in att)
                                switch (a)
                                {
                                    case "text": str += "text "; break;
                                    case "int": str += "int(11) "; break;
                                    case "NN": str += "NOT NULL "; break;
                                    case "PK": nonsqlite_prikey = "PRIMARY KEY (" + t.Key + ")"; break;
                                }
                        }
                        break;
                    #endregion
                    #region Sqlite
                    case RCLibrary.Core.DataBaseTypes.Sqlite:
                        {
                            if (att.Count(c => c == "pk") > 0 && att.Count(c => c == "NN") > 0)
                                att = att.Where(c => c != "NN").ToArray();

                            foreach (var a in att)
                                switch (a)
                                {
                                    case "text": str += "TEXT "; break;
                                    case "int": str += "INTEGER "; break;
                                    case "PK": str += "PRIMARY KEY "; break;
                                }
                        }
                        break;
                        #endregion
                }

                cmstr += string.Format("{0} {1},", t.Key, str);
            }

            if (nonsqlite_prikey != "")
                cmstr += string.Format("{0},", nonsqlite_prikey);

            cmstr = cmstr.Substring(0, cmstr.Length - 1);

            if (ServType == RCLibrary.Core.DataBaseTypes.MySQl)
                cmstr += ") ENGINE=InnoDB DEFAULT CHARSET=utf8;";
            else if (ServType == RCLibrary.Core.DataBaseTypes.Sqlite)
                cmstr += ");";


            ExecuteNonQuery(cmstr);

        exist2:
            DebugSystem.Write("Found charactersextdata table");
            DebugSystem.Write("Verifying charactersextdata columns");
            //table exists verify columns  
            //table exists verify columns  
            foreach (string h in extdtcol.Keys)
            {
                if (GetDataTable("select " + h + " from " + "charactersextdata") == null)
                {
                    DebugSystem.Write("Recreating " + "charactersextdata" + " table");

                    ExecuteNonQuery("drop table if exists " + "charactersextdata");
                    goto retry2;
                }
            }
            #endregion

            #region chartent Verification
            DebugSystem.Write("Checking for chartent table");
        retry3:

            if (GetDataTable("SELECT * FROM " + "chartent") != null) goto exist3;

            DebugSystem.Write("Setuping up chartent table");

            nonsqlite_prikey = "";
            cmstr = "create table " + "chartent" + " (";

            foreach (var t in chartent)
            {
                var str = "";
                var att = t.Value.Split('/');

                switch (ServType)
                {
                    #region Mysql
                    case RCLibrary.Core.DataBaseTypes.MySQl:
                        {
                            foreach (var a in att)
                                switch (a)
                                {
                                    case "text": str += "text "; break;
                                    case "int": str += "int(11) "; break;
                                    case "NN": str += "NOT NULL "; break;
                                    case "PK": nonsqlite_prikey = "PRIMARY KEY (" + t.Key + ")"; break;
                                }
                        }
                        break;
                    #endregion
                    #region Sqlite
                    case RCLibrary.Core.DataBaseTypes.Sqlite:
                        {
                            if (att.Count(c => c == "pk") > 0 && att.Count(c => c == "NN") > 0)
                                att = att.Where(c => c != "NN").ToArray();

                            foreach (var a in att)
                                switch (a)
                                {
                                    case "text": str += "TEXT "; break;
                                    case "int": str += "INTEGER "; break;
                                    case "PK": str += "PRIMARY KEY "; break;
                                }
                        }
                        break;
                        #endregion
                }

                cmstr += string.Format("{0} {1},", t.Key, str);
            }

            if (nonsqlite_prikey != "")
                cmstr += string.Format("{0},", nonsqlite_prikey);

            cmstr = cmstr.Substring(0, cmstr.Length - 1);

            if (ServType == RCLibrary.Core.DataBaseTypes.MySQl)
                cmstr += ") ENGINE=InnoDB DEFAULT CHARSET=utf8;";
            else if (ServType == RCLibrary.Core.DataBaseTypes.Sqlite)
                cmstr += ");";


            ExecuteNonQuery(cmstr);

        exist3:
            //table exists verify columns  
            //table exists verify columns  
            foreach (string h in chartent.Keys)
            {
                if (GetDataTable("select " + h + " from " + "chartent") == null)
                {
                    DebugSystem.Write("Recreating " + "chartent" + " table");

                    ExecuteNonQuery("drop table if exists " + "chartent");
                    goto retry3;
                }
            }
            #endregion

            #region charquest Verification
            DebugSystem.Write("Checking for charquest table");
        retry4:

            if (GetDataTable("SELECT * FROM " + "charquest") != null) goto exist4;

            DebugSystem.Write("Setuping up charquest table");

            nonsqlite_prikey = "";
            cmstr = "create table " + "charquest" + " (";

            foreach (var t in charquest)
            {
                var str = "";
                var att = t.Value.Split('/');

                switch (ServType)
                {
                    #region Mysql
                    case RCLibrary.Core.DataBaseTypes.MySQl:
                        {
                            foreach (var a in att)
                                switch (a)
                                {
                                    case "text": str += "text "; break;
                                    case "int": str += "int(11) "; break;
                                    case "NN": str += "NOT NULL "; break;
                                    case "PK": nonsqlite_prikey = "PRIMARY KEY (" + t.Key + ")"; break;
                                }
                        }
                        break;
                    #endregion
                    #region Sqlite
                    case RCLibrary.Core.DataBaseTypes.Sqlite:
                        {
                            if (att.Count(c => c == "pk") > 0 && att.Count(c => c == "NN") > 0)
                                att = att.Where(c => c != "NN").ToArray();

                            foreach (var a in att)
                                switch (a)
                                {
                                    case "text": str += "TEXT "; break;
                                    case "int": str += "INTEGER "; break;
                                    case "PK": str += "PRIMARY KEY "; break;
                                }
                        }
                        break;
                        #endregion
                }

                cmstr += string.Format("{0} {1},", t.Key, str);
            }

            if (nonsqlite_prikey != "")
                cmstr += string.Format("{0},", nonsqlite_prikey);

            cmstr = cmstr.Substring(0, cmstr.Length - 1);

            if (ServType == RCLibrary.Core.DataBaseTypes.MySQl)
                cmstr += ") ENGINE=InnoDB DEFAULT CHARSET=utf8;";
            else if (ServType == RCLibrary.Core.DataBaseTypes.Sqlite)
                cmstr += ");";


            ExecuteNonQuery(cmstr);

        exist4:
            //table exists verify columns  
            //table exists verify columns  
            foreach (string h in charquest.Keys)
            {
                if (GetDataTable("select " + h + " from " + "charquest") == null)
                {
                    DebugSystem.Write("Recreating " + "charquest" + " table");

                    ExecuteNonQuery("drop table if exists " + "charquest");
                    goto retry4;
                }
            }
            #endregion

            #region charunlocks Verification
            DebugSystem.Write("Checking for charunlocks table");
        retry5:

            if (GetDataTable("SELECT * FROM " + "charunlocks") != null) goto exist5;

            DebugSystem.Write("Setuping up charunlocks table");

            nonsqlite_prikey = "";
            cmstr = "create table " + "charunlocks" + " (";

            foreach (var t in charunlocks)
            {
                var str = "";
                var att = t.Value.Split('/');

                switch (ServType)
                {
                    #region Mysql
                    case RCLibrary.Core.DataBaseTypes.MySQl:
                        {
                            foreach (var a in att)
                                switch (a)
                                {
                                    case "text": str += "text "; break;
                                    case "int": str += "int(11) "; break;
                                    case "NN": str += "NOT NULL "; break;
                                    case "PK": nonsqlite_prikey = "PRIMARY KEY (" + t.Key + ")"; break;
                                }
                        }
                        break;
                    #endregion
                    #region Sqlite
                    case RCLibrary.Core.DataBaseTypes.Sqlite:
                        {
                            if (att.Count(c => c == "pk") > 0 && att.Count(c => c == "NN") > 0)
                                att = att.Where(c => c != "NN").ToArray();

                            foreach (var a in att)
                                switch (a)
                                {
                                    case "text": str += "TEXT "; break;
                                    case "int": str += "INTEGER "; break;
                                    case "PK": str += "PRIMARY KEY "; break;
                                }
                        }
                        break;
                        #endregion
                }

                cmstr += string.Format("{0} {1},", t.Key, str);
            }

            if (nonsqlite_prikey != "")
                cmstr += string.Format("{0},", nonsqlite_prikey);

            cmstr = cmstr.Substring(0, cmstr.Length - 1);

            if (ServType == RCLibrary.Core.DataBaseTypes.MySQl)
                cmstr += ") ENGINE=InnoDB DEFAULT CHARSET=utf8;";
            else if (ServType == RCLibrary.Core.DataBaseTypes.Sqlite)
                cmstr += ");";


            ExecuteNonQuery(cmstr);

        exist5:
            //table exists verify columns  
            //table exists verify columns  
            foreach (string h in charunlocks.Keys)
            {
                if (GetDataTable("select " + h + " from " + "charunlocks") == null)
                {
                    DebugSystem.Write("Recreating " + "charunlocks" + " table");

                    ExecuteNonQuery("drop table if exists " + "charunlocks");
                    goto retry5;
                }
            }
            #endregion

            #region inv Verification
            DebugSystem.Write("Checking for inventory table");
        retry6:

            if (GetDataTable("SELECT * FROM " + "inventory") != null) goto exist6;

            DebugSystem.Write("Setuping up inventory table");

            nonsqlite_prikey = "";
            cmstr = "create table " + "inventory" + " (";

            foreach (var t in inv)
            {
                var str = "";
                var att = t.Value.Split('/');

                switch (ServType)
                {
                    #region Mysql
                    case RCLibrary.Core.DataBaseTypes.MySQl:
                        {
                            foreach (var a in att)
                                switch (a)
                                {
                                    case "text": str += "text "; break;
                                    case "int": str += "int(11) "; break;
                                    case "NN": str += "NOT NULL "; break;
                                    case "PK": nonsqlite_prikey = "PRIMARY KEY (" + t.Key + ")"; break;
                                }
                        }
                        break;
                    #endregion
                    #region Sqlite
                    case RCLibrary.Core.DataBaseTypes.Sqlite:
                        {
                            if (att.Count(c => c == "pk") > 0 && att.Count(c => c == "NN") > 0)
                                att = att.Where(c => c != "NN").ToArray();

                            foreach (var a in att)
                                switch (a)
                                {
                                    case "text": str += "TEXT "; break;
                                    case "int": str += "INTEGER "; break;
                                    case "PK": str += "PRIMARY KEY "; break;
                                }
                        }
                        break;
                        #endregion
                }

                cmstr += string.Format("{0} {1},", t.Key, str);
            }

            if (nonsqlite_prikey != "")
                cmstr += string.Format("{0},", nonsqlite_prikey);

            cmstr = cmstr.Substring(0, cmstr.Length - 1);

            if (ServType == RCLibrary.Core.DataBaseTypes.MySQl)
                cmstr += ") ENGINE=InnoDB DEFAULT CHARSET=utf8;";
            else if (ServType == RCLibrary.Core.DataBaseTypes.Sqlite)
                cmstr += ");";


            ExecuteNonQuery(cmstr);

        exist6:
            //table exists verify columns  
            //table exists verify columns  
            foreach (string h in inv.Keys)
            {
                if (GetDataTable("select " + h + " from " + "inventory") == null)
                {
                    DebugSystem.Write("Recreating " + "inventory" + " table");

                    ExecuteNonQuery("drop table if exists " + "inventory");
                    goto retry6;
                }
            }
            #endregion

            #region stats Verification
            DebugSystem.Write("Checking for stats table");
        retry7:

            if (GetDataTable("SELECT * FROM " + "stats") != null) goto exist7;

            DebugSystem.Write("Setuping up stats table");

            nonsqlite_prikey = "";
            cmstr = "create table " + "stats" + " (";

            foreach (var t in stats)
            {
                var str = "";
                var att = t.Value.Split('/');

                switch (ServType)
                {
                    #region Mysql
                    case RCLibrary.Core.DataBaseTypes.MySQl:
                        {
                            foreach (var a in att)
                                switch (a)
                                {
                                    case "text": str += "text "; break;
                                    case "int": str += "int(11) "; break;
                                    case "NN": str += "NOT NULL "; break;
                                    case "PK": nonsqlite_prikey = "PRIMARY KEY (" + t.Key + ")"; break;
                                }
                        }
                        break;
                    #endregion
                    #region Sqlite
                    case RCLibrary.Core.DataBaseTypes.Sqlite:
                        {
                            if (att.Count(c => c == "pk") > 0 && att.Count(c => c == "NN") > 0)
                                att = att.Where(c => c != "NN").ToArray();

                            foreach (var a in att)
                                switch (a)
                                {
                                    case "text": str += "TEXT "; break;
                                    case "int": str += "INTEGER "; break;
                                    case "PK": str += "PRIMARY KEY "; break;
                                }
                        }
                        break;
                        #endregion
                }

                cmstr += string.Format("{0} {1},", t.Key, str);
            }

            if (nonsqlite_prikey != "")
                cmstr += string.Format("{0},", nonsqlite_prikey);

            cmstr = cmstr.Substring(0, cmstr.Length - 1);

            if (ServType == RCLibrary.Core.DataBaseTypes.MySQl)
                cmstr += ") ENGINE=InnoDB DEFAULT CHARSET=utf8;";
            else if (ServType == RCLibrary.Core.DataBaseTypes.Sqlite)
                cmstr += ");";


            ExecuteNonQuery(cmstr);

        exist7:
            DebugSystem.Write("Found stats table");
            DebugSystem.Write("Verifying stats columns");
            //table exists verify columns  
            //table exists verify columns  
            foreach (string h in stats.Keys)
            {
                if (GetDataTable("select " + h + " from " + "stats") == null)
                {
                    DebugSystem.Write("Recreating " + "stats" + " table");

                    ExecuteNonQuery("drop table if exists " + "stats");
                    goto retry7;
                }
            }
            #endregion
        }

        public bool LockName(uint dbacc, string name)
        {
            if (name == null) return false;

            List<KeyValuePair<string, object>> parameters = new List<KeyValuePair<string, object>>();
            parameters.Add(new KeyValuePair<string, object>("@myname", name.ToLower()));

            DataTable src = null;

            if (!client_requested_names.Contains(name.ToLower()))
            {
                try { src = GetDataTable("SELECT * FROM characters where name_clean = @myname", new DbParam("@myname", name.ToLower())); }
                catch (Exception ex) { DebugSystem.Write(new ExceptionData(ex)); return false; }

                if (src.Rows.Count == 0)
                {

                    if (CharNames.ContainsKey(name.ToLower()))
                    {
                        if (CharNames[name.ToLower()] != dbacc) return false;
                        else
                        {
                            client_requested_names.Add(name.ToLower()); return true;
                        }
                    }
                    else
                        client_requested_names.Add(name.ToLower()); return true;
                }
                return false;
            }
            else
                return false;
        }

        public void unLockName(string name)
        {
            if (name == null) return;
            if (client_requested_names.Contains(name.ToLower()))
                client_requested_names.Remove(name.ToLower());
        }

        //public bool NameTaken(string name,uint target)
        //{
        //    MySqlCommand cmd = null;
        //    MySqlDataReader reader = null;
        //    DataTable src = null;
        //    DataRow[] rows = new DataRow[0];

        //    MySqlConnection conn = GenerateConn();
        //    try { conn.Open(); }
        //    catch (MySqlException f) { DebugSystem.Write(f); throw; }

        //    cmd = new MySqlCommand("SELECT * FROM characters where name = @myname", conn);
        //    cmd.Parameters.AddWithValue("@myname", name);
        //    try { reader = cmd.ExecuteReader(); }
        //    catch (MySqlException ex) { DebugSystem.Write(new ExceptionData(ex)); throw; }          
        //    src = new DataTable();
        //    src.Load(reader);
        //    if (src.Rows.Count != 0)
        //    {
        //        cmd = new MySqlCommand("SELECT * FROM characters where charID =" + target, conn);
        //        cmd.Parameters.AddWithValue("@myname", name);
        //        try { reader = cmd.ExecuteReader(); }
        //        catch (MySqlException ex) { DebugSystem.Write(new ExceptionData(ex)); throw; }
        //        src = new DataTable();
        //        src.Load(reader);
        //        if (src.Rows.Count != 0)
        //            return !(src.Rows[0]["name_clean"].ToString() == name.ToLower());
        //        else
        //            return true;
        //    }            
        //    conn.Close();
        //    return false;
        //}
        //public bool ApplyNameChange(uint charID, string name, out Exception err)
        //{
        //    MySqlCommand cmd = null;
        //    DataRow[] rows = new DataRow[0];

        //    MySqlConnection conn = GenerateConn();
        //    try { conn.Open(); }
        //    catch (MySqlException f) { DebugSystem.Write(f); err = f; return false; }

        //    cmd = new MySqlCommand("UPDATE characters SET name = @myname, name_clean=@myname2 where charID ='" + charID + "'", conn);
        //    cmd.Parameters.AddWithValue("@myname", name);
        //    cmd.Parameters.AddWithValue("@myname2", name.ToLower());
        //    try { err = null; return (cmd.ExecuteNonQuery() == 1); }
        //    catch (MySqlException ex) { DebugSystem.Write(new ExceptionData(ex)); err = ex; return false; }
        //    finally
        //    {
        //        conn.Close();
        //    }
        //}

        public void DeleteCharacter(UInt32 ID)
        {
            try { Delete("characters", "charID = '" + ID + "';"); }
            catch (Exception ex) { DebugSystem.Write(new ExceptionData(ex)); }

            try { Delete("charactersExtData", "charID = '" + ID + "';"); }
            catch (Exception ex) { DebugSystem.Write(new ExceptionData(ex)); }

            try { Delete("stats", "charID = '" + ID + "';"); }
            catch (Exception ex) { DebugSystem.Write(new ExceptionData(ex)); }

            try { Delete("inventory", "charID = '" + ID + "';"); }
            catch (Exception ex) { DebugSystem.Write(new ExceptionData(ex)); }

            try { ExecuteNonQuery("DELETE FROM character_pets WHERE charID = '" + ID + "';"); }
            catch (Exception ex) { DebugSystem.Write(new ExceptionData(ex)); }

            try { ExecuteNonQuery("DELETE FROM charquest WHERE charID = '" + ID + "';"); }
            catch (Exception ex) { DebugSystem.Write(new ExceptionData(ex)); }

            try { ExecuteNonQuery("DELETE FROM chartent WHERE charID = '" + ID + "';"); }
            catch (Exception ex) { DebugSystem.Write(new ExceptionData(ex)); }

            try { ExecuteNonQuery("DELETE FROM charunlocks WHERE charID = '" + ID + "';"); }
            catch (Exception ex) { DebugSystem.Write(new ExceptionData(ex)); }

            try { ExecuteNonQuery("DELETE FROM character_skills WHERE charID = '" + ID + "';"); }
            catch (Exception ex) { DebugSystem.Write(new ExceptionData(ex)); }

            try { ExecuteNonQuery("DELETE FROM Friends WHERE charID = '" + ID + "' OR friendID = '" + ID + "';"); }
            catch (Exception ex) { DebugSystem.Write(new ExceptionData(ex)); }

            if (Cache.ContainsKey((int)ID))
            {
                Character t;
                Cache.TryRemove((int)ID, out t);
            }
        }

        public DataTable GetAllCharacters()
        {
            try
            {
                return GetDataTable("SELECT charID, slot, name, nickname, location_map, location_x, location_y, gold, element, job FROM characters ORDER BY charID");
            }
            catch (Exception ex)
            {
                DebugSystem.Write(new ExceptionData(ex));
                return null;
            }
        }

        public Character GetCharacterData(uint charID)
        {
            bool good = true;

            if (Cache.ContainsKey((int)charID))
                return Cache[(int)charID];

            Character t = new Character();

            DataTable src = null;

            DataRow[] rows;

            #region Get Character
            try { src = GetDataTable("SELECT * FROM characters where charID = '" + charID + "'"); }
            catch (MySqlException ex) { DebugSystem.Write(new ExceptionData(ex)); throw; }

            if (src.Rows.Count > 0)
            {
                rows = new DataRow[src.Rows.Count];
                src.Rows.CopyTo(rows, 0);
                t.Slot = byte.Parse(rows[0]["slot"].ToString());
                t.CharID = uint.Parse(rows[0]["charID"].ToString());
                t.Head = byte.Parse(rows[0]["head"].ToString());
                t.Body = (BodyStyle)uint.Parse(rows[0]["body"].ToString());
                t.CharName = rows[0]["name"].ToString();
                t.NickName = rows[0]["nickname"].ToString();
                t.LoginMap = ushort.Parse(rows[0]["location_map"].ToString());
                t.CurX = ushort.Parse(rows[0]["location_x"].ToString());
                t.CurY = ushort.Parse(rows[0]["location_y"].ToString());
                t.HairColor = ushort.Parse(rows[0]["haircolor"].ToString());
                t.SkinColor = ushort.Parse(rows[0]["skincolor"].ToString());
                t.ClothingColor = ushort.Parse(rows[0]["clothingcolor"].ToString());
                t.EyeColor = ushort.Parse(rows[0]["eyecolor"].ToString());
                t.SetGold(int.Parse(rows[0]["gold"].ToString()));
                t.Element = (Affinity)byte.Parse(rows[0]["element"].ToString());
                t.Job = (RebornJob)byte.Parse(rows[0]["job"].ToString());
            }
            else
            {
                DebugSystem.Write(DBServer + "No Character Data found for " + charID);
                return null;
            }
            #endregion

            #region load stat data
            src = GetDataTable("SELECT * FROM stats where charID = '" + charID + "'");

            if (src.Rows.Count > 0)
            {
                rows = new DataRow[src.Rows.Count];
                src.Rows.CopyTo(rows, 0);
                List<long[]> tmp2 = new List<long[]>();
                if (t == null) t = new Character();
                foreach (var row in rows)
                {
                    switch (long.Parse(row["statID"].ToString()))
                    {
                        case 25: t.CurHP = int.Parse(row["StatusUp"].ToString()); break;
                        case 26: t.CurSP = ushort.Parse(row["StatusUp"].ToString()); break;
                        case 38: t.SkillPoints = ushort.Parse(row["StatusUp"].ToString()); break;
                        case 36: t.TotalExp = long.Parse(row["StatusUp"].ToString()); break;
                        case 28: t.baseStr = ushort.Parse(row["StatusUp"].ToString()); break;
                        case 29: t.baseCon = ushort.Parse(row["StatusUp"].ToString()); break;
                        case 30: t.baseAgi = ushort.Parse(row["StatusUp"].ToString()); break;
                        case 27: t.baseInt = ushort.Parse(row["StatusUp"].ToString()); break;
                        case 33: t.baseWis = ushort.Parse(row["StatusUp"].ToString()); break;
                    }
                }
            }
            #endregion

            #region load equips
            src = GetDataTable("SELECT * FROM inventory where charID = '" + charID + "' AND storID =1");

            if (src.Rows.Count > 0)
            {
                rows = new DataRow[src.Rows.Count];

                src.Rows.CopyTo(rows, 0);

                ushort id;

                for (int i = 0; i < rows.Length; i++)
                {
                    id = ushort.Parse(rows[i]["itemID"].ToString());
                    if (id != 0)
                    {
                        var baseItem = ItemDat?.GetItemByID(id);
                        if (baseItem == null || baseItem.ItemID == 0)
                        {
                            baseItem = new DataFiles.PhxItemInfo() { ItemID = id, ItemName = Encoding.ASCII.GetBytes("Item " + id) };
                        }
                        byte pos = byte.Parse(rows[i]["pos"].ToString());
                        if (pos >= 1 && pos <= 6)
                        {
                            t[pos].CopyFrom(baseItem);
                            t[pos].Ammt = 1;
                            t[pos].Damage = byte.Parse(rows[i]["dmg"].ToString());
                        }
                    }
                }
            }
            else
            {
                t.SetBeginnerOutfit();
            }
            #endregion

            Cache[(int)charID] = t;
            return t;
        }

        public bool GetCharacterData(uint charID, ref Player t)
        {
            if (charID == 0) return false;

            if (Cache.ContainsKey((int)charID))
            {
                var c = Cache[(int)charID];
                t.CharID = c.CharID;
                t.Head = c.Head;
                t.Body = c.Body;
                t.CharName = c.CharName;
                t.NickName = c.NickName;
                t.LoginMap = c.LoginMap;
                t.CurX = c.CurX;
                t.CurY = c.CurY;
                t.HairColor = c.HairColor;
                t.SkinColor = c.SkinColor;
                t.ClothingColor = c.ClothingColor;
                t.EyeColor = c.EyeColor;
                t.SetGold((int)c.Gold);
                t.Element = c.Element;
                t.Job = c.Job;
                t.baseStr = c.baseStr;
                t.baseCon = c.baseCon;
                t.baseAgi = c.baseAgi;
                t.baseInt = c.baseInt;
                t.baseWis = c.baseWis;
                t.CurHP = c.CurHP;
                t.CurSP = c.CurSP;
                t.TotalExp = c.TotalExp;
                t.SkillPoints = c.SkillPoints;
                t.Potential = c.Potential;
                for (byte i = 1; i <= 6; i++)
                {
                    if (c[i].ItemID > 0)
                    {
                        t[i].CopyFrom(c[i]);
                        t[i].Ammt = c[i].Ammt;
                        t[i].Damage = c[i].Damage;
                    }
                }
                return true;
            }

            DataTable src = null;
            DataRow[] rows = new DataRow[0];

            try { src = GetDataTable("SELECT * FROM characters where charID = '" + charID + "'"); }
            catch (MySqlException ex) { DebugSystem.Write(new ExceptionData(ex)); throw; }

            if (src.Rows.Count > 0)
            {
                rows = new DataRow[src.Rows.Count];
                src.Rows.CopyTo(rows, 0);
                t.CharID = uint.Parse(rows[0]["charID"].ToString());
                t.Head = byte.Parse(rows[0]["head"].ToString());
                t.Body = (BodyStyle)uint.Parse(rows[0]["body"].ToString());
                t.CharName = rows[0]["name"].ToString();
                t.NickName = rows[0]["nickname"].ToString();
                t.LoginMap = ushort.Parse(rows[0]["location_map"].ToString());
                t.CurX = ushort.Parse(rows[0]["location_x"].ToString());
                t.CurY = ushort.Parse(rows[0]["location_y"].ToString());
                t.HairColor = ushort.Parse(rows[0]["haircolor"].ToString());
                t.SkinColor = ushort.Parse(rows[0]["skincolor"].ToString());
                t.ClothingColor = ushort.Parse(rows[0]["clothingcolor"].ToString());
                t.EyeColor = ushort.Parse(rows[0]["eyecolor"].ToString());
                t.SetGold(int.Parse(rows[0]["gold"].ToString()));
                t.Element = (Affinity)byte.Parse(rows[0]["element"].ToString());
                t.Job = (RebornJob)byte.Parse(rows[0]["job"].ToString());
            }
            else
            {
                DebugSystem.Write("Character not found for " + charID);
                return false;
            }
            //load stat data
            src = GetDataTable("SELECT * FROM stats where charID = '" + charID + "'");

            if (src.Rows.Count > 0)
            {
                rows = new DataRow[src.Rows.Count];
                src.Rows.CopyTo(rows, 0);
                foreach (var row in rows)
                {
                    switch (long.Parse(row["statID"].ToString()))
                    {
                        case 25: t.CurHP = int.Parse(row["StatusUp"].ToString()); break;
                        case 26: t.CurSP = ushort.Parse(row["StatusUp"].ToString()); break;
                        case 38: t.SkillPoints = ushort.Parse(row["StatusUp"].ToString()); break;
                        case 36: t.TotalExp = long.Parse(row["StatusUp"].ToString()); break;
                        case 28: t.baseStr = ushort.Parse(row["StatusUp"].ToString()); break;
                        case 29: t.baseCon = ushort.Parse(row["StatusUp"].ToString()); break;
                        case 30: t.baseAgi = ushort.Parse(row["StatusUp"].ToString()); break;
                        case 27: t.baseInt = ushort.Parse(row["StatusUp"].ToString()); break;
                        case 33: t.baseWis = ushort.Parse(row["StatusUp"].ToString()); break;
                    }
                }
            }

            //load equips
            src = GetDataTable("SELECT * FROM inventory where charID = '" + charID + "' AND storID =1");

            if (src.Rows.Count > 0)
            {
                rows = new DataRow[src.Rows.Count];
                src.Rows.CopyTo(rows, 0);
                ushort id;

                for (int i = 0; i < rows.Length; i++)
                {
                    id = ushort.Parse(rows[i]["itemID"].ToString());
                    if (id != 0)
                    {
                        var baseItem = ItemDat?.GetItemByID(id);
                        if (baseItem == null || baseItem.ItemID == 0)
                        {
                            baseItem = new DataFiles.PhxItemInfo() { ItemID = id, ItemName = Encoding.ASCII.GetBytes("Item " + id) };
                        }
                        byte pos = byte.Parse(rows[i]["pos"].ToString());
                        if (pos >= 1 && pos <= 6)
                        {
                            t[pos].CopyFrom(baseItem);
                            t[pos].Ammt = 1;
                            t[pos].Damage = byte.Parse(rows[i]["dmg"].ToString());
                        }
                    }
                }
            }
            else
            {
                t.SetBeginnerOutfit();
            }

            #region load storage (Props Keeper vault)
            try
            {
                src = GetDataTable("SELECT * FROM inventory where charID = '" + charID + "' AND storID = 2");
                if (src != null && src.Rows.Count > 0)
                {
                    rows = new DataRow[src.Rows.Count];
                    src.Rows.CopyTo(rows, 0);
                    for (int i = 0; i < rows.Length; i++)
                    {
                        ushort id = ushort.Parse(rows[i]["itemID"].ToString());
                        if (id != 0)
                        {
                            var baseItem = ItemDat?.GetItemByID(id);
                            if (baseItem == null || baseItem.ItemID == 0)
                            {
                                baseItem = new DataFiles.PhxItemInfo() { ItemID = id, ItemName = Encoding.ASCII.GetBytes("Item " + id) };
                            }
                            byte pos = byte.Parse(rows[i]["pos"].ToString());
                            byte qty = byte.Parse(rows[i]["qty"].ToString());
                            byte dmg = byte.Parse(rows[i]["dmg"].ToString());
                            if (pos >= 1 && pos <= 50 && t.Storage != null)
                            {
                                t.Storage[pos].CopyFrom(baseItem);
                                t.Storage[pos].Ammt = qty;
                                t.Storage[pos].Damage = dmg;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[CharacterDataBase] Error loading storage for charID {charID}: {ex.Message}");
            }
            #endregion

            #region load settings from ExtData
            try
            {
                src = GetDataTable(string.Format("SELECT Settings FROM charactersExtData where charID = '{0}'", charID));
                if (src.Rows.Count > 0 && src.Rows[0]["Settings"] != DBNull.Value)
                {
                    string settingsStr = src.Rows[0]["Settings"].ToString();
                    if (!string.IsNullOrEmpty(settingsStr))
                    {
                        t.Settings.Load(settingsStr);
                        DebugSystem.Write($"[DEBUG] Loaded settings for {t.CharName}: PKABLE={t.Settings.PKABLE}");
                    }
                }
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[DEBUG] Error loading settings: {ex.Message}");
            }
            #endregion

            return true;
        }

        public bool WriteNewPlayer(uint charID, Player player)
        {
            if (charID == 0) return false;

            // Wipe any residual stale data for this charID (e.g. from previously deleted characters)
            try
            {
                ExecuteNonQuery("DELETE FROM character_pets WHERE charID = '" + charID + "';");
                ExecuteNonQuery("DELETE FROM charquest WHERE charID = '" + charID + "';");
                ExecuteNonQuery("DELETE FROM chartent WHERE charID = '" + charID + "';");
                ExecuteNonQuery("DELETE FROM charunlocks WHERE charID = '" + charID + "';");
                ExecuteNonQuery("DELETE FROM inventory WHERE charID = '" + charID + "';");
                ExecuteNonQuery("DELETE FROM stats WHERE charID = '" + charID + "';");
                ExecuteNonQuery("DELETE FROM charactersExtData WHERE charID = '" + charID + "';");
                ExecuteNonQuery("DELETE FROM Friends WHERE charID = '" + charID + "' OR friendID = '" + charID + "';");
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[CharacterDataBase] Error wiping stale data for new charID {charID}: {ex.Message}");
            }

            DataTable src = null;
            DataRow[] rows = new DataRow[0];

            #region write character Data

            Character c = (Character)player;
            Dictionary<string, object> insert = new Dictionary<string, object>();
            insert.Add("charID", player.CharID);
            insert.Add("slot", player.Slot);
            insert.Add("head", c.Head.ToString());
            insert.Add("body", ((byte)c.Body));
            insert.Add("name", c.CharName);
            insert.Add("name_clean", c.CharName.ToLower());
            insert.Add("nickname", c.NickName);
            insert.Add("location_map", c.LoginMap);
            insert.Add("location_x", c.CurX);
            insert.Add("location_y", c.CurY);
            insert.Add("haircolor", c.HairColor);
            insert.Add("skincolor", c.SkinColor);
            insert.Add("clothingcolor", c.ClothingColor);
            insert.Add("eyecolor", c.EyeColor);
            insert.Add("gold", c.Gold);
            insert.Add("element", ((byte)c.Element));
            insert.Add("rebirth", BitConverter.GetBytes(c.Reborn)[0]);
            insert.Add("job", ((byte)c.Job));//fix
            insert.Add("online", BitConverter.GetBytes(false)[0]);

            try { Insert("characters", insert); }
            catch (MySqlException ex) { DebugSystem.Write(new ExceptionData(ex)); return false; }

            #endregion

            #region Write Extr Data

            try { src = GetDataTable(string.Format("SELECT * FROM {0} where {1}", "charactersExtData", "charID = '" + charID + "'")); }
            catch (MySqlException ex) { DebugSystem.Write(new ExceptionData(ex)); return false; }

            Dictionary<string, object> cols = new Dictionary<string, object>();
            cols.Add("charID", charID.ToString());
            cols.Add("Settings", player.Settings.ToString());
            cols.Add("Friends", player.GetFriends_Flag);
            cols.Add("Guild", "0");
            cols.Add("Mail", /*player.GetMailboxFlags()*/"");

            if (src.Rows.Count == 0)
            {
                try { Insert("charactersExtData", cols); }
                catch (MySqlException ex) { DebugSystem.Write(new ExceptionData(ex)); throw; }
            }
            else
            {
                try { Update("charactersExtData", cols, "charID = '" + charID + "';"); }
                catch (MySqlException ex) { DebugSystem.Write(new ExceptionData(ex)); throw; }
            }
            #endregion

            #region write stats

            foreach (var u in player.Stat_toSave)
            {
                try { src = GetDataTable(string.Format("SELECT * FROM {0} where {1}", "stats", "charID = '" + charID + "' AND statID = '" + u[0] + "'")); }
                catch (MySqlException ex) { DebugSystem.Write(new ExceptionData(ex)); }

                cols = new Dictionary<string, object>();
                cols.Add("statIdx", "0");
                cols.Add("charID", charID.ToString());
                cols.Add("statID", u[0].ToString());
                cols.Add("StatusUp", u[1].ToString());
                cols.Add("potential", player.Potential.ToString());



                if (src.Rows.Count == 0)
                {
                    try
                    {
                        Insert("stats", cols);
                    }
                    catch (MySqlException ex) { DebugSystem.Write(new ExceptionData(ex)); throw; }
                }
                else
                {
                    try { Update("stats", cols, "charID = '" + charID + "'AND statID = '" + u[0] + "';"); }
                    catch (MySqlException ex) { DebugSystem.Write(new ExceptionData(ex)); throw; }
                }
            }
            #endregion

            #region predelete inv
            try { ExecuteNonQuery("DELETE FROM inventory where charID ='" + charID + "';"); }
            catch (MySqlException ex) { DebugSystem.Write(new ExceptionData(ex)); throw; }
            #endregion

            #region write inv
            if (player.Inv.InventoryDBData != null)
            {
                string t = "";
                foreach (var u in player.Inv.InventoryDBData)
                {
                    t += string.Format("('{0}','{1}','0','{2}','{3}','{4}','{5}','{6}','{7}','{8}','{9}'),",
                         u.Key, charID, u.Value[0], u.Value[1], u.Value[2], u.Value[3], u.Value[4], u.Value[5], u.Value[6], u.Value[7]);
                }
                t = t.Substring(0, t.Length - 1);
                t += ";";
                ExecuteNonQuery(string.Format("INSERT INTO inventory (invIdx,charID,storID,itemID,dmg,qty,pos,socketID,bombID,sewID,forge) VALUES {0}", t));

            }
            #endregion

            #region write eqs

            if (player.EqData != null)
            {
                string t = "";
                foreach (var u in player.EqData)
                {
                    t += string.Format("('{0}','{1}','1','{2}','{3}','{4}','{5}','{6}','{7}','{8}','{9}'),",
                            u.Key, charID, u.Value[0], u.Value[1], u.Value[2], u.Value[3], u.Value[4], u.Value[5], u.Value[6], u.Value[7]);
                }
                t = t.Substring(0, t.Length - 1);
                t += ";";
                ExecuteNonQuery(string.Format("INSERT INTO inventory (invIdx,charID,storID,itemID,dmg,qty,pos,socketID,bombID,sewID,forge) VALUES {0}", t));
            }

            if (Cache.ContainsKey((int)charID))
                Cache[(int)charID] = player;
            #endregion

            return true;
        }
        public bool WritePlayer(uint charID, Player player)
        {
            bool rem = false;
            if (charID == 0) return rem;

            if (Cache.ContainsKey((int)charID))
                Cache[(int)charID] = player;

            DataTable src = null;

            #region write character Data
            Character c = player;
            Dictionary<string, object> insert = new Dictionary<string, object>();
            insert.Add("head", c.Head.ToString());
            insert.Add("body", ((byte)c.Body).ToString());
            insert.Add("nickname", c.NickName);

            // Safer position saving logic
            string mapID, mapX, mapY;

            // Only save PrevMap if in Tent AND PrevMap is valid
            if (c.CurMap != null && c.CurMap.Type == MapType.Tent && player.PrevMap != null && player.PrevMap.DstMap != 0)
            {
                mapID = player.PrevMap.DstMap.ToString();
                mapX = player.PrevMap.DstX_Axis.ToString();
                mapY = player.PrevMap.DstY_Axis.ToString();
            }
            else
            {
                // Otherwise always save current map position
                mapID = (c.CurMap != null) ? c.CurMap.MapID.ToString() : "0";
                mapX = c.CurX.ToString();
                mapY = c.CurY.ToString();
            }

            insert.Add("location_map", mapID);
            insert.Add("location_x", mapX);
            insert.Add("location_y", mapY);
            insert.Add("haircolor", c.HairColor.ToString());
            insert.Add("skincolor", c.SkinColor.ToString());
            insert.Add("clothingcolor", c.ClothingColor.ToString());
            insert.Add("eyecolor", c.EyeColor.ToString());
            insert.Add("gold", c.Gold.ToString());
            insert.Add("element", ((byte)c.Element).ToString());
            insert.Add("rebirth", BitConverter.GetBytes(c.Reborn)[0].ToString());
            insert.Add("job", ((byte)c.Job).ToString());//fix
            insert.Add("online", BitConverter.GetBytes(false)[0].ToString());

            Update("characters", insert, "charID = '" + charID + "'");

            #endregion

            #region write stats

            foreach (var u in player.Stat_toSave)
            {
                src = GetDataTable(string.Format("SELECT * FROM {0} where {1}", "stats", "charID = '" + charID + "' AND statID = '" + u[0] + "'"));

                if (src.Rows.Count == 0)
                {
                    ExecuteNonQuery(string.Format("INSERT INTO stats {0}", string.Format("(statIdx,charID,statID,StatusUp,potential) VALUES('0','{0}','{1}','{2}','{3}');", charID, u[0], u[1], player.Potential)));
                }
                else
                {
                    ExecuteNonQuery(string.Format("UPDATE stats SET {0} where charID = '" + charID + "' AND statID = '" + u[0] + "'", string.Format(" statID = '{0}',StatusUp ='{1}',potential ='{2}'", u[0], u[1], player.Potential)));
                }
            }
            #endregion

            #region write inv
            try
            {
                ExecuteNonQuery("DELETE FROM inventory WHERE charID = '" + charID + "' AND storID = '0';");
                if (player.Inv.InventoryDBData != null)
                {
                    List<string> invRows = new List<string>();
                    foreach (var u in player.Inv.InventoryDBData)
                    {
                        if (u.Value[0] > 0) // itemID > 0
                        {
                            invRows.Add(string.Format("('{0}','{1}','0','{2}','{3}','{4}','{5}','{6}','{7}','{8}','{9}')",
                                 u.Key, charID, u.Value[0], u.Value[1], u.Value[2], u.Value[3], u.Value[4], u.Value[5], u.Value[6], u.Value[7]));
                        }
                    }
                    if (invRows.Count > 0)
                    {
                        ExecuteNonQuery(string.Format("INSERT INTO inventory (invIdx,charID,storID,itemID,dmg,qty,pos,socketID,bombID,sewID,forge) VALUES {0};", string.Join(",", invRows)));
                    }
                }
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[CharacterDataBase] Error saving inventory for charID {charID}: {ex.Message}");
            }
            #endregion

            #region write eqs
            try
            {
                ExecuteNonQuery("DELETE FROM inventory WHERE charID = '" + charID + "' AND storID = '1';");
                if (player.EqData != null)
                {
                    List<string> eqRows = new List<string>();
                    foreach (var u in player.EqData)
                    {
                        if (u.Value[0] > 0) // itemID > 0
                        {
                            eqRows.Add(string.Format("('{0}','{1}','1','{2}','{3}','{4}','{5}','{6}','{7}','{8}','{9}')",
                                 u.Key, charID, u.Value[0], u.Value[1], u.Value[2], u.Value[3], u.Value[4], u.Value[5], u.Value[6], u.Value[7]));
                        }
                    }
                    if (eqRows.Count > 0)
                    {
                        ExecuteNonQuery(string.Format("INSERT INTO inventory (invIdx,charID,storID,itemID,dmg,qty,pos,socketID,bombID,sewID,forge) VALUES {0};", string.Join(",", eqRows)));
                    }
                }
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[CharacterDataBase] Error saving equips for charID {charID}: {ex.Message}");
            }

            #region write storage (Props Keeper vault)
            try
            {
                ExecuteNonQuery("DELETE FROM inventory WHERE charID = '" + charID + "' AND storID = '2';");
                if (player.Storage != null && player.Storage.InventoryDBData != null)
                {
                    List<string> storRows = new List<string>();
                    foreach (var u in player.Storage.InventoryDBData)
                    {
                        if (u.Value[0] > 0) // itemID > 0
                        {
                            storRows.Add(string.Format("('{0}','{1}','2','{2}','{3}','{4}','{5}','{6}','{7}','{8}','{9}')",
                                 u.Key, charID, u.Value[0], u.Value[1], u.Value[2], u.Value[3], u.Value[4], u.Value[5], u.Value[6], u.Value[7]));
                        }
                    }
                    if (storRows.Count > 0)
                    {
                        ExecuteNonQuery(string.Format("INSERT INTO inventory (invIdx,charID,storID,itemID,dmg,qty,pos,socketID,bombID,sewID,forge) VALUES {0};", string.Join(",", storRows)));
                    }
                }
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[CharacterDataBase] Error saving storage for charID {charID}: {ex.Message}");
            }
            #endregion

            #endregion

            #region write ext data
            ExecuteNonQuery(string.Format("UPDATE charactersExtData SET {0} where charID = '" + charID + "';", string.Format(" Settings = '{0}', Friends = '{1}', Guild = '{2}', Mail = '{3}'", player.Settings.ToString(), player.GetFriends_Flag, "0", "")));
            #endregion

            #region write pets
            try
            {
                ExecuteNonQuery("CREATE TABLE IF NOT EXISTS character_pets (id INTEGER PRIMARY KEY AUTOINCREMENT, charID INT NOT NULL, slot TINYINT NOT NULL, petID INT NOT NULL, petName TEXT, level TINYINT DEFAULT 1, hp INT DEFAULT 250, maxHp INT DEFAULT 250, sp INT DEFAULT 100, maxSp INT DEFAULT 100, amity TINYINT DEFAULT 60, isBattle TINYINT DEFAULT 1, isRide TINYINT DEFAULT 0, isHotel TINYINT DEFAULT 0);");
                try { ExecuteNonQuery("ALTER TABLE character_pets ADD COLUMN isHotel TINYINT DEFAULT 0;"); } catch { }
                ExecuteNonQuery("DELETE FROM character_pets WHERE charID = '" + charID + "';");
                List<string> petRows = new List<string>();

                // 1. Active Player Pets (isHotel = 0)
                if (player.PlayerPets != null && player.PlayerPets.Count > 0)
                {
                    foreach (var pet in player.PlayerPets.Values)
                    {
                        if (pet.PetID > 0)
                        {
                            petRows.Add(string.Format("('{0}','{1}','{2}','{3}','{4}','{5}','{6}','{7}','{8}','{9}','{10}','{11}','0')",
                                charID, pet.Slot, pet.PetID, (pet.PetName ?? "").Replace("'", "''"), pet.Level, pet.HP, pet.MaxHP, pet.SP, pet.MaxSP, pet.Amity, pet.IsBattle ? 1 : 0, pet.IsRide ? 1 : 0));
                        }
                    }
                }

                // 2. Pet Hotel Pets (isHotel = 1)
                if (player.HotelPets != null && player.HotelPets.Count > 0)
                {
                    foreach (var pet in player.HotelPets.Values)
                    {
                        if (pet.PetID > 0)
                        {
                            petRows.Add(string.Format("('{0}','{1}','{2}','{3}','{4}','{5}','{6}','{7}','{8}','{9}','{10}','{11}','1')",
                                charID, pet.Slot, pet.PetID, (pet.PetName ?? "").Replace("'", "''"), pet.Level, pet.HP, pet.MaxHP, pet.SP, pet.MaxSP, pet.Amity, 0, 0));
                        }
                    }
                }

                if (petRows.Count > 0)
                {
                    ExecuteNonQuery(string.Format("INSERT INTO character_pets (charID,slot,petID,petName,level,hp,maxHp,sp,maxSp,amity,isBattle,isRide,isHotel) VALUES {0};", string.Join(",", petRows)));
                }
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[CharacterDataBase] Error saving pets for charID {charID}: {ex.Message}");
            }
            #endregion

            #region write skills
            try
            {
                ExecuteNonQuery("CREATE TABLE IF NOT EXISTS character_skills (id INTEGER PRIMARY KEY AUTOINCREMENT, charID INT NOT NULL, skillID INT NOT NULL, grade TINYINT DEFAULT 1, exp INT DEFAULT 0, UNIQUE(charID, skillID));");
                ExecuteNonQuery("DELETE FROM character_skills WHERE charID = '" + charID + "';");
                if (player.PlayerSkills != null && player.PlayerSkills.Count > 0)
                {
                    List<string> skillRows = new List<string>();
                    foreach (var sk in player.PlayerSkills)
                    {
                        if (sk.SkillID > 0)
                        {
                            skillRows.Add(string.Format("('{0}','{1}','{2}','{3}')",
                                charID, sk.SkillID, sk.Grade, sk.Exp));
                        }
                    }
                    if (skillRows.Count > 0)
                    {
                        ExecuteNonQuery(string.Format("INSERT INTO character_skills (charID,skillID,grade,exp) VALUES {0};", string.Join(",", skillRows)));
                    }
                }
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[CharacterDataBase] Error saving skills for charID {charID}: {ex.Message}");
            }
            #endregion

            return true;
        }

        public void SendOnlineCharacters(Player src)
        {
            PacketBuilder tmp = new PacketBuilder();
            tmp.Begin(null);

            SendPacket p;
            foreach (var y in (from c in Assembly.GetExecutingAssembly().GetTypes()
                               where c.IsClass && c.IsSubclassOf(typeof(GmBot))
                               select c))
            {
                var c = (Activator.CreateInstance(y) as GmBot);
                p = new SendPacket();
                p.Pack8(4);
                p.Pack32(c.CharID);
                p.Pack8((byte)c.Body); //body style
                p.Pack8((byte)c.Element); //element
                p.Pack8(c.Level); //level
                p.Pack16(10019); //map id
                p.Pack16(722); //x
                p.Pack16(995); //y
                p.Pack8(0);
                p.Pack16(c.Head);
                p.Pack16(c.HairColor);
                p.Pack16(c.SkinColor);
                p.Pack16(c.ClothingColor);
                p.Pack16(c.EyeColor);
                p.Pack8(c.WornCount);//clothesAmmt); // ammt of clothes
                p.PackArray(c.Worn_Equips);
                p.Pack32(0); p.Pack8(0); //??
                p.PackBool(c.Reborn); //is rebirth
                p.Pack8((byte)c.Job); //rb class
                p.PackString(c.CharName);//(BYTE*)c.CharacterName,c.nameLen); //CharacterName
                p.PackString(c.NickName);//(BYTE*)c.nick,c.nickLen); //nickname
                p.Pack8(255); //??
                tmp.Add(p);
            }

            int playerCount = 0;
            foreach (var c in Characters_Online.Values)
            {
                // Skip sending the player to themselves
                if (c.CharID == src.CharID)
                {
                    DebugSystem.Write($"[SendOnlineCharacters] Skipping self (CharID={c.CharID})");
                    continue;
                }

                // Skip if player doesn't have CurMap set yet
                if (c.CurMap == null)
                {
                    DebugSystem.Write($"[SendOnlineCharacters] Skipping player {c.CharName} (CharID={c.CharID}) - CurMap is null");
                    continue;
                }

                try
                {
                    DebugSystem.Write($"[SendOnlineCharacters] Adding player: CharID={c.CharID}, Name={c.CharName}, Map={c.CurMap.MapID}, Pos=({c.CurX},{c.CurY})");

                    p = new SendPacket();
                    p.Pack8(4);
                    p.Pack32(c.CharID);
                    p.Pack8((byte)c.Body); //body style
                    p.Pack8((byte)c.Element); //element
                    p.Pack8(c.Level); //level
                    p.Pack16((ushort)c.CurMap.MapID); //map id
                    p.Pack16(c.CurX); //x
                    p.Pack16(c.CurY); //y
                    p.Pack8(0); p.Pack8(c.Head); p.Pack8(0);
                    p.Pack16(c.HairColor);
                    p.Pack16(c.SkinColor);
                    p.Pack16(c.ClothingColor);
                    p.Pack16(c.EyeColor);
                    p.Pack8(c.WornCount);//clothesAmmt); // ammt of clothes
                    p.PackArray(c.Worn_Equips);
                    p.Pack32(0); p.Pack8(0); //??
                    p.PackBool(c.Reborn); //is rebirth
                    p.Pack8((byte)c.Job); //rb class
                    p.PackString(c.CharName);//(BYTE*)c.CharacterName,c.nameLen); //CharacterName
                    p.PackString(c.NickName);//(BYTE*)c.nick,c.nickLen); //nickname
                    p.Pack8(255); //??
                    tmp.Add(p);
                    playerCount++;

                    DebugSystem.Write($"[SendOnlineCharacters] Successfully packed player {c.CharName}");
                }
                catch (Exception ex)
                {
                    DebugSystem.Write($"[SendOnlineCharacters] ERROR packing player {c.CharName}: {ex.Message}\n{ex.StackTrace}");
                }
            }

            DebugSystem.Write($"[SendOnlineCharacters] Sent {playerCount} online players to {src.CharName}");
            try
            {
                src.Send(new SendPacket(tmp.End()));
                DebugSystem.Write($"[SendOnlineCharacters] Packet successfully sent to {src.CharName}");
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[SendOnlineCharacters] ERROR sending packet to {src.CharName}: {ex.Message}\n{ex.StackTrace}");
            }
        }

        public void OnCharacterJoin(Player src)
        {
            Characters_Online.TryAdd((int)src.CharID, src);
        }

        public List<Player> GetOnlinePlayers()
        {
            return Characters_Online.Values.OfType<Player>().ToList();
        }

        public void BroadcastNewPlayer(Player newPlayer)
        {
            // Validate player has required data
            if (newPlayer == null || newPlayer.CurMap == null)
            {
                DebugSystem.Write("[CharacterDataBase] Cannot broadcast new player - player or CurMap is null");
                return;
            }

            try
            {
                // Create spawn packet for the new player using PacketBuilder (like SendOnlineCharacters)
                PacketBuilder tmp = new PacketBuilder();
                tmp.Begin(null);

                SendPacket p = new SendPacket();
                p.Pack8(4); // Spawn player packet type
                p.Pack32(newPlayer.CharID);
                p.Pack8((byte)newPlayer.Body);
                p.Pack8((byte)newPlayer.Element);
                p.Pack8(newPlayer.Level);
                p.Pack16((ushort)newPlayer.CurMap.MapID);
                p.Pack16(newPlayer.CurX);
                p.Pack16(newPlayer.CurY);
                p.Pack8(0);
                p.Pack8(newPlayer.Head);
                p.Pack8(0);
                p.Pack16(newPlayer.HairColor);
                p.Pack16(newPlayer.SkinColor);
                p.Pack16(newPlayer.ClothingColor);
                p.Pack16(newPlayer.EyeColor);
                p.Pack8(newPlayer.WornCount);
                p.PackArray(newPlayer.Worn_Equips ?? new byte[0]);
                p.Pack32(0);
                p.Pack8(0);
                p.PackBool(newPlayer.Reborn);
                p.Pack8((byte)newPlayer.Job);
                p.PackString(newPlayer.CharName ?? "");
                p.PackString(newPlayer.NickName ?? "");
                p.Pack8(255);
                tmp.Add(p);

                SendPacket broadcastPacket = new SendPacket(tmp.End());

                // Send to all other online players
                int broadcastCount = 0;
                foreach (var player in Characters_Online.Values.OfType<Player>())
                {
                    if (player.CharID != newPlayer.CharID && !player.isDisconnected())
                    {
                        try
                        {
                            DebugSystem.Write($"[BroadcastNewPlayer] Sending {newPlayer.CharName} to player {player.CharName} (CharID={player.CharID})");
                            player.Send(broadcastPacket);
                            broadcastCount++;
                            DebugSystem.Write($"[BroadcastNewPlayer] Successfully sent to {player.CharName}");
                        }
                        catch (Exception ex)
                        {
                            DebugSystem.Write($"[CharacterDataBase] Error broadcasting new player to {player.CharName}: {ex.Message}\n{ex.StackTrace}");
                        }
                    }
                    else
                    {
                        if (player.CharID == newPlayer.CharID)
                            DebugSystem.Write($"[BroadcastNewPlayer] Skipping self (CharID={player.CharID})");
                        else if (player.isDisconnected())
                            DebugSystem.Write($"[BroadcastNewPlayer] Skipping disconnected player {player.CharName}");
                    }
                }

                DebugSystem.Write($"[CharacterDataBase] Broadcasted new player {newPlayer.CharName} to {broadcastCount} other players");
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[CharacterDataBase] Error in BroadcastNewPlayer: {ex.Message}\n{ex.StackTrace}");
            }
        }

        public void OnCharacterLeave(Player src)
        {
            Character s;
            if (Characters_Online.TryRemove((int)src.CharID, out s))
            {
                //Begin Saving Character Data
                WritePlayer(src.CharID, src);



            }
        }
    }
}
