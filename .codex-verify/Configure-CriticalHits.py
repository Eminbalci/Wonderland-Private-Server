from pathlib import Path
import struct,json,hashlib
root=Path(__file__).resolve().parent.parent
source=Path(r'D:/Game Private/WLRI/data/Item.dat');raw=source.read_bytes()
assert len(raw)%451==0
def u8(r,i):return ((r[i]^0x9a)-9)&255
def u16(r,i):return ((struct.unpack_from('<H',r,i)[0]^0xefc3)-9)&65535
items=[]
# Loader object includes a four-byte header: native offsets0x31/0x33 map to file45/47.
# Tooltip244102/2477a6 requires special137 and nonzero grade, displays grade*2+10 percent.
for off in range(0,len(raw),451):
 r=raw[off:off+451]
 if u8(r,47)==137 and u8(r,45)>0:
  items.append(dict(item_id=u16(r,16),name=r[1:15][::-1].rstrip(b'\0').decode('latin1'),chance_percent=u8(r,45)*2+10))
assert len(items)==217 and len({r['item_id'] for r in items})==len(items)
assert next(r['chance_percent'] for r in items if r['item_id']==11101)==24
assert next(r['chance_percent'] for r in items if r['item_id']==11105)==30
(root/'Data/critical_hits.json').write_text(json.dumps(dict(damage_multiplier=1.5,source_sha256=hashlib.sha256(raw).hexdigest(),items=items),indent=2)+'\n',encoding='utf-8')
print('Configured',len(items),'native critical equipment entries; totals capped at100% in combat.')
