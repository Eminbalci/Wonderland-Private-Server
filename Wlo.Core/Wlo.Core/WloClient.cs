using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;

namespace Wlo.Core;

public class WloClient : IClient, IDisposable
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

	public WloClient()
	{
		m_Lock = new object();
		m_UnfinishedPacket = new IncomingPacket();
		m_IncomingPacketList = new Queue<IPacket>();
		m_OutgoingList = new Queue<OutgoingPacket>();
		m_nTimer = 0;
		m_Disconnected = false;
	}

	public WloClient(Socket sock)
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

	~WloClient()
	{
	}

	public void Dispose()
	{
		if (!isDisconnected())
		{
			Disconnect();
		}
		m_Socket = null;
		m_UnfinishedPacket = new IncomingPacket();
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
					if (ex.SocketErrorCode != SocketError.WouldBlock)
					{
						throw ex;
					}
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

	public bool RecvProc()
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
				throw ex2;
			}
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
		return (m_ipaddr != null) ? (m_ipaddr.Address.ToString() + ":" + m_ipaddr.Port) : null;
	}

	public string LocalPort()
	{
		return (m_ipaddr != null) ? m_Socket.LocalEndPoint.ToString().Split(':')[1] : null;
	}

	public bool isDisconnected()
	{
		return (!m_Disconnected && m_Socket == null) || !m_Socket.Connected;
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
		m_Socket.ReceiveTimeout = 1000;
		m_Socket.SendTimeout = 1000;
		m_Socket.Blocking = true;
		try
		{
			m_Socket.Connect(IPAddress.Parse(ip), port);
		}
		catch (ObjectDisposedException)
		{
		}
		return m_Socket.Connected;
	}

	private void Sargs_Completed(object sender, SocketAsyncEventArgs e)
	{
		throw new NotImplementedException();
	}

	public void Disconnect()
	{
		lock (m_Lock)
		{
			m_Disconnected = true;
			m_Socket.Shutdown(SocketShutdown.Both);
			m_Socket.Close();
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
			p.Encode(xor);
			p.Flags = pFlags;
			m_OutgoingList.Enqueue(new OutgoingPacket(p));
		}
	}

	public void SetBlock(bool enable_SocketBlocking)
	{
		m_Socket.Blocking = enable_SocketBlocking;
	}
}
