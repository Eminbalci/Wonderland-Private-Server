using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Game; // For Player
using Network; // For SendPacket

namespace Game.DataFiles
{
    #region Enums
    public enum Dialogtype
    {
        none,
        yes_no,
        invspacechek,
        petspacecheck,
        itemcheck,
        npccheck,

    }

    #endregion

    public class Dialog
    {
        public Dialogtype type;
        public int req;

        public byte[] reg;
        public byte[] yes;
        public byte[] no;

    }
    public class MapData
    {
        public UInt16 mapID;
        public UInt16 sceneID;
        public UInt32 dataptr;
        public UInt16 datalen;
        public UInt16 unknownword;
        public SceneInfo Scene;
        public List<BattleInfoEntries> ExtBattleInfo = new List<BattleInfoEntries>();
        public List<MapObjectEntries> Npclist = new List<MapObjectEntries>();
        public List<Entry_Exit_Point_Entries> Entry_Points = new List<Entry_Exit_Point_Entries>();
        public List<MiningAreaEntries> MiningAreas = new List<MiningAreaEntries>();
        public List<ItemsinMapEntries> ItemAreas = new List<ItemsinMapEntries>();
        public List<EventsinMapEntries> Events = new List<EventsinMapEntries>();
        public List<GroupEntries> Group = new List<GroupEntries>();
        public List<WarpInfo> WarpLoc = new List<WarpInfo>();
        public List<preEventEntries> PreEvents = new List<preEventEntries>();
        public List<InteractiveInfoEntries> InteractiveInfo = new List<InteractiveInfoEntries>();
        public List<ExtgroupEntries> ExtGroup = new List<ExtgroupEntries>();
        public categoryoffset offsetlist = new categoryoffset();
    }
    public class npcWalkStep
    {
        public UInt32 x;
        public UInt32 y;
        public UInt32 delay;
    }
    public class walkpattern
    {
        public byte unknownbyte1;
        public byte unknownbyte2;
        public byte unknownbyte3;
        public byte steps_needed;
        public UInt32 unknowndword1;
        public UInt32 unknowndword2;
        public List<npcWalkStep> walksteps = new List<npcWalkStep>();
    }
    public class MapObjectEntries
    {
        public enum Interaction_Type
        {
            none,
            Talking,
            Answering,
            Buying,
            Selling,
        }
        DateTime finishwalk_anim;
        DateTime LastBattle = new DateTime();
        int curstep = 0;

        #region MapNpc Info
        public UInt16 clickId;
        public string Name;
        public byte unknownbyte1;
        public UInt32 x;
        public UInt32 y;
        public List<byte> Events = new List<byte>();
        public List<byte> unknownbytearray2 = new List<byte>();
        public byte unknownbyte2; //usually 0
        public UInt32 npcId;
        public byte rotation;
        public byte unknownbyte4;//mobs 4,5 (4 has random walk)
        public byte unknownbyte5;
        public List<npcWalkStep> walksteps = new List<npcWalkStep>();
        public byte unknownbyte6;
        public byte unknownbyte7;
        public UInt32 unknowndword1;
        public UInt32 unknowndword2;
        public byte unknownbyte8;
        public byte unknownbyte9;
        public byte unknownbyte10;
        public List<walkpattern> walkpatterns = new List<walkpattern>();
        public UInt16 unknownword1;
        public UInt16 unknownword2; //usually 255
        public UInt16 unknownword3;
        public UInt16 unknownword4;
        public UInt16 unknownword5;
        #endregion

        public void Update(DateTime time, List<Player> players)
        {
            // Player state enum needs to be checked. Assuming Game.Code.Enums.PlayerState exists or needs alias.
            // Using int cast or just removing check if enum is not available?
            // Player Enums is likely in Game.Enums
            var list = players; // Removed .Where(c => (int)c.State == 2) due to missing State property 
                                // Or assume PlayerState is available in Game namespace.
                                // Let's assume Game.Enums is imported via Game... No, check imports.

            #region Walking
            if (finishwalk_anim < time)
                if (walksteps.Count > 0)
                {
                    try
                    {
                        if (curstep >= walksteps.Count) curstep = 0;

                        var step = walksteps[curstep];

                        foreach (var p in list)
                        {
                            SendPacket y = new SendPacket();
                            y.PackArray(new byte[] { 22, 2 });
                            y.Pack16(clickId);
                            y.Pack16((ushort)step.x);
                            y.Pack16((ushort)step.y);
                            y.Pack8(3);
                            p.Send(y);
                        }
                        curstep++;

                        finishwalk_anim = DateTime.Now.Add(new TimeSpan(0, 0, 1));
                    }
                    catch { }
                }
                else
                    switch (unknownbyte4)
                    {
                        case 2:
                        case 3:
                        case 4:
                            {
                                Random rand = new Random();
                                foreach (var p in list)
                                {
                                    SendPacket r = new SendPacket();
                                    r.PackArray(new byte[] { 22, 2 });
                                    r.Pack16(clickId);
                                    if (curstep >= rand.Next(2, 5))
                                    {
                                        r.Pack16((ushort)x);
                                        r.Pack16((ushort)y);
                                        curstep = 0;
                                    }
                                    else
                                    {
                                        int curx = (int)x;
                                        int cury = (int)y;


                                        r.Pack16((ushort)rand.Next(curx - (curx / curstep), curx + (curx / 2 + curstep)));
                                        r.Pack16((ushort)rand.Next(cury - (cury / curstep), cury + (cury / 2 + curstep)));
                                    }
                                    r.Pack8(3);
                                    p.Send(r);
                                }

                                curstep++;
                                finishwalk_anim = DateTime.Now.Add(new TimeSpan(0, 0, 2));
                            }
                            break;
                    }
            #endregion

            return;
        }

        // Commented out interaction logic that depends on cGlobal
        /*
        public void Interact(Interaction_Type l = Interaction_Type.none, byte answer = 0, byte slot = 0, byte ammt = 0)
        {
           // Removed because it depends on cGlobal which is not available in Core
        }
        */
    }
    public class Entry_Exit_Point_Entries
    {
        public UInt16 clickID;
        public string Name;
        public byte unknownbyte1;
        public UInt32 x;//map x location
        public UInt32 y;//map y location
        public List<byte> unknownbytearray1 = new List<byte>();
        public List<byte> unknownbytearray2 = new List<byte>();
        public byte unknownbyte2; //usually 0
        public UInt32 unknowndword1;
        public UInt32 unknowndword2;
        public byte unknownbyte3;
        public UInt32 unknowndword3;
        public UInt32 unknowndword4;
        public virtual bool Enter(ref Player src)
        {
            //portal Requirements
            return true;
        }

    }
    public class MiningAreaEntries
    {
        public UInt16 clickID;
        public string Name;
        public byte unknownbyte1;
        public UInt32 x;
        public UInt32 y;
        public List<byte> unknownbytearray1 = new List<byte>();
        public List<byte> unknownbytearray2 = new List<byte>();
        public byte unknownbyte2; //usually 0
        public UInt32 unknowndword1;
        public UInt32 unknowndword2;
        public byte unknownbyte3;
        public byte unknownbyte4;
    }
    public class ItemsinMapEntries
    {
        public UInt16 clickID;
        public bool pickedup;
        public TimeSpan dropin;
        public string Name;
        public byte unknownbyte1;
        public UInt32 x;
        public UInt32 y;
        public List<byte> unknownbytearray1 = new List<byte>();
        public List<byte> unknownbytearray2 = new List<byte>();
        public byte unknownbyte2; //usually 0
        public bool Drop(TimeSpan TimeSpan)
        {
            if (!pickedup) return false;
            else if (dropin < TimeSpan)
                return true;
            else
                return false;
        }
        public UInt32 itemID;
        public byte unknownbyte3;
        public byte unknownbyte4;
        public byte unknownbyte5;
        public UInt16 unknownword1;
        public UInt16 unknownword2;
    }
    public class EventSubEntry
    {
        public byte subIndex;
        public byte unknownbyte1;
        public UInt16 unknownword1;
        public UInt16 unknownword2;
        public UInt16 unknownword3;
        public UInt16 unknownword4;
        public UInt16 unknownword5;
        public UInt16 unknownword6;
        public UInt32 unknowndword1;
        public UInt32 unknowndword2;
        public List<EventSubSubEntry> SubEntry = new List<EventSubSubEntry>();
    }
    public struct EventSubSubEntry
    {
        public byte subsubIndex;
        public byte DialogPtr;
        public UInt16 dialog1;
        public UInt16 dialog2;
        public UInt16 dialog3;
        public UInt16 dialog4;
        public UInt32 unknowndword1;
        public UInt32 unknowndword2;
        public UInt32 unknowndword3;
    }
    public class EventsinMapEntries
    {
        public UInt16 clickID;
        public byte unknownbyte1;
        public string Name;
        public List<EventSubEntry> SubEntry = new List<EventSubEntry>();
    }
    public class GroupSubEntry
    {
        public byte unknownbyte1;
        public byte unknownbyte2;
        public byte unknownbyte3;
    }

    public class GroupEntries
    {
        public UInt16 clickID;
        public string Name;
        public byte unknownbyte1;
        public byte unknownbyte2;
        public byte unknownbyte3;
        public UInt16 unknownword1;
        public List<GroupSubEntry> subentry = new List<GroupSubEntry>();
        public byte unknownbyte4;
    }
    public class WarpInfo
    {
        public UInt16 clickID;
        public string Name;
        public UInt16 mapID;
        public UInt32 x;
        public UInt32 y;
        public byte unknownbyte1;
        public byte neededtopass;
        public byte unknownbyte3;
    }
    public class InteractiveInfoSubEntry
    {
        public byte unknownbyte1;
        public byte unknownbyte2;
        public byte unknownbyte3;
        public byte unknownbyte4;
        public byte unknownbyte5;
    }
    public class InteractiveInfoEntries
    {
        public UInt16 entryID;
        public string Name;
        public byte unknownbyte1;
        public byte unknownbyte2;
        public byte unknownbyte3;
        public List<InteractiveInfoSubEntry> subentry = new List<InteractiveInfoSubEntry>();
    }
    public class BattleInfoSubEntry
    {
        public byte unknownbyte1;
        public byte unknownbyte2;
        public byte unknownbyte3;
    }
    public class BattleInfoEntries
    {
        public UInt16 entryID;
        public string Name;
        public byte unknownbyte1;
        public byte unknownbyte2;
        public byte unknownbyte3;
        public byte unknownbyte4;
        public byte unknownbyte5;
        public byte unknownbyte6;
        public byte unknownbyte7;
        public byte unknownbyte8;
        public byte unknownbyte9;
        public byte unknownbyte10;
        public byte unknownbyte11;
        public byte unknownbyte12;
        public byte unknownbyte13;
        public byte unknownbyte14;
        public byte unknownbyte15;
        public byte unknownbyte16;
        public List<BattleInfoSubEntry> subentry1 = new List<BattleInfoSubEntry>();
        public List<BattleInfoSubEntry> subentry2 = new List<BattleInfoSubEntry>();
        public byte unknownbyte17;
    }
    public class preEventSubEntry
    {
        public byte subIndex;
        public List<byte> unknown = new List<byte>();
        public List<preEventSubSubEntry> subentry2 = new List<preEventSubSubEntry>();
    }
    public class preEventSubSubEntry
    {
        public byte subIndex;
        List<byte> unknown = new List<byte>();
    }
    public class preEventEntries
    {
        public UInt16 clickID;
        public byte unknownbyte1;
        public string Name;
        public List<preEventSubEntry> subentry1 = new List<preEventSubEntry>();


    }
    public class extGroupSubEntry
    {
        public byte Index;
        public byte unknownbyte1;
        public byte unknownbyte2;

    }
    public class ExtgroupEntries
    {
        public UInt16 clickID;
        public string Name;
        public byte unknownbyte1;
        public UInt16 unknownword1;
        public UInt16 unknownword2;
        public List<extGroupSubEntry> subEntry = new List<extGroupSubEntry>();
    }
    public struct categoryoffset
    {
        public UInt32 Scenedata;
        public UInt32 NPC;
        public UInt32 Entry;
        public UInt32 Mining;
        public UInt32 Items;
        public UInt32 Events;
        public UInt32 Groups;
        public UInt32 Warp;
        public UInt32 Interactiveinfo;
        public UInt32 Battleinfo;
        public UInt32 PreEvent;
        public UInt32 groupext;

    }
}
