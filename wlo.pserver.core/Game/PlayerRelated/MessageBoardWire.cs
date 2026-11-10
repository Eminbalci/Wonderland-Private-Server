using System;
using System.Collections.Generic;
using Network;

namespace Game.PlayerRelated
{
    public class MessageBoardPost
    {
        public uint Id;
        public uint AuthorId;
        public byte[] AuthorName;
        public byte[] Title;
        public byte[] Content;
        public long PostedAtMillis;
    }

    public static class MessageBoardWire
    {
        public const int PageSize = 10;
        public const int PostsPerAuthor = 3;
        public const int MaxPosts = 2550;
        public const int TitleMaxBytes = 30;
        public const int ContentMaxBytes = 151;
        public const int AuthorMaxBytes = 14;
        public const long LifetimeMillis = 86400000;

        public static double OleDate(long unixMillis) => unixMillis / 86400000.0 + 25569.0;

        public static bool ValidText(byte[] text, int limit, bool multiline)
        {
            if (text == null || text.Length < 1 || text.Length > limit)
                return false;

            for (int i = 0; i < text.Length; i++)
            {
                byte b = text[i];
                if (b == 127) return false;
                if (b < 32)
                {
                    if (!multiline) return false;
                    if (b != 10 && b != 13 && b != 9) return false;
                }
            }
            return true;
        }

        public static int LastPage(long count) => (int)Math.Max(1, (count + 9) / 10);

        public static byte[] BuildPagination(uint count, byte page)
        {
            SendPacket p = new SendPacket();
            p.Pack8(82);
            p.Pack8(4);
            p.Pack32(count);
            p.Pack8(page);
            return p.Buffer;
        }

        public static byte[] BuildEntries(IList<MessageBoardPost> posts)
        {
            SendPacket p = new SendPacket();
            p.Pack8(82);
            p.Pack8(1);
            if (posts != null)
            {
                foreach (var post in posts)
                {
                    if (post == null) continue;
                    uint authorLen = (uint)(post.AuthorName != null ? post.AuthorName.Length : 0);
                    p.Pack32(authorLen);
                    if (authorLen > 0)
                        p.PackArray(post.AuthorName);

                    uint titleLen = (uint)(post.Title != null ? post.Title.Length : 0);
                    p.Pack32(titleLen);
                    if (titleLen > 0)
                        p.PackArray(post.Title);

                    p.PackDouble(OleDate(post.PostedAtMillis));
                    p.Pack32(post.AuthorId);
                    p.Pack32(post.Id);
                }
            }
            return p.Buffer;
        }

        public static byte[] BuildContent(MessageBoardPost post)
        {
            SendPacket p = new SendPacket();
            p.Pack8(82);
            p.Pack8(2);
            if (post != null)
            {
                uint authorLen = (uint)(post.AuthorName != null ? post.AuthorName.Length : 0);
                p.Pack32(authorLen);
                if (authorLen > 0)
                    p.PackArray(post.AuthorName);

                uint titleLen = (uint)(post.Title != null ? post.Title.Length : 0);
                p.Pack32(titleLen);
                if (titleLen > 0)
                    p.PackArray(post.Title);

                p.PackDouble(OleDate(post.PostedAtMillis));

                uint contentLen = (uint)(post.Content != null ? post.Content.Length : 0);
                p.Pack32(contentLen);
                p.Pack32(post.AuthorId);
                if (contentLen > 0)
                    p.PackArray(post.Content);
            }
            return p.Buffer;
        }
    }
}
