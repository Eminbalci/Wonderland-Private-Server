using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Data.Common;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace RCLibrary.Core;

public class DataBase {
    private readonly object mlock = new object();

    private MySqlConnection mysqladptcnn;

    private MySqlDataAdapter mysqldbAdpter;

    protected DataBaseTypes ServType = DataBaseTypes.MySQl;

    protected string DBFile = "";

    protected string User = "wlodbadmin";

    protected string Pass = "0penf1r3";

    protected string DB = "wlo";

    protected string Port = "3306";

    protected string ServerIP = "127.0.0.1";

    private string Connection_String {
        get {
            if (ServType == DataBaseTypes.MySQl) {
                return $"Server = {ServerIP}; Port = {Port}; Database = {DB}; Uid = {User}; Pwd = {Pass};";
            }
            if (ServType == DataBaseTypes.Sqlite) {
                return $"Data Source={DBFile};Version=3;";
            }
            if (ServType == DataBaseTypes.SQl) {
                return $"Server={ServerIP};Database={DB};User Id={User};Password={Pass};";
            }
            return "";
        }
    }

    public DataBase() {
        try {
            string text = "";
            if (!File.Exists("database.override.txt")) {
                return;
            }
            using StreamReader streamReader = new StreamReader("database.override.txt");
            while ((text = streamReader.ReadLine()) != null) {
                switch (text.Split('|')[0]) {
                    case "Type":
                        ServType = (DataBaseTypes)byte.Parse(text.Split('|')[1]);
                        break;
                    case "User":
                        User = text.Split('|')[1];
                        break;
                    case "Pass":
                        Pass = text.Split('|')[1];
                        break;
                    case "DB":
                        DB = text.Split('|')[1];
                        break;
                    case "Port":
                        Port = text.Split('|')[1];
                        break;
                    case "IP":
                        ServerIP = text.Split('|')[1];
                        break;
                    case "File":
                        DBFile = text.Split('|')[1];
                        break;
                }
            }
        } catch {
        }
    }

    ~DataBase() {
        try {
            if (mysqladptcnn != null) {
                ((DbConnection)(object)mysqladptcnn).Close();
            }
        } catch {
        }
    }

    public bool TestConnection() {
        //IL_004a: Unknown result type (might be due to invalid IL or missing references)
        //IL_0051: Expected O, but got Unknown
        switch (ServType) {
            case DataBaseTypes.Sqlite: {
                    SQLiteConnection sQLiteConnection = new SQLiteConnection(Connection_String);
                    try {
                        sQLiteConnection.Open();
                        sQLiteConnection.Close();
                        return true;
                    } catch (Exception) {
                        sQLiteConnection.Close();
                    }
                    break;
                }
            case DataBaseTypes.MySQl: {
                    MySqlConnection val = new MySqlConnection(Connection_String);
                    try {
                        ((DbConnection)(object)val).Open();
                        ((DbConnection)(object)val).Close();
                        return true;
                    } catch (Exception) {
                        ((DbConnection)(object)val).Close();
                    }
                    break;
                }
        }
        return false;
    }

    public void Dispose() {
        if (mysqldbAdpter != null) {
            ((Component)(object)mysqldbAdpter).Dispose();
        }
        if (mysqladptcnn == null) {
            return;
        }
        try {
            if (mysqladptcnn != null) {
                ((DbConnection)(object)mysqladptcnn).Close();
            }
        } catch {
        }
    }

    public bool InitializeMysqlAdapter(string selectquery) {
        //IL_0040: Unknown result type (might be due to invalid IL or missing references)
        //IL_004a: Expected O, but got Unknown
        //IL_0052: Unknown result type (might be due to invalid IL or missing references)
        //IL_005c: Expected O, but got Unknown
        //IL_006e: Unknown result type (might be due to invalid IL or missing references)
        //IL_0074: Expected O, but got Unknown
        try {
            mysqladptcnn = new MySqlConnection($"Server = {ServerIP}; Port = {Port}; Database = {DB}; Uid = {User}; Pwd = {Pass}");
            mysqldbAdpter = new MySqlDataAdapter(selectquery, mysqladptcnn);
            ((DbConnection)(object)mysqladptcnn).Open();
            MySqlCommandBuilder val = new MySqlCommandBuilder(mysqldbAdpter);
            mysqldbAdpter.DeleteCommand = val.GetDeleteCommand();
            mysqldbAdpter.UpdateCommand = val.GetUpdateCommand();
            mysqldbAdpter.InsertCommand = val.GetInsertCommand();
            return true;
        } catch (Exception ex) {
            DebugSystem.Write(new ExceptionData(ex, ExceptionSeverity.Error, "InitializeMysqlAdapter", "C:\\Users\\Rommel JR\\Dropbox\\Wonderland Online Dev Group\\Pserver Core\\CServer\\RCLibrary\\Database\\Database.cs", 205));
        }
        return false;
    }

    public int Refresh(ref DataSet src) {
        try {
            src.Clear();
            return ((DataAdapter)(object)mysqldbAdpter).Fill(src);
        } catch (Exception ex) {
            DebugSystem.Write(new ExceptionData(ex, ExceptionSeverity.Error, "Refresh", "C:\\Users\\Rommel JR\\Dropbox\\Wonderland Online Dev Group\\Pserver Core\\CServer\\RCLibrary\\Database\\Database.cs", 216));
        }
        return 0;
    }

    public int Refresh(ref DataSet src, string table) {
        try {
            src.Clear();
            return ((DbDataAdapter)(object)mysqldbAdpter).Fill(src, table);
        } catch (Exception ex) {
            DebugSystem.Write(new ExceptionData(ex, ExceptionSeverity.Error, "Refresh", "C:\\Users\\Rommel JR\\Dropbox\\Wonderland Online Dev Group\\Pserver Core\\CServer\\RCLibrary\\Database\\Database.cs", 226));
        }
        return 0;
    }

    public async Task<DataSet> RefreshAsync() {
        DataSet t = new DataSet();
        await mysqldbAdpter.FillAsync(t);
        return t;
    }

    public int Update(DataSet src) {
        try {
            return ((DataAdapter)(object)mysqldbAdpter).Update(src);
        } catch (Exception ex) {
            DebugSystem.Write(new ExceptionData(ex, ExceptionSeverity.Error, "Update", "C:\\Users\\Rommel JR\\Dropbox\\Wonderland Online Dev Group\\Pserver Core\\CServer\\RCLibrary\\Database\\Database.cs", 241));
        }
        return 0;
    }

    public int Update(DataTable src) {
        try {
            return ((DbDataAdapter)(object)mysqldbAdpter).Update(src);
        } catch {
        }
        return 0;
    }

    public DataTable GetDataTable(string query, params DbParam[] parameters) {
        //IL_00c4: Unknown result type (might be due to invalid IL or missing references)
        //IL_00cb: Expected O, but got Unknown
        //IL_00d7: Unknown result type (might be due to invalid IL or missing references)
        //IL_00de: Expected O, but got Unknown
        DataTable dataTable = null;
        try {
            switch (ServType) {
                case DataBaseTypes.Sqlite: {
                        SQLiteConnection sQLiteConnection = new SQLiteConnection(Connection_String);
                        try {
                            sQLiteConnection.Open();
                            SQLiteCommand sQLiteCommand = new SQLiteCommand(sQLiteConnection);
                            sQLiteCommand.CommandText = query;
                            for (int j = 0; j < parameters.Length; j++) {
                                DbParam dbParam2 = parameters[j];
                                sQLiteCommand.Parameters.AddWithValue(dbParam2.identifier, dbParam2.value);
                            }
                            SQLiteDataReader sQLiteDataReader = sQLiteCommand.ExecuteReader();
                            dataTable = new DataTable();
                            dataTable.Load(sQLiteDataReader);
                            sQLiteDataReader.Close();
                            sQLiteConnection.Close();
                            return dataTable;
                        } catch {
                            sQLiteConnection.Close();
                        }
                        break;
                    }
                case DataBaseTypes.MySQl: {
                        MySqlConnection val = new MySqlConnection(Connection_String);
                        try {
                            ((DbConnection)(object)val).Open();
                            MySqlCommand val2 = new MySqlCommand(query, val);
                            if (parameters != null) {
                                for (int i = 0; i < parameters.Length; i++) {
                                    DbParam dbParam = parameters[i];
                                    val2.Parameters.AddWithValue(dbParam.identifier, (object)dbParam.value);
                                }
                            }
                            MySqlDataReader val3 = val2.ExecuteReader();
                            dataTable = new DataTable();
                            dataTable.Load((IDataReader)val3);
                            ((DbDataReader)(object)val3).Close();
                            ((DbConnection)(object)val).Close();
                            return dataTable;
                        } catch {
                            ((DbConnection)(object)val).Close();
                        }
                        break;
                    }
            }
        } catch (Exception ex) {
            DebugSystem.Write(new ExceptionData(ex, ExceptionSeverity.Error, "GetDataTable", "C:\\Users\\Rommel JR\\Dropbox\\Wonderland Online Dev Group\\Pserver Core\\CServer\\RCLibrary\\Database\\Database.cs", 319));
            dataTable = new DataTable();
        }
        return dataTable;
    }

    public int ExecuteNonQuery(string sql, params DbParam[] parameters) {
        //IL_00a0: Unknown result type (might be due to invalid IL or missing references)
        //IL_00a7: Expected O, but got Unknown
        //IL_00b2: Unknown result type (might be due to invalid IL or missing references)
        //IL_00b9: Expected O, but got Unknown
        int result = 0;
        switch (ServType) {
            case DataBaseTypes.Sqlite: {
                    SQLiteConnection sQLiteConnection = new SQLiteConnection(Connection_String);
                    sQLiteConnection.Open();
                    SQLiteCommand sQLiteCommand = new SQLiteCommand(sQLiteConnection);
                    sQLiteCommand.CommandText = sql;
                    if (parameters != null) {
                        for (int j = 0; j < parameters.Length; j++) {
                            DbParam dbParam2 = parameters[j];
                            sQLiteCommand.Parameters.AddWithValue(dbParam2.identifier, dbParam2.value);
                        }
                    }
                    result = sQLiteCommand.ExecuteNonQuery();
                    sQLiteConnection.Close();
                    break;
                }
            case DataBaseTypes.MySQl: {
                    MySqlConnection val = new MySqlConnection(Connection_String);
                    ((DbConnection)(object)val).Open();
                    MySqlCommand val2 = new MySqlCommand(sql, val);
                    if (parameters != null) {
                        for (int i = 0; i < parameters.Length; i++) {
                            DbParam dbParam = parameters[i];
                            val2.Parameters.AddWithValue(dbParam.identifier, (object)dbParam.value);
                        }
                    }
                    DebugSystem.Write(DebugItemType.DataBase_Heavy, "Running MYSQL DB Query: " + ((DbCommand)(object)val2).CommandText);
                    result = ((DbCommand)(object)val2).ExecuteNonQuery();
                    ((DbConnection)(object)val).Close();
                    break;
                }
        }
        return result;
    }

    public int ExecuteNonQuery(string sql) {
        //IL_0054: Unknown result type (might be due to invalid IL or missing references)
        //IL_005b: Expected O, but got Unknown
        //IL_0066: Unknown result type (might be due to invalid IL or missing references)
        //IL_006d: Expected O, but got Unknown
        int result = 0;
        switch (ServType) {
            case DataBaseTypes.Sqlite: {
                    SQLiteConnection sQLiteConnection = new SQLiteConnection(Connection_String);
                    sQLiteConnection.Open();
                    SQLiteCommand sQLiteCommand = new SQLiteCommand(sQLiteConnection);
                    sQLiteCommand.CommandText = sql;
                    result = sQLiteCommand.ExecuteNonQuery();
                    sQLiteConnection.Close();
                    break;
                }
            case DataBaseTypes.MySQl: {
                    MySqlConnection val = new MySqlConnection(Connection_String);
                    ((DbConnection)(object)val).Open();
                    MySqlCommand val2 = new MySqlCommand(sql, val);
                    DebugSystem.Write(DebugItemType.DataBase_Heavy, "Running MYSQL DB Query: " + ((DbCommand)(object)val2).CommandText);
                    result = ((DbCommand)(object)val2).ExecuteNonQuery();
                    ((DbConnection)(object)val).Close();
                    break;
                }
        }
        return result;
    }

    public int ExecuteNonQuery(DbCommand command) {
        //IL_004a: Unknown result type (might be due to invalid IL or missing references)
        //IL_0050: Expected O, but got Unknown
        int result = 0;
        switch (ServType) {
            case DataBaseTypes.Sqlite: {
                    SQLiteConnection sQLiteConnection = new SQLiteConnection(Connection_String);
                    sQLiteConnection.Open();
                    command.Connection = sQLiteConnection;
                    result = command.ExecuteNonQuery();
                    sQLiteConnection.Close();
                    break;
                }
            case DataBaseTypes.MySQl: {
                    MySqlConnection val = new MySqlConnection(Connection_String);
                    ((DbConnection)(object)val).Open();
                    command.Connection = (DbConnection)(object)val;
                    DebugSystem.Write(DebugItemType.DataBase_Heavy, "Running MYSQL DB Query: " + command.CommandText);
                    result = command.ExecuteNonQuery();
                    ((DbConnection)(object)val).Close();
                    break;
                }
        }
        return result;
    }

    public string ExecuteScalar(string sql) {
        //IL_0067: Unknown result type (might be due to invalid IL or missing references)
        //IL_006e: Expected O, but got Unknown
        //IL_0079: Unknown result type (might be due to invalid IL or missing references)
        //IL_0080: Expected O, but got Unknown
        switch (ServType) {
            case DataBaseTypes.Sqlite: {
                    SQLiteConnection sQLiteConnection = new SQLiteConnection(DBFile);
                    sQLiteConnection.Open();
                    SQLiteCommand sQLiteCommand = new SQLiteCommand(sQLiteConnection);
                    sQLiteCommand.CommandText = sql;
                    object obj2 = sQLiteCommand.ExecuteScalar();
                    sQLiteConnection.Close();
                    if (obj2 != null) {
                        return obj2.ToString();
                    }
                    break;
                }
            case DataBaseTypes.MySQl: {
                    MySqlConnection val = new MySqlConnection(Connection_String);
                    ((DbConnection)(object)val).Open();
                    MySqlCommand val2 = new MySqlCommand(sql, val);
                    object obj = ((DbCommand)(object)val2).ExecuteScalar();
                    ((DbConnection)(object)val).Close();
                    if (obj != null) {
                        return obj.ToString();
                    }
                    break;
                }
        }
        return "";
    }

    public void Update(string tableName, Dictionary<string, object> data, string where, params DbParam[] parameters) {
        lock (mlock) {
            string text = "";
            if (data.Count >= 1) {
                foreach (KeyValuePair<string, object> datum in data) {
                    try {
                        text += $" {datum.Key.ToString()} = '{datum.Value.ToString()}',";
                    } catch (Exception ex) {
                        DebugSystem.Write(new ExceptionData(ex, ExceptionSeverity.Error, "Update", "C:\\Users\\Rommel JR\\Dropbox\\Wonderland Online Dev Group\\Pserver Core\\CServer\\RCLibrary\\Database\\Database.cs", 495));
                    }
                }
                text = text.Substring(0, text.Length - 1);
            }
            ExecuteNonQuery($"update {tableName} set {text} where {where};", parameters);
        }
    }

    public void Update(string sql, params DbParam[] parameters) {
        lock (mlock) {
            ExecuteNonQuery(sql, parameters);
        }
    }

    public void Delete(string tableName, string where, KeyValuePair<string, string>[] parameters = null) {
        lock (mlock) {
            ExecuteNonQuery(string.Format("delete from {0} where {1};", tableName, where, parameters));
        }
    }

    public void Insert(string tableName, Dictionary<string, object> data) {
        lock (mlock) {
            string text = "";
            string text2 = "";
            foreach (KeyValuePair<string, object> datum in data) {
                text += $" {datum.Key.ToString()},";
                text2 += $" '{datum.Value}',";
            }
            text = text.Substring(0, text.Length - 1);
            text2 = text2.Substring(0, text2.Length - 1);
            ExecuteNonQuery($"insert into {tableName}({text}) values({text2});");
        }
    }

    public void PreparedQuery(string query, List<CMDParameter[]> prepparams) {
        //IL_01b2: Unknown result type (might be due to invalid IL or missing references)
        //IL_01b9: Expected O, but got Unknown
        //IL_01c4: Unknown result type (might be due to invalid IL or missing references)
        //IL_01cb: Expected O, but got Unknown
        //IL_00ac: Unknown result type (might be due to invalid IL or missing references)
        //IL_00b3: Expected O, but got Unknown
        //IL_0225: Unknown result type (might be due to invalid IL or missing references)
        //IL_022c: Expected O, but got Unknown
        lock (mlock) {
            switch (ServType) {
                case DataBaseTypes.Sqlite: {
                        SQLiteConnection sQLiteConnection = new SQLiteConnection(Connection_String);
                        sQLiteConnection.Open();
                        SQLiteCommand sQLiteCommand = new SQLiteCommand(sQLiteConnection);
                        sQLiteCommand.CommandText = query;
                        for (int l = 0; l < prepparams.Count; l++) {
                            if (l == 0) {
                                for (int m = 0; m < prepparams[l].Count(); m++) {
                                    MySqlParameter val4 = new MySqlParameter(prepparams[l][m].identifier, (MySqlDbType)prepparams[l][m].DBType, prepparams[l][m].size);
                                    ((DbParameter)(object)val4).Value = prepparams[l][m].value;
                                    sQLiteCommand.Parameters.Add(val4);
                                }
                                sQLiteCommand.Prepare();
                                sQLiteCommand.ExecuteNonQuery();
                            } else {
                                for (int n = 0; n < prepparams[l].Count(); n++) {
                                    sQLiteCommand.Parameters[prepparams[l][n].identifier].Value = prepparams[l][n].value;
                                }
                                sQLiteCommand.ExecuteNonQuery();
                            }
                        }
                        sQLiteConnection.Close();
                        break;
                    }
                case DataBaseTypes.MySQl: {
                        MySqlConnection val = new MySqlConnection(Connection_String);
                        ((DbConnection)(object)val).Open();
                        MySqlCommand val2 = new MySqlCommand(query, val);
                        for (int i = 0; i < prepparams.Count; i++) {
                            if (i == 0) {
                                for (int j = 0; j < prepparams[i].Count(); j++) {
                                    MySqlParameter val3 = new MySqlParameter(prepparams[i][j].identifier, (MySqlDbType)prepparams[i][j].DBType, prepparams[i][j].size);
                                    ((DbParameter)(object)val3).Value = prepparams[i][j].value;
                                    val2.Parameters.Add(val3);
                                }
                                ((DbCommand)(object)val2).Prepare();
                                ((DbCommand)(object)val2).ExecuteNonQuery();
                            } else {
                                for (int k = 0; k < prepparams[i].Count(); k++) {
                                    ((DbParameter)(object)val2.Parameters[prepparams[i][k].identifier]).Value = prepparams[i][k].value;
                                }
                                DebugSystem.Write(DebugItemType.DataBase_Heavy, "Running MYSQL DB Query: " + ((DbCommand)(object)val2).CommandText);
                                ((DbCommand)(object)val2).ExecuteNonQuery();
                            }
                        }
                    ((DbConnection)(object)val).Close();
                        break;
                    }
            }
        }
    }

    public bool ClearDB() {
        lock (mlock) {
            DataTable dataTable = new DataTable();
            switch (ServType) {
                case DataBaseTypes.Sqlite:
                    try {
                        dataTable = GetDataTable("select NAME from SQLITE_MASTER where type='table' order by NAME;");
                    } catch (Exception) {
                        return false;
                    }
                    break;
            }
            if (dataTable == null) {
                return false;
            }
            foreach (DataRow row in dataTable.Rows) {
                ClearTable(row["NAME"].ToString());
            }
            return true;
        }
    }

    public bool ClearTable(string table) {
        lock (mlock) {
            try {
                ExecuteNonQuery($"delete from {table};");
                return true;
            } catch (Exception) {
                return false;
            }
        }
    }

    public virtual bool VerifyPassword(string check, string with) {
        return check == with;
    }

    public virtual bool VerifySaltedPassword(string password, string salt, string with) {
        return false;
    }

    public string hashMD5(string wert) {
        byte[] bytes = Encoding.UTF8.GetBytes(wert);
        byte[] array = MD5.Create().ComputeHash(bytes);
        return BitConverter.ToString(array).Replace("-", "").ToLower();
    }
}
