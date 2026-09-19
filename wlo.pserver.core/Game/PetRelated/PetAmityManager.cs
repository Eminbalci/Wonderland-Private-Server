using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Network;

namespace Game.PetRelated
{
    public static class PetAmityManager
    {
        public static void OnPetDeath(Player player, ushort petId)
        {
            if (player == null || petId == 0) return;

            var pet = player.PlayerPets?.Values.FirstOrDefault(pt => pt.PetID == petId || Player.IsSamePetOrCompanion(pt.PetID, petId));
            if (pet == null) return;

            // Reduce Amity on battle defeat
            if (pet.Amity > 1)
            {
                pet.Amity = (byte)Math.Max(0, pet.Amity - 1);
            }
            else
            {
                pet.Amity = 0;
            }

            // Sync updated amity to client (Stat 283 = 0x011B: Amity)
            player.SendPetStat(pet.Slot, 0x011B, pet.Amity);

            // Check for pet desertion if Amity drops below 20
            if (pet.Amity < 20)
            {
                SendSystemMsg(player, $"Your companion {pet.PetName}'s Amity has fallen to {pet.Amity} (below 20)! They have deserted you and returned to the wild.");
                DebugSystem.Write($"[PetAmity] Companion {pet.PetName} (Slot {pet.Slot}) deserted {player.CharName} due to low amity ({pet.Amity}).");

                if (player.ActivePetID == pet.PetID || Player.IsSamePetOrCompanion(player.ActivePetID, pet.PetID))
                {
                    player.ActivePetID = 0;
                    player.Send(Tools.FromFormat("bbd", 19, 4, 0));
                }

                player.PlayerPets.Remove(pet.Slot);
                player.Send(Tools.FromFormat("bbb", 19, 2, pet.Slot));
                player.SaveCharacterData();
                return;
            }

            SendSystemMsg(player, $"Your pet {pet.PetName} was defeated! Pet Amity decreased by 1 (Current: {pet.Amity}/100).");
            player.SaveCharacterData();
            DebugSystem.Write($"[PetAmity] Pet #{petId} ({pet.PetName}) of {player.CharName} lost 1 amity upon battle death (Amity: {pet.Amity}).");
        }

        public static bool FeedPet(Player player, ushort foodItemId)
        {
            if (player == null) return false;

            var pet = player.PlayerPets?.Values.FirstOrDefault(pt => pt.IsBattle || pt.Slot == 1);
            if (pet == null)
            {
                SendSystemMsg(player, "You do not have an active companion to feed!");
                return false;
            }

            if (pet.Amity >= 100)
            {
                SendSystemMsg(player, $"{pet.PetName}'s Amity is already at maximum (100)!");
                return false;
            }

            byte amityGain = 1;
            switch (foodItemId)
            {
                case 30025: amityGain = 3; break; // Rice Ball
                case 28020: amityGain = 2; break; // Roast Meat
                case 28021: amityGain = 2; break; // Roast Pork
                case 28014: amityGain = 1; break; // Apple/Fruit
                default: amityGain = 1; break;
            }

            // Remove food from inventory
            if (!player.Inv.RemoveItemById(foodItemId, 1))
            {
                SendSystemMsg(player, "You do not have that pet food in your inventory!");
                return false;
            }

            pet.Amity = (byte)Math.Min(100, pet.Amity + amityGain);
            player.SendPetStat(pet.Slot, 0x011B, pet.Amity);
            player.SaveCharacterData();

            // Play Pet happy emote
            SendPacket pEmote = new SendPacket();
            pEmote.Pack8(5);
            pEmote.Pack8(5);
            pEmote.Pack32(player.CharID);
            pEmote.Pack16(60012); // Pet heart/love effect
            player.CurMap?.Broadcast(pEmote);

            SendSystemMsg(player, $"Fed {pet.PetName}! Pet Amity increased by +{amityGain} (Current: {pet.Amity}/100)!");
            DebugSystem.Write($"[PetAmity] Player {player.CharName} fed {pet.PetName} with #{foodItemId} (+{amityGain} amity -> {pet.Amity}).");
            return true;
        }

        private static void SendSystemMsg(Player p, string msg)
        {
            if (p == null || string.IsNullOrEmpty(msg)) return;
            SendPacket s = new SendPacket();
            s.Pack8(23);
            s.Pack8(57);
            s.Pack8(0);
            s.PackString(msg);
            p.Send(s);
        }
    }
}
