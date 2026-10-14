using System;
using Game;
using Game.Experimental;
using Network;

namespace Network.ActionCodes
{
    /// <summary>
    /// AC 84: Vehicle Fueling, Durability Restoration & Mount Control Protocol.
    /// Handles refueling, repairing mounts, and boarding/dismounting vehicles.
    /// </summary>
    public class AC84 : AC
    {
        public override int ID => 84;

        public override void ProcessPkt(Player c, RecievePacket p)
        {
            if (c == null || p == null) return;

            byte subCode = p.B ?? 1;
            DebugSystem.Write($"[AC84] Vehicle action from {c.CharName}: SubCode={subCode}");

            try
            {
                if (ExperimentalFeaturesManager.Instance.EnableVehicleTransport)
                {
                    switch (subCode)
                    {
                        case 1: // Refuel Vehicle
                            ushort fuelItemId = 28001; // Gasoline / Diesel Fuel
                            if (p.Buffer.Length >= 8)
                            {
                                fuelItemId = p.Unpack16();
                            }
                            ExperimentalFeaturesManager.Instance.VehicleTransport.RefuelVehicle(c, fuelItemId, 100);

                            SendPacket respRefuel = new SendPacket();
                            respRefuel.Pack8((byte)ID);
                            respRefuel.Pack8(1);
                            respRefuel.Pack16(100); // Fuel Amount
                            respRefuel.Pack8(1);   // Success
                            c.Send(respRefuel);
                            break;

                        case 2: // Repair Vehicle
                            ushort hammerItemId = 25001;
                            ExperimentalFeaturesManager.Instance.VehicleTransport.RepairVehicle(c, hammerItemId, 25);

                            SendPacket respRepair = new SendPacket();
                            respRepair.Pack8((byte)ID);
                            respRepair.Pack8(2);
                            respRepair.Pack16(100); // Durability restored
                            respRepair.Pack8(1);
                            c.Send(respRepair);
                            break;

                        case 3: // Mount Vehicle
                            ushort mountItemId = 20001;
                            if (p.Buffer.Length >= 8)
                            {
                                mountItemId = p.Unpack16();
                            }
                            bool mounted = ExperimentalFeaturesManager.Instance.VehicleTransport.MountVehicle(c, mountItemId);

                            SendPacket respMount = new SendPacket();
                            respMount.Pack8((byte)ID);
                            respMount.Pack8(3);
                            respMount.Pack16(mountItemId);
                            respMount.Pack8(mounted ? (byte)1 : (byte)0);
                            c.Send(respMount);
                            break;

                        case 4: // Dismount Vehicle
                            bool dismounted = ExperimentalFeaturesManager.Instance.VehicleTransport.DismountVehicle(c);

                            SendPacket respDismount = new SendPacket();
                            respDismount.Pack8((byte)ID);
                            respDismount.Pack8(4);
                            respDismount.Pack8(dismounted ? (byte)1 : (byte)0);
                            c.Send(respDismount);
                            break;

                        default:
                            SendPacket respDef = new SendPacket();
                            respDef.Pack8((byte)ID);
                            respDef.Pack8(subCode);
                            respDef.Pack16(100);
                            respDef.Pack8(1);
                            c.Send(respDef);
                            break;
                    }
                }
                else
                {
                    SendPacket resp = new SendPacket();
                    resp.Pack8((byte)ID);
                    resp.Pack8(subCode);
                    resp.Pack16(100);
                    resp.Pack8(1);
                    c.Send(resp);
                }
            }
            catch (Exception ex)
            {
                DebugSystem.Write(new ExceptionData(ex));
            }
        }
    }
}
