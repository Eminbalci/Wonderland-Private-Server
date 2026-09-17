import json

with open(r"D:\GitHub\wlopacketcapture\recordings\session_20260911_150803\packets.jsonl", "r", encoding="utf-8") as f:
    for l in f:
        data = json.loads(l)
        seq = data.get("sequence_id")
        if seq in (250, 251, 252, 253, 254, 255, 256, 380, 381, 382, 383, 630, 631, 632, 633, 634, 635):
            raw = data.get("raw_hex", "")
            p = bytes([b ^ 0xAD for b in bytes.fromhex(raw)]) if raw else b""
            ac = p[4] if len(p) > 4 else None
            sub = p[5] if len(p) > 5 else None
            dir = data.get("direction")
            print(f"Seq {seq} {dir} AC {ac}:{sub} Hex: {p[4:15].hex()}")
