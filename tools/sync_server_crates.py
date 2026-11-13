#!/usr/bin/env python3
"""Copy artist-authored Compute packaging to every server crate, losslessly.
Preserve each asset GUID and matching local fileIDs, root save identity and
content reference. Compute is the editable master, never a generic PC crate.
"""
from pathlib import Path
import re
from server_assets import COMP, Prefab, read_guid, ref

FOLDER=COMP/'servers'
MASTER=FOLDER/'Crate_ReadyServer_Compute.prefab'

def identities(p):
    """Stable component identity by hierarchy path, class and script GUID."""
    transforms={i:v for i,v in p.objects(4)}
    by_go={v['m_GameObject']['fileID']:i for i,v in transforms.items()}
    def path(go):
        tr=transforms[by_go[go]];parent=tr['m_Father']['fileID']
        return path(transforms[parent]['m_GameObject']['fileID'])+'/'+p.data(go)['m_Name'] if parent else '$'
    result={};counts={}
    for i,v in p.objects():
        go=i if p.cls(i)==1 else v['m_GameObject']['fileID']
        key=(path(go),p.cls(i),v.get('m_Script',{}).get('guid',''))
        ordinal=counts.get(key,0);counts[key]=ordinal+1
        result[i]=key+(ordinal,)
    assert len(set(result.values()))==len(result)
    return result

def sync_crate(path,content=None):
    path=Path(path);source=Prefab(MASTER);dest=Prefab(path)
    source_ids=identities(source);dest_ids={key:i for i,key in identities(dest).items()}
    # All current server crates have the same hierarchy/components. Refuse a
    # silent destructive remap if a new artist-authored node needs handling.
    assert set(source_ids.values())==set(dest_ids),(path,'crate topology differs from master')
    mapping={i:dest_ids[key] for i,key in source_ids.items()}
    previous_root=dest.root()[0]
    box_id,box=dest.mono('ReadyBuildBox')[0]
    content_ref=box['prefab'] if content is None else dict(fileID=Prefab(content).root()[0],guid=read_guid(content),type=3)
    blocks={}
    for i,b in source.blocks.items():
        b=re.sub(r'^(--- !u!\d+ &)(-?\d+)',lambda m:m[1]+str(mapping[int(m[2])]),b,count=1,flags=re.M)
        b=re.sub(r'\{fileID: (-?\d+)\}',lambda m:ref(mapping.get(int(m[1]),int(m[1]))),b)
        blocks[mapping[i]]=b
    dest.blocks={i:blocks[i] for i in dest.blocks};dest._parsed={}
    root,_=dest.root();assert root==previous_root
    dest.set(root,'m_Name',path.stem)
    dest.set(dest.item()[0],'spawnId',path.stem)
    dest.set(box_id,'prefab','{fileID: %(fileID)s, guid: %(guid)s, type: %(type)s}' % content_ref)
    # The server-sized crate fits its payload: debris must collide normally.
    if 'ignoreDebrisCollisions' in dest.data(box_id):dest.set(box_id,'ignoreDebrisCollisions','0')
    else:dest.append_field(box_id,'ignoreDebrisCollisions','0')
    path.write_text(dest.render())
    return read_guid(path),root,str(path.relative_to(COMP.parents[2]))

def main():
    for path in sorted(FOLDER.glob('Crate_*.prefab')):
        if path.stem=='Crate_Server' or path.stem.startswith('Crate_ReadyServer_'):
            sync_crate(path)
            print('Synced',path.stem,'from Compute; content/fileIDs/GUID preserved; debris collisions enabled')

if __name__=='__main__':main()
