import json

with open('D:/GitHub/wlopacketcapture/recordings/session_20260911_150803/packets.jsonl', 'r', encoding='utf-8') as f:
    for line in f:
        p = json.loads(line)
        if p.get('action_code') == 22:
            sub = p.get('sub_code')
            payload = p.get('payload_hex', '')
            seq = p.get('sequence_id')
            direction = p.get('direction')
            print(f"Seq {seq}, Dir {direction}, Sub {sub}, Len {len(payload)//2}: {payload[:80]}")
            if sub == 4 and direction == 'S->C':
                b = bytes.fromhex(payload)
                # payload begins AFTER action_code and sub_code
                # Let's check format: count: 2B
                if len(b) >= 2:
                    cnt = int.from_bytes(b[0:2], 'little')
                    print(f"  AC 22:4 NPC Count = {cnt}")
                    offset = 2
                    for i in range(cnt):
                        if offset + 14 > len(b): break
                        cid = int.from_bytes(b[offset:offset+2], 'little')
                        state = int.from_bytes(b[offset+2:offset+4], 'little')
                        x = int.from_bytes(b[offset+4:offset+6], 'little')
                        y = int.from_bytes(b[offset+6:offset+8], 'little')
                        etype = b[offset+8]
                        dur = int.from_bytes(b[offset+9:offset+13], 'little')
                        st = b[offset+13]
                        offset += 14
                        print(f"    NPC #{i+1}: ClickID={cid} State=0x{state:04X} ({state}) at ({x},{y}) Type={etype} Dur={dur} Stance={st}")
