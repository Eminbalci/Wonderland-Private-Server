using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Wlo.Core;

public class Packet : IPacket
{
	private byte[] m_buffer;

	public ushort m_nUnpackIndex;

	private bool header;

	private PacketFlags m_Flag;

	public IEnumerable<byte> Buffer => m_buffer;

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

	public Packet(bool useheader = true)
	{
		header = useheader;
		m_buffer = new byte[0];
		m_nUnpackIndex = 0;
	}

	public Packet(IEnumerable<byte> init, int nLength = -1)
	{
		m_buffer = new byte[0];
		if (nLength > -1)
		{
			header = true;
			m_buffer = new byte[4];
			CopyToBuffer(init.ToArray(), nLength);
		}
		else
		{
			header = false;
			m_buffer = init.ToArray();
		}
		m_nUnpackIndex = 0;
	}

	public Packet(byte[] init, int nLength = -1)
	{
		m_buffer = new byte[0];
		if (nLength > -1)
		{
			header = true;
			CopyToBuffer(init.ToArray(), nLength);
		}
		else
		{
			header = false;
			m_buffer = init;
		}
		m_nUnpackIndex = 0;
	}

	public unsafe void CopyBufferTo(byte[] Bytes, int nOffset, int nLength)
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

	public unsafe void CopyToBuffer(byte[] Bytes, int nLength)
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

	public void Encode(byte xor = 173)
	{
		int num = 0;
		while (num < m_buffer.Length)
		{
			m_buffer[num] ^= xor;
			int num2 = num + 1;
			num = num2;
		}
	}

	public void Decode(byte xor = 173)
	{
		Encode(xor);
	}

	public static T ConvertfromFormat<T>(string packetFormat, params object[] list)
	{
		IEnumerable<byte> enumerable = new byte[0];
		int num = 0;
		int num2 = 0;
		for (int i = 0; i < packetFormat.Length; i++)
		{
			int num3;
			switch (packetFormat[i])
			{
			case 'D':
				enumerable = enumerable.Concat(BitConverter.GetBytes(Convert.ToUInt32(list[num])));
				num2 += 4;
				break;
			case 'd':
				enumerable = enumerable.Concat(BitConverter.GetBytes(Convert.ToInt32(list[num])));
				num2 += 4;
				break;
			case 'W':
				enumerable = enumerable.Concat(BitConverter.GetBytes(Convert.ToUInt16(list[num])));
				num2 += 2;
				break;
			case 'w':
				enumerable = enumerable.Concat(BitConverter.GetBytes(Convert.ToInt16(list[num])));
				num2 += 2;
				break;
			case 'B':
			{
				List<byte> list2 = enumerable.ToList();
				list2.Add(Convert.ToByte(list[num]));
				enumerable = list2;
				num3 = num2 + 1;
				num2 = num3;
				break;
			}
			case 'b':
			{
				List<byte> list2 = enumerable.ToList();
				list2.Add(Convert.ToByte(list[num]));
				enumerable = list2;
				num3 = num2 + 1;
				num2 = num3;
				break;
			}
			case 'L':
				enumerable = enumerable.Concat(BitConverter.GetBytes(Convert.ToUInt64(list[num])));
				num2 += 8;
				break;
			case 'l':
				enumerable = enumerable.Concat(BitConverter.GetBytes(Convert.ToInt64(list[num])));
				num2 += 8;
				break;
			case 'S':
			case 's':
			{
				string text = Convert.ToString(list[num]);
				List<byte> list2 = enumerable.ToList();
				list2.Add((byte)text.Length);
				list2.AddRange(Encoding.ASCII.GetBytes(text));
				enumerable = list2;
				num2 += text.Length + 1;
				break;
			}
			}
			num3 = num + 1;
			num = num3;
		}
		enumerable = BitConverter.GetBytes(Convert.ToUInt16(17652)).Concat(BitConverter.GetBytes(Convert.ToUInt16(num2))).Concat(enumerable);
		if (typeof(T) == typeof(Packet))
		{
			return (T)Convert.ChangeType(new Packet(enumerable), typeof(T));
		}
		if (typeof(T) == typeof(OutgoingPacket))
		{
			return (T)Convert.ChangeType(new OutgoingPacket(new Packet(enumerable)), typeof(T));
		}
		return default(T);
	}

	public static Packet FromArray(IEnumerable<byte> src)
	{
		Packet packet = new Packet(src, src.Count());
		packet.SetHeader(17652);
		return packet;
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

	public void PackArray(IEnumerable<byte> nVal)
	{
		if (m_buffer.Length == 0)
		{
			throw new Exception("Cannot use Pack Array to initialize an Array");
		}
		if (nVal != null)
		{
			PackArray(nVal.ToArray());
		}
	}

	public void PackArray(byte[] nVal)
	{
		if (m_buffer.Length == 0)
		{
			throw new Exception("Cannot use Pack Array to initialize an Array");
		}
		if (nVal != null)
		{
			CopyToBuffer(nVal, nVal.Length);
		}
	}

	public void PackDouble(double nVal)
	{
		if (header && m_buffer.Length < 4)
		{
			m_buffer = new byte[4];
		}
		CopyToBuffer(BitConverter.GetBytes(nVal), BitConverter.GetBytes(nVal).Length);
	}

	public void Pack64(ulong nVal)
	{
		if (header && m_buffer.Length < 4)
		{
			m_buffer = new byte[4];
		}
		CopyToBuffer(BitConverter.GetBytes(nVal), 8);
	}

	public void Pack32(uint nVal)
	{
		if (header && m_buffer.Length < 4)
		{
			m_buffer = new byte[4];
		}
		CopyToBuffer(BitConverter.GetBytes(nVal), 4);
	}

	public void Pack16(ushort nVal)
	{
		if (header && m_buffer.Length < 4)
		{
			m_buffer = new byte[4];
		}
		CopyToBuffer(BitConverter.GetBytes(nVal), 2);
	}

	public void Pack8(byte nVal)
	{
		if (header && m_buffer.Length < 4)
		{
			m_buffer = new byte[4];
		}
		CopyToBuffer(new byte[2] { nVal, 0 }, 1);
	}

	public void PackBool(bool val)
	{
		if (header && m_buffer.Length < 4)
		{
			m_buffer = new byte[4];
		}
		CopyToBuffer(BitConverter.GetBytes(val), 1);
	}

	public void PackString(string strVal)
	{
		Pack8((byte)(strVal?.Length ?? 0));
		CopyToBuffer(Encoding.ASCII.GetBytes((strVal == null) ? "" : strVal), strVal?.Length ?? 0);
	}

	public void PackStringN(string strVal)
	{
		if (header && m_buffer.Length < 4)
		{
			m_buffer = new byte[4];
		}
		CopyToBuffer(Encoding.ASCII.GetBytes(strVal), strVal.Length);
	}

	public void SetHeader(ushort nSignature = 17652)
	{
		if (m_buffer.Length > 4)
		{
			byte[] bytes = BitConverter.GetBytes(nSignature);
			int num = 0;
			while (num < 2)
			{
				m_buffer[num] = bytes[num];
				int num2 = num + 1;
				num = num2;
			}
			bytes = BitConverter.GetBytes((ushort)(m_buffer.Length - 4));
			int num3 = 2;
			while (num3 < 4)
			{
				m_buffer[num3] = bytes[num3 - 2];
				int num2 = num3 + 1;
				num3 = num2;
			}
		}
	}

	public override string ToString()
	{
		return string.Format("[Packet Size- {0} Group- {1} Header- {2} Data- {3}]", m_buffer.Length, m_buffer[4] + " " + ((m_buffer.Length > 5) ? m_buffer[5] : 0), string.Join(",", (from c in m_buffer.Take(4)
			select c.ToString()).ToArray()), string.Join(",", (from c in m_buffer.Skip(4)
			select c.ToString()).ToArray()));
	}
}
