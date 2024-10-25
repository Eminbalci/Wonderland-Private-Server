using System.Runtime.InteropServices;

namespace DataFiles;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public class PhoneixNpc
{
	public byte NpcNameLength;

	[MarshalAs(UnmanagedType.ByValArray, SizeConst = 20)]
	public byte[] NpcName;

	public byte Type;

	public ushort NpcID;

	public ushort ImageNum;

	public ushort ImageNumSmall;

	public uint ColorCode1;

	public uint ColorCode2;

	public uint ColorCode3;

	public uint ColorCode4;

	public byte Catchable;

	public byte UnknownByte2;

	public byte UnknownByte3;

	public byte Level;

	public uint HP;

	public uint SP;

	public ushort STR;

	public ushort CON;

	public ushort INT;

	public ushort WIS;

	public ushort AGI;

	public byte ImageNumEnlarge;

	public byte element;

	public ushort SkillID1;

	public ushort SkillID2;

	public ushort SkillID3;

	public ushort ItemID1;

	public ushort ItemID2;

	public ushort ItemID3;

	public ushort ItemID4;

	public ushort ItemID5;

	public byte UnknownByte5;

	public ushort UnknownWord14;

	public ushort UnknownWord15;

	public ushort UnknownWord16;

	public ushort UnknownWord17;

	public byte GeneralAttack1;

	public ushort UnknownWord18;

	public byte GeneralAttack2;

	public byte UnknownByte8;

	public ushort TalkImage;

	public ushort UnknownWord20;

	public ushort UnknownWord21;

	public ushort SPD;

	public ushort GeneralAttack3;

	public byte UnknownByte9;

	public byte Transferrable;

	public byte PK_NPC;

	public ushort UnknownWord24;

	public byte UnknownByte12;

	public byte NPCQuestID;

	public byte HumanNPC;

	public byte UnknownByte15;

	public byte HP_times2;

	public ushort UnknownWord25;

	public ushort Tradeable;

	public ushort UnknownWord27;

	public ushort UnknownWord28;

	public ushort UnknownWord29;

	public uint UnknownDword2;

	public uint UnknownDword3;

	public uint UnknownDword4;

	public uint UnknownDword5;

	public uint UnknownDword6;
}
