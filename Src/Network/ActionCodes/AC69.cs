using System;
using Game;
using Network;

namespace Network.ActionCodes
{
    /// <summary>
    /// AC 69: Companion Rebirth & Pet Evolution Protocol.
    /// Handles companion rebirth ascension quests and model evolution upon reaching maximum amity and level thresholds.
    /// </summary>
    public class AC69 : AC
    {
        public override int ID => 69;

        public const int RequiredRebirthLevel = 100;
        public const int RequiredRebirthAmity = 100;

        public override void ProcessPkt(Player c, RecievePacket p)
        {
            if (c == null || p == null) return;

            byte subCode = p.B ?? 1;
            DebugSystem.Write($"[AC69] Pet rebirth evolution from {c.CharName}: SubCode={subCode}");

            try
            {
                byte petSlot = p.Buffer.Length >= 7 ? p.Unpack8() : (byte)0;

                if (!c.PlayerPets.TryGetValue(petSlot, out var pet) || pet == null || pet.PetID == 0)
                {
                    SendPacket err = new SendPacket();
                    err.Pack8((byte)ID);
                    err.Pack8(subCode);
                    err.Pack8(petSlot);
                    err.Pack8(0);
                    c.Send(err);
                    return;
                }

                if (pet.Reborn)
                {
                    SendPacket alreadyReborn = new SendPacket();
                    alreadyReborn.Pack8((byte)ID);
                    alreadyReborn.Pack8(subCode);
                    alreadyReborn.Pack8(petSlot);
                    alreadyReborn.Pack8(0);
                    c.Send(alreadyReborn);
                    c.SendSystemMessage($"[Rebirth] {pet.PetName} has already undergone Rebirth ascension.");
                    return;
                }

                if (pet.Level < RequiredRebirthLevel || pet.Amity < RequiredRebirthAmity)
                {
                    SendPacket notEligible = new SendPacket();
                    notEligible.Pack8((byte)ID);
                    notEligible.Pack8(subCode);
                    notEligible.Pack8(petSlot);
                    notEligible.Pack8(0); // Not eligible
                    c.Send(notEligible);
                    c.SendSystemMessage($"[Rebirth] {pet.PetName} is not eligible for Rebirth. Requirements: Level {RequiredRebirthLevel}+ (Current: {pet.Level}), Amity {RequiredRebirthAmity} (Current: {pet.Amity}).");
                    return;
                }

                // Execute Rebirth Ascension
                pet.Reborn = true;
                pet.Level = 1;
                pet.Exp = 0;
                pet.SkillPoints += 50; // Rebirth bonus stat points
                pet.Amity = 100;

                c.SaveCharacterData();

                // Send rebirth success packet
                SendPacket resp = new SendPacket();
                resp.Pack8((byte)ID);
                resp.Pack8(subCode);
                resp.Pack8(petSlot);
                resp.Pack8(1); // 1 = Evolution Ascended
                c.Send(resp);

                // Refresh pet on client
                SendPacket petPkt = Game.QuestRelated.QuestManager.CreatePetPacket(c, pet.PetID, pet.Slot, pet.HP, pet.MaxHP, pet.SP, pet.MaxSP, pet.Amity, pet.Level, pet.Str, pet.Con, pet.Int, pet.Wis, pet.Agi, pet.Exp, pet.Reborn, pet.Job);
                c.Send(petPkt);
                Game.QuestRelated.QuestManager.SendPetSkills(c, pet.PetID, pet.Slot);

                c.SendSystemMessage($"[Rebirth] Congratulations! {pet.PetName} has successfully attained Rebirth Ascension!");
                DebugSystem.Write($"[AC69] Pet in slot #{petSlot} ({pet.PetName}) of {c.CharName} successfully underwent Rebirth.");
            }
            catch (Exception ex)
            {
                DebugSystem.Write(new ExceptionData(ex));
            }
        }
    }
}
