using System;

namespace Phoenix.Core.Networking;

[Flags]
public enum PacketFlags
{
	None = 0,
	Disconnect = 1
}
