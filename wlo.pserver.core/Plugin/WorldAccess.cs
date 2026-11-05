using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Game;
using Game.Maps;
using Network;

namespace Plugin
{
    /// <summary>
    /// Exposes world server host operations to plugins and server subsystem services.
    /// </summary>
    public interface WorldServerHost
    {
        /// <summary>
        /// Retrieves an online player by character ID, or null if not found.
        /// </summary>
        Player GetPlayer(uint charId);

        /// <summary>
        /// Checks if a player with the given character ID is currently online.
        /// </summary>
        bool isOnline(uint charId);

        /// <summary>
        /// Gracefully disconnects a player by character ID.
        /// </summary>
        void DisconnectPlayer(uint charId);

        /// <summary>
        /// Broadcasts a network packet to all active maps and players in the world.
        /// </summary>
        void Broadcast(SendPacket pkt);

        /// <summary>
        /// Broadcasts a network packet with optional single-target routing or exclusion.
        /// </summary>
        void Broadcast(SendPacket pkt, uint? directTo = null, bool exclude = false);

        /// <summary>
        /// Returns an enumerable snapshot of all online players currently in the world.
        /// </summary>
        IEnumerable<Player> GetAllPlayers();
    }
}
