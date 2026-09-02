using System;
using System.Linq;
using Game;
using Game.Code;
using Network;

namespace Network.ActionCodes
{
    public class AC31 : AC
    {
        public override int ID { get { return 31; } }

        public override void ProcessPkt(Player p, RecievePacket r)
        {
            byte sub = r.Unpack8();
            switch (sub)
            {
                case 1: Recv1(p, r); break; // Open / List / Sync Pet Hotel
                case 2:
                case 4: RecvWithdraw(p, r); break; // Withdraw Pet from Hotel
                case 3: RecvDeposit(p, r); break; // Deposit Pet into Hotel
                case 7: RecvClose(p, r); break; // Close Hotel
                default:
                    DebugSystem.Write($"[AC31] Unknown subcode {sub} for {p.CharName}");
                    break;
            }
        }

        // Sub 1: Open / List Pet Hotel
        void Recv1(Player p, RecievePacket r)
        {
            try
            {
                p.OpenPetHotel();
                DebugSystem.Write($"[AC31.Recv1] {p.CharName} requested Pet Hotel list.");
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[AC31.Recv1] Error: {ex.Message}");
            }
        }

        // Sub 3: Deposit Pet from Player's Team Slot into Pet Hotel
        void RecvDeposit(Player p, RecievePacket r)
        {
            try
            {
                byte petSlot = r.Unpack8();
                if (petSlot < 1 || petSlot > 4) return;

                if (p.PlayerPets == null || !p.PlayerPets.TryGetValue(petSlot, out var pet) || pet == null || pet.PetID == 0)
                {
                    DebugSystem.Write($"[AC31.RecvDeposit] Pet slot {petSlot} is empty for {p.CharName}");
                    return;
                }

                // Check hotel capacity (Max 20 pets)
                if (p.HotelPets == null) p.HotelPets = new System.Collections.Generic.Dictionary<byte, Player.PlayerPetData>();
                if (p.HotelPets.Count >= 20)
                {
                    p.SendSystemMessage("⚠️ Pet Hotel is full! (Max 20 pets)");
                    return;
                }

                // Find first free hotel slot (1..20)
                byte freeHotelSlot = 1;
                while (p.HotelPets.ContainsKey(freeHotelSlot) && freeHotelSlot <= 20) freeHotelSlot++;

                if (freeHotelSlot > 20)
                {
                    p.SendSystemMessage("⚠️ Pet Hotel is full! (Max 20 pets)");
                    return;
                }

                // If this pet was battling / following, dismiss it
                if (p.ActivePetID == pet.PetID || pet.IsBattle)
                {
                    p.ActivePetID = 0;
                    pet.IsBattle = false;
                    SendPacket dismissPkt = new SendPacket();
                    dismissPkt.Pack8(19);
                    dismissPkt.Pack8(5);
                    dismissPkt.Pack32(p.CharID);
                    dismissPkt.Pack32(pet.PetID);
                    p.Send(dismissPkt);
                    p.CurMap?.Broadcast(dismissPkt);
                }

                if (pet.IsRide)
                {
                    p.UnridePet();
                    pet.IsRide = false;
                }

                // Create hotel pet copy
                var hotelPet = new Player.PlayerPetData()
                {
                    Slot = freeHotelSlot,
                    PetID = pet.PetID,
                    PetName = pet.PetName,
                    Level = pet.Level,
                    HP = pet.HP,
                    MaxHP = pet.MaxHP,
                    SP = pet.SP,
                    MaxSP = pet.MaxSP,
                    Amity = pet.Amity,
                    IsBattle = false,
                    IsRide = false
                };

                // Add to Hotel, remove from Player team
                p.HotelPets[freeHotelSlot] = hotelPet;
                p.PlayerPets.Remove(petSlot);

                // 1. Tell client to remove pet from active team roster
                p.Send(Tools.FromFormat("bbb", 19, 2, petSlot));
                p.Send(Tools.FromFormat("bbb", 31, 3, petSlot));

                // 2. Tell client to add pet to Hotel UI list
                SendPacket hPkt = new SendPacket();
                hPkt.Pack8(31);
                hPkt.Pack8(3);
                hPkt.Pack8(freeHotelSlot);
                hPkt.Pack16((ushort)hotelPet.PetID);
                hPkt.Pack8(hotelPet.Level);
                hPkt.Pack32((uint)hotelPet.HP);
                hPkt.Pack32((uint)hotelPet.MaxHP);
                hPkt.Pack16((ushort)hotelPet.SP);
                hPkt.Pack16((ushort)hotelPet.MaxSP);
                hPkt.Pack8(hotelPet.Amity);
                hPkt.PackString(hotelPet.PetName ?? "");
                p.Send(hPkt);

                // 3. Save to database immediately
                DataBase.CharacterDataBase.GlobalInstance?.WritePlayer(p.CharID, p);

                p.SendSystemMessage($"🏨 '{hotelPet.PetName}' (#{hotelPet.PetID}) deposited into Pet Hotel (Slot {freeHotelSlot}).");
                DebugSystem.Write($"[AC31.RecvDeposit] {p.CharName} deposited '{hotelPet.PetName}' (ID: {hotelPet.PetID}) from Team Slot {petSlot} to Hotel Slot {freeHotelSlot}");
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[AC31.RecvDeposit] Error: {ex.Message}");
            }
        }

        // Sub 4 / 2: Withdraw Pet from Hotel Slot into Active Team
        void RecvWithdraw(Player p, RecievePacket r)
        {
            try
            {
                byte hotelSlot = r.Unpack8();
                if (hotelSlot < 1 || hotelSlot > 20) return;

                if (p.HotelPets == null || !p.HotelPets.TryGetValue(hotelSlot, out var pet) || pet == null || pet.PetID == 0)
                {
                    DebugSystem.Write($"[AC31.RecvWithdraw] Hotel slot {hotelSlot} is empty for {p.CharName}");
                    return;
                }

                // Check active team capacity (Max 4 pets)
                if (p.PlayerPets == null) p.PlayerPets = new System.Collections.Generic.Dictionary<byte, Player.PlayerPetData>();
                if (p.PlayerPets.Count >= 4)
                {
                    p.SendSystemMessage("⚠️ Your companion team is full! (Max 4 pets)");
                    return;
                }

                // Find first free team slot (1..4)
                byte freeTeamSlot = 1;
                while (p.PlayerPets.ContainsKey(freeTeamSlot) && freeTeamSlot <= 4) freeTeamSlot++;

                if (freeTeamSlot > 4)
                {
                    p.SendSystemMessage("⚠️ Your companion team is full! (Max 4 pets)");
                    return;
                }

                // Create active team pet copy
                var teamPet = new Player.PlayerPetData()
                {
                    Slot = freeTeamSlot,
                    PetID = pet.PetID,
                    PetName = pet.PetName,
                    Level = pet.Level,
                    HP = pet.HP,
                    MaxHP = pet.MaxHP,
                    SP = pet.SP,
                    MaxSP = pet.MaxSP,
                    Amity = pet.Amity,
                    IsBattle = false,
                    IsRide = false
                };

                // Add to Player team, remove from Hotel
                p.PlayerPets[freeTeamSlot] = teamPet;
                p.HotelPets.Remove(hotelSlot);

                // 1. Tell client to remove pet from Hotel UI list
                p.Send(Tools.FromFormat("bbb", 31, 4, hotelSlot));

                // 2. Dispatch companion reward packet to add pet to active team
                Game.QuestRelated.QuestManager.SendCompanionReward(p, teamPet.PetID, teamPet.PetName, setBattle: false);

                // 3. Save to database immediately
                DataBase.CharacterDataBase.GlobalInstance?.WritePlayer(p.CharID, p);

                p.SendSystemMessage($"🏨 '{teamPet.PetName}' (#{teamPet.PetID}) retrieved from Pet Hotel to Team Slot {freeTeamSlot}!");
                DebugSystem.Write($"[AC31.RecvWithdraw] {p.CharName} withdrew '{teamPet.PetName}' (ID: {teamPet.PetID}) from Hotel Slot {hotelSlot} to Team Slot {freeTeamSlot}");
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[AC31.RecvWithdraw] Error: {ex.Message}");
            }
        }

        // Sub 7: Close Pet Hotel
        void RecvClose(Player p, RecievePacket r)
        {
            try
            {
                p.Send(Tools.FromFormat("bb", 31, 7));
                p.Send(Tools.FromFormat("bb", 20, 8));
                p.Send(Tools.FromFormat("bb", 5, 4));
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[AC31.RecvClose] Error: {ex.Message}");
            }
        }
    }
}
