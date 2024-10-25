using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace DataFiles;

public class PhxNpcDat : IDataManager
{
	private bool Loaded = true;

	public BindingList<PhoneixNpc> NpcList = new BindingList<PhoneixNpc>();

	private readonly object m_Lock = new object();

	public Action<int, int, float> onWritePercentChange;

	public Action<object> onDebug;

	public event ProgressChangedEventHandler onLoadProgressChanged;

	public Task<bool> Load(string file)
	{
		return Task.Factory.StartNew(delegate
		{
			try
			{
				NpcList.Clear();
				if (onDebug != null)
				{
					onDebug("Beginning to Load Npc Dat");
				}
				if (File.Exists(file))
				{
					onDebug("Loading from " + file);
					using FileStream fileStream = new FileStream(file, FileMode.Open, FileAccess.Read);
					long num = fileStream.Length;
					while (num >= Marshal.SizeOf(typeof(PhoneixNpc)))
					{
						PhoneixNpc item = ReadFromItems<PhoneixNpc>(fileStream);
						lock (m_Lock)
						{
							NpcList.Add(item);
						}
						num -= Marshal.SizeOf(typeof(PhoneixNpc));
						if (this.onLoadProgressChanged != null)
						{
							this.onLoadProgressChanged(this, new ProgressChangedEventArgs((int)Math.Round((decimal)((fileStream.Length - num) / fileStream.Length * 100)), null));
						}
					}
					fileStream.Close();
					fileStream.Dispose();
					if (onDebug != null)
					{
						onDebug("Loaded " + NpcList.Count + " Npc");
					}
					if (onDebug != null)
					{
						onDebug("Loading of NpcDat has completed");
					}
					return true;
				}
				if (onDebug != null)
				{
					onDebug(file + " has not been found");
				}
				return false;
			}
			catch (Exception obj)
			{
				if (onDebug != null)
				{
					onDebug(obj);
				}
				return false;
			}
		});
	}

	public object GetObject(object a)
	{
		if (a.GetType().IsValueType)
		{
			return GetNpcbyID(ushort.Parse(a.ToString()));
		}
		return GetNpcbyName(a.ToString());
	}

	public PhoneixNpc GetNpcbyID(ushort ID)
	{
		foreach (PhoneixNpc npc in NpcList)
		{
			if (npc.NpcID == ID)
			{
				return npc;
			}
		}
		return null;
	}

	public PhoneixNpc GetNpcbyName(string Name)
	{
		foreach (PhoneixNpc npc in NpcList)
		{
			if (Encoding.ASCII.GetString(npc.NpcName) == Name)
			{
				return npc;
			}
		}
		return null;
	}

	private void DecodeItem32(ref uint val)
	{
		val = Convert.ToUInt32((val ^ 0xBAEB716) - 9);
	}

	private void DecodeItem32(ref int val)
	{
		val = Convert.ToInt32((val ^ 0xBAEB716) - 9);
	}

	private void DecodeItem16(ref ushort val)
	{
		val = Convert.ToUInt16((val ^ 0x5209) - 9);
	}

	private void DecodeItem8(ref byte val)
	{
		val = Convert.ToByte((val ^ 0xC8) - 9);
	}

	private T ReadFromItems<T>(Stream fs)
	{
		byte[] array = new byte[Marshal.SizeOf(typeof(T))];
		fs.Read(array, 0, Marshal.SizeOf(typeof(T)));
		GCHandle gCHandle = GCHandle.Alloc(array, GCHandleType.Pinned);
		T result = (T)Marshal.PtrToStructure(gCHandle.AddrOfPinnedObject(), typeof(T));
		gCHandle.Free();
		return result;
	}

	public bool bit_set(ushort value, ushort index)
	{
		return (value & (1 << (int)index)) != 0;
	}

	private byte[] ItemtoBytes<T>(T file)
	{
		byte[] array = new byte[Marshal.SizeOf(typeof(T))];
		GCHandle gCHandle = GCHandle.Alloc(array, GCHandleType.Pinned);
		try
		{
			IntPtr ptr = gCHandle.AddrOfPinnedObject();
			Marshal.StructureToPtr((object)file, ptr, fDeleteOld: false);
		}
		catch (Exception)
		{
		}
		finally
		{
			gCHandle.Free();
		}
		return array;
	}

	public bool WriteItems(string file)
	{
		int num = 0;
		try
		{
			using FileStream fileStream = new FileStream(file, FileMode.Create, FileAccess.Write);
			for (int i = 0; i < NpcList.Count; i++)
			{
				fileStream.Write(ItemtoBytes(NpcList[i]), 0, Marshal.SizeOf((object)NpcList[i]));
				num += Marshal.SizeOf((object)NpcList[i]);
				if (onWritePercentChange != null)
				{
					onWritePercentChange(i + 1, NpcList.Count, (i + 1) / NpcList.Count * 100);
				}
			}
		}
		catch
		{
			return false;
		}
		return true;
	}
}
