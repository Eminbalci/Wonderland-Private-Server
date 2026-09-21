using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Game.Code;
using Network;
using DataFiles;
using RCLibrary.Core.Networking;
using Game;

namespace Game
{
    public class Character : EquipManager, INotifyPropertyChanged
    {
        readonly object c_lock = new object();
        Action<SendPacket> Send;

        Dictionary<string, string> m_colors; public Dictionary<string, string> Colors { get { lock (c_lock) return m_colors; } }

        UInt16 m_hairColor; public UInt16 HairColor { get { lock (c_lock)return m_hairColor; } set { lock (c_lock)m_hairColor = value; } }
        UInt16 m_skinColor; public UInt16 SkinColor { get { lock (c_lock)return m_skinColor; } set { lock (c_lock)m_skinColor = value; } }
        UInt16 m_clothingColor; public UInt16 ClothingColor { get { lock (c_lock)return m_clothingColor; } set { lock (c_lock)m_clothingColor = value; } }
        UInt16 m_eyeColor; public UInt16 EyeColor { get { lock (c_lock)return m_eyeColor; } set { lock (c_lock)m_eyeColor = value; } }
        UInt32 m_colorcode1; public UInt32 ColorCode1 { get { lock (c_lock) return m_colorcode1 != 0 ? m_colorcode1 : (((uint)m_skinColor << 16) | (uint)m_hairColor); } set { lock (c_lock)m_colorcode1 = value; } }
        UInt32 m_colorcode2; public UInt32 ColorCode2 { get { lock (c_lock) return m_colorcode2 != 0 ? m_colorcode2 : (((uint)m_eyeColor << 16) | (uint)m_clothingColor); } set { lock (c_lock)m_colorcode2 = value; } }
        UInt32 m_charID = 0; public virtual UInt32 CharID { get { lock (c_lock)return m_charID; } set { lock (c_lock)m_charID = value; } }
        string m_name; public String CharName { get { lock (c_lock)return m_name; } set { lock (c_lock)m_name = value; } }
        string m_nickname; public String NickName { get { lock (c_lock)return m_nickname; } set { lock (c_lock)m_nickname = value; } }
        public uint SpouseID { get; set; } = 0;
        public string SpouseName { get; set; } = string.Empty;
        public bool IsMarried => SpouseID > 0;
        UInt16 m_x; public UInt16 CurX { get { lock (c_lock)return m_x; } set { lock (c_lock)m_x = value; } }
        UInt16 m_y; public UInt16 CurY { get { lock (c_lock)return m_y; } set { lock (c_lock)m_y = value; } }
        IMap curMap;
        public virtual IMap CurMap
        {
            get { lock (c_lock) return curMap; }
            set
            {
                lock (c_lock)
                {
                    curMap = value;
                }
            }
        }
        ushort m_loginMap; public UInt16 LoginMap { get { lock (c_lock)return m_loginMap; } set { lock (c_lock)m_loginMap = value; } }
        byte m_slot; public byte Slot { get { lock (c_lock)return m_slot; } set { lock (c_lock)m_slot = value; } }

        public Character(Action<IPacket> src,PhxItemDat itemdat)
            : base(src,itemdat)
        {
            Send = src;
            m_colors = new Dictionary<string, string>();
            m_nickname = "";
            m_name = "";
        }
        public Character()
            : base(null,null)
        {
            m_colors = new Dictionary<string, string>();
        }

        public override void ProcessSocket(Player src, RecievePacket p)
        {
            base.ProcessSocket(src, p);
        }

        public override void Clear()
        {
            base.Clear();
            m_colors.Clear();
        }

        public void SendCharacterData()
        {
            var pkt = this.ToAC3Packet();
            if (pkt != null) Send(pkt);
        }
        public virtual void Send_5_3() //logging in player info
        {
            PacketBuilder p = new PacketBuilder();
            p.Begin();
            p.Add((byte)5);                              // Offset 0: ActionCode (byte)
            p.Add((byte)3);                              // Offset 1: SubCode (byte)
            p.Add((byte)Element);                        // Offset 2: Element (byte)
            p.Add((uint)CurHP);                          // Offset 3..6: CurHP (uint, 4B)
            p.Add((ushort)CurSP);                        // Offset 7..8: CurSP (ushort, 2B)
            p.Add((ushort)Con);                          // Offset 9..10: CON (ushort, 2B)
            p.Add((ushort)Int);                          // Offset 11..12: INT (ushort, 2B)
            p.Add((ushort)Str);                          // Offset 13..14: STR (ushort, 2B)
            p.Add((ushort)Agi);                          // Offset 15..16: AGI (ushort, 2B)
            p.Add((ushort)Wis);                          // Offset 17..18: WIS (ushort, 2B)
            p.Add((byte)(Level > 0 ? Level : 1));        // Offset 19: Level (byte, 1B)
            p.Add((uint)TotalExp);                       // Offset 20..23: TotalExp (uint, 4B)
            p.Add((ushort)FullHP);                       // Offset 24..25: FullHP (ushort, 2B)
            p.Add((ushort)FullSP);                       // Offset 26..27: FullSP (ushort, 2B)
            
            // Client internal static state offsets (0x1c .. 0x3d = 34 bytes)
            p.Add((uint)417);                            // Offset 28..31: DWord (4B)
            p.Add((ushort)0);                            // Offset 32..33: Word (2B)
            p.Add((uint)0);                              // Offset 34..37: DWord (4B)
            p.Add((uint)240);                            // Offset 38..41: DWord (4B)
            p.Add((uint)0);                              // Offset 42..45: DWord (4B)
            p.Add((uint)0);                              // Offset 46..49: DWord (4B)
            p.Add((uint)0);                              // Offset 50..53: DWord (4B)
            p.Add((uint)0);                              // Offset 54..57: DWord (4B)
            p.Add((uint)0);                              // Offset 58..61: DWord (4B)

            // Offset 62..63 (0x3E..0x3F): SkillCount (ushort)
            p.Add((ushort)0);

            // Post-skill trailer offsets
            p.Add((ushort)SkillPoints);                  // 2 bytes: Available Stat Points (StatusUp)
            p.Add((ushort)Potential);                    // 2 bytes: Potential
            p.Add((byte)0);                              // 1 byte: Padding
            p.Add((byte)(Reborn ? 1 : 0));               // 1 byte: Reborn flag
            p.Add((byte)Potential);                      // 1 byte: Potential byte
            p.Add((byte)Job);                            // 1 byte: Reborn Job

            Send(new SendPacket(p.End()));
        }

        #region Inotify Property
        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChangedEventHandler handler = PropertyChanged;
            if (handler != null) handler(this, new PropertyChangedEventArgs(propertyName));
        }
        protected bool SetField<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }
        #endregion
    }

    public static class charExt
    {

        public static IEnumerable<Byte> ToArray(this Character src)
        {
            if (src == null) return null;
            PacketBuilder temp = new PacketBuilder();
            temp.Begin(null);
            temp.Add((byte)src.Slot);
            temp.Add(src.CharName);
            temp.Add((byte)src.Level);
            temp.Add((byte)src.Element);
            temp.Add((uint)src.CurHP);
            temp.Add((uint)src.FullHP);
            temp.Add((uint)src.CurSP);
            temp.Add((uint)src.FullSP);
            temp.Add((ulong)src.TotalExp);
            temp.Add((ushort)src.Body);
            temp.Add((ushort)src.Head);
            temp.Add((uint)src.ColorCode1);
            temp.Add((uint)src.ColorCode2);

            for (byte a = 1; a < 7; a++)
                temp.Add((ushort)(src[a] != null ? src[a].ItemID : 0));
            temp.Add((ushort)0); // slot 7

            return temp.End();
        }
        public static SendPacket ToAC3Packet(this Character src)
        {
            if (src == null) return null;
            SendPacket p = new SendPacket();
            p.Pack8(3);
            p.Pack32(src.CharID);
            p.Pack8((byte)src.Body);
            p.Pack16((ushort)(src.CurMap != null ? src.CurMap.MapID : src.LoginMap));
            p.Pack16(src.CurX);
            p.Pack16(src.CurY);
            p.Pack8(0);
            p.Pack8(src.Head);
            p.Pack8(0);
            p.Pack16(src.HairColor);
            p.Pack16(src.SkinColor);
            p.Pack16(src.ClothingColor);
            p.Pack16(src.EyeColor);
            p.Pack8(src.WornCount);
            p.PackArray(src.Worn_Equips ?? new byte[0]);
            p.Pack32(0);
            p.PackString(src.CharName ?? "");
            p.PackString(src.NickName ?? "");
            p.Pack32(0);
            return p;
        }

        public static SendPacket ToAC4Packet(this Character src)
        {
            if (src == null) return null;
            SendPacket p = new SendPacket();
            p.Pack8(4);
            p.Pack32(src.CharID);
            p.Pack8((byte)src.Body);
            p.Pack8((byte)src.Element);
            p.Pack8(src.Level > 0 ? src.Level : (byte)1);
            p.Pack16((ushort)(src.CurMap != null ? src.CurMap.MapID : src.LoginMap));
            p.Pack16(src.CurX);
            p.Pack16(src.CurY);
            p.Pack8(0);
            p.Pack8(src.Head);
            p.Pack8(0);
            p.Pack16(src.HairColor);
            p.Pack16(src.SkinColor);
            p.Pack16(src.ClothingColor);
            p.Pack16(src.EyeColor);
            p.Pack8(src.WornCount);
            p.PackArray(src.Worn_Equips ?? new byte[0]);
            p.Pack32(0);
            p.Pack8(0);
            p.PackBool(src.Reborn);
            p.Pack8((byte)src.Job);
            p.PackString(src.CharName ?? "");
            p.PackString(src.NickName ?? "");
            p.Pack8(255);
            uint guildId = (src is Player pl) ? (uint)pl.GuildID : 0u;
            p.Pack32(guildId);
            p.Pack8(1);
            return p;
        }
    }
}

