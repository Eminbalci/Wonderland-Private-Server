using System;
using System.Net;
using System.Net.NetworkInformation;

namespace RCLibrary.Core.Networking;

public static class CanPing
{
	private static Ping pingsender;

	public static bool Them(IPAddress ip)
	{
		Ping ping = new Ping();
		PingReply pingReply = ping.Send(ip);
		if (pingReply.Status == IPStatus.Success)
		{
			DebugSystem.Write(DebugItemType.Network_Heavy, "{0}\r\n{1}\r\n{2}\r\n{3}\r\n{4}\r\n{5}", "Pinging " + ip.ToString().ToString(), "Address: " + pingReply.Address.ToString(), "RoundTrip time: " + pingReply.RoundtripTime, "Time to live: " + pingReply.Options.Ttl, "Don't fragment: " + pingReply.Options.DontFragment, "Buffer size: " + pingReply.Buffer.Length);
			return true;
		}
		DebugSystem.Write($"Uable to Ping {ip.ToString()} <{pingReply.Status}>");
		return false;
	}
}
