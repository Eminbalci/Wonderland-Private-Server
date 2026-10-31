using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Game;
using Network;

namespace Network.ActionCodes
{
    public class AC35 : AC
    {
        public override int ID { get { return 35; } }
        public override void ProcessPkt(Player p, RecievePacket r)
        {
            switch (r.Unpack8())
            {
                case 2: Recv2(p, r); break;
                case 12: Recv12(p, r); break; // 0x0C: Opcode 0x230C (CHAT_BROADCAST)
            }
        }

        void Recv2(Player p, RecievePacket e)
        {
            byte slot = e.Unpack8();
            string uknw = e.UnpackString();
            string pw = e.UnpackString();

            // Allow deletion if:
            // 1. Cipher is not set (empty/null) - user hasn't configured deletion password yet
            // 2. Cipher matches the provided password
            bool cipherMatches = string.IsNullOrEmpty(p.UserAcc.Cipher) || p.UserAcc.Cipher == pw;

            if (cipherMatches)
            {
                cGlobal.gCharacterDataBase.DeleteCharacter((slot == 1) ? p.UserAcc.Character1ID : p.UserAcc.Character2ID);

                cGlobal.gUserDataBase.Update_Player_ID(p.UserAcc.DataBaseID, 0, slot);

                if (cGlobal.gCharacterDataBase.GetCharacterData(p.UserAcc.Character1ID) == null && cGlobal.gCharacterDataBase.GetCharacterData(p.UserAcc.Character2ID) == null)
                    p.UserAcc.Cipher = "";
                p.Send(Tools.FromFormat("bbbb", 24, 5, 53, 0));
                p.Send(Tools.FromFormat("bbbb", 24, 5, 52, 0));
                p.Send(Tools.FromFormat("bbbb", 24, 5, 54, 0));
                p.Send(Tools.FromFormat("bbbb", 24, 5, 183, 0));
                p.Send(Tools.FromFormat("bb", 20, 8));
                p.Send(Tools.FromFormat("bbbb", 35, 2, 1, slot));
            }
            else
                p.Send(Tools.FromFormat("bbbb", 35, 2, 3, slot));

        }

        void Send_24_5(Player player, byte value)
        {
            player.Send(Tools.FromFormat("bbbb", 24, 5, value, 0));

        }
            void Recv12(Player p, RecievePacket r)
        {
            try
            {
                byte channelId = (r.GetPtr() < r.Buffer.Length) ? r.Unpack8() : (byte)0;
                string msg = r.UnpackStringN();
                if (string.IsNullOrEmpty(msg)) return;

                DebugSystem.Write($"[Chat.AC35_12] Channel={channelId} [{p.CharName}]: {msg}");

                if (p.IsMuted)
                {
                    p.SendSystemMessage($"[Server] You are currently muted until {p.MutedUntil.Value:yyyy-MM-dd HH:mm:ss} UTC.");
                    return;
                }

                // If chat command, execute via AC02 handler
                if (msg.StartsWith(":") || msg.StartsWith("/"))
                {
                    var ac02 = Network.ActionCodes.AC.GetAction(2) as AC02;
                    if (ac02 != null)
                    {
                        SendPacket cmdPkt = new SendPacket();
                        cmdPkt.Pack8(2);
                        cmdPkt.Pack8(2);
                        cmdPkt.PackStringN(msg);
                        RecievePacket cmdRecv = new RecievePacket(cmdPkt.Buffer);
                        ac02.ProcessPkt(p, cmdRecv);
                        return;
                    }
                }

                // Broadcast authentic 0x230C payload: [35, 12, channelId, senderId(4B), msg]
                SendPacket broadcast = new SendPacket();
                broadcast.Pack8(35); // 0x23
                broadcast.Pack8(12); // 0x0C
                broadcast.Pack8(channelId);
                broadcast.Pack32(p.CharID);
                broadcast.PackStringN(msg);

                if (p.CurMap != null)
                {
                    p.CurMap.Broadcast(broadcast);
                }
                else
                {
                    p.Send(broadcast);
                }
            }
            catch (Exception ex)
            {
                DebugSystem.Write(new ExceptionData(ex));
            }
        }
}
}
