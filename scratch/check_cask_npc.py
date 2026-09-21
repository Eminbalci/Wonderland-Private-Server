import struct

with open(r"D:\GitHub\Wonderland-Private-Server\Data\Npc.dat", "rb") as f:
    raw = f.read()

# Npc struct size from PhoneixNpc.cs:
# byte NpcNameLength (1)
# byte[20] NpcName (20)
# byte Type (1)
# ushort NpcID (2)
# ushort ImageNum (2)
# ushort ImageNumSmall (2)
# uint ColorCode1..4 (16)
# byte Catchable (1)
# byte UnknownByte2, 3 (2)
# byte Level (1)
# uint HP, SP (8)
# ushort STR, CON, INT, WIS, AGI (10)
# byte ImageNumEnlarge (1)
# byte element (1)
# ushort SkillID1..3 (6)

# Total size = 1 + 20 + 1 + 2 + 2 + 2 + 16 + 1 + 2 + 1 + 8 + 10 + 1 + 1 + 6 = 74 bytes!
# Let's verify size:
npc_size = 74
print(f"File size: {len(raw)}, total NPCs: {len(raw) / npc_size}")

# Let's find TID 19037 or search for 19037
for i in range(0, len(raw) - npc_size + 1, npc_size):
    chunk = raw[i:i+npc_size]
    # NpcID is at offset 22 (ushort)
    npcid = struct.unpack("<H", chunk[22:24])[0]
    if npcid in (19034, 19035, 19037, 19038, 19039):
        name_len = chunk[0]
        name = chunk[1:1+name_len].decode('ascii', errors='ignore')
        img_num = struct.unpack("<H", chunk[24:26])[0]
        img_small = struct.unpack("<H", chunk[26:28])[0]
        print(f"NpcID={npcid} Name='{name}' ImageNum={img_num} ImageSmall={img_small}")
