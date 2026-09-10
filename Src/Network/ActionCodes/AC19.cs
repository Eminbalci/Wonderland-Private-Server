using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Network;
using Game;
using Game.Code;
using wlo.pserver.core.Game;

namespace Network.ActionCodes
{
    /// <summary>
    /// Handles Battle Standby and Active Battle Companion state (AC 19).
    /// </summary>
    public class AC19 : AC
    {
        public override int ID { get { return 19; } }

        public override void ProcessPkt(Player player, RecievePacket p)
        {
            byte sub = (byte)(p.B ?? 0);
            p.SetPtr(6); // Skip packet header (4B), AC (1B), and Sub (1B)

            switch (sub)
            {
                case 1:
                case 4:
                    RecvSetBattlePet(player, p);
                    break;
                case 2:
                    Recv2(player, p);
                    break;
                case 5:
                    RecvRestBattlePet(player, p);
                    break;
                default:
                    DebugSystem.Write($"[AC19] Action Code 19,{sub} - attempting battle pet toggle");
                    RecvSetBattlePet(player, p);
                    break;
            }
        }

        /// <summary>
        /// Set Active Battle Pet: C->S [19, 1/4, pet_id or slot]
        /// </summary>
        private void RecvSetBattlePet(Player player, RecievePacket p)
        {
            try
            {
                uint petId = 0;
                if ((p.Count - p.GetPtr()) >= 4)
                {
                    petId = p.Unpack32();
                }
                else if ((p.Count - p.GetPtr()) >= 2)
                {
                    petId = p.Unpack16();
                }
                else if ((p.Count - p.GetPtr()) >= 1)
                {
                    petId = p.Unpack8();
                }

                // If petId is a slot number (1..4) rather than actual TemplateID
                if (petId <= 4 && player.PlayerPets != null && player.PlayerPets.TryGetValue((byte)petId, out var slotPet))
                {
                    petId = slotPet.PetID;
                }

                // If still 0, fallback to first pet in player's bag
                if (petId == 0 && player.PlayerPets != null && player.PlayerPets.Count > 0)
                {
                    petId = player.PlayerPets.Values.FirstOrDefault()?.PetID ?? 0;
                }

                if (petId == 0) return;

                player.ActivePetID = petId;

                Player.PlayerPetData activePet = null;
                if (player.PlayerPets != null)
                {
                    foreach (var kvp in player.PlayerPets)
                    {
                        if (kvp.Value.PetID == petId)
                        {
                            kvp.Value.IsBattle = true;
                            activePet = kvp.Value;
                        }
                        else
                        {
                            kvp.Value.IsBattle = false;
                        }
                    }
                }

                // 1. Send authentic AC 19:1 Set Battle Pet packet and full AC 15:4, AC 15:1, AC 19:4, AC 13:5, AC 5:8 to map
                player.Send(Tools.FromFormat("bbd", 19, 1, petId));
                player.BroadcastPetAppearance(petId, activePet?.PetName);

                // 2. Synchronize Pet Level & Stats so Party UI and Status Window show authentic Level and HP/SP
                if (activePet != null)
                {
                    byte slot = activePet.Slot;
                    uint petLv = (uint)Math.Max(1, (int)activePet.Level);
                    uint petHp = (uint)Math.Max(1, (int)activePet.HP);
                    uint petMaxHp = (uint)Math.Max(1, (int)activePet.MaxHP);
                    uint petSp = (uint)Math.Max(0, (int)activePet.SP);
                    uint petMaxSp = (uint)Math.Max(0, (int)activePet.MaxSP);

                    player.Send(Tools.FromFormat("bbbbdd", 8, 2, 35, slot, petLv, 0)); // Level
                    player.Send(Tools.FromFormat("bbbbdd", 8, 2, 37, slot, (uint)Math.Max(0, (int)petLv - 1), 0)); // Level offset
                    player.Send(Tools.FromFormat("bbbbdd", 8, 2, 38, slot, 0, 0)); // Potential points
                    player.Send(Tools.FromFormat("bbbbdd", 8, 2, 207, slot, petMaxHp, 0)); // MaxHP
                    player.Send(Tools.FromFormat("bbbbdd", 8, 2, 25, slot, petHp, 0)); // CurHP
                    player.Send(Tools.FromFormat("bbbbdd", 8, 2, 208, slot, petMaxSp, 0)); // MaxSP
                    player.Send(Tools.FromFormat("bbbbdd", 8, 1, 26, slot, petSp, 0)); // CurSP
                    player.Send(Tools.FromFormat("bbbbdd", 8, 1, 205, slot, petMaxHp, 0)); // FullHP
                    player.Send(Tools.FromFormat("bbbbdd", 8, 1, 206, slot, petMaxSp, 0)); // FullSP
                }

                player.Send(Tools.FromFormat("bbbs", 23, 57, 0, "Pet is now in Battle Mode!"));
                DebugSystem.Write($"[AC19] Player {player.CharName} set active battle pet ID {petId} (Lv.{activePet?.Level ?? 1})");
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[AC19.RecvSetBattlePet] Error: {ex.Message}");
            }
        }

        /// <summary>
        /// Rest Companion from Battle: C->S [19, 5]
        /// </summary>
        private void RecvRestBattlePet(Player player, RecievePacket p)
        {
            try
            {
                player.ActivePetID = 0;
                if (player.PlayerPets != null)
                {
                    foreach (var kvp in player.PlayerPets)
                    {
                        kvp.Value.IsBattle = false;
                    }
                }

                SendPacket restPkt = Tools.FromFormat("bbd", 19, 5, player.CharID);
                player.Send(restPkt);
                player.CurMap?.Broadcast(restPkt, "Ex", player.CharID);

                // Send AC 15:2 dismiss to map peers
                SendPacket dismissPkt = Tools.FromFormat("bbdb", 15, 2, player.CharID, (byte)1);
                player.CurMap?.Broadcast(dismissPkt, "Ex", player.CharID);

                // Send AC 5:8 appearance refresh
                SendPacket refreshPkt = Tools.FromFormat("bbdb", 5, 8, player.CharID, (byte)0);
                player.Send(refreshPkt);
                player.CurMap?.Broadcast(refreshPkt, "Ex", player.CharID);

                DebugSystem.Write($"[AC19] Player {player.CharName} rested active battle companion");
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[AC19.RecvRestBattlePet] Error: {ex.Message}");
            }
        }

        /// <summary>
        /// Toggle Battle Standby Stance: C->S [19, 2] -> Echo S->C [19, 2]
        /// </summary>
        private void Recv2(Player player, RecievePacket p)
        {
            try
            {
                SendPacket togglePkt = Tools.FromFormat("bb", 19, 2);
                player.Send(togglePkt);
                player.CurMap?.Broadcast(togglePkt);

                DebugSystem.Write($"[AC19] Player {player.CharName} toggled battle standby stance");
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[AC19.Recv2] Error: {ex.Message}");
            }
        }
    }
}
