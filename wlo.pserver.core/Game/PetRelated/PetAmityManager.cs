using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Network;

namespace Game.PetRelated
{
    public static class PetAmityManager
    {
        public static void OnPetDeath(Player player, Player.PlayerPetData pet)
        {
            if (player == null || pet == null || !player.PlayerPets.Values.Contains(pet)) return;
            pet.Amity = (byte)Math.Max(0, pet.Amity - 1);
            Game.QuestRelated.QuestManager.SendPetAmity(player, pet);
            if (pet.Amity < 20)
            {
                byte slot = pet.ClientSlot;
                if (pet.IsBattle || Player.IsSamePetOrCompanion(player.ActivePetID, pet.PetID))
                {
                    player.ActivePetID = 0;
                    pet.IsBattle = false;
                    player.UnridePet();
                    player.Send(Tools.FromFormat("bb", 19, 2));
                    player.CurMap?.Broadcast(Tools.FromFormat("bbd", 19, 7, player.CharID), "Ex", player.CharID);
                    var despawn = Tools.FromFormat("bbdd", 5, 8, player.CharID, 0);
                    player.Send(despawn);
                    player.CurMap?.Broadcast(despawn, "Ex", player.CharID);
                }
                player.PlayerPets.Remove(pet.Slot);
                pet.ClientSlot = 0;
                if (slot != 0)
                {
                    var dismiss = Tools.FromFormat("bbdb", 15, 2, player.CharID, slot);
                    player.Send(dismiss);
                    player.CurMap?.Broadcast(dismiss, "Ex", player.CharID);
                }
                player.SendSystemMessage($"{pet.PetName} has deserted because amity fell below 20.");
            }
            else player.SendSystemMessage($"{pet.PetName} lost 1 amity after defeat ({pet.Amity}/100).");
            player.SaveCharacterData();
        }

        public static bool TryFeedPet(Player player, byte slot, byte requestedCount, byte clientPetSlot)
        {
            if (player == null || slot < 1 || slot > 50 || requestedCount == 0 || clientPetSlot < 1 || clientPetSlot > 4) return false;
            lock (player.Inv.SyncRoot)
            {
                var pet = player.GetClientPet(clientPetSlot);
                var item = player.Inv[slot];
                if (pet == null || pet.Amity >= 100 || item.ItemID == 0 || item.Ammt == 0 || item.isLocked) return false;
                var info = item.Data;
                int gain = 0;
                if (info?.StatusType == null || info.StatusUp == null) return false;
                for (int i = 0; i < Math.Min(info.StatusType.Length, info.StatusUp.Length); i++)
                    if (info.StatusType[i] == 64) gain += Math.Max(0, info.StatusUp[i] - 100);
                if (gain <= 0) return false;

                int needed = (100 - pet.Amity + gain - 1) / gain;
                byte count = (byte)Math.Min(needed, Math.Min(requestedCount, item.Ammt));
                var consumed = player.Inv.RemoveItem(slot, count);
                if (consumed == null) return false;
                byte previous = pet.Amity;
                pet.Amity = (byte)Math.Min(100, previous + (long)gain * consumed.Ammt);
                Game.QuestRelated.QuestManager.SendPetAmity(player, pet);
                player.Send(Tools.FromFormat("bb", 23, 15));
                player.SendHeadBanner(pet.PetName + ": Amity +" + (pet.Amity - previous) + " (" + pet.Amity + "/100)");
                player.SaveCharacterData();
                return true;
            }
        }

        public static bool FeedPet(Player player, ushort foodItemId)
        {
            if (player == null) return false;
            var pet = player.PlayerPets.Values.FirstOrDefault(x => x.IsBattle && x.ClientSlot != 0);
            if (pet != null)
                for (byte slot = 1; slot <= 50; slot++)
                    if (player.Inv[slot].ItemID == foodItemId && TryFeedPet(player, slot, 1, pet.ClientSlot)) return true;
            player.SendHeadBanner("Select a battle pet and use an available amity item.");
            return false;
        }
    }
}
