using System;
using System.Collections.Generic;
using Game;
using Game.Experimental;
using Game.PlayerRelated;
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
                case 1: Recv1(p, r); break;  // Native Post
                case 2: Recv2(p, r); break;  // Native Page
                case 3: Recv3(p, r); break;  // Native Read
                case 4: Recv4(p, r); break;  // Native Delete
                case 7: Recv7(p, r); break;  // Add / Post Message
                case 8: Recv8(p, r); break;  // Request Posts / Tab
                case 9: Recv9(p, r); break;  // Open / Read Message
                case 11: Recv11(p, r); break; // Open Write Panel
                default:
                    DebugSystem.Write($"[AC82] Unhandled subcode {subCode}");
                    break;
            }
        }

        private void Recv1(Player p, RecievePacket r)
        {
            try
            {
                if (r.GetPtr() < 6) r.SetPtr(6);
                int rem = r.Buffer.Length - r.GetPtr();
                if (rem < 4) return;
                uint titleLen = r.Unpack32();
                if (titleLen < 1 || titleLen > 30) return;
                if (r.Buffer.Length - r.GetPtr() < titleLen) return;
                byte[] title = new byte[titleLen];
                Array.Copy(r.Buffer, r.GetPtr(), title, 0, (int)titleLen);
                r.SetPtr(r.GetPtr() + (int)titleLen);

                if (r.Buffer.Length - r.GetPtr() < 4) return;
                uint contentLen = r.Unpack32();
                if (contentLen < 1 || contentLen > 151) return;
                if (r.Buffer.Length - r.GetPtr() < contentLen) return;
                byte[] content = new byte[contentLen];
                Array.Copy(r.Buffer, r.GetPtr(), content, 0, (int)contentLen);
                r.SetPtr(r.GetPtr() + (int)contentLen);

                int trailing = r.Buffer.Length - r.GetPtr();
                if (trailing == 1)
                {
                    if (r.Unpack8() != 1) return;
                }
                else if (trailing != 0)
                {
                    return;
                }

                byte[] authorName = System.Text.Encoding.ASCII.GetBytes(p.CharName ?? "");
                if (authorName.Length < 1 || authorName.Length > 14) return;

                long nowMillis = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                var db = DataBase.CharacterDataBase.GlobalInstance;
                if (db == null) return;

                int result = db.CreateMessageBoardPost(p.CharID, authorName, title, content, nowMillis, out _);
                if (result == 1)
                {
                    p.Send(Tools.FromFormat("bb", 82, 5));
                }
                else if (result == 2)
                {
                    p.Send(Tools.FromFormat("bb", 82, 6));
                }
                else if (result == 0)
                {
                    p.Send(Tools.FromFormat("bbb", 82, 8, 1));
                    SendPageReply(p, 1, nowMillis);
                }
            }
            catch (Exception ex)
            {
                DebugSystem.Write(new ExceptionData(ex));
            }
        }

        private void Recv2(Player p, RecievePacket r)
        {
            try
            {
                if (r.GetPtr() < 6) r.SetPtr(6);
                int remaining = r.Buffer.Length - r.GetPtr();
                if (remaining < 0 || remaining > 2) return;

                byte page = 1;
                if (remaining >= 1)
                {
                    page = r.Unpack8();
                    if (page == 0) return;
                }
                if (remaining == 2)
                {
                    byte second = r.Unpack8();
                    if (second != 1) return;
                }

                long nowMillis = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                SendPageReply(p, page, nowMillis);
            }
            catch (Exception ex)
            {
                DebugSystem.Write(new ExceptionData(ex));
            }
        }

        private void Recv3(Player p, RecievePacket r)
        {
            try
            {
                if (r.GetPtr() < 6) r.SetPtr(6);
                int remaining = r.Buffer.Length - r.GetPtr();
                if (remaining != 4 && remaining != 17) return;

                uint id = r.Unpack32();
                if (remaining == 17)
                {
                    uint page = r.Unpack32();
                    double date = r.UnpackDouble();
                    byte flag = r.Unpack8();
                    if (flag != 1) return;
                }

                long nowMillis = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                var db = DataBase.CharacterDataBase.GlobalInstance;
                if (db == null) return;

                var post = db.MessageBoardEntry(id, nowMillis);
                if (post != null)
                {
                    p.Send(MessageBoardWire.BuildContent(post));
                }
                else
                {
                    p.Send(Tools.FromFormat("bb", 82, 7));
                }
            }
            catch (Exception ex)
            {
                DebugSystem.Write(new ExceptionData(ex));
            }
        }

        private void Recv4(Player p, RecievePacket r)
        {
            try
            {
                if (r.GetPtr() < 6) r.SetPtr(6);
                int remaining = r.Buffer.Length - r.GetPtr();
                if (remaining != 4 && remaining != 17) return;

                uint id = r.Unpack32();
                if (remaining == 17)
                {
                    uint page = r.Unpack32();
                    double date = r.UnpackDouble();
                    byte flag = r.Unpack8();
                    if (flag != 1) return;
                }

                long nowMillis = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                var db = DataBase.CharacterDataBase.GlobalInstance;
                if (db == null) return;

                bool deleted = db.DeleteMessageBoardPost(p.CharID, id, nowMillis);
                if (!deleted)
                {
                    p.Send(Tools.FromFormat("bb", 82, 7));
                }
                else
                {
                    SendPageReply(p, 1, nowMillis);
                }
            }
            catch (Exception ex)
            {
                DebugSystem.Write(new ExceptionData(ex));
            }
        }

        private void SendPageReply(Player p, byte page, long nowMillis)
        {
            var db = DataBase.CharacterDataBase.GlobalInstance;
            if (db == null) return;

            int offset = (page - 1) * MessageBoardWire.PageSize;
            var posts = db.MessageBoardPage(offset, MessageBoardWire.PageSize, nowMillis, out long count);
            int lastPage = MessageBoardWire.LastPage(count);
            if (page > lastPage)
            {
                page = (byte)Math.Min((int)byte.MaxValue, lastPage);
                offset = (page - 1) * MessageBoardWire.PageSize;
                posts = db.MessageBoardPage(offset, MessageBoardWire.PageSize, nowMillis, out _);
            }

            p.Send(MessageBoardWire.BuildPagination((uint)count, page));
            p.Send(MessageBoardWire.BuildEntries(posts));
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
