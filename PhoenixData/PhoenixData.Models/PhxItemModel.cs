using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text;
using DataFiles;

namespace PhoenixData.Models;

public class PhxItemModel : INotifyPropertyChanged
{
	public PhxItemInfo Datasrc { get; private set; }

	public ushort ItemID
	{
		get
		{
			return Datasrc.ItemID;
		}
		set
		{
			SetField(ref Datasrc.ItemID, value, "ItemID");
		}
	}

	public string Name
	{
		get
		{
			return Encoding.Default.GetString(Datasrc.ItemName);
		}
		set
		{
			if (SetField(ref Datasrc.ItemName, value.ToByteArray(), "Name"))
			{
				Datasrc.ItemNameLength = (byte)value.Length;
			}
		}
	}

	public byte Type
	{
		get
		{
			return Datasrc.ItemType;
		}
		set
		{
			Datasrc.ItemType = value;
		}
	}

	public ushort Sml_Icon
	{
		get
		{
			return Datasrc.IconNum;
		}
		set
		{
			SetField(ref Datasrc.IconNum, value, "Sml_Icon");
		}
	}

	public ushort Lrg_Icon
	{
		get
		{
			return Datasrc.LargeIconNum;
		}
		set
		{
			SetField(ref Datasrc.LargeIconNum, value, "Lrg_Icon");
		}
	}

	public ushort Equip_Pos
	{
		get
		{
			return Datasrc.Equippos;
		}
		set
		{
			SetField(ref Datasrc.Equippos, value, "Equip_Pos");
		}
	}

	public ushort Level
	{
		get
		{
			return Datasrc.level;
		}
		set
		{
			SetField(ref Datasrc.level, value, "Level");
		}
	}

	public byte Rank
	{
		get
		{
			return Datasrc.rank;
		}
		set
		{
			SetField(ref Datasrc.rank, value, "Rank");
		}
	}

	public byte CellWidth
	{
		get
		{
			return Datasrc.cellwidth;
		}
		set
		{
			SetField(ref Datasrc.cellwidth, value, "CellWidth");
		}
	}

	public byte CellHeight
	{
		get
		{
			return Datasrc.cellheight;
		}
		set
		{
			SetField(ref Datasrc.cellheight, value, "CellHeight");
		}
	}

	public ushort[] StatusType
	{
		get
		{
			return Datasrc.StatusType;
		}
		set
		{
			SetField(ref Datasrc.StatusType, value, "StatusType");
		}
	}

	public int[] StatusUp
	{
		get
		{
			return Datasrc.StatusUp;
		}
		set
		{
			SetField(ref Datasrc.StatusUp, value, "StatusUp");
		}
	}

	public event PropertyChangedEventHandler PropertyChanged;

	public PhxItemModel(PhxItemInfo src = null)
	{
		if (src == null)
		{
			Datasrc = new PhxItemInfo();
		}
		else
		{
			Datasrc = src;
		}
	}

	protected virtual void OnPropertyChanged(string propertyName)
	{
		this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
	}

	protected bool SetField<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
	{
		if (EqualityComparer<T>.Default.Equals(field, value))
		{
			return false;
		}
		field = value;
		OnPropertyChanged(propertyName);
		return true;
	}
}
