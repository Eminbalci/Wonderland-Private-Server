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
                byte result = (r != null && r.Buffer.Length > 6) ? r[6] : (byte)0;
                bool isWin = (result == 1);

                DebugSystem.Write($"[AC57.Recv1] Player {p.CharName} finished minigame. Result: {(isWin ? "WON (1)" : "LOST (0)")}");

                // 1. Always ACK with AC 57 Sub 2 (Minigame Ended)
                SendPacket endPkt = new SendPacket();
                endPkt.PackArray(new byte[] { 57, 2 });
                p.Send(endPkt);

                if (isWin)
                {
                    // 2. Victory Flow (Frame 3597):
                    // Execute pending victory callback / quest reward (which sends AC 23:6)
                    try
                    {
                        p.OnMinigameWon?.Invoke();
                    }
                    catch (Exception ex)
                    {
                        DebugSystem.Write($"[AC57.Recv1] Exception in OnMinigameWon: {ex.Message}");
                    }
                    p.OnMinigameWon = null;

                    p.Send(Tools.FromFormat("bbwb", 24, 5, 0x0044, 1));
                    p.Send(Tools.FromFormat("bbb", 6, 2, 1));
                    p.Send(Tools.FromFormat("bb", 20, 10)); // AC 20 Sub 10: Minigame Victory Fanfare
                }
                else
                {
                    // Loss Flow from official PCAP (Frame 14118 & 4074):
                    p.SendSystemMessage(" Minigame failed. You can try again anytime!");

                    try
                    {
                        p.OnMinigameLost?.Invoke();
                    }
                    catch (Exception ex)
                    {
                        DebugSystem.Write($"[AC57.Recv1] Exception in OnMinigameLost: {ex.Message}");
                    }
                    p.OnMinigameLost = null;
                }

                // Unlock callback on window close (AC 20 Sub 6 -> AC 20 Sub 8)
                p.OnInteractionComplete = () =>
                {
                    p.Send(Tools.FromFormat("bb", 20, 8));
                    p.Send(Tools.FromFormat("bb", 5, 4));
                };
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[AC57.Recv1] Error processing minigame result: {ex.Message}");
                p.Send(Tools.FromFormat("bb", 20, 8));
                p.Send(Tools.FromFormat("bb", 5, 4));
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
