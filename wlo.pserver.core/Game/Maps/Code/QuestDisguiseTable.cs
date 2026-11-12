using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;

namespace Game.Maps.Code
{
    /// <summary>
    /// Quest disguise rules from Wonderland-Gonline (wiki_quest_disguise_defaults.json, StoryQuestDisguise).
    /// Each rule names the map, event and branch it applies to, and the opcode pattern (code 12 transform,
    /// d1 2 = NPC disguise, d2 = template). A matching operation is an authored disguise.
    /// </summary>
    public static class QuestDisguiseTable
    {
        public class Scope
        {
            public ushort[] maps { get; set; }
            public ushort[] events { get; set; }
            public ushort[] branches { get; set; }
            public bool allow_missing { get; set; }
        }

        public class Pattern
        {
            public byte code { get; set; }
            public byte d1 { get; set; }
            public ushort d2 { get; set; }
            public byte d3 { get; set; }
            public byte value { get; set; }
        }

        public class Rule
        {
            public string id { get; set; }
            public string kind { get; set; }
            public Scope scope { get; set; }
            public Pattern pattern { get; set; }
        }

        private static List<Rule> _rules = new List<Rule>();

        public static void Load(string path)
        {
            var loaded = new List<Rule>();
            try
            {
                if (File.Exists(path))
                {
                    var parsed = new JavaScriptSerializer().Deserialize<Rule[]>(File.ReadAllText(path)) ?? new Rule[0];
                    loaded.AddRange(parsed.Where(r => r != null && r.scope != null && r.pattern != null));
                }
            }
            catch (Exception ex)
            {
                DebugSystem.Write("[QuestDisguise] Failed to load rules: " + ex.Message);
            }
            _rules = loaded;
            DebugSystem.Write($"[QuestDisguise] Loaded {_rules.Count} disguise rules.");
        }

        /// <summary>True when an authored disguise rule matches this map, event and operation.</summary>
        public static bool Matches(ushort mapId, ushort eventId, DataFiles.EventSubSubEntry op)
        {
            // Field map (Gonline Op -> C#): Code = DialogPtr, D1..D3 = dialog1..3, Value = dialog4 high byte + unknowndword1.
            return _rules.Any(r =>
                r.scope.maps != null && r.scope.maps.Contains(mapId) &&
                r.scope.events != null && r.scope.events.Contains(eventId) &&
                r.pattern.code == op.DialogPtr &&
                r.pattern.d1 == op.dialog1 &&
                r.pattern.d2 == op.dialog2 &&
                r.pattern.d3 == op.dialog3 &&
                r.pattern.value == ((op.dialog4 >> 8) | (int)(op.unknowndword1 << 8)));
        }
    }
}
