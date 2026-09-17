import struct
import sys
sys.stdout.reconfigure(encoding='utf-8')

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
scene_id, data_ptr, data_len = maps[target_map]
offset_ptr = data_ptr + data_len - 44
offsets = struct.unpack("<11I", d[offset_ptr:offset_ptr+44])
item_offset = offsets[3] # categoryoffset.Items
p = data_ptr + item_offset
elen = struct.unpack("<H", d[p:p+2])[0]; p += 2
print(f"Map {target_map} Items: {elen}")
for i in range(elen):
    cid = struct.unpack("<H", d[p:p+2])[0]; p += 2
    raw_name_len = d[p]; p += 1
    raw_name = d[p:p+19]; p += 19
    name = raw_name.split(b'\0')[0].decode('big5', errors='ignore')
    ub1 = d[p]; p += 1
    x, y = struct.unpack("<II", d[p:p+8]); p += 8
    blen1 = d[p]; p += 1 + blen1
    blen2 = d[p]; p += 1 + blen2
    ub2 = d[p]; p += 1
    item_id = struct.unpack("<I", d[p:p+4])[0]; p += 4
    ub3, ub4, ub5 = d[p:p+3]; p += 3
    uw1, uw2 = struct.unpack("<2H", d[p:p+4]); p += 4
    print(f"  Item #{i+1}: ClickID={cid} Name='{name}' ItemID={item_id} at ({x},{y})")
