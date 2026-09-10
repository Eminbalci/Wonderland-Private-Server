using System;
using Game;
using Network;

namespace Network.ActionCodes
{
    /// <summary>
    /// AC 18: Character Emotes & Gesture Animations Protocol.
    /// Broadcasts visual actions (sit, wave, cheer, bow, dance, cry) to surrounding players.
    /// </summary>
    public class AC18 : AC
    {
        public override int ID => 18;

        public override void ProcessPkt(Player c, RecievePacket p)
        {
            if (c == null || p == null) return;

            byte subCode = p.B ?? 1;
            ushort emoteId = 1;
            if (p.Buffer.Length >= 8)
            {
                emoteId = p.Unpack16();
            }

            DebugSystem.Write($"[AC18] Emote triggered by {c.CharName}: SubCode={subCode}, EmoteID={emoteId}");

            try
            {
                // Broadcast animation packet to current map
                SendPacket bcast = new SendPacket();
                bcast.Pack8((byte)ID);
                bcast.Pack8(subCode);
                bcast.Pack32(c.CharID);
                bcast.Pack16(emoteId);

                if (c.CurMap != null)
                {
                    c.CurMap.Broadcast(bcast);
                }
                else
                {
                    c.Send(bcast);
                }
            }
            catch (Exception ex)
            {
                DebugSystem.Write(new ExceptionData(ex));
            }
        }
    }
}
