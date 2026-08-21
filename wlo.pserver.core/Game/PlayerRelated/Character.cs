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
    public class Character : EquipManager
    {
        readonly object c_lock = new object();
        Action<SendPacket> Send;

        byte emote;
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
            p.Add((byte)5);
            p.Add((byte)3);
            p.Add((byte)Element);
            p.Add(CurHP);
            p.Add((ushort)CurSP);
            p.Add(Str); //base str
            p.Add(Con); //base con
            p.Add(Int); //base int
            p.Add(Wis); //base wis
            p.Add(Agi); //base agi
            p.Add(Level); //lvl
            p.Add(TotalExp); //exp ???
            //p.Add(Level -1); //lvl -1
            p.Add(FullHP); //max hp
            p.Add((ushort)FullSP); //max sp
            //-------------- 7 DWords
            p.Add(417);
            p.Add(0);
            p.Add(0);
            p.Add(240);
            p.Add(0);
            p.Add(0);
            p.Add(0);

            //--------------- Skills
            p.Add((ushort)0);
            //--------------- table with rebirth and job
            p.Add(0);
            p.Add(Reborn);
            p.Add((byte)Job);
            p.Add((byte)Potential);
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
            return p;
        }
    }
}

