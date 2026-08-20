# Technical Specification: Vehicle Movement & Portal Unlocking

## 1. Vehicle Movement Synchronization (AC 06 & AC 15 Sub 14)
- **PCAP Reference:** `firstquestandsomechests.pcapng` Frames 1027-1028
- **Behavior:** When riding a vehicle (e.g. Raft `48016`), movement updates trigger `AC 15 Sub 14` (`0f 0e 15 [CharID] 00 00 00 00 00 00`) alongside standard map movement broadcasts so that client vehicle physics and directional controls remain responsive.
- **Ocean Transition:** Sea portal (Portal ID 11) on Newbie Island (10036) seamlessly transfers the player to World Ocean Map 11016 at (134, 1126) without modal locks.
