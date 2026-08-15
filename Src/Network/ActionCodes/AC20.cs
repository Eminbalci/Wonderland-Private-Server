using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Network;
using Network.ActionCodes;
using Game;

namespace Wonderland_Private_Server.ActionCodes
{
    public class AC20 : AC
    {
        public override int ID { get { return 20; } }
        public override void ProcessPkt(Player r, RecievePacket p)
        {
            switch (p.Unpack8())
            {
                case 1: Recv1(r, p); break;
                case 6: Recv6(r, p); break;
                case 9: Recv9(r, p); break;
                case 8: Recv8(r, p); break;
            }
        }
        void Recv8(Player p, RecievePacket r)
        {
            ushort portalID = r.Unpack16();
            DebugSystem.Write($"[AC20.Recv8] Player {p.CharName} stepped on portal {portalID} on Map {p.CurMap?.MapID} at pos({p.CurX},{p.CurY})");
            if (p.CurMap == null || !p.CurMap.Teleport(TeleportType.Regular, p, portalID))
            {
                p.Send(Tools.FromFormat("bb", 20, 8));
            }
        }
        void Recv1(Player p, RecievePacket r)
        {
            if (p.CurMap == null)
            {
                p.Send(Tools.FromFormat("bb", 20, 8));
                return;
            }

            // NPC click - get click ID from packet (handle 3-byte padding if present)
            ushort clickID = 0;
            if (r.Buffer.Count() - r.GetPtr() >= 4)
            {
                r.Unpack8(); r.Unpack8(); r.Unpack8();
                clickID = r.Unpack8();
            }
            else
            {
                clickID = r.Unpack8();
            }

            DebugSystem.Write($"[AC20.Recv1] Player {p.CharName} clicked NPC {clickID} on Map {p.CurMap.MapID}");

            // Prioritize NPC interaction (dialogue, quests, battles)
            if (p.CurMap.ProcessInteraction((byte)clickID, p))
            {
                // Interaction handled successfully
                return;
            }

            // Door / Portal trigger fallback ONLY if object name or type is explicitly a door
            if (p.CurMap.mapData != null && p.CurMap.mapData.Npclist != null)
            {
                var clickedNpc = p.CurMap.mapData.Npclist.FirstOrDefault(n => n.clickId == clickID);
                if (clickedNpc != null && (clickedNpc.Name ?? "").ToLower().Contains("door") && clickedNpc.unknownbytearray2 != null && clickedNpc.unknownbytearray2.Count > 0)
                {
                    ushort linkedPortal = clickedNpc.unknownbytearray2[0];
                    DebugSystem.Write($"[AC20.Recv1] Object {clickID} is an explicit door linking to portal {linkedPortal} on Map {p.CurMap.MapID}");
                    if (p.CurMap.Teleport(TeleportType.Regular, p, linkedPortal))
                    {
                        return;
                    }
                }
            }

            // Send default response if interaction fails
            p.Send(Tools.FromFormat("bb", 20, 8));
        }
        void Recv6(Player p, RecievePacket r)
        {
            if (!p.ContinueInteraction())
            {
                p.Send(Tools.FromFormat("bb", 20, 8));

                if (p.Flags.HasFlag(PlayerFlag.Warping))
                {
                    p.Flags.Add(PlayerFlag.InMap);
                    p.Send(Tools.FromFormat("bb", 5, 4));
                }

                //switch (p.State)
                //{
                //    case PlayerState.InGame_Warping:
                //        {
                //            tmp = new SendPacket();
                //            tmp.Pack(new byte[] { 5, 4 });
                //            p.Send(tmp);
                //        } break;
                //    case PlayerState.InGame_Interacting:
                //        {
                //            p.object_interactingwith = null;
                //        } break;
                //}

            }
        }
        void Recv9(Player p, RecievePacket r)
        {

        }
    }
}