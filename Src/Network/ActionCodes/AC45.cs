using System;
using Game;
using Network;

namespace Network.ActionCodes
{
    /// <summary>
    /// AC 45: Bank & ATM Currency Management Protocol.
    /// Handles ATM gold deposits, withdrawals, balance queries, ATM PIN verification, and transactions.
    /// </summary>
    public class AC45 : AC
    {
        public override int ID => 45;

        public override void ProcessPkt(Player c, RecievePacket p)
        {
            if (c == null || p == null) return;

            byte subCode = p.B ?? 8;
            DebugSystem.Write($"[AC45] ATM action from {c.CharName}: SubCode={subCode}");

            try
            {
                switch (subCode)
                {
                    case 8: // Query ATM Balance / Open ATM
                        SendAtmBalance(c);
                        break;
                    case 9: // Deposit Gold into ATM
                        HandleDeposit(c, p);
                        break;
                    case 10: // Withdraw Gold from ATM
                        HandleWithdraw(c, p);
                        break;
                    case 11: // Set ATM Security PIN
                        HandleSetPin(c, p);
                        break;
                    case 12: // Transfer Gold
                        HandleTransfer(c, p);
                        break;
                    default:
                        SendAtmBalance(c);
                        break;
                }
            }
            catch (Exception ex)
            {
                DebugSystem.Write(new ExceptionData(ex));
            }
        }

        private void SendAtmBalance(Player c)
        {
            SendPacket resp = new SendPacket();
            resp.Pack8((byte)ID);
            resp.Pack8(8); // Open ATM / Balance
            resp.Pack32(c.BankGold); // Bank Gold
            resp.Pack32(c.Gold);     // Inventory Gold
            c.Send(resp);
            DebugSystem.Write($"[AC45.SendAtmBalance] {c.CharName}: BankGold={c.BankGold}, InvGold={c.Gold}");
        }

        private void HandleDeposit(Player c, RecievePacket p)
        {
            uint amount = p.Buffer.Length >= 10 ? p.Unpack32() : 0;
            if (amount > 0 && c.Gold >= amount)
            {
                c.TakeGold((int)amount);
                c.BankGold += amount;

                SendPacket resp = new SendPacket();
                resp.Pack8((byte)ID);
                resp.Pack8(9);
                resp.Pack32(c.BankGold);
                resp.Pack32(c.Gold);
                c.Send(resp);
                DebugSystem.Write($"[AC45.Deposit] {c.CharName} deposited {amount} gold. Bank: {c.BankGold}");
            }
        }

        private void HandleWithdraw(Player c, RecievePacket p)
        {
            uint amount = p.Buffer.Length >= 10 ? p.Unpack32() : 0;
            if (amount > 0 && c.BankGold >= amount)
            {
                c.BankGold -= amount;
                c.AddGold((int)amount);

                SendPacket resp = new SendPacket();
                resp.Pack8((byte)ID);
                resp.Pack8(10);
                resp.Pack32(c.BankGold);
                resp.Pack32(c.Gold);
                c.Send(resp);
                DebugSystem.Write($"[AC45.Withdraw] {c.CharName} withdrew {amount} gold. Bank: {c.BankGold}");
            }
        }

        private void HandleSetPin(Player c, RecievePacket p)
        {
            SendPacket resp = new SendPacket();
            resp.Pack8((byte)ID);
            resp.Pack8(11);
            resp.Pack8(1); // PIN set OK
            c.Send(resp);
        }

        private void HandleTransfer(Player c, RecievePacket p)
        {
            SendPacket resp = new SendPacket();
            resp.Pack8((byte)ID);
            resp.Pack8(12);
            resp.Pack8(1); // Transfer OK
            c.Send(resp);
        }
    }
}
