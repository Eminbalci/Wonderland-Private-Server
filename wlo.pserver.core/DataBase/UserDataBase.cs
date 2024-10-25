using Game;
using Game.Code;
using MySql.Data.MySqlClient;
using RCLibrary.Core;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;

namespace DataBase {
    public enum GMStatus {
        None,
    }



    public sealed class UserDataBase : RCLibrary.Core.DataBase {
        readonly object mylock = new object();

        //Used to provide flexibility to alter columns name and match them with the correct value
        public string TableName = "user";
        public string Username_Ref = "username";
        public string Password_Ref = "password";
        public string DataBaseID_Ref = "userID";
        public string CharacterID1_Ref = "character1ID";
        public string CharacterID2_Ref = "character2ID";
        public string IM_Ref = "IM";
        public string Char_Delete_Code_Ref = "char_delete_code";
        public VerifyPassType PassVerification;


        List<User> GOnlineUsers;

        bool shutdown = false;

        public UserDataBase() {

        }

        public void VerifySetup() {

            #region characters Columns
            Dictionary<string, string> col = new Dictionary<string, string>();
            col.Add("userID", "int/NN/PK/AI");
            col.Add("username", "text/NN");
            col.Add("password", "text/NN");
            col.Add("email", "text/NN");


            #region characters table Verification
            DebugSystem.Write("Checking for users table");
        retry:

            if (GetDataTable("SELECT * FROM users") != null) goto exist;

            DebugSystem.Write("Setuping up users table");

            string nonsqlite_prikey = "";
            string cmstr = "create table users (";

            foreach (var t in col) {
                var str = "";
                var att = t.Value.Split('/');

                switch (ServType) {
                    #region Mysql
                    case RCLibrary.Core.DataBaseTypes.MySQl: {
                            foreach (var a in att)
                                switch (a) {
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
                    case RCLibrary.Core.DataBaseTypes.Sqlite: {
                            if (att.Count(c => c == "pk") > 0 && att.Count(c => c == "NN") > 0)
                                att = att.Where(c => c != "NN").ToArray();

                            foreach (var a in att)
                                switch (a) {
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
            DebugSystem.Write("Found users table");
            DebugSystem.Write("Verifying users columns");
            //table exists verify columns  
            //table exists verify columns  
            foreach (string h in col.Keys) {
                if (GetDataTable("select " + h + " from users") == null) {
                    DebugSystem.Write("Recreating users table");

                    ExecuteNonQuery("drop table if exists users");
                    goto retry;
                }
            }
            #endregion
            #endregion
        }

        public int Count() { return GOnlineUsers.Count; }

        public bool isLoggedin(string user) {
            bool resp = (GOnlineUsers.Count(c => c.UserName == user) > 0);
            DebugSystem.Write(DebugItemType.Info_Heavy, "Checking if User '{0}' is Online... [Resp]: {1}", DebugItemType.Info_Heavy, user, resp);
            return resp;
        }

        public override bool VerifyPassword(string check, string with) {
            return base.VerifyPassword(check, with);
        }

        public override bool VerifySaltedPassword(string password, string salt, string with) {
            return (hashMD5(hashMD5(salt) + hashMD5(password)) == with);
        }

        public bool Update_Player_ID(uint user, UInt32 id, byte slot) {
            if (user == 0) return false;

            string col = "";
            switch (slot) {
                case 1: { col = CharacterID1_Ref; } break;
                case 2: { col = CharacterID2_Ref; } break;
            }

            Dictionary<string, string> cols = new Dictionary<string, string>();
            cols.Add(col, id.ToString());

            //try { Update(TableName, cols, UserID_Ref + " = '" + user + "'"); }
            //catch (MySqlException ex) { DebugSystem.Write(ex); return false; }

            return true;
        }

        public bool GetUserData(string user, string pass, out uint userID, out string[] userData) {
            DataRow[] rows = new DataRow[0];

            var src = GetDataTable("SELECT * FROM " + TableName + " WHERE " + Username_Ref + " = @id", new DbParam("@id", user));

            if (src.Rows.Count > 0) {
                rows = new DataRow[src.Rows.Count];
                src.Rows.CopyTo(rows, 0);

                switch (PassVerification) {
                    case VerifyPassType.None:
                        if (VerifyPassword(pass, rows[0][Password_Ref].ToString())) {
                            string ch = "";
                            if (rows[0][Char_Delete_Code_Ref] != DBNull.Value)
                                ch = rows[0][Char_Delete_Code_Ref].ToString();
                            uint.TryParse(rows[0][DataBaseID_Ref].ToString(), out userID);
                            userData = new string[] { rows[0][Username_Ref].ToString(), ch, (rows[0][IM_Ref].ToString() == "") ? "0" : rows[0][IM_Ref].ToString() };
                            return true;
                        }
                        break;
                    //if (BCrypt.Net.BCrypt.Verify(pass, rows[0][Password_Ref].ToString()))
                    //{
                    //    string ch = "0";
                    //    if (rows[0][Char_Delete_Code_Ref] != DBNull.Value)
                    //        ch = rows[0][Char_Delete_Code_Ref].ToString();

                    //    return new string[] { rows[0][Username_Ref].ToString(), ch, (rows[0][IM_Ref].ToString() == "")?"0":rows[0][IM_Ref].ToString()};
                    //}
                    case VerifyPassType.IPBoard_3x:
                        if (VerifySaltedPassword(pass, rows[0]["members_pass_salt"].ToString(), rows[0][Password_Ref].ToString())) {
                            string ch = "";
                            if (rows[0][Char_Delete_Code_Ref] != DBNull.Value)
                                ch = rows[0][Char_Delete_Code_Ref].ToString();
                            uint.TryParse(rows[0][DataBaseID_Ref].ToString(), out userID);
                            userData = new string[] { rows[0][Username_Ref].ToString(), ch, (rows[0][IM_Ref].ToString() == "") ? "0" : rows[0][IM_Ref].ToString() };
                            return true;
                        }
                        break;
                }
            }
            userID = 0;
            userData = null;
            return false;
        }

        public int GetIMPoints(uint user) {
            DataTable src = null;
            DataRow[] rows = new DataRow[0];

            try {
                src = GetDataTable("SELECT * FROM " + TableName + " where " + DataBaseID_Ref + " = '" + user + "'"
                    );
            } catch (MySqlException ex) { DebugSystem.Write(new ExceptionData(ex)); return 0; }

            if (src.Rows.Count > 0) {
                rows = new DataRow[src.Rows.Count];
                src.Rows.CopyTo(rows, 0);
                return int.Parse(rows[0][IM_Ref].ToString());
            }
            return 0;
        }

        public bool UpdateUser(uint user, string delete = null, object im = null, object char1 = null, object char2 = null) {
            if (user == 0) return false;

            Dictionary<string, string> str = new Dictionary<string, string>();
            if (delete != null) str.Add(Char_Delete_Code_Ref, delete);
            if (im != null) str.Add(IM_Ref, im.ToString());
            if (char1 != null) str.Add(CharacterID1_Ref, char1.ToString());
            if (char2 != null) str.Add(CharacterID2_Ref, char2.ToString());

            //try { Update(TableName, str, UserID_Ref + " = '" + user + "'"); }
            //catch (MySqlException ex) { DebugSystem.Write(new ExceptionData(ex)); return false; }

            return true;
        }

        public bool OnLogin(User usr) {
            return true;
        }
        public void OnLogOff(User usr) {
        }
    }


}
