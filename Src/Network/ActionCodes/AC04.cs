using System;
using Game;
using Game.Bots;
using Network;

namespace Network.ActionCodes
{
    /// <summary>
    /// AC 4: Map Entity Replication & Player Visual Spawning Protocol.
    /// Handles visual avatar appearance, equipment layers, element attributes, coordinates,
    /// and guild/title flags for players, GM bots, and dynamic entities across map boundaries.
    /// </summary>
    public class AC04 : AC
    {
        public override int ID => 4;

        public override void ProcessPkt(Player c, RecievePacket p)
        {
            if (c == null || p == null) return;

            byte subCode = p.B ?? 0;
            DebugSystem.Write($"[AC04] Entity query from {c.CharName ?? "Client"}: SubCode={subCode}");

            try
            {
                // Client queries for nearby dynamic entity replication
                if (c.CurMap != null)
                {
                    SendPacket sp = BuildPlayerSpawnPacket(c);
                    c.Send(sp);
                }
            }
            catch (Exception ex)
            {
                DebugSystem.Write(new ExceptionData(ex));
            }
        }

        /// <summary>
        /// Constructs an authentic AC 4 player spawn packet frame matching official wire captures.
        /// Payload: [04, CharID:d, Body:b, Elem:b, Lvl:b, MapID:w, X:w, Y:w, Dir:b, Head:w, Hair:w, Skin:w, Clothes:w, Eyes:w, WornCount:b, Equips[], Padding:5B, Reborn:b, Job:b, Name:s, Nick:s, Mode:b (255), GuildID:d, Flag:b (1)]
        /// </summary>
        public static SendPacket BuildPlayerSpawnPacket(Player player)
        {
            if (player == null) throw new ArgumentNullException(nameof(player));

            SendPacket p = new SendPacket();
            p.Pack8(4);
            p.Pack32(player.CharID);
            p.Pack8((byte)player.Body);
            p.Pack8((byte)player.Element);
            p.Pack8(player.Level);
            p.Pack16((ushort)(player.CurMap?.MapID ?? 0));
            p.Pack16(player.CurX);
            p.Pack16(player.CurY);
            p.Pack8(0); // Direction
            p.Pack16(player.Head);
            p.Pack16(player.HairColor);
            p.Pack16(player.SkinColor);
            p.Pack16(player.ClothingColor);
            p.Pack16(player.EyeColor);
            p.Pack8(player.WornCount);
            p.PackArray(player.Worn_Equips ?? new byte[0]);
            p.Pack32(0);
            p.Pack8(0);
            p.PackBool(player.Reborn);
            p.Pack8((byte)player.Job);
            p.PackString(player.CharName ?? "");
            p.PackString(player.NickName ?? "");
            p.Pack8(255); // Alignment mode byte
            p.Pack32(player.GuildID);
            p.Pack8(1); // Active entity flag
            return p;
        }

        /// <summary>
        /// Constructs an authentic AC 4 GM bot spawn packet frame.
        /// </summary>
        public static SendPacket BuildGmBotSpawnPacket(GmBot bot)
        {
            if (bot == null) throw new ArgumentNullException(nameof(bot));

            SendPacket p = new SendPacket();
            p.Pack8(4);
            p.Pack32(bot.CharID);
            p.Pack8((byte)bot.Body);
            p.Pack8((byte)bot.Element);
            p.Pack8(bot.Level);
            p.Pack16(10019);
            p.Pack16(722);
            p.Pack16(995);
            p.Pack8(0);
            p.Pack16(bot.Head);
            p.Pack16(bot.HairColor);
            p.Pack16(bot.SkinColor);
            p.Pack16(bot.ClothingColor);
            p.Pack16(bot.EyeColor);
            p.Pack8(bot.WornCount);
            p.PackArray(bot.Worn_Equips ?? new byte[0]);
            p.Pack32(0);
            p.Pack8(0);
            p.PackBool(bot.Reborn);
            p.Pack8((byte)bot.Job);
            p.PackString(bot.CharName ?? "");
            p.PackString(bot.NickName ?? "");
            p.Pack8(255);
            p.Pack32(0); // GuildID
            p.Pack8(1); // Active entity flag
            return p;
        }
    }
}
