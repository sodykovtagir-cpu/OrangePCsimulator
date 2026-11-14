#!/usr/bin/env python3
"""Patch only the requested controls; retain all authored transforms/styles."""
import re,difflib
from server_assets import ROOT,Prefab,ref,HEADER

def save_patch(p,path):
 old=Prefab(path);pieces=[]
 for i,block in p.blocks.items():
  if i not in old.blocks:pieces.append('\n'.join(line.rstrip()for line in block.splitlines())+'\n');continue
  before=old.blocks[i].splitlines(True);after=block.splitlines(True)
  matcher=difflib.SequenceMatcher(None,[line.rstrip()for line in before],[line.rstrip()for line in after],autojunk=False)
  pieces.append(''.join(''.join(before[a:b]if tag=='equal'else after[c:d])for tag,a,b,c,d in matcher.get_opcodes()))
 text=HEADER+''.join(pieces)
 if text!=path.read_text():path.write_text(text)

def upgrade():
 # Bind only the RawImage supplied by the author.
 f=ROOT/'Assets/Scripts/Assembly-CSharp/PC/Component/OggleResultPrefab.prefab'
 if f.exists():
  p=Prefab(f);app,v=p.mono('OggleResultItem')[0]
  raw=next((i for i,d in p.objects(114)if 'm_Texture'in d and 'm_UVRect'in d),None)
  if raw is not None and 'icon'not in v:p.append_field(app,'icon',ref(raw));save_patch(p,f)
 f=ROOT/'Assets/Resources/apps/SiteEditor.prefab';p=Prefab(f);app,v=p.mono('SiteEditor')[0]
 # Clone just the authored +page button for a Rename command.
 if not any(c.get('m_MethodName')=='RenamePage'for _,d in p.objects(114)for c in d.get('m_OnClick',{}).get('m_PersistentCalls',{}).get('m_Calls',[])):
  source=next(i for i,d in p.objects(114)if any(c.get('m_MethodName')=='AddPage'for c in d.get('m_OnClick',{}).get('m_PersistentCalls',{}).get('m_Calls',[])))
  go=p.data(source)['m_GameObject']['fileID'];tr=next(i for i,d in p.objects(224)if d['m_GameObject']['fileID']==go);parent=p.data(tr)['m_Father']['fileID'];trs=[]
  def walk(i):
   trs.append(i)
   for child in p.data(i).get('m_Children',[]):walk(child['fileID'])
  walk(tr);gos={p.data(i)['m_GameObject']['fileID']for i in trs};ids=[i for i,d in p.objects()if i in gos or d.get('m_GameObject',{}).get('fileID')in gos];mapping={i:p.alloc()for i in ids}
  for i in ids:
   text=re.sub(r'(^--- !u!\d+ &)(-?\d+)',lambda m:m[1]+str(mapping[int(m[2])]),p.blocks[i],count=1,flags=re.M)
   text=re.sub(r'\{fileID: (-?\d+)\}',lambda m:ref(mapping.get(int(m[1]),int(m[1]))),text);p.blocks[mapping[i]]=text
   if p.cls(i)==114 and 'm_Text'in p.data(i):p.set(mapping[i],'m_Text',"'Rename'")
  p.blocks[mapping[source]]=p.blocks[mapping[source]].replace('m_MethodName: AddPage','m_MethodName: RenamePage');p.set(mapping[go],'m_Name','Rename page');p.child(parent,mapping[tr])
  def span(i,a,b):p.set(i,'m_AnchorMin','{x: %s, y: 1}'%a);p.set(i,'m_AnchorMax','{x: %s, y: 1}'%b)
  span(mapping[tr],.84,.93);span(tr,.76,.84)
  pathgo=p.data(v['pagePath']['fileID'])['m_GameObject']['fileID'];pathtr=next(i for i,d in p.objects(224)if d['m_GameObject']['fileID']==pathgo);span(pathtr,.62,.76)
  delete=next(i for i,d in p.objects(114)if any(c.get('m_MethodName')=='DeletePage'for c in d.get('m_OnClick',{}).get('m_PersistentCalls',{}).get('m_Calls',[])));deletego=p.data(delete)['m_GameObject']['fileID'];span(next(i for i,d in p.objects(224)if d['m_GameObject']['fileID']==deletego),.93,1)
  save_patch(p,f)
 for i,d in p.objects(114):
  if str(d.get('m_Text','')).startswith('$100 / 30 real minutes'):
   p.set(i,'m_Text',"'$100 / 30 min. Custom domain +$1000 / 30 min.'")
 save_patch(p,f)
 print('Bound authored RawImage; added Rename page using the existing button style')
if __name__=='__main__':upgrade()
