import json

with open(r"D:\GitHub\wlopacketcapture\recordings\session_20260911_150803\packets.jsonl", "r", encoding="utf-8") as f:
    for l in f:
        data = json.loads(l)
        seq = data.get("sequence_id")
        dir = data.get("direction")
        raw = data.get("raw_hex", "")
        if not raw: continue
        p = bytes([b ^ 0xAD for b in bytes.fromhex(raw)])
        ac = p[4] if len(p) > 4 else None
        sub = p[5] if len(p) > 5 else None
        
        # Check for character login or name
        if ac == 5 and sub == 3:
            print(f"Seq {seq} Player Info AC 5:3")
        if ac == 23 and sub in (2, 5):
            print(f"Seq {seq} Inventory / Item AC 23:{sub}")
        if seq in (630, 631, 632, 633, 634, 635, 636, 637, 638, 639, 640):
            print(f"Seq {seq} {dir} AC {ac}:{sub} Len={len(p)}")
