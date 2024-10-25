using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;

namespace Phoenix.Core.Networking;

public class IClient : ISocket, IDisposable
{
	private IPEndPoint m_ipaddr;

	private Socket m_Socket;

	private readonly object m_Lock;

	private IncomingPacket m_UnfinishedPacket;

	public Queue<IPacket> m_IncomingPacketList;

	private Queue<OutgoingPacket> m_OutgoingList;

	private bool m_Disconnected = true;

	protected byte xor = 173;

	private int m_nTimer;

	public Queue<OutgoingPacket> SendQueue => m_OutgoingList;

	public event EventHandler onConnectionLost;

	public IClient()
	{
		m_Lock = new object();
		m_UnfinishedPacket = new IncomingPacket();
		m_IncomingPacketList = new Queue<IPacket>();
		m_OutgoingList = new Queue<OutgoingPacket>();
		m_nTimer = 0;
		m_Disconnected = false;
	}

	public IClient(Socket sock)
	{
		m_Lock = new object();
		m_Socket = sock;
		m_UnfinishedPacket = new IncomingPacket();
		m_IncomingPacketList = new Queue<IPacket>();
		m_OutgoingList = new Queue<OutgoingPacket>();
		m_nTimer = 0;
		m_Disconnected = false;
		m_ipaddr = sock.RemoteEndPoint as IPEndPoint;
		SetTimer();
	}

	~IClient()
	{
	}

	public void Dispose()
	{
		if (!isDisconnected())
		{
			Disconnect();
		}
		m_Socket = null;
		m_UnfinishedPacket = null;
		m_OutgoingList = null;
		m_ipaddr = null;
	}

	public bool SendProc()
	{
		bool result = true;
		if (m_OutgoingList.Count > 0)
		{
			OutgoingPacket outgoingPacket = m_OutgoingList.Peek();
			if (!outgoingPacket.IsDone())
			{
				byte[] nextSet = outgoingPacket.GetNextSet();
				try
				{
					ushort nBytes = (ushort)m_Socket.Send(nextSet, SocketFlags.None);
					outgoingPacket.ReportBytesSent(nBytes);
				}
				catch (SocketException ex)
				{
					DebugSystem.Write(ex);
					if (ex.SocketErrorCode != SocketError.WouldBlock)
					{
						result = false;
					}
				}
				catch (Exception data)
				{
					DebugSystem.Write(data);
				}
			}
			else
			{
				if (m_OutgoingList.Peek().GetFlags() == PacketFlags.Disconnect)
				{
					Disconnect();
				}
				m_OutgoingList.Dequeue();
			}
		}
		return result;
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
		catch (Exception data)
		{
			DebugSystem.Write(data);
			flag = false;
		}
		return flag;
	}

	public IPacket GetIncomingPacket()
	{
		Packet packet = m_UnfinishedPacket.GetPacket();
		if (null != packet)
		{
			m_UnfinishedPacket = new IncomingPacket();
		}
		return packet;
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
		m_Socket = new Socket(SocketType.Stream, ProtocolType.Tcp);
		m_Socket.Blocking = true;
		try
		{
			m_Socket.Connect(IPAddress.Parse(ip), port);
		}
		catch (ObjectDisposedException)
		{
		}
		catch (Exception data)
		{
			DebugSystem.Write(data);
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
			m_OutgoingList.Clear();
			m_IncomingPacketList.Clear();
			m_UnfinishedPacket = new IncomingPacket();
			if (this.onConnectionLost != null)
			{
				this.onConnectionLost(this, null);
			}
		}
	}

	public void SendPacket(IPacket p, PacketFlags pFlags = PacketFlags.None)
	{
		lock (m_Lock)
		{
			p.Flags = pFlags;
			m_OutgoingList.Enqueue(new OutgoingPacket(p));
		}
	}

	public void SetBlock(bool enable_SocketBlocking)
	{
		m_Socket.Blocking = enable_SocketBlocking;
	}
}
