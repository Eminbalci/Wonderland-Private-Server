import struct

with open(r"D:\GitHub\Wonderland-Private-Server\Data\eve.Emg", "rb") as f:
    d = f.read()

ptr = 8
map_count = struct.unpack("<I", d[ptr:ptr+4])[0]
ptr += 4

maps = {}
for i in range(map_count):
    map_id, scene_id, data_ptr, data_len = struct.unpack("<HHIH", d[ptr:ptr+10])
    ptr += 10
    maps[map_id] = (scene_id, data_ptr, data_len)

for target_map in [10003, 10001, 10002, 12000, 12001, 12002, 12003]:
    if target_map not in maps:
        print(f"Map {target_map} not found.")
        continue
    scene_id, data_ptr, data_len = maps[target_map]
    offset_ptr = data_ptr + data_len - 44
    offsets = struct.unpack("<11I", d[offset_ptr:offset_ptr+44])
    npc_offset = offsets[0]
    if npc_offset == 0:
        print(f"Map {target_map}: No NPCs.")
        continue
    p = data_ptr + npc_offset
    elen = struct.unpack("<H", d[p:p+2])[0]
    p += 2
    print(f"\nMap {target_map} (Scene {scene_id}): {elen} NPCs")
    for n in range(elen):
        click_id = struct.unpack("<H", d[p:p+2])[0]; p += 2
        raw_name = d[p:p+20]; p += 20
        name = raw_name.split(b'\0')[0].decode('big5', errors='ignore')
        unknownbyte1 = d[p]; p += 1
        nx, ny = struct.unpack("<II", d[p:p+8]); p += 8
        blen = d[p]; p += 1 + blen
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
        print(f"  ClickID={click_id} TID={npc_id} Name='{name}' at ({nx},{ny}) type={unknownbyte1} uw2={uw2}")
