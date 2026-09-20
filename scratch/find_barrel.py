import struct

with open(r"D:\GitHub\Wonderland-Private-Server\Data\eve.Emg", "rb") as f:
    d = f.read()

# Stage 1: Load Map Entries
# Header: 8 bytes, then entrylen (4 bytes)
ptr = 8
map_count = struct.unpack("<I", d[ptr:ptr+4])[0]
ptr += 4
print(f"Total map count in header: {map_count}")

maps = {}
for i in range(map_count):
    map_id, scene_id, data_ptr, data_len = struct.unpack("<HHIH", d[ptr:ptr+10])
    ptr += 10
    maps[map_id] = (scene_id, data_ptr, data_len)

print(f"Loaded {len(maps)} maps.")

# Let's inspect map 10003 or any map around 10000-13000
# Also find all NPCs on maps where coordinates are around X in [500, 800], Y in [1300, 1600]
matches = []

for map_id, (scene_id, data_ptr, data_len) in maps.items():
    if data_len < 44:
        continue
    offset_ptr = data_ptr + data_len - 44
    offsets = struct.unpack("<11I", d[offset_ptr:offset_ptr+44])
    npc_offset = offsets[0] # categoryoffset.NPC
    if npc_offset == 0:
        continue
    p = data_ptr + npc_offset
    elen = struct.unpack("<H", d[p:p+2])[0]
    p += 2
    for n in range(elen):
        click_id = struct.unpack("<H", d[p:p+2])[0]
        p += 2
        raw_name = d[p:p+20]
        p += 20
        # decode name (big5)
        name = raw_name.split(b'\0')[0].decode('big5', errors='ignore')
        unknownbyte1 = d[p]; p += 1
        nx, ny = struct.unpack("<II", d[p:p+8]); p += 8
        # skip events
        blen = d[p]; p += 1 + blen
        # skip unknownbytearray2
        blen = d[p]; p += 1 + blen
        unknownbyte2 = d[p]; p += 1
        npc_id = struct.unpack("<I", d[p:p+4])[0]; p += 4
        rot = d[p]; p += 1
        ub4 = d[p]; p += 1
        ub5 = d[p]; p += 1
        blen = d[p]; p += 1 + blen * 12
        ub6 = d[p]; p += 1
        ub7 = d[p]; p += 1
        udw1, udw2 = struct.unpack("<II", d[p:p+8]); p += 8
        ub8, ub9, ub10 = d[p:p+3]; p += 3
        blen = d[p]; p += 1 + blen * (1 + 1 + 1 + 1 + 4 + 4 + 10 * 12)
        uw1, uw2, uw3, uw4, uw5 = struct.unpack("<5H", d[p:p+10]); p += 10

        if 500 <= nx <= 800 and 1300 <= ny <= 1600:
            matches.append((map_id, click_id, npc_id, name, nx, ny, unknownbyte1, uw1, uw2))

print(f"Found {len(matches)} NPCs matching coords:")
for m in matches:
    print(f"Map {m[0]}: ClickID={m[1]} TID={m[2]} Name='{m[3]}' at ({m[4]},{m[5]}) type={m[6]} uw1={m[7]} uw2={m[8]}")
