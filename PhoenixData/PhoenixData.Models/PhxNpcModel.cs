using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text;
using DataFiles;

namespace PhoenixData.Models;

public class PhxNpcModel : PhcNpcModelExt, INotifyPropertyChanged, IComparer<PhxNpcModel>, IComparable<PhxNpcModel>
{
	public PhoneixNpc Datasrc;

	public string Name
	{
		get
		{
			return Encoding.Default.GetString(Datasrc.NpcName);
		}
		set
		{
			if (SetField(ref Datasrc.NpcName, value.ToByteArray(), "Name"))
			{
				Datasrc.NpcNameLength = (byte)value.Length;
			}
		}
	}

	public ushort NpcID
	{
		get
		{
			return Datasrc.NpcID;
		}
		set
		{
			SetField(ref Datasrc.NpcID, value, "NpcID");
		}
	}

	public ushort Sml_Icon
	{
		get
		{
			return Datasrc.ImageNumSmall;
		}
		set
		{
			SetField(ref Datasrc.ImageNumSmall, value, "Sml_Icon");
		}
	}

	public ushort Lrg_Icon
	{
		get
		{
			return Datasrc.ImageNum;
		}
		set
		{
			SetField(ref Datasrc.ImageNum, value, "Lrg_Icon");
		}
	}

	public byte Type
	{
		get
		{
			return Datasrc.Type;
		}
		set
		{
			SetField(ref Datasrc.Type, value, "Type");
		}
	}

	public byte Catchable
	{
		get
		{
			return Datasrc.Catchable;
		}
		set
		{
			SetField(ref Datasrc.Catchable, value, "Catchable");
		}
	}

	public bool isMerc => Tradeable == 180;

	public ushort Tradeable
	{
		get
		{
			return Datasrc.Tradeable;
		}
		set
		{
			SetField(ref Datasrc.Tradeable, value, "Tradeable");
			OnPropertyChanged("isMerc");
		}
	}

	public bool ImageNumEnlarge
	{
		get
		{
			return Datasrc.ImageNumEnlarge == 0;
		}
		set
		{
			SetField(ref Datasrc.ImageNumEnlarge, BitConverter.GetBytes(value)[0], "ImageNumEnlarge");
		}
	}

	public bool Transferrable
	{
		get
		{
			return Datasrc.Transferrable == 0;
		}
		set
		{
			SetField(ref Datasrc.Transferrable, BitConverter.GetBytes(value)[0], "Transferrable");
		}
	}

	public bool HumanNPC
	{
		get
		{
			return Datasrc.HumanNPC == 0;
		}
		set
		{
			SetField(ref Datasrc.HumanNPC, BitConverter.GetBytes(value)[0], "HumanNPC");
		}
	}

	public ushort HP_times2
	{
		get
		{
			return Datasrc.HP_times2;
		}
		set
		{
			SetField(ref Datasrc.HP_times2, (byte)value, "HP_times2");
		}
	}

	public uint HP
	{
		get
		{
			return Datasrc.HP;
		}
		set
		{
			SetField(ref Datasrc.HP, value, "HP");
		}
	}

	public uint SP
	{
		get
		{
			return Datasrc.SP;
		}
		set
		{
			SetField(ref Datasrc.SP, value, "SP");
		}
	}

	public ushort STR
	{
		get
		{
			return Datasrc.STR;
		}
		set
		{
			SetField(ref Datasrc.STR, value, "STR");
		}
	}

	public ushort CON
	{
		get
		{
			return Datasrc.CON;
		}
		set
		{
			SetField(ref Datasrc.CON, value, "CON");
		}
	}

	public ushort INT
	{
		get
		{
			return Datasrc.INT;
		}
		set
		{
			SetField(ref Datasrc.INT, value, "INT");
		}
	}

	public ushort WIS
	{
		get
		{
			return Datasrc.WIS;
		}
		set
		{
			SetField(ref Datasrc.WIS, value, "WIS");
		}
	}

	public ushort AGI
	{
		get
		{
			return Datasrc.AGI;
		}
		set
		{
			SetField(ref Datasrc.AGI, value, "AGI");
		}
	}

	public ushort SPD
	{
		get
		{
			return Datasrc.SPD;
		}
		set
		{
			SetField(ref Datasrc.SPD, value, "SPD");
		}
	}

	public byte Level
	{
		get
		{
			return Datasrc.Level;
		}
		set
		{
			SetField(ref Datasrc.Level, value, "Level");
		}
	}

	public int Element
	{
		get
		{
			try
			{
				return (Datasrc.element >= 7) ? 5 : Datasrc.element;
			}
			catch
			{
			}
			return 0;
		}
		set
		{
			SetField(ref Datasrc.element, (byte)((value == 5) ? 7u : ((uint)value)), "Element");
		}
	}

	public byte Unknwn1
	{
		get
		{
			return Datasrc.UnknownByte2;
		}
		set
		{
			SetField(ref Datasrc.UnknownByte2, value, "Unknwn1");
		}
	}

	public byte Unknwn2
	{
		get
		{
			return Datasrc.UnknownByte3;
		}
		set
		{
			SetField(ref Datasrc.UnknownByte3, value, "Unknwn2");
		}
	}

	public byte Unknwn3
	{
		get
		{
			return Datasrc.UnknownByte5;
		}
		set
		{
			SetField(ref Datasrc.UnknownByte5, value, "Unknwn3");
		}
	}

	public event PropertyChangedEventHandler PropertyChanged;

	public PhxNpcModel()
	{
		Datasrc = new PhoneixNpc();
	}

	public PhxNpcModel(PhoneixNpc src = null)
	{
		items = new BindingList<PhxItemInfo>();
		if (src != null)
		{
			Datasrc = src;
		}
		else
		{
			Datasrc = new PhoneixNpc();
		}
	}

	public int Compare(PhxNpcModel x, PhxNpcModel y)
	{
		if (x.NpcID < y.NpcID)
		{
			return 1;
		}
		if (x.NpcID > y.NpcID)
		{
			return -1;
		}
		return 0;
	}

	public int CompareTo(PhxNpcModel other)
	{
		if (other == null)
		{
			return 1;
		}
		if (other.NpcID == NpcID)
		{
			return 0;
		}
		return (NpcID >= other.NpcID) ? 1 : (-1);
	}

	protected virtual void OnPropertyChanged(string propertyName)
	{
		this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
	}

	protected bool SetField<T>(T field, T value, [CallerMemberName] string propertyName = null)
	{
		if (EqualityComparer<T>.Default.Equals(field, value))
		{
			return false;
		}
		field = value;
		OnPropertyChanged(propertyName);
		return true;
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
