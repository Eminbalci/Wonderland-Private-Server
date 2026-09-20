import json

with open(r"D:\GitHub\wlopacketcapture\recordings\session_20260911_150803\packets.jsonl", "r", encoding="utf-8") as f:
    for l in f:
        data = json.loads(l)
        seq = data.get("sequence_id")
        dir = data.get("direction")
        if 655 <= seq <= 685:
            raw = data.get("raw_hex", "")
            if not raw: continue
            p = bytes([b ^ 0xAD for b in bytes.fromhex(raw)])
            ac = p[4] if len(p) > 4 else None
            sub = p[5] if len(p) > 5 else None
            print(f"Seq {seq:4d} {dir} AC {ac}:{sub} Hex: {p[4:].hex()}")
