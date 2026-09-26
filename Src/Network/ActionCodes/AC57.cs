using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Game;
using Game.QuestRelated;
using Network;

namespace Network.ActionCodes
{
    /// <summary>
    /// ActionCode 57 (0x39 Hex): Official Wonderland Online Minigame System.
    /// Handles minigame start, player win/loss results, victory fanfares, and quest rewards.
    /// Verified from official packet captures:
    /// - Type 3 (Whack-a-Mole / Seed 0x012AF8): 'minigameyikaybettim.pcapng' & 'minigameyikazandim.pcapng'
    /// - Type 4 (Woodcutting / Archery / Target / Seed 0x012710): 'baskabirminigamekaybettim.pcapng' & 'baskabirminigamekazandim.pcapng'
    /// </summary>
    public class AC57 : AC
    {
        public override int ID { get { return 57; } } // 0x39 in Hex

        public override void ProcessPkt(Player p, RecievePacket r)
        {
            if (p == null || r == null) return;

            byte sub = r.B ?? 0;
            switch (sub)
            {
                case 1:
                    Recv1(p, r);
                    break;
                default:
                    DebugSystem.Write($"[AC57] Unhandled Minigame subcode: AC 57,{sub}");
                    break;
            }
        }

        /// <summary>
        /// Handles minigame conclusion report sent from client.
        /// Result: 0 = Lost / Failed, 1 = Won / Passed.
        /// </summary>
        private void Recv1(Player p, RecievePacket r)
        {
            try
            {
                // A result is one byte. Malformed or late packets must not finish a game.
                if (r.Buffer.Length != 7 || r[6] > 1) return;
                var won = p.OnMinigameWon;
                var lost = p.OnMinigameLost;
                if (won == null && lost == null) return;
                bool native = p.NativeEventActive;
                bool isWin = r[6] == 1;
                // Consume both callbacks before invoking: the callback can start another game.
                p.OnMinigameWon = null;
                p.OnMinigameLost = null;
                p.Send(Tools.FromFormat("bb", 57, 2));
                if (isWin) won?.Invoke();
                else lost?.Invoke();
                // The native event owns rewards and unlocking, even if it finished synchronously.
                if (native) return;
                if (isWin) p.Send(Tools.FromFormat("bb", 20, 10));
                else p.SendSystemMessage(" Minigame failed. You can try again anytime!");
                if (p.OnMinigameWon == null && p.OnMinigameLost == null && p.OnInteractionComplete == null)
                    p.CancelInteraction();
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[AC57.Recv1] Error processing minigame result: {ex.Message}");
                p.CancelInteraction();
            }
        }

        /// <summary>
        /// Starts an authentic minigame session on the client.
        /// </summary>
        /// <param name="p">Target Player</param>
        /// <param name="gameType">Minigame ID (3 = Whack-a-Mole, 4 = Woodcutting/Target, 1 = Mining, etc.)</param>
        /// <param name="seed">Seed / Difficulty parameter</param>
        /// <param name="questId">Optional QuestID to automatically advance upon winning</param>
        /// <param name="onWon">Victory callback</param>
        /// <param name="onLost">Loss callback</param>
        public static void LaunchMinigame(Player p, byte gameType = 3, uint seed = 0x012AF8, uint questId = 0, Action onWon = null, Action onLost = null)
        {
            if (p == null) return;

            p.OnMinigameWon = () =>
            {
                if (questId > 0)
                {
                    QuestManager.SendQuestUpdate(p, questId, QuestState.InProgress, 1);
                }
                onWon?.Invoke();
            };
            p.OnMinigameLost = onLost;

            // Frame 0578 / 0096: Start Minigame (AC 57 Sub 1) + Minigame Mode Lock (AC 20 Sub 9)
            SendPacket startPkt = new SendPacket();
            startPkt.PackArray(new byte[] { 57, 1, gameType });
            startPkt.Pack8((byte)(seed & 0xFF));
            startPkt.Pack8((byte)((seed >> 8) & 0xFF));
            startPkt.Pack8((byte)((seed >> 16) & 0xFF));
            p.Send(startPkt);

            SendPacket lockPkt = new SendPacket();
            lockPkt.PackArray(new byte[] { 20, 9 });
            p.Send(lockPkt);

            DebugSystem.Write($"[AC57] Launched Minigame (Type: {gameType}, Seed: 0x{seed:X}, QuestID: {questId}) for {p.CharName}.");
        }
    }
}
