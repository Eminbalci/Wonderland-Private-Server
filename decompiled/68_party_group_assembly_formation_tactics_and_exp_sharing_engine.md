# Document 68: Party Group Assembly, Formation Tactics, and EXP Sharing Engine

## 1. Executive Summary & Engine Architecture

Cooperative gameplay in Wonderland Online centers on the party system, rendered through the tabbed interface `Form_Team_2` within the client's social notebook (`FUN_002880c4`, `FUN_002b07e8`, `FUN_002b09e0`, `FUN_0045f764`, and `FUN_00482a3c`). The engine supports up to 4 player characters in a single adventuring company.

The party engine controls three interconnected operational pillars:
1. **Bilateral Teaming Lifecycle:** Orchestrating invitations, join applications, leadership succession, and member dismissals via Action Code `0x15` (`21` decimal).
2. **Follow Formation Kinematics:** Tethering party members in a snake-like trailing formation behind the party leader (`*(actor + 0x207d) = 2`), synchronizing portal warps, vehicle boarding, and map transitions.
3. **Turn-Based Battle Entry & EXP Sharing:** Pulling all active party members into coordinated combat encounters, maintaining companion slots, and distributing monster defeat experience points according to party-size multipliers and level-weighting formulas.

```
+------------------------------------------------------------------------------------+
|                         PARTY GROUP ENGINE ARCHITECTURE                            |
+------------------------------------------------------------------------------------+
                                      |
         +----------------------------+----------------------------+
         | Social UI: Form_Team_2                                  | Overworld Kinematics
         v                                                         v
  [4-Member Roster Interface]                               [Follow Trail Movement]
  - Leader Designation & Badge                              - Leader Actor +0x21BC
  - HP / SP Real-Time Telemetry                             - Follow State: +0x207D == 2
  - Kick, Leave, Delegate Buttons                           - 32-Pixel Inter-Member Spacing
         |                                                         |
         +----------------------------+----------------------------+
                                      |
                                      v
                     [Combat & Experience Distribution]
                  Party Size Bonus: +20% EXP per Extra Member
                                      |
                                      v
                 [Network Dispatcher: Action Code 0x15 (21)]
       Subcodes: 0x01 (Invite/Join), 0x02 (Leave/Disband), 0x03 (Delegate)
```

---

## 2. Memory Layouts & Data Structures

### 2.1 Party Member Telemetry Record (`0x24` = 36 Bytes)
The party leader and client maintain real-time telemetry for each party member in array `PTR_DAT_004c9918 + 0x21E8 + (slot * 4)`:

| Offset | Type | Field Name | Description |
|---|---|---|---|
| `+0x00` | `uint` | `CharacterId` | Unique 32-bit player character identifier |
| `+0x04` | `char[16]` | `Name` | Character nameplate string |
| `+0x14` | `byte` | `Level` | Current character level ($1..199$) |
| `+0x15` | `byte` | `Element` | Elemental affinity (1 = Earth, 2 = Water, 3 = Fire, 4 = Wind) |
| `+0x16` | `byte` | `RebirthClass`| Rebirth job (Killer, Warrior, Knight, Sage, Priest, Prophet) |
| `+0x17` | `byte` | `IsLeader` | `1` = Party Leader, `0` = Member |
| `+0x18` | `uint` | `CurrentHp` | Current hit points |
| `+0x1C` | `uint` | `MaxHp` | Maximum hit points |
| `+0x20` | `uint` | `CurrentSp` | Current spirit points |

### 2.2 Local Actor Party Registers

| Offset | Type | Field Name | Description |
|---|---|---|---|
| `+0x207D`| `byte` | `FormationState`| `0` = Solo, `1` = Party Leader, `2` = Party Follower |
| `+0x21BC`| `uint` | `LeaderGuid` | Character ID of the party leader |
| `+0x21E4`| `byte` | `MemberCount` | Current number of party members ($1..4$) |
| `+0x21E8`| `int*[4]` | `MemberPointers`| References to world entity structures of party members |

---

## 3. UI Form Geometry, Coordinates & Controls

### 3.1 `Form_Team_2` Layout Specifications
`Form_Team_2` is rendered as the primary tab of the social organization notebook ($345 \times 240$ px):
- **4 Member Rows:**
  - Vertically spaced at stride 42 px ($Y = 40 + (i - 1) \times 42$ px for $i = 1..4$).
  - Shows animated character headshot, Level badge, Element icon, and dual HP/SP gauge bars.
- **Action Buttons:**
  - `"Join Team"` / `"Team Invite"`: Triggered dynamically via `FUN_002b07e8` depending on selection state.
  - `"Leave Team"`: Submits packet `Action Code 0x15:0x02`.
  - `"Disband Team"`: Available only to the party leader (`IsLeader == 1`).
  - `"Want to be a team leader?"`: Prompts bilateral leadership delegation.

```
+---------------------------------------------------------------------------------+
| Form_Team_2: Party Management (345 x 240 px)                                    |
+---------------------------------------------------------------------------------+
| [Tab: Party (Active)] [Tab: Army Group] [Tab: Guild Info] [Tab: Bulletin]  [X]  |
+---------------------------------------------------------------------------------+
| Member 1 [Leader]: HeroX   (LV 120 Killer, Fire)  HP: [||||||||||] SP: [||||||] |
| Member 2:          Mage01  (LV 115 Sage, Water)   HP: [||||||||  ] SP: [||||||] |
| Member 3:          KnightA (LV 118 Knight, Earth) HP: [||||||||||] SP: [||||  ] |
| Member 4:          [Empty Slot / Invite Available]                              |
+---------------------------------------------------------------------------------+
| Actions: [Invite / Apply]    [Kick Member]    [Pass Leader]    [Leave Team]     |
+---------------------------------------------------------------------------------+
```

---

## 4. Network Protocol, Wire Frames & Action Codes

### 4.1 Packet Framing
Party actions use Action Code `0x15` (`21` decimal):
- **Opcode:** `0x15`
- **Subcodes:**
  - `0x01`: Send Invitation / Join Application (C2S via `FUN_00482a3c`)
  - `0x02`: Leave Team / Kick Member / Disband (C2S)
  - `0x03`: Delegate Party Leadership (C2S)
  - `0x05`: Party Telemetry Broadcast (S2C HP/SP Synchronization)
  - `0x06`: Team Dissolution Notification (S2C via `"\n disbanded"`)

### 4.2 C# Packet Structure Definitions

```csharp
namespace Wonderland.Network.Packets.Party
{
    /// <summary>
    /// Client-to-server request to invite another player or apply to join their party.
    /// Wire Header: Action Code 0x15, Subcode 0x01.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct C2S_PartyInviteOrJoinPacket
    {
        public byte ActionCode;       // 0x15 (21)
        public byte SubCode;          // 0x01
        public uint TargetCharacterId;// Target player GUID
        public byte Mode;             // 1 = Join Request, 2 = Team Invite
    }

    /// <summary>
    /// Client-to-server request to leave, kick a member, or disband the party.
    /// Wire Header: Action Code 0x15, Subcode 0x02.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct C2S_PartyLeaveOrKickPacket
    {
        public byte ActionCode;       // 0x15 (21)
        public byte SubCode;          // 0x02
        public uint TargetCharacterId;// Character ID to kick, or self ID to leave
    }

    /// <summary>
    /// Server-to-client broadcast updating full party roster telemetry.
    /// Wire Header: Action Code 0x15, Subcode 0x05.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct S2C_PartyTelemetryPacket
    {
        public byte ActionCode;       // 0x15 (21)
        public byte SubCode;          // 0x05
        public uint LeaderId;         // Party leader GUID
        public byte MemberCount;      // Number of active members (1..4)
        // Trailing: PartyMemberWireData[MemberCount]
    }
}
```

---

## 5. Experience Distribution Calculus & Follow Kinematics

### 5.1 Shared Party Experience Formula
When a party defeats a monster yielding $\text{BaseEXP}$, the total party pool scales with group size:

$$\text{TotalPool} = \text{BaseEXP} \times \left(1.0 + (\text{MemberCount} - 1) \times 0.20\right)$$

Each member's share is weighted by their level relative to the group's aggregate level:

$$\text{EXP}_{\text{member}} = \text{TotalPool} \times \frac{\text{Level}_{\text{member}}}{\sum_{k=1}^{\text{MemberCount}} \text{Level}_k}$$

**Level Discrepancy Safeguard:**
If $|\text{Level}_{\text{member}} - \text{Level}_{\text{monster}}| > 20$, an attenuation curve reduces experience by $5\%$ per excess level differential to prevent high-level power-leveling exploits.

### 5.2 Follow Movement Kinematics (`FUN_0045f764`)
When moving overworld terrain:
- Leader records breadcrumb position history ($X_t, Y_t$) at 100 ms intervals.
- Follower $k$ interpolates towards breadcrumb step $t - (k \times 16)$, maintaining a 32-pixel trailing distance.
- Follower position collision checks are bypassed against other party members to prevent clumping deadlocks.

---

## 6. Private Server Implementation Guide & C# Production Code

The following service provides full server-side party lifecycle management, bilateral invitations, leadership succession, and experience distribution.

```csharp
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Wonderland.Network.Packets.Party;

namespace Wonderland.Server.Services.Party
{
    public sealed class PartyGroup
    {
        public uint PartyId { get; init; }
        public uint LeaderCharacterId { get; set; }
        public List<PlayerCharacter> Members { get; } = new();

        public bool IsFull => Members.Count >= 4;

        public void Broadcast(object packet)
        {
            foreach (var member in Members)
            {
                member.Send(packet);
            }
        }
    }

    public interface IPartyService
    {
        bool InviteOrApply(PlayerCharacter sender, C2S_PartyInviteOrJoinPacket packet, out string error);
        bool LeaveOrKick(PlayerCharacter sender, uint targetId, out string error);
        void DistributeCombatExp(PartyGroup party, uint baseExp, uint monsterLevel);
    }

    public sealed class PartyService : IPartyService
    {
        private readonly ConcurrentDictionary<uint, PartyGroup> _parties = new();
        private int _nextPartyId = 1;

        public bool InviteOrApply(PlayerCharacter sender, C2S_PartyInviteOrJoinPacket packet, out string error)
        {
            error = string.Empty;

            if (sender == null) throw new ArgumentNullException(nameof(sender));

            var target = sender.CurrentMap?.FindPlayer(packet.TargetCharacterId);
            if (target == null)
            {
                error = "Target player offline";
                return false;
            }

            if (target.IsTeamingDisabled)
            {
                error = "Teaming is turned off";
                return false;
            }

            if (packet.Mode == 2) // Invite to Party
            {
                var party = sender.Party;
                if (party == null)
                {
                    // Create new party with sender as leader
                    party = new PartyGroup
                    {
                        PartyId = (uint)System.Threading.Interlocked.Increment(ref _nextPartyId),
                        LeaderCharacterId = sender.CharacterId
                    };
                    party.Members.Add(sender);
                    sender.Party = party;
                    _parties[party.PartyId] = party;
                }

                if (party.IsFull)
                {
                    error = "Target team is full";
                    return false;
                }

                // Transmit invitation prompt to target player
                target.Send(new S2C_PartyInvitePrompt { SenderName = sender.Name, SenderId = sender.CharacterId });
            }

            return true;
        }

        public bool LeaveOrKick(PlayerCharacter sender, uint targetId, out string error)
        {
            error = string.Empty;

            if (sender == null) throw new ArgumentNullException(nameof(sender));

            var party = sender.Party;
            if (party == null)
            {
                error = "Not in a party";
                return false;
            }

            lock (party)
            {
                var target = party.Members.FirstOrDefault(m => m.CharacterId == targetId);
                if (target == null)
                {
                    error = "Member not found in party";
                    return false;
                }

                // If kicking, sender must be leader
                if (targetId != sender.CharacterId && party.LeaderCharacterId != sender.CharacterId)
                {
                    error = "Only leader can kick members";
                    return false;
                }

                party.Members.Remove(target);
                target.Party = null;
                target.FormationState = 0;

                if (party.Members.Count <= 1)
                {
                    // Disband party if only 1 or 0 members remain
                    foreach (var remaining in party.Members)
                    {
                        remaining.Party = null;
                        remaining.FormationState = 0;
                        remaining.Send(new S2C_PartyDisbandedNotice());
                    }
                    _parties.TryRemove(party.PartyId, out _);
                }
                else if (party.LeaderCharacterId == targetId)
                {
                    // Migrate leadership to next member
                    party.LeaderCharacterId = party.Members[0].CharacterId;
                    party.Members[0].FormationState = 1; // Leader
                }
            }

            return true;
        }

        public void DistributeCombatExp(PartyGroup party, uint baseExp, uint monsterLevel)
        {
            if (party == null || party.Members.Count == 0) return;

            lock (party)
            {
                double partyMultiplier = 1.0 + (party.Members.Count - 1) * 0.20;
                double totalPool = baseExp * partyMultiplier;
                double totalLevels = party.Members.Sum(m => (double)m.Level);

                foreach (var member in party.Members)
                {
                    double share = totalPool * (member.Level / totalLevels);

                    // Check level differential attenuation
                    int levelDiff = Math.Abs((int)member.Level - (int)monsterLevel);
                    if (levelDiff > 20)
                    {
                        double penalty = Math.Max(0.1, 1.0 - (levelDiff - 20) * 0.05);
                        share *= penalty;
                    }

                    member.AddExp((uint)Math.Max(1, Math.Round(share)));
                }
            }
        }
    }
}
```
