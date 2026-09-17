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
                case 5:
                    RecvRestBattlePet(player, p);
                    break;
                default:
                    DebugSystem.Write($"[AC19] Action Code 19,{sub} received for player {player.CharName}");
                    break;
            }
        }

        /// <summary>
        /// Set Active Battle Pet: C->S [19, 1, pet_id (4B)]
        /// Official server responds with:
        /// 1. S->C AC 15:4 broadcast to player and map peers (spawns follower sprite)
        /// 2. S->C AC 19:1 [PetID: 4B] sent to owner
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

                Player.PlayerPetData activePet = null;

                // 1. If petId matches a slot number (1..4) in player's pet list
                if (petId <= 4 && player.PlayerPets != null && player.PlayerPets.TryGetValue((byte)petId, out var slotPet))
                {
                    activePet = slotPet;
                }

                // 2. Match by exact PetID or companion alias equivalence (e.g. 12178 <-> 12032)
                if (activePet == null && player.PlayerPets != null)
                {
                    activePet = player.PlayerPets.Values.FirstOrDefault(pet => Player.IsSamePetOrCompanion(pet.PetID, petId));
                }

                // 3. Match by dictionary key if petId <= 255
                if (activePet == null && petId <= 255 && player.PlayerPets != null && player.PlayerPets.ContainsKey((byte)petId))
                {
                    activePet = player.PlayerPets[(byte)petId];
                }

                // 4. Fallback to first pet in player's bag
                if (activePet == null && player.PlayerPets != null && player.PlayerPets.Count > 0)
                {
                    activePet = player.PlayerPets.Values.FirstOrDefault();
                }

                if (activePet == null) return;

                // Resolve the broadcast-safe companion ID (e.g. Robinson: 12032 DB -> 12178 client display)
                uint broadcastPetId = Player.GetCompanionBroadcastId(activePet.PetID);
                player.ActivePetID = broadcastPetId;

                foreach (var kvp in player.PlayerPets)
                {
                    kvp.Value.IsBattle = (kvp.Value == activePet);
                }

                // 1. Authentic AC 15:4 Map Pet Visual Entity broadcast to player and map peers
                player.BroadcastPetAppearance(broadcastPetId, activePet.PetName);

                // 2. Authentic AC 19:1 Set Battle Pet confirmation sent to owner
                player.Send(Tools.FromFormat("bbd", 19, 1, broadcastPetId));

                DebugSystem.Write($"[AC19] Player {player.CharName} set active battle pet '{activePet.PetName}' ID {broadcastPetId} (Slot {activePet.Slot}, Lv.{activePet.Level})");
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[AC19.RecvSetBattlePet] Error: {ex.Message}");
            }
        }

        /// <summary>
        /// Rest Companion from Battle / Standby: C->S [19, 2]
        /// Official server responds with:
        /// 1. S->C AC 19:7 [CharID: 4B] broadcast to player and map peers (despawns pet follower)
        /// 2. S->C AC 19:2 (empty body) broadcast to player and map peers
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

                if (player.ActiveMountID > 0)
                {
                    player.UnridePet();
                }

                // 1. Authentic AC 19:7 [CharID: 4B] despawns pet follower sprite from overworld
                SendPacket despawnPkt = Tools.FromFormat("bbd", 19, 7, player.CharID);
                player.Send(despawnPkt);
                player.CurMap?.Broadcast(despawnPkt, "Ex", player.CharID);

                // 2. Authentic AC 19:2 standby stance confirmation
                SendPacket togglePkt = Tools.FromFormat("bb", 19, 2);
                player.Send(togglePkt);
                player.CurMap?.Broadcast(togglePkt, "Ex", player.CharID);

                DebugSystem.Write($"[AC19] Player {player.CharName} rested active battle companion");
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[AC19.RecvRestBattlePet] Error: {ex.Message}");
            }
        }
    }
}
