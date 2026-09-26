from pathlib import Path
import hashlib,struct,json
src=Path(r'D:/Game Private/WLRI/aLogin.exe')
b=bytearray(src.read_bytes())
expected='CA19EE087B601182E872235E58E6BF367F4BB21879226DFE3CC96F1C99624F61'
assert hashlib.sha256(b).hexdigest().upper()==expected,'Unexpected client build; do not patch'
pe=struct.unpack_from('<I',b,60)[0];opt=pe+24;n=struct.unpack_from('<H',b,pe+6)[0];sh=opt+struct.unpack_from('<H',b,pe+20)[0];base=struct.unpack_from('<I',b,opt+28)[0]
sections=[struct.unpack_from('<8sIIII',b,sh+i*40) for i in range(n)]
def offset(va):
 for _,vs,rv,sz,raw in sections:
  if rv<=va-base<rv+max(vs,sz):return raw+va-base-rv
 raise ValueError(hex(va))
def align(x,a):return (x+a-1)//a*a
sa,fa=struct.unpack_from('<II',b,opt+32)
rva=align(max(s[2]+max(s[1],s[3]) for s in sections),sa);raw=align(len(b),fa);start=base+rva
assert sh+(n+1)*40<=min(s[4] for s in sections if s[3])
# This build leaves an unused ninth section descriptor outside NumberOfSections.
assert b[sh+n*40:sh+(n+1)*40].hex()=='00000000000000000000000000808900000000000034520000000000000000000000000040000050'
assert all(not (0 < struct.unpack_from('<II',b,opt+96+i*8)[0] < 1024) for i in range(16))
code=bytearray();hooks=[]
def jump(target):
 here=start+len(code);code.extend(b'\xe9'+struct.pack('<i',target-here-5))
def comparisons(reg,target):
 for item in (34199,34229,34333):
  code.extend(b'\x66\x81'+bytes([reg])+struct.pack('<H',item))
  here=start+len(code);code.extend(b'\x0f\x84'+struct.pack('<i',target-here-6))
def hook(va,original,kind):
 assert b[offset(va):offset(va)+len(original)]==original
 begin=start+len(code)
 if kind in ('static','dynamic'):
  comparisons(0xfa,begin+33+len(original)+5) # CMP DX, item; JE return
  code.extend(original);jump(va+len(original))
  code.extend(b'\xb0'+bytes([kind=='dynamic'])+b'\xc3')
 else:
  comparisons(0xfb,0x2a452e) # CMP BX, item; JE native dynamic contents renderer
  code.extend(original);jump(va+len(original))
 patch=b'\xe9'+struct.pack('<i',begin-va-5)+b'\x90'*(len(original)-5)
 b[offset(va):offset(va)+len(original)]=patch
 hooks.append(dict(kind=kind,address=hex(va),target=hex(begin),original=original.hex(),patch=patch.hex()))
hook(0x3d19cc,bytes.fromhex('558bec515356'),'static')
hook(0x3d3694,bytes.fromhex('558bec515356'),'dynamic')
hook(0x2a3d56,bytes.fromhex('0fb7c33d2d860000'),'contents')
header=struct.pack('<8sIIIIIIHHI',b'.gacha\0\0',len(code),rva,align(len(code),fa),raw,0,0,0,0,0x60000020)
b[sh+n*40:sh+(n+1)*40]=header
struct.pack_into('<H',b,pe+6,n+1)
struct.pack_into('<I',b,opt+56,align(rva+len(code),sa))
struct.pack_into('<I',b,opt+4,struct.unpack_from('<I',b,opt+4)[0]+align(len(code),fa))
struct.pack_into('<I',b,opt+64,0)
b.extend(bytes(raw-len(b)));b.extend(code);b.extend(bytes(align(len(code),fa)-len(code)))
out=Path('.codex-verify/gacha-client');out.mkdir(exist_ok=True)
(out/'aLogin.exe').write_bytes(b)
manifest=dict(source=str(src),source_sha256=expected,patched_sha256=hashlib.sha256(b).hexdigest().upper(),section_va=hex(start),hooks=hooks)
(out/'manifest.json').write_text(json.dumps(manifest,indent=2))
print(json.dumps(manifest,indent=2))
