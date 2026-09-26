using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace DataFiles
{
    /// <summary>
    /// Represents the comprehensive resolution result of a Talk ID lookup.
    /// </summary>
    public class TalkResolutionResult
    {
        public uint RawId { get; set; }
        public int RecordIndex { get; set; } = -1;
        public string RawText { get; set; }
        public string FormattedText { get; set; }
        public string SoundEffect { get; set; }
        public int SpeakerFaceIndex { get; set; } = -1;
        public string ResolutionMethod { get; set; }
        public bool Success { get; set; }

        public override string ToString()
        {
            return Success
                ? $"[Talk #{RawId} -> Rec #{RecordIndex} via {ResolutionMethod}]: \"{FormattedText}\""
                : $"[Talk #{RawId} -> FAILED]";
        }
    }

    /// <summary>
    /// High-performance, universal Talk ID Resolution Engine.
    /// Maps authentic Eve.emg bytecode dialogue IDs to English Talk.dat records
    /// with full token replacement (#n/#n, #s sound effects, #f portraits, and #R/#B colors).
    /// </summary>
    public static class TalkResolver
    {
        private static PhxTalkDat _talkDatInstance;
        private static readonly object _syncLock = new object();

        // Regex for token parsing
        private static readonly Regex _soundRegex = new Regex(@"#s([a-zA-Z0-9_-]+)/#s", RegexOptions.Compiled);
        private static readonly Regex _faceRegex = new Regex(@"#f([0-9]+)/#f", RegexOptions.Compiled);
        private static readonly Regex _colorRegex = new Regex(@"#[RGBYrgby](.*?)/#[RGBYrgby]", RegexOptions.Compiled);
        private static readonly Regex _generalTagRegex = new Regex(@"#[a-zA-Z0-9]+/[#a-zA-Z0-9]+", RegexOptions.Compiled);

        /// <summary>
        /// Registers or updates the active PhxTalkDat database instance.
        /// </summary>
        public static void Initialize(PhxTalkDat talkDat)
        {
            lock (_syncLock)
            {
                _talkDatInstance = talkDat;
            }
        }

        /// <summary>
        /// Resolves a raw Talk ID or bytecode offset to its formatted dialogue string.
        /// </summary>
        public static string Resolve(uint rawTalkId, string playerName = null, bool stripFormatting = false)
        {
            var res = ResolveDetailed(rawTalkId, playerName);
            if (!res.Success) return null;

            return stripFormatting ? StripFormatting(res.FormattedText) : res.FormattedText;
        }

        /// <summary>
        /// Attempts to resolve a Talk ID, returning true if found.
        /// </summary>
        public static bool TryResolve(uint rawTalkId, out string dialogue, string playerName = null, bool stripFormatting = false)
        {
            dialogue = Resolve(rawTalkId, playerName, stripFormatting);
            return !string.IsNullOrEmpty(dialogue);
        }

        /// <summary>
        /// Performs deep resolution of a Talk ID, extracting sound cues, face indexes, and metadata.
        /// </summary>
        public static TalkResolutionResult ResolveDetailed(uint rawTalkId, string playerName = null)
        {
            var result = new TalkResolutionResult
            {
                RawId = rawTalkId,
                Success = false
            };

            if (rawTalkId == 0) return result;

            uint lookupId = rawTalkId;

            PhxTalkDat dat;
            lock (_syncLock)
            {
                dat = _talkDatInstance;
            }

            if (dat == null)
            {
                result.ResolutionMethod = "DatabaseNotInitialized";
                return result;
            }

            string rawText = null;
            int recordIdx = -1;

            // EVE operands are Talk.dat IDs, not record positions. Resolve the
            // decrypted header before considering an explicit byte offset.
            if (dat.TryGetById(lookupId, out rawText))
            {
                recordIdx = dat.GetOffset(lookupId) / 292;
                return BuildResult(result, rawText, recordIdx, "HeaderTalkId", playerName);
            }

            // Keep the legacy explicit-offset API, but never guess a different
            // dialogue from a missing 16-bit ID or a chapter/record offset.
            if (rawTalkId > ushort.MaxValue && dat.TryGetByOffset(rawTalkId, out rawText))
            {
                return BuildResult(result, rawText, -1, "DirectByteOffset", playerName);
            }

            return result;
        }

        private static TalkResolutionResult BuildResult(TalkResolutionResult res, string rawText, int recordIndex, string method, string playerName)
        {
            res.RawText = rawText;
            res.RecordIndex = recordIndex;
            res.ResolutionMethod = method;
            res.Success = true;

            // Extract sound effect
            var soundMatch = _soundRegex.Match(rawText);
            if (soundMatch.Success)
            {
                res.SoundEffect = soundMatch.Groups[1].Value;
            }

            // Extract face index
            var faceMatch = _faceRegex.Match(rawText);
            if (faceMatch.Success && int.TryParse(faceMatch.Groups[1].Value, out int faceIdx))
            {
                res.SpeakerFaceIndex = faceIdx;
            }

            // Format dialogue with player name
            res.FormattedText = FormatTokens(rawText, playerName);
            return res;
        }

        /// <summary>
        /// Replaces dynamic in-game tokens (#n/#n for character name).
        /// </summary>
        public static string FormatTokens(string text, string playerName)
        {
            if (string.IsNullOrEmpty(text)) return "";

            string pName = !string.IsNullOrEmpty(playerName) ? playerName : "Adventurer";
            string formatted = text.Replace("#n/#n", pName).Replace("#n", pName);
            return formatted;
        }

        /// <summary>
        /// Strips formatting tags (#R, #B, #f, #s) for plain-text presentation.
        /// </summary>
        public static string StripFormatting(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";

            string clean = _soundRegex.Replace(text, "");
            clean = _faceRegex.Replace(clean, "");
            clean = _colorRegex.Replace(clean, "$1");
            clean = _generalTagRegex.Replace(clean, "");
            return clean.Trim();
        }
    }
}
