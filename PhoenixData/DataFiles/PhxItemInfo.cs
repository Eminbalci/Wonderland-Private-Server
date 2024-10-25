using System.Runtime.InteropServices;

namespace DataFiles;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public class PhxItemInfo
{
	public byte ItemNameLength;

	[MarshalAs(UnmanagedType.ByValArray, SizeConst = 20)]
	public byte[] ItemName;

	public byte ItemType;

	public ushort ItemID;

	public ushort IconNum;

	public ushort LargeIconNum;

	public ushort Equippos;

	public ushort level;

	public byte rank;

	public byte cellheight;

	public byte cellwidth;

	[MarshalAs(UnmanagedType.ByValArray, SizeConst = 2)]
	public ushort[] StatusType;

	[MarshalAs(UnmanagedType.ByValArray, SizeConst = 2)]
	public int[] StatusUp;

	public PhxItemInfo()
	{
		StatusType = new ushort[2];
		StatusUp = new int[2];
	}

	public override string ToString()
	{
		return ItemID.ToString();
	}
}
