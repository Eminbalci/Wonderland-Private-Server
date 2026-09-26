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
                    DebugSystem.Write($"[AC19] Ignored unsupported subcode {sub}");
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

                Player.PlayerPetData activePet = null;

                // 1. If petId matches a slot number (1..4) in player's pet list
                if (petId >= 1 && petId <= 4)
                {
                    activePet = player.GetClientPet((byte)petId);
                }

                // 2. Match by exact PetID or companion alias equivalence (e.g. 12178 <-> 12032)
                if (activePet == null && player.PlayerPets != null)
                {
                    activePet = player.PlayerPets.Values.FirstOrDefault(pet => pet.ClientSlot != 0 && Player.IsSamePetOrCompanion(pet.PetID, petId));
                }

                if (activePet == null) return;

                // Resolve the broadcast-safe companion ID (e.g. Robinson: 12032 DB -> 12178 client display)
                uint broadcastPetId = Player.GetCompanionBroadcastId(activePet.PetID);
                // Battle selection changes state only. AC 15:2 releases a roster
                // entry and must not be sent when switching to another owned pet.
                player.ActivePetID = broadcastPetId;

                foreach (var kvp in player.PlayerPets)
                {
                    if (kvp.Value == activePet)
                    {
                        kvp.Value.IsBattle = true;
                    }
                    else
                    {
                        kvp.Value.IsBattle = false;
                    }
                }

                // Select the existing companion and refresh its appearance.
                player.Send(Tools.FromFormat("bbd", 19, 1, broadcastPetId));
                player.BroadcastPetAppearance(broadcastPetId, activePet.PetName);

                // BroadcastPetAppearance already synchronizes this pet's stats.
                byte slot = activePet.Slot;
                player.SaveCharacterData();

                player.Send(Tools.FromFormat("bbbs", 23, 57, 0, $"{activePet.PetName ?? "Pet"} is now in Battle Mode!"));
                DebugSystem.Write($"[AC19] Player {player.CharName} set active battle pet '{activePet.PetName}' ID {broadcastPetId} (Slot {slot}, Lv.{activePet.Level})");
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[AC19.RecvSetBattlePet] Error: {ex.Message}");
            }
        }

        /// <summary>
        /// Rest Companion from Battle.
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

                // Native dispatcher: 19:2 clears the owner's selection; 19:7 clears a map peer.
                player.Send(Tools.FromFormat("bb", 19, 2));
                player.CurMap?.Broadcast(Tools.FromFormat("bbd", 19, 7, player.CharID), "Ex", player.CharID);

                // Rest preserves roster membership; do not send AC 15:2 here.
                // Send AC 5:8 appearance refresh
                SendPacket refreshPkt = Tools.FromFormat("bbdb", 5, 8, player.CharID, (byte)0);
                player.Send(refreshPkt);
                player.CurMap?.Broadcast(refreshPkt, "Ex", player.CharID);
                player.SaveCharacterData();

                DebugSystem.Write($"[AC19] Player {player.CharName} rested active battle companion");
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[AC19.RecvRestBattlePet] Error: {ex.Message}");
            }
        }

    }
}
