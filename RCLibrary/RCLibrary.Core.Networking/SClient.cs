using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;

namespace RCLibrary.Core.Networking;

public class SClient : ISocket, IDisposable
{
	private IPEndPoint m_ipaddr;

	private Socket m_Socket;

	private readonly object m_Lock;

	private IncomingPacket m_UnfinishedPacket;

	public Queue<IPacket> m_IncomingPacketList;

	private bool m_Disconnected = true;

	protected byte xor = 173;

	private int m_nTimer;

	public Action onConnectionLost;

	public SClient()
	{
		m_Lock = new object();
		m_UnfinishedPacket = new IncomingPacket();
		m_IncomingPacketList = new Queue<IPacket>();
		m_nTimer = 0;
		m_Disconnected = false;
	}

	public SClient(Socket sock)
	{
		m_Lock = new object();
		m_Socket = sock;
		m_UnfinishedPacket = new IncomingPacket();
		m_IncomingPacketList = new Queue<IPacket>();
		m_nTimer = 0;
		m_Disconnected = false;
		m_ipaddr = sock.RemoteEndPoint as IPEndPoint;
		SetTimer();
	}

	~SClient()
	{
	}

	public void Dispose()
	{
		if (!isDisconnected())
		{
			Disconnect();
		}
		m_Socket = null;
		m_IncomingPacketList.Clear();
		m_UnfinishedPacket = null;
		m_ipaddr = null;
	}

	public bool RecvProc(bool queuePacket = true)
	{
		bool flag = true;
		ushort num = m_UnfinishedPacket.HowMuch();
		byte[] array = new byte[num];
		try
		{
			ushort num2 = (ushort)m_Socket.Receive(array, num, SocketFlags.None);
			if (num2 == 0)
			{
				flag = false;
			}
			else
			{
				if (!m_UnfinishedPacket.InData(array, num2, xor))
				{
					flag = false;
				}
				IPacket incomingPacket;
				if (queuePacket && (incomingPacket = GetIncomingPacket()) != null)
				{
					m_IncomingPacketList.Enqueue(incomingPacket);
				}
				if (flag)
				{
					SetTimer();
				}
			}
		}
		catch (ObjectDisposedException)
		{
		}
		catch (SocketException ex2)
		{
			if (ex2.SocketErrorCode != SocketError.WouldBlock)
			{
				flag = false;
			}
		}
		catch (Exception ex3)
		{
			DebugSystem.Write(new ExceptionData(ex3, ExceptionSeverity.Error, "RecvProc", "C:\\Users\\Rommel JR\\Dropbox\\Wonderland Online Dev Group\\Pserver Core\\CServer\\RCLibrary\\Networking\\Sockets\\Client2.cs", 111));
			flag = false;
		}
		return flag;
	}

	public IPacket GetIncomingPacket()
	{
		Packet packet = m_UnfinishedPacket.GetPacket();
		if (packet != null)
		{
			m_UnfinishedPacket = new IncomingPacket();
		}
		return packet;
	}

	public string SockAddress()
	{
		return (m_ipaddr != null) ? (m_ipaddr.ToString() + ":" + m_ipaddr.Port) : null;
	}

	public string LocalPort()
	{
		return (m_ipaddr != null) ? m_Socket.LocalEndPoint.ToString().Split(':')[1] : null;
	}

	public bool isDisconnected()
	{
		try
		{
			return (!m_Disconnected && m_Socket == null) || !m_Socket.Connected;
		}
		catch
		{
			return true;
		}
	}

	private void SetTimer()
	{
		m_nTimer = Environment.TickCount;
	}

	public int Elapsed()
	{
		return Environment.TickCount - m_nTimer;
	}

	public bool Connect(string ip, int port)
	{
		m_Socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
		m_Socket.Blocking = true;
		try
		{
			m_Socket.Connect(IPAddress.Parse(ip), port);
		}
		catch (ObjectDisposedException)
		{
		}
		catch (Exception ex2)
		{
			DebugSystem.Write(new ExceptionData(ex2, ExceptionSeverity.Error, "Connect", "C:\\Users\\Rommel JR\\Dropbox\\Wonderland Online Dev Group\\Pserver Core\\CServer\\RCLibrary\\Networking\\Sockets\\Client2.cs", 183));
			return false;
		}
		return m_Socket.Connected;
	}

	public void Disconnect()
	{
		lock (m_Lock)
		{
			m_Disconnected = true;
			try
			{
				m_Socket.Shutdown(SocketShutdown.Both);
				m_Socket.Close();
			}
			catch
			{
			}
			m_Socket = null;
			m_IncomingPacketList.Clear();
			m_UnfinishedPacket = new IncomingPacket();
			if (onConnectionLost != null)
			{
				onConnectionLost();
			}
		}
	}

	public void SendPacket(IPacket p)
	{
		SendPacket(p, p.Flags);
	}

	public void SendPacket(IPacket p, PacketFlags pFlags = PacketFlags.None)
	{
		p.Flags = pFlags;
		lock (m_Lock)
		{
			OutgoingPacket outgoingPacket = new OutgoingPacket(p, PacketFlags.None, 173);
			while (!outgoingPacket.IsDone() && m_Socket != null && !isDisconnected())
			{
				try
				{
					ushort nBytes = (ushort)m_Socket.Send(outgoingPacket.GetNextSet(), SocketFlags.None);
					outgoingPacket.ReportBytesSent(nBytes);
				}
				catch (SocketException ex)
				{
					if (ex.SocketErrorCode != SocketError.WouldBlock)
					{
						DebugSystem.Write(new ExceptionData(ex, ExceptionSeverity.Error, "SendPacket", "C:\\Users\\Rommel JR\\Dropbox\\Wonderland Online Dev Group\\Pserver Core\\CServer\\RCLibrary\\Networking\\Sockets\\Client2.cs", 231));
					}
				}
				catch (Exception ex2)
				{
					DebugSystem.Write(new ExceptionData(ex2, ExceptionSeverity.Error, "SendPacket", "C:\\Users\\Rommel JR\\Dropbox\\Wonderland Online Dev Group\\Pserver Core\\CServer\\RCLibrary\\Networking\\Sockets\\Client2.cs", 236));
				}
			}
		}
	}

	public void SetBlock(bool enable_SocketBlocking)
	{
		m_Socket.Blocking = enable_SocketBlocking;
	}
}
