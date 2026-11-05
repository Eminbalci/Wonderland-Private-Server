using System;
using System.Threading;
using Game.DataFiles;
using RCLibrary.Core;

namespace Game.Battle
{
    public static class BattleBackdropManager
    {
        public const byte CompatibilitySelector = 1;
        public const byte CompatibilityFormationMetadata = 0;
        private static GroundBackdropCatalog catalog = GroundBackdropCatalog.Empty;

        // Called during asset initialization, before accepting players. Publish
        // the complete immutable catalog atomically; never parse archives per fight.
        public static void Initialize()
        {
            Initialize(PathHelper.GetDataFilePath("Ground.MMG"));
        }

        public static void Initialize(string path)
        {
            try
            {
                var loaded = GroundBackdropCatalog.Load(path);
                Interlocked.Exchange(ref catalog, loaded);
                DebugSystem.Write("[BattleBackdrop] Loaded Ground.MMG backdrops for " + loaded.Maps.Count + " maps.");
            }
            catch (Exception ex)
            {
                // Missing/corrupt optional archive leaves the previous complete
                // snapshot intact; a first-load failure uses compatibility scenery.
                DebugSystem.Write("[BattleBackdrop] Cannot load " + path + ": " + ex.Message + "; retaining compatibility/previous backdrop definitions.");
            }
        }

        public static byte Resolve(Player player)
        {
            if (player == null || player.CurMap == null) return CompatibilitySelector;
            return Resolve((ushort)player.CurMap.MapID, player.CurX, player.CurY);
        }

        public static byte Resolve(ushort mapId, int x, int y)
        {
            byte selector;
            if (Volatile.Read(ref catalog).TryResolve(mapId, x, y, out selector)) return selector;
            // Preserve meaningful low map selectors; missing scenes are handled
            // by the native client's existing fallback to scene 59081.
            return mapId <= byte.MaxValue ? (byte)mapId : CompatibilitySelector;
        }
    }
}
