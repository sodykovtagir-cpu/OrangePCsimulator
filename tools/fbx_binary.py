"""Minimal read-only binary FBX node/property reader for actual-model shop renders."""
import struct,zlib
from pathlib import Path

def read(p):
 b=Path(p).read_bytes();ver=struct.unpack_from('<I',b,23)[0];wide=ver>=7500;fmt='<QQQ' if wide else '<III';hdr=25 if wide else 13
 def prop(i):
  t=chr(b[i]);i+=1
  formats={'Y':'h','C':'?','I':'i','F':'f','D':'d','L':'q'}
  if t in formats:
   f='<'+formats[t];return struct.unpack_from(f,b,i)[0],i+struct.calcsize(f)
  if t in ['S','R']:
   n=struct.unpack_from('<I',b,i)[0];i+=4;return b[i:i+n],i+n
  if t in 'fdlibc':
   n,enc,size=struct.unpack_from('<III',b,i);i+=12;data=b[i:i+size];i+=size
   if enc:data=zlib.decompress(data)
   return struct.unpack('<'+{'f':'f','d':'d','l':'q','i':'i','b':'?','c':'b'}[t]*n,data),i
  raise ValueError(t)
 def node(i):
  end,n,plen=struct.unpack_from(fmt,b,i);ln=b[i+hdr-1];i+=hdr
  if end==0:return None,i
  name=b[i:i+ln].decode();i+=ln;pr=[]
  for _ in range(n):v,i=prop(i);pr.append(v)
  children=[]
  while i<end-hdr:
   x,i=node(i)
   if x:children.append(x)
  return (name,pr,children),end
 nodes=[];i=27
 while i<len(b)-hdr:
  nd,i=node(i)
  if nd is None:break
  nodes.append(nd)
 return nodes
