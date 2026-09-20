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
category_names = ["NPC", "Entry", "Mining", "Items", "Events", "Groups", "Warp", "Interactive", "Battle", "PreEvent", "GroupExt"]

for idx, name in enumerate(category_names):
    off = offsets[idx]
    if off == 0:
        print(f"Category {name}: 0 entries")
        continue
    p = data_ptr + off
    elen = struct.unpack("<H", d[p:p+2])[0]
    print(f"Category {name} (off={off}): {elen} entries")
    if name == "Items":
        p += 2
        for i in range(elen):
            click_id = struct.unpack("<H", d[p:p+2])[0]; p += 2
            item_id = struct.unpack("<H", d[p:p+2])[0]; p += 2
            x, y = struct.unpack("<II", d[p:p+8]); p += 8
            slot = d[p]; p += 1
            respawn = struct.unpack("<I", d[p:p+4])[0]; p += 4
            print(f"  ItemArea #{i+1}: Slot={slot} ClickID={click_id} ItemID={item_id} at ({x},{y}) Respawn={respawn}")
    elif name == "Interactive":
        p += 2
        for i in range(elen):
            cid = struct.unpack("<H", d[p:p+2])[0]; p += 2
            raw = d[p:p+20]; p += 20
            print(f"  Interactive #{i+1}: ClickID={cid} Raw={raw.hex()}")
