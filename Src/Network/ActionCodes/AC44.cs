using System;
using Game;
using Game.PetRelated;
using Network;

namespace Network.ActionCodes
{
    /// <summary>
    /// AC 44: Companion rebirth form (client form_NPCUpgrade, verified in aLogin.exe).
    ///   C->S [44,1,clientPetSlot]; S->C [44,1,result] and [44,2] (open form).
    /// </summary>
    public class AC44 : AC
    {
        public override int ID => 44;

        public override void ProcessPkt(Player c, RecievePacket p)
        {
            if (c == null || p == null) return;
            try
            {
                if (p.B != 1 || p.Buffer.Length < 7) return;
                p.SetPtr(6);
                CompanionRebirthManager.HandleRequest(c, p.Unpack8());
            }
            catch (Exception ex)
            {
                DebugSystem.Write("[AC44] Error: " + ex.Message);
            }
        }
    }
}
