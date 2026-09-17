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

target_map = 10003
if target_map in maps:
    scene_id, data_ptr, data_len = maps[target_map]
    print(f"Map {target_map}: Scene={scene_id} Ptr={data_ptr} Len={data_len}")
    offset_ptr = data_ptr + data_len - 44
    offsets = struct.unpack("<11I", d[offset_ptr:offset_ptr+44])
    npc_offset = offsets[0]
    p = data_ptr + npc_offset
    elen = struct.unpack("<H", d[p:p+2])[0]
    p += 2
    print(f"Total NPCs on Map {target_map}: {elen}")
    for n in range(elen):
        click_id = struct.unpack("<H", d[p:p+2])[0]; p += 2
        raw_name = d[p:p+20]; p += 20
        name = raw_name.split(b'\0')[0].decode('big5', errors='ignore')
        unknownbyte1 = d[p]; p += 1
        nx, ny = struct.unpack("<II", d[p:p+8]); p += 8
        blen = d[p]; p += 1
        events = list(d[p:p+blen]); p += blen
        blen2 = d[p]; p += 1
        ub_arr2 = list(d[p:p+blen2]); p += blen2
        unknownbyte2 = d[p]; p += 1
        npc_id = struct.unpack("<I", d[p:p+4])[0]; p += 4
        rot = d[p]; p += 1
        ub4 = d[p]; p += 1
        ub5 = d[p]; p += 1
        w_len = d[p]; p += 1
        p += w_len * 12
        ub6, ub7 = d[p:p+2]; p += 2
        udw1, udw2 = struct.unpack("<II", d[p:p+8]); p += 8
        ub8, ub9, ub10 = d[p:p+3]; p += 3
        wp_len = d[p]; p += 1
        p += wp_len * (4 + 8 + 10 * 12)
        uw1, uw2, uw3, uw4 = struct.unpack("<4H", d[p:p+8]); p += 8
        print(f"  #{n+1}: ClickID={click_id} TID={npc_id} Name='{name}' at ({nx},{ny}) Events={events} uw2={uw2}")
