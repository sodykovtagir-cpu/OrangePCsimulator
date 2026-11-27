#!/usr/bin/env python3
"""Incremental hosting-panel upgrade: domain-kind dropdown and top-up button.

Reuses the authored chrome (dropdown clone of the owned-sites list, button clone
of the publish button) and keeps every existing transform/style untouched. Safe
to run repeatedly: it does nothing once the controls exist.
"""
from pathlib import Path
import re
from server_assets import ROOT,Prefab,ref,read_guid
from unity_ui_emitter import UIEmitter

def upgrade():
 C=ROOT/'Assets/Scripts/Assembly-CSharp/PC/Component/Software'
 target=ROOT/'Assets/Resources/apps/SiteEditor.prefab'
 p=Prefab(target);app,v=p.mono('SiteEditor')[0]
 if v.get('hostDomainKind',{}).get('fileID') and v.get('topUpButton',{}).get('fileID'):
  print('Hosting panel already has the domain-kind dropdown; preserving user edits')
  return
 names={p.data(d['m_GameObject']['fileID'])['m_Name']:i for i,d in p.objects(224)}
 body=names['Site hosting panel']
 def cls(i):return p.cls(i)
 def tr_of(go):return next(i for i,d in p.objects(224)if d['m_GameObject']['fileID']==go)
 class B:
  def __init__(self):self.blocks={}
  def next_id(self):return p.alloc()
  def add(self,text):
   text=text.strip()+'\n';self.blocks[int(re.search(r'&(-?\d+)',text)[1])]=text
 b=B();ui=UIEmitter(b)
 def layout(t,y=0,h=28,x=6,w=-12,amin=(0,1),amax=(1,1),pivot=(0,1)):
  for k,val in [('m_AnchorMin',amin),('m_AnchorMax',amax),('m_Pivot',pivot),('m_AnchoredPosition',(x,-y)),('m_SizeDelta',(w,h))]:p.set(t,k,'{x: %s, y: %s}'%val)
 def clone(source_id,parent):
  source=Prefab(text=p.render())
  go=source.data(source_id)['m_GameObject']['fileID']
  rt=next(i for i,d in source.objects(224)if d['m_GameObject']['fileID']==go)
  transforms=[]
  def walk(t):
   transforms.append(t)
   for ch in source.data(t).get('m_Children',[]):walk(ch['fileID'])
  walk(rt)
  gos={source.data(i)['m_GameObject']['fileID']for i in transforms}
  ids=[i for i,d in source.objects()if i in gos or d.get('m_GameObject',{}).get('fileID')in gos]
  mapping={i:p.alloc()for i in ids}
  for i in ids:
   text=re.sub(r'(^--- !u!\d+ &)(-?\d+)',lambda m:m[1]+str(mapping[int(m[2])]),source.blocks[i],count=1,flags=re.M)
   text=re.sub(r'\{fileID: (-?\d+)\}',lambda m:ref(mapping.get(int(m[1]),int(m[1]))),text)
   p.blocks[mapping[i]]=text
  t=mapping[rt];p.set(t,'m_Father',ref(parent));p.child(parent,t)
  return mapping[source_id],t
 def event(i,method):
  p.set(i,'m_OnClick',f'''    m_PersistentCalls:
      m_Calls:
      - m_Target: {{fileID: {app}}}
        m_TargetAssemblyTypeName: PC.Component.Software.SiteEditor, Assembly-CSharp
        m_MethodName: {method}
        m_Mode: 1
        m_Arguments:
          m_ObjectArgument: {{fileID: 0}}
          m_ObjectArgumentAssemblyTypeName: UnityEngine.Object, UnityEngine
          m_IntArgument: 0
          m_FloatArgument: 0
          m_StringArgument:
          m_BoolArgument: 0
        m_CallState: 2
''')
 # The domain-kind dropdown reuses the authored "Owned sites" dropdown.
 drop=clone(v['hostedSites']['fileID'],body)
 p.set(p.data(drop[0])['m_GameObject']['fileID'],'m_Name','Host domain kind')
 layout(drop[1],98,28,x=330,w=-342)
 p.set(drop[0],'m_Value','0')
 event_data=[{'m_Text':'Стандартный адрес'},{'m_Text':'Свой домен'}]
 # UGUI stores the option list inside the serialized OptionDataList wrapper.
 p.set(drop[0],'m_Options',"    m_Options:\n"+"".join('    - m_Text: %s\n      m_Image: {fileID: 0}\n'%o['m_Text'] for o in event_data))
 # The top-up button reuses the authored publish button and its style.
 publish=next(i for i,d in p.objects(114)if any(c.get('m_MethodName')=='PublishSite'for c in d.get('m_OnClick',{}).get('m_PersistentCalls',{}).get('m_Calls',[])))
 top=clone(publish,body)
 p.set(p.data(top[0])['m_GameObject']['fileID'],'m_Name','Top up hosting')
 # The cloned button carries its own caption child: rename that text, not the parent.
 cap=p.data(next(i for i,d in p.objects(224) if d['m_GameObject']['fileID']==p.data(top[0])['m_GameObject']['fileID']))['m_Children'][0]['fileID']
 capgo=p.data(cap)['m_GameObject']['fileID']
 for i,d in Prefab(text=p.render()).objects(114):
  if d.get('m_GameObject',{}).get('fileID')==capgo and 'm_Text'in d:p.set(i,'m_Text','Пополнить +$100')
 event(top[0],'TopUpHosting')
 layout(top[1],134,28,x=2,w=-4,amin=(.75,1),amax=(1,1))
 for key,value in [('hostDomainKind',drop[0]),('topUpButton',top[0])]:
  if not v.get(key,{}).get('fileID'):p.append_field(app,key,ref(value))
 p.save(target)
 print('Added the domain-kind dropdown (standard/custom/local) and the +$100 top-up button to the hosting panel')

if __name__=='__main__':upgrade()
