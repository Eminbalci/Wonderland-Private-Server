namespace Wlo.Core;

public class IncomingPacket
{
	protected Packet m_Packet;

	protected bool m_bHeaderReceived;

	protected bool m_bReady;

	protected ushort m_nExpecting;

	protected ushort m_nGotten;

	public IncomingPacket()
	{
		m_Packet = new Packet();
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
			m_Packet.CopyToBuffer(data, length);
			m_nGotten += length;
			if (m_nGotten >= 4)
			{
				m_bHeaderReceived = true;
				m_Packet.Decode(xor);
				if (17652 != m_Packet.Unpack16())
				{
					result = false;
				}
				m_nExpecting = m_Packet.Unpack16();
				m_nExpecting += 4;
				m_bHeaderReceived = true;
				m_Packet.Encode(xor);
			}
		}
		else
		{
			m_Packet.CopyToBuffer(data, length);
			m_nGotten += length;
			if (m_nGotten >= m_nExpecting)
			{
				m_Packet.Decode(xor);
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
			return m_Packet;
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
}
