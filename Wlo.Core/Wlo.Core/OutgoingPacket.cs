using System.Linq;

namespace Wlo.Core;

public class OutgoingPacket
{
	private IPacket m_Packet;

	private ushort m_nSent;

	public OutgoingPacket(IPacket pkt, PacketFlags pflag = PacketFlags.None)
	{
		m_Packet = pkt;
		m_nSent = 0;
		m_Packet.Flags = pflag;
	}

	public PacketFlags GetFlags()
	{
		return m_Packet.Flags;
	}

	public bool IsDone()
	{
		if (m_Packet.Buffer.Count() > m_nSent)
		{
			return false;
		}
		return true;
	}

	public byte[] GetNextSet()
	{
		return m_Packet.Buffer.Skip(m_nSent).ToArray();
	}

	public void ReportBytesSent(ushort nBytes)
	{
		m_nSent += nBytes;
	}
}
