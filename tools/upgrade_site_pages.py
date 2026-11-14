#!/usr/bin/env python3
"""Incremental pages/context/hosting-panel authoring; preserve edited chrome."""
import re,hashlib
from server_assets import ROOT,Prefab,ref,read_guid
from unity_ui_emitter import UIEmitter

def upgrade():
 C=ROOT/'Assets/Scripts/Assembly-CSharp/PC/Component/Software'
 for file in [*C.glob('SiteEditor*.cs'),C/'BrowserHosted.cs',*(C/'Sites').glob('*.cs'),ROOT/'Assets/Scripts/Assembly-CSharp/WorkshopSites.cs']:
  if not file.exists():continue
  meta=file.with_suffix(file.suffix+'.meta')
  if not meta.exists():
   guid=hashlib.md5(('orange-sites:'+str(file.relative_to(ROOT))).encode()).hexdigest();meta.write_text(f'fileFormatVersion: 2\nguid: {guid}\nMonoImporter:\n  externalObjects: {{}}\n  serializedVersion: 2\n  defaultReferences: []\n  executionOrder: 0\n  icon: {{instanceID: 0}}\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n')
 target=ROOT/'Assets/Resources/apps/SiteEditor.prefab';p=Prefab(target);app,v=p.mono('SiteEditor')[0]
 def normalize_fonts():
  samples=set()
  for key in ['boldButton','italicButton']:
   go=p.data(v[key]['fileID'])['m_GameObject']['fileID']
   rt=next(d for _,d in p.objects(224)if d['m_GameObject']['fileID']==go)
   for child in rt['m_Children']:samples.add(p.data(child['fileID'])['m_GameObject']['fileID'])
  for i,d in p.objects(114):
   if 'm_FontData'in d and d['m_GameObject']['fileID']not in samples:
    p.blocks[i]=re.sub(r'(    m_FontStyle:) \d+',r'\1 0',p.blocks[i])
 if v.get('pageTabs',{}).get('fileID'):
  print('Pages/hosting panels already authored; preserving user edits');return
 class B:
  def __init__(self):self.chunks=[]
  def next_id(self):return p.alloc()
  def add(self,s):
   s=s.strip()+'\n';self.chunks.append(s);p.blocks[int(re.search(r'&(-?\d+)',s)[1])]=s
 b=B();ui=UIEmitter(b);refs={};font=read_guid(ROOT/'Assets/Resources/SiteFonts/default.ttf')
 def tr(go):return next(i for i,d in p.objects(224)if d['m_GameObject']['fileID']==go)
 names={p.data(d['m_GameObject']['fileID'])['m_Name']:i for i,d in p.objects(224)}
 root=next(i for i,d in p.objects(224)if d['m_Father']['fileID']==0)
 def layout(t,y=0,h=28,x=6,w=-12,amin=(0,1),amax=(1,1),pivot=(0,1)):
  for k,val in [('m_AnchorMin',amin),('m_AnchorMax',amax),('m_Pivot',pivot),('m_AnchoredPosition',(x,-y)),('m_SizeDelta',(w,h))]:p.set(t,k,'{x: %s, y: %s}'%val)
 def panel(name,parent,y=0,h=28,color='0.74, 0.74, 0.74, 1'):
  go,t,img=ui.background(name,parent,0,0,100,h,color,raycast=1);layout(t,y,h,x=0,w=0);return go,t,img
 def label(name,parent,text,y=0,h=20):
  i=ui.text(name,parent,6,y,100,h,text,size=12,color='0.06, 0.06, 0.06, 1',best_fit=1,min_size=9,max_size=12,align=0)
  t=tr(p.data(i)['m_GameObject']['fileID']);layout(t,y,h);return i
 def component(go,source,values):
  i=p.alloc();s=re.sub(r'^--- !u!114 &\d+',f'--- !u!114 &{i}',source,count=1,flags=re.M);p.blocks[i]=s;p.set(i,'m_GameObject',ref(go))
  for k,val in values.items():p.set(i,k,str(val))
  p.blocks[go]=p.blocks[go].replace('  m_Layer:',f'  - component: {{fileID: {i}}}\n  m_Layer:',1);return i
 button_source=p.blocks[v['rowTemplate']['fileID']];input_source=p.blocks[v['fields'][0]['input']['fileID']];scroll_source=next(p.blocks[i]for i,d in p.objects(114)if 'm_Viewport'in d and 'm_Content'in d)
 def event(i,method,key='m_OnClick',mode=1):
  p.set(i,key,f'''    m_PersistentCalls:
      m_Calls:
      - m_Target: {{fileID: {app}}}
        m_TargetAssemblyTypeName: PC.Component.Software.SiteEditor, Assembly-CSharp
        m_MethodName: {method}
        m_Mode: {mode}
        m_Arguments:
          m_ObjectArgument: {{fileID: 0}}
          m_ObjectArgumentAssemblyTypeName: UnityEngine.Object, UnityEngine
          m_IntArgument: 0
          m_FloatArgument: 0
          m_StringArgument:
          m_BoolArgument: 0
        m_CallState: 2
''')
 def button(name,parent,title,method,y=0):
  i,_,t=ui.button(name,parent,6,y,100,28,title,'0.83, 0.83, 0.83, 1',label_size=12,label_color='0.06, 0.06, 0.06, 1');layout(t,y);event(i,method);return i,t
 def input(name,parent,y=0,h=28):
  go,t,img=panel(name,parent,y,h,'1, 1, 1, 1');layout(t,y,h);txt=label(name+' text',t,'',2,h-4)
  i=component(go,input_source,{'m_TargetGraphic':ref(img),'m_TextComponent':ref(txt),'m_Text':"''",'m_LineType':0,'m_ReadOnly':0,'m_CharacterLimit':512});return i,t
 def clone(source_id,parent):
  source=Prefab(text=p.render());go=source.data(source_id)['m_GameObject']['fileID'];rt=next(i for i,d in source.objects(224)if d['m_GameObject']['fileID']==go);ts=[]
  def walk(t):
   ts.append(t)
   for ch in source.data(t).get('m_Children',[]):walk(ch['fileID'])
  walk(rt);gos={source.data(i)['m_GameObject']['fileID']for i in ts};ids=[i for i,d in source.objects()if i in gos or d.get('m_GameObject',{}).get('fileID')in gos];mapping={i:p.alloc()for i in ids}
  for i in ids:
   s=re.sub(r'(^--- !u!\d+ &)(-?\d+)',lambda m:m[1]+str(mapping[int(m[2])]),source.blocks[i],count=1,flags=re.M);s=re.sub(r'\{fileID: (-?\d+)\}',lambda m:ref(mapping.get(int(m[1]),int(m[1]))),s);p.blocks[mapping[i]]=s
  t=mapping[rt];p.set(t,'m_Father',ref(parent));p.child(parent,t);return mapping[source_id],t
 def dropdown(name,parent,y=0):
  i,t=clone(v['commandDropdown']['fileID'],parent);p.set(p.data(i)['m_GameObject']['fileID'],'m_Name',name);layout(t,y);return i,t
 def mask(go):
  source=f'''--- !u!114 &0
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: 0}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: 31a19414c41e5ae4aae2af33fee712f6, type: 3}}
  m_Name:
  m_EditorClassIdentifier:
  m_ShowMaskGraphic: 0
''';component(go,source,{})
 # Page tab strip lives between the original toolbar and workspace.
 _,bar,_=panel('Page tab toolbar',names['EditorBody'],34,30)
 tabgo,tabview,_=panel('Page tab viewport',bar,0,28);layout(tabview,0,28,x=0,w=0,amax=(.61,1));mask(tabgo)
 _,tabs,_=panel('Page tabs',tabview,0,28);layout(tabs,0,28,x=0,w=112,amax=(0,1));refs['pageTabs']=tabs
 component(tabgo,scroll_source,{'m_Viewport':ref(tabview),'m_Content':ref(tabs),'m_Horizontal':1,'m_Vertical':0,'m_HorizontalScrollbar':ref(0),'m_VerticalScrollbar':ref(0)})
 refs['pagePath'],t=input('Page path',bar);layout(t,0,28,x=2,w=-4,amin=(.62,1),amax=(.84,1));p.set(refs['pagePath'],'m_Text',"'/about'")
 for a,z,title,method in [(.84,.92,'+ page','AddPage'),(.92,1,'− page','DeletePage')]:_,t=button(title,bar,title,method);layout(t,0,28,x=2,w=-4,amin=(a,1),amax=(z,1))
 work=names['Workspace'];old=p.data(work);p.set(work,'m_AnchoredPosition','{x: 0, y: -25}');p.set(work,'m_SizeDelta','{x: 0, y: -98}')
 # Background image and viewport controls appended to the existing site section.
 site=tr(v['sitePanel']['fileID']);bottom=p.data(site)['m_SizeDelta']['y'];label('Background image label',site,'Page background image',bottom+4)
 refs['backgroundDropdown'],_=dropdown('Background image',site,bottom+26);event(refs['backgroundDropdown'],'BackgroundChanged','m_OnValueChanged',0)
 button('Import background',site,'Import background image','ImportBackground',bottom+62);button('Browser sizing',site,'Browser size / fixed size','FitBrowserPage',bottom+98);p.set(site,'m_SizeDelta','{x: 0, y: %s}'%(bottom+140))
 # Context overlay dismisses on outside click. No runtime UI builder.
 overlay,ot,img=panel('Hierarchy context overlay',root,0,0,'0, 0, 0, 0');layout(ot,0,0,x=0,w=0,amin=(0,0),amax=(1,1),pivot=(.5,.5));refs['hierarchyMenu']=overlay;p.set(overlay,'m_IsActive','0')
 i=component(overlay,button_source,{'m_TargetGraphic':ref(img)});event(i,'CloseHierarchyMenu')
 _,ct,_=panel('Hierarchy context menu',ot,0,300);layout(ct,0,300,x=0,w=210,amax=(0,1));refs['contextBox']=ct
 refs['contextName'],_=input('Rename value',ct,6)
 for j,(title,method)in enumerate([('Rename','ContextRename'),('Duplicate','ContextDuplicate'),('Delete','ContextDelete'),('Bring to front','LayerFront'),('Forward one layer','LayerForward'),('Backward one layer','LayerBackward'),('Send to back','LayerBack')]):button(title,ct,title,method,42+j*36)
 # Independent hosting / analytics panel (not part of element properties).
 overlay,ht,_=panel('Site hosting overlay',root,0,0,'0, 0, 0, 0.65');layout(ht,0,0,x=0,w=0,amin=(0,0),amax=(1,1),pivot=(.5,.5));refs['hostingPanel']=overlay;p.set(overlay,'m_IsActive','0')
 _,body,_=panel('Site hosting panel',ht,24,-48);layout(body,24,-48,x=24,w=-48,amin=(0,1),amax=(1,1));p.set(body,'m_AnchorMin','{x: 0, y: 0}');p.set(body,'m_AnchoredPosition','{x: 0, y: 0}');p.set(body,'m_Pivot','{x: 0.5, y: 0.5}')
 label('Hosting title',body,'Website hosting / statistics',6,24)
 label('Hosting tariff',body,'$100 / 30 real minutes, including offline. Custom URL +$1000 once. Offline charges are collected on next connection. Publish confirms this tariff. Stop hosting cancels future charges.',34,58)
 refs['hostURL'],_=input('Host URL',body,98)
 for j,(title,method)in enumerate([('Publish / confirm tariff','PublishSite'),('Refresh statistics','RefreshHosting'),('Stop selected site','StopHostedSite'),('Close','CloseHosting')]):_,t=button(title,body,title,method,134);layout(t,134,28,x=2,w=-4,amin=(j/4,1),amax=((j+1)/4,1))
 refs['hostedSites'],_=dropdown('Owned sites',body,170)
 go,viewport,_=panel('Hosting statistics viewport',body,208,-216,'1, 1, 1, 1');layout(viewport,208,-216,x=6,w=-12,amin=(0,1),amax=(1,1));p.set(viewport,'m_AnchorMin','{x: 0, y: 0}');p.set(viewport,'m_Pivot','{x: 0, y: 1}');mask(go)
 refs['hostingStats']=label('Hosting statistics',viewport,'Connecting…',0,2400);stats_tr=tr(p.data(refs['hostingStats'])['m_GameObject']['fileID'])
 component(go,scroll_source,{'m_Viewport':ref(viewport),'m_Content':ref(stats_tr),'m_Horizontal':0,'m_Vertical':1,'m_HorizontalScrollbar':ref(0),'m_VerticalScrollbar':ref(0)})
 # Compact action editor: runtime moves the following authored controls up.
 refs['actionEditor']=names['Action editor'];parent=p.data(names['Action editor'])['m_Father']['fileID'];children=p.data(parent)['m_Children'];after=False;tail=[]
 for child in children:
  if(child['fileID']==names['Action editor']):after=True;continue
  if after:tail.append(child['fileID'])
 # Attach emitter children after native cloned children, preserving both.
 for parent,kids in ui.children.items():
  for kid in kids:p.child(parent,kid)
 for key,value in refs.items():p.append_field(app,key,ref(value))
 p.append_field(app,'actionTail','\n'+'\n'.join('  - '+ref(t)for t in tail))
 # Restore real white fields and an outline around every editable text box.
 for i,d in list(p.objects(114)):
  if 'm_TextComponent'in d:
   graphic=d['m_TargetGraphic']['fileID']
   if graphic in p.blocks and 'm_Color'in p.data(graphic):
    p.set(graphic,'m_Color','{r: 1, g: 1, b: 1, a: 1}')
    father=tr(p.data(graphic)['m_GameObject']['fileID'])
    for name,amin,amax,pos,size in [('Top',(0,1),(1,1),(0,0),(0,1)),('Bottom',(0,0),(1,0),(0,0),(0,1)),('Left',(0,0),(0,1),(0,0),(1,0)),('Right',(1,0),(1,1),(0,0),(1,0))]:
     _,edge,_=ui.background('Input frame '+name,father,0,0,1,1,'0.35, 0.35, 0.35, 1',raycast=0)
     layout(edge,0,size[1],x=0,w=size[0],amin=amin,amax=amax,pivot=(.5,.5))
 for i,d in p.objects(114):
  if 'm_FontData'in d:
   s=p.blocks[i];s=re.sub(r'(    m_Font:) .*',r'\1 {fileID: 12800000, guid: '+font+', type: 3}',s);s=s.replace('    m_RichText: 1','    m_RichText: 0');p.blocks[i]=s
 for parent,kids in ui.children.items():
  existing={x['fileID']for x in p.data(parent).get('m_Children',[])}
  for kid in kids:
   if kid not in existing:p.child(parent,kid)
 normalize_fonts()
 p.save(target);print('Authored page tabs, hierarchy context/layers, background and hosting panels; restored white inputs')
if __name__=='__main__':upgrade()
