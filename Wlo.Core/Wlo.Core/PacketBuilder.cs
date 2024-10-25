using System.Collections.Generic;
using System.Linq;

namespace Wlo.Core;

public class PacketBuilder
{
	private Packet tmp;

	private bool useHead = true;

	private ushort header;

	public void Begin(ushort header = 17652)
	{
		this.header = header;
		tmp = new Packet(new byte[4] { 244, 68, 0, 0 });
	}

	public void Begin(bool Head)
	{
		tmp = new Packet(useHead = Head);
	}

	public void Add(IPacket p)
	{
		Add(p.Buffer.ToArray());
	}

	public void Add(byte obj)
	{
		tmp.Pack8(obj);
	}

	public void Add(ushort obj)
	{
		tmp.Pack16(obj);
	}

	public void Add(uint obj)
	{
		tmp.Pack32(obj);
	}

	public void Add(ulong obj)
	{
		tmp.Pack64(obj);
	}

	public void Add(short obj)
	{
		tmp.Pack16((ushort)obj);
	}

	public void Add(int obj)
	{
		tmp.Pack32((uint)obj);
	}

	public void Add(long obj)
	{
		tmp.Pack64((ulong)obj);
	}

	public void Add(string obj, bool nullstring = false)
	{
		if (!nullstring)
		{
			tmp.PackString(obj);
		}
		else
		{
			tmp.PackStringN(obj);
		}
	}

	public void Add(bool obj)
	{
		tmp.PackBool(obj);
	}

	public void Add(byte[] obj)
	{
		tmp.CopyToBuffer(obj, obj.Length);
	}

	public void Add(IEnumerable<byte> obj)
	{
		tmp.CopyToBuffer(obj.ToArray(), obj.Count());
	}

	public Packet End()
	{
		try
		{
			if (useHead)
			{
				tmp.SetHeader(header);
			}
			return tmp;
		}
		finally
		{
			tmp = null;
		}
	}
}
