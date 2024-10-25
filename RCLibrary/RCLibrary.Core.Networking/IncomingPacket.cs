namespace RCLibrary.Core.Networking;

internal class IncomingPacket : Packet
{
	protected bool m_bHeaderReceived;

	protected bool m_bReady;

	protected ushort m_nExpecting;

	protected ushort m_nGotten;

	public IncomingPacket()
	{
		m_bHeaderReceived = false;
		m_bReady = false;
		m_nExpecting = 0;
		m_nGotten = 0;
	}

	public bool InData(byte[] data, ushort length, byte xor = 173)
	{
		bool result = true;
		if (!m_bHeaderReceived)
		{
			CopyToBuffer(data, length);
			m_nGotten += length;
			if (m_nGotten >= 4)
			{
				m_bHeaderReceived = true;
				Encode(ref m_buffer, xor);
				if (17652 != Unpack16())
				{
					result = false;
				}
				m_nExpecting = Unpack16();
				m_nExpecting += 4;
				m_bHeaderReceived = true;
				Encode(ref m_buffer, xor);
			}
		}
		else
		{
			CopyToBuffer(data, length);
			m_nGotten += length;
			if (m_nGotten >= m_nExpecting)
			{
				Encode(ref m_buffer, xor);
				m_bReady = true;
			}
		}
		return result;
	}

	public bool IsReady()
	{
		return m_bReady;
	}

	public Packet GetPacket()
	{
		if (m_bReady)
		{
			return this;
		}
		return null;
	}

	public ushort HowMuch()
	{
		if (!m_bHeaderReceived)
		{
			return (ushort)(4 - m_nGotten);
		}
		return (ushort)(m_nExpecting - m_nGotten);
	}

	public void Encode(ref byte[] m_buffer, byte xor = 173)
	{
		int num = 0;
		while (num < m_buffer.Length)
		{
			m_buffer[num] ^= xor;
			int num2 = num + 1;
			num = num2;
		}
	}

	public override void Clear()
	{
		m_bHeaderReceived = false;
		m_bReady = false;
		m_nExpecting = 0;
		m_nGotten = 0;
		base.Clear();
	}
}
