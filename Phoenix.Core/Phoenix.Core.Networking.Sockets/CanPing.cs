using System;
using System.Net;
using System.Net.NetworkInformation;

namespace Phoenix.Core.Networking.Sockets;

public static class CanPing
{
	private static Ping pingsender;

	public static bool Them(IPAddress ip)
	{
		Ping ping = new Ping();
		PingReply pingReply = ping.Send(ip);
		if (pingReply.Status == IPStatus.Success)
		{
			DebugSystem.Write("Pinging " + ip.MapToIPv4().ToString());
			DebugSystem.Write("Address: {0}", pingReply.Address.ToString());
			DebugSystem.Write("RoundTrip time: {0}", pingReply.RoundtripTime);
			DebugSystem.Write("Time to live: {0}", pingReply.Options.Ttl);
			DebugSystem.Write("Don't fragment: {0}", pingReply.Options.DontFragment);
			DebugSystem.Write("Buffer size: {0}", pingReply.Buffer.Length);
			return true;
		}
		DebugSystem.Write("Uable to Ping " + ip.MapToIPv4().ToString());
		DebugSystem.Write(pingReply.Status);
		return false;
	}
}
