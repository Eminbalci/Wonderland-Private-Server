using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Game;
using Game.Code;
using Game.Maps;
using Network;
using RCLibrary.Core.Networking;

namespace Network.ActionCodes
{
    public class AC11 : AC
    {
        public override int ID { get { return 11; } }

        public override void ProcessPkt(Player p, RecievePacket r)
        {
            DebugSystem.Write(DebugItemType.Error, $"[DEBUG] AC11 PK Mode. Sub: {r.B}");
            switch (r.B)
            {
                case 1: Recv1(ref p, r); break; // Battle escape/run away
                case 2: Recv2(ref p, r); break; // PK initiated
                case 4: Recv4(ref p, r); break; // Join battle
                case 5: Recv5(ref p, r); break; // Watch battle
                default:
                    DebugSystem.Write(DebugItemType.Error, $"[DEBUG] AC11 Unknown SubAction: {r.B}");
                    break;
            }
        }

        void Recv1(ref Player p, RecievePacket r)
        {
            // Battle escape/run away
            try
            {
                byte escapeType = r.Unpack8();
                DebugSystem.Write(DebugItemType.Error, $"[DEBUG] AC11 Recv1 Escape. Type: {escapeType}");

                switch (escapeType)
                {
                    case 3: // Run away from battle
                        if (p.MyBattle != null)
                        {
                            // TODO: Implement battle escape logic
                            DebugSystem.Write(DebugItemType.Error, $"[DEBUG] {p.CharName} attempting to flee battle");
                        }
                        break;
                }
            }
            catch (Exception ex)
            {
                DebugSystem.Write(DebugItemType.Error, $"[DEBUG] AC11 Recv1 Exception: {ex.Message}");
            }
        }

        void Recv2(ref Player p, RecievePacket r)
        {
            // PK initiated
            try
            {
                byte pkType = r.Unpack8();
                uint rawTargetID = r.Unpack32();
                uint targetID = rawTargetID >> 8; // Decode composite ID
                ushort clickID = r.Unpack16();

                DebugSystem.Write(DebugItemType.Error, $"[DEBUG] AC11 Recv2 PK. Type: {pkType}, TargetID: {targetID}, ClickID: {clickID}");

                switch (pkType)
                {
                    case 2: // PK against other player
                        HandlePlayerPK(p, targetID);
                        break;
                    case 3: // PK against NPC (monster battle)
                        HandleNpcPK(p, targetID, clickID);
                        break;
                    case 4: // Join existing battle
                        DebugSystem.Write(DebugItemType.Error, $"[DEBUG] AC11 Join battle request from {p.CharName}");
                        break;
                    case 5: // Watch existing battle
                        DebugSystem.Write(DebugItemType.Error, $"[DEBUG] AC11 Watch battle request from {p.CharName}");
                        break;
                }
            }
            catch (Exception ex)
            {
                DebugSystem.Write(DebugItemType.Error, $"[DEBUG] AC11 Recv2 Exception: {ex.Message}");
            }
        }

        void HandlePlayerPK(Player attacker, uint targetID)
        {
            GameMap map = attacker.CurMap as GameMap;
            if (map == null)
            {
                DebugSystem.Write(DebugItemType.Error, $"[DEBUG] AC11 PK failed: Attacker not in a map");
                return;
            }

            Player target = map.PlayersList.FirstOrDefault(x => x.CharID == targetID);
            if (target == null)
            {
                DebugSystem.Write(DebugItemType.Error, $"[DEBUG] AC11 PK failed: Target {targetID} not found");
                return;
            }

            // Check if both players have PK enabled
            if (!attacker.Settings.PKABLE)
            {
                DebugSystem.Write(DebugItemType.Error, $"[DEBUG] AC11 PK failed: {attacker.CharName} has PK disabled");
                SendPKError(attacker, "PK modunuz kapalı!");
                return;
            }

            if (!target.Settings.PKABLE)
            {
                DebugSystem.Write(DebugItemType.Error, $"[DEBUG] AC11 PK failed: {target.CharName} has PK disabled");
                SendPKError(attacker, $"{target.CharName} PK modunu kapatmış!");
                return;
            }

            DebugSystem.Write(DebugItemType.Error, $"[DEBUG] AC11 PK initiated: {attacker.CharName} vs {target.CharName}");

            // Battle system integration is complex - requires the old Map/Battle system
            // The old system in Maps/Map.cs has onPk_Started but uses different Player type
            // TODO: Integrate battle system with new Player/Map architecture
            DebugSystem.Write(DebugItemType.Error, $"[DEBUG] AC11 PK: Battle system not yet integrated with new architecture");
        }

        void HandleNpcPK(Player attacker, uint npcID, ushort clickID)
        {
            DebugSystem.Write(DebugItemType.Error, $"[DEBUG] AC11 NPC Battle: {attacker.CharName} attacking NPC {npcID}");

            // TODO: Implement NPC battle logic
            // GameMap map = attacker.CurMap as GameMap;
            // if (map != null)
            //     map.onNpcPk(attacker, npc);
        }

        void SendPKError(Player p, string message)
        {
            // Send error message to player
            SendPacket pkt = new SendPacket();
            pkt.PackArray(new byte[] { 2, 3 });
            pkt.Pack32(100); // Error code
            pkt.PackString(message);
            p.Send(pkt);
        }

        void Recv4(ref Player p, RecievePacket r)
        {
            // Join battle
            try
            {
                uint rawTargetID = r.Unpack32();
                uint targetID = rawTargetID >> 8;
                DebugSystem.Write(DebugItemType.Error, $"[DEBUG] AC11 Recv4 Join Battle. Target: {targetID}");
                // TODO: Implement battle join logic
            }
            catch (Exception ex)
            {
                DebugSystem.Write(DebugItemType.Error, $"[DEBUG] AC11 Recv4 Exception: {ex.Message}");
            }
        }

        void Recv5(ref Player p, RecievePacket r)
        {
            // Watch battle
            try
            {
                uint rawTargetID = r.Unpack32();
                uint targetID = rawTargetID >> 8;
                DebugSystem.Write(DebugItemType.Error, $"[DEBUG] AC11 Recv5 Watch Battle. Target: {targetID}");
                // TODO: Implement battle watch logic
            }
            catch (Exception ex)
            {
                DebugSystem.Write(DebugItemType.Error, $"[DEBUG] AC11 Recv5 Exception: {ex.Message}");
            }
        }
    }
}
