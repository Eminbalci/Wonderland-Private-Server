from pathlib import Path
import struct,json,hashlib
exec(Path('.codex-verify/Inspect-NativeDispatch.py').read_text().split('for ac in')[0])
b=Path('.codex-verify/deploy-backup-20260919-131033-013-native-gacha/aLogin.exe').read_bytes()
def dest(item):
 pc=0x2a3d59;eax=item;z=False;greater=False;above=False
 for _ in range(80):
  q=offset(pc);op=b[q];
  if b[q:q+5]==bytes.fromhex('be01000000'):return pc
  if pc==0x2a452e:return None
  if op in (0x3d,0x2d,0x05):
   v=u32(pc+1);v=v if v<0x80000000 else v-0x100000000
   result=eax-v if op!=5 else eax+v;z=result==0;greater=result>0;above=eax>(v&0xffffffff)
   if op!=0x3d:eax=result
   pc+=5
  elif op==0x48:eax-=1;z=eax==0;pc+=1
  elif op==0x83:
   v=b[q+2];result=eax-v;z=result==0;greater=result>0;above=(eax&0xffffffff)>v
   if b[q+1]==0xe8:eax=result
   else:assert b[q+1]==0xf8
   pc+=3
  elif op in (0x7f,0x74,0x77):
   cond={0x7f:greater,0x74:z,0x77:above}[op];pc+=2+(struct.unpack_from('<b',b,q+1)[0] if cond else 0)
  elif op==0x0f:
   cond={0x8f:greater,0x84:z,0x87:above}[b[q+1]];pc+=6+(struct.unpack_from('<i',b,q+2)[0] if cond else 0)
  elif op==0xe9:pc+=5+struct.unpack_from('<i',b,q+1)[0]
  elif b[q:q+3]==bytes.fromhex('ff2485'):pc=u32(u32(pc+3)+4*eax)
  else:raise ValueError((hex(pc),b[q:q+8].hex()))
 raise ValueError('loop')
defs={i['id']:i for i in json.loads(Path('.codex-verify/mall-item-definitions.json').read_text(encoding='utf-8-sig'))}
allpacks=[]
for item in range(65536):
 va=dest(item)
 if not va:continue
 q=offset(va);glob=u32(va+7);table=u32(glob)+10
 chunk=b[q:q+72];count=chunk[chunk.index(bytes.fromhex('83fe'))+2]-1
 pet=bytes.fromhex('e819090000') in chunk
 rows=[]
 for i in range(count):
  rid,qty,unknown=struct.unpack_from('<HHI',b,offset(table)+i*8)
  rows.append(dict(item_id=rid,name=defs.get(rid,{}).get('name','MISSING'),quantity=qty,client_value=unknown))
 allpacks.append(dict(item_id=item,name=defs.get(item,{}).get('name','MISSING'),kind='pet' if pet else 'item',case_va=hex(va),table_va=hex(table),rewards=rows))
Path('.codex-verify/client-gacha-extracted.json').write_text(json.dumps(allpacks,indent=2))
for p in allpacks:print(p['item_id'],p['name'],p['kind'],len(p['rewards']),'missing',[(r['item_id'],r['quantity']) for r in p['rewards'] if r['name']=='MISSING'])
print('Gift pack:',json.dumps(allpacks[0],indent=2))
