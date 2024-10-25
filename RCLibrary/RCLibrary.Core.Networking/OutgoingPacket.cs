using System.Linq;

namespace RCLibrary.Core.Networking;

internal class OutgoingPacket : Packet
{
	private ushort xor;

	private ushort m_nSent;

	public override byte[] Buffer => m_buffer;

	public OutgoingPacket(IPacket pkt, PacketFlags pflag = PacketFlags.None, ushort xor = 173)
	{
		CopyToBuffer(pkt.Buffer, pkt.Count);
		m_nSent = 0;
		this.xor = xor;
	}

	public PacketFlags GetFlags()
	{
		return base.Flags;
	}

	public bool IsDone()
	{
		if (base.Count > m_nSent)
		{
			return false;
		}
		return true;
	}

	public byte[] GetNextSet()
	{
		byte[] result = Buffer.Skip(m_nSent).ToArray();
		Encode(ref result, 173);
		return result;
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

	public void ReportBytesSent(ushort nBytes)
	{
		m_nSent += nBytes;
	}
}
