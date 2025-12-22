using System;
using System.Collections.Generic;
using System.Data;
using Game.Code;
using RCLibrary.Core;

namespace DataBase
{
    /// <summary>
    /// Database for managing portal and warp destination data
    /// </summary>
    public sealed class PortalDataBase : RCLibrary.Core.DataBase
    {
        /// <summary>
        /// Static instance for global access from other projects
        /// </summary>
        public static PortalDataBase Instance { get; private set; }

        public PortalDataBase()
        {
            Instance = this;
        }

        public void VerifySetup()
        {
            DebugSystem.Write("Checking for portals table");

            // Create portals table
            if (GetDataTable("SELECT * FROM portals LIMIT 1") == null)
            {
                DebugSystem.Write("Setting up portals table");
                string createPortals = @"
                    CREATE TABLE portals (
                        id INTEGER PRIMARY KEY AUTOINCREMENT,
                        mapID INTEGER NOT NULL,
                        portalID INTEGER NOT NULL,
                        destID INTEGER NOT NULL
                    )";
                try { ExecuteNonQuery(createPortals); }
                catch (Exception ex) { DebugSystem.Write("Failed to create portals table: " + ex.Message); }
            }

            DebugSystem.Write("Checking for warp_destinations table");

            // Create warp_destinations table
            if (GetDataTable("SELECT * FROM warp_destinations LIMIT 1") == null)
            {
                DebugSystem.Write("Setting up warp_destinations table");
                string createDest = @"
                    CREATE TABLE warp_destinations (
                        id INTEGER PRIMARY KEY AUTOINCREMENT,
                        mapID INTEGER NOT NULL,
                        destID INTEGER NOT NULL,
                        dstMap INTEGER NOT NULL,
                        dstX INTEGER NOT NULL,
                        dstY INTEGER NOT NULL
                    )";
                try { ExecuteNonQuery(createDest); }
                catch (Exception ex) { DebugSystem.Write("Failed to create warp_destinations table: " + ex.Message); }
            }

            DebugSystem.Write("Portal database setup complete");
        }

        #region Portals CRUD

        public DataTable GetAllPortals()
        {
            try
            {
                return GetDataTable("SELECT * FROM portals ORDER BY mapID, portalID");
            }
            catch (Exception ex)
            {
                DebugSystem.Write(new ExceptionData(ex));
                return null;
            }
        }

        public DataTable GetPortalsForMap(uint mapID)
        {
            try
            {
                return GetDataTable("SELECT * FROM portals WHERE mapID = @mapID",
                    new DbParam("@mapID", mapID));
            }
            catch (Exception ex)
            {
                DebugSystem.Write(new ExceptionData(ex));
                return null;
            }
        }

        public bool AddPortal(uint mapID, byte portalID, byte destID)
        {
            try
            {
                ExecuteNonQuery("INSERT INTO portals (mapID, portalID, destID) VALUES (@mapID, @portalID, @destID)",
                    new DbParam("@mapID", mapID),
                    new DbParam("@portalID", portalID),
                    new DbParam("@destID", destID));
                DebugSystem.Write($"Added portal: Map {mapID}, Portal {portalID} -> Dest {destID}");
                return true;
            }
            catch (Exception ex)
            {
                DebugSystem.Write(new ExceptionData(ex));
                return false;
            }
        }

        public bool UpdatePortal(int id, uint mapID, byte portalID, byte destID)
        {
            try
            {
                ExecuteNonQuery("UPDATE portals SET mapID = @mapID, portalID = @portalID, destID = @destID WHERE id = @id",
                    new DbParam("@id", id),
                    new DbParam("@mapID", mapID),
                    new DbParam("@portalID", portalID),
                    new DbParam("@destID", destID));
                return true;
            }
            catch (Exception ex)
            {
                DebugSystem.Write(new ExceptionData(ex));
                return false;
            }
        }

        public bool DeletePortal(int id)
        {
            try
            {
                ExecuteNonQuery("DELETE FROM portals WHERE id = @id", new DbParam("@id", id));
                return true;
            }
            catch (Exception ex)
            {
                DebugSystem.Write(new ExceptionData(ex));
                return false;
            }
        }

        #endregion

        #region Destinations CRUD

        public DataTable GetAllDestinations()
        {
            try
            {
                return GetDataTable("SELECT * FROM warp_destinations ORDER BY mapID, destID");
            }
            catch (Exception ex)
            {
                DebugSystem.Write(new ExceptionData(ex));
                return null;
            }
        }

        public DataTable GetDestinationsForMap(uint mapID)
        {
            try
            {
                return GetDataTable("SELECT * FROM warp_destinations WHERE mapID = @mapID",
                    new DbParam("@mapID", mapID));
            }
            catch (Exception ex)
            {
                DebugSystem.Write(new ExceptionData(ex));
                return null;
            }
        }

        public bool AddDestination(uint mapID, byte destID, ushort dstMap, ushort dstX, ushort dstY)
        {
            try
            {
                ExecuteNonQuery("INSERT INTO warp_destinations (mapID, destID, dstMap, dstX, dstY) VALUES (@mapID, @destID, @dstMap, @dstX, @dstY)",
                    new DbParam("@mapID", mapID),
                    new DbParam("@destID", destID),
                    new DbParam("@dstMap", dstMap),
                    new DbParam("@dstX", dstX),
                    new DbParam("@dstY", dstY));
                DebugSystem.Write($"Added destination: Map {mapID}, Dest {destID} -> Map {dstMap} ({dstX}, {dstY})");
                return true;
            }
            catch (Exception ex)
            {
                DebugSystem.Write(new ExceptionData(ex));
                return false;
            }
        }

        public bool UpdateDestination(int id, uint mapID, byte destID, ushort dstMap, ushort dstX, ushort dstY)
        {
            try
            {
                ExecuteNonQuery("UPDATE warp_destinations SET mapID = @mapID, destID = @destID, dstMap = @dstMap, dstX = @dstX, dstY = @dstY WHERE id = @id",
                    new DbParam("@id", id),
                    new DbParam("@mapID", mapID),
                    new DbParam("@destID", destID),
                    new DbParam("@dstMap", dstMap),
                    new DbParam("@dstX", dstX),
                    new DbParam("@dstY", dstY));
                return true;
            }
            catch (Exception ex)
            {
                DebugSystem.Write(new ExceptionData(ex));
                return false;
            }
        }

        public bool DeleteDestination(int id)
        {
            try
            {
                ExecuteNonQuery("DELETE FROM warp_destinations WHERE id = @id", new DbParam("@id", id));
                return true;
            }
            catch (Exception ex)
            {
                DebugSystem.Write(new ExceptionData(ex));
                return false;
            }
        }

        #endregion

        #region Load into Dictionary for Map use

        /// <summary>
        /// Loads portals for a specific map into a dictionary
        /// </summary>
        public Dictionary<byte, DbPortal> LoadPortalsForMap(uint mapID)
        {
            var result = new Dictionary<byte, DbPortal>();
            try
            {
                var dt = GetPortalsForMap(mapID);
                if (dt != null)
                {
                    foreach (DataRow row in dt.Rows)
                    {
                        byte portalID = Convert.ToByte(row["portalID"]);
                        result[portalID] = new DbPortal
                        {
                            PortalID = portalID,
                            DestID = Convert.ToByte(row["destID"])
                        };
                    }
                }
            }
            catch (Exception ex)
            {
                DebugSystem.Write(new ExceptionData(ex));
            }
            return result;
        }

        /// <summary>
        /// Loads destinations for a specific map into a dictionary
        /// </summary>
        public Dictionary<byte, DbDestination> LoadDestinationsForMap(uint mapID)
        {
            var result = new Dictionary<byte, DbDestination>();
            try
            {
                var dt = GetDestinationsForMap(mapID);
                if (dt != null)
                {
                    foreach (DataRow row in dt.Rows)
                    {
                        byte destID = Convert.ToByte(row["destID"]);
                        result[destID] = new DbDestination
                        {
                            DestID = destID,
                            DstMap = Convert.ToUInt16(row["dstMap"]),
                            DstX = Convert.ToUInt16(row["dstX"]),
                            DstY = Convert.ToUInt16(row["dstY"])
                        };
                    }
                }
            }
            catch (Exception ex)
            {
                DebugSystem.Write(new ExceptionData(ex));
            }
            return result;
        }

        #endregion
    }

    /// <summary>
    /// Portal data loaded from database
    /// </summary>
    public class DbPortal
    {
        public byte PortalID { get; set; }
        public byte DestID { get; set; }
    }

    /// <summary>
    /// Destination data loaded from database
    /// </summary>
    public class DbDestination
    {
        public byte DestID { get; set; }
        public ushort DstMap { get; set; }
        public ushort DstX { get; set; }
        public ushort DstY { get; set; }
    }
}
