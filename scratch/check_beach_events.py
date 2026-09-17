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
ev_offset = offsets[4] # categoryoffset.Events
p = data_ptr + ev_offset
elen = struct.unpack("<H", d[p:p+2])[0]
p += 2
print(f"Total events on Map {target_map}: {elen}")

for e in range(elen):
    click_id = struct.unpack("<H", d[p:p+2])[0]; p += 2
    unk_b = d[p]; p += 1
    ev_name = d[p:p+20].split(b'\0')[0].decode('big5', errors='ignore'); p += 20
    sub_count = d[p]; p += 1
    print(f"\nEvent #{e+1}: ClickID={click_id} Name='{ev_name}' SubCount={sub_count}")
    for s in range(sub_count):
        sub_idx = d[p]; p += 1
        ub1 = d[p]; p += 1
        uw1, uw2, uw3, uw4, uw5, uw6 = struct.unpack("<6H", d[p:p+12]); p += 12
        udw1, udw2 = struct.unpack("<2I", d[p:p+8]); p += 8
        blen2 = d[p]; p += 1
        print(f"  Sub #{s+1} (idx={sub_idx}): uw1={uw1} uw2={uw2} uw3={uw3} uw4={uw4} op_count={blen2}")
        for op_idx in range(blen2):
            ss_idx, dialog_ptr = d[p:p+2]; p += 2
            d1, d2, d3, d4 = struct.unpack("<4H", d[p:p+8]); p += 8
            dw1, dw2, dw3 = struct.unpack("<3I", d[p:p+12]); p += 12
            print(f"    Op #{op_idx+1}: ptr={dialog_ptr} d1={d1} d2={d2} d3={d3} d4={d4} dw1={dw1}")
