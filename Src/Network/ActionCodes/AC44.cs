using System;
using Game;
using Game.Experimental;
using Network;

namespace Network.ActionCodes
{
    /// <summary>
    /// AC 44: Character Title Badge & Hot Spring Bathing Protocol.
    /// Handles unlocking/equipping titles and hot spring thermal relaxation vitality recovery.
    /// </summary>
    public class AC44 : AC
    {
        public override int ID => 44;

        public override void ProcessPkt(Player c, RecievePacket p)
        {
            if (c == null || p == null) return;

            byte subCode = p.B ?? 1;
            DebugSystem.Write($"[AC44] Title/HotSpring action from {c.CharName}: SubCode={subCode}");

            try
            {
                if (ExperimentalFeaturesManager.Instance.EnableHotSpringBathing)
                {
                    switch (subCode)
                    {
                        case 1: // Enter Hot Spring or Equip Title
                            ushort val = 0;
                            if (p.Buffer != null && p.Buffer.Length >= 8)
                            {
                                val = p.Unpack16();
                            }

                            if (val > 1000) // Title ID
                            {
                                c.Title = val;
                                SendPacket respTitle = new SendPacket();
                                respTitle.Pack8((byte)ID);
                                respTitle.Pack8(subCode);
                                respTitle.Pack16(val);
                                c.Send(respTitle);

                                SendPacket bcast = new SendPacket();
                                bcast.Pack8((byte)ID);
                                bcast.Pack8(1);
                                bcast.Pack32(c.CharID);
                                bcast.Pack16(val);
                                c.CurMap?.Broadcast(bcast);
                            }
                            else
                            {
                                ushort mapId = (ushort)(c.CurMap?.MapID ?? 0);
                                ExperimentalFeaturesManager.Instance.HotSpringBathing.EnterHotSpring(c, mapId);
                            }
                            break;

                        case 2: // Pulse Rest Tick
                            ExperimentalFeaturesManager.Instance.HotSpringBathing.PulseRestTick(c);
                            break;

                        case 3: // Exit Hot Spring
                            ExperimentalFeaturesManager.Instance.HotSpringBathing.ExitHotSpring(c);
                            break;

                        default:
                            SendPacket resp = new SendPacket();
                            resp.Pack8((byte)ID);
                            resp.Pack8(subCode);
                            resp.Pack8(1);
                            c.Send(resp);
                            break;
                    }
                }
                else
                {
                    // Basic Title badge update
                    ushort titleId = 0;
                    if (p.Buffer != null && p.Buffer.Length >= 8)
                    {
                        titleId = p.Unpack16();
                    }
                    c.Title = titleId;

                    SendPacket resp = new SendPacket();
                    resp.Pack8((byte)ID);
                    resp.Pack8(subCode);
                    resp.Pack16(titleId);
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
