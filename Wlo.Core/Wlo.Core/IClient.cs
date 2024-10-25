namespace Wlo.Core;

public interface IClient
{
	string SockAddress();

	bool isDisconnected();

	void SendPacket(IPacket p, PacketFlags pFlags = PacketFlags.None);

	void Disconnect();
}
