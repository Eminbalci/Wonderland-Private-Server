namespace RCLibrary.Core.Networking;

public interface IPacket
{
	byte[] Buffer { get; }

	int Count { get; }

	PacketFlags Flags { get; set; }
}
