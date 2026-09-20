$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$handler = Get-Content -LiteralPath (Join-Path $repo 'Src\Network\ActionCodes\AC19.cs') -Raw
# Compile the actual AC19 handler with an isolated transport/database double.
# This validates handler state/packet choices, not native-client rendering.
$harness = @"
namespace Network {
    public class AC { public virtual int ID { get { return 0; } } public virtual void ProcessPkt(Game.Player p, RecievePacket r) {} }
    public class SendPacket { public object[] Values; }
    public class RecievePacket {
        private byte[] data; private int pos;
        public RecievePacket(byte[] data) { this.data = data; }
        public byte? B { get { return data[5]; } }
        public int Count { get { return data.Length; } }
        public void SetPtr(int p) { pos = p; }
        public int GetPtr() { return pos; }
        public uint Unpack32() { uint v = BitConverter.ToUInt32(data, pos); pos += 4; return v; }
        public ushort Unpack16() { ushort v = BitConverter.ToUInt16(data, pos); pos += 2; return v; }
        public byte Unpack8() { return data[pos++]; }
    }
    public static class Tools { public static SendPacket FromFormat(string format, params object[] values) { return new SendPacket { Values = values }; } }
}
namespace Game.Code { public static class DebugSystem { public static void Write(string text) { if (text.Contains("Error:")) throw new Exception(text); } } }
namespace wlo.pserver.core.Game { internal class NamespaceMarker {} }
namespace Game {
    public class TestMap {
        public List<Network.SendPacket> Packets = new List<Network.SendPacket>();
        public void Broadcast(Network.SendPacket p, string mode = "", uint owner = 0) { Packets.Add(p); }
    }
    public class Player {
        public class PlayerPetData { public byte Slot, ClientSlot; public uint PetID; public string PetName; public byte Level; public bool IsBattle; }
        public Dictionary<byte, PlayerPetData> PlayerPets = new Dictionary<byte, PlayerPetData>();
        public uint CharID = 4510001, ActivePetID; public string CharName = "Mizaki";
        public TestMap CurMap = new TestMap();
        public List<Network.SendPacket> Packets = new List<Network.SendPacket>();
        public int Saves, AppearanceUpdates;
        public PlayerPetData GetClientPet(byte slot) { return PlayerPets.Values.FirstOrDefault(p => p.ClientSlot == slot && slot != 0); }
        public void Send(Network.SendPacket p) { Packets.Add(p); }
        public void SaveCharacterData() { Saves++; }
        public void BroadcastPetAppearance(uint id, string name) { AppearanceUpdates++; }
        public static uint GetCompanionBroadcastId(uint id) { return id == 12032 ? 12178u : id; }
        public static bool IsSamePetOrCompanion(uint a, uint b) { return GetCompanionBroadcastId(a) == GetCompanionBroadcastId(b); }
    }
}
public static class PetBattleRosterChecks {
    static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    static Network.RecievePacket Select(uint id) {
        return new Network.RecievePacket(new byte[] { 0xF4, 0x44, 6, 0, 19, 1, (byte)id, (byte)(id >> 8), (byte)(id >> 16), (byte)(id >> 24) });
    }
    public static string Run() {
        var p = new Game.Player();
        var robin = new Game.Player.PlayerPetData { Slot = 1, ClientSlot = 2, PetID = 12032, PetName = "Robinson1", Level = 5, IsBattle = true };
        var xao = new Game.Player.PlayerPetData { Slot = 2, ClientSlot = 1, PetID = 14156, PetName = "Xaolan", Level = 1 };
        p.PlayerPets.Add(1, robin); p.PlayerPets.Add(2, xao); p.ActivePetID = 12178;
        var handler = new Network.ActionCodes.AC19();
        uint[] selections = { 14156, 12178, 14156, 14156, 12178 };
        foreach (uint id in selections) {
            p.Packets.Clear(); p.CurMap.Packets.Clear();
            handler.ProcessPkt(p, Select(id));
            Check(p.ActivePetID == id, "Wrong active pet");
            Check(robin.IsBattle == (id == 12178) && xao.IsBattle == (id == 14156), "Battle flags are not exclusive");
            Check(p.PlayerPets.Count == 2 && p.PlayerPets[1] == robin && p.PlayerPets[2] == xao, "Roster changed on Battle");
            Check(robin.Slot == 1 && xao.Slot == 2 && robin.Level == 5 && xao.Level == 1, "Identity or stats changed");
            Check(p.Packets.Any(v => Convert.ToByte(v.Values[0]) == 19 && Convert.ToByte(v.Values[1]) == 1 && Convert.ToUInt32(v.Values[2]) == id), "Missing correct Battle ACK");
            Check(!p.Packets.Concat(p.CurMap.Packets).Any(v => Convert.ToByte(v.Values[0]) == 15 && (Convert.ToByte(v.Values[1]) == 1 || Convert.ToByte(v.Values[1]) == 2)), "Battle emitted recruit/release");
        }
        int saves = p.Saves;
        handler.ProcessPkt(p, Select(99999));
        Check(p.Saves == saves && p.ActivePetID == 12178, "Unknown pet selected a fallback");
        handler.ProcessPkt(p, Select(1));
        Check(p.ActivePetID == 14156 && xao.IsBattle && !robin.IsBattle, "Client slot was treated as DB slot");
        handler.ProcessPkt(p, Select(2));
        Check(p.ActivePetID == 12178 && robin.IsBattle && !xao.IsBattle, "Second client slot selected wrong identity");
        handler.ProcessPkt(p, Select(3));
        Check(p.ActivePetID == 12178, "Empty slot selected fallback");
        p.Packets.Clear(); p.CurMap.Packets.Clear();
        handler.ProcessPkt(p, new Network.RecievePacket(new byte[] { 0xF4, 0x44, 2, 0, 19, 2 }));
        Check(p.ActivePetID == 0 && !robin.IsBattle && !xao.IsBattle, "Rest left an active pet");
        Check(p.PlayerPets.Count == 2 && p.PlayerPets[1] == robin && p.PlayerPets[2] == xao, "Rest changed roster");
        Check(p.Packets.Any(v => Convert.ToByte(v.Values[0]) == 19 && Convert.ToByte(v.Values[1]) == 2 && v.Values.Length == 2), "Owner Rest ACK malformed");
        Check(p.CurMap.Packets.Any(v => Convert.ToByte(v.Values[0]) == 19 && Convert.ToByte(v.Values[1]) == 7 && Convert.ToUInt32(v.Values[2]) == p.CharID), "Peer Rest ACK malformed");
        Check(!p.Packets.Concat(p.CurMap.Packets).Any(v => Convert.ToByte(v.Values[0]) == 15 && Convert.ToByte(v.Values[1]) == 2), "Rest emitted release");
        return "PASS: five Battle selections (including repeated selection), invalid ID, Rest; roster and identity preserved; no recruit/release packets.";
    }
}
"@
Add-Type -TypeDefinition ($handler + [Environment]::NewLine + $harness)
[PetBattleRosterChecks]::Run()

# Cover the actual appearance method too; its transport is not part of the double above.
$playerSource = Get-Content -LiteralPath (Join-Path $repo 'wlo.pserver.core\Game\Player.cs') -Raw
$start = $playerSource.IndexOf('public void BroadcastPetAppearance(')
$end = $playerSource.IndexOf('public bool AddPetToPartyList(', $start)
$appearance = $playerSource.Substring($start, $end - $start)
if ($appearance -match '(?m)^\s*Send\(petPkt\);') { throw 'Appearance still sends recruit to owner.' }
if ($appearance -notmatch 'CurMap\?\.Broadcast\(mapPkt, "Ex", this.CharID\)') { throw 'Peer appearance record was removed.' }
if ($appearance -match 'Pack8\(19\)|Send\(mapPkt\)') { throw 'Appearance sends owner-only selection incorrectly.' }
if ($appearance -notmatch 'SendPetProgression\(this, pet\)') { throw 'Pet progression refresh was removed.' }
'PASS: appearance source keeps peer packet and progression, without owner re-recruit.'
