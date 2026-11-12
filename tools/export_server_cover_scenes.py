#!/usr/bin/env python3
"""Export real Unity prefab meshes/materials/transforms for Blender renders.
No replacement boxes, invented geometry or image-generation models are used.
"""
import json,re,struct,hashlib,math
from pathlib import Path
import numpy as np
from server_assets import ROOT,COMP,Prefab,read_guid
from fbx_binary import read as read_fbx
INDEX={}
for p in (ROOT/'Assets').rglob('*.meta'):
    m=re.search(r'^guid: (\w+)',p.read_text(errors='ignore'),re.M)
    if m:INDEX[m[1]]=Path(str(p)[:-5])
SOURCES={}
def track(path):
    p=Path(path);SOURCES[str(p.relative_to(ROOT))]=hashlib.sha256(p.read_bytes()).hexdigest()
def unitydata(path):
    track(path);p=Prefab(path);return p.data(next(iter(p.blocks)))
def matrix(tr):
    x,y,z,w=[tr['m_LocalRotation'][k] for k in 'xyzw'];q=np.array([[1-2*(y*y+z*z),2*(x*y-z*w),2*(x*z+y*w)], [2*(x*y+z*w),1-2*(x*x+z*z),2*(y*z-x*w)], [2*(x*z-y*w),2*(y*z+x*w),1-2*(x*x+y*y)]])
    out=np.eye(4);out[:3,:3]=q@np.diag([tr['m_LocalScale'][k] for k in 'xyz']);out[:3,3]=[tr['m_LocalPosition'][k] for k in 'xyz'];return out

def native_mesh(path):
    v=unitydata(path);vd=v['m_VertexData'];count=vd['m_VertexCount'];chs=vd['m_Channels']
    raw=bytes.fromhex(re.search(r'^    _typelessdata: ([0-9a-fA-F]+)',Path(path).read_text(),re.M)[1]);fmts={0:('f',4),1:('e',2),2:('B',1),3:('b',1),4:('H',2),5:('h',2),6:('B',1),7:('b',1),8:('H',2),9:('h',2),10:('I',4),11:('i',4)}
    streams={}
    for c in chs:
        if c['dimension']:streams[c['stream']]=max(streams.get(c['stream'],0),c['offset']+c['dimension']*fmts[c['format']][1])
    offsets={};offset=0
    for st,stride in sorted(streams.items()):offsets[st]=offset;offset=(offset+stride*count+15)&~15
    def channel(idx):
        if idx>=len(chs) or not chs[idx]['dimension']:return []
        c=chs[idx];fmt,size=fmts[c['format']];dim=c['dimension']
        return [list(struct.unpack_from('<'+fmt*dim,raw,offsets[c['stream']]+i*streams[c['stream']]+c['offset'])) for i in range(count)]
    vertices=channel(0);uv=channel(4);s=Path(path).read_text();hexbuf=re.search(r'^  m_IndexBuffer: ([0-9a-fA-F]+)',s,re.M)[1]
    size=4 if v['m_IndexFormat'] else 2;buf=bytes.fromhex(hexbuf);indices=struct.unpack('<'+('I' if size==4 else 'H')*(len(buf)//size),buf)
    faces=[];mi=[]
    for mat,sub in enumerate(v['m_SubMeshes']):
        assert sub['topology']==0,(path,sub['topology']);start=sub['firstByte']//size;end=start+sub['indexCount']
        for i in range(start,end,3):faces.append(list(indices[i:i+3])[::-1]);mi.append(mat)
    return dict(vertices=vertices,faces=faces,uv=[[uv[i][:2] for i in f] for f in faces] if uv else [],material_indices=mi)

FBX_CACHE={}
def fbx_mesh(path,model):
    if path not in FBX_CACHE:
        track(path);nodes=read_fbx(path);objects=next(x[2] for x in nodes if x[0]=='Objects');connections=next(x[2] for x in nodes if x[0]=='Connections');FBX_CACHE[path]=(objects,connections)
    objects,connections=FBX_CACHE[path];mid=next(v[0] for t,v,c in objects if t=='Model' and v[1].split(b'\0')[0].decode()==model)
    geometries={v[0]:c for t,v,c in objects if t=='Geometry'}
    gid=next(v[1] for t,v,c in connections if v[2]==mid and v[1] in geometries);ch=geometries[gid];fields={t:v for t,v,c in ch}
    vertices=fields['Vertices'][0];vertices=[[-vertices[i]/100,vertices[i+1]/100,vertices[i+2]/100] for i in range(0,len(vertices),3)]
    indices=fields['PolygonVertexIndex'][0];faces=[];polys=[];f=[];inds=[]
    for loop,i in enumerate(indices):
        f.append(-i-1 if i<0 else i);inds.append(loop)
        if i<0:faces.append(f[::-1]);polys.append(inds[::-1]);f=[];inds=[]
    uv=[]
    layer=next((c for t,v,c in ch if t=='LayerElementUV'),None)
    if layer:
        layer={t:v for t,v,c in layer};uvs=layer['UV'][0];mapping=layer['MappingInformationType'][0];refmode=layer['ReferenceInformationType'][0];uvinds=layer.get('UVIndex',[None])[0]
        for face,loops in zip(faces,polys):
            row=[]
            for vertex,loop in zip(face,loops):
                idx=loop if mapping==b'ByPolygonVertex' else vertex
                if refmode==b'IndexToDirect':idx=uvinds[idx]
                row.append(list(uvs[2*idx:2*idx+2]))
            uv.append(row)
    return dict(vertices=vertices,faces=faces,uv=uv,material_indices=[0]*len(faces))

def material(g):
    if not g:return dict(name='Default',color=[.55,.55,.55,1],roughness=.5,metallic=0)
    p=INDEX[g]
    if p.suffix=='.fbx':
        track(p)
        objects=next(x[2] for x in read_fbx(p) if x[0]=='Objects')
        mat=next(x for x in objects if x[0]=='Material' and x[1][1].split(b'\0')[0]==b'Material.001')
        props=next(x[2] for x in mat[2] if x[0]=='Properties70')
        col=next(x[1][-3:] for x in props if x[1][0]==b'DiffuseColor')
        return dict(name='FBX Material.001',color=list(col)+[1],roughness=.5,metallic=0)
    v=unitydata(p);props=v.get('m_SavedProperties',{})
    def values(k):
        val=props.get(k,{})
        return val if isinstance(val,dict) else {k:v for d in val for k,v in d.items()}
    colors=values('m_Colors');floats=values('m_Floats');texs=values('m_TexEnvs');col=colors.get('_Color',dict(r=1,g=1,b=1,a=1))
    out=dict(name=v.get('m_Name',p.stem),color=[col[k] for k in 'rgba'],roughness=max(.15,1-floats.get('_Glossiness',.4)),metallic=floats.get('_Metallic',0))
    tex=texs.get('_MainTex',{}).get('m_Texture',{}).get('guid')
    if tex:
        path=INDEX[tex];track(path);out['texture']=str(path.relative_to(ROOT))
    return out

NAMES={-5495902117074765545:'Cube',4729988176110476404:'Cover',970325530914192494:'HDDSlot.002',1191265918788314024:'HDDSlot.003',5006157043924845488:'HDDSlot.001',1446483524335072940:'HDDSlot',-6375363235782234975:'ATX',4734059676377214840:'CPU',-2253887542714077937:'PSUThin_2000kW',-8007441879807768515:'Switch'}
def scene(source):
    path=COMP/(source+'.prefab');track(path);p=Prefab(path);draws=[]
    if source=='ServerCooler':
        # Unity prefab overrides the FBX root to +90 X and the blade to
        # identity/scale 1 (the source FBX blade was 100x smaller). Apply
        # those authored overrides to raw FBX meshes, not Blender's imported
        # axis-conversion transforms. This matches the actual prefab.
        mats={}
        pi=p.objects(1001)[0][1];mods=pi['m_Modification']['m_Modifications']
        for mod in mods:
            if mod['propertyPath']=='m_Materials.Array.data[0]':mats[mod['target']['fileID']]=material(mod['objectReference'].get('guid'))
        q={axis:next(float(m['value']) for m in mods if m['propertyPath']=='m_LocalRotation.'+axis) for axis in 'xyzw'}
        pose=matrix(dict(m_LocalRotation=q,m_LocalPosition=dict(x=0,y=0,z=0),m_LocalScale=dict(x=1,y=1,z=1)))
        fbx=ROOT/'Assets/Models/ServerFan.fbx';track(fbx);track(ROOT/'Assets/Texture2D/Cooler.png')
        specs={'Blade.001':mats[4226133384550400255],'Fan':mats[-261092084840383615],
               'Metal':dict(name='FBX Cooler',color=[1,1,1,1],roughness=.5,metallic=0,texture='Assets/Texture2D/Cooler.png')}
        for model,spec in specs.items():
            draws.append(dict(name=model,mesh=fbx_mesh(fbx,model),matrix=pose.tolist(),materials=[spec]))
        return dict(draws=draws,source=source)
    trs={v['m_GameObject']['fileID']:(i,v) for i,v in p.objects(4) if 'm_GameObject' in v}
    rootgo,roottr=p.root();cache={}
    def world(t):
        if t in cache:return cache[t]
        v=p.data(t);par=v['m_Father']['fileID'];out=matrix(v)
        if par:out=world(par)@out
        cache[t]=out;return out
    # Product images are centered on the part, not its scene spawn position.
    root_inv=np.linalg.inv(world(roottr));renders={v['m_GameObject']['fileID']:v for i,v in p.objects(23)}
    for _,v in p.objects(33):
        go=v['m_GameObject']['fileID'];m=v['m_Mesh'];renderer=renders[go]
        if not renderer.get('m_Enabled',1):continue
        meshpath=INDEX[m['guid']]
        mesh=native_mesh(meshpath) if meshpath.suffix=='.asset' else fbx_mesh(meshpath,NAMES[m['fileID']])
        draws.append(dict(name=p.data(go).get('m_Name','Mesh'),mesh=mesh,matrix=(root_inv@world(trs[go][0])).tolist(),materials=[material(x.get('guid')) for x in renderer['m_Materials']]))
    return dict(draws=draws,source=source)

if __name__=='__main__':
    manifest=json.loads((ROOT/'tools/server_catalog_manifest.json').read_text());output=ROOT/'tools/server_cover_scenes.json'
    scenes={key:scene(v['source']) for key,v in manifest['covers'].items()}
    output.write_text(json.dumps(dict(scenes=scenes,sources=SOURCES),separators=(',',':'))+'\n')
    print('Exported',len(scenes),'real-model scenes;',len(SOURCES),'source hashes')
