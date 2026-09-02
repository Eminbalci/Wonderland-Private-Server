using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using MySql.Data.MySqlClient;

namespace Phoenix.Core;

public class DataBase
{
	protected enum Types
	{
		Undefined,
		Sqlite,
		MySQl,
		SQl
	}

	protected Types ServType = Types.MySQl;

	protected string DBFile = "";

	protected string User = "wlodbadmin";

	protected string Pass = "0penf1r3";

	protected string DB = "wlo";

	protected string Port = "3306";

	protected string ServerIP = "127.0.0.1";

	private string Connection_String
	{
		get
		{
			if (ServType == Types.MySQl)
			{
				return $"Server = {ServerIP}; Port = {Port}; Database = {DB}; Uid = {User}; Pwd = {Pass}";
			}
			if (ServType == Types.Sqlite)
			{
				return $"Data Source={DBFile};Version=3;";
			}
			if (ServType == Types.SQl)
			{
				return $"Server={ServerIP};Database={DB};User Id={User};Password={Pass};";
			}
			return "";
		}
	}

	public DataBase()
	{
		try
		{
			// Default to Sqlite ServerDataBase.db
			ServType = Types.Sqlite;
			DBFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ServerDataBase.db");

			string configPath = "database.override.txt";
			if (!File.Exists(configPath))
			{
				configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "database.override.txt");
			}

			if (File.Exists(configPath))
			{
				using StreamReader streamReader = new StreamReader(configPath);
				string text = "";
				while ((text = streamReader.ReadLine()) != null)
				{
					if (string.IsNullOrWhiteSpace(text) || !text.Contains("|")) continue;
					var parts = text.Split('|');
					switch (parts[0])
					{
					case "Type":
						ServType = (Types)byte.Parse(parts[1]);
						break;
					case "User":
						User = parts[1];
						break;
					case "Pass":
						Pass = parts[1];
						break;
					case "DB":
						DB = parts[1];
						break;
					case "Port":
						Port = parts[1];
						break;
					case "IP":
						ServerIP = parts[1];
						break;
					case "File":
						DBFile = Path.IsPathRooted(parts[1]) ? parts[1] : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, parts[1]);
						break;
					}
				}
			}
		}
		catch
		{
		}
	}

	public bool TestConnection()
	{
		//IL_0053: Unknown result type (might be due to invalid IL or missing references)
		//IL_0059: Expected O, but got Unknown
		switch (ServType)
		{
		case Types.Sqlite:
		{
			SQLiteConnection sQLiteConnection = new SQLiteConnection(Connection_String);
			try
			{
				sQLiteConnection.Open();
				sQLiteConnection.Close();
				return true;
			}
			catch (Exception)
			{
				sQLiteConnection.Close();
			}
			break;
		}
		case Types.MySQl:
		{
			MySqlConnection val = new MySqlConnection(Connection_String);
			try
			{
				((DbConnection)(object)val).Open();
				((DbConnection)(object)val).Close();
				return true;
			}
			catch (Exception)
			{
				((DbConnection)(object)val).Close();
			}
			break;
		}
		}
		return false;
	}

	public DataTable GetDataTable(string query, params DbParam[] parameters)
	{
		//IL_00d2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d9: Expected O, but got Unknown
		//IL_00e5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ec: Expected O, but got Unknown
		DataTable dataTable = new DataTable();
		try
		{
			switch (ServType)
			{
			case Types.Sqlite:
			{
				SQLiteConnection sQLiteConnection = new SQLiteConnection(Connection_String);
				try
				{
					sQLiteConnection.Open();
					SQLiteCommand sQLiteCommand = new SQLiteCommand(sQLiteConnection);
					sQLiteCommand.CommandText = query;
					DbParam[] array = parameters;
					for (int i = 0; i < array.Length; i++)
					{
						DbParam dbParam = array[i];
						sQLiteCommand.Parameters.AddWithValue(dbParam.identifier, dbParam.value);
					}
					SQLiteDataReader sQLiteDataReader = sQLiteCommand.ExecuteReader();
					dataTable.Load(sQLiteDataReader);
					sQLiteDataReader.Close();
					sQLiteConnection.Close();
					return dataTable;
				}
				catch
				{
					sQLiteConnection.Close();
				}
				break;
			}
			case Types.MySQl:
			{
				MySqlConnection val = new MySqlConnection(Connection_String);
				try
				{
					((DbConnection)(object)val).Open();
					MySqlCommand val2 = new MySqlCommand(query, val);
					if (parameters != null)
					{
						DbParam[] array = parameters;
						for (int i = 0; i < array.Length; i++)
						{
							DbParam dbParam = array[i];
							val2.Parameters.AddWithValue(dbParam.identifier, (object)dbParam.value);
						}
					}
					MySqlDataReader val3 = val2.ExecuteReader();
					dataTable.Load((IDataReader)val3);
					((DbDataReader)(object)val3).Close();
					((DbConnection)(object)val).Close();
					return dataTable;
				}
				catch
				{
					((DbConnection)(object)val).Close();
				}
				break;
			}
			}
		}
		catch (Exception data)
		{
			DebugSystem.Write(data);
			dataTable = new DataTable();
		}
		return dataTable;
	}

	public int ExecuteNonQuery(string sql, params DbParam[] parameters)
	{
		//IL_00af: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b6: Expected O, but got Unknown
		//IL_00c1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c8: Expected O, but got Unknown
		int result = 0;
		switch (ServType)
		{
		case Types.Sqlite:
		{
			SQLiteConnection sQLiteConnection = new SQLiteConnection(Connection_String);
			sQLiteConnection.Open();
			SQLiteCommand sQLiteCommand = new SQLiteCommand(sQLiteConnection);
			sQLiteCommand.CommandText = sql;
			if (parameters != null)
			{
				DbParam[] array = parameters;
				for (int i = 0; i < array.Length; i++)
				{
					DbParam dbParam = array[i];
					sQLiteCommand.Parameters.AddWithValue(dbParam.identifier, dbParam.value);
				}
			}
			result = sQLiteCommand.ExecuteNonQuery();
			sQLiteConnection.Close();
			break;
		}
		case Types.MySQl:
		{
			MySqlConnection val = new MySqlConnection(Connection_String);
			((DbConnection)(object)val).Open();
			MySqlCommand val2 = new MySqlCommand(sql, val);
			if (parameters != null)
			{
				DbParam[] array = parameters;
				for (int i = 0; i < array.Length; i++)
				{
					DbParam dbParam = array[i];
					val2.Parameters.AddWithValue(dbParam.identifier, (object)dbParam.value);
				}
			}
			DebugSystem.Write("Running MYSQL DB Query: {0}", DebugItemType.DataBase_Heavy, ((DbCommand)(object)val2).CommandText);
			result = ((DbCommand)(object)val2).ExecuteNonQuery();
			((DbConnection)(object)val).Close();
			break;
		}
		}
		return result;
	}

	public int ExecuteNonQuery(string sql)
	{
		//IL_005c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0062: Expected O, but got Unknown
		//IL_006b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0072: Expected O, but got Unknown
		int result = 0;
		switch (ServType)
		{
		case Types.Sqlite:
		{
			SQLiteConnection sQLiteConnection = new SQLiteConnection(Connection_String);
			sQLiteConnection.Open();
			SQLiteCommand sQLiteCommand = new SQLiteCommand(sQLiteConnection);
			sQLiteCommand.CommandText = sql;
			result = sQLiteCommand.ExecuteNonQuery();
			sQLiteConnection.Close();
			break;
		}
		case Types.MySQl:
		{
			MySqlConnection val = new MySqlConnection(Connection_String);
			((DbConnection)(object)val).Open();
			MySqlCommand val2 = new MySqlCommand(sql, val);
			DebugSystem.Write("Running MYSQL DB Query: {0}", DebugItemType.DataBase_Heavy, ((DbCommand)(object)val2).CommandText);
			result = ((DbCommand)(object)val2).ExecuteNonQuery();
			((DbConnection)(object)val).Close();
			break;
		}
		}
		return result;
	}

	public int ExecuteNonQuery(DbCommand command)
	{
		//IL_0052: Unknown result type (might be due to invalid IL or missing references)
		//IL_0058: Expected O, but got Unknown
		int result = 0;
		switch (ServType)
		{
		case Types.Sqlite:
		{
			SQLiteConnection sQLiteConnection = new SQLiteConnection(Connection_String);
			sQLiteConnection.Open();
			command.Connection = sQLiteConnection;
			result = command.ExecuteNonQuery();
			sQLiteConnection.Close();
			break;
		}
		case Types.MySQl:
		{
			MySqlConnection val = new MySqlConnection(Connection_String);
			((DbConnection)(object)val).Open();
			command.Connection = (DbConnection)(object)val;
			DebugSystem.Write("Running MYSQL DB Query: {0}", DebugItemType.DataBase_Heavy, command.CommandText);
			result = command.ExecuteNonQuery();
			((DbConnection)(object)val).Close();
			break;
		}
		}
		return result;
	}

	public string ExecuteScalar(string sql)
	{
		//IL_006f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0075: Expected O, but got Unknown
		//IL_007e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0085: Expected O, but got Unknown
		switch (ServType)
		{
		case Types.Sqlite:
		{
			SQLiteConnection sQLiteConnection = new SQLiteConnection(DBFile);
			sQLiteConnection.Open();
			SQLiteCommand sQLiteCommand = new SQLiteCommand(sQLiteConnection);
			sQLiteCommand.CommandText = sql;
			object obj = sQLiteCommand.ExecuteScalar();
			sQLiteConnection.Close();
			if (obj != null)
			{
				return obj.ToString();
			}
			break;
		}
		case Types.MySQl:
		{
			MySqlConnection val = new MySqlConnection(Connection_String);
			((DbConnection)(object)val).Open();
			MySqlCommand val2 = new MySqlCommand(sql, val);
			object obj = ((DbCommand)(object)val2).ExecuteScalar();
			((DbConnection)(object)val).Close();
			if (obj != null)
			{
				return obj.ToString();
			}
			break;
		}
		}
		return "";
	}

	public void Update(string tableName, Dictionary<string, object> data, string where, params DbParam[] parameters)
	{
		string text = "";
		if (data.Count >= 1)
		{
			foreach (KeyValuePair<string, object> datum in data)
			{
				text += $" {datum.Key.ToString()} = '{datum.Value.ToString()}',";
			}
			text = text.Substring(0, text.Length - 1);
		}
		ExecuteNonQuery($"update {tableName} set {text} where {where};", parameters);
	}

	public void PreparedUpdate(string query, MySqlParameter[] prepparams, List<object[]> data)
	{
		//IL_001f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0025: Expected O, but got Unknown
		if (prepparams.Length != data[0].Length)
		{
			throw new Exception("");
		}
		MySqlCommand val = new MySqlCommand();
		((DbCommand)(object)val).CommandText = query;
	}

	public void Delete(string tableName, string where, KeyValuePair<string, string>[] parameters = null)
	{
		ExecuteNonQuery(string.Format("delete from {0} where {1};", tableName, where, parameters));
	}

	public void Insert(string tableName, Dictionary<string, object> data)
	{
		string text = "";
		string text2 = "";
		foreach (KeyValuePair<string, object> datum in data)
		{
			text += $" {datum.Key.ToString()},";
			text2 += $" '{datum.Value}',";
		}
		text = text.Substring(0, text.Length - 1);
		text2 = text2.Substring(0, text2.Length - 1);
		ExecuteNonQuery($"insert into {tableName}({text}) values({text2});");
	}

	public void PreparedInsert(string query, List<CMDParameter[]> prepparams)
	{
		//IL_0176: Unknown result type (might be due to invalid IL or missing references)
		//IL_017d: Expected O, but got Unknown
		//IL_0188: Unknown result type (might be due to invalid IL or missing references)
		//IL_018f: Expected O, but got Unknown
		//IL_0096: Unknown result type (might be due to invalid IL or missing references)
		//IL_009d: Expected O, but got Unknown
		//IL_01e3: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ea: Expected O, but got Unknown
		switch (ServType)
		{
		case Types.Sqlite:
		{
			SQLiteConnection sQLiteConnection = new SQLiteConnection(Connection_String);
			sQLiteConnection.Open();
			SQLiteCommand sQLiteCommand = new SQLiteCommand(sQLiteConnection);
			sQLiteCommand.CommandText = query;
			for (int i = 0; i < prepparams.Count; i++)
			{
				if (i == 0)
				{
					for (int j = 0; j < prepparams[i].Count(); j++)
					{
						MySqlParameter val3 = new MySqlParameter(prepparams[i][j].identifier, (MySqlDbType)prepparams[i][j].DBType, prepparams[i][j].size);
						((DbParameter)(object)val3).Value = prepparams[i][j].value;
						sQLiteCommand.Parameters.Add(val3);
					}
					sQLiteCommand.Prepare();
					sQLiteCommand.ExecuteNonQuery();
				}
				else
				{
					for (int j = 0; j < prepparams[i].Count(); j++)
					{
						sQLiteCommand.Parameters[prepparams[i][j].identifier].Value = prepparams[i][j].value;
					}
					sQLiteCommand.ExecuteNonQuery();
				}
			}
			sQLiteConnection.Close();
			break;
		}
		case Types.MySQl:
		{
			MySqlConnection val = new MySqlConnection(Connection_String);
			((DbConnection)(object)val).Open();
			MySqlCommand val2 = new MySqlCommand(query, val);
			for (int i = 0; i < prepparams.Count; i++)
			{
				if (i == 0)
				{
					for (int j = 0; j < prepparams[i].Count(); j++)
					{
						MySqlParameter val3 = new MySqlParameter(prepparams[i][j].identifier, (MySqlDbType)prepparams[i][j].DBType, prepparams[i][j].size);
						((DbParameter)(object)val3).Value = prepparams[i][j].value;
						val2.Parameters.Add(val3);
					}
					((DbCommand)(object)val2).Prepare();
					((DbCommand)(object)val2).ExecuteNonQuery();
				}
				else
				{
					for (int j = 0; j < prepparams[i].Count(); j++)
					{
						((DbParameter)(object)val2.Parameters[prepparams[i][j].identifier]).Value = prepparams[i][j].value;
					}
					DebugSystem.Write("Running MYSQL DB Query: {0}", DebugItemType.DataBase_Heavy, ((DbCommand)(object)val2).CommandText);
					((DbCommand)(object)val2).ExecuteNonQuery();
				}
			}
			((DbConnection)(object)val).Close();
			break;
		}
		}
	}

	public bool ClearDB()
	{
		DataTable dataTable = new DataTable();
		switch (ServType)
		{
		case Types.Sqlite:
			try
			{
				dataTable = GetDataTable("select NAME from SQLITE_MASTER where type='table' order by NAME;");
			}
			catch (Exception)
			{
				return false;
			}
			break;
		}
		if (dataTable == null)
		{
			return false;
		}
		foreach (DataRow row in dataTable.Rows)
		{
			ClearTable(row["NAME"].ToString());
		}
		return true;
	}

	public bool ClearTable(string table)
	{
		try
		{
			ExecuteNonQuery($"delete from {table};");
			return true;
		}
		catch (Exception)
		{
			return false;
		}
	}

	public virtual bool VerifyPassword(string check, string with)
	{
		return check == with;
	}
}
