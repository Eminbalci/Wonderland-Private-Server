using System;
using System.Linq;
using Game;
using Game.QuestRelated;
using Network;

namespace Game.PetRelated
{
    /// <summary>
    /// Companion rebirth at the Altar (map 12766, "伙伴轉生"), verified in aLogin.exe:
    ///   Eve condition type 21 [state] asks the fellow's rebirth state, opcode 7 action 27
    ///   opens the form with S->C [44,2]; the client answers C->S [44,1,clientPetSlot] and
    ///   shows the result of S->C [44,1,code] (0 ok, 2 no pet, 3 level, 6 can't be reborn,
    ///   7 amity &lt; 60, 8 remove equipment, 255 exception).
    /// </summary>
    public static class CompanionRebirthManager
    {
        public const byte RequiredLevel = 100;
        public const byte RequiredAmity = 60;

        public static Player.PlayerPetData GetFellow(Player p)
        {
            if (p?.PlayerPets == null) return null;
            Player.PlayerPetData fellow = null;
            if (p.ActivePetID != 0)
                fellow = p.PlayerPets.Values.FirstOrDefault(pet => pet != null && Player.IsSamePetOrCompanion(pet.PetID, p.ActivePetID));
            return fellow ?? p.PlayerPets.Values.FirstOrDefault(pet => pet != null && pet.IsBattle);
        }

        /// <summary>Eve condition 21: 5 no fellow, 6 already reborn, 2 level too low, 1 eligible.</summary>
        public static int GetRebirthState(Player p)
        {
            var fellow = GetFellow(p);
            if (fellow == null) return 5;
            if (fellow.Reborn) return 6;
            if (fellow.Level < RequiredLevel) return 2;
            return 1;
        }

        public static void OpenForm(Player p)
        {
            p?.Send(Tools.FromFormat("bb", 44, 2));
        }

        private static void Result(Player p, byte code)
        {
            p.Send(Tools.FromFormat("bbb", 44, 1, code));
        }

        public static void HandleRequest(Player p, byte clientSlot)
        {
            if (p == null) return;
            try
            {
                var pet = p.GetClientPet(clientSlot);
                if (pet == null) { Result(p, 2); return; }
                if (pet.Reborn) { Result(p, 6); return; }
                if (pet.Level < RequiredLevel) { Result(p, 3); return; }
                if (pet.Amity < RequiredAmity) { Result(p, 7); return; }
                if (pet.Eq_Head != 0 || pet.Eq_Body != 0 || pet.Eq_Weapon != 0 ||
                    pet.Eq_Wrist != 0 || pet.Eq_Shoes != 0 || pet.Eq_Special != 0) { Result(p, 8); return; }

                pet.Reborn = true;
                pet.Level = 1;
                pet.Exp = 0;
                pet.NormalizeClientStats(true, p.Inv);

                Result(p, 0);
                var listPacket = QuestManager.CreatePetListPacket(p);
                if (listPacket != null) p.Send(listPacket);
                QuestManager.SendPetProgression(p, pet);
                p.SaveCharacterData();
                p.SendHeadBanner($"{pet.PetName} has been reborn!");
                DebugSystem.Write($"[CompanionRebirth] {p.CharName} reborn pet {pet.PetName} (ID {pet.PetID})");
            }
            catch (Exception ex)
            {
                DebugSystem.Write("[CompanionRebirth] Error: " + ex.Message);
                Result(p, 255);
            }
        }
    }
}
