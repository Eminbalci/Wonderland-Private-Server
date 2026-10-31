using System;
using System.Data;
using Game.Maps;
using Network;

namespace Game.PlayerRelated
{
    /// <summary>
    /// Teleport Scroll (verified in aLogin.exe). Using the scroll opens the client world map;
    /// the client then sends AC74:1 [slot, point] to record the current spot or, after the
    /// teleport animation, AC74:2 [slot, point] to teleport. S->C [74,1,point,map,x,y] fills
    /// the client's saved points (player +0x2358 + point*6).
    /// </summary>
    public static class TeleportScrollManager
    {
        private static bool _tableReady;
        private static readonly object _tableLock = new object();

        public static int GetPointCount(ushort itemId)
        {
            switch (itemId)
            {
                case 34163: return 1; // Teleport Scroll
                case 34195: return 2; // client 0x13ee78: 0x8593 enables point 2
                case 34373: return 3; // client 0x13ee78: 0x8645 enables point 3
                default: return 0;
            }
        }

        public static bool IsTeleportScroll(ushort itemId) { return GetPointCount(itemId) > 0; }

        private static DataBase.CharacterDataBase Db()
        {
            var db = DataBase.CharacterDataBase.GlobalInstance;
            if (db == null) return null;
            lock (_tableLock)
            {
                if (!_tableReady)
                {
                    db.ExecuteNonQuery("CREATE TABLE IF NOT EXISTS character_teleport_points (charID INTEGER NOT NULL, idx INTEGER NOT NULL, mapID INTEGER NOT NULL, posX INTEGER NOT NULL, posY INTEGER NOT NULL, PRIMARY KEY(charID, idx))");
                    _tableReady = true;
                }
            }
            return db;
        }

        private static SendPacket PointPacket(byte idx, ushort map, ushort x, ushort y)
        {
            var pkt = new SendPacket();
            pkt.Pack8(74); pkt.Pack8(1); pkt.Pack8(idx);
            pkt.Pack16(map); pkt.Pack16(x); pkt.Pack16(y);
            return pkt;
        }

        public static void SendAllPoints(Player p)
        {
            if (p == null || p.CharID == 0) return;
            var db = Db();
            DataTable dt = db?.GetDataTable("SELECT idx, mapID, posX, posY FROM character_teleport_points WHERE charID = " + p.CharID);
            if (dt == null) return;
            foreach (DataRow row in dt.Rows)
            {
                byte idx = Convert.ToByte(row["idx"]);
                if (idx < 1 || idx > 3) continue;
                p.Send(PointPacket(idx, Convert.ToUInt16(row["mapID"]), Convert.ToUInt16(row["posX"]), Convert.ToUInt16(row["posY"])));
            }
        }

        private static bool ValidScroll(Player p, byte slot, byte idx)
        {
            if (slot < 1 || slot > 50) return false;
            int max = GetPointCount(p.Inv[slot].ItemID);
            if (max == 0) { p.SendHeadBanner("A Teleport Scroll is required."); return false; }
            if (idx < 1 || idx > max) { p.SendHeadBanner("This scroll cannot use that point."); return false; }
            return true;
        }

        public static void Record(Player p, byte slot, byte idx)
        {
            if (p == null || p.CurMap == null) return;
            lock (p.Inv.SyncRoot)
            {
                if (!ValidScroll(p, slot, idx)) return;
            }
            ushort map = (ushort)p.CurMap.MapID, x = p.CurX, y = p.CurY;
            var db = Db();
            if (db == null) return;
            db.ExecuteNonQuery("REPLACE INTO character_teleport_points (charID, idx, mapID, posX, posY) VALUES (" +
                p.CharID + ", " + idx + ", " + map + ", " + x + ", " + y + ")");
            p.Send(PointPacket(idx, map, x, y));
            p.SendHeadBanner("Location recorded.");
            DebugSystem.Write($"[Teleport] {p.CharName} recorded point {idx}: map {map} ({x},{y})");
        }

        public static void TeleportTo(Player p, byte slot, byte idx)
        {
            if (p == null || p.CurMap == null) return;
            if (Game.Battle.PvEBattleManager.IsInBattle(p))
            {
                p.SendHeadBanner("Cannot teleport during battle.");
                return;
            }
            var db = Db();
            DataTable dt = db?.GetDataTable("SELECT mapID, posX, posY FROM character_teleport_points WHERE charID = " + p.CharID + " AND idx = " + idx);
            if (dt == null || dt.Rows.Count == 0 || Convert.ToUInt16(dt.Rows[0]["mapID"]) == 0)
            {
                p.SendHeadBanner("No location recorded for this point.");
                return;
            }
            var warp = new WarpData
            {
                DstMap = Convert.ToUInt16(dt.Rows[0]["mapID"]),
                DstX_Axis = Convert.ToUInt16(dt.Rows[0]["posX"]),
                DstY_Axis = Convert.ToUInt16(dt.Rows[0]["posY"])
            };
            lock (p.Inv.SyncRoot)
            {
                if (!ValidScroll(p, slot, idx)) return;
                p.Inv.RemoveItem(slot, 1, true);
            }
            p.CurMap.Teleport(TeleportType.Regular, p, 0, warp);
            p.SaveCharacterData();
            DebugSystem.Write($"[Teleport] {p.CharName} teleported to point {idx}: map {warp.DstMap} ({warp.DstX_Axis},{warp.DstY_Axis})");
        }
    }
}
