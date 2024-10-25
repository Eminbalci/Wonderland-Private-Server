using System;

namespace RCLibrary.Core.Networking;

[Flags]
public enum PacketFlags
{
	None = 0,
	Disconnect = 1,
	Queued = 3,
	Queue_Dc = 4
}
