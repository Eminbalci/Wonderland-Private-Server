using System;
using Game;
using Game.Code;
using Network;

namespace Network.ActionCodes
{
    public class AC30 : AC
    {
        public override int ID { get { return 30; } }

        public override void ProcessPkt(Player p, RecievePacket r)
        {
            byte sub = r.Unpack8();
            switch (sub)
            {
                case 1: Recv1(p, r); break; // Withdraw or request
                case 2: Recv2(p, r); break; // Deposit item from Bag into Storage
                case 3: Recv3(p, r); break; // Withdraw item from Storage into Bag
                case 4: Recv4(p, r); break; // Move item within Storage
                default:
                    DebugSystem.Write($"[AC30] Unknown subcode {sub} for {p.CharName}");
                    break;
            }
        }

        // Sub 1: Withdraw or Slot click
        void Recv1(Player p, RecievePacket r)
        {
            try
            {
                byte storSlot = r.Unpack8();
                byte ammt = 1;
                try { ammt = r.Unpack8(); } catch { ammt = 1; }
                Withdraw(p, storSlot, ammt);
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[AC30.Recv1] Error: {ex.Message}");
            }
        }

        // Sub 2: Deposit: Bag Slot -> Storage
        void Recv2(Player p, RecievePacket r)
        {
            try
            {
                byte bagSlot = r.Unpack8();
                byte ammt = 1;
                try { ammt = r.Unpack8(); } catch { ammt = 1; }
                if (ammt == 0) ammt = 1;

                if (bagSlot < 1 || bagSlot > 50) return;

                var bagItem = p.Inv[bagSlot];
                if (bagItem == null || bagItem.ItemID == 0) return;

                byte transferAmmt = Math.Min(bagItem.Ammt, ammt);

                // Find first free slot in storage
                byte targetSlot = 0;
                for (byte s = 1; s <= 50; s++)
                {
                    if (p.Storage[s].ItemID == 0)
                    {
                        targetSlot = s;
                        break;
                    }
                }

                if (targetSlot == 0)
                {
                    p.SendSystemMessage("⚠️ Props Keeper storage is full!");
                    return;
                }

                // Copy item to storage slot
                p.Storage[targetSlot].CopyFrom(bagItem);
                p.Storage[targetSlot].Ammt = transferAmmt;

                // Remove from bag
                p.Inv.RemoveItem(bagSlot, transferAmmt, true);

                // Refresh client bag UI so right pane immediately updates
                p.Send(new SendPacket(p.Inv.GetAC23_5()));

                // Send Storage Update to Client (AC 30:2 with 24 trailing bytes)
                SendPacket sp = new SendPacket();
                sp.Pack8(30);
                sp.Pack8(2);
                sp.Pack8(targetSlot);
                sp.Pack16(p.Storage[targetSlot].ItemID);
                sp.Pack8(p.Storage[targetSlot].Ammt);
                sp.Pack8(p.Storage[targetSlot].Damage);
                sp.PackArray(new byte[24]);
                p.Send(sp);

                // Send AC 30:2 short
                SendPacket spShort = new SendPacket();
                spShort.Pack8(30);
                spShort.Pack8(2);
                spShort.Pack8(targetSlot);
                spShort.Pack16(p.Storage[targetSlot].ItemID);
                spShort.Pack8(p.Storage[targetSlot].Ammt);
                spShort.Pack8(p.Storage[targetSlot].Damage);
                p.Send(spShort);

                // Send AC 30:1
                SendPacket sp1 = new SendPacket();
                sp1.Pack8(30);
                sp1.Pack8(1);
                sp1.Pack8(targetSlot);
                sp1.Pack16(p.Storage[targetSlot].ItemID);
                sp1.Pack8(p.Storage[targetSlot].Ammt);
                sp1.Pack8(p.Storage[targetSlot].Damage);
                sp1.PackArray(new byte[24]);
                p.Send(sp1);

                // Send AC 29:1
                SendPacket sp29 = new SendPacket();
                sp29.Pack8(29);
                sp29.Pack8(1);
                sp29.Pack8(targetSlot);
                sp29.Pack16(p.Storage[targetSlot].ItemID);
                sp29.Pack8(p.Storage[targetSlot].Ammt);
                sp29.Pack8(p.Storage[targetSlot].Damage);
                sp29.PackArray(new byte[24]);
                p.Send(sp29);

                // Sync all stored items
                p.Send(new SendPacket(p.Storage.GetAC30_5(30, 5)));
                p.Send(new SendPacket(p.Storage.GetAC30_5(30, 1)));
                p.Send(new SendPacket(p.Storage.GetAC30_5(29, 1)));
                p.Send(new SendPacket(p.Storage.GetAC30_5(29, 5)));

                // Persist storage and bag changes immediately
                DataBase.CharacterDataBase.GlobalInstance?.WritePlayer(p.CharID, p);

                p.SendSystemMessage($"📥 Stored '{p.Storage[targetSlot].Name}' x{transferAmmt} into Storage Slot {targetSlot}.");
                DebugSystem.Write($"[AC30.Recv2] {p.CharName} deposited Item #{p.Storage[targetSlot].ItemID} (x{transferAmmt}) from Bag Slot {bagSlot} to Storage Slot {targetSlot}");
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[AC30.Recv2] Error: {ex.Message}");
            }
        }

        // Sub 3: Withdraw: Storage Slot -> Bag
        void Recv3(Player p, RecievePacket r)
        {
            try
            {
                byte storSlot = r.Unpack8();
                byte ammt = 1;
                try { ammt = r.Unpack8(); } catch { ammt = 1; }
                Withdraw(p, storSlot, ammt);
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[AC30.Recv3] Error: {ex.Message}");
            }
        }

        void Withdraw(Player p, byte storSlot, byte ammt = 1)
        {
            if (storSlot < 1 || storSlot > 50) return;

            var storItem = p.Storage[storSlot];
            if (storItem == null || storItem.ItemID == 0) return;

            byte transferAmmt = Math.Min(storItem.Ammt, ammt == 0 ? (byte)1 : ammt);

            // Add to player inventory (bag)
            p.Inv.AddItem(storItem, 0, true);

            // Remove from storage
            p.Storage.RemoveItem(storSlot, transferAmmt, false);

            // Refresh client bag UI
            p.Send(new SendPacket(p.Inv.GetAC23_5()));

            // Clear / update storage slot on client
            SendPacket sp = new SendPacket();
            sp.Pack8(30);
            sp.Pack8(3);
            sp.Pack8(storSlot);
            p.Send(sp);

            SendPacket sp29 = new SendPacket();
            sp29.Pack8(29);
            sp29.Pack8(2);
            sp29.Pack8(storSlot);
            p.Send(sp29);

            SendPacket sp1 = new SendPacket();
            sp1.Pack8(30);
            sp1.Pack8(1);
            sp1.Pack8(storSlot);
            sp1.Pack16(0);
            sp1.Pack8(0);
            sp1.Pack8(0);
            sp1.PackArray(new byte[24]);
            p.Send(sp1);

            // Sync all stored items
            p.Send(new SendPacket(p.Storage.GetAC30_5(30, 5)));
            p.Send(new SendPacket(p.Storage.GetAC30_5(30, 1)));
            p.Send(new SendPacket(p.Storage.GetAC30_5(29, 1)));

            // Persist storage and bag changes immediately
            DataBase.CharacterDataBase.GlobalInstance?.WritePlayer(p.CharID, p);

            p.SendSystemMessage($"📤 Withdrew '{storItem.Name}' x{transferAmmt} from Storage Slot {storSlot}.");
            DebugSystem.Write($"[AC30.Withdraw] {p.CharName} withdrew Item #{storItem.ItemID} (x{transferAmmt}) from Storage Slot {storSlot} to Bag");
        }

        // Sub 4: Move within storage
        void Recv4(Player p, RecievePacket r)
        {
            try
            {
                byte srcSlot = r.Unpack8();
                byte dstSlot = r.Unpack8();
                byte ammt = 1;
                try { ammt = r.Unpack8(); } catch { ammt = 1; }

                if (srcSlot < 1 || srcSlot > 50 || dstSlot < 1 || dstSlot > 50) return;

                p.Storage.MoveItem(srcSlot, dstSlot, ammt);

                // Sync all stored items
                p.Send(new SendPacket(p.Storage.GetAC30_5(30, 5)));
                p.Send(new SendPacket(p.Storage.GetAC30_5(30, 1)));
                p.Send(new SendPacket(p.Storage.GetAC30_5(29, 1)));

                // Persist storage changes immediately
                DataBase.CharacterDataBase.GlobalInstance?.WritePlayer(p.CharID, p);

                DebugSystem.Write($"[AC30.Recv4] {p.CharName} moved item in storage from {srcSlot} to {dstSlot}");
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[AC30.Recv4] Error: {ex.Message}");
            }
        }
    }
}
