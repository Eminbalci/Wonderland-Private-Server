using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Game.Code;
using Network;

namespace Game.Experimental
{
    public class BulletinPost
    {
        public uint PostId { get; set; }
        public uint AuthorCharId { get; set; }
        public string AuthorName { get; set; }
        public uint GuildId { get; set; }
        public string Title { get; set; }
        public string Content { get; set; }
        public bool IsGlobal { get; set; }
        public DateTime PostedAt { get; set; }

        public BulletinPost(uint id, Player author, uint guildId, string title, string content, bool isGlobal)
        {
            PostId = id;
            AuthorCharId = author?.CharID ?? 0;
            AuthorName = author?.CharName ?? "Anonymous";
            GuildId = guildId;
            Title = title ?? "Notice";
            Content = content ?? string.Empty;
            IsGlobal = isGlobal;
            PostedAt = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Community Blackboard Bulletin and Guild Notice Board Engine (AC 82 / Spec 57 & 99).
    /// Handles public announcements, guild messages, and notice board management.
    /// </summary>
    public class CommunityBulletinEngine
    {
        private readonly ConcurrentDictionary<uint, BulletinPost> m_posts = new ConcurrentDictionary<uint, BulletinPost>();
        private uint m_nextPostId = 100;
        private readonly object m_postLock = new object();

        public int TotalPostsCount => m_posts.Count;

        public CommunityBulletinEngine()
        {
            // Seed welcome notice
            m_posts[1] = new BulletinPost(1, null, 0, "Welcome to Wonderland Online", "Server is operational. Check out the Experimental Lab for new features!", true);
            m_posts[2] = new BulletinPost(2, null, 0, "Festival Races Active", "Visit South Island coastal circuit to participate in seasonal races.", true);
        }

        public BulletinPost PostMessage(Player author, uint guildId, string title, string content, bool isGlobal)
        {
            if (author == null && !isGlobal) return null;

            lock (m_postLock)
            {
                uint id = ++m_nextPostId;
                var post = new BulletinPost(id, author, guildId, title, content, isGlobal);
                m_posts[id] = post;

                DebugSystem.Write($"[CommunityBulletinEngine] New Post #{id} by {post.AuthorName}: '{post.Title}' (Global: {isGlobal}, Guild: {guildId})");
                return post;
            }
        }

        public BulletinPost BroadcastGlobalAnnouncement(string title, string content)
        {
            return PostMessage(null, 0, title, content, true);
        }

        public IReadOnlyList<BulletinPost> GetPosts(uint guildId, bool includeGlobal = true)
        {
            return m_posts.Values
                .Where(p => (includeGlobal && p.IsGlobal) || (guildId > 0 && p.GuildId == guildId))
                .OrderByDescending(p => p.PostedAt)
                .Take(50)
                .ToList()
                .AsReadOnly();
        }

        public bool DeletePost(Player requester, uint postId)
        {
            if (requester == null) return false;

            if (m_posts.TryGetValue(postId, out var post))
            {
                if (post.AuthorCharId == requester.CharID || requester.GM) // Author or GM
                {
                    return m_posts.TryRemove(postId, out _);
                }
            }
            return false;
        }

        /// <summary>
        /// S->C AC 82:8 Sends bulletin tab list to player
        /// </summary>
        public void SendTabList(Player player, uint guildId)
        {
            if (player == null) return;

            try
            {
                var posts = GetPosts(guildId, true);
                SendPacket p = new SendPacket();
                p.Pack8(82);
                p.Pack8(8); // Tab response
                p.Pack8((byte)posts.Count);

                foreach (var post in posts)
                {
                    p.Pack32(post.PostId);
                    p.Pack32(post.AuthorCharId);
                    p.PackString(post.Title);
                }
                player.Send(p);
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[CommunityBulletinEngine] SendTabList error: {ex.Message}");
            }
        }

        /// <summary>
        /// S->C AC 82:9 Sends full post message content
        /// </summary>
        public void SendPostContent(Player player, uint postId)
        {
            if (player == null) return;

            if (m_posts.TryGetValue(postId, out var post))
            {
                SendPacket p = new SendPacket();
                p.Pack8(82);
                p.Pack8(9); // Message content response
                p.Pack32(post.PostId);
                p.PackString(post.AuthorName);
                p.PackString(post.Title);
                p.PackString(post.Content);
                player.Send(p);
            }
        }
    }
}
