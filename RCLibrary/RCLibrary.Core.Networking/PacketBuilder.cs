using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace RCLibrary.Core.Networking;

public class PacketBuilder
{
	private List<byte> tmp;

	private ushort? header;

	public void Begin(ushort? header = 17652)
	{
		this.header = header;
		tmp = new List<byte>();
	}

	public void Add(object src)
	{
		if (src is IPacket)
		{
			tmp.AddRange((src as IPacket).Buffer.ToArray());
		}
		else if (src is byte)
		{
			tmp.AddRange(BitConverter.GetBytes((byte)src).Take(1).ToArray());
		}
		else if (src is ushort)
		{
			tmp.AddRange(BitConverter.GetBytes((ushort)src).Take(2).ToArray());
		}
		else if (src is uint)
		{
			tmp.AddRange(BitConverter.GetBytes((uint)src).Take(4).ToArray());
		}
		else if (src is ulong)
		{
			tmp.AddRange(BitConverter.GetBytes((ulong)src).Take(8).ToArray());
		}
		else if (src is long)
		{
			tmp.AddRange(BitConverter.GetBytes((long)src).Take(8).ToArray());
		}
		else if (src is int)
		{
			tmp.AddRange(BitConverter.GetBytes((int)src).Take(4).ToArray());
		}
		else if (src is short)
		{
			tmp.AddRange(BitConverter.GetBytes((short)src).Take(2).ToArray());
		}
		else if (src is bool)
		{
			tmp.AddRange(BitConverter.GetBytes((bool)src).Take(1).ToArray());
		}
	}

	public void Add(string obj, bool nullstring = false)
	{
		if (obj != null)
		{
			if (!nullstring)
			{
				tmp.Add((byte)obj.Length);
			}
			tmp.AddRange(Encoding.ASCII.GetBytes(obj));
		}
	}

	public void Add(byte[] obj)
	{
		tmp.AddRange(obj);
	}

	public void Add(IEnumerable<byte> obj)
	{
		tmp.AddRange(obj.ToArray());
	}

	public byte[] End()
	{
		try
		{
			if (header.HasValue)
			{
				tmp.InsertRange(0, BitConverter.GetBytes((ushort)tmp.Count));
				tmp.InsertRange(0, BitConverter.GetBytes(header.Value));
				return tmp.ToArray();
			}
			return tmp.ToArray();
		}
		finally
		{
			tmp = null;
		}
	}
}
