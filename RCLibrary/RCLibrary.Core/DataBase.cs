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

    public static DataBaseTypes DefaultServType { get; set; } = DataBaseTypes.Sqlite;
    public static string DefaultServerIP { get; set; } = "127.0.0.1";
    public static string DefaultPort { get; set; } = "3306";
    public static string DefaultDB { get; set; } = "wlo";
    public static string DefaultUser { get; set; } = "root";
    public static string DefaultPass { get; set; } = "";
    public static string DefaultDBFile { get; set; } = "ServerDataBase.db";
    private static bool _configLoaded = false;

    public static event Action<DataBaseTypes, string, string, string, string, string, string> OnDatabaseConfigChanged;

    protected DataBaseTypes ServType = DataBaseTypes.Sqlite;

    protected string DBFile = "";

    protected string User = "root";

    protected string Pass = "";

    protected string DB = "wlo";

    protected string Port = "3306";

    protected string ServerIP = "127.0.0.1";

    public DataBaseTypes DatabaseType { get => ServType; set => ServType = value; }
    public string DatabaseServerIP { get => ServerIP; set => ServerIP = value; }
    public string DatabasePort { get => Port; set => Port = value; }
    public string DatabaseName { get => DB; set => DB = value; }
    public string DatabaseUser { get => User; set => User = value; }
    public string DatabasePass { get => Pass; set => Pass = value; }
    public string DatabaseFile { get => DBFile; set => DBFile = value; }

    public string Connection_String {
        get {
            if (ServType == DataBaseTypes.MySQl) {
                return $"Server={ServerIP};Port={Port};Database={DB};Uid={User};Pwd={Pass};Charset=utf8mb4;SslMode=none;AllowUserVariables=true;ConvertZeroDateTime=true;";
            }
            if (ServType == DataBaseTypes.Sqlite) {
                string resolved = !string.IsNullOrEmpty(DBFile)
                    ? (Path.IsPathRooted(DBFile) ? DBFile : PathHelper.ResolveDatabaseFile(DBFile))
                    : PathHelper.ResolveDatabaseFile("ServerDataBase.db");
                return $"Data Source={resolved};Version=3;";
            }
            if (ServType == DataBaseTypes.SQl) {
                return $"Server={ServerIP};Database={DB};User Id={User};Password={Pass};";
            }
            return "";
        }
    }

    public static string GetConfigFilePath() {
        string baseDir = AppDomain.CurrentDomain.BaseDirectory;
        string candidate = Path.Combine(baseDir, "database.override.txt");
        if (File.Exists(candidate)) return candidate;
        string rootCandidate = Path.Combine(PathHelper.AppRootDirectory, "database.override.txt");
        if (File.Exists(rootCandidate)) return rootCandidate;
        return candidate;
    }

    public static void LoadGlobalConfig(bool forceReload = false) {
        if (_configLoaded && !forceReload) return;
        _configLoaded = true;
        try {
            string configPath = GetConfigFilePath();
            if (File.Exists(configPath)) {
                using StreamReader streamReader = new StreamReader(configPath);
                string text = "";
                while ((text = streamReader.ReadLine()) != null) {
                    if (string.IsNullOrWhiteSpace(text) || !text.Contains("|")) continue;
                    var parts = text.Split(new char[] { '|' }, 2);
                    switch (parts[0].Trim()) {
                        case "Type":
                            if (byte.TryParse(parts[1].Trim(), out byte t))
                                DefaultServType = (DataBaseTypes)t;
                            break;
                        case "User": DefaultUser = parts[1].Trim(); break;
                        case "Pass": DefaultPass = parts[1]; break;
                        case "DB": DefaultDB = parts[1].Trim(); break;
                        case "Port": DefaultPort = parts[1].Trim(); break;
                        case "IP": DefaultServerIP = parts[1].Trim(); break;
                        case "File": DefaultDBFile = parts[1].Trim(); break;
                    }
                }
            }
        } catch (Exception ex) {
            DebugSystem.Write($"[DataBase.LoadGlobalConfig] Error: {ex.Message}");
        }
    }

    public static bool SaveGlobalConfig(DataBaseTypes type, string ip, string port, string db, string user, string pass, string dbFile, out string error) {
        error = "";
        try {
            DefaultServType = type;
            DefaultServerIP = ip ?? "127.0.0.1";
            DefaultPort = port ?? "3306";
            DefaultDB = db ?? "wlo";
            DefaultUser = user ?? "root";
            DefaultPass = pass ?? "";
            DefaultDBFile = !string.IsNullOrEmpty(dbFile) ? dbFile : "ServerDataBase.db";

            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"Type|{(byte)DefaultServType}");
            sb.AppendLine($"User|{DefaultUser}");
            sb.AppendLine($"Pass|{DefaultPass}");
            sb.AppendLine($"DB|{DefaultDB}");
            sb.AppendLine($"Port|{DefaultPort}");
            sb.AppendLine($"IP|{DefaultServerIP}");
            sb.AppendLine($"File|{DefaultDBFile}");

            string content = sb.ToString();

            string targetPath1 = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "database.override.txt");
            File.WriteAllText(targetPath1, content, Encoding.UTF8);

            string targetPath2 = Path.Combine(PathHelper.AppRootDirectory, "database.override.txt");
            if (!string.Equals(Path.GetFullPath(targetPath1), Path.GetFullPath(targetPath2), StringComparison.OrdinalIgnoreCase)) {
                try { File.WriteAllText(targetPath2, content, Encoding.UTF8); } catch { }
            }

            OnDatabaseConfigChanged?.Invoke(DefaultServType, DefaultServerIP, DefaultPort, DefaultDB, DefaultUser, DefaultPass, DefaultDBFile);
            DebugSystem.Write(DebugItemType.Info_Light, $"[DataBase] Configuration successfully saved. Active Provider: {DefaultServType}");
            return true;
        } catch (Exception ex) {
            error = ex.Message;
            DebugSystem.Write(DebugItemType.Error, $"[DataBase.SaveGlobalConfig] Error: {ex.Message}");
            return false;
        }
    }

    public void Reconfigure(DataBaseTypes type, string ip, string port, string db, string user, string pass, string dbFile) {
        ServType = type;
        ServerIP = ip ?? "127.0.0.1";
        Port = port ?? "3306";
        DB = db ?? "wlo";
        User = user ?? "root";
        Pass = pass ?? "";
        DBFile = !string.IsNullOrEmpty(dbFile)
            ? (Path.IsPathRooted(dbFile) ? dbFile : PathHelper.ResolveDatabaseFile(dbFile))
            : PathHelper.ResolveDatabaseFile("ServerDataBase.db");
    }

    public static void ReconfigureAllInstances(DataBaseTypes type, string ip, string port, string db, string user, string pass, string dbFile, params DataBase[] instances) {
        if (instances == null) return;
        foreach (var inst in instances) {
            if (inst != null) {
                inst.Reconfigure(type, ip, port, db, user, pass, dbFile);
            }
        }
    }

    public DataBase() {
        LoadGlobalConfig();
        ServType = DefaultServType;
        ServerIP = DefaultServerIP;
        Port = DefaultPort;
        DB = DefaultDB;
        User = DefaultUser;
        Pass = DefaultPass;
        DBFile = !string.IsNullOrEmpty(DefaultDBFile)
            ? (Path.IsPathRooted(DefaultDBFile) ? DefaultDBFile : PathHelper.ResolveDatabaseFile(DefaultDBFile))
            : PathHelper.ResolveDatabaseFile("ServerDataBase.db");
    }

    public static string TranslateSqlForMySql(string sql) {
        if (string.IsNullOrWhiteSpace(sql)) return sql;

        string result = sql;

        result = System.Text.RegularExpressions.Regex.Replace(
            result, 
            @"INTEGER\s+PRIMARY\s+KEY\s+AUTOINCREMENT", 
            "INT NOT NULL AUTO_INCREMENT PRIMARY KEY", 
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        result = System.Text.RegularExpressions.Regex.Replace(
            result, 
            @"\bAUTOINCREMENT\b", 
            "AUTO_INCREMENT", 
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        result = System.Text.RegularExpressions.Regex.Replace(
            result, 
            @"INTEGER\s+PRIMARY\s+KEY", 
            "INT PRIMARY KEY", 
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        result = System.Text.RegularExpressions.Regex.Replace(
            result, 
            @"TEXT\s+PRIMARY\s+KEY", 
            "VARCHAR(255) PRIMARY KEY", 
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        result = System.Text.RegularExpressions.Regex.Replace(
            result, 
            @"\bINSERT\s+OR\s+REPLACE\s+INTO\b", 
            "REPLACE INTO", 
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        result = System.Text.RegularExpressions.Regex.Replace(
            result,
            @"ON\s+CONFLICT\s*\(.*?\)\s+DO\s+UPDATE\s+SET",
            "ON DUPLICATE KEY UPDATE",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        result = System.Text.RegularExpressions.Regex.Replace(
            result, 
            @"\bBEGIN\s+TRANSACTION;?\b", 
            "START TRANSACTION;", 
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        result = System.Text.RegularExpressions.Regex.Replace(
            result, 
            @"SELECT\s+\*\s+FROM\s+sqlite_master\s+WHERE\s+type\s*=\s*'table'", 
            "SELECT table_name AS name FROM information_schema.tables WHERE table_schema = DATABASE()", 
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        result = System.Text.RegularExpressions.Regex.Replace(
            result, 
            @"SELECT\s+NAME\s+FROM\s+SQLITE_MASTER\s+WHERE\s+type\s*=\s*'table'\s+ORDER\s+BY\s+NAME;?", 
            "SELECT table_name AS NAME FROM information_schema.tables WHERE table_schema = DATABASE() ORDER BY table_name;", 
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        result = System.Text.RegularExpressions.Regex.Replace(
            result, 
            @"\bCREATE\s+TABLE\s+(?!IF\s+NOT\s+EXISTS\b)", 
            "CREATE TABLE IF NOT EXISTS ", 
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        return result;
    }

    public static DataTable Query(string sql) {
        try {
            LoadGlobalConfig();
            if (DefaultServType == DataBaseTypes.MySQl) {
                string connStr = $"Server={DefaultServerIP};Port={DefaultPort};Database={DefaultDB};Uid={DefaultUser};Pwd={DefaultPass};Charset=utf8mb4;SslMode=none;AllowUserVariables=true;";
                using (var conn = new MySqlConnection(connStr)) {
                    conn.Open();
                    string transSql = TranslateSqlForMySql(sql);
                    using (var cmd = new MySqlCommand(transSql, conn)) {
                        using (var reader = cmd.ExecuteReader()) {
                            DataTable dt = new DataTable();
                            dt.Load(reader);
                            return dt;
                        }
                    }
                }
            } else {
                string dbFile = PathHelper.ResolveDatabaseFile(string.IsNullOrEmpty(DefaultDBFile) ? "ServerDataBase.db" : DefaultDBFile);
                using (var conn = new SQLiteConnection($"Data Source={dbFile};Version=3;")) {
                    conn.Open();
                    using (var cmd = new SQLiteCommand(sql, conn)) {
                        using (var reader = cmd.ExecuteReader()) {
                            DataTable dt = new DataTable();
                            dt.Load(reader);
                            return dt;
                        }
                    }
                }
            }
        } catch (Exception ex) {
            DebugSystem.Write($"[DataBase.Query] Error: {ex.Message} -> SQL: {sql}");
            return null;
        }
    }

    public static int Execute(string sql) {
        try {
            LoadGlobalConfig();
            if (DefaultServType == DataBaseTypes.MySQl) {
                string connStr = $"Server={DefaultServerIP};Port={DefaultPort};Database={DefaultDB};Uid={DefaultUser};Pwd={DefaultPass};Charset=utf8mb4;SslMode=none;AllowUserVariables=true;";
                using (var conn = new MySqlConnection(connStr)) {
                    conn.Open();
                    string transSql = TranslateSqlForMySql(sql);
                    using (var cmd = new MySqlCommand(transSql, conn)) {
                        return cmd.ExecuteNonQuery();
                    }
                }
            } else {
                string dbFile = PathHelper.ResolveDatabaseFile(string.IsNullOrEmpty(DefaultDBFile) ? "ServerDataBase.db" : DefaultDBFile);
                using (var conn = new SQLiteConnection($"Data Source={dbFile};Version=3;")) {
                    conn.Open();
                    using (var cmd = new SQLiteCommand(sql, conn)) {
                        return cmd.ExecuteNonQuery();
                    }
                }
            }
        } catch (Exception ex) {
            DebugSystem.Write($"[DataBase.Execute] Error: {ex.Message} -> SQL: {sql}");
            return -1;
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

    public static bool EnsureMySqlDatabaseExists(string ip, string port, string db, string user, string pass, out string error) {
        error = null;
        try {
            string serverConnStr = $"Server={ip};Port={port};Uid={user};Pwd={pass};Charset=utf8mb4;SslMode=none;AllowUserVariables=true;Connection Timeout=5;";
            using (var conn = new MySqlConnection(serverConnStr)) {
                conn.Open();
                using (var cmd = new MySqlCommand($"CREATE DATABASE IF NOT EXISTS `{db}` DEFAULT CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;", conn)) {
                    cmd.ExecuteNonQuery();
                }
            }
            return true;
        } catch (Exception ex) {
            error = ex.Message;
            return false;
        }
    }

    public static bool TestConnection(DataBaseTypes type, string ip, string port, string db, string user, string pass, string dbFile, out string error, out string serverVersion) {
        error = null;
        serverVersion = "";
        try {
            if (type == DataBaseTypes.Sqlite) {
                string resolvedPath = !string.IsNullOrEmpty(dbFile)
                    ? (Path.IsPathRooted(dbFile) ? dbFile : PathHelper.ResolveDatabaseFile(dbFile))
                    : PathHelper.ResolveDatabaseFile("ServerDataBase.db");

                string dir = Path.GetDirectoryName(resolvedPath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) {
                    Directory.CreateDirectory(dir);
                }

                using (var conn = new SQLiteConnection($"Data Source={resolvedPath};Version=3;")) {
                    conn.Open();
                    serverVersion = $"SQLite {conn.ServerVersion}";
                    conn.Close();
                }
                return true;
            } else if (type == DataBaseTypes.MySQl) {
                string connStr = $"Server={ip};Port={port};Database={db};Uid={user};Pwd={pass};Charset=utf8mb4;SslMode=none;AllowUserVariables=true;Connection Timeout=5;";
                try {
                    using (var conn = new MySqlConnection(connStr)) {
                        conn.Open();
                        serverVersion = $"MySQL {conn.ServerVersion}";
                        conn.Close();
                    }
                    return true;
                } catch (MySqlException mex) when (mex.Number == 1049) {
                    string rootConnStr = $"Server={ip};Port={port};Uid={user};Pwd={pass};Charset=utf8mb4;SslMode=none;AllowUserVariables=true;Connection Timeout=5;";
                    using (var conn = new MySqlConnection(rootConnStr)) {
                        conn.Open();
                        serverVersion = $"MySQL {conn.ServerVersion} (Server Connected, Database '{db}' does not exist yet and will be auto-created)";
                        conn.Close();
                    }
                    return true;
                }
            }
            return false;
        } catch (Exception ex) {
            error = ex.Message;
            return false;
        }
    }

    public static bool MigrateSqliteToMySql(string sqlitePath, string mysqlIp, string mysqlPort, string mysqlDb, string mysqlUser, string mysqlPass, Action<string, int, int> progressCallback, out string resultSummary) {
        resultSummary = "";
        try {
            string resolvedSqlite = !string.IsNullOrEmpty(sqlitePath)
                ? (Path.IsPathRooted(sqlitePath) ? sqlitePath : PathHelper.ResolveDatabaseFile(sqlitePath))
                : PathHelper.ResolveDatabaseFile("ServerDataBase.db");

            if (!File.Exists(resolvedSqlite)) {
                resultSummary = $"SQLite source database file not found at: {resolvedSqlite}";
                return false;
            }

            if (!EnsureMySqlDatabaseExists(mysqlIp, mysqlPort, mysqlDb, mysqlUser, mysqlPass, out var createDbErr)) {
                resultSummary = $"Failed to create/verify target MySQL database '{mysqlDb}': {createDbErr}";
                return false;
            }

            string mysqlConnStr = $"Server={mysqlIp};Port={mysqlPort};Database={mysqlDb};Uid={mysqlUser};Pwd={mysqlPass};Charset=utf8mb4;SslMode=none;AllowUserVariables=true;";

            using (var sqliteConn = new SQLiteConnection($"Data Source={resolvedSqlite};Version=3;Read Only=True;"))
            using (var mysqlConn = new MySqlConnection(mysqlConnStr)) {
                sqliteConn.Open();
                mysqlConn.Open();

                using (var cmd = new MySqlCommand("SET FOREIGN_KEY_CHECKS = 0;", mysqlConn)) {
                    cmd.ExecuteNonQuery();
                }

                var tables = new List<string>();
                using (var cmd = new SQLiteCommand("SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%' ORDER BY name;", sqliteConn))
                using (var reader = cmd.ExecuteReader()) {
                    while (reader.Read()) {
                        tables.Add(reader.GetString(0));
                    }
                }

                int totalTables = tables.Count;
                int tablesMigrated = 0;
                int totalRowsMigrated = 0;

                for (int tIdx = 0; tIdx < totalTables; tIdx++) {
                    string tableName = tables[tIdx];
                    progressCallback?.Invoke($"Migrating table {tableName} ({tIdx + 1}/{totalTables})...", tIdx + 1, totalTables);

                    string createSql = "";
                    using (var cmd = new SQLiteCommand($"SELECT sql FROM sqlite_master WHERE type='table' AND name='{tableName}';", sqliteConn)) {
                        var obj = cmd.ExecuteScalar();
                        if (obj != null) createSql = obj.ToString();
                    }

                    if (string.IsNullOrWhiteSpace(createSql)) continue;

                    string mySqlCreate = TranslateSqlForMySql(createSql);
                    using (var cmd = new MySqlCommand(mySqlCreate, mysqlConn)) {
                        try { cmd.ExecuteNonQuery(); } catch { }
                    }

                    var dt = new DataTable();
                    using (var cmd = new SQLiteCommand($"SELECT * FROM [{tableName}];", sqliteConn))
                    using (var reader = cmd.ExecuteReader()) {
                        dt.Load(reader);
                    }

                    if (dt.Rows.Count > 0) {
                        using (var trans = mysqlConn.BeginTransaction()) {
                            try {
                                var colNames = new List<string>();
                                var paramNames = new List<string>();
                                for (int c = 0; c < dt.Columns.Count; c++) {
                                    colNames.Add($"`{dt.Columns[c].ColumnName}`");
                                    paramNames.Add($"@p{c}");
                                }

                                string insertSql = $"REPLACE INTO `{tableName}` ({string.Join(", ", colNames)}) VALUES ({string.Join(", ", paramNames)});";

                                using (var cmd = new MySqlCommand(insertSql, mysqlConn, trans)) {
                                    for (int c = 0; c < dt.Columns.Count; c++) {
                                        cmd.Parameters.Add(new MySqlParameter($"@p{c}", MySqlDbType.VarChar));
                                    }

                                    foreach (DataRow row in dt.Rows) {
                                        for (int c = 0; c < dt.Columns.Count; c++) {
                                            object val = row[c];
                                            cmd.Parameters[c].Value = (val == null || Convert.IsDBNull(val)) ? DBNull.Value : val;
                                        }
                                        cmd.ExecuteNonQuery();
                                        totalRowsMigrated++;
                                    }
                                }
                                trans.Commit();
                            } catch (Exception ex) {
                                trans.Rollback();
                                DebugSystem.Write(DebugItemType.Error, $"[Migration] Error migrating data for {tableName}: {ex.Message}");
                            }
                        }
                    }

                    tablesMigrated++;
                }

                using (var cmd = new MySqlCommand("SET FOREIGN_KEY_CHECKS = 1;", mysqlConn)) {
                    cmd.ExecuteNonQuery();
                }

                resultSummary = $"Successfully migrated {tablesMigrated} tables and {totalRowsMigrated} records to MySQL database '{mysqlDb}'.";
                return true;
            }
        } catch (Exception ex) {
            resultSummary = $"Migration failed: {ex.Message}";
            return false;
        }
    }

    public bool TestConnection() {
        return TestConnection(ServType, ServerIP, Port, DB, User, Pass, DBFile, out _, out _);
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
        DataTable dataTable = null;
        try {
            switch (ServType) {
                case DataBaseTypes.Sqlite: {
                        using (var sQLiteConnection = new SQLiteConnection(Connection_String)) {
                            sQLiteConnection.Open();
                            using (var sQLiteCommand = new SQLiteCommand(sQLiteConnection)) {
                                sQLiteCommand.CommandText = query;
                                if (parameters != null) {
                                    for (int j = 0; j < parameters.Length; j++) {
                                        DbParam dbParam2 = parameters[j];
                                        sQLiteCommand.Parameters.AddWithValue(dbParam2.identifier, dbParam2.value);
                                    }
                                }
                                using (var sQLiteDataReader = sQLiteCommand.ExecuteReader()) {
                                    dataTable = new DataTable();
                                    dataTable.Load(sQLiteDataReader);
                                    return dataTable;
                                }
                            }
                        }
                    }
                case DataBaseTypes.MySQl: {
                        using (var val = new MySqlConnection(Connection_String)) {
                            ((DbConnection)(object)val).Open();
                            string transQuery = query;
                            var pragmaMatch = System.Text.RegularExpressions.Regex.Match(transQuery, @"PRAGMA\s+table_info\s*\(\s*['""]?(\w+)['""]?\s*\)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                            if (pragmaMatch.Success) {
                                string targetTable = pragmaMatch.Groups[1].Value;
                                transQuery = $"SELECT COLUMN_NAME AS name, DATA_TYPE AS type, IS_NULLABLE AS `notnull`, COLUMN_DEFAULT AS dflt_value, CASE WHEN COLUMN_KEY = 'PRI' THEN 1 ELSE 0 END AS pk FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = '{targetTable}'";
                            } else {
                                transQuery = TranslateSqlForMySql(transQuery);
                            }
                            using (var val2 = new MySqlCommand(transQuery, val)) {
                                if (parameters != null) {
                                    for (int i = 0; i < parameters.Length; i++) {
                                        DbParam dbParam = parameters[i];
                                        val2.Parameters.AddWithValue(dbParam.identifier, (object)dbParam.value);
                                    }
                                }
                                using (var val3 = val2.ExecuteReader()) {
                                    dataTable = new DataTable();
                                    dataTable.Load((IDataReader)val3);
                                    return dataTable;
                                }
                            }
                        }
                    }
            }
        } catch (Exception ex) {
            DebugSystem.Write(DebugItemType.DataBase_Heavy, $"[DataBase.GetDataTable] Query: {query} Error: {ex.Message}");
            return null;
        }
        return dataTable;
    }

    public int ExecuteNonQuery(string sql, params DbParam[] parameters) {
        int result = 0;
        try {
            switch (ServType) {
                case DataBaseTypes.Sqlite: {
                        using (var sQLiteConnection = new SQLiteConnection(Connection_String)) {
                            sQLiteConnection.Open();
                            using (var sQLiteCommand = new SQLiteCommand(sQLiteConnection)) {
                                sQLiteCommand.CommandText = sql;
                                if (parameters != null) {
                                    for (int j = 0; j < parameters.Length; j++) {
                                        DbParam dbParam2 = parameters[j];
                                        sQLiteCommand.Parameters.AddWithValue(dbParam2.identifier, dbParam2.value);
                                    }
                                }
                                result = sQLiteCommand.ExecuteNonQuery();
                            }
                        }
                        break;
                    }
                case DataBaseTypes.MySQl: {
                        using (var val = new MySqlConnection(Connection_String)) {
                            ((DbConnection)(object)val).Open();
                            string transSql = TranslateSqlForMySql(sql);
                            using (var val2 = new MySqlCommand(transSql, val)) {
                                if (parameters != null) {
                                    for (int i = 0; i < parameters.Length; i++) {
                                        DbParam dbParam = parameters[i];
                                        val2.Parameters.AddWithValue(dbParam.identifier, (object)dbParam.value);
                                    }
                                }
                                DebugSystem.Write(DebugItemType.DataBase_Heavy, "Running MYSQL DB Query: " + ((DbCommand)(object)val2).CommandText);
                                try {
                                    result = ((DbCommand)(object)val2).ExecuteNonQuery();
                                } catch (MySqlException myEx) when (myEx.Number == 1061 || myEx.Number == 1050) {
                                    // Ignore duplicate key or table already exists
                                }
                            }
                        }
                        break;
                    }
            }
        } catch (Exception ex) {
            DebugSystem.Write(DebugItemType.Error, $"[DataBase.ExecuteNonQuery] Error: {ex.Message} -> SQL: {sql}");
            return -1;
        }
        return result;
    }

    public int ExecuteNonQuery(string sql) {
        return ExecuteNonQuery(sql, (DbParam[])null);
    }

    public int ExecuteNonQuery(DbCommand command) {
        int result = 0;
        try {
            switch (ServType) {
                case DataBaseTypes.Sqlite: {
                        using (var sQLiteConnection = new SQLiteConnection(Connection_String)) {
                            sQLiteConnection.Open();
                            command.Connection = sQLiteConnection;
                            result = command.ExecuteNonQuery();
                        }
                        break;
                    }
                case DataBaseTypes.MySQl: {
                        using (var val = new MySqlConnection(Connection_String)) {
                            ((DbConnection)(object)val).Open();
                            command.Connection = (DbConnection)(object)val;
                            command.CommandText = TranslateSqlForMySql(command.CommandText);
                            DebugSystem.Write(DebugItemType.DataBase_Heavy, "Running MYSQL DB Query: " + command.CommandText);
                            try {
                                result = command.ExecuteNonQuery();
                            } catch (MySqlException myEx) when (myEx.Number == 1061 || myEx.Number == 1050) {
                                // Ignore duplicate key or table already exists
                            }
                        }
                        break;
                    }
            }
        } catch (Exception ex) {
            DebugSystem.Write(DebugItemType.Error, $"[DataBase.ExecuteNonQuery(cmd)] Error: {ex.Message}");
            return -1;
        }
        return result;
    }

    public string ExecuteScalar(string sql) {
        try {
            switch (ServType) {
                case DataBaseTypes.Sqlite: {
                        using (var sQLiteConnection = new SQLiteConnection(Connection_String)) {
                            sQLiteConnection.Open();
                            using (var sQLiteCommand = new SQLiteCommand(sQLiteConnection)) {
                                sQLiteCommand.CommandText = sql;
                                object obj2 = sQLiteCommand.ExecuteScalar();
                                if (obj2 != null) return obj2.ToString();
                            }
                        }
                        break;
                    }
                case DataBaseTypes.MySQl: {
                        using (var val = new MySqlConnection(Connection_String)) {
                            ((DbConnection)(object)val).Open();
                            string transSql = TranslateSqlForMySql(sql);
                            using (var val2 = new MySqlCommand(transSql, val)) {
                                object obj = ((DbCommand)(object)val2).ExecuteScalar();
                                if (obj != null) return obj.ToString();
                            }
                        }
                        break;
                    }
            }
        } catch (Exception ex) {
            DebugSystem.Write(DebugItemType.Error, $"[DataBase.ExecuteScalar] Error: {ex.Message} -> SQL: {sql}");
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
                case DataBaseTypes.MySQl:
                    try {
                        dataTable = GetDataTable("SELECT table_name AS NAME FROM information_schema.tables WHERE table_schema = DATABASE() ORDER BY table_name;");
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
