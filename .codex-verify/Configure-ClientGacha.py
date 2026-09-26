from pathlib import Path
import json,math,struct,hashlib
p=Path('.codex-verify/client-gacha-extracted.json');packs=json.loads(p.read_text());defs={x['id']:x for x in json.loads(Path('.codex-verify/mall-item-definitions.json').read_text(encoding='utf-8-sig'))}
for x in packs:
 ids=[r['item_id'] for r in x['rewards']]
 if len(set(ids))!=len(ids):print('repeated rewards',x['item_id'])
print('Name placeholders',[(i,defs[i]) for i in [34381,34382,34383,34384]])
# Add only the definition present in client but absent from server, using the same
# packed 47-byte schema. Other entries remain byte-for-byte unchanged.
client=Path(r'D:/Game Private/WLRI/data/Item.dat').read_bytes();data=Path('Data/itemDat.wpdat').read_bytes()
rows={struct.unpack_from('<H',data,i+22)[0]:data[i:i+47] for i in range(0,len(data),47)}
def word(r,o):return ((struct.unpack_from('<H',r,o)[0]^0xefc3)-9)&65535
def byte(r,o):return ((r[o]^0x9a)-9)&255
def dword(r,o):return ((struct.unpack_from('<I',r,o)[0]^0x0b80f4b4)-9)&0xffffffff
for off in range(0,len(client),451):
 r=client[off:off+451]
 if word(r,16)!=27025:continue
 name=r[1:15][::-1].rstrip(b'\0');assert name==b'Ice Snowflake'
 # Native width/height field is not needed for this 1x1 consumable; same layout
 # as all existing Pet_Fruit entries, equipped in no normal equipment slot (8).
 entry=struct.pack('<B20sBHHHHHBBBHHii',20,name,byte(r,15),27025,word(r,18),word(r,20),byte(r,46),byte(r,34),byte(r,35),1,1,word(r,30),word(r,32),dword(r,36),dword(r,40))
 assert struct.unpack('<B20sBHHHHHBBBHHii',entry)[2:]==(20,27025,5229,1000,8,0,0,1,1,0,0,100,100)
 if 27025 not in rows:
  Path('.codex-verify/itemDat-before-client-gacha.wpdat').write_bytes(data)
  Path('Data/itemDat.wpdat').write_bytes(data+entry)
 defs[27025]={'id':27025,'name':'Ice Snowflake','type':20}
 break
else:raise ValueError('Client item absent')
config=[];report=['# Client gacha pools','', 'Rewards and quantities follow the original client preview order (down each column, then next column). Weights are custom, not official: geometric decline from first to last about 100:1, normalized to 10000 (100%).','']
for pack in packs:
 n=len(pack['rewards']);raw=[100**(-i/(n-1)) for i in range(n)];scaled=[r/sum(raw)*10000 for r in raw];weights=[math.floor(r) for r in scaled]
 for i in sorted(range(n),key=lambda i:scaled[i]-weights[i],reverse=True)[:10000-sum(weights)]:weights[i]+=1
 assert sum(weights)==10000 and all(weights[i]>weights[i+1] for i in range(n-1)) and min(weights)>0
 out=dict(item_id=pack['item_id'],name=pack['name'],source_table=pack['table_va'],rewards=[])
 report+=['## '+str(pack['item_id'])+' '+pack['name'],'','| Order | Item | Quantity | Chance |','| --- | --- | --- | --- |']
 for i,(r,w) in enumerate(zip(pack['rewards'],weights)):
  assert r['item_id'] in defs
  item=dict(item_id=r['item_id'],name=defs[r['item_id']]['name'],quantity=r['quantity'],weight=w);out['rewards'].append(item)
  report.append(f"| {i+1} | {item['item_id']} {item['name']} | {item['quantity']} | {w/100:.2f}% |")
 report.append('');config.append(out)
Path('Data/gacha_packs.json').write_text(json.dumps(config,indent=2,ensure_ascii=False)+'\n',encoding='utf-8')
Path('Data/gacha_rates.md').write_text('\n'.join(report),encoding='utf-8')
print('Configured',len(config),'packs',sum(len(x['rewards']) for x in config),'reward rows; item27025 definition imported')
