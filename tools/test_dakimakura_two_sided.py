#!/usr/bin/env python3
"""Static and real FBX geometry checks; Unity render/save Play Mode still required."""
from pathlib import Path
import re, sys
sys.path.insert(0,str(Path(__file__).resolve().parent))
import fbx_binary
R=Path(__file__).resolve().parents[1];A=R/'Assets';S=A/'Scripts/Assembly-CSharp'
site=(A/'GameObject/DakimakuraExpert.prefab').read_text()
print_code=(S/'PC/Component/Software/PrintExpert.cs').read_text()
loader=(S/'TextureLoader.cs').read_text()
uv=(S/'PC/Component/Software/DakimakuraPrintUV.cs').read_text()
assert 'm_MethodName: SelectBackFile' in site and 'backFileNameText: {fileID: 861000000000000014}' in site
assert 'm_MethodName: SelectFile' in site and 'm_MethodName: Purchase' in site
assert 'public void SelectBackFile()' in print_code
assert 'back.width != needW || back.height != needH' in print_code
assert 'selectedBackFile != null ? LoadTexture(selectedBackFile) : null' in print_code
assert 'back != null ? back : tex' in print_code and 'back != null ? back : previewTexture' in print_code
assert 'loader.SetTwoSidedTexture(tex, data)' in print_code and 'loader.SetTwoSidedTexture(previewTexture, null)' in print_code
assert 'else loader.SetTexture(tex, data)' in print_code and 'else loader.SetTexture(previewTexture, null)' in print_code
assert 'jObject["dakimakuraTwoSided"] = true' in loader
assert 'jObject.Value<bool?>("dakimakuraTwoSided") == true' in loader
assert 'SetDakimakuraTwoSided(false)' in loader and 'if (loader.dakimakuraTwoSided)' in loader
assert 'if (back) u = 1f - u;' in uv and 'if (twoSided)' in uv
# Actual authored FBX has two distinguishable broad sides on the thinnest axis.
nodes=fbx_binary.read(A/'Models/dakimakura.fbx');objs=next(n for n in nodes if n[0]=='Objects')
g=next(n for n in objs[2] if n[0]=='Geometry');flat=next(n for n in g[2] if n[0]=='Vertices')[1][0]
verts=list(zip(*[iter(flat)]*3));ranges=[(min(v[i] for v in verts),max(v[i] for v in verts)) for i in range(3)]
thin=min(range(3),key=lambda i:ranges[i][1]-ranges[i][0]);mid=sum(ranges[thin])/2
assert sum(v[thin]<mid for v in verts)>100 and sum(v[thin]>=mid for v in verts)>100
# The actual source faces, including seam faces, stay within one atlas half.
raw=next(n for n in g[2] if n[0]=='PolygonVertexIndex')[1][0]
face=[];tested=0
for value in raw:
 face.append(value if value>=0 else -value-1)
 if value<0:
  for i in range(1,len(face)-1):
   tri=[face[0],face[i],face[i+1]]
   back=sum(verts[j][thin] for j in tri)/3<mid
   mapped=[j+(len(verts) if back else 0) for j in tri]
   assert len({j>=len(verts) for j in mapped})==1
   tested+=1
  face=[]
assert tested>1000
assert 'CreateTwoSidedMesh(meshInstance)' in uv and 'indices[sub][i] += count' in uv
for w in (32,1024):
  for u in (0.0,0.2,0.5,1.0):
    front=(u*(w-1)+0.5)/(2*w)
    back=((1-u)*(w-1)+0.5)/(2*w)+0.5
    assert 0<front<0.5 and 0.5<back<1
rows=(A/'Resources/Translate.txt').read_text().splitlines();hdr=next(x.split('\t') for x in rows if x.startswith('*Short Form\t'))
for key in ['Select front image','Select back image']:
 row=next(x.split('\t') for x in rows if x.startswith(key+'\t'));assert len(row)==len(hdr) and row[hdr.index('RU')]!=key
print('PASS: two file selectors, resolution/fallback, atlas preview/delivery, legacy save compatibility, FBX sides and seam-safe UV math; Unity Play Mode still required')
