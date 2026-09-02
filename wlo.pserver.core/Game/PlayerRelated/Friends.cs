using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Network;
using Game.Bots;

namespace Game.PlayerRelated
{
    public class Friendlist
    {
        public static Func<uint, bool> IsPlayerOnlineHandler { get; set; }

        private readonly Character[] m_friendlist;
        private readonly Action<SendPacket> _sendPacket;
        private readonly Player _owner;

        public Friendlist(Player owner, Action<SendPacket> sendAction)
        {
            _owner = owner;
            _sendPacket = sendAction;
            m_friendlist = new Character[50];
            m_friendlist[0] = new Cupid(); // Default assistant bot
        }

        public Friendlist(Action<SendPacket> sendAction) : this(null, sendAction) { }

        public void SendFriendList()
        {
            if (_sendPacket == null) return;

            SendPacket y = new SendPacket();
            y.Pack8(14);
            y.Pack8(5);

            foreach (Character h in m_friendlist)
            {
                if (h == null || h is Cupid) continue;
                bool isOnline = (IsPlayerOnlineHandler != null && IsPlayerOnlineHandler(h.CharID));

                y.Pack32(h.CharID);
                y.PackString(h.CharName ?? $"Player #{h.CharID}");
                y.Pack8((byte)h.Level);
                y.Pack8((byte)(h.Reborn ? 1 : 0));
                y.Pack8((byte)h.Job);
                y.Pack8((byte)h.Element);
                y.Pack8((byte)h.Body);
                y.Pack8((byte)h.Head);
                y.Pack16(h.HairColor);
                y.Pack16(h.SkinColor);
                y.Pack16(h.ClothingColor);
                y.Pack16(h.EyeColor);
                y.PackString(h.NickName ?? "");
                y.PackString(""); // GuildName string
                y.Pack8((byte)(isOnline ? 1 : 0));
            }
            _sendPacket(y);
        }

        public bool IsFriend(uint charId)
        {
            for (int i = 0; i < m_friendlist.Length; i++)
            {
                if (m_friendlist[i] != null && m_friendlist[i].CharID == charId)
                    return true;
            }
            return false;
        }

        public void AddFriend(Player t)
        {
            if (t == null) return;
            if (IsFriend(t.CharID)) return;

            // Find first empty slot (slot 0 reserved for Cupid)
            int slot = -1;
            for (int i = 1; i < m_friendlist.Length; i++)
            {
                if (m_friendlist[i] == null)
                {
                    slot = i;
                    break;
                }
            }

            if (slot == -1) return; // Friend list full

            m_friendlist[slot] = t;

            // 1. Send AC 14:9 (Friend Add Success)
            SendPacket s = new SendPacket();
            s.Pack8(14);
            s.Pack8(9);
            s.Pack32(t.CharID);
            s.Pack8(0);
            _sendPacket?.Invoke(s);

            // 2. Send AC 14:7 (Friend Online Info)
            SendPacket sInfo = new SendPacket();
            sInfo.Pack8(14);
            sInfo.Pack8(7);
            sInfo.Pack32(t.CharID);
            sInfo.PackString(t.CharName ?? $"Player #{t.CharID}");
            _sendPacket?.Invoke(sInfo);
        }

        public void DelFriend(uint targetCharId)
        {
            for (int a = 1; a < m_friendlist.Length; a++)
            {
                if (m_friendlist[a] != null && m_friendlist[a].CharID == targetCharId)
                {
                    m_friendlist[a] = null;

                    // Send AC 14:4 (Friend Removed)
                    SendPacket s = new SendPacket();
                    s.Pack8(14);
                    s.Pack8(4);
                    s.Pack32(targetCharId);
                    _sendPacket?.Invoke(s);
                    break;
                }
            }
        }

        public bool LoadFriends(string str)
        {
            if (string.IsNullOrEmpty(str) || str == "none") return true;
            var frilist = str.Split('&');
            int idx = 1; // 0 is reserved for Cupid bot

            for (int a = 0; a < frilist.Length && idx < 50; a++)
            {
                if (!string.IsNullOrEmpty(frilist[a]) && uint.TryParse(frilist[a], out uint fid) && fid > 0)
                {
                    m_friendlist[idx++] = new Character { CharID = fid, CharName = $"Friend #{fid}" };
                }
            }
            return true;
        }

        public string GetFriends_Flag
        {
            get
            {
                var idList = new List<string>();
                for (int a = 1; a < m_friendlist.Length; a++) // Skip Cupid bot
                {
                    if (m_friendlist[a] != null && m_friendlist[a].CharID > 0)
                    {
                        idList.Add(m_friendlist[a].CharID.ToString());
                    }
                }
                return idList.Count > 0 ? string.Join("&", idList) : "none";
            }
        }
    }
}
