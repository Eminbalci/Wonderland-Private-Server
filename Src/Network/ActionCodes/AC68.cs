using System;
using Game;
using Network;

namespace Network.ActionCodes
{
    /// <summary>
    /// AC 68: Companion Potential Training & Stat Point Allocation Protocol.
    /// Handles applying potential enhancement pills and spending stat points on active pets.
    /// </summary>
    public class AC68 : AC
    {
        public override int ID => 68;

        public const int MaxPotentialCap = 100;

        public override void ProcessPkt(Player c, RecievePacket p)
        {
            if (c == null || p == null) return;

            byte subCode = p.B ?? 1;
            DebugSystem.Write($"[AC68] Pet training action from {c.CharName}: SubCode={subCode}");

            try
            {
                byte petSlot = p.Buffer.Length >= 7 ? p.Unpack8() : (byte)0;
                byte statType = p.Buffer.Length >= 8 ? p.Unpack8() : (byte)0;

                var pet = c.GetClientPet(petSlot);
                if (pet == null || pet.PetID == 0)
                {
                    SendPacket err = new SendPacket();
                    err.Pack8((byte)ID);
                    err.Pack8(subCode);
                    err.Pack8(petSlot);
                    err.Pack8(statType);
                    err.Pack8(0); // Failed
                    c.Send(err);
                    return;
                }

                // SubCode 1: Potential Pill / Training Capsule
                if (subCode == 1)
                {
                    if (pet.Potential >= MaxPotentialCap)
                    {
                        SendPacket capErr = new SendPacket();
                        capErr.Pack8((byte)ID);
                        capErr.Pack8(subCode);
                        capErr.Pack8(petSlot);
                        capErr.Pack8(statType);
                        capErr.Pack8(0); // Failed (Cap reached)
                        c.Send(capErr);
                        c.SendSystemMessage($"[Pet Training] {pet.PetName} has already reached the maximum potential limit ({MaxPotentialCap}).");
                        return;
                    }

                    pet.Potential = (ushort)Math.Min(MaxPotentialCap, pet.Potential + 1);
                    c.SaveCharacterData();
                    Game.QuestRelated.QuestManager.SendPetProgression(c, pet);

                    SendPacket resp = new SendPacket();
                    resp.Pack8((byte)ID);
                    resp.Pack8(subCode);
                    resp.Pack8(petSlot);
                    resp.Pack8(statType);
                    resp.Pack8(1); // Success
                    c.Send(resp);
                    c.SendSystemMessage($"[Pet Training] {pet.PetName}'s potential increased to {pet.Potential}/{MaxPotentialCap}!");
                    return;
                }

                // SubCode 2: Stat point distribution
                if (subCode == 2 && pet.SkillPoints > 0 && statType >= 1 && statType <= 5)
                {
                    pet.SkillPoints--;
                    switch (statType)
                    {
                        case 1: pet.Str++; break;
                        case 2: pet.Con++; break;
                        case 3: pet.Int++; break;
                        case 4: pet.Wis++; break;
                        case 5: pet.Agi++; break;
                    }
                    Game.QuestRelated.QuestManager.SendPetProgression(c, pet);
                    c.SaveCharacterData();

                    SendPacket resp = new SendPacket();
                    resp.Pack8((byte)ID);
                    resp.Pack8(subCode);
                    resp.Pack8(petSlot);
                    resp.Pack8(statType);
                    resp.Pack8(1); // Success
                    c.Send(resp);
                    return;
                }

                SendPacket defResp = new SendPacket();
                defResp.Pack8((byte)ID);
                defResp.Pack8(subCode);
                defResp.Pack8(petSlot);
                defResp.Pack8(statType);
                defResp.Pack8(1);
                c.Send(defResp);
            }
            catch (Exception ex)
            {
                DebugSystem.Write(new ExceptionData(ex));
            }
        }
    }
}
