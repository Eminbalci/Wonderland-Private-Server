using System;
using System.Collections.Concurrent;
using System.Linq;
using Game.Code;
using Network;

namespace Game.Experimental
{
    public class PinVaultRecord
    {
        public uint CharId { get; set; }
        public string CharName { get; set; }
        public string HashedPin { get; set; }
        public bool IsUnlocked { get; set; }
        public int FailedAttempts { get; set; }
        public DateTime LockoutUntil { get; set; }
        public bool IsLockedOut => DateTime.UtcNow < LockoutUntil;

        public PinVaultRecord(uint id, string name, string pin = "123456")
        {
            CharId = id;
            CharName = name;
            HashedPin = pin;
            IsUnlocked = false;
            FailedAttempts = 0;
            LockoutUntil = DateTime.MinValue;
        }
    }

    /// <summary>
    /// Secondary PIN Vault & Anti-Theft Security Engine (AC 24 / Spec 54 & 73).
    /// Manages 6-digit secondary PIN codes for trade, drop, and character disposal security.
    /// </summary>
    public class SecondaryPinVaultEngine
    {
        private readonly ConcurrentDictionary<uint, PinVaultRecord> m_vaults = new ConcurrentDictionary<uint, PinVaultRecord>();

        public PinVaultRecord GetOrAddVault(Player player)
        {
            if (player == null) return new PinVaultRecord(0, "Guest");
            return m_vaults.GetOrAdd(player.CharID, id => new PinVaultRecord(id, player.CharName));
        }

        public bool SetPin(Player player, string newPin)
        {
            if (player == null || string.IsNullOrWhiteSpace(newPin) || newPin.Length < 4 || newPin.Length > 8)
            {
                return false;
            }

            var vault = GetOrAddVault(player);
            lock (vault)
            {
                vault.HashedPin = newPin;
                vault.FailedAttempts = 0;
                vault.LockoutUntil = DateTime.MinValue;
                vault.IsUnlocked = true;

                DebugSystem.Write($"[SecondaryPinVaultEngine] Player {player.CharName} updated secondary security PIN.");

                // S->C AC 24:1 PIN set ACK
                SendPacket p = new SendPacket();
                p.Pack8(24);
                p.Pack8(1);
                p.Pack8(1); // Success
                player.Send(p);

                return true;
            }
        }

        public bool VerifyPin(Player player, string pin)
        {
            if (player == null) return false;

            var vault = GetOrAddVault(player);
            lock (vault)
            {
                if (vault.IsLockedOut)
                {
                    double remainMins = (vault.LockoutUntil - DateTime.UtcNow).TotalMinutes;
                    player.SendSystemMessage($"PIN Vault is locked due to multiple failed attempts! Try again in {remainMins:F1} minutes.");
                    return false;
                }

                if (vault.HashedPin == pin)
                {
                    vault.IsUnlocked = true;
                    vault.FailedAttempts = 0;

                    DebugSystem.Write($"[SecondaryPinVaultEngine] Player {player.CharName} verified secondary PIN successfully.");

                    // S->C AC 24:2 PIN verified OK
                    SendPacket p = new SendPacket();
                    p.Pack8(24);
                    p.Pack8(2);
                    p.Pack8(1); // Verified OK
                    player.Send(p);

                    return true;
                }
                else
                {
                    vault.FailedAttempts++;
                    DebugSystem.Write($"[SecondaryPinVaultEngine] FAILED PIN attempt #{vault.FailedAttempts} for {player.CharName}");

                    if (vault.FailedAttempts >= 3)
                    {
                        vault.LockoutUntil = DateTime.UtcNow.AddMinutes(15);
                        DebugSystem.Write($"[SecondaryPinVaultEngine] LOCKOUT TRIGGERED for {player.CharName} until {vault.LockoutUntil:HH:mm:ss}");
                        player.SendSystemMessage("Security PIN locked for 15 minutes due to 3 failed attempts!");
                    }

                    SendPacket p = new SendPacket();
                    p.Pack8(24);
                    p.Pack8(2);
                    p.Pack8(0); // Failed
                    p.Pack8((byte)vault.FailedAttempts);
                    player.Send(p);

                    return false;
                }
            }
        }

        public void ResetLockout(uint charId)
        {
            if (m_vaults.TryGetValue(charId, out var vault))
            {
                vault.FailedAttempts = 0;
                vault.LockoutUntil = DateTime.MinValue;
                vault.IsUnlocked = true;
                DebugSystem.Write($"[SecondaryPinVaultEngine] Admin reset PIN lockout for CharID {charId}");
            }
        }
    }
}
