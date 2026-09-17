import struct
import sys
sys.stdout.reconfigure(encoding='utf-8')

with open(r'Data/eve.Emg', 'rb') as f:
    d = f.read()

ptr = 8
map_count = struct.unpack('<I', d[ptr:ptr+4])[0]
ptr += 4

maps = {}
for i in range(map_count):
    map_id, scene_id, data_ptr, data_len = struct.unpack('<HHIH', d[ptr:ptr+10])
    ptr += 10
    maps[map_id] = (scene_id, data_ptr, data_len)

target_map = 10003
scene_id, data_ptr, data_len = maps[target_map]
offset_ptr = data_ptr + data_len - 44
offsets = struct.unpack('<11I', d[offset_ptr:offset_ptr+44])
npc_offset = offsets[0]
ev_offset = offsets[4]

# Read NPCs
p = data_ptr + npc_offset
elen = struct.unpack('<H', d[p:p+2])[0]; p += 2
npcs = []
for n in range(elen):
    click_id = struct.unpack('<H', d[p:p+2])[0]; p += 2
    name = d[p:p+20].split(b'\0')[0].decode('big5', errors='ignore'); p += 20
    unk_b1 = d[p]; p += 1
    nx, ny = struct.unpack('<II', d[p:p+8]); p += 8
    blen = d[p]; p += 1
    events = list(d[p:p+blen]); p += blen
    blen2 = d[p]; p += 1
    p += blen2
    unk_b2 = d[p]; p += 1
    npc_id = struct.unpack('<I', d[p:p+4])[0]; p += 4
    rot = d[p]; p += 1
    p += 2
    w_len = d[p]; p += 1
    p += w_len * 12
    p += 2
    udw1, udw2 = struct.unpack('<II', d[p:p+8]); p += 8
    p += 3
    wp_len = d[p]; p += 1
    p += wp_len * (4 + 8 + 10 * 12)
    uw = struct.unpack('<5H', d[p:p+10]); p += 10
    npcs.append({'click_id': click_id, 'name': name, 'tid': npc_id, 'x': nx, 'y': ny, 'events': events, 'uw': uw})

# Read Events
p = data_ptr + ev_offset
elen = struct.unpack('<H', d[p:p+2])[0]; p += 2
events = []
for e in range(elen):
    click_id = struct.unpack('<H', d[p:p+2])[0]; p += 2
    unk_b = d[p]; p += 1
    ev_name = d[p:p+20].split(b'\0')[0].decode('big5', errors='ignore'); p += 20
    sub_count = d[p]; p += 1
    subs = []
    for s in range(sub_count):
        sub_idx = d[p]; p += 1
        ub1 = d[p]; p += 1
        uw = struct.unpack('<6H', d[p:p+12]); p += 12
        udw = struct.unpack('<2I', d[p:p+8]); p += 8
        blen2 = d[p]; p += 1
        ops = []
        for op_idx in range(blen2):
            ss_idx, dialog_ptr = d[p:p+2]; p += 2
            d1, d2, d3, d4 = struct.unpack('<4H', d[p:p+8]); p += 8
            dw1, dw2, dw3 = struct.unpack('<3I', d[p:p+12]); p += 12
            ops.append({'ptr': dialog_ptr, 'd1': d1, 'd2': d2, 'd3': d3, 'd4': d4, 'dw1': dw1})
        subs.append({'sub_idx': sub_idx, 'uw': uw, 'ops': ops})
    events.append({'click_id': click_id, 'name': ev_name, 'subs': subs})

for npc in npcs:
    print(f"NPC ClickID={npc['click_id']} TID={npc['tid']} ({npc['x']},{npc['y']}) Events={npc['events']}")
    for ev_idx in npc['events']:
        if 1 <= ev_idx <= len(events):
            ev = events[ev_idx-1]
            print(f"  Attached Event #{ev_idx} (ClickID={ev['click_id']}, Name={ev['name']}):")
            for sub in ev['subs']:
                print(f"    Sub {sub['sub_idx']}: uw1={sub['uw'][0]} ops={len(sub['ops'])}")
                for op in sub['ops']:
                    print(f"      Op: ptr={op['ptr']} d1={op['d1']} d2={op['d2']} d3={op['d3']} d4={op['d4']}")
