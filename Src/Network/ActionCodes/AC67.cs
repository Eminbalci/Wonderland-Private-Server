using System;
using Game;
using Game.PetRelated;
using Network;

namespace Network.ActionCodes
{
    /// <summary>
    /// AC 67: Companion & Pet Feeding / Amity Recovery Protocol.
    /// Handles feeding companions and pets with food items (Rice Balls, Meats, Fruits) to increase Amity/Loyalty.
    /// </summary>
    public class AC67 : AC
    {
        public override int ID => 67;

        public override void ProcessPkt(Player c, RecievePacket p)
        {
            if (c == null || p == null) return;

            byte subCode = p.B ?? 1;
            ushort foodItemId = 0;
            if (p.Buffer.Length >= 8)
            {
                foodItemId = p.Unpack16();
            }

            DebugSystem.Write($"[AC67] Pet feed request from {c.CharName}: SubCode={subCode}, FoodID={foodItemId}");

            try
            {
                bool success = PetAmityManager.FeedPet(c, foodItemId);

                SendPacket resp = new SendPacket();
                resp.Pack8((byte)ID);
                resp.Pack8(subCode);
                resp.Pack16(foodItemId);
                resp.Pack8(success ? (byte)1 : (byte)0);
                c.Send(resp);
            }
            catch (Exception ex)
            {
                DebugSystem.Write(new ExceptionData(ex));
            }
        }
    }
}
