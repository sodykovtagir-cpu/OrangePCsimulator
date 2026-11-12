"""Small lossless Unity YAML editor used by the server catalog tooling."""
from pathlib import Path
import hashlib, re, yaml
ROOT=Path(__file__).resolve().parents[1]
COMP=ROOT/'Assets/Resources/components'
HEADER='%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n'

def guid(key):return hashlib.md5(('orange-server-catalog-v1:'+key).encode()).hexdigest()
def read_guid(path):return re.search(r'^guid: (\w+)',Path(str(path)+'.meta').read_text(),re.M)[1]
def meta(path,kind='PrefabImporter'):
    p=Path(str(path)+'.meta')
    if p.exists():return read_guid(path)
    g=guid(str(Path(path).relative_to(ROOT)))
    folder='folderAsset: yes\n' if Path(path).is_dir() else ''
    p.write_text(f'fileFormatVersion: 2\nguid: {g}\n{folder}{kind}:\n  externalObjects: {{}}\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n')
    return g

def ref(i):return '{fileID: '+str(i)+'}'
class Prefab:
    def __init__(self,path=None,text=None):
        self.path=Path(path) if path else None
        s=text if text is not None else self.path.read_text()
        self.blocks={int(m[2]):m[0] for m in re.finditer(r'^--- !u!(\d+) &(-?\d+)(?: stripped)?\n.*?(?=^--- !u!|\Z)',s,re.M|re.S)}
        self.next=920000000000000000
        self._parsed={}
    def data(self,i):
        b=self.blocks[i]
        cached=self._parsed.get(i)
        if cached is None or cached[0]!=b:
            cached=(b,next(iter(yaml.safe_load(b.split('\n',1)[1]).values())))
            self._parsed[i]=cached
        return cached[1]
    def cls(self,i):return int(re.match(r'--- !u!(\d+)',self.blocks[i])[1])
    def objects(self,cls=None):return [(i,self.data(i)) for i in self.blocks if cls is None or self.cls(i)==cls]
    def alloc(self):
        self.next+=1
        while self.next in self.blocks:self.next+=1
        return self.next
    def set(self,i,key,value):
        pat=r'^  '+re.escape(key)+r':[^\n]*\n(?:^(?:    .*|  -.*)\n)*'
        body='  '+key+':'+ ('\n'+value if value.startswith('    ') or value.startswith('  -') else ' '+value+'\n')
        self.blocks[i],count=re.subn(pat,lambda m:body,self.blocks[i],count=1,flags=re.M)
        assert count,(self.path,i,key)
    def append_field(self,i,key,value):self.blocks[i]+='  '+key+': '+value+'\n'
    def child(self,parent,tr):
        b=self.blocks[parent]
        if '  m_Children: []' in b:b=b.replace('  m_Children: []','  m_Children:\n  - '+ref(tr))
        else:b=b.replace('  m_Father:','  - '+ref(tr)+'\n  m_Father:',1)
        self.blocks[parent]=b
    def mono(self,script):
        g=read_guid(next((ROOT/'Assets/Scripts').rglob(script+'.cs')))
        return [(i,v) for i,v in self.objects(114) if v.get('m_Script',{}).get('guid')==g]
    def root(self):
        roots=[(i,v['m_GameObject']['fileID']) for i,v in self.objects(4) if v.get('m_Father',{}).get('fileID')==0]
        if roots:return roots[0][1],roots[0][0]
        # Authored model variants: a stripped root GameObject with added Item.
        go=next(v['m_GameObject']['fileID'] for i,v in self.objects(114) if 'spawnId' in v)
        tr=next((i for i,v in self.objects(4) if 'stripped\n' in self.blocks[i]),None)
        return go,tr
    def item(self):
        root,_=self.root()
        return next((i,v) for i,v in self.objects(114) if 'spawnId' in v and v['m_GameObject']['fileID']==root)
    def render(self):return HEADER+''.join(self.blocks.values())
    def save(self,path=None):
        p=Path(path or self.path);p.parent.mkdir(parents=True,exist_ok=True)
        p.write_text('\n'.join(s.rstrip() for s in self.render().splitlines())+'\n')
    def clone_into(self,source,parent):
        sub=Prefab(source);mapping={i:self.alloc() for i in sub.blocks}
        for i,b in sub.blocks.items():
            b=re.sub(r'(&)(-?\d+)(?= stripped\n|\n)',lambda m:m[1]+str(mapping[int(m[2])]),b,count=1)
            b=re.sub(r'\{fileID: (-?\d+)\}',lambda m:ref(mapping.get(int(m[1]),int(m[1]))),b)
            self.blocks[mapping[i]]=b
        go,tr=sub.root();item,iv=sub.item();go=mapping[go];item=mapping[item]
        if tr is not None:
            tr=mapping[tr];self.set(tr,'m_Father',ref(parent));self.set(tr,'m_LocalPosition','{x: 0, y: 0, z: 0}');self.set(tr,'m_LocalRotation','{x: 0, y: 0, z: 0, w: 1}')
        else:
            pi,piData=next(iter(sub.objects(1001)));pid=mapping[pi]
            b=self.blocks[pid].replace('m_TransformParent: {fileID: 0}','m_TransformParent: '+ref(parent),1)
            # Rotation/position are relative to the authored slot, as in TryAttach.
            for axis in 'xyz':
                b=re.sub(r'(propertyPath: m_LocalPosition\.'+axis+r'\n      value:) [^\n]+',r'\g<1> 0',b,count=1)
            for axis in 'xyzw':
                b=re.sub(r'(propertyPath: m_LocalRotation\.'+axis+r'\n      value:) [^\n]+',r'\g<1> '+('1' if axis=='w' else '0'),b,count=1)
            self.blocks[pid]=b
            srcguid=piData['m_SourcePrefab']['guid']
            sourceTR=next(x['target']['fileID'] for x in piData['m_Modification']['m_Modifications'] if x['propertyPath']=='m_LocalPosition.x')
            tr=self.alloc();self.blocks[tr]=f'--- !u!4 &{tr} stripped\nTransform:\n  m_CorrespondingSourceObject: {{fileID: {sourceTR}, guid: {srcguid}, type: 3}}\n  m_PrefabInstance: '+ref(pid)+'\n  m_PrefabAsset: {fileID: 0}\n'
        self.child(parent,tr)
        # These components live in the prefab before runtime. No spawning.
        return item,mapping,tr
