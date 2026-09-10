using System;
using Game;
using Network;

namespace Network.ActionCodes
{
    /// <summary>
    /// AC 44: Character Title Badge Management Protocol.
    /// Handles unlocking, equipping, and displaying custom character titles.
    /// </summary>
    public class AC44 : AC
    {
        public override int ID => 44;

        public override void ProcessPkt(Player c, RecievePacket p)
        {
            if (c == null || p == null) return;

            byte subCode = p.B ?? 1;
            ushort titleId = 0;
            if (p.Buffer.Length >= 8)
            {
                titleId = p.Unpack16();
            }

            DebugSystem.Write($"[AC44] Title badge update from {c.CharName}: SubCode={subCode}, TitleID={titleId}");

            try
            {
                c.Title = titleId;

                SendPacket resp = new SendPacket();
                resp.Pack8((byte)ID);
                resp.Pack8(subCode);
                resp.Pack16(titleId);
                c.Send(resp);

                // Broadcast visual title update to map
                SendPacket bcast = new SendPacket();
                bcast.Pack8((byte)ID);
                bcast.Pack8(1);
                bcast.Pack32(c.CharID);
                bcast.Pack16(titleId);
                c.CurMap?.Broadcast(bcast);
            }
            catch (Exception ex)
            {
                DebugSystem.Write(new ExceptionData(ex));
            }
        }
    }
}
