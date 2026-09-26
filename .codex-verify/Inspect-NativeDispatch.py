import struct
from pathlib import Path
b=Path(r'D:/Game Private/WLRI/aLogin.exe').read_bytes();pe=struct.unpack_from('<I',b,60)[0];n=struct.unpack_from('<H',b,pe+6)[0];opt=pe+24;base=struct.unpack_from('<I',b,opt+28)[0];sh=opt+struct.unpack_from('<H',b,pe+20)[0]
def offset(va):
 rva=va-base
 for i in range(n):
  _,vs,rv,sz,raw=struct.unpack_from('<8sIIII',b,sh+i*40)
  if rv<=rva<rv+max(vs,sz):return raw+rva-rv
 raise ValueError(hex(va))
def u32(va):return struct.unpack_from('<I',b,offset(va))[0]
for ac in [2,5,8,23,29,30,35]:
 idx=b[offset(0x2dde6c+ac)];print('AC',ac,hex(u32(0x2ddf34+4*idx)))
for sub in [5,6,8,9,15,16,17,57,208]:print('AC23',sub,hex(u32(0x2e5a1f+4*sub)))
