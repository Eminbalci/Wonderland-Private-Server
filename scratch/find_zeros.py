import json
import struct

with open(r"D:\GitHub\wlopacketcapture\recordings\session_20260911_150803\packets.jsonl", "r", encoding="utf-8") as f:
    for l in f:
        data = json.loads(l)
        raw = data.get("raw_hex", "")
        if not raw: continue
        p = bytes([b ^ 0xAD for b in bytes.fromhex(raw)])
        if len(p) > 5 and p[4] == 22 and p[5] == 4:
            seq = data.get("sequence_id")
            for i in range(6, len(p) - 13, 14):
                click_id, state, x, y, ent_type, dur, stance = struct.unpack("<HHHHBIB", p[i:i+14])
                if state == 0:
                    print(f"Seq {seq}: ClickID={click_id} State=0x{state:04X} X={x} Y={y} Type={ent_type} Dur={dur} Stance={stance}")
