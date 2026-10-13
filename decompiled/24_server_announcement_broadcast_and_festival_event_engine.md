# Server Announcement Broadcast and Festival Event Engine

## 1. Executive Summary & Event Architecture

The Wonderland Online client embeds an automated festival and system announcement HUD dispatcher in subroutine `FUN_004447b4` (RVA `0x004447b4`, line 456040 in [`aLogin_decompiled.c`](file:///D:/GitHub/Wonderland-Private-Server/decompiled/aLogin_decompiled.c)).

Whenever the server broadcasts a system event or holiday festival status update, the packet is framed and passed into `FUN_004447b4(context, packet_ptr)`. The handler inspects byte `*(packet_ptr + 1)` (the Event Sub-Opcode) and emits colorized, high-priority system announcements directly to the player's HUD chat frame via `FUN_004a487c(PTR_DAT_004c8f3c, 0, string_ptr, 0xFF, 0)`.

---

## 2. Exhaustive Festival & Event Sub-Opcode Matrix (`FUN_004447b4`)

The table below catalogs all 70+ event states, operational phases, and exact client string prompts extracted from the binary:

| Sub-Opcode (Hex / Dec) | Event Name & Description | Event Phase | Broadcast String Displayed to Client |
|---|---|---|---|
| `0x01` (1) | Double EXP Event | Activation | `"Double EXP event has begun! Don't miss it!"` |
| `0x02` (2) | Double EXP Event | Deactivation | Standard Double EXP End Notice |
| `0x03` (3) | Item Mall Mini-Games | Activation | `"Double rewards in Item Mall mini-games has begun!"` |
| `0x04` (4) | Item Mall Mini-Games | Deactivation | `"Double rewards in Item Mall mini-games has ended!"` |
| `0x05` (5) | New Year's Eve Celebration | Activation | `"Dear Players, new years eve double expereience activity has started!"` |
| `0x06` (6) | New Year's Eve Celebration | Deactivation | `"New Year Eve's double EXP event has ended!"` |
| `0x07` (7) | WLO Official Anniversary | Activation | `"Anniversary begun! Double EXP, mini-games, and Forges 50% Off!"` |
| `0x08` (8) | WLO Official Anniversary | Deactivation | `"WLO Anniversary has ended!"` |
| `0x09` (9) | EXP Bonus Campaign | Activation | `"EXP Bonus event has begun!"` |
| `0x0A` (10) | EXP Bonus Campaign | Deactivation | `"EXP Bonus event has ended!"` |
| `0x0B` (11) | White Day Romance Event | Activation | `"White Day event has begun!"` |
| `0x0C` (12) | White Day Romance Event | Deactivation | `"White Day event has ended!"` |
| `0x0D` (13) | Happy May Festival | Activation | `"Happy May has begun! Double EXP, mini-games, and Forges 50% Off"` |
| `0x0E` (14) | Happy May Festival | Deactivation | `"Happy May has ended!"` |
| `0x0F` (15) | Sailing Maritime Event | Activation | `"Sailing has begun! Double EXP, mini-games, and Forges 50% Off!"` |
| `0x10` (16) | Sailing Maritime Event | Deactivation | `"Sailing event has ended!"` |
| `0x13` (19) | Weekend EXP Surge | Activation | `"EXP Bonus event has begun! Don't miss it!"` |
| `0x14` (20) | Weekend EXP Surge | Deactivation | `"EXP Bonus event has ended!"` |
| `0x15` (21) | Jade Rabbit Spring Festival | Activation | `"Jade Rabbit Spring Festival event has begun!"` |
| `0x16` (22) | Jade Rabbit Spring Festival | Deactivation | `"Jade Rabbit Spring Festival event has ended!"` |
| `0x19` (25) | National Day Holiday | Activation | `"National Day has begun!"` |
| `0x1A` (26) | National Day Holiday | Deactivation | `"National Day has ended!"` |
| `0x1B` (27) | Halloween Spooktacular | Activation | `"Halloween double EXP event has begun!"` |
| `0x1C` (28) | Halloween Spooktacular | Deactivation | `"Halloween double EXP event has ended!"` |
| `0x1D` (29) | Mid-Season Double EXP | Activation | `"Double EXP event has begun! Don't miss it!"` |
| `0x21` (33) | Trivia Quiz Zone | Activation | `"Quiz Zone event has begun!"` |
| `0x22` (34) | Trivia Quiz Zone | Deactivation | `"Quiz Zone event has ended."` |
| `0x23` (35) | Server-Wide Quiz Contest | Activation | `"Quiz Event has begun. Don't miss to join it!"` |
| `0x24` (36) | Server-Wide Quiz Contest | Deactivation | `"Quiz Event has ended. Thanks for participation!"` |
| `0x25` (37) | Guild / Team PVP Arena | Activation | `"Team PVP event has begun! Don't miss it!"` |
| `0x26` (38) | Guild / Team PVP Arena | Deactivation | `"Team PVP event has ended!"` |
| `0x27` (39) | Valentine's Day Romance | Activation | `"Valentine's Day double EXP event has begun!"` |
| `0x28` (40) | Valentine's Day Romance | Deactivation | `"Valentine's Day double EXP event has ended!"` |
| `0x2E` (46) | Summer Double Rewards | Deactivation | `"Summer double rewards event has ended!"` |
| `0x2F` (47) | Happy Summer Carnival | Activation | `"Happy Summer has begun! Forges are 50% Off!"` |
| `0x30` (48) | Summer Double Rewards | Deactivation | `"Summer double rewards event has ended!"` |
| `0x33` (51) | Lunar New Year Celebration | Activation | `"Lunar New Year special event has begun!"` |
| `0x34` (52) | Blacksmith Forge Promotion | Activation | `"Forging 50% off has begun!"` |
| `0x35` (53) | Blacksmith Forge Promotion | Deactivation | `"Forging 50% off has ended!"` |
| `0x36` (54) | Dragon Boat Festival | Activation | `"Dragon Boat double EXP event has begun!"` |
| `0x37` (55) | Dragon Boat Festival | Deactivation | `"Dragon Boat double EXP event has ended!"` |
| `0x39` (57) | Year-End Countdown | Deactivation | `"New Years Eve double EXP has ended!"` |
| `0x3A` (58) | Summer Vacation Campaign | Activation | `"Summer Vacation double EXP event has begun!"` |
| `0x3B` (59) | Summer Vacation Campaign | Deactivation | `"Summer Vacation double EXP event has ended!"` |
| `0x3C` (60) | Forge Point Subsidy Event | Activation | `"Forge 60 pts discount event has begun!"` |
| `0x3D` (61) | Forge Point Subsidy Event | Deactivation | `"Forge 60 pts discount event has ended!"` |
| `0x3E` (62) | Dragon Boat Festival Wave 2 | Deactivation | `"Dragon Boat double EXP event has ended!"` |
| `0x41` (65) | Mid-Autumn Moon Festival | Activation | `"Mid-Autumn Festival event has begun!"` |
| `0x42` (66) | Mid-Autumn Moon Festival | Deactivation | `"Mid-Autumn Festival event has ended!"` |
| `0x43` (67) | Thanksgiving Harvest Festival| Activation | `"Thanksgiving Turkey event has begun!"` |
| `0x44` (68) | Thanksgiving Harvest Festival| Deactivation | `"Thanksgiving Turkey event has ended!"` |

---

## 3. Protocol Framing & Implementation Guidance

To trigger any of the authentic client announcements from `Wonderland-Private-Server`:

```csharp
// Server-Side Event Broadcast Packet
public static void BroadcastSystemEvent(byte subOpcode)
{
    var packet = new PacketWriter();
    packet.WriteByte(0x2C);        // Action Code: System Announcement / Event Broadcast
    packet.WriteByte(subOpcode);    // Sub-Opcode matching FUN_004447b4
    Server.BroadcastToAll(packet.Build());
}
```

When received, the client executes `FUN_004447b4`, looks up the static string table, and routes the message through `FUN_004a487c` with color code `0xFF` (golden yellow system banner), ensuring authentic player notification across all channels.
