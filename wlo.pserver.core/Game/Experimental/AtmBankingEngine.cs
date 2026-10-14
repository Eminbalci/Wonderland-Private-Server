using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Game.Code;
using Network;

namespace Game.Experimental
{
    public class BankAccount
    {
        public uint CharId { get; set; }
        public string OwnerName { get; set; }
        public uint BalanceGold { get; set; }
        public DateTime LastTransactionTime { get; set; }
        public int TotalTransactions { get; set; }

        public BankAccount(uint charId, string name, uint initialDeposit = 0)
        {
            CharId = charId;
            OwnerName = name ?? "Account Holder";
            BalanceGold = initialDeposit;
            LastTransactionTime = DateTime.UtcNow;
            TotalTransactions = initialDeposit > 0 ? 1 : 0;
        }
    }

    /// <summary>
    /// ATM Banking, Personal Currency Vault and Inter-Player Wire Remittance Engine (AC 26 / Spec 46).
    /// Provides secure gold storage exceeding bag gold caps, transfer wire protocols, and municipal tariffs.
    /// </summary>
    public class AtmBankingEngine
    {
        private readonly ConcurrentDictionary<uint, BankAccount> m_accounts = new ConcurrentDictionary<uint, BankAccount>();
        private readonly object m_vaultLock = new object();
        public const uint MaxVaultCapacity = 2000000000; // 2 Billion Gold
        public const double WireTariffRate = 0.01; // 1% Municipal Wire Fee

        public int ActiveAccountCount => m_accounts.Count;
        public uint TotalVaultedGold => (uint)m_accounts.Values.Sum(a => (long)a.BalanceGold);

        public IReadOnlyCollection<BankAccount> GetAccounts() => m_accounts.Values.ToList().AsReadOnly();

        public BankAccount GetOrCreateAccount(Player player)
        {
            if (player == null) return null;
            return m_accounts.GetOrAdd(player.CharID, id => new BankAccount(id, player.CharName));
        }

        public bool Deposit(Player player, uint amount)
        {
            if (player == null || amount == 0) return false;

            try
            {
                lock (m_vaultLock)
                {
                    if (player.Gold < amount)
                    {
                        player.SendSystemMessage("Insufficient cash on hand to deposit.");
                        return false;
                    }

                    var acc = GetOrCreateAccount(player);
                    if ((long)acc.BalanceGold + amount > MaxVaultCapacity)
                    {
                        player.SendSystemMessage("Deposit exceeds maximum vault capacity (2,000,000,000G).");
                        return false;
                    }

                    player.Gold -= amount;
                    acc.BalanceGold += amount;
                    acc.LastTransactionTime = DateTime.UtcNow;
                    acc.TotalTransactions++;

                    DebugSystem.Write($"[AtmBankingEngine] {player.CharName} deposited {amount:N0}G into Vault. New Balance: {acc.BalanceGold:N0}G");

                    // S->C AC 26:2 Deposit ACK
                    SendPacket p = new SendPacket();
                    p.Pack8(26);
                    p.Pack8(2);
                    p.Pack32(acc.BalanceGold);
                    p.Pack32(player.Gold);
                    player.Send(p);

                    return true;
                }
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[AtmBankingEngine] Deposit error: {ex.Message}");
                return false;
            }
        }

        public bool Withdraw(Player player, uint amount)
        {
            if (player == null || amount == 0) return false;

            try
            {
                lock (m_vaultLock)
                {
                    var acc = GetOrCreateAccount(player);
                    if (acc.BalanceGold < amount)
                    {
                        player.SendSystemMessage("Insufficient vault balance.");
                        return false;
                    }

                    acc.BalanceGold -= amount;
                    player.Gold += amount;
                    acc.LastTransactionTime = DateTime.UtcNow;
                    acc.TotalTransactions++;

                    DebugSystem.Write($"[AtmBankingEngine] {player.CharName} withdrew {amount:N0}G from Vault. New Balance: {acc.BalanceGold:N0}G");

                    // S->C AC 26:3 Withdraw ACK
                    SendPacket p = new SendPacket();
                    p.Pack8(26);
                    p.Pack8(3);
                    p.Pack32(acc.BalanceGold);
                    p.Pack32(player.Gold);
                    player.Send(p);

                    return true;
                }
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[AtmBankingEngine] Withdraw error: {ex.Message}");
                return false;
            }
        }

        public bool Transfer(Player sender, uint recipientCharId, uint amount)
        {
            if (sender == null || amount == 0 || sender.CharID == recipientCharId) return false;

            try
            {
                lock (m_vaultLock)
                {
                    var senderAcc = GetOrCreateAccount(sender);
                    uint tariff = Math.Max(1, (uint)(amount * WireTariffRate));
                    uint totalDeduction = amount + tariff;

                    if (senderAcc.BalanceGold < totalDeduction)
                    {
                        sender.SendSystemMessage($"Insufficient funds. Wire requires {amount:N0}G + {tariff:N0}G wire fee.");
                        return false;
                    }

                    if (!m_accounts.TryGetValue(recipientCharId, out var recipientAcc))
                    {
                        recipientAcc = new BankAccount(recipientCharId, $"Citizen #{recipientCharId}");
                        m_accounts[recipientCharId] = recipientAcc;
                    }

                    senderAcc.BalanceGold -= totalDeduction;
                    recipientAcc.BalanceGold += amount;
                    senderAcc.LastTransactionTime = DateTime.UtcNow;
                    senderAcc.TotalTransactions++;
                    recipientAcc.TotalTransactions++;

                    DebugSystem.Write($"[AtmBankingEngine] Wire Transfer: {sender.CharName} sent {amount:N0}G to {recipientAcc.OwnerName} (Fee: {tariff:N0}G)");

                    // S->C AC 26:4 Wire Transfer ACK
                    SendPacket p = new SendPacket();
                    p.Pack8(26);
                    p.Pack8(4);
                    p.Pack32(recipientCharId);
                    p.Pack32(amount);
                    p.Pack32(senderAcc.BalanceGold);
                    sender.Send(p);

                    return true;
                }
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[AtmBankingEngine] Transfer error: {ex.Message}");
                return false;
            }
        }
    }
}
