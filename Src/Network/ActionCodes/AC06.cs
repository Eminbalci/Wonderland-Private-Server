using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Network;
using Game;

namespace Network.ActionCodes
{
    public class AC06:AC
    {
        public override int ID { get { return 06; } }
        public override void ProcessPkt(Player r, RecievePacket p)
        {
                switch (p.Unpack8())
                {
                    case 1: Recv1(r, p); break;
                    //case 2: Recv2(r, p); break;
                    //case 3: Recv3(r, p); break;
                }
        }
        void Recv1(Player p, RecievePacket g)
        {
            if (p.NativeEventActive || Game.Battle.PvEBattleManager.IsInBattle(p)) return;
            if (g.Count > 5)
            {
                ushort previousX = p.CurX;
                ushort previousY = p.CurY;
                byte direction = g.Unpack8();
                p.CurX = g.Unpack16();
                p.CurY = g.Unpack16();
                double movedDistance = Math.Sqrt(Math.Pow((int)p.CurX - previousX, 2) + Math.Pow((int)p.CurY - previousY, 2));

                p.CurMap?.Broadcast(Tools.FromFormat("bbdbww", 6, 1, p.CharID, direction, p.CurX, p.CurY));

                if (!Game.Battle.PvEBattleManager.IsInBattle(p) && p.CurMap is GameMap eventMap &&
                    Game.Maps.EveEventInterpreter.TryExecuteRegion(p, eventMap, previousX, previousY)) return;

                // Vehicle Durability / Fuel wear on movement
                if (p.ActiveVehicleID != 0 && p.Inv != null)
                {
                    p.Inv.ApplyVehicleWear((ushort)p.ActiveVehicleID, 1);

                    // Check if sailing in a raft on Map 11016 and stepping onto the beach/sand
                    if (p.CurMap != null && p.CurMap.MapID == 11016 && p.CurX >= 280 && p.CurY >= 950)
                    {
                        DebugSystem.Write($"[AC06] Player {p.CharName} reached the beach shore on Map 11016 at ({p.CurX},{p.CurY}). Wrecking raft and disembarking.");
                        Game.PlayerRelated.VehicleManager.WreckVehicle(p);
                    }
                }

                // Proximity and Random Encounter Step Progression
                if (!Game.Battle.PvEBattleManager.IsInBattle(p) && p.CurMap is GameMap gMap)
                {
                    // Ignore duplicate position packets and protect players immediately after entering a map/battle.
                    if (movedDistance == 0 || p.IsInBattleCooldown() || p.IsInMapEncounterGracePeriod())
                    {
                        return;
                    }

                    ushort mid = (ushort)gMap.MapID;
                    if (!Game.Battle.PvEBattleManager.IsSafeTownMap(mid))
                    {
                        // 1. Proximity check to nearby roaming monsters. 180 units was large enough
                        // to trigger combat on the first visible movement from many spawn points.
                        const double proximityEncounterRadius = 72.0;
                        var nearbyMob = gMap.NpcList?
                            .OfType<Game.Maps.QuestNpc>()
                            .FirstOrDefault(n => n.IsWildMonster() && !n.IsBroken
                                && !p.HiddenNpcClickIDs.Contains(n.CickID)
                                && Game.QuestRelated.PreEventInterpreter.ShouldNpcBeVisible(p, mid, n.CickID)
                                && Math.Sqrt(Math.Pow((int)p.CurX - (int)n.X, 2) + Math.Pow((int)p.CurY - (int)n.Y, 2)) <= proximityEncounterRadius);

                        if (nearbyMob != null)
                        {
                            if (p.ProximityEncounterArmed)
                            {
                                Game.Battle.PvEBattleManager.StartProximityEncounter(p, gMap, nearbyMob);
                                if (Game.Battle.PvEBattleManager.IsInBattle(p))
                                {
                                    p.ProximityEncounterArmed = false;
                                    p.StepsSinceLastBattle = 0;
                                    p.NextBattleSteps = Game.Maps.QuestNpc.NextRandom(18, 35);
                                }
                            }
                            return;
                        }

                        // A proximity encounter is edge-triggered: the player must leave every
                        // monster's radius before standing near a monster can start another battle.
                        p.ProximityEncounterArmed = true;

                        // 2. Step-based encounter progression
                        p.StepsSinceLastBattle++;
                        if (p.StepsSinceLastBattle >= p.NextBattleSteps)
                        {
                            p.StepsSinceLastBattle = 0;
                            p.NextBattleSteps = Game.Maps.QuestNpc.NextRandom(18, 35);
                            Game.Battle.PvEBattleManager.CheckAndTriggerRandomEncounter(p);
                        }
                    }

                    // Floor step triggers handled natively or through dedicated map portals
                }
            }
        }
        void Recv2(Player p, RecievePacket r)
        {
        }
        void Recv3(Player p, RecievePacket r)
        {
        }
    }
}
