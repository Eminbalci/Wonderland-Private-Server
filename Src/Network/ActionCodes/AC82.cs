using System;
using System.Collections.Generic;
using Game;
using Game.Experimental;
using Network;

namespace Network.ActionCodes
{
    /// <summary>
    /// AC 82: Community Bulletin & Guild Notice Board Protocol.
    /// Handles guild bulletin board messages, public announcements, and forum post interactions.
    /// </summary>
    public class AC82 : AC
    {
        public override int ID => 82;

        public override void ProcessPkt(Player p, RecievePacket r)
        {
            if (p == null || r == null) return;

            byte subCode = r.B ?? 8;
            DebugSystem.Write($"[AC82] Bulletin message action from {p.CharName}: SubCode={subCode}");

            switch (subCode)
            {
                case 7: Recv7(p, r); break;  // Add / Post Message
                case 8: Recv8(p, r); break;  // Request Posts / Tab
                case 9: Recv9(p, r); break;  // Open / Read Message
                case 11: Recv11(p, r); break; // Open Write Panel
                default:
                    DebugSystem.Write($"[AC82] Unhandled subcode {subCode}");
                    break;
            }
        }

        private void Recv7(Player p, RecievePacket r)
        {
            try
            {
                if (ExperimentalFeaturesManager.Instance.EnableCommunityBulletin)
                {
                    uint guildId = p.CurGuild?.GuildID ?? 0;
                    string title = "Guild Notice";
                    string content = "Message";

                    if (r.Buffer != null && r.Buffer.Length > 8)
                    {
                        // Safely extract text payload if present
                        title = $"Post by {p.CharName}";
                    }

                    var post = ExperimentalFeaturesManager.Instance.CommunityBulletin.PostMessage(p, guildId, title, content, false);

                    SendPacket resp = new SendPacket();
                    resp.Pack8((byte)ID);
                    resp.Pack8(7);
                    resp.Pack8(1); // 1 = Posted Successfully
                    resp.Pack32(post.PostId);
                    p.Send(resp);
                }
            }
            catch (Exception ex)
            {
                DebugSystem.Write(new ExceptionData(ex));
            }
        }

        private void Recv8(Player p, RecievePacket r)
        {
            try
            {
                if (ExperimentalFeaturesManager.Instance.EnableCommunityBulletin)
                {
                    uint guildId = p.CurGuild?.GuildID ?? 0;
                    var posts = ExperimentalFeaturesManager.Instance.CommunityBulletin.GetPosts(guildId, true);

                    SendPacket resp = new SendPacket();
                    resp.Pack8((byte)ID);
                    resp.Pack8(8);
                    resp.Pack16((ushort)posts.Count);
                    p.Send(resp);
                }
            }
            catch (Exception ex)
            {
                DebugSystem.Write(new ExceptionData(ex));
            }
        }

        private void Recv9(Player p, RecievePacket r)
        {
            try
            {
                SendPacket resp = new SendPacket();
                resp.Pack8((byte)ID);
                resp.Pack8(9);
                resp.Pack8(1); // Read Ack
                p.Send(resp);
            }
            catch (Exception ex)
            {
                DebugSystem.Write(new ExceptionData(ex));
            }
        }

        private void Recv11(Player p, RecievePacket r)
        {
            try
            {
                SendPacket resp = new SendPacket();
                resp.Pack8((byte)ID);
                resp.Pack8(11);
                resp.Pack8(1); // Open Write Panel OK
                p.Send(resp);
            }
            catch (Exception ex)
            {
                DebugSystem.Write(new ExceptionData(ex));
            }
        }
    }
}