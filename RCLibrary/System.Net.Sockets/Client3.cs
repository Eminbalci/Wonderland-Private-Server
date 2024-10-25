using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using RCLibrary.Core.Networking;

namespace System.Net.Sockets;

public class Client3 : IDisposable
{
	private IPEndPoint m_ipaddr;

	private IPEndPoint m_cipaddr;

	private Stopwatch m_dcTimer;

	private Socket m_Socket;

	private readonly object m_Lock;

	private IncomingPacket m_UnfinishedPacket;

	public ConcurrentQueue<IPacket> m_IncomingPackets;

	private ConcurrentQueue<OutgoingPacket> m_OutgoingPackets;

	private ManualResetEvent cansend;

	private ManualResetEvent sendblock = new ManualResetEvent(initialState: true);

	private bool _reuseSock = false;

	private bool _disconnecting = false;

	private bool m_Disconnected = true;

	private bool is_recieving = false;

	private bool is_sending = false;

	private bool dcevent_sent = false;

	protected byte xor = 173;

	public Action onConnectionLost;

	public Action<IPacket> onPacketRecved;

	private Thread _thrd;

	public bool ReuseSocket
	{
		get
		{
			lock (m_Lock)
			{
				return _reuseSock;
			}
		}
		set
		{
			lock (m_Lock)
			{
				_reuseSock = value;
			}
		}
	}

	public IPAddress IPAddr => (m_ipaddr != null) ? m_ipaddr.Address.MapToIPv4() : null;

	public Client3()
	{
		m_Lock = new object();
		cansend = new ManualResetEvent(initialState: false);
		m_Socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
		m_Socket.LingerState = new LingerOption(enable: false, 0);
		m_Socket.NoDelay = true;
		m_Socket.Ttl = 42;
		m_Socket.SendTimeout = 1000;
		m_Socket.ReceiveTimeout = 1000;
		m_cipaddr = m_Socket.LocalEndPoint as IPEndPoint;
		m_UnfinishedPacket = new IncomingPacket();
		m_IncomingPackets = new ConcurrentQueue<IPacket>();
		m_OutgoingPackets = new ConcurrentQueue<OutgoingPacket>();
		m_dcTimer = new Stopwatch();
		m_Disconnected = false;
		_disconnecting = false;
		if (m_Socket != null)
		{
			SetTimer();
			_thrd = new Thread(RecvProc);
			_thrd.Start(null);
		}
	}

	public Client3(Socket sock = null)
	{
		m_Lock = new object();
		cansend = new ManualResetEvent(initialState: false);
		m_Socket = sock;
		if (m_Socket == null)
		{
			m_Socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
		}
		m_Socket.LingerState = new LingerOption(enable: false, 0);
		m_Socket.NoDelay = true;
		m_Socket.Ttl = 42;
		m_Socket.SendTimeout = 1000;
		m_Socket.ReceiveTimeout = 1000;
		m_ipaddr = sock.RemoteEndPoint as IPEndPoint;
		m_cipaddr = sock.LocalEndPoint as IPEndPoint;
		m_UnfinishedPacket = new IncomingPacket();
		m_IncomingPackets = new ConcurrentQueue<IPacket>();
		m_OutgoingPackets = new ConcurrentQueue<OutgoingPacket>();
		m_dcTimer = new Stopwatch();
		m_Disconnected = false;
		_disconnecting = false;
		if (m_Socket != null)
		{
			SetTimer();
			_thrd = new Thread(RecvProc);
			_thrd.Start(null);
		}
	}

	public void Dispose()
	{
		if (!isDisconnected())
		{
			Disconnect();
		}
		m_OutgoingPackets = new ConcurrentQueue<OutgoingPacket>();
		m_IncomingPackets = new ConcurrentQueue<IPacket>();
		m_UnfinishedPacket = new IncomingPacket();
		m_ipaddr = null;
	}

	private async void RecvProc(object src)
	{
		bool bRet = (is_recieving = true);
		await Task.Delay(3);
		try
		{
			ushort size = m_UnfinishedPacket.HowMuch();
			byte[] buffer = new byte[size];
			ushort length = (ushort)m_Socket.Receive(buffer, size, SocketFlags.None);
			if (length == 0)
			{
				bRet = false;
			}
			else
			{
				if (!m_UnfinishedPacket.InData(buffer, length, xor))
				{
					bRet = false;
				}
				IPacket packet;
				IPacket p = (packet = m_UnfinishedPacket.GetPacket());
				if (packet != null)
				{
					if (onPacketRecved == null)
					{
						m_IncomingPackets.Enqueue(p);
					}
					else
					{
						while (m_IncomingPackets.Count > 0)
						{
							if (m_IncomingPackets.TryDequeue(out var g))
							{
								onPacketRecved(g as Packet);
							}
							g = null;
						}
						onPacketRecved(p);
					}
					m_UnfinishedPacket = new IncomingPacket();
				}
				if (bRet)
				{
					SetTimer();
				}
			}
		}
		catch (ObjectDisposedException)
		{
			bRet = false;
		}
		catch (SocketException ex3)
		{
			SocketException ex = ex3;
			if (ex.SocketErrorCode == SocketError.TimedOut || ex.SocketErrorCode == SocketError.Interrupted)
			{
				bRet = true;
			}
			else if (ex.SocketErrorCode != SocketError.WouldBlock)
			{
				bRet = false;
			}
		}
		catch (Exception ex4)
		{
			Exception e = ex4;
			DebugSystem.Write(new ExceptionData(e, ExceptionSeverity.Error, "RecvProc", "C:\\Users\\Rommel JR\\Dropbox\\Wonderland Online Dev Group\\Pserver Core\\CServer\\RCLibrary\\Networking\\Sockets\\Client3.cs", 183));
			bRet = false;
		}
		if (!(is_recieving = (is_sending = bRet)))
		{
			if (!dcevent_sent)
			{
				dcevent_sent = true;
				if (onConnectionLost != null)
				{
					onConnectionLost();
				}
			}
			Disconnect();
		}
		else
		{
			SendProc(null);
		}
	}

	private void SendProc(object src)
	{
		bool flag = (is_sending = true);
		if (m_OutgoingPackets.Count > 0)
		{
			try
			{
				if (m_OutgoingPackets.TryPeek(out var result))
				{
					while (!result.IsDone())
					{
						ushort nBytes;
						lock (m_Lock)
						{
							nBytes = (ushort)m_Socket.Send(result.GetNextSet(), SocketFlags.None);
						}
						result.ReportBytesSent(nBytes);
					}
					if (result.IsDone())
					{
						if (result.GetFlags() == PacketFlags.Disconnect)
						{
							flag = false;
						}
						m_OutgoingPackets.TryDequeue(out result);
					}
				}
			}
			catch (ObjectDisposedException)
			{
				flag = false;
			}
			catch (SocketException ex2)
			{
				if (ex2.SocketErrorCode == SocketError.TimedOut || ex2.SocketErrorCode == SocketError.Interrupted)
				{
					flag = true;
				}
				else if (ex2.SocketErrorCode != SocketError.WouldBlock)
				{
					flag = false;
				}
			}
			catch (Exception ex3)
			{
				flag = false;
				DebugSystem.Write(new ExceptionData(ex3, ExceptionSeverity.Error, "SendProc", "C:\\Users\\Rommel JR\\Dropbox\\Wonderland Online Dev Group\\Pserver Core\\CServer\\RCLibrary\\Networking\\Sockets\\Client3.cs", 243));
			}
		}
		if (!(is_recieving = (is_sending = flag)))
		{
			if (!dcevent_sent)
			{
				dcevent_sent = true;
				if (onConnectionLost != null)
				{
					onConnectionLost();
				}
			}
			Disconnect();
		}
		else
		{
			RecvProc(null);
		}
	}

	public IPacket GetIncomingPacket()
	{
		IPacket result = null;
		if (m_IncomingPackets.Count > 0)
		{
			m_IncomingPackets.TryDequeue(out result);
		}
		return result;
	}

	public Socket GetSocket()
	{
		return m_Socket;
	}

	public string SockAddress()
	{
		return (m_ipaddr != null) ? string.Concat(m_ipaddr.Address.MapToIPv4(), ":", m_ipaddr.Port) : null;
	}

	public string LocalPort()
	{
		return (m_ipaddr != null) ? m_Socket.LocalEndPoint.ToString().Split(':')[1] : null;
	}

	public bool isDisconnected()
	{
		try
		{
			return m_Disconnected || !is_recieving || !is_sending || m_Socket == null;
		}
		catch
		{
			return true;
		}
	}

	private void SetTimer()
	{
		m_dcTimer.Restart();
	}

	public TimeSpan Elapsed()
	{
		return m_dcTimer.Elapsed;
	}

	public bool Connect(string ip, int port)
	{
		m_dcTimer.Reset();
		bool result = false;
		try
		{
			if (isDisconnected())
			{
				m_Socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
				dcevent_sent = false;
				_disconnecting = false;
				m_Disconnected = false;
				m_Socket.Blocking = true;
				m_Socket.LingerState = new LingerOption(enable: false, 0);
				m_Socket.NoDelay = true;
				m_Socket.Ttl = 42;
				m_Socket.ReceiveTimeout = 1000;
				m_Socket.SendTimeout = 1000;
				lock (m_Lock)
				{
					m_Socket.Connect(IPAddress.Parse(ip), port);
				}
				if (m_Socket != null && m_Socket.Connected)
				{
					m_dcTimer.Start();
					is_recieving = (is_sending = true);
					m_ipaddr = m_Socket.RemoteEndPoint as IPEndPoint;
					m_cipaddr = m_Socket.LocalEndPoint as IPEndPoint;
					result = m_Socket.Connected;
					_thrd = new Thread(RecvProc);
					_thrd.Start(null);
				}
				else
				{
					m_Disconnected = true;
				}
			}
		}
		catch (Exception ex)
		{
			DebugSystem.Write(new ExceptionData(ex, ExceptionSeverity.Error, "Connect", "C:\\Users\\Rommel JR\\Dropbox\\Wonderland Online Dev Group\\Pserver Core\\CServer\\RCLibrary\\Networking\\Sockets\\Client3.cs", 353));
			m_Disconnected = true;
			return false;
		}
		Thread.Sleep(3);
		return result;
	}

	public void Disconnect()
	{
		if (_disconnecting)
		{
			return;
		}
		if (_thrd != null)
		{
			_thrd.Join();
		}
		_disconnecting = true;
		if (m_Socket != null && m_Socket.Connected)
		{
			try
			{
				lock (m_Lock)
				{
					m_Socket.Shutdown(SocketShutdown.Both);
				}
				lock (m_Lock)
				{
					m_Socket.BeginDisconnect(_reuseSock, Disconnected, m_Socket).AsyncWaitHandle.WaitOne();
					return;
				}
			}
			catch
			{
				Disconnected(null);
				return;
			}
		}
		Disconnected(null);
	}

	private void Disconnected(IAsyncResult src)
	{
		m_Disconnected = true;
		try
		{
			lock (m_Lock)
			{
				m_Socket.Close();
			}
		}
		catch
		{
		}
		m_IncomingPackets = new ConcurrentQueue<IPacket>();
		m_OutgoingPackets = new ConcurrentQueue<OutgoingPacket>();
		m_UnfinishedPacket = new IncomingPacket();
		if (onConnectionLost != null)
		{
			onConnectionLost();
		}
	}

	public virtual void SendPacket(IPacket p)
	{
		m_OutgoingPackets.Enqueue(new OutgoingPacket(p, PacketFlags.None, 173));
	}

	public virtual void SendPacket(IPacket p, PacketFlags pFlags)
	{
		p.Flags = pFlags;
		SendPacket(p);
	}

	public void SetBlock(bool enable_SocketBlocking)
	{
		m_Socket.Blocking = enable_SocketBlocking;
	}
}
