"""Offline authoring of the complete native SiteEditor UI. No runtime builder.
Keep every editor control in the prefab, including the inspector's input fields.
"""
import re
from server_assets import ROOT,Prefab,read_guid,ref
from unity_ui_emitter import UIEmitter

C=ROOT/'Assets/Scripts/Assembly-CSharp/PC/Component/Software'

class Builder:
    def __init__(self):self.i=930100000000000000;self.chunks=[]
    def next_id(self):self.i+=1;return self.i
    def add(self,s):self.chunks.append(s.strip())

def populate(p,root,app):
    b=Builder();ui=UIEmitter(b);changes=[];components=[];refs={};fields=[]
    def layout(tr,amin=(0,1),amax=(1,1),pos=(0,0),size=(0,0),pivot=(0,1)):
        def vec(v):return '{x: %s, y: %s}'%v
        for k,v in [('m_AnchorMin',amin),('m_AnchorMax',amax),('m_AnchoredPosition',pos),('m_SizeDelta',size),('m_Pivot',pivot)]:changes.append((tr,k,vec(v)))
    def fill(tr,left=0,bottom=0,right=0,top=0,amin=(0,0),amax=(1,1)):
        layout(tr,amin,amax,((left-right)/2,(bottom-top)/2),(-left-right,-bottom-top),(.5,.5))
    def panel(name,parent,color='0.74, 0.74, 0.74, 1'):
        go,tr,img=ui.background(name,parent,0,0,100,100,color,raycast=1,track=parent!=root);fill(tr);return go,tr,img
    def mono(go,guid,body=''):
        i=b.next_id();b.add(f'''--- !u!114 &{i}
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {go}}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {guid}, type: 3}}
  m_Name:
  m_EditorClassIdentifier:
{body}''');components.append((go,i));return i
    def mask(go):mono(go,'31a19414c41e5ae4aae2af33fee712f6','  m_ShowMaskGraphic: 0')
    def label(name,parent,text,y=0,h=20,size=12):
        i=ui.text(name,parent,8,y,200,h,text,color='0.08, 0.08, 0.08, 1',size=size,best_fit=1,min_size=9,max_size=size)
        # Find its transform in generated blocks (stable names are for authors).
        tr=next(int(re.search(r'&([0-9]+)',s)[1]) for s in reversed(b.chunks) if s.startswith('--- !u!224'))
        layout(tr,pos=(8,-y),size=(-16,h),pivot=(0,1));return i
    def button(name,parent,title,method,y=0,h=28):
        i,img,tr=ui.button(name,parent,8,y,180,h,title,'0.83, 0.83, 0.83, 1',label_size=12,label_color='0.05, 0.05, 0.05, 1')
        layout(tr,pos=(8,-y),size=(-16,h));events.append((i,method));return i,tr
    source=Prefab(ROOT/'Assets/Resources/apps/TextEditor.prefab')
    input_template=next(i for i,v in source.objects(114) if 'm_TextComponent' in v)
    scroll_template=next(i for i,v in source.objects(114) if 'm_Viewport' in v and 'm_Content' in v)
    templates=[]
    def clone_component(template,go,overrides):
        i=b.next_id();s=re.sub(r'^--- !u!114 &\d+',f'--- !u!114 &{i}',source.blocks[template],count=1,flags=re.M)
        q=Prefab(text=s);q.set(i,'m_GameObject',ref(go))
        for key,value in overrides.items():q.set(i,key,str(value))
        b.add(q.blocks[i]);components.append((go,i));templates.append(i);return i
    def input_field(name,parent,key,title,y,h=28,multiline=False,readonly=False):
        if title:label(name+' label',parent,title,y);y+=20
        go,tr,img=panel(name,parent,'1, 1, 1, 1');layout(tr,pos=(8,-y),size=(-16,h))
        text=ui.text(name+' text',tr,5,2,180,h-4,'',color='0.05, 0.05, 0.05, 1',size=12,align=0 if multiline else 3)
        texttr=next(int(re.search(r'&([0-9]+)',s)[1]) for s in reversed(b.chunks) if s.startswith('--- !u!224'))
        fill(texttr,5,2,5,2)
        i=clone_component(input_template,go,{'m_TargetGraphic':ref(img),'m_TextComponent':ref(text),'m_Text':"''",'m_LineType':2 if multiline else 0,'m_CharacterLimit':8192 if multiline else 4096,'m_ReadOnly':1 if readonly else 0})
        if key:fields.append((key,i))
        return i,y+h+10
    events=[]
    body,bt,_=panel('EditorBody',root,'0.94, 0.94, 0.94, 1');fill(bt,6,6,6,42)
    _,bar,_=panel('Toolbar',bt,'0.94, 0.94, 0.94, 1');layout(bar,size=(0,30))
    for index,(name,method) in enumerate([('New','NewProject'),('Open','OpenFile'),('Save','Save'),('Undo','Undo'),('Redo','Redo'),('Preview','TogglePreview'),('Host','Host')]):
        button_id,tr=button(name,bar,name,method)
        layout(tr,amin=(index/7,1),amax=((index+1)/7,1),pos=(0,-1),size=(-4,28),pivot=(.5,1))
        if name in ('Undo','Redo'):refs[name.lower()+'Button']=button_id
    _,workspace,_=panel('Workspace',bt,'0.94, 0.94, 0.94, 1');fill(workspace,0,24,0,38)
    _,left,_=panel('Palette',workspace);fill(left,right=6,amax=(.18,1))
    for y,title,method in [(8,'Button','AddButton'),(44,'Text','AddText'),(80,'Image','AddImage'),(130,'Site settings','SiteSettings'),(166,'Import file','ImportDownload')]:
        button(title,left,title,method,y)
    _,border,_=panel('Canvas border',workspace,'0.12, 0.12, 0.12, 1');fill(border,right=6,amin=(.18,0),amax=(.72,1))
    canvas,ct,_=panel('SiteCanvas',border,'1, 1, 1, 1');fill(ct,2,2,2,2);mask(canvas)
    refs['view']=mono(canvas,read_guid(C/'Sites/SiteCanvas.cs'))
    # The renderer fills this content dynamically; it is the USER'S page, not editor chrome.
    clone_component(scroll_template,canvas,{'m_Content':ref(0),'m_Viewport':ref(ct),'m_Horizontal':0,'m_Vertical':1,'m_HorizontalScrollbar':ref(0),'m_VerticalScrollbar':ref(0)})
    right,rt,_=panel('Inspector',workspace);fill(rt,amin=(.72,0))
    viewport,vt,_=panel('Inspector viewport',rt);fill(vt);mask(viewport)
    _,content,_=panel('Inspector content',vt);layout(content,size=(0,1000));refs['inspector']=content
    clone_component(scroll_template,right,{'m_Content':ref(content),'m_Viewport':ref(vt),'m_Horizontal':0,'m_Vertical':1,'m_HorizontalScrollbar':ref(0),'m_VerticalScrollbar':ref(0)})
    site,st,_=panel('Site properties',content);layout(st,size=(0,1010));refs['sitePanel']=site
    y=8
    for key,title,h in [('title','Title',28),('description','Description',60),('keywords','Keywords',28),('pageWidth','Page width',28),('pageHeight','Page height',28),('background','Background #RRGGBB',28)]:
        _,y=input_field(key,st,key,title,y,h,h>28)
    button('Import icon',st,'Import site icon','ImportIcon',y);y+=36
    refs['iconInfo']=label('Icon filename',st,'Icon:',y,32);y+=40
    label('Root heading',st,'Site root files',y);y+=20
    refs['assetList'],y=input_field('Root files',st,None,'',y,150,True,True)
    refs['assetToRemove'],y=input_field('Remove filename',st,None,'File to remove',y)
    button('Remove asset',st,'Remove file','RemoveAsset',y);y+=40
    label('Help',st,'Hold text to edit. Drag elements on the canvas.',y,65);y+=73
    layout(st,size=(0,y))
    element,et,_=panel('Element properties',content);refs['elementPanel']=element
    refs['elementId']=label('Element ID',et,'ID:',8,34);y=46
    for key,title in [('name','Name'),('x','Position X'),('y','Position Y'),('width','Width'),('height','Height'),('pivotX','Pivot X (0..1)'),('pivotY','Pivot Y (0..1)')]:
        _,y=input_field(key,et,key,title,y)
    button('Pivot top left',et,'Pivot: top left','PivotTopLeft',y);y+=36
    button('Pivot center',et,'Pivot: center','PivotCenter',y);y+=36
    for key,title,h in [('text','Text',60),('textColor','Text color #RRGGBB',28),('fontSize','Text size',28),('font','Font',28)]:
        _,y=input_field(key,et,key,title,y,h,h>28)
    for title,method in [('Default','FontDefault'),('Mono','FontMono'),('Medium','FontMedium')]:
        button('Font '+title,et,'Font: '+title,method,y);y+=36
    _,y=input_field('fill',et,'fill','Fill #RRGGBB / #RRGGBBAA',y)
    button('Import fill',et,'Import fill image','ImportFill',y);y+=36
    button('Clear fill',et,'Clear image','ClearImage',y);y+=36
    _,y=input_field('onClick',et,'onClick','OnClick - Lua (button only)',y,130,True)
    label('Lua help',et,'RedirectURL("www.modforge.com")\nStartDownload("site/myapp.exe")',y,70,size=11);y+=78
    button('Delete element',et,'Delete element','DeleteElement',y);y+=40
    layout(et,size=(0,y))
    preview,pt,_=panel('Preview properties',content);layout(pt,size=(0,130));refs['previewPanel']=preview
    label('Preview help',pt,'Preview: Lua actions are shown in the status bar. Files are not saved or run.',8,110)
    refs['status']=label('Status',bt,'Ready',0,20,size=11)
    statustr=next(int(re.search(r'&([0-9]+)',s)[1]) for s in reversed(b.chunks) if s.startswith('--- !u!224'))
    layout(statustr,amin=(0,0),amax=(1,0),pos=(0,0),size=(-8,20),pivot=(.5,0))
    ui.finalize()
    extra=Prefab(text='\n'.join(b.chunks)+'\n');p.blocks.update(extra.blocks);p.child(root,bt)
    for tr,key,value in changes:p.set(tr,key,value)
    for go,i in components:
        # Add each component to its actual GameObject; preserve emitter components.
        p.blocks[go]=p.blocks[go].replace('  m_Layer:',f'  - component: {{fileID: {i}}}\n  m_Layer:',1)
    for i,method in events:
        p.blocks[i]=p.blocks[i].replace('      m_Calls: []',f'''      m_Calls:
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
        m_CallState: 2''')
    for key,i in refs.items():p.append_field(app,key,ref(i))
    p.append_field(app,'fields','\n'+'\n'.join('  - key: '+key+'\n    input: '+ref(i) for key,i in fields))
    # Content panels are exclusive. Their controls remain fully authored.
    p.set(element,'m_IsActive','0');p.set(preview,'m_IsActive','0')
    # No rich-text interpretation in user-editable labels or InputField text.
    for i,v in p.objects(114):
        if 'm_FontData' in v:p.blocks[i]=p.blocks[i].replace('    m_RichText: 1','    m_RichText: 0')
    return refs,fields
