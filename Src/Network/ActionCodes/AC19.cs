using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Network;
using Game;
using Game.Code;
using wlo.pserver.core.Game;

namespace Network.ActionCodes
{
    /// <summary>
    /// Handles Battle Standby and Active Battle Companion state (AC 19).
    /// </summary>
    public class AC19 : AC
    {
        public override int ID { get { return 19; } }

        public override void ProcessPkt(Player player, RecievePacket p)
        {
            byte sub = (byte)(p.B ?? 0);
            p.SetPtr(6); // Skip packet header (4B), AC (1B), and Sub (1B)

            switch (sub)
            {
                case 1:
                case 4:
                    RecvSetBattlePet(player, p);
                    break;
                case 2:
                    Recv2(player, p);
                    break;
                case 5:
                    RecvRestBattlePet(player, p);
                    break;
                default:
                    DebugSystem.Write($"[AC19] Action Code 19,{sub} - attempting battle pet toggle");
                    RecvSetBattlePet(player, p);
                    break;
            }
        }

        /// <summary>
        /// Set Active Battle Pet: C->S [19, 1/4, pet_id or slot]
        /// </summary>
        private void RecvSetBattlePet(Player player, RecievePacket p)
        {
            try
            {
                uint petId = 0;
                if ((p.Count - p.GetPtr()) >= 4)
                {
                    petId = p.Unpack32();
                }
                else if ((p.Count - p.GetPtr()) >= 2)
                {
                    petId = p.Unpack16();
                }
                else if ((p.Count - p.GetPtr()) >= 1)
                {
                    petId = p.Unpack8();
                }

                // If petId is a slot number (1..4) rather than actual TemplateID
                if (petId <= 4 && player.PlayerPets != null && player.PlayerPets.TryGetValue((byte)petId, out var petData))
                {
                    petId = petData.PetID;
                }

                // If still 0, fallback to first pet in player's bag
                if (petId == 0 && player.PlayerPets != null && player.PlayerPets.Count > 0)
                {
                    petId = player.PlayerPets.Values.FirstOrDefault()?.PetID ?? 0;
                }

                if (petId == 0) return;

                player.UnridePet();
                player.ActivePetID = petId;

                if (player.PlayerPets != null)
                {
                    foreach (var kvp in player.PlayerPets)
                    {
                        kvp.Value.IsBattle = (kvp.Value.PetID == petId);
                    }
                }

                // 1. Send active battle pet packets to player
                player.Send(Tools.FromFormat("bbd", 19, 1, petId));
                player.Send(Tools.FromFormat("bbdd", 19, 4, player.CharID, petId));

                // 2. Broadcast companion follower to map
                if (player.CurMap != null)
                {
                    player.CurMap.Broadcast(Tools.FromFormat("bbdd", 19, 1, player.CharID, petId));
                    player.CurMap.Broadcast(Tools.FromFormat("bbdd", 19, 4, player.CharID, petId));

                    // Force refresh player appearance to spawn companion on ground
                    SendPacket refresh = new SendPacket();
                    refresh.PackArray(new byte[] { 5, 8 });
                    refresh.Pack32(player.CharID);
                    refresh.Pack8(0);
                    player.CurMap.Broadcast(refresh);
                }

                player.Send(Tools.FromFormat("bbbs", 23, 57, 0, "Pet is now in Battle Mode!"));
                DebugSystem.Write($"[AC19] Player {player.CharName} set active battle pet ID {petId}");
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[AC19.RecvSetBattlePet] Error: {ex.Message}");
            }
        }

        /// <summary>
        /// Rest Companion from Battle: C->S [19, 5]
        /// </summary>
        private void RecvRestBattlePet(Player player, RecievePacket p)
        {
            try
            {
                player.ActivePetID = 0;
                if (player.PlayerPets != null)
                {
                    foreach (var kvp in player.PlayerPets)
                    {
                        kvp.Value.IsBattle = false;
                    }
                }

                SendPacket restPkt = Tools.FromFormat("bbd", 15, 17, player.CharID);
                player.Send(restPkt);
                player.Send(Tools.FromFormat("bbd", 19, 5, player.CharID));

                if (player.CurMap != null)
                {
                    player.CurMap.Broadcast(restPkt);
                    player.CurMap.Broadcast(Tools.FromFormat("bbd", 19, 5, player.CharID));

                    SendPacket refresh = new SendPacket();
                    refresh.PackArray(new byte[] { 5, 8 });
                    refresh.Pack32(player.CharID);
                    refresh.Pack8(0);
                    player.CurMap.Broadcast(refresh);
                }

                player.Send(Tools.FromFormat("bbbs", 23, 57, 0, "Pet is now resting."));
                DebugSystem.Write($"[AC19] Player {player.CharName} rested active battle companion.");
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[AC19.RecvRestBattlePet] Error: {ex.Message}");
            }
        }

        /// <summary>
        /// Toggle Battle Standby Stance: C->S [19, 2] -> Echo S->C [19, 2]
        /// </summary>
        private void Recv2(Player player, RecievePacket p)
        {
            try
            {
                SendPacket togglePkt = Tools.FromFormat("bb", 19, 2);
                player.Send(togglePkt);
                player.CurMap?.Broadcast(togglePkt);

                DebugSystem.Write($"[AC19] Player {player.CharName} toggled battle standby stance");
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[AC19.Recv2] Error: {ex.Message}");
            }
        }
    }
}
