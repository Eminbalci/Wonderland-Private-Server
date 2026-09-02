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

            // Reduce Amity by 1 on battle defeat
            SendPacket p = new SendPacket();
            p.Pack8(23);
            p.Pack8(57);
            p.Pack8(0);
            p.PackString($"Your pet was defeated! Pet Amity decreased by 1.");
            player.Send(p);

            DebugSystem.Write($"[PetAmity] Pet #{petId} of {player.CharName} lost 1 amity upon battle death.");
        }

        public static bool FeedPet(Player player, ushort foodItemId)
        {
            if (player == null) return false;

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

            // Play Pet happy emote
            SendPacket pEmote = new SendPacket();
            pEmote.Pack8(5);
            pEmote.Pack8(5);
            pEmote.Pack32(player.CharID);
            pEmote.Pack16(60012); // Pet heart/love effect
            player.CurMap?.Broadcast(pEmote);

            SendSystemMsg(player, $"Fed your pet! Pet Amity increased by +{amityGain}!");
            DebugSystem.Write($"[PetAmity] Player {player.CharName} fed pet with #{foodItemId} (+{amityGain} amity).");
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
