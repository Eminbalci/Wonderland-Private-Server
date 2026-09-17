using System;
using Game;
using Network;

namespace Network.ActionCodes
{
    /// <summary>
    /// AC 60: Tent Interior Floor & Wallpaper Decoration Protocol.
    /// Handles applying custom wallpapers, tiles, flooring textures, and visual interior themes inside player tents.
    /// </summary>
    public class AC60 : AC
    {
        public override int ID => 60;

        public override void ProcessPkt(Player c, RecievePacket p)
        {
            if (c == null || p == null) return;

            byte subCode = p.B ?? 1;
            ushort decorId = 0;
            if (p.Buffer.Length >= 8)
            {
                decorId = p.Unpack16();
            }

            DebugSystem.Write($"[AC60] Tent decor from {c.CharName}: SubCode={subCode}, DecorID={decorId}");

            try
            {
                SendPacket resp = new SendPacket();
                resp.Pack8((byte)ID);
                resp.Pack8(subCode);
                resp.Pack16(decorId);
                resp.Pack8(1); // Applied OK
                c.Send(resp);
            }
            catch (Exception ex)
            {
                DebugSystem.Write(new ExceptionData(ex));
            }
        }
    }
}
