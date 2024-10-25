using System.Net;

namespace RCLibrary.Core;

public interface DBConnectInfo
{
	string User { get; }

	string Pass { get; }

	string DataBase { get; }

	int Port { get; }

	IPAddress ServerIP { get; }

	DataBaseTypes Server_Type { get; }

	bool VerifyPassword(string check, string with, int useverify = 0);
}
