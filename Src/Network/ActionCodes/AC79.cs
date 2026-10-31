using System;
using Game;
using Game.PlayerRelated;
using Network;

namespace Network.ActionCodes
{
    /// <summary>
    /// AC 79: Rebirth job selection (verified in aLogin.exe).
    ///   S->C [79,1]          opens the job form (client 0x302d4c -> 0x45e364).
    ///                        STR > INT shows Killer/Warrior/Knight, INT > STR shows
    ///                        Wit/Priest/Seer, equal shows both groups.
    ///   S->C [79,2,1]        "Inventory is full!" (client 0x1ab058).
    ///   C->S [79,1,job]      rebirth confirm (client 0x1aaf38).
    ///   C->S [79,2,job,mirrorSlot,capeSlot]  job change with Transfer Mirror (client 0x1aaf58).
    /// </summary>
    public class AC79 : AC
    {
        public override int ID => 79;

        public static void OpenRebirthForm(Player p)
        {
            if (p == null) return;
            p.RebornFormOpen = true;
            p.Send(Tools.FromFormat("bb", 79, 1));
        }

        public override void ProcessPkt(Player c, RecievePacket p)
        {
            if (c == null || p == null) return;
            try
            {
                p.SetPtr(6);
                int len = p.Buffer.Length - 6;
                DebugSystem.Write($"[AC79] {c.CharName} rebirth packet 79:{p.B} payload=[{BitConverter.ToString(p.Buffer, 6, Math.Max(0, len))}]");
                if (len < 1) return;
                byte jobByte = p.Unpack8();
                if (jobByte < 1 || jobByte > 6) return;
                var job = (Game.PlayerRelated.RebornJob)jobByte;

                if (p.B == 1)
                {
                    // Only accept a rebirth the server offered through the NPC script (or a GM).
                    if (!c.RebornFormOpen && !GmManager.IsGm(c))
                    {
                        DebugSystem.Write($"[AC79] Ignored unsolicited rebirth request from {c.CharName}.");
                        return;
                    }
                    c.RebornFormOpen = false;
                    RebornManager.PerformReborn(c, job);
                }
                else if (p.B == 2 && len >= 3)
                {
                    byte mirrorSlot = p.Unpack8();
                    byte capeSlot = p.Unpack8();
                    RebornManager.ChangeJob(c, job, mirrorSlot, capeSlot);
                }
            }
            catch (Exception ex)
            {
                DebugSystem.Write(new ExceptionData(ex));
            }
        }
    }
}
