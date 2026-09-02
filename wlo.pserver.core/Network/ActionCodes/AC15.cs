using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Game;
using Game.PlayerRelated;

namespace Network.ActionCodes
{
    public class AC15 : AC
    {
        public override int ID { get { return 15; } }

        public override void ProcessPkt(Player p, RecievePacket r)
        {
            if (p == null) return;

            byte subAction = r.B ?? 0;
            DebugSystem.Write($"[AC15] Player {p.CharName} sent Vehicle/Pet SubAction {subAction}");

            switch (subAction)
            {
                case 10: // Mount / Board Vehicle
                    try
                    {
                        uint charId = r.Unpack32();
                        ushort vehicleId = r.Unpack16();
                        VehicleManager.MountVehicle(p, vehicleId);
                    }
                    catch (Exception ex)
                    {
                        DebugSystem.Write($"[AC15:10] Exception mounting vehicle: {ex.Message}");
                    }
                    break;

                case 11: // Dismount / Exit Vehicle
                    try
                    {
                        VehicleManager.DismountVehicle(p);
                    }
                    catch (Exception ex)
                    {
                        DebugSystem.Write($"[AC15:11] Exception dismounting vehicle: {ex.Message}");
                    }
                    break;

                case 14: // Refuel Vehicle
                    try
                    {
                        ushort fuelAmount = r.Unpack16();
                        VehicleManager.RefuelVehicle(p, fuelAmount);
                    }
                    catch (Exception ex)
                    {
                        DebugSystem.Write($"[AC15:14] Exception refueling vehicle: {ex.Message}");
                    }
                    break;

                default:
                    DebugSystem.Write($"[AC15] Unhandled SubAction {subAction}");
                    break;
            }
        }
    }
}
