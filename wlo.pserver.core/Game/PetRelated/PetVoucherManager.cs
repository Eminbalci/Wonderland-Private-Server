using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;
using DataFiles;
using Game.Battle;
using Game.QuestRelated;
using Network;

namespace Game.PetRelated
{
    public static class PetVoucherManager
    {
        public class Entry
        {
            public ushort item_id { get; set; }
            public ushort pet_id { get; set; }
        }

        private static Dictionary<ushort, ushort> vouchers = new Dictionary<ushort, ushort>();
        private static PhxItemDat items;

        public static void Load(PhxItemDat data, string path)
        {
            var loaded = new Dictionary<ushort, ushort>();
            try
            {
                var entries = new JavaScriptSerializer().Deserialize<Entry[]>(File.ReadAllText(path));
                if (entries == null || entries.Length == 0) throw new InvalidDataException("Empty pet voucher table");
                foreach (var entry in entries)
                {
                    if (entry == null || entry.item_id == 0 || entry.pet_id == 0 || loaded.ContainsKey(entry.item_id))
                        throw new InvalidDataException("Invalid pet voucher mapping");
                    loaded.Add(entry.item_id, entry.pet_id);
                }
            }
            catch (Exception ex)
            {
                loaded.Clear();
                DebugSystem.Write("[PetVoucher] Disabled invalid configuration: " + ex.Message);
            }
            items = data;
            vouchers = loaded;
        }

        // True means this is a voucher and the request has been handled, including refusals.
        public static bool TryRedeem(Player player, byte slot, byte count = 1, ushort target = 0)
        {
            if (player?.Inv == null || slot < 1 || slot > 50) return false;
            lock (player.Inv.SyncRoot)
            {
                var item = player.Inv[slot];
                ushort petId;
                if (!vouchers.TryGetValue(item.ItemID, out petId)) return false;
                if (count != 1 || target != 0)
                {
                    player.SendHeadBanner("Use one pet voucher at a time on your character.");
                    return true;
                }
                if (item.Ammt == 0 || item.isLocked || PvEBattleManager.IsInBattle(player))
                {
                    player.SendHeadBanner("Cannot use this pet voucher right now. Voucher retained.");
                    return true;
                }
                if (items?.GetItemByID(item.ItemID) == null || DataFiles.SceneDataManager.GetNpcBaseStats(petId) == null)
                {
                    player.SendHeadBanner("This pet has no available data. Voucher retained.");
                    return true;
                }
                var roster = player.PlayerPets;
                if (roster == null) return true;
                lock (roster)
                {
                    if (roster.Values.Any(p => p != null && p.PetID != 0 && Player.IsSamePetOrCompanion(p.PetID, petId)))
                    {
                        player.SendHeadBanner("This pet is already in your party. Voucher retained.");
                        return true;
                    }
                    byte petSlot = 1;
                    while (petSlot <= 4 && roster.ContainsKey(petSlot)) petSlot++;
                    if (petSlot > 4 || roster.Values.Count(p => p != null && p.PetID != 0) >= 4)
                    {
                        player.SendHeadBanner("Your pet party is full. Free a slot first. Voucher retained.");
                        return true;
                    }
                    var pet = new Player.PlayerPetData
                    {
                        Slot = petSlot, PetID = petId, PetName = DataFiles.SceneDataManager.GetNpcName(petId),
                        Level = 1, Amity = 60
                    };
                    pet.InitializeBaseStats();
                    pet.NormalizeClientStats(true);
                    if (!player.RegisterClientPet(pet))
                    {
                        player.SendHeadBanner("No free pet slot is available. Voucher retained.");
                        return true;
                    }
                    roster.Add(petSlot, pet);
                    player.Inv.RemoveItem(slot, 1);
                    player.Send(QuestManager.CreatePetPacket(player, pet.PetID, pet.Slot,
                        pet.HP, pet.MaxHP, pet.SP, pet.MaxSP, pet.Amity, pet.Level,
                        pet.Str, pet.Con, pet.Int, pet.Wis, pet.Agi, pet.Exp, pet.Reborn, pet.Job));
                    QuestManager.SendPetProgression(player, pet);
                    var namePacket = QuestManager.CreatePetNamePacket(player, pet);
                    if (namePacket != null) player.Send(namePacket);
                    player.Send(Tools.FromFormat("bb", 23, 15));
                    player.SendHeadBanner(pet.PetName + " joined your party.");
                    player.SaveCharacterData();
                }
                return true;
            }
        }
    }
}
