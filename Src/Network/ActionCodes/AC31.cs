using System;
using System.Collections.Generic;
using System.Linq;
using Game;
using Game.Code;
using Game.QuestRelated;

namespace Network.ActionCodes
{
    public class AC31 : AC
    {
        public override int ID { get { return 31; } }

        public override void ProcessPkt(Player p, RecievePacket r)
        {
            if (r.GetPtr() >= r.Buffer.Length) return;
            byte sub = r.Unpack8();
            byte[] data = r.Buffer.Skip(r.GetPtr()).ToArray();
            if (sub == 1) { if (data.Length == 0) p.ConfirmNpcRest(); return; }
            byte[] deposits, withdrawals;
            // Native sender 0x2d0f96: 2 = hotel slots, 3 = team slots,
            // 4 = team count + slots + hotel count + slots (exchange).
            if (sub == 2) { deposits = new byte[0]; withdrawals = data; }
            else if (sub == 3) { deposits = data; withdrawals = new byte[0]; }
            else if (sub == 4)
            {
                if (data.Length < 2 || data[0] > 4 || data.Length < data[0] + 2) return;
                int n = data[0];
                int m = data[n + 1];
                if (m > 10 || data.Length != n + m + 2) return;
                deposits = data.Skip(1).Take(n).ToArray();
                withdrawals = data.Skip(n + 2).ToArray();
            }
            else return; // 31:1 is a rest confirmation, never a hotel-open request.
            if (deposits.Length + withdrawals.Length == 0 || deposits.Length > 4 || withdrawals.Length > 10 ||
                deposits.Distinct().Count() != deposits.Length || withdrawals.Distinct().Count() != withdrawals.Length ||
                deposits.Any(s => s < 1 || s > 4) || withdrawals.Any(s => s < 1 || s > 10) ||
                Game.Battle.PvEBattleManager.IsInBattle(p)) return;

            var outgoing = deposits.Select(p.GetClientPet).ToArray();
            if (outgoing.Any(pet => pet == null || pet.PetID == 0) || p.HotelPets == null ||
                withdrawals.Any(s => !p.HotelPets.ContainsKey(s) || p.HotelPets[s] == null)) return;
            var incoming = withdrawals.Select(s => p.HotelPets[s]).ToArray();
            var team = new Dictionary<byte, Player.PlayerPetData>(p.PlayerPets);
            var hotel = new Dictionary<byte, Player.PlayerPetData>(p.HotelPets);
            foreach (var pet in outgoing) team.Remove(pet.Slot);
            foreach (byte slot in withdrawals) hotel.Remove(slot);
            if (team.Count + incoming.Length > 4 || hotel.Count + outgoing.Length > 10)
            {
                p.SendSystemMessage(" Not enough free pet slots.");
                p.Send(Tools.FromFormat("bb", 31, 8));
                p.Send(Tools.FromFormat("bb", 31, 12));
                return;
            }
            var finalTeam = team.Values.Concat(incoming).ToArray();
            for (int i = 0; i < finalTeam.Length; i++)
                for (int j = 0; j < i; j++)
                    if (Player.IsSamePetOrCompanion(finalTeam[i].PetID, finalTeam[j].PetID))
                    {
                        p.SendSystemMessage(" You already have this pet in your team.");
                        p.Send(Tools.FromFormat("bb", 31, 8));
                        p.Send(Tools.FromFormat("bb", 31, 12));
                        return;
                    }

            // Validate the whole selection before moving any pet. Exchange also works
            // when both lists are full; records retain all equipment and progression.
            foreach (var pet in outgoing)
            {
                if (pet.IsRide) p.UnridePet();
                if (pet.IsBattle || Player.IsSamePetOrCompanion(p.ActivePetID, pet.PetID))
                {
                    p.ActivePetID = 0;
                    p.Send(Tools.FromFormat("bb", 19, 2));
                    p.CurMap?.Broadcast(Tools.FromFormat("bbd", 19, 7, p.CharID), "Ex", p.CharID);
                }
                byte slot = 1;
                while (hotel.ContainsKey(slot)) slot++;
                // Client copies the team record before AC15:2 deletes it.
                p.Send(Tools.FromFormat("bbbb", 31, 3, slot, pet.ClientSlot));
                p.Send(Tools.FromFormat("bbdb", 15, 2, p.CharID, pet.ClientSlot));
                pet.Slot = slot; pet.ClientSlot = 0; pet.IsBattle = false; pet.IsRide = false;
                hotel[slot] = pet;
            }
            p.PlayerPets = team;
            p.HotelPets = hotel;

            for (int i = 0; i < withdrawals.Length; i++)
            {
                byte hotelSlot = withdrawals[i];
                var pet = incoming[i];
                byte slot = 1;
                while (team.ContainsKey(slot)) slot++;
                pet.Slot = slot; pet.ClientSlot = 0; pet.IsBattle = false; pet.IsRide = false;
                team[slot] = pet;
                p.RegisterClientPet(pet);
                // 1. Native 31:5 copies hotel slot to team slot in client memory before 31:4 clears it.
                p.Send(Tools.FromFormat("bbbb", 31, 5, hotelSlot, pet.ClientSlot));
                // 2. Native 31:4 removes record from hotel slot.
                p.Send(Tools.FromFormat("bbb", 31, 4, hotelSlot));
            }

            // Full native roster restores names and worn items as well as HP/SP.
            var roster = QuestManager.CreatePetListPacket(p);
            if (roster != null) p.Send(roster);
            foreach (var pet in incoming) QuestManager.SendPetProgression(p, pet);
            p.SendPetHotelList();

            // Native AC31:8 resets storage manager lock flags (0x129, 0x4).
            // Native AC31:12 clears Form_PetHotel modal lock (0x174) and calls RefreshForm (0x2242d0) to re-enable team buttons.
            p.Send(Tools.FromFormat("bb", 31, 8));
            p.Send(Tools.FromFormat("bb", 31, 12));
            p.SaveCharacterData();
        }
    }
}
