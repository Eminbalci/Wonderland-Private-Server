using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Game.Code;
using Network;

namespace Game.Experimental
{
    [Flags]
    public enum TerrainDomain : byte
    {
        None = 0,
        Land = 1,
        Water = 2,
        Air = 4,
        Subsea = 8
    }

    public class VehicleDefinition
    {
        public ushort VehicleItemId { get; set; }
        public string Name { get; set; }
        public TerrainDomain Domain { get; set; }
        public int MaxFuel { get; set; }
        public int MaxSpeedModifier { get; set; }
        public byte PassengerCapacity { get; set; }

        public VehicleDefinition(ushort id, string name, TerrainDomain domain, int maxFuel, int speed, byte capacity)
        {
            VehicleItemId = id;
            Name = name;
            Domain = domain;
            MaxFuel = maxFuel;
            MaxSpeedModifier = speed;
            PassengerCapacity = capacity;
        }
    }

    public class MountedVehicleState
    {
        public uint DriverCharId { get; set; }
        public string DriverName { get; set; }
        public ushort VehicleItemId { get; set; }
        public string VehicleName { get; set; }
        public int CurrentFuel { get; set; }
        public int MaxFuel { get; set; }
        public int Durability { get; set; }
        public byte MaxSeats { get; set; }
        public List<uint> PassengerCharIds { get; } = new List<uint>();
        public DateTime MountedAt { get; set; }

        public MountedVehicleState(Player driver, VehicleDefinition def)
        {
            DriverCharId = driver?.CharID ?? 0;
            DriverName = driver?.CharName ?? "Pilot";
            VehicleItemId = def.VehicleItemId;
            VehicleName = def.Name;
            CurrentFuel = def.MaxFuel;
            MaxFuel = def.MaxFuel;
            Durability = 100;
            MaxSeats = def.PassengerCapacity;
            MountedAt = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Vehicle Fleet, Fuel Consumption & Mount Durability Engine (AC 84 & AC 86 / Spec 28 & 42).
    /// Manages the 29-vehicle taxonomy, multi-seat transport boarding, fuel depletion, and repair.
    /// </summary>
    public class VehicleTransportEngine
    {
        private readonly ConcurrentDictionary<ushort, VehicleDefinition> m_definitions =
            new ConcurrentDictionary<ushort, VehicleDefinition>();
        private readonly ConcurrentDictionary<uint, MountedVehicleState> m_activeMounts =
            new ConcurrentDictionary<uint, MountedVehicleState>();
        private readonly object m_mountLock = new object();

        public int ActiveMountCount => m_activeMounts.Count;
        public int RegisteredVehicleTypeCount => m_definitions.Count;

        public VehicleTransportEngine()
        {
            InitializeFleetTaxonomy();
        }

        private void InitializeFleetTaxonomy()
        {
            // Seed the canonical 29-vehicle fleet taxonomy from client TrafficSetting
            Register(20001, "Wooden Raft", TerrainDomain.Water, 100, 120, 4);
            Register(20002, "Canoe", TerrainDomain.Water, 150, 140, 2);
            Register(20003, "Motorboat", TerrainDomain.Water, 500, 220, 4);
            Register(20004, "Sailboat", TerrainDomain.Water, 300, 160, 4);
            Register(20005, "Steamship", TerrainDomain.Water, 1000, 200, 8);
            Register(20006, "Submarine", TerrainDomain.Water | TerrainDomain.Subsea, 800, 180, 4);
            Register(20007, "Flying Carpet", TerrainDomain.Air, 250, 250, 2);
            Register(20008, "Hot Air Balloon", TerrainDomain.Air, 400, 180, 4);
            Register(20009, "Propeller Plane", TerrainDomain.Air, 600, 300, 2);
            Register(20010, "Zeppelin Airship", TerrainDomain.Air, 1500, 280, 8);
            Register(20011, "Steam Locomotive", TerrainDomain.Land, 1200, 220, 6);
            Register(20012, "Mechanical Beetle", TerrainDomain.Land, 300, 150, 1);
            Register(20013, "Sports Car", TerrainDomain.Land, 450, 240, 2);
            Register(20014, "Hovercraft", TerrainDomain.Land | TerrainDomain.Water, 600, 210, 4);
            Register(20015, "Space Capsule", TerrainDomain.Air | TerrainDomain.Subsea, 2000, 350, 4);
            Register(20016, "Bicycle", TerrainDomain.Land, 100, 130, 1);
            Register(20017, "Wooden Wagon", TerrainDomain.Land, 250, 140, 4);
            Register(20018, "Scooter", TerrainDomain.Land, 300, 160, 2);
            Register(20019, "Glider", TerrainDomain.Air, 150, 200, 1);
            Register(20020, "Helicopter", TerrainDomain.Air, 750, 270, 4);
            Register(20021, "UFO Saucer", TerrainDomain.Air | TerrainDomain.Land | TerrainDomain.Water, 2500, 400, 4);
            Register(20022, "Bathysphere", TerrainDomain.Subsea, 700, 130, 2);
            Register(20023, "Magic Broom", TerrainDomain.Air, 200, 220, 1);
            Register(20024, "Dragon Boat", TerrainDomain.Water, 350, 170, 6);
            Register(20025, "Snowmobile", TerrainDomain.Land, 400, 190, 2);
            Register(20026, "Jet Ski", TerrainDomain.Water, 450, 240, 2);
            Register(20027, "Solar Cruiser", TerrainDomain.Air | TerrainDomain.Water, 1200, 260, 6);
            Register(20028, "Armored Tank", TerrainDomain.Land, 900, 160, 4);
            Register(20029, "Galactic Shuttle", TerrainDomain.Air, 3000, 450, 8);
        }

        public void Register(ushort id, string name, TerrainDomain domain, int maxFuel, int speed, byte capacity)
        {
            m_definitions[id] = new VehicleDefinition(id, name, domain, maxFuel, speed, capacity);
        }

        public VehicleDefinition GetDefinition(ushort vehicleItemId)
        {
            if (m_definitions.TryGetValue(vehicleItemId, out var def))
            {
                return def;
            }
            return null;
        }

        public bool MountVehicle(Player driver, ushort vehicleItemId)
        {
            if (driver == null) return false;
            lock (m_mountLock)
            {
                if (!m_definitions.TryGetValue(vehicleItemId, out var def))
                {
                    def = new VehicleDefinition(vehicleItemId, "Standard Mount", TerrainDomain.Land, 200, 150, 2);
                    m_definitions[vehicleItemId] = def;
                }

                var state = new MountedVehicleState(driver, def);
                m_activeMounts[driver.CharID] = state;

                DebugSystem.Write($"[VehicleTransport] {driver.CharName} mounted {def.Name} (Capacity: {def.PassengerCapacity})");
                return true;
            }
        }

        public bool DismountVehicle(Player driver)
        {
            if (driver == null) return false;
            lock (m_mountLock)
            {
                if (m_activeMounts.TryRemove(driver.CharID, out var state))
                {
                    DebugSystem.Write($"[VehicleTransport] {driver.CharName} dismounted {state.VehicleName}");
                    return true;
                }
                return false;
            }
        }

        public int ConsumeFuel(Player driver, int distanceTravelled)
        {
            if (driver == null || distanceTravelled <= 0) return 0;
            lock (m_mountLock)
            {
                if (!m_activeMounts.TryGetValue(driver.CharID, out var state)) return 0;

                int burnRate = Math.Max(1, distanceTravelled / 20);
                state.CurrentFuel = Math.Max(0, state.CurrentFuel - burnRate);

                if (state.CurrentFuel == 0)
                {
                    DebugSystem.Write($"[VehicleTransport] {driver.CharName}'s vehicle {state.VehicleName} ran out of fuel!");
                }
                return state.CurrentFuel;
            }
        }

        public bool RefuelVehicle(Player driver, ushort fuelItemId, int fuelAmount = 100)
        {
            if (driver == null) return false;
            lock (m_mountLock)
            {
                if (!m_activeMounts.TryGetValue(driver.CharID, out var state)) return false;

                state.CurrentFuel = Math.Min(state.MaxFuel, state.CurrentFuel + fuelAmount);
                DebugSystem.Write($"[VehicleTransport] {driver.CharName} refueled {state.VehicleName}: {state.CurrentFuel}/{state.MaxFuel}");
                return true;
            }
        }

        public bool RepairVehicle(Player driver, ushort hammerItemId, int durabilityPoints = 25)
        {
            if (driver == null) return false;
            lock (m_mountLock)
            {
                if (!m_activeMounts.TryGetValue(driver.CharID, out var state)) return false;

                state.Durability = Math.Min(100, state.Durability + durabilityPoints);
                DebugSystem.Write($"[VehicleTransport] {driver.CharName} repaired {state.VehicleName} durability to {state.Durability}%");
                return true;
            }
        }

        public bool AddPassenger(Player driver, Player passenger)
        {
            if (driver == null || passenger == null) return false;
            lock (m_mountLock)
            {
                if (!m_activeMounts.TryGetValue(driver.CharID, out var state)) return false;

                if (state.PassengerCharIds.Count >= state.MaxSeats - 1)
                {
                    DebugSystem.Write($"[VehicleTransport] {state.VehicleName} is at full capacity!");
                    return false;
                }

                if (!state.PassengerCharIds.Contains(passenger.CharID))
                {
                    state.PassengerCharIds.Add(passenger.CharID);
                    DebugSystem.Write($"[VehicleTransport] {passenger.CharName} boarded {driver.CharName}'s {state.VehicleName}");
                    return true;
                }
                return false;
            }
        }

        public bool RemovePassenger(Player driver, uint passengerCharId)
        {
            if (driver == null) return false;
            lock (m_mountLock)
            {
                if (!m_activeMounts.TryGetValue(driver.CharID, out var state)) return false;
                return state.PassengerCharIds.Remove(passengerCharId);
            }
        }

        public MountedVehicleState GetMountedVehicle(uint driverCharId)
        {
            if (m_activeMounts.TryGetValue(driverCharId, out var state))
            {
                return state;
            }
            return null;
        }

        public IReadOnlyList<MountedVehicleState> GetAllActiveMounts()
        {
            return new List<MountedVehicleState>(m_activeMounts.Values);
        }

        public IReadOnlyList<VehicleDefinition> GetAllDefinitions()
        {
            return new List<VehicleDefinition>(m_definitions.Values);
        }
    }
}
