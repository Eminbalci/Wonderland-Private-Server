using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Game.Code;
using Network;

namespace Game.Experimental
{
    public enum StatusConditionType : byte
    {
        Freeze = 1,
        Petrify = 2,
        Sleep = 3,
        Confusion = 4,
        Seal = 5,
        Poison = 6,
        HotWheel = 7,
        EarthWall = 8,
        GoddessBlessing = 9
    }

    public class ActiveStatusEffect
    {
        public uint TargetId { get; set; }
        public string TargetName { get; set; }
        public StatusConditionType Type { get; set; }
        public byte RemainingTurns { get; set; }
        public int Potency { get; set; }
        public DateTime AppliedAt { get; set; }

        public ActiveStatusEffect(uint targetId, string targetName, StatusConditionType type, byte turns, int potency = 0)
        {
            TargetId = targetId;
            TargetName = targetName ?? "Combatant";
            Type = type;
            RemainingTurns = turns;
            Potency = potency;
            AppliedAt = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Tactical Turn-Based Combat Status Condition, Crowd Control, and Buff Decay Engine (AC 114/115 / Spec 92).
    /// Tracks elemental afflictions (Freeze, Petrify, Sleep), damage-over-time (Poison), and protective ward buffs.
    /// </summary>
    public class CombatStatusConditionEngine
    {
        private readonly ConcurrentDictionary<string, ActiveStatusEffect> m_effects = new ConcurrentDictionary<string, ActiveStatusEffect>();
        private readonly object m_statusLock = new object();

        public int ActiveConditionCount => m_effects.Count;

        public IReadOnlyCollection<ActiveStatusEffect> GetActiveEffects() => m_effects.Values.ToList().AsReadOnly();

        private string MakeKey(uint targetId, StatusConditionType type) => $"{targetId}_{type}";

        public bool AfflictStatus(Player target, StatusConditionType type, byte turns = 3, int potency = 0)
        {
            if (target == null || turns == 0) return false;

            try
            {
                lock (m_statusLock)
                {
                    string key = MakeKey(target.CharID, type);
                    var effect = new ActiveStatusEffect(target.CharID, target.CharName, type, turns, potency);
                    m_effects[key] = effect;

                    DebugSystem.Write($"[CombatStatusConditionEngine] Afflicted {target.CharName} with {type} for {turns} turns (Potency: {potency})");

                    // S->C AC 114:1 Afflict Status Packet
                    SendPacket p = new SendPacket();
                    p.Pack8(114);
                    p.Pack8(1);
                    p.Pack32(target.CharID);
                    p.Pack8((byte)type);
                    p.Pack8(turns);
                    p.Pack16((ushort)potency);
                    target.Send(p);

                    // Broadcast status effect overlay to map peers
                    target.CurMap?.Broadcast(p);

                    return true;
                }
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[CombatStatusConditionEngine] AfflictStatus error: {ex.Message}");
                return false;
            }
        }

        public bool CanAct(uint targetId)
        {
            // Freeze, Petrify, Sleep, and Seal prevent action execution
            StatusConditionType[] incapacitating = { StatusConditionType.Freeze, StatusConditionType.Petrify, StatusConditionType.Sleep, StatusConditionType.Seal };
            foreach (var cond in incapacitating)
            {
                if (m_effects.ContainsKey(MakeKey(targetId, cond)))
                    return false;
            }
            return true;
        }

        public List<ActiveStatusEffect> ProcessTurnDecay(Player target)
        {
            if (target == null) return new List<ActiveStatusEffect>();

            var expired = new List<ActiveStatusEffect>();
            try
            {
                lock (m_statusLock)
                {
                    var playerKeys = m_effects.Keys.Where(k => k.StartsWith($"{target.CharID}_")).ToList();
                    foreach (var key in playerKeys)
                    {
                        if (m_effects.TryGetValue(key, out var eff))
                        {
                            eff.RemainingTurns--;
                            if (eff.Type == StatusConditionType.Poison)
                            {
                                int poisonDmg = Math.Max(20, target.CurHP / 10);
                                target.CurHP = Math.Max(1, target.CurHP - poisonDmg);
                                DebugSystem.Write($"[CombatStatusConditionEngine] {target.CharName} suffered {poisonDmg} Poison damage! (HP: {target.CurHP})");
                            }

                            if (eff.RemainingTurns <= 0)
                            {
                                m_effects.TryRemove(key, out _);
                                expired.Add(eff);
                                DebugSystem.Write($"[CombatStatusConditionEngine] Condition {eff.Type} expired on {target.CharName}");
                            }
                        }
                    }

                    // S->C AC 114:2 Turn Decay Synchronization
                    SendPacket p = new SendPacket();
                    p.Pack8(114);
                    p.Pack8(2);
                    p.Pack32(target.CharID);
                    p.Pack8((byte)expired.Count);
                    foreach (var exp in expired)
                    {
                        p.Pack8((byte)exp.Type);
                    }
                    target.Send(p);
                    target.CurMap?.Broadcast(p);
                }
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[CombatStatusConditionEngine] ProcessTurnDecay error: {ex.Message}");
            }
            return expired;
        }

        public bool Dispel(Player target, StatusConditionType type)
        {
            if (target == null) return false;

            try
            {
                lock (m_statusLock)
                {
                    string key = MakeKey(target.CharID, type);
                    if (m_effects.TryRemove(key, out _))
                    {
                        DebugSystem.Write($"[CombatStatusConditionEngine] Dispelled {type} from {target.CharName}");

                        // S->C AC 114:3 Dispel frame
                        SendPacket p = new SendPacket();
                        p.Pack8(114);
                        p.Pack8(3);
                        p.Pack32(target.CharID);
                        p.Pack8((byte)type);
                        target.Send(p);
                        target.CurMap?.Broadcast(p);

                        return true;
                    }
                    return false;
                }
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[CombatStatusConditionEngine] Dispel error: {ex.Message}");
                return false;
            }
        }
    }
}
