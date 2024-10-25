using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DataFiles;

public class PhxItemDat : IDataManager
{
	private List<PhxItemInfo> m_List = new List<PhxItemInfo>();

	private readonly object m_Lock = new object();

	private long m_nSize;

	public Action<int, int, float> onWritePercentChange;

	public Action<object> onDebug;

	public long Size => m_nSize;

	public event ProgressChangedEventHandler onLoadProgressChanged;

	private void DecodeItem32(ref uint val)
	{
		val = Convert.ToUInt32((val ^ 0xB80F4B4) - 9);
	}

	private void DecodeItem32(ref int val)
	{
		val = Convert.ToInt32((val ^ 0xB80F4B4) - 9);
	}

	private void DecodeItem16(ref ushort val)
	{
		val = Convert.ToUInt16((val ^ 0xEFC3) - 9);
	}

	private void DecodeItem8(ref byte val)
	{
		val = Convert.ToByte((val ^ 0x9A) - 9);
	}

	public bool bit_set(ushort value, ushort index)
	{
		return (value & (1 << (int)index)) != 0;
	}

	public PhxItemInfo GetItemByID(ushort ItemID)
	{
		PhxItemInfo result = new PhxItemInfo();
		bool flag = false;
		lock (m_Lock)
		{
			foreach (PhxItemInfo item in m_List)
			{
				if (item.ItemID == ItemID)
				{
					result = item;
					flag = true;
					break;
				}
			}
		}
		if (!flag)
		{
			onDebug(new ItemNotFoundException("Item => " + ItemID + " could not be found."));
		}
		return result;
	}

	public PhxItemInfo GetItemByName(string Name)
	{
		PhxItemInfo result = new PhxItemInfo();
		bool flag = false;
		lock (m_Lock)
		{
			foreach (PhxItemInfo item in m_List)
			{
				if (string.Compare(Name, Encoding.ASCII.GetString(item.ItemName)) == 0)
				{
					result = item;
					flag = true;
					break;
				}
			}
		}
		if (!flag)
		{
			onDebug(new ItemNotFoundException("Item => \"" + Name + "\" could not be found."));
		}
		return result;
	}

	public object GetObject(object a)
	{
		if (a.GetType().IsValueType)
		{
			return GetItemByID(ushort.Parse(a.ToString()));
		}
		return GetItemByName(a.ToString());
	}

	public List<PhxItemInfo> GetItemList()
	{
		List<PhxItemInfo> result = new List<PhxItemInfo>();
		lock (m_Lock)
		{
			result = m_List;
		}
		return result;
	}

	public Task<bool> Load(string file)
	{
		return Task.Factory.StartNew(delegate
		{
			try
			{
				if (onDebug != null)
				{
					onDebug("Beginning to Load Item Dat");
				}
				if (!File.Exists(file))
				{
					if (onDebug != null)
					{
						onDebug(file + " has not been found");
					}
					return false;
				}
				if (onDebug != null)
				{
					onDebug("Loading from " + file);
				}
				using (FileStream fileStream = new FileStream(file, FileMode.Open, FileAccess.Read))
				{
					long num = fileStream.Length;
					m_nSize = fileStream.Length;
					object @lock = default(object);
					if (m_List.Count > 0)
					{
						bool lockTaken = false;
						try
						{
							Monitor.Enter(@lock = m_Lock, ref lockTaken);
							m_List.Clear();
						}
						finally
						{
							if (lockTaken)
							{
								Monitor.Exit(@lock);
							}
						}
					}
					while (num >= Marshal.SizeOf(typeof(PhxItemInfo)))
					{
						PhxItemInfo item = Generics.ConvertTo<PhxItemInfo>(fileStream);
						bool lockTaken2 = false;
						try
						{
							Monitor.Enter(@lock = m_Lock, ref lockTaken2);
							m_List.Add(item);
						}
						finally
						{
							if (lockTaken2)
							{
								Monitor.Exit(@lock);
							}
						}
						num -= Marshal.SizeOf(typeof(PhxItemInfo));
					}
					fileStream.Close();
					fileStream.Dispose();
					if (onDebug != null)
					{
						onDebug("Loaded " + m_List.Count + " Items");
					}
					if (onDebug != null)
					{
						onDebug("Loading of ItemDat has completed");
					}
				}
				return true;
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

	public bool WriteItems(string file)
	{
		int num = 0;
		try
		{
			using FileStream fileStream = new FileStream(file, FileMode.Create, FileAccess.Write);
			for (int i = 0; i < m_List.Count; i++)
			{
				fileStream.Write(Generics.ConvertTo(m_List[i]), 0, Marshal.SizeOf((object)m_List[i]));
				num += Marshal.SizeOf((object)m_List[i]);
				if (onWritePercentChange != null)
				{
					onWritePercentChange(i + 1, m_List.Count, (i + 1) / m_List.Count * 100);
				}
			}
			fileStream.Flush();
		}
		catch
		{
			return false;
		}
		return true;
	}

	public void UnloadItems()
	{
		lock (m_Lock)
		{
			m_List.Clear();
		}
	}
}
