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

            // Normalize 24-bit composite packet IDs (e.g. 0x82B3E -> 0x2B3E = 11070)
            uint lookupId = (rawTalkId > 0xFFFF && (rawTalkId & 0xFFFF) >= 10000) ? (rawTalkId & 0xFFFF) : rawTalkId;

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
            string method = "Unknown";

            // =========================================================================
            // 1. SYSTEM & INTERACTIVE WORLD OBJECTS (11000..11100)
            // =========================================================================
            if (lookupId >= 11000 && lookupId <= 11100)
            {
                recordIdx = (int)(lookupId - 11000);
                method = "System_Section";
            }
            // =========================================================================
            // 2. STORYLINE & COMPANION PROGRESSION (20000..29999)
            // =========================================================================
            else if (lookupId >= 20000 && lookupId <= 29999)
            {
                recordIdx = (int)(lookupId - 18904);
                method = "Story_ProgressionSection";
            }
            // =========================================================================
            // 3. WORLD, TOWNS, VILLAGES & QUEST DIALOGUES (30000..49999)
            // =========================================================================
            else if (lookupId >= 30000 && lookupId <= 49999)
            {
                recordIdx = (int)(lookupId - 23105);
                method = "World_VillageSection";
            }

            if (recordIdx >= 0 && dat.TryGetByRecordIndex((uint)recordIdx, out rawText))
            {
                return BuildResult(result, rawText, recordIdx, method, playerName);
            }

            // =========================================================================
            // 3. DIRECT BYTE OFFSET IN TALK.DAT
            // =========================================================================
            if (dat.TryGetByOffset(rawTalkId, out rawText))
            {
                return BuildResult(result, rawText, -1, "DirectByteOffset", playerName);
            }

            // =========================================================================
            // 4. DIRECT RECORD INDEX (0..17,494)
            // =========================================================================
            if (rawTalkId < 18000 && dat.TryGetByRecordIndex(rawTalkId, out rawText))
            {
                return BuildResult(result, rawText, (int)rawTalkId, "DirectRecordIndex", playerName);
            }

            // =========================================================================
            // 5. UNIVERSAL EVE CHAPTER BASE OFFSETS (60000+, 50000+, 40000+, 30000+, 20000+)
            // =========================================================================
            uint[] chapterBases = { 60000, 50000, 40000, 30000, 20000 };
            foreach (var b in chapterBases)
            {
                if (rawTalkId >= b)
                {
                    uint calculatedIdx = rawTalkId - b;
                    if (calculatedIdx < 18000 && dat.TryGetByRecordIndex(calculatedIdx, out rawText))
                    {
                        return BuildResult(result, rawText, (int)calculatedIdx, $"ChapterBase_{b}", playerName);
                    }
                }
            }

            // =========================================================================
            // 6. RAW TALK ID HEADER / 16-BIT MASKED LOOKUP
            // =========================================================================
            if (dat.TryGetById(rawTalkId, out rawText))
            {
                return BuildResult(result, rawText, -1, "HeaderTalkId", playerName);
            }

            uint pureId = rawTalkId & 0xFFFF;
            if (pureId > 0 && pureId != rawTalkId && dat.TryGetById(pureId, out rawText))
            {
                return BuildResult(result, rawText, -1, "Masked16BitId", playerName);
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
