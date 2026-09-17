using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.IO;
using Game;

namespace Game.DataFiles
{
    public class EveManager
    {
        byte[] eveData;
        Dictionary<ushort, MapData> Maps = new Dictionary<ushort, MapData>();
        public EveManager()
        {
        }
        public bool LoadFile(string filename)
        {
            DebugSystem.Write("Loading Eve.EMG.....");
            Maps.Clear();
            if (!File.Exists(filename)) return false;
            byte[] data = File.ReadAllBytes(filename);
            eveData = data;
            try { ReadData(data); }
            catch (Exception e) { DebugSystem.Write(e.ToString()); return false; }
            DebugSystem.Write("done loading Eve");
            return true;
        }
        public UInt16 GetSceneIDbyMapID(UInt16 id)
        {
            foreach (MapData r in Maps.Values)
                if (r.mapID == id)
                    return r.sceneID;
            return 0;
        }
        public MapData GetMapData(ushort ID)
        {
            if (Maps.ContainsKey(ID))
                return Maps[ID];
            return null;
        }

        public IReadOnlyDictionary<ushort, MapData> AllMaps => Maps;

        void ReadData(byte[] d)
        {
            int ptr = 0;
            // header is 12 bytes
            //entry length is important to know when to stop 
            ptr += 8;
            uint entrylen = GetDWord(d, ptr); ptr += 4;
            // Now Process by categories
            //MapData <---better to be call since houses all info within that map
            Load_MapEntries(ref ptr, d, entrylen);
            //after the long processing we now enter the second stage of the long processing
            //going throug each map... to pull the req offset data
            Load_ScenceData(ref ptr, d, (uint)d.Length);
            //after that we enter the third Stage here well will seek out the category infos
            //n inject the data we need into the maps within the Mapmanager
            //to avoid using EveManager as the ref to speed up the server
            //since we have the offest values we will go straight to it to pull certain data
            //tricky as hell i kno
            Load_FinalData();

            // Debug logic removed to break dependency on cGlobal
            /*
            List<KeyValuePair<byte,string>> str = new List<KeyValuePair<byte,string>>();
            List<KeyValuePair<byte, string>> str2 = new List<KeyValuePair<byte, string>>();
            List<byte> src = new List<byte>();
            List<byte> src2 = new List<byte>();
            */
            // Logic removed...
        }
        void Load_MapEntries(ref int ptr, byte[] d, uint len)
        {
            DebugSystem.Write("Stage 1\r\nReading Map Entries.....");
            //used to load MapEntries
            //File.Delete("LogOutput.txt");
            for (int a = 0; a < len; a++)
            {
                MapData tmp = new MapData();
                tmp.mapID = GetWord(d, ptr); ptr += 2;
                tmp.sceneID = GetWord(d, ptr); ptr += 2;
                tmp.dataptr = GetDWord(d, ptr); ptr += 4;
                tmp.datalen = GetWord(d, ptr); ptr += 2;
                if (!Maps.ContainsKey(tmp.mapID))
                    Maps.Add(tmp.mapID, tmp);
            }
            DebugSystem.Write("Finished Reading Maps");
        }
        void Load_ScenceData(ref int ptr2, byte[] d, uint len)
        {
            DebugSystem.Write("Stage2\r\nReading Scene Data Category Offset Entries.....");
            //used to load Scence Data Entries

            foreach (MapData scen in Maps.Values)
            {
                int count = 0;
                int ptr = (int)scen.dataptr;
                //find the Map that the data belongs to
                UInt16 id = GetWord(d, ptr); ptr += 2;///skipping        
                if (scen == null) throw new Exception("Sence NUll found");
                scen.unknownword = GetWord(d, ptr); ptr += 2;
                ptr = ((int)(scen.datalen + scen.dataptr) - 44);
                // ptr += ptr2;
                #region Offsets to Data
                categoryoffset j = new categoryoffset();
                j.NPC = GetDWord(d, ptr); ptr += 4;
                j.Entry = GetDWord(d, ptr); ptr += 4;
                j.Mining = GetDWord(d, ptr); ptr += 4;
                j.Items = GetDWord(d, ptr); ptr += 4;
                j.Events = GetDWord(d, ptr); ptr += 4;
                j.Groups = GetDWord(d, ptr); ptr += 4;
                j.Warp = GetDWord(d, ptr); ptr += 4;
                j.Interactiveinfo = GetDWord(d, ptr); ptr += 4;
                j.Battleinfo = GetDWord(d, ptr); ptr += 4;
                j.PreEvent = GetDWord(d, ptr); ptr += 4;
                j.groupext = GetDWord(d, ptr); ptr += 4;
                scen.offsetlist = j;
                #endregion

                #region data Check
                if (scen.datalen == 1028)
                {
                }
                if (ptr2 > scen.datalen + scen.dataptr)
                {
                }
                if (ptr < scen.datalen + scen.dataptr)
                {
                    //DebugSystem.Write("Map" + scen.mapID.ToString() + " data mistmach... counted length-"
                    //+ count.ToString() + " lower than req" + scen.datalen);
                    ptr2 += scen.datalen;
                }
                else if (count > scen.datalen)
                {
                    //DebugSystem.Write("Map" + scen.mapID.ToString() + " data mistmach... counted length-"
                    //+ count.ToString() + " greater than req" + scen.datalen);
                    ptr2 += scen.datalen;
                }
                ptr2 += scen.datalen;
                if (ptr2 > 4760262)
                    break;
                #endregion
            }
            DebugSystem.Write("EveData has been Read successfully");
            DebugSystem.Write("Map Data found -" + Maps.Count.ToString());
        }
        void Load_FinalData()
        {
            Stopwatch timer = new Stopwatch();
            DebugSystem.Write("Stage 3..Loading Data for Maps...");
            timer.Reset();
            timer.Start();
            //this will be the loader
            //Entry n Warp for Now
            int ct = 0;
            foreach (MapData r in Maps.Values.ToList())
            {
                ct++;
                try
                {
                    //map.MapID = r.mapID;
                    //if (r.mapID == 11016)
                    //{
                    //}
                    //try
                    //{
                    //    map.sceneinfo = globals.gDataManager.SceneManager.GetSceneByID(r.sceneID);
                    //    map.name = map.sceneinfo.Name;
                    //    map.mapData = r;
                    //}
                    //catch { }

                    Maps[r.mapID].Entry_Points = LoadEntryEntries(r);
                    Maps[r.mapID].Npclist = LoadNpcEntries(r);
                    Maps[r.mapID].MiningAreas = LoadMiningEntries(r);
                    Maps[r.mapID].ItemAreas = LoadItemEntries(r);
                    Maps[r.mapID].WarpLoc = LoadWarpEntries(r);
                    Maps[r.mapID].InteractiveInfo = LoadInteractiveEntries(r);
                    Maps[r.mapID].Events = LoadEventEntries(r);
                    Maps[r.mapID].Group = LoadGroupEntries(r);
                    Maps[r.mapID].ExtGroup = LoadgroupExtEntries(r);
                    Maps[r.mapID].PreEvents = LoadpreEventEntries(r);
                    Maps[r.mapID].ExtBattleInfo = LoadBattleEntries(r);
                }
                catch (Exception e) { DebugSystem.Write(e.ToString()); }
            }

            DebugSystem.Write("Operation took-> " + timer.Elapsed.ToString());
            timer.Stop();
            DebugSystem.Write("Data loaded successfully");
        }
        private static Encoding _big5Encoding = null;
        public static Encoding Big5Encoding
        {
            get
            {
                if (_big5Encoding == null)
                {
                    try
                    {
                        _big5Encoding = Encoding.GetEncoding(950); // Big5 Traditional Chinese
                    }
                    catch
                    {
                        try { _big5Encoding = Encoding.GetEncoding("big5"); }
                        catch { _big5Encoding = Encoding.Default; }
                    }
                }
                return _big5Encoding;
            }
        }

        public static string DecodeEveString(byte[] buffer, int offset, int maxLength)
        {
            if (buffer == null || offset < 0 || offset >= buffer.Length || maxLength <= 0) return "";
            int available = Math.Min(maxLength, buffer.Length - offset);
            if (available <= 0) return "";

            // Find actual string length before trailing / embedded null bytes
            int len = 0;
            while (len < available && buffer[offset + len] != 0)
            {
                len++;
            }
            if (len == 0) return "";

            try
            {
                string decoded = Big5Encoding.GetString(buffer, offset, len).Trim('\0', ' ', '\r', '\n');
                if (!string.IsNullOrEmpty(decoded))
                    return decoded;
            }
            catch
            {
            }

            try
            {
                return Encoding.UTF8.GetString(buffer, offset, len).Trim('\0', ' ', '\r', '\n');
            }
            catch
            {
                return Encoding.Default.GetString(buffer, offset, len).Trim('\0', ' ', '\r', '\n');
            }
        }

        public List<MapObjectEntries> LoadNpcEntries(MapData y)
        {
            try
            {
                byte[] d = eveData;
                List<MapObjectEntries> map = new List<MapObjectEntries>();
                if (y == null || y.offsetlist.NPC == 0)
                    return map;

                var ptr = (int)y.offsetlist.NPC + (int)y.dataptr;
                if (ptr < 0 || ptr + 2 > d.Length)
                    return map;

                UInt16 elen = GetWord(d, ptr); ptr += 2;
                for (int a = 0; a < elen; a++)
                {
                    if (ptr + 50 > d.Length) break;

                    MapObjectEntries tmp = new MapObjectEntries();
                    tmp.clickId = GetWord(d, ptr); ptr += 2;
                    tmp.Name = DecodeEveString(d, ptr, 20);
                    ptr += 20;

                    if (ptr + 10 > d.Length) break;
                    tmp.unknownbyte1 = d[ptr]; ptr++;
                    tmp.x = GetDWord(d, ptr); ptr += 4;
                    tmp.y = GetDWord(d, ptr); ptr += 4;

                    if (ptr >= d.Length) break;
                    int blen = d[ptr]; ptr++;
                    for (int c = 0; c < blen; c++)
                    {
                        if (ptr >= d.Length) break;
                        tmp.Events.Add(d[ptr]); ptr++;
                    }

                    if (ptr >= d.Length) break;
                    blen = d[ptr]; ptr++;
                    for (int c = 0; c < blen; c++)
                    {
                        if (ptr >= d.Length) break;
                        tmp.unknownbytearray2.Add(d[ptr]); ptr++;
                    }

                    if (ptr + 8 > d.Length) break;
                    tmp.unknownbyte2 = d[ptr]; ptr++;
                    tmp.npcId = GetDWord(d, ptr); ptr += 4;
                    tmp.rotation = d[ptr]; ptr++;
                    tmp.unknownbyte4 = d[ptr]; ptr++;
                    tmp.unknownbyte5 = d[ptr]; ptr++;

                    if (ptr >= d.Length) break;
                    blen = d[ptr]; ptr++;
                    for (int c = 0; c < blen; c++)
                    {
                        if (ptr + 12 > d.Length) break;
                        npcWalkStep r = new npcWalkStep();
                        r.x = GetDWord(d, ptr); ptr += 4;
                        r.y = GetDWord(d, ptr); ptr += 4;
                        r.delay = GetDWord(d, ptr); ptr += 4;
                        tmp.walksteps.Add(r);
                    }

                    if (ptr + 13 > d.Length) break;
                    tmp.unknownbyte6 = d[ptr]; ptr++;
                    tmp.unknownbyte7 = d[ptr]; ptr++;
                    tmp.unknowndword1 = GetDWord(d, ptr); ptr += 4;
                    tmp.unknowndword2 = GetDWord(d, ptr); ptr += 4;
                    tmp.unknownbyte8 = d[ptr]; ptr++;
                    tmp.unknownbyte9 = d[ptr]; ptr++;
                    tmp.unknownbyte10 = d[ptr]; ptr++;

                    if (ptr >= d.Length) break;
                    blen = d[ptr]; ptr++;
                    for (int c = 0; c < blen; c++)
                    {
                        if (ptr + 92 > d.Length) break;
                        walkpattern r = new walkpattern();
                        r.unknownbyte1 = d[ptr]; ptr++;
                        r.unknownbyte2 = d[ptr]; ptr++;
                        r.unknownbyte3 = d[ptr]; ptr++;
                        r.steps_needed = d[ptr]; ptr++;
                        r.unknowndword1 = GetDWord(d, ptr); ptr += 4;
                        r.unknowndword2 = GetDWord(d, ptr); ptr += 4;
                        for (int dd = 0; dd < 10; dd++)
                        {
                            npcWalkStep st = new npcWalkStep();
                            st.x = GetDWord(d, ptr); ptr += 4;
                            st.y = GetDWord(d, ptr); ptr += 4;
                            r.walksteps.Add(st);
                        }
                        tmp.walkpatterns.Add(r);
                    }

                    if (ptr + 8 > d.Length) break;
                    tmp.unknownword1 = GetWord(d, ptr); ptr += 2;
                    tmp.unknownword2 = GetWord(d, ptr); ptr += 2;
                    tmp.unknownword3 = GetWord(d, ptr); ptr += 2;
                    tmp.unknownword4 = GetWord(d, ptr); ptr += 2;

                    map.Add(tmp);
                }
                return map;
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"Error loading NPC entries for map {y.mapID}: {ex.Message}");
                return new List<MapObjectEntries>();
            }
        }
        public List<Entry_Exit_Point_Entries> LoadEntryEntries(MapData r)
        {
            try
            {
                var d = eveData;
                List<Entry_Exit_Point_Entries> map = new List<Entry_Exit_Point_Entries>();
                if (r == null || r.offsetlist.Entry == 0)
                    return map;

                var ptr = (int)r.offsetlist.Entry + (int)r.dataptr;
                #region Entry/Exit Points
                //Entry/Exit Points

                // Bounds check
                if (ptr < 0 || ptr + 2 > d.Length)
                    return map;

                var elen = GetWord(d, ptr); ptr += 2;
                for (int a = 0; a < elen; a++)
                {
                    // Bounds check before reading entry
                    if (ptr + 50 > d.Length) // Minimum size check
                        break;

                    Entry_Exit_Point_Entries tmp = new Entry_Exit_Point_Entries();
                    tmp.clickID = GetWord(d, ptr); ptr += 2;
                    tmp.Name = DecodeEveString(d, ptr, 20);
                    ptr += 20;
                    tmp.unknownbyte1 = d[ptr]; ptr++;
                    tmp.x = GetDWord(d, ptr); ptr += 4;
                    tmp.y = GetDWord(d, ptr); ptr += 4;

                    if (ptr >= d.Length) break;
                    int blen = d[ptr]; ptr++;
                    for (int c = 0; c < blen; c++)
                    {
                        if (ptr >= d.Length) break;
                        tmp.unknownbytearray1.Add(d[ptr]); ptr++;
                    }

                    if (ptr >= d.Length) break;
                    blen = d[ptr]; ptr++;
                    for (int c = 0; c < blen; c++)
                    {
                        if (ptr >= d.Length) break;
                        tmp.unknownbytearray2.Add(d[ptr]); ptr++;
                    }

                    if (ptr + 17 > d.Length) break; // Check remaining fields
                    tmp.unknownbyte2 = d[ptr]; ptr++;
                    tmp.unknowndword1 = GetDWord(d, ptr); ptr += 4;
                    tmp.unknowndword2 = GetDWord(d, ptr); ptr += 4;
                    tmp.unknownbyte3 = d[ptr]; ptr++;
                    tmp.unknowndword3 = GetDWord(d, ptr); ptr += 4;
                    tmp.unknowndword4 = GetDWord(d, ptr); ptr += 4;

                    map.Add(tmp);
                }
                #endregion
                return map;
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"Error loading Entry entries for map {r.mapID}: {ex.Message}");
                return new List<Entry_Exit_Point_Entries>();
            }
        }
        public List<MiningAreaEntries> LoadMiningEntries(MapData r)
        {
            try
            {
                var d = eveData;
                List<MiningAreaEntries> map = new List<MiningAreaEntries>();
                if (r == null || r.offsetlist.Mining == 0)
                    return map;

                var ptr = (int)r.offsetlist.Mining + (int)r.dataptr;
                #region Mining Area

                if (ptr < 0 || ptr + 2 > d.Length)
                    return map;

                var elen = GetWord(d, ptr); ptr += 2;
                for (int a = 0; a < elen; a++)
                {
                    if (ptr + 40 > d.Length) break; // Minimum size check

                    MiningAreaEntries tmp = new MiningAreaEntries();
                    tmp.clickID = GetWord(d, ptr); ptr += 2;
                    tmp.Name = DecodeEveString(d, ptr, 20);
                    ptr += 20;
                    if (ptr >= d.Length) break;
                    tmp.unknownbyte1 = d[ptr]; ptr++;
                    tmp.x = GetDWord(d, ptr); ptr += 4;
                    tmp.y = GetDWord(d, ptr); ptr += 4;

                    if (ptr >= d.Length) break;
                    int blen = d[ptr]; ptr++;
                    for (int c = 0; c < blen; c++)
                    {
                        if (ptr >= d.Length) break;
                        tmp.unknownbytearray1.Add(d[ptr]); ptr++;
                    }

                    if (ptr >= d.Length) break;
                    blen = d[ptr]; ptr++;
                    for (int c = 0; c < blen; c++)
                    {
                        if (ptr >= d.Length) break;
                        tmp.unknownbytearray2.Add(d[ptr]); ptr++;
                    }

                    if (ptr + 11 > d.Length) break; // Check remaining fields
                    tmp.unknownbyte2 = d[ptr]; ptr++;
                    blen = d[ptr]; ptr++;
                    tmp.unknowndword1 = GetDWord(d, ptr); ptr += 4;
                    tmp.unknowndword2 = GetDWord(d, ptr); ptr += 4;
                    tmp.unknownbyte3 = d[ptr]; ptr++;
                    tmp.unknownbyte4 = d[ptr]; ptr += 2;

                    map.Add(tmp);
                }
                #endregion
                return map;
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"Error loading Mining entries for map {r.mapID}: {ex.Message}");
                return new List<MiningAreaEntries>();
            }
        }
        public List<ItemsinMapEntries> LoadItemEntries(MapData r)
        {
            try
            {
                var d = eveData;
                List<ItemsinMapEntries> map = new List<ItemsinMapEntries>();
                if (r == null || r.offsetlist.Items == 0)
                    return map;

                var ptr = (int)r.offsetlist.Items + (int)r.dataptr;
                #region Items in Map

                if (ptr < 0 || ptr + 2 > d.Length)
                    return map;

                var elen = GetWord(d, ptr); ptr += 2;
                for (int a = 0; a < elen; a++)
                {
                    if (ptr + 35 > d.Length) break; // Minimum size check

                    ItemsinMapEntries tmp = new ItemsinMapEntries();
                    tmp.clickID = GetWord(d, ptr); ptr += 2;

                    if (ptr >= d.Length) break;
                    int len = d[ptr]; ptr++;
                    tmp.Name = DecodeEveString(d, ptr, 19);
                    ptr += 19;

                    if (ptr >= d.Length) break;
                    tmp.unknownbyte1 = d[ptr]; ptr++;
                    tmp.x = GetDWord(d, ptr); ptr += 4;
                    tmp.y = GetDWord(d, ptr); ptr += 4;

                    if (ptr >= d.Length) break;
                    int blen = d[ptr]; ptr++;
                    for (int c = 0; c < blen; c++)
                    {
                        if (ptr >= d.Length) break;
                        tmp.unknownbytearray1.Add(d[ptr]); ptr++;
                    }

                    if (ptr >= d.Length) break;
                    blen = d[ptr]; ptr++;
                    for (int c = 0; c < blen; c++)
                    {
                        if (ptr >= d.Length) break;
                        tmp.unknownbytearray2.Add(d[ptr]); ptr++;
                    }

                    if (ptr + 10 > d.Length) break; // Check remaining fields
                    tmp.unknownbyte2 = d[ptr]; ptr++;
                    tmp.itemID = GetDWord(d, ptr); ptr += 4;
                    tmp.unknownbyte3 = d[ptr]; ptr++;
                    tmp.unknownbyte4 = d[ptr]; ptr++;
                    tmp.unknownbyte5 = d[ptr]; ptr++;
                    tmp.unknownword1 = GetWord(d, ptr); ptr += 2;
                    tmp.unknownword2 = GetWord(d, ptr); ptr += 2;
                    map.Add(tmp);
                }
                #endregion
                return map;
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"Error loading Item entries for map {r.mapID}: {ex.Message}");
                return new List<ItemsinMapEntries>();
            }
        }
        public List<EventsinMapEntries> LoadEventEntries(MapData r)
        {
            try
            {
                var d = eveData;
                List<EventsinMapEntries> map = new List<EventsinMapEntries>();
                if (r == null || r.offsetlist.Events == 0)
                    return map;

                var ptr = (int)r.offsetlist.Events + (int)r.dataptr;
                #region Events in Map

                if (ptr < 0 || ptr + 2 > d.Length)
                    return map;

                var elen = GetWord(d, ptr); ptr += 2;
                for (int a = 0; a < elen; a++)
                {
                    if (ptr + 25 > d.Length) break;

                    EventsinMapEntries tmp = new EventsinMapEntries();
                    tmp.clickID = GetWord(d, ptr); ptr += 2;

                    if (ptr >= d.Length) break;
                    tmp.unknownbyte1 = d[ptr]; ptr++;
                    tmp.Name = DecodeEveString(d, ptr, 20);
                    ptr += 20;

                    if (ptr >= d.Length) break;
                    int blen = d[ptr]; ptr++;
                    for (int c = 0; c < blen; c++)
                    {
                        if (ptr + 25 > d.Length) break;
                        EventSubEntry u = new EventSubEntry();
                        u.subIndex = d[ptr]; ptr++;
                        u.unknownbyte1 = d[ptr]; ptr++;
                        u.unknownword1 = GetWord(d, ptr); ptr += 2;
                        u.unknownword2 = GetWord(d, ptr); ptr += 2;
                        u.unknownword3 = GetWord(d, ptr); ptr += 2;
                        u.unknownword4 = GetWord(d, ptr); ptr += 2;
                        u.unknownword5 = GetWord(d, ptr); ptr += 2;
                        u.unknownword6 = GetWord(d, ptr); ptr += 2;
                        u.unknowndword1 = GetDWord(d, ptr); ptr += 4;
                        u.unknowndword2 = GetDWord(d, ptr); ptr += 4;

                        if (ptr >= d.Length) break;
                        int blen2 = d[ptr]; ptr++;
                        for (int cs = 0; cs < blen2; cs++)
                        {
                            if (ptr + 22 > d.Length) break;
                            EventSubSubEntry ur = new EventSubSubEntry();
                            ur.subsubIndex = d[ptr]; ptr++;
                            ur.DialogPtr = d[ptr]; ptr++;
                            ur.dialog1 = GetWord(d, ptr); ptr += 2;
                            ur.dialog2 = GetWord(d, ptr); ptr += 2;
                            ur.dialog3 = GetWord(d, ptr); ptr += 2;
                            ur.dialog4 = GetWord(d, ptr); ptr += 2;
                            ur.unknowndword1 = GetDWord(d, ptr); ptr += 4;
                            ur.unknowndword2 = GetDWord(d, ptr); ptr += 4;
                            ur.unknowndword3 = GetDWord(d, ptr); ptr += 4;
                            u.SubEntry.Add(ur);
                        }
                        tmp.SubEntry.Add(u);
                    }
                    map.Add(tmp);
                }
                #endregion
                return map;
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"Error loading Event entries for map {r.mapID}: {ex.Message}");
                return new List<EventsinMapEntries>();
            }
        }
        public List<GroupEntries> LoadGroupEntries(MapData r)
        {
            try
            {
                var d = eveData;
                List<GroupEntries> map = new List<GroupEntries>();
                if (r == null || r.offsetlist.Groups == 0)
                    return map;

                var ptr = (int)r.offsetlist.Groups + (int)r.dataptr;
                #region Groups

                if (ptr < 0 || ptr + 2 > d.Length)
                    return map;

                var elen = GetWord(d, ptr); ptr += 2;
                for (int a = 0; a < elen; a++)
                {
                    if (ptr + 30 > d.Length) break;

                    GroupEntries tmp = new GroupEntries();
                    tmp.clickID = GetWord(d, ptr); ptr += 2;
                    tmp.Name = DecodeEveString(d, ptr, 20);
                    ptr += 20;
                    if (ptr + 6 > d.Length) break;
                    tmp.unknownbyte1 = d[ptr]; ptr++;
                    tmp.unknownbyte2 = d[ptr]; ptr++;
                    tmp.unknownbyte3 = d[ptr]; ptr++;
                    tmp.unknownword1 = GetWord(d, ptr); ptr += 2;

                    if (ptr >= d.Length) break;
                    int blen = d[ptr]; ptr++;
                    for (int c = 0; c < blen; c++)
                    {
                        if (ptr + 3 > d.Length) break;
                        GroupSubEntry y = new GroupSubEntry();
                        y.unknownbyte1 = d[ptr]; ptr++;
                        y.unknownbyte2 = d[ptr]; ptr++;
                        y.unknownbyte3 = d[ptr]; ptr++;
                        tmp.subentry.Add(y);
                    }
                    if (ptr >= d.Length) break;
                    tmp.unknownbyte4 = d[ptr]; ptr++;
                    map.Add(tmp);
                }
                #endregion
                return map;
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"Error loading Group entries for map {r.mapID}: {ex.Message}");
                return new List<GroupEntries>();
            }
        }
        public List<WarpInfo> LoadWarpEntries(MapData r)
        {
            try
            {
                var d = eveData;
                List<WarpInfo> map = new List<WarpInfo>();
                if (r == null || r.offsetlist.Warp == 0)
                    return map;

                var ptr = (int)r.offsetlist.Warp + (int)r.dataptr;
                #region Warp Info

                if (ptr < 0 || ptr + 2 > d.Length)
                    return map;

                var elen = GetWord(d, ptr); ptr += 2;
                for (int a = 0; a < elen; a++)
                {
                    if (ptr + 35 > d.Length) break;

                    WarpInfo tmp = new WarpInfo();
                    tmp.clickID = GetWord(d, ptr); ptr += 2;
                    tmp.Name = DecodeEveString(d, ptr, 20);
                    ptr += 20;
                    if (ptr + 13 > d.Length) break;
                    tmp.mapID = GetWord(d, ptr); ptr += 2;
                    tmp.x = GetDWord(d, ptr); ptr += 4;
                    tmp.y = GetDWord(d, ptr); ptr += 4;
                    tmp.unknownbyte1 = d[ptr]; ptr++;
                    tmp.neededtopass = d[ptr]; ptr++;
                    tmp.unknownbyte3 = d[ptr]; ptr++;
                    map.Add(tmp);
                }
                #endregion
                return map;
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"Error loading Warp entries for map {r.mapID}: {ex.Message}");
                return new List<WarpInfo>();
            }
        }
        public List<InteractiveInfoEntries> LoadInteractiveEntries(MapData r)
        {
            try
            {
                var d = eveData;
                List<InteractiveInfoEntries> map = new List<InteractiveInfoEntries>();
                if (r == null || r.offsetlist.Interactiveinfo == 0)
                    return map;

                var ptr = (int)r.offsetlist.Interactiveinfo + (int)r.dataptr;
                #region Interactive info

                if (ptr < 0 || ptr + 2 > d.Length)
                    return map;

                var elen = GetWord(d, ptr); ptr += 2;
                for (int a = 0; a < elen; a++)
                {
                    if (ptr + 25 > d.Length) break;

                    InteractiveInfoEntries tmp = new InteractiveInfoEntries();
                    tmp.entryID = GetWord(d, ptr); ptr += 2;
                    tmp.Name = DecodeEveString(d, ptr, 20);
                    ptr += 20;
                    if (ptr + 4 > d.Length) break;
                    tmp.unknownbyte1 = d[ptr]; ptr++;
                    tmp.unknownbyte2 = d[ptr]; ptr++;
                    tmp.unknownbyte3 = d[ptr]; ptr++;

                    if (ptr >= d.Length) break;
                    int blen = d[ptr]; ptr++;
                    for (int c = 0; c < blen; c++)
                    {
                        if (ptr + 5 > d.Length) break;
                        InteractiveInfoSubEntry y = new InteractiveInfoSubEntry();
                        y.unknownbyte1 = d[ptr]; ptr++;
                        y.unknownbyte2 = d[ptr]; ptr++;
                        y.unknownbyte3 = d[ptr]; ptr++;
                        y.unknownbyte4 = d[ptr]; ptr++;
                        y.unknownbyte5 = d[ptr]; ptr++;
                        tmp.subentry.Add(y);
                    }
                    map.Add(tmp);
                }
                #endregion
                return map;
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"Error loading Interactive entries for map {r.mapID}: {ex.Message}");
                return new List<InteractiveInfoEntries>();
            }
        }
        public List<BattleInfoEntries> LoadBattleEntries(MapData r)
        {
            try
            {
                var d = eveData;
                List<BattleInfoEntries> map = new List<BattleInfoEntries>();
                if (r == null || r.offsetlist.Battleinfo == 0)
                    return map;

                var ptr = (int)r.offsetlist.Battleinfo + (int)r.dataptr;
                #region BattleInfomation

                if (ptr < 0 || ptr + 2 > d.Length)
                    return map;

                var elen = GetWord(d, ptr); ptr += 2;
                for (int a = 0; a < elen; a++)
                {
                    if (ptr + 40 > d.Length) break;

                    BattleInfoEntries tmp = new BattleInfoEntries();
                    tmp.entryID = GetWord(d, ptr); ptr += 2;
                    tmp.Name = DecodeEveString(d, ptr, 20);
                    ptr += 20;
                    if (ptr + 18 > d.Length) break;
                    tmp.unknownbyte1 = d[ptr]; ptr++;
                    tmp.unknownbyte2 = d[ptr]; ptr++;
                    tmp.unknownbyte3 = d[ptr]; ptr++;
                    tmp.unknownbyte4 = d[ptr]; ptr++;
                    tmp.unknownbyte5 = d[ptr]; ptr++;
                    tmp.unknownbyte6 = d[ptr]; ptr++;
                    tmp.unknownbyte7 = d[ptr]; ptr++;
                    tmp.unknownbyte8 = d[ptr]; ptr++;
                    tmp.unknownbyte9 = d[ptr]; ptr++;
                    tmp.unknownbyte10 = d[ptr]; ptr++;
                    tmp.unknownbyte11 = d[ptr]; ptr++;
                    tmp.unknownbyte12 = d[ptr]; ptr++;
                    tmp.unknownbyte13 = d[ptr]; ptr++;
                    tmp.unknownbyte14 = d[ptr]; ptr++;
                    tmp.unknownbyte15 = d[ptr]; ptr++;
                    tmp.unknownbyte16 = d[ptr]; ptr++;

                    if (ptr >= d.Length) break;
                    int blen = d[ptr]; ptr++;
                    for (int c = 0; c < blen; c++)
                    {
                        if (ptr + 3 > d.Length) break;
                        BattleInfoSubEntry y = new BattleInfoSubEntry();
                        y.unknownbyte1 = d[ptr]; ptr++;
                        y.unknownbyte2 = d[ptr]; ptr++;
                        y.unknownbyte3 = d[ptr]; ptr++;
                        tmp.subentry1.Add(y);
                    }

                    if (ptr >= d.Length) break;
                    blen = d[ptr]; ptr++;
                    for (int c = 0; c < blen; c++)
                    {
                        if (ptr + 3 > d.Length) break;
                        BattleInfoSubEntry y = new BattleInfoSubEntry();
                        y.unknownbyte1 = d[ptr]; ptr++;
                        y.unknownbyte2 = d[ptr]; ptr++;
                        y.unknownbyte3 = d[ptr]; ptr++;
                        tmp.subentry2.Add(y);
                    }

                    if (ptr >= d.Length) break;
                    tmp.unknownbyte17 = d[ptr]; ptr++;
                    map.Add(tmp);
                }
                #endregion
                return map;
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"Error loading Battle entries for map {r.mapID}: {ex.Message}");
                return new List<BattleInfoEntries>();
            }
        }
        public List<preEventEntries> LoadpreEventEntries(MapData r)
        {
            try
            {
                var d = eveData;
                List<preEventEntries> map = new List<preEventEntries>();
                if (r == null || r.offsetlist.PreEvent == 0)
                    return map;

                var ptr = (int)r.offsetlist.PreEvent + (int)r.dataptr;
                #region Pre-Event

                if (ptr < 0 || ptr + 2 > d.Length)
                    return map;

                var elen = GetWord(d, ptr); ptr += 2;
                for (int a = 0; a < elen; a++)
                {
                    if (ptr + 25 > d.Length) break;

                    preEventEntries tmp = new preEventEntries();
                    tmp.clickID = GetWord(d, ptr); ptr += 2;

                    if (ptr >= d.Length) break;
                    tmp.unknownbyte1 = d[ptr]; ptr++;
                    tmp.Name = DecodeEveString(d, ptr, 20);
                    ptr += 20;

                    if (ptr >= d.Length) break;
                    int blen = d[ptr]; ptr++;
                    for (int c = 0; c < blen; c++)
                    {
                        if (ptr + 23 > d.Length) break;
                        preEventSubEntry y = new preEventSubEntry();
                        y.subIndex = d[ptr]; ptr++;
                        for (int ga = 0; ga < 21; ga++)
                        {
                            if (ptr >= d.Length) break;
                            y.unknown.Add(d[ptr]); ptr++;
                        }

                        if (ptr >= d.Length) break;
                        int blen2 = d[ptr]; ptr++;
                        for (int cl = 0; cl < blen2; cl++)
                        {
                            if (ptr + 22 > d.Length) break;
                            preEventSubSubEntry fy = new preEventSubSubEntry();
                            fy.subIndex = d[ptr]; ptr++;
                            for (int ga = 0; ga < 21; ga++)
                            {
                                if (ptr >= d.Length) break;
                                fy.unknown.Add(d[ptr]); ptr++;
                            }
                            y.subentry2.Add(fy);
                        }
                        tmp.subentry1.Add(y);
                    }
                    map.Add(tmp);
                }
                #endregion
                return map;
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"Error loading PreEvent entries for map {r.mapID}: {ex.Message}");
                return new List<preEventEntries>();
            }
        }
        public List<ExtgroupEntries> LoadgroupExtEntries(MapData r)
        {
            try
            {
                var d = eveData;
                List<ExtgroupEntries> map = new List<ExtgroupEntries>();
                if (r == null || r.offsetlist.groupext == 0)
                    return map;

                var ptr = (int)r.offsetlist.groupext + (int)r.dataptr;
                #region Ext Group Info

                if (ptr < 0 || ptr + 2 > d.Length)
                    return map;

                var elen = GetWord(d, ptr); ptr += 2;
                for (int a = 0; a < elen; a++)
                {
                    if (ptr + 28 > d.Length) break;

                    ExtgroupEntries tmp = new ExtgroupEntries();
                    tmp.clickID = GetWord(d, ptr); ptr += 2;
                    tmp.Name = DecodeEveString(d, ptr, 20);
                    ptr += 20;
                    if (ptr + 7 > d.Length) break;
                    tmp.unknownbyte1 = d[ptr]; ptr++;
                    tmp.unknownword1 = GetWord(d, ptr); ptr += 2;
                    tmp.unknownword2 = GetWord(d, ptr); ptr += 2;

                    if (ptr + 2 > d.Length) break;
                    int blen = GetWord(d, ptr); ptr += 2;
                    for (int c = 0; c < blen; c++)
                    {
                        if (ptr + 3 > d.Length) break;
                        extGroupSubEntry e = new extGroupSubEntry();
                        e.Index = d[ptr]; ptr++;
                        e.unknownbyte1 = d[ptr]; ptr++;
                        e.unknownbyte2 = d[ptr]; ptr++;
                        tmp.subEntry.Add(e);
                    }
                    map.Add(tmp);
                }
                #endregion
                return map;
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"Error loading ExtGroup entries for map {r.mapID}: {ex.Message}");
                return new List<ExtgroupEntries>();
            }
        }

        #region load helpers
        private UInt16 GetWord(byte[] data, int ptr)
        {
            if (ptr < 0 || ptr + 1 >= data.Length)
                return 0;
            return (UInt16)((data[ptr + 1] << 8) + data[ptr]);
        }
        private UInt32 GetDWord(byte[] data, int ptr)
        {
            if (ptr < 0 || ptr + 3 >= data.Length)
                return 0;
            return (UInt32)((data[ptr + 3] << 24) + (data[ptr + 2] << 16) + (data[ptr + 1] << 8) + data[ptr]);
        }
        #endregion
    }
}
