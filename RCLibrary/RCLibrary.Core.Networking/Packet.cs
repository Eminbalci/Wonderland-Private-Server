using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace RCLibrary.Core.Networking;

public abstract class Packet : IPacket
{
	protected byte[] m_buffer;

	private ushort nSignature;

	private ushort m_nUnpackIndex;

	protected bool header = false;

	private PacketFlags m_Flag;

	public byte this[int index] => m_buffer[index];

	public PacketFlags Flags
	{
		get
		{
			return m_Flag;
		}
		set
		{
			m_Flag = value;
		}
	}

	public virtual byte[] Buffer => m_buffer;

	public int Count => m_buffer.Length;

	public int GetPtr()
	{
		return m_nUnpackIndex;
	}

	public Packet()
	{
		m_buffer = new byte[0];
		m_nUnpackIndex = 0;
	}

	public Packet(IEnumerable<byte> init, int nLength = -1)
	{
		m_buffer = new byte[0];
		if (nLength > -1)
		{
			CopyToBuffer(init.ToArray(), nLength);
		}
		else
		{
			m_buffer = init.ToArray();
		}
		m_nUnpackIndex = 0;
	}

	public Packet(byte[] init, int nLength = -1)
	{
		m_buffer = new byte[0];
		if (nLength > -1)
		{
			CopyToBuffer(init.ToArray(), nLength);
		}
		else
		{
			m_buffer = init;
		}
		m_nUnpackIndex = 0;
	}

	protected unsafe void CopyBufferTo(byte[] Bytes, int nOffset, int nLength)
	{
		fixed (byte* ptr = Bytes)
		{
			fixed (byte* ptr3 = m_buffer)
			{
				byte* ptr2 = ptr;
				byte* ptr4 = ptr3 + nOffset;
				int num = 0;
				while (num < nLength)
				{
					*(ptr2++) = *(ptr4++);
					int num2 = num + 1;
					num = num2;
				}
			}
		}
	}

	protected unsafe void CopyToBuffer(byte[] Bytes, int nLength)
	{
		byte[] array = new byte[m_buffer.Length + nLength];
		fixed (byte* ptr = array)
		{
			fixed (byte* ptr3 = m_buffer)
			{
				byte* ptr2 = ptr;
				byte* ptr4 = ptr3;
				int num = 0;
				while (num < m_buffer.Length)
				{
					*(ptr2++) = *(ptr4++);
					int num2 = num + 1;
					num = num2;
				}
			}
			fixed (byte* ptr6 = Bytes)
			{
				byte* ptr5 = ptr + m_buffer.Length;
				byte* ptr7 = ptr6;
				int num3 = 0;
				while (num3 < nLength)
				{
					*(ptr5++) = *(ptr7++);
					int num2 = num3 + 1;
					num3 = num2;
				}
			}
		}
		m_buffer = array;
	}

	public double UnpackDouble()
	{
		double result = 0.0;
		if (m_nUnpackIndex + 8 <= m_buffer.Length)
		{
			result = BitConverter.ToDouble(m_buffer, m_nUnpackIndex);
			m_nUnpackIndex += 8;
		}
		return result;
	}

	public ulong Unpack64()
	{
		ulong result = 0uL;
		if (m_nUnpackIndex + 8 <= m_buffer.Length)
		{
			result = BitConverter.ToUInt64(m_buffer, m_nUnpackIndex);
			m_nUnpackIndex += 8;
		}
		return result;
	}

	public uint Unpack32()
	{
		uint result = 0u;
		if (m_nUnpackIndex + 4 <= m_buffer.Length)
		{
			result = BitConverter.ToUInt32(m_buffer, m_nUnpackIndex);
			m_nUnpackIndex += 4;
		}
		return result;
	}

	public ushort Unpack16()
	{
		ushort result = 0;
		if (m_nUnpackIndex + 2 <= m_buffer.Length)
		{
			result = BitConverter.ToUInt16(m_buffer, m_nUnpackIndex);
			m_nUnpackIndex += 2;
		}
		return result;
	}

	public byte Unpack8()
	{
		byte result = 0;
		if (m_nUnpackIndex + 1 <= m_buffer.Length)
		{
			result = m_buffer[m_nUnpackIndex];
			ushort nUnpackIndex = (ushort)(m_nUnpackIndex + 1);
			m_nUnpackIndex = nUnpackIndex;
		}
		return result;
	}

	public bool UnpackBool()
	{
		bool result = false;
		if (m_nUnpackIndex + 1 <= m_buffer.Length)
		{
			result = BitConverter.ToBoolean(m_buffer, m_nUnpackIndex);
			m_nUnpackIndex++;
		}
		return result;
	}

	public string UnpackString()
	{
		string result = "";
		int num = Unpack8();
		if (m_nUnpackIndex + num <= m_buffer.Length)
		{
			result = Encoding.ASCII.GetString(m_buffer, m_nUnpackIndex, num);
		}
		m_nUnpackIndex += (ushort)num;
		return result;
	}

	public string UnpackStringN()
	{
		string text = "";
		if (m_nUnpackIndex < m_buffer.Length)
		{
			text = Encoding.ASCII.GetString(m_buffer, m_nUnpackIndex, m_buffer.Length - m_nUnpackIndex);
			m_nUnpackIndex += (ushort)text.Length;
		}
		return text;
	}

	public virtual void PackArray(IEnumerable<byte> nVal)
	{
		if (nVal != null)
		{
			PackArray(nVal.ToArray());
		}
	}

	public virtual void PackArray(byte[] nVal)
	{
		if (nVal != null)
		{
			CopyToBuffer(nVal, nVal.Length);
		}
	}

	public virtual void PackDouble(double nVal)
	{
		CopyToBuffer(BitConverter.GetBytes(nVal), BitConverter.GetBytes(nVal).Length);
	}

	public virtual void Pack64(ulong nVal)
	{
		CopyToBuffer(BitConverter.GetBytes(nVal), 8);
	}

	public virtual void Pack32(uint nVal)
	{
		CopyToBuffer(BitConverter.GetBytes(nVal), 4);
	}

	public virtual void Pack16(ushort nVal)
	{
		CopyToBuffer(BitConverter.GetBytes(nVal), 2);
	}

	public virtual void Pack8(byte nVal)
	{
		CopyToBuffer(new byte[2] { nVal, 0 }, 1);
	}

	public virtual void PackBool(bool val)
	{
		CopyToBuffer(BitConverter.GetBytes(val), 1);
	}

	public virtual void PackString(string strVal)
	{
		Pack8((byte)(strVal?.Length ?? 0));
		CopyToBuffer(Encoding.ASCII.GetBytes((strVal == null) ? "" : strVal), strVal?.Length ?? 0);
	}

	public virtual void PackStringN(string strVal)
	{
		CopyToBuffer(Encoding.ASCII.GetBytes(strVal), strVal.Length);
	}

	protected virtual void SetPtr(int ptr = 0)
	{
		m_nUnpackIndex = (ushort)ptr;
	}

	public virtual void Clear()
	{
		m_buffer = new byte[0];
		m_nUnpackIndex = 0;
	}

	public new virtual string ToString()
	{
		return string.Format("[Packet Header- {0} Size- {1} AC- {2} Data- {3}]", string.Join(",", m_buffer.Take(2)), m_buffer.Length, m_buffer[4] + " " + ((m_buffer.Length > 5) ? m_buffer[5].ToString() : ""), string.Join(",", m_buffer.Skip(4)));
	}
}
