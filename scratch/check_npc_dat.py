import os
import struct

npc_dat_path = r"D:\GitHub\Wonderland-Private-Server\Data\Npc.dat"
with open(npc_dat_path, "rb") as f:
    raw = f.read()

# Npc.dat is encrypted with XOR 0x5209? Or plain?
# Let's check docs/04_data_file_pipeline_and_asset_parsing.md
# docs says: Npc.dat (4,928 NPCs via XOR 0x5209)
print(f"Npc.dat size: {len(raw)}")
