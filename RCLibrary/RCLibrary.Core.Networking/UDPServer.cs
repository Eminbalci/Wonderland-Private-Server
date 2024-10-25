using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace RCLibrary.Core.Networking;

public class UDPServer
{
	private int _bindPort;

	public byte XOR;

	private IPEndPoint _broadcast;

	private UdpClient _listener;

	private ConcurrentQueue<IPacket> _IncomingPackets;

	private IncomingPacket incomingPkt;

	public Action<IPEndPoint, IPacket> onPacketRecved;

	private Thread _thrd;

	private bool _run;

	public UDPServer(int port)
	{
		XOR = 186;
		_bindPort = port;
	}

	public virtual void Start()
	{
		_listener = new UdpClient(_broadcast = new IPEndPoint(IPAddress.Any, _bindPort));
		_IncomingPackets = new ConcurrentQueue<IPacket>();
		incomingPkt = new IncomingPacket();
		_run = true;
		_thrd = new Thread(DoWork);
		_thrd.Start();
	}

	public virtual void Stop()
	{
		_run = false;
		try
		{
			_listener.Close();
		}
		catch
		{
		}
		if (_thrd != null)
		{
			_thrd.Join(6000);
			if (_thrd.IsAlive)
			{
				_thrd.Abort();
			}
		}
	}

	private void DoWork()
	{
		do
		{
			DebugSystem.Write("Waiting for Broadcast");
			try
			{
				byte[] array = new byte[0];
				try
				{
					array = _listener.Receive(ref _broadcast);
				}
				catch
				{
				}
				if (array.Length == 0)
				{
					continue;
				}
				int num = 0;
				int num2 = 0;
				DebugSystem.Write("Recieve Broadcast from " + _broadcast.ToString());
				while ((num2 = incomingPkt.HowMuch()) > 0 && num < array.Length)
				{
					if (!incomingPkt.InData(array.Skip(num).ToArray(), (ushort)num2, XOR))
					{
						incomingPkt = new IncomingPacket();
					}
					num += num2;
				}
				Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
				Packet packet;
				if ((packet = incomingPkt.GetPacket()) != null)
				{
					if (onPacketRecved == null)
					{
						_IncomingPackets.Enqueue(packet);
					}
					else
					{
						while (_IncomingPackets.Count > 0)
						{
							if (_IncomingPackets.TryDequeue(out var result))
							{
								onPacketRecved(_broadcast, result as Packet);
							}
						}
						onPacketRecved(_broadcast, packet);
					}
					incomingPkt = new IncomingPacket();
				}
				socket.Close();
			}
			catch (Exception ex)
			{
				DebugSystem.Write(new ExceptionData(ex, ExceptionSeverity.Error, "DoWork", "C:\\Users\\Rommel JR\\Dropbox\\Wonderland Online Dev Group\\Pserver Core\\CServer\\RCLibrary\\Networking\\Sockets\\UDPServer.cs", 113));
			}
		}
		while (_run);
	}
}
