namespace Phoenix.Core.Networking;

public interface ISocket
{
	string LocalPort();

	string SockAddress();

	bool isDisconnected();

	void Disconnect();
}
