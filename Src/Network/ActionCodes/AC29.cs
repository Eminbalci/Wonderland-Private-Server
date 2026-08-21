using System;
using Game;
using Game.Code;
using Network;

namespace Network.ActionCodes
{
    public class AC29 : AC
    {
        public override int ID { get { return 29; } }

        public override void ProcessPkt(Player p, RecievePacket r)
        {
            switch (r.Unpack8())
            {
                case 1: Recv1(p, r); break; // Deposit item into storage
                case 2: Recv2(p, r); break; // Withdraw item from storage
                case 3: Recv3(p, r); break; // Move item within storage
            }
        }

        // Deposit: Bag -> Storage
        void Recv1(Player p, RecievePacket r)
        {
            try
            {
                byte bagSlot = r.Unpack8();
                byte storSlot = r.Unpack8();
                byte ammt = r.Unpack8();

                if (bagSlot < 1 || bagSlot > 50 || storSlot < 1 || storSlot > 50 || ammt == 0) return;

                var bagItem = p.Inv[bagSlot];
                if (bagItem == null || bagItem.ItemID == 0) return;

                byte transferAmmt = Math.Min(bagItem.Ammt, ammt);

                // Add to storage
                p.Storage.AddItem(bagItem, storSlot, false);
                p.Storage[storSlot].Ammt = transferAmmt;

                // Remove from bag
                p.Inv.RemoveItem(bagSlot, transferAmmt, true);

                // Sync bag so right pane updates
                p.Send(new SendPacket(p.Inv.GetAC23_5()));

                // Notify client storage update
                SendPacket sp = new SendPacket();
                sp.Pack8(29);
                sp.Pack8(1);
                sp.Pack8(storSlot);
                sp.Pack16(p.Storage[storSlot].ItemID);
                sp.Pack8(p.Storage[storSlot].Ammt);
                sp.Pack8(p.Storage[storSlot].Damage);
                sp.PackArray(new byte[24]);
                p.Send(sp);

                // Also sync storage
                p.Send(new SendPacket(p.Storage.GetAC30_5(30, 5)));
                p.Send(new SendPacket(p.Storage.GetAC30_5(29, 1)));

                DataBase.CharacterDataBase.GlobalInstance?.WritePlayer(p.CharID, p);

                p.SendSystemMessage($"📥 Stored '{p.Storage[storSlot].Name}' x{transferAmmt} into Storage Slot {storSlot}.");
                DebugSystem.Write($"[AC29.Recv1] {p.CharName} deposited Item #{p.Storage[storSlot].ItemID} (x{transferAmmt}) from Bag Slot {bagSlot} to Storage Slot {storSlot}");
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[AC29.Recv1] Error: {ex.Message}");
            }
        }

        // Withdraw: Storage -> Bag
        void Recv2(Player p, RecievePacket r)
        {
            try
            {
                byte storSlot = r.Unpack8();
                byte bagSlot = r.Unpack8();
                byte ammt = r.Unpack8();

                if (storSlot < 1 || storSlot > 50 || bagSlot < 1 || bagSlot > 50 || ammt == 0) return;

                var storItem = p.Storage[storSlot];
                if (storItem == null || storItem.ItemID == 0) return;

                byte transferAmmt = Math.Min(storItem.Ammt, ammt);

                // Add to bag
                p.Inv.AddItem(storItem, bagSlot, true);

                // Remove from storage
                p.Storage.RemoveItem(storSlot, transferAmmt, false);

                // Sync bag so right pane updates
                p.Send(new SendPacket(p.Inv.GetAC23_5()));

                // Notify client storage update
                SendPacket sp = new SendPacket();
                sp.Pack8(29);
                sp.Pack8(2);
                sp.Pack8(storSlot);
                p.Send(sp);

                // Also sync storage
                p.Send(new SendPacket(p.Storage.GetAC30_5(30, 5)));
                p.Send(new SendPacket(p.Storage.GetAC30_5(29, 1)));

                DataBase.CharacterDataBase.GlobalInstance?.WritePlayer(p.CharID, p);

                p.SendSystemMessage($"📤 Withdrew '{storItem.Name}' x{transferAmmt} from Storage Slot {storSlot}.");
                DebugSystem.Write($"[AC29.Recv2] {p.CharName} withdrew Item #{storItem.ItemID} (x{transferAmmt}) from Storage Slot {storSlot} to Bag Slot {bagSlot}");
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[AC29.Recv2] Error: {ex.Message}");
            }
        }

        // Move within storage
        void Recv3(Player p, RecievePacket r)
        {
            try
            {
                byte srcSlot = r.Unpack8();
                byte dstSlot = r.Unpack8();
                byte ammt = r.Unpack8();

                if (srcSlot < 1 || srcSlot > 50 || dstSlot < 1 || dstSlot > 50) return;

                p.Storage.MoveItem(srcSlot, dstSlot, ammt);

                DataBase.CharacterDataBase.GlobalInstance?.WritePlayer(p.CharID, p);

                DebugSystem.Write($"[AC29.Recv3] {p.CharName} moved item in storage from {srcSlot} to {dstSlot}");
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[AC29.Recv3] Error: {ex.Message}");
            }
        }
    }
}
