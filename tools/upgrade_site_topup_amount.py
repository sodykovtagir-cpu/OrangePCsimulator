#!/usr/bin/env python3
"""Add the arbitrary top-up amount field to the hosting panel.

The amount input reuses the authored domain field, so the chrome stays the
same. The top-up button moves to its own row beside it, and the owned-sites
list/statistics below shift down by one row. Safe to run repeatedly.
"""
from pathlib import Path
import re
from server_assets import ROOT,Prefab,ref,read_guid
from unity_ui_emitter import UIEmitter

def upgrade():
 target=ROOT/'Assets/Resources/apps/SiteEditor.prefab'
 p=Prefab(target);app,v=p.mono('SiteEditor')[0]
 if v.get('topUpAmount',{}).get('fileID'):
  print('Hosting panel already has the top-up amount field; preserving user edits')
  return
 names={p.data(d['m_GameObject']['fileID'])['m_Name']:i for i,d in p.objects(224)}
 body=names['Site hosting panel']
 def layout(t,y=0,h=28,x=6,w=-12,amin=(0,1),amax=(1,1),pivot=(0,1)):
  for k,val in [('m_AnchorMin',amin),('m_AnchorMax',amax),('m_Pivot',pivot),('m_AnchoredPosition',(x,-y)),('m_SizeDelta',(w,h))]:
   p.set(t,k,'{x: %s, y: %s}'%val)
 source=Prefab(text=p.render())
 def clone(source_id,parent):
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
 # Amount field: a clone of the domain input, prefilled with the old $100.
 amount,amount_tr=clone(v['hostURL']['fileID'],body)
 p.set(p.data(amount)['m_GameObject']['fileID'],'m_Name','Top up amount')
 p.set(amount,'m_Text',"'100'")
 layout(amount_tr,170,28,x=2,w=-8,amin=(0,1),amax=(.6,1))
 # The top-up button moves down to its own row next to the amount.
 top_tr=names['Top up hosting']
 layout(top_tr,170,28,x=2,w=-4,amin=(.6,1),amax=(1,1))
 cap=p.data(top_tr)['m_Children'][0]['fileID'];capgo=p.data(cap)['m_GameObject']['fileID']
 for i,d in Prefab(text=p.render()).objects(114):
  if d.get('m_GameObject',{}).get('fileID')==capgo and 'm_Text'in d:p.set(i,'m_Text','Пополнить')
 # Everything below moves down by one row to make space.
 layout(names['Owned sites'],206,28)
 viewport=names['Hosting statistics viewport']
 p.set(viewport,'m_AnchoredPosition','{x: 6, y: -244}')
 p.set(viewport,'m_SizeDelta','{x: -12, y: -252}')
 # The dropdown now offers exactly two address kinds: offline hosting is automatic.
 p.set(v['hostDomainKind']['fileID'],'m_Options',"    m_Options:\n    - m_Text: Стандартный адрес\n      m_Image: {fileID: 0}\n    - m_Text: Свой домен\n      m_Image: {fileID: 0}\n")
 if not v.get('topUpAmount',{}).get('fileID'):p.append_field(app,'topUpAmount',ref(amount))
 p.save(target)
 print('Added the arbitrary top-up amount field and a two-choice address dropdown')

if __name__=='__main__':upgrade()
