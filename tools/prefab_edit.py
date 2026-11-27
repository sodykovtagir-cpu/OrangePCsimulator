#!/usr/bin/env python3
"""Inspect/edit a single non-nested Unity YAML prefab by fileID.

Examples:
  python3 tools/prefab_edit.py inspect 'Assets/Resources/components/Server.prefab'
  python3 tools/prefab_edit.py set PREFAB FILEID m_Enabled 0 --write
  python3 tools/prefab_edit.py remove-collider PREFAB FILEID --write
Default is dry run. References to removed colliders must be absent, apart from
its owner's component list. Unsupported prefab instances are rejected.
"""
import argparse, re, sys
from pathlib import Path
from unity_asset_tool import UnityDoc

REF = re.compile(r'\{fileID: (-?\d+)\}')
def main():
    p=argparse.ArgumentParser(description=__doc__)
    sub=p.add_subparsers(dest='cmd',required=True)
    for name in ('inspect','set','remove-collider'):
        a=sub.add_parser(name);a.add_argument('prefab',type=Path)
        if name!='inspect': a.add_argument('fileid',type=int);a.add_argument('--write',action='store_true')
        if name=='set':a.add_argument('field');a.add_argument('value')
    a=p.parse_args();d=UnityDoc.load(str(a.prefab));ids=[x.file_id for x in d.objects]
    if len(ids)!=len(set(ids)): raise ValueError('duplicate fileIDs')
    if any(x.class_id==1001 or x.stripped for x in d.objects): raise ValueError('nested prefab instances are not supported')
    byid={o.file_id:o for o in d.objects}
    if a.cmd=='inspect':
        for o in d.objects:
            if o.class_id in (1,54,64,65):
                print(o.file_id,o.type_name,'name='+str(o.name),'owner='+str(o.get('m_GameObject')), 'trigger='+str(o.get('m_IsTrigger')), 'size='+str(o.get('m_Size')), 'center='+str(o.get('m_Center')))
        return
    if a.fileid not in byid: raise ValueError('fileID not found')
    obj=byid[a.fileid]
    if a.cmd=='set':
        if '\n' in a.value or not re.fullmatch(r'[A-Za-z_][A-Za-z0-9_]*',a.field): raise ValueError('invalid field/value')
        if len(re.findall(r'^  '+re.escape(a.field)+r':',obj.body,re.M))!=1: raise ValueError('field must occur once at top level')
        obj.body=re.sub(r'^(  '+re.escape(a.field)+r':)[ \t]*.*$', lambda m: m.group(1)+' '+a.value, obj.body, count=1, flags=re.M)
    else:
        if obj.class_id not in (64,65,135,136): raise ValueError('not a collider')
        match=REF.fullmatch(obj.get('m_GameObject') or '')
        owner=byid.get(int(match.group(1))) if match else None
        if not owner or owner.class_id!=1: raise ValueError('collider owner missing')
        line=f'  - component: {{fileID: {a.fileid}}}\n'
        if owner.body.count(line)!=1: raise ValueError('owner component list is inconsistent')
        for other in d.objects:
            if other is obj or other is owner: continue
            if a.fileid in (int(n) for n in REF.findall(other.body)):
                raise ValueError(f'collider referenced by fileID {other.file_id}')
        owner.body=owner.body.replace(line,'',1);d.objects.remove(obj)
    text=d.render()
    if a.write:
        tmp=a.prefab.with_name(a.prefab.name+'.tmp');tmp.write_text(text,encoding='utf-8');tmp.replace(a.prefab)
        print('updated',a.prefab)
    else: print('dry run; pass --write to apply')
if __name__=='__main__':
    try: main()
    except (ValueError, OSError) as e: print('error:',e,file=sys.stderr);sys.exit(1)
