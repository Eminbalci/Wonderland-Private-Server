import json
import struct

def xor_bytes(data: bytes, key: int = 0xAD) -> bytes:
    return bytes([b ^ key for b in data])

with open(r"D:\GitHub\wlopacketcapture\recordings\session_20260911_150803\packets.jsonl", "r", encoding="utf-8") as f:
    for line in f:
        pkt = json.loads(line)
        raw_hex = pkt.get("raw_hex", "")
        if not raw_hex:
            continue
        data = bytes.fromhex(raw_hex)
        if pkt.get("is_encrypted", False) or (len(data) >= 2 and data[0] == 0x59 and data[1] == 0xE9):
            data = xor_bytes(data, 0xAD)
        
        if len(data) >= 6 and data[0] == 0xF4 and data[1] == 0x44:
            ac = data[4]
            sub = data[5]
            if ac == 22 and sub == 4:
                seq = pkt.get("sequence_id")
                print(f"\n=== Seq {seq} AC 22:4 TotalLen {len(data)} ===")
                offset = 6
                idx = 0
                while offset + 14 <= len(data):
                    idx += 1
                    click_id, state, x, y, ent_type, duration, stance = struct.unpack("<HHHHBIB", data[offset:offset+14])
                    offset += 14
                    print(f"  #{idx:02d}: ClickID={click_id:3d} State=0x{state:04X} ({state:5d}) X={x:4d} Y={y:4d} Type={ent_type} Dur={duration} Stance={stance}")
