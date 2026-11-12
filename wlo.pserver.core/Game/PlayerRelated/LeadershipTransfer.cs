using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.PlayerRelated
{
    /// <summary>
    /// Team leadership transfer confirmation, as in Wonderland-Gonline (partyLeadershipTransfer, 5 s timeout).
    /// The leader offers (AC13 sub 15 status 1); only the candidate's acceptance (AC13 sub 1 status 3)
    /// commits the transfer. Refusal, timeout, cancellation and busy states are reported separately.
    /// </summary>
    public static class LeadershipTransfer
    {
        public const int TimeoutMilliseconds = 5000;

        public const byte NoticeOffered = 1;
        public const byte NoticeStatus = 2;
        public const byte NoticeChanged = 3;
        public const byte StatusRefused = 2;
        public const byte StatusTimedOut = 3;
        public const byte StatusCancelled = 4;
        public const byte StatusBusy = 5;

        public const byte ResponseDeclined = 2;
        public const byte ResponseAccepted = 3;

        public sealed class Pending
        {
            public uint LeaderId;
            public uint CandidateId;
            public DateTime Deadline;
            public System.Threading.Timer Timer;
        }

        private static readonly Dictionary<uint, Pending> _byLeader = new Dictionary<uint, Pending>();
        private static readonly object _lock = new object();

        /// <summary>Starts an offer. Returns false when the leader already has an open offer.</summary>
        /// <summary>Called with (leaderId, candidateId) when an unanswered offer times out.</summary>
        public static Action<uint, uint> OnExpired;

        public static bool Offer(uint leaderId, uint candidateId, DateTime now)
        {
            lock (_lock)
            {
                if (_byLeader.ContainsKey(leaderId)) return false;
                var pending = new Pending { LeaderId = leaderId, CandidateId = candidateId, Deadline = now.AddMilliseconds(TimeoutMilliseconds) };
                _byLeader[leaderId] = pending;
                // One-shot timer per offer: fires after the timeout and expires it if still open.
                var timer = new System.Threading.Timer(_ => ExpireOne(pending), null, TimeoutMilliseconds, System.Threading.Timeout.Infinite);
                pending.Timer = timer;
                return true;
            }
        }

        private static void ExpireOne(Pending pending)
        {
            bool stillOpen;
            lock (_lock)
            {
                stillOpen = _byLeader.TryGetValue(pending.LeaderId, out var current) && current == pending;
                if (stillOpen) _byLeader.Remove(pending.LeaderId);
            }
            if (stillOpen && OnExpired != null) OnExpired(pending.LeaderId, pending.CandidateId);
        }

        /// <summary>Candidate accepts: returns the pending offer once, removing it.</summary>
        public static bool TryAccept(uint candidateId, out uint leaderId)
        {
            leaderId = 0;
            lock (_lock)
            {
                var entry = _byLeader.Values.FirstOrDefault(x => x.CandidateId == candidateId);
                if (entry == null) return false;
                _byLeader.Remove(entry.LeaderId);
                leaderId = entry.LeaderId;
                return true;
            }
        }

        /// <summary>Candidate declines or the leader cancels; returns the leader id when an offer was open.</summary>
        public static bool Clear(uint memberId, out uint leaderId)
        {
            leaderId = 0;
            lock (_lock)
            {
                var entry = _byLeader.Values.FirstOrDefault(x => x.LeaderId == memberId || x.CandidateId == memberId);
                if (entry == null) return false;
                _byLeader.Remove(entry.LeaderId);
                leaderId = entry.LeaderId;
                return true;
            }
        }

        /// <summary>Removes and returns the offers whose deadline passed.</summary>
        public static List<Pending> Expire(DateTime now)
        {
            var expired = new List<Pending>();
            lock (_lock)
            {
                foreach (var entry in _byLeader.Values.Where(x => x.Deadline <= now).ToList())
                {
                    _byLeader.Remove(entry.LeaderId);
                    expired.Add(entry);
                }
            }
            return expired;
        }

        public static bool IsOffering(uint memberId)
        {
            lock (_lock) { return _byLeader.ContainsKey(memberId) || _byLeader.Values.Any(x => x.CandidateId == memberId); }
        }
    }
}
