using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Wonderland_Private_Server.Code.Objects;
using Wonderland_Private_Server.Utilities;
using Network;
using Game;
using RCLibrary.Core;

namespace Network.ActionCodes
{
    public class AC08 : AC
    {
        public override int ID { get { return 8; } }

        public override void ProcessPkt(Player r, RecievePacket p)
        {
            if (r == null || p == null || p.Buffer == null || p.Buffer.Length < 6) return;

            byte sub = p.B ?? 0;
            p.SetPtr(6); // Skip AC header (byte 4) and Subcode (byte 5)

            switch (sub)
            {
                case 1:
                case 2:
                case 27: // INT
                case 28: // STR
                case 29: // CON
                case 30: // AGI
                case 33: // WIS
                    HandleStatAllocation(r, p, sub);
                    break;
                default:
                    DebugSystem.Write($"[AC08] Unhandled subcode {sub} from {r.CharName}");
                    break;
            }
        }

        private void HandleStatAllocation(Player r, RecievePacket p, byte subCode)
        {
            try
            {
                byte[] buf = p.Buffer;
                int ptr = 6;
                int dataLen = buf.Length - ptr;

                DebugSystem.Write($"[AC08] Stat allocation packet from {r.CharName}: Sub={subCode}, Len={buf.Length}, Hex={BitConverter.ToString(buf).Replace("-", " ")}");

                byte targetType = 0; // 0 = Player, 1..4 = Pet
                List<KeyValuePair<byte, uint>> allocations = new List<KeyValuePair<byte, uint>>();

                // Case A: Subcode itself is a Stat ID (27, 28, 29, 30, 33)
                if (subCode == 27 || subCode == 28 || subCode == 29 || subCode == 30 || subCode == 33)
                {
                    uint amount = 1;
                    if (dataLen >= 4)
                        amount = BitConverter.ToUInt32(buf, ptr);
                    else if (dataLen >= 2)
                        amount = BitConverter.ToUInt16(buf, ptr);
                    else if (dataLen >= 1)
                        amount = buf[ptr];

                    allocations.Add(new KeyValuePair<byte, uint>(subCode, Math.Max(1u, amount)));
                }
                // Case B: Subcode 1 or 2 (Batch or single with header)
                else if (dataLen >= 1)
                {
                    byte firstByte = buf[ptr++];
                    dataLen--;

                    // If firstByte is a Stat ID directly
                    if (firstByte == 27 || firstByte == 28 || firstByte == 29 || firstByte == 30 || firstByte == 33)
                    {
                        uint amount = 1;
                        if (dataLen >= 4)
                            amount = BitConverter.ToUInt32(buf, ptr);
                        else if (dataLen >= 2)
                            amount = BitConverter.ToUInt16(buf, ptr);
                        else if (dataLen >= 1)
                            amount = buf[ptr];

                        allocations.Add(new KeyValuePair<byte, uint>(firstByte, Math.Max(1u, amount)));
                    }
                    else
                    {
                        targetType = firstByte; // 0 = Player, 1+ = Pet

                        if (dataLen >= 1)
                        {
                            byte secondByte = buf[ptr++];
                            dataLen--;

                            // If secondByte is a Stat ID
                            if (secondByte == 27 || secondByte == 28 || secondByte == 29 || secondByte == 30 || secondByte == 33)
                            {
                                uint amount = 1;
                                if (dataLen >= 4)
                                    amount = BitConverter.ToUInt32(buf, ptr);
                                else if (dataLen >= 2)
                                    amount = BitConverter.ToUInt16(buf, ptr);
                                else if (dataLen >= 1)
                                    amount = buf[ptr];

                                allocations.Add(new KeyValuePair<byte, uint>(secondByte, Math.Max(1u, amount)));
                            }
                            else
                            {
                                // Multi-entry batch: secondByte is the count of modified stats
                                int count = Math.Max(1, (int)secondByte);

                                for (int i = 0; i < count && dataLen >= 1; i++)
                                {
                                    byte statId = buf[ptr++];
                                    dataLen--;

                                    uint amount = 1;
                                    if (dataLen >= 4)
                                    {
                                        amount = BitConverter.ToUInt32(buf, ptr);
                                        ptr += 4;
                                        dataLen -= 4;
                                    }
                                    else if (dataLen >= 2)
                                    {
                                        amount = BitConverter.ToUInt16(buf, ptr);
                                        ptr += 2;
                                        dataLen -= 2;
                                    }
                                    else if (dataLen >= 1)
                                    {
                                        amount = buf[ptr++];
                                        dataLen--;
                                    }

                                    if (statId == 27 || statId == 28 || statId == 29 || statId == 30 || statId == 33)
                                    {
                                        allocations.Add(new KeyValuePair<byte, uint>(statId, Math.Max(1u, amount)));
                                    }
                                }
                            }
                        }
                    }
                }

                if (allocations.Count == 0)
                {
                    DebugSystem.Write($"[AC08] No valid stat allocations parsed for {r.CharName}");
                    return;
                }

                // 1. Handle Player Stat Allocation
                if (targetType == 0)
                {
                    bool appliedAny = false;
                    foreach (var alloc in allocations)
                    {
                        byte statId = alloc.Key;
                        uint amount = alloc.Value;

                        if (r.SkillPoints >= amount && amount > 0)
                        {
                            r.SkillPoints -= (ushort)amount;
                            string statName = "STR";
                            int newStat = 0;

                            switch (statId)
                            {
                                case 28: // STR
                                    r.baseStr += (ushort)amount;
                                    statName = "STR";
                                    newStat = r.Str;
                                    break;
                                case 29: // CON
                                    r.baseCon += (ushort)amount;
                                    statName = "CON";
                                    newStat = r.Con;
                                    break;
                                case 27: // INT
                                    r.baseInt += (ushort)amount;
                                    statName = "INT";
                                    newStat = r.Int;
                                    break;
                                case 33: // WIS
                                    r.baseWis += (ushort)amount;
                                    statName = "WIS";
                                    newStat = r.Wis;
                                    break;
                                case 30: // AGI
                                    r.baseAgi += (ushort)amount;
                                    statName = "AGI";
                                    newStat = r.Agi;
                                    break;
                            }

                            appliedAny = true;
                            DebugSystem.Write($"[AC08] Allocated +{amount} to {statName} (StatID {statId}) for {r.CharName}. New base={newStat}, Remaining SkillPoints={r.SkillPoints}");
                            r.SendSystemMessage($"✨ [{statName} +{amount}] Stat upgraded! Current {statName}: {newStat} (Available Points: {r.SkillPoints})");
                        }
                        else
                        {
                            r.SendSystemMessage($"⚠️ Not enough stat points to allocate +{amount} (Available: {r.SkillPoints})");
                        }
                    }

                    if (appliedAny)
                    {
                        // Sync full updated stats and points to client
                        r.Send8_1(true);

                        // Unlock element & progression skills if thresholds are met
                        Game.SkillRelated.SkillManager.CheckAndUnlockProgressionSkills(r);

                        // Immediate database persistence
                        DataBase.CharacterDataBase.GlobalInstance?.WritePlayer(r.CharID, r);
                    }
                }
                // 2. Handle Pet Stat Allocation (targetType = Pet Slot 1..4)
                else
                {
                    byte petSlot = (byte)targetType;
                    Player.PlayerPetData targetPet = null;

                    if (r.PlayerPets != null)
                    {
                        if (r.PlayerPets.TryGetValue(petSlot, out var p1))
                            targetPet = p1;
                        else if (petSlot > 0 && r.PlayerPets.TryGetValue((byte)(petSlot - 1), out var p0))
                            targetPet = p0;
                        else if (r.PlayerPets.Values.Any(pet => pet.IsBattle))
                            targetPet = r.PlayerPets.Values.FirstOrDefault(pet => pet.IsBattle);
                    }

                    if (targetPet != null)
                    {
                        bool petApplied = false;
                        foreach (var alloc in allocations)
                        {
                            byte statId = alloc.Key;
                            uint amount = alloc.Value;

                            if (targetPet.SkillPoints >= amount && amount > 0)
                            {
                                targetPet.SkillPoints -= (ushort)amount;
                                string statName = "STR";
                                int newStat = 0;

                                switch (statId)
                                {
                                    case 28:
                                        targetPet.Str += (ushort)amount;
                                        statName = "STR";
                                        newStat = targetPet.Str;
                                        break;
                                    case 29:
                                        targetPet.Con += (ushort)amount;
                                        statName = "CON";
                                        newStat = targetPet.Con;
                                        break;
                                    case 27:
                                        targetPet.Int += (ushort)amount;
                                        statName = "INT";
                                        newStat = targetPet.Int;
                                        break;
                                    case 33:
                                        targetPet.Wis += (ushort)amount;
                                        statName = "WIS";
                                        newStat = targetPet.Wis;
                                        break;
                                    case 30:
                                        targetPet.Agi += (ushort)amount;
                                        statName = "AGI";
                                        newStat = targetPet.Agi;
                                        break;
                                }

                                petApplied = true;
                                DebugSystem.Write($"[AC08] Allocated +{amount} to Pet '{targetPet.PetName}' {statName}. Remaining Points: {targetPet.SkillPoints}");
                                r.SendSystemMessage($"🐾 [{targetPet.PetName} {statName} +{amount}] Current: {newStat} (Pet Points: {targetPet.SkillPoints})");
                                r.Send(Tools.FromFormat("bbbbdd", 8, 2, statId, petSlot, (uint)newStat, 0));
                                r.Send(Tools.FromFormat("bbbbdd", 8, 2, 38, petSlot, (uint)targetPet.SkillPoints, 0));
                            }
                            else
                            {
                                r.SendSystemMessage($"⚠️ Pet '{targetPet.PetName}' has insufficient stat points (Available: {targetPet.SkillPoints})");
                            }
                        }

                        if (petApplied)
                        {
                            DataBase.CharacterDataBase.GlobalInstance?.WritePlayer(r.CharID, r);
                        }
                    }
                    else
                    {
                        DebugSystem.Write($"[AC08] Pet slot {petSlot} not found for {r.CharName}");
                    }
                }
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[AC08] Error during stat allocation: {ex.Message}");
            }
        }
    }
}
