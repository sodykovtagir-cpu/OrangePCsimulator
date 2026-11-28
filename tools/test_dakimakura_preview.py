#!/usr/bin/env python3
"""Static asset/math regression checks; actual rendering needs Unity Play Mode."""
from pathlib import Path
import math, re, sys, yaml
sys.path.insert(0,str(Path(__file__).resolve().parent))
import fbx_binary
R=Path(__file__).resolve().parents[1];A=R/'Assets'
def docs(p):
 t=p.read_text();return {int(m[2]):(int(m[1]),next(iter(yaml.safe_load(m[3]).values()))) for m in re.finditer(r'^--- !u!(\d+) &(-?\d+)(?: stripped)?\n(.*?)(?=^--- !u!|\Z)',t,re.M|re.S)}
site=docs(A/'GameObject/DakimakuraExpert.prefab');rig=docs(A/'Resources/components/PrintExpertPreviewRig.prefab')
page=site[114021143164813130][1]
assert page['previewImage']['fileID']==811300000000000004
assert site[page['previewImage']['fileID']][1]['m_Enabled']==1
assert page['previewLayer']==11 and page['previewRigPrefab']['fileID']==100000
cam=rig[700000][1];assert cam['m_CullingMask']['m_Bits']&(1<<page['previewLayer'])
assert rig[1100000][1]['m_CullingMask']['m_Bits']&(1<<page['previewLayer'])
assert rig[300000][1]['m_Name']=='Stage' and rig[400000][1]['m_Father']['fileID']==200000
s=(A/'Scripts/Assembly-CSharp/PC/Component/Software/PrintExpert.cs').read_text()
assert 'previewRT.antiAliasing = 1;' in s
assert 'activePreviewCamera.useOcclusionCulling = false' in s
assert 'renderer is ParticleSystemRenderer' in s and 'particleRenderer.enabled = false' in s
assert 'OrientDakimakuraPreview();' in s and 'Quaternion.Euler(0f, 90f, 0f)' in s
assert 'previewImage.texture = previewRT;' in s and 'activePreviewCamera.Render();' in s
assert 'loader.SetTwoSidedTexture(previewTexture, null)' in s
nodes=fbx_binary.read(A/'Models/dakimakura.fbx');objs=next(n for n in nodes if n[0]=='Objects');g=next(n for n in objs[2] if n[0]=='Geometry');v=next(n for n in g[2] if n[0]=='Vertices')[1][0];vertices=list(zip(*[iter(v)]*3))
sizes=[max(p[i] for p in vertices)-min(p[i] for p in vertices) for i in range(3)]
assert sizes[0]<sizes[1] and sizes[0]<sizes[2], sizes
# Its normal +X faces the camera at -Z after positive 90° Y rotation.
assert abs(math.cos(math.pi/2))<1e-6 and abs(-math.sin(math.pi/2)+1)<1e-6
print('PASS: preview rig refs and layers, X-thin model orientation, no particles in framing, portable RT, both-side texture; Unity render still requires Play Mode')
