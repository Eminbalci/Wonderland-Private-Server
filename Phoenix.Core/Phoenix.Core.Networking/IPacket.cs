using System.Collections.Generic;

namespace Phoenix.Core.Networking;

public interface IPacket
{
	IEnumerable<byte> Buffer { get; }

	PacketFlags Flags { get; set; }

	void Encode(byte xor = 173);

	void Decode(byte xor = 173);
}
