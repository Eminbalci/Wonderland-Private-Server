using System;
using System.Collections.Generic;
using System.Linq;
using Game;
using Network;

namespace Network.ActionCodes
{
    /// <summary>
    /// AC 71: Carnival Arcade Minigames Protocol.
    /// Handles arcade machine interactions (Pachinko, Whack-a-Mole, High-Low Card Game) and gameplay states.
    /// </summary>
    public class AC71 : AC
    {
        public override int ID => 71;

        private static readonly System.Security.Cryptography.RandomNumberGenerator Rng = System.Security.Cryptography.RandomNumberGenerator.Create();

        // Uniform roll in [0, total) without modulo bias.


        // Uniform roll in [0, total) without modulo bias.
        private static long NextRoll(long total)
        {
            if (total <= 0) return 0;
            byte[] buf = new byte[8];
            ulong limit = ulong.MaxValue - (ulong.MaxValue % (ulong)total);
            while (true)
            {
                Rng.GetBytes(buf);
                ulong v = BitConverter.ToUInt64(buf, 0);
                if (v < limit) return (long)(v % (ulong)total);
            }
        }

        public override void ProcessPkt(Player c, RecievePacket p)
        {
            if (c == null || p == null) return;

            byte subCode = p.B ?? 6;
            DebugSystem.Write($"[AC71] Arcade minigame action from {c.CharName}: SubCode={subCode}");

            try
            {
                // Play (Gonline arcadeCommand): the game must be enabled, the prize must fit the bag,
                // then payment (points, or a fun/play token), then one prize is drawn by weight and added.
                // A play that cannot be paid, drawn or stored changes nothing.
                var game = Game.Experimental.ArcadeGameTable.Find(subCode);
                if (game == null || !game.enabled)
                {
                    c.SendSystemMessage("This machine is unavailable. No payment taken.");
                    c.Send(new byte[] { (byte)ID, subCode, 2 }); // rejected
                    return;
                }
                long total = game.rewards.Sum(rw => rw.weight);
                var rolled = Game.Experimental.ArcadeGameTable.Draw(subCode, NextRoll(total));
                if (rolled == null)
                {
                    c.SendSystemMessage("This reward is unavailable. No payment taken.");
                    c.Send(new byte[] { (byte)ID, subCode, 2 }); // rejected
                    return;
                }
                var prizes = new Dictionary<ushort, int> { { rolled.item_id, rolled.quantity } };
                if (c.Inv == null || !c.Inv.CanAddItems(prizes))
                {
                    c.SendSystemMessage("Please free an inventory slot before playing.");
                    c.Send(new byte[] { (byte)ID, subCode, 5 }); // bag full
                    return;
                }
                // Payment: a play token when the machine takes tokens and the player has one, else points.
                ushort tokenId = 0;
                if (game.token_count > 0 && c.Inv != null)
                {
                    if (c.Inv.ContainsItem(34008)) tokenId = 34008; // play token
                    else if (c.Inv.ContainsItem(34007)) tokenId = 34007; // fun token
                }
                if (tokenId != 0)
                {
                    c.Inv.RemoveItemById(tokenId, game.token_count);
                }
                else
                {
                    int points = Game.PlayerRelated.ItemMallManager.GetUserPoints(c);
                    if (points < game.point_cost)
                    {
                        c.SendSystemMessage("Not enough IM Points for this play. No payment taken.");
                        c.Send(new byte[] { (byte)ID, subCode, 2 }); // rejected
                        return;
                    }
                    Game.PlayerRelated.ItemMallManager.SetUserPoints(c, (int)(points - game.point_cost));
                }
                c.Inv.AddItem(rolled.item_id, rolled.quantity);
                c.Send(new byte[] { (byte)ID, subCode, 4, rolled.index, (byte)rolled.item_id, (byte)(rolled.item_id >> 8), rolled.quantity }); // prize
                DebugSystem.Write($"[AC71] {c.CharName} played kind {subCode}: prize index {rolled.index}, item {rolled.item_id} x{rolled.quantity}, token {tokenId}, cost {game.point_cost}");
            }
            catch (Exception ex)
            {
                DebugSystem.Write(new ExceptionData(ex));
            }
        }
    }
}
