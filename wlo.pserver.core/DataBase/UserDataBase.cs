using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using Game;
using Game.Code;
using MySql.Data.MySqlClient;
using RCLibrary.Core;

namespace DataBase
{
    public enum GMStatus
    {
        None,
    }



    public sealed class UserDataBase : RCLibrary.Core.DataBase
    {
        readonly object mylock = new object();

        //Used to provide flexibility to alter columns name and match them with the correct value
        public string TableName = "users";
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

        public UserDataBase()
        {

        }

        public void VerifySetup()
        {

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
            DebugSystem.Write("Found users table");
            DebugSystem.Write("Verifying users columns");
            //table exists verify columns  
            //table exists verify columns  
            foreach (string h in col.Keys)
            {
                if (GetDataTable("select " + h + " from users") == null)
                {
                    DebugSystem.Write("Recreating users table");

                    ExecuteNonQuery("drop table if exists users");
                    goto retry;
                }
            }
            #endregion
            #endregion
        }

        public int Count() { return GOnlineUsers.Count; }

        public bool isLoggedin(string user)
        {
            bool resp = (GOnlineUsers.Count(c => c.UserName == user) > 0);
            DebugSystem.Write(DebugItemType.Info_Heavy, "Checking if User '{0}' is Online... [Resp]: {1}", DebugItemType.Info_Heavy, user, resp);
            return resp;
        }

        public override bool VerifyPassword(string check, string with)
        {
            return base.VerifyPassword(check, with);
        }

        public override bool VerifySaltedPassword(string password, string salt, string with)
        {
            return (hashMD5(hashMD5(salt) + hashMD5(password)) == with);
        }

        public bool Update_Player_ID(uint user, UInt32 id, byte slot)
        {
            if (user == 0) return false;

            string col = "";
            switch (slot)
            {
                case 1: { col = CharacterID1_Ref; } break;
                case 2: { col = CharacterID2_Ref; } break;
            }

            Dictionary<string, string> cols = new Dictionary<string, string>();
            cols.Add(col, id.ToString());

            //try { Update(TableName, cols, UserID_Ref + " = '" + user + "'"); }
            //catch (MySqlException ex) { DebugSystem.Write(ex); return false; }

            return true;
        }

        public bool GetUserData(string user, string pass, out uint userID, out string[] userData)
        {
            DebugSystem.Write($"[UserDataBase.GetUserData] Called for user: '{user}'");
            DebugSystem.Write($"[UserDataBase.GetUserData] TableName={TableName}, Username_Ref={Username_Ref}");

            DataRow[] rows = new DataRow[0];

            try
            {
                var src = GetDataTable("SELECT * FROM " + TableName + " WHERE " + Username_Ref + " = @id", new DbParam("@id", user));
                DebugSystem.Write($"[UserDataBase.GetUserData] Query executed. Rows found: {src?.Rows.Count ?? -1}");

                if (src.Rows.Count > 0)
                {
                    rows = new DataRow[src.Rows.Count];
                    src.Rows.CopyTo(rows, 0);

                    DebugSystem.Write($"[UserDataBase.GetUserData] PassVerification type: {PassVerification}");
                    switch (PassVerification)
                    {
                        case VerifyPassType.None:
                            DebugSystem.Write("[UserDataBase.GetUserData] Using VerifyPassType.None");
                            if (VerifyPassword(pass, rows[0][Password_Ref].ToString()))
                            {
                                DebugSystem.Write("[UserDataBase.GetUserData] Password verified successfully!");
                                string ch = "";
                                // Check if char_delete_code column exists before accessing it
                                if (src.Columns.Contains(Char_Delete_Code_Ref) && rows[0][Char_Delete_Code_Ref] != DBNull.Value)
                                    ch = rows[0][Char_Delete_Code_Ref].ToString();
                                uint.TryParse(rows[0][DataBaseID_Ref].ToString(), out userID);

                                // Check if IM column exists before accessing it
                                string imValue = "0";
                                if (src.Columns.Contains(IM_Ref) && !string.IsNullOrEmpty(rows[0][IM_Ref].ToString()))
                                    imValue = rows[0][IM_Ref].ToString();

                                userData = new string[] { rows[0][Username_Ref].ToString(), ch, imValue };
                                DebugSystem.Write($"[UserDataBase.GetUserData] Returning TRUE with userID={userID}");
                                return true;
                            }
                            else
                            {
                                DebugSystem.Write("[UserDataBase.GetUserData] Password verification FAILED");
                            }
                            break;
                        case VerifyPassType.IPBoard_3x:
                            DebugSystem.Write("[UserDataBase.GetUserData] Using VerifyPassType.IPBoard_3x");
                            if (VerifySaltedPassword(pass, rows[0]["members_pass_salt"].ToString(), rows[0][Password_Ref].ToString()))
                            {
                                string ch = "";
                                // Check if char_delete_code column exists before accessing it
                                if (src.Columns.Contains(Char_Delete_Code_Ref) && rows[0][Char_Delete_Code_Ref] != DBNull.Value)
                                    ch = rows[0][Char_Delete_Code_Ref].ToString();
                                uint.TryParse(rows[0][DataBaseID_Ref].ToString(), out userID);

                                // Check if IM column exists before accessing it
                                string imValue = "0";
                                if (src.Columns.Contains(IM_Ref) && !string.IsNullOrEmpty(rows[0][IM_Ref].ToString()))
                                    imValue = rows[0][IM_Ref].ToString();

                                userData = new string[] { rows[0][Username_Ref].ToString(), ch, imValue };
                                return true;
                            }
                            break;
                    }
                }
                else
                {
                    DebugSystem.Write($"[UserDataBase.GetUserData] No user found with username '{user}'");
                }
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[UserDataBase.GetUserData] EXCEPTION: {ex.Message}\n{ex.StackTrace}");
            }

            userID = 0;
            userData = null;
            DebugSystem.Write("[UserDataBase.GetUserData] Returning FALSE");
            return false;
        }

        public int GetIMPoints(uint user)
        {
            DataTable src = null;
            DataRow[] rows = new DataRow[0];

            try
            {
                src = GetDataTable("SELECT * FROM " + TableName + " where " + DataBaseID_Ref + " = '" + user + "'"
                    );
            }
            catch (MySqlException ex) { DebugSystem.Write(new ExceptionData(ex)); return 0; }

            if (src.Rows.Count > 0)
            {
                rows = new DataRow[src.Rows.Count];
                src.Rows.CopyTo(rows, 0);
                return int.Parse(rows[0][IM_Ref].ToString());
            }
            return 0;
        }

        public bool SetIMPoints(uint userId, int points)
        {
            if (userId == 0) return false;
            try
            {
                string query = "UPDATE " + TableName + " SET " + IM_Ref + " = @im WHERE " + DataBaseID_Ref + " = @uid";
                ExecuteNonQuery(query, new DbParam("@im", points), new DbParam("@uid", userId));
                return true;
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[UserDataBase] Error updating IM points: {ex.Message}");
                return false;
            }
        }

        public bool SetIMBonusPoints(uint userId, int bonusPoints)
        {
            if (userId == 0) return false;
            try
            {
                try
                {
                    ExecuteNonQuery("ALTER TABLE " + TableName + " ADD COLUMN im_bonus INTEGER DEFAULT 0");
                }
                catch { }

                string query = "UPDATE " + TableName + " SET im_bonus = @bonus WHERE " + DataBaseID_Ref + " = @uid";
                ExecuteNonQuery(query, new DbParam("@bonus", bonusPoints), new DbParam("@uid", userId));
                return true;
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[UserDataBase] Error updating IM bonus points: {ex.Message}");
                return false;
            }
        }

        public int GetIMBonusPoints(uint user)
        {
            if (user == 0) return 0;
            try
            {
                var dt = GetDataTable("SELECT im_bonus FROM " + TableName + " WHERE " + DataBaseID_Ref + " = '" + user + "'");
                if (dt != null && dt.Rows.Count > 0 && dt.Rows[0]["im_bonus"] != DBNull.Value)
                {
                    return Convert.ToInt32(dt.Rows[0]["im_bonus"]);
                }
            }
            catch { }
            return 0;
        }

        public bool UpdateUser(uint user, string delete = null, object im = null, object char1 = null, object char2 = null)
        {
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

        /// <summary>
        /// Registers a new user in the database
        /// </summary>
        public bool RegisterUser(string username, string password, string email, out string errorMessage)
        {
            errorMessage = "";

            try
            {
                // Check if username already exists
                var existing = GetDataTable("SELECT * FROM users WHERE username = @username",
                    new DbParam("@username", username));

                if (existing != null && existing.Rows.Count > 0)
                {
                    errorMessage = "Username already exists";
                    return false;
                }

                // Insert new user
                string insertQuery = "INSERT INTO users (username, password, email) VALUES (@username, @password, @email)";
                ExecuteNonQuery(insertQuery,
                    new DbParam("@username", username),
                    new DbParam("@password", password),
                    new DbParam("@email", email));

                DebugSystem.Write("New user registered: " + username);
                return true;
            }
            catch (Exception ex)
            {
                DebugSystem.Write(new ExceptionData(ex));
                errorMessage = "Registration failed: " + ex.Message;
                return false;
            }
        }

        /// <summary>
        /// Gets all registered users from the database
        /// </summary>
        public DataTable GetAllUsers()
        {
            try
            {
                // Use IFNULL/COALESCE to handle missing char_delete_code column gracefully
                string query = "SELECT userID, username, password, email";

                // Try to include char_delete_code if it exists
                var testTable = GetDataTable("SHOW COLUMNS FROM users LIKE 'char_delete_code'");
                if (testTable != null && testTable.Rows.Count > 0)
                {
                    query += ", char_delete_code as cipher";
                }

                query += " FROM users ORDER BY userID";

                return GetDataTable(query);
            }
            catch (Exception ex)
            {
                DebugSystem.Write(new ExceptionData(ex));
                return null;
            }
        }

        /// <summary>
        /// Deletes a user by their ID
        /// </summary>
        public bool DeleteUser(int userID)
        {
            try
            {
                ExecuteNonQuery("DELETE FROM users WHERE userID = @id", new DbParam("@id", userID));
                DebugSystem.Write($"User deleted: ID {userID}");
                return true;
            }
            catch (Exception ex)
            {
                DebugSystem.Write(new ExceptionData(ex));
                return false;
            }
        }

        /// <summary>
        /// Updates a user's password
        /// </summary>
        public bool UpdatePassword(int userID, string newPassword)
        {
            try
            {
                ExecuteNonQuery("UPDATE users SET password = @password WHERE userID = @id",
                    new DbParam("@password", newPassword),
                    new DbParam("@id", userID));
                DebugSystem.Write($"Password updated for user ID {userID}");
                return true;
            }
            catch (Exception ex)
            {
                DebugSystem.Write(new ExceptionData(ex));
                return false;
            }
        }

        public bool OnLogin(User usr)
        {
            return true;
        }
        public void OnLogOff(User usr)
        {
        }
    }


}
