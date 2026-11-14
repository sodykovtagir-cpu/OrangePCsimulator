#!/usr/bin/env python3
"""Incrementally upgrade the USER-EDITED SiteEditor prefab. Never rebuild chrome,
canvas, toolbar, hierarchy or existing site-property positions. Idempotent.
"""
import re,hashlib
from server_assets import ROOT,Prefab,ref,read_guid
from unity_ui_emitter import UIEmitter
C=ROOT/'Assets/Scripts/Assembly-CSharp/PC/Component/Software'

def upgrade():
    for script in [C/'SiteEditorInspector.cs',*(C/'Sites').glob('*.cs')]:
        meta=script.with_suffix(script.suffix+'.meta')
        if not meta.exists():
            guid=hashlib.md5(('orange-sites:'+str(script.relative_to(ROOT))).encode()).hexdigest()
            meta.write_text(f'fileFormatVersion: 2\nguid: {guid}\nMonoImporter:\n  externalObjects: {{}}\n  serializedVersion: 2\n  defaultReferences: []\n  executionOrder: 0\n  icon: {{instanceID: 0}}\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n')
    target=ROOT/'Assets/Resources/apps/SiteEditor.prefab';p=Prefab(target);app,v=p.mono('SiteEditor')[0]
    if v.get('pivotDropdown',{}).get('fileID',0):
        print('Site inspector already upgraded; preserving authored prefab');return
    class Builder:
        def __init__(self):self.i=940200000000000000;self.chunks=[]
        def next_id(self):self.i+=1;return self.i
        def add(self,s):self.chunks.append(s.strip())
    b=Builder();ui=UIEmitter(b);changes=[];components=[];events=[];refs={};fields=[];sections=[];args=[]
    def tr_for(go):return next(i for i,d in p.objects(224) if d['m_GameObject']['fileID']==go)
    root=next(i for i,d in p.objects(224) if d['m_Father']['fileID']==0)
    element=tr_for(v['elementPanel']['fileID']);site=tr_for(v['sitePanel']['fileID'])
    def remove_tree(tr):
        d=p.data(tr)
        for child in list(d.get('m_Children',[])):remove_tree(child['fileID'])
        go=d['m_GameObject']['fileID'];father=d['m_Father']['fileID']
        if father in p.blocks:
            kids=[x for x in p.data(father).get('m_Children',[]) if x['fileID']!=tr]
            p.set(father,'m_Children','\n'.join('  - '+ref(x['fileID']) for x in kids)+'\n' if kids else '[]')
        for i in list(p.blocks):
            if i==go or p.data(i).get('m_GameObject',{}).get('fileID')==go:del p.blocks[i]
    # Snapshot native UI templates before replacing the element inspector.
    input_source=p.blocks[v['fields'][0]['input']['fileID']]
    scroll_source=next(p.blocks[i] for i,d in p.objects(114) if 'm_Viewport' in d and 'm_Content' in d)
    dropdown_source=Prefab(text=p.render());dropdown_id=v['commandDropdown']['fileID']
    root_input=v['assetList']['fileID'];root_tr=tr_for(p.data(root_input)['m_GameObject']['fileID']);root_layout=p.data(root_tr).copy()
    for child in list(p.data(element).get('m_Children',[])):remove_tree(child['fileID'])
    old_field_keys={'title','description','keywords','pageWidth','pageHeight','background'}
    fields=[(f['key'],f['input']['fileID'])for f in v['fields']if f['key'] in old_field_keys]
    names={'Root files','Remove filename','Remove filename label','Remove asset'}
    for child in list(p.data(site).get('m_Children',[])):
        tr=child['fileID'];name=p.data(p.data(tr)['m_GameObject']['fileID'])['m_Name']
        if name in names:remove_tree(tr)
    # Remove obsolete serialized references, keeping user's other fields intact.
    for key in ['fields','elementId','assetList','assetToRemove','commandDropdown','commandArguments','argumentHint','actionRoot']:
        p.blocks[app]=re.sub(r'^  '+key+r':[^\n]*\n(?:^(?:    .*|  -.*)\n)*','',p.blocks[app],flags=re.M)
    def layout(tr,y=0,h=28,x=6,w=-12,amin=(0,1),amax=(1,1),pivot=(0,1)):
        for k,value in [('m_AnchorMin',amin),('m_AnchorMax',amax),('m_AnchoredPosition',(x,-y)),('m_SizeDelta',(w,h)),('m_Pivot',pivot)]:changes.append((tr,k,'{x: %s, y: %s}'%value))
    def panel(name,parent,h=28,y=0):
        go,tr,img=ui.background(name,parent,0,0,100,h,'0.74, 0.74, 0.74, 1',raycast=1);layout(tr,y,h,x=0,w=0);return go,tr,img
    def text(name,parent,title,y=0,h=20):
        i=ui.text(name,parent,6,y,100,h,title,color='0.06, 0.06, 0.06, 1',size=12,best_fit=1,min_size=9,max_size=12)
        tr=next(int(re.search(r'&(\d+)',s)[1])for s in reversed(b.chunks)if s.startswith('--- !u!224'));layout(tr,y,h);return i
    def component(go,source,overrides):
        i=b.next_id();s=re.sub(r'^--- !u!114 &\d+',f'--- !u!114 &{i}',source,count=1,flags=re.M)
        q=Prefab(text=s);q.set(i,'m_GameObject',ref(go))
        for key,value in overrides.items():q.set(i,key,str(value))
        b.add(q.blocks[i]);components.append((go,i));return i
    def mono(go,guid,body):
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
    def button(name,parent,title,method,y=0):
        i,_,tr=ui.button(name,parent,6,y,100,28,title,'0.83, 0.83, 0.83, 1',label_size=12,label_color='0.06, 0.06, 0.06, 1');layout(tr,y);events.append((i,method));return i,tr
    def input_field(name,parent,title,y,h=28,key=None):
        text(name+' label',parent,title,y);y+=20
        go,tr,img=panel(name,parent,h,y);layout(tr,y,h)
        label=text(name+' text',tr,'',2,h-4)
        # Text area fills the input without inheriting its absolute y.
        txttr=next(int(re.search(r'&(\d+)',s)[1])for s in reversed(b.chunks)if s.startswith('--- !u!224'))
        layout(txttr,2,h-4,x=5,w=-10)
        i=component(go,input_source,{'m_TargetGraphic':ref(img),'m_TextComponent':ref(label),'m_Text':"''",'m_LineType':2 if h>28 else 0,'m_CharacterLimit':8192,'m_ReadOnly':0})
        if key:fields.append((key,i))
        return i,y+h+8
    def clone_subtree(src,source_id,parent):
        go=src.data(source_id)['m_GameObject']['fileID'];tr=next(i for i,d in src.objects(224) if d['m_GameObject']['fileID']==go)
        ts=[]
        def collect(t):
            ts.append(t)
            for ch in src.data(t).get('m_Children',[]):collect(ch['fileID'])
        collect(tr);gos={src.data(t)['m_GameObject']['fileID']for t in ts}
        ids=[i for i,d in src.objects()if i in gos or d.get('m_GameObject',{}).get('fileID')in gos];mapping={i:b.next_id()for i in ids}
        q=Prefab(text=''.join(src.blocks[i] for i in ids))
        blocks=[]
        for i in ids:
            s=q.blocks[i];s=re.sub(r'(^--- !u!\d+ &)(-?\d+)',lambda m:m[1]+str(mapping[int(m[2])]),s,count=1,flags=re.M)
            s=re.sub(r'\{fileID: (-?\d+)\}',lambda m:ref(mapping.get(int(m[1]),int(m[1]))),s);blocks.append(s)
        q=Prefab(text=''.join(blocks));q.set(mapping[tr],'m_Father',ref(parent))
        for chunk in q.blocks.values():b.add(chunk)
        ui._track(parent,mapping[tr]);return mapping[source_id],mapping[tr],mapping[go]
    def dropdown(name,parent,title,y):
        text(name+' label',parent,title,y);i,tr,go=clone_subtree(dropdown_source,dropdown_id,parent);layout(tr,y+20)
        changes.append((go,'m_Name',name));return i,y+56
    def list_view(name,parent,y,h):
        go,tr,img=panel(name,parent,h,y);layout(tr,y,h)
        mono(go,'31a19414c41e5ae4aae2af33fee712f6','  m_ShowMaskGraphic: 0')
        _,ct,_=panel(name+' content',tr,36)
        component(go,scroll_source,{'m_Viewport':ref(tr),'m_Content':ref(ct),'m_Horizontal':0,'m_Vertical':1,'m_HorizontalScrollbar':ref(0),'m_VerticalScrollbar':ref(0)})
        return ct,tr
    def section(name,capability):
        go,tr,_=panel(name,element);sections.append((capability,go));return tr
    def end_section(tr,y):layout(tr,0,y,x=0,w=0)
    refs['elementId']=text('Element ID',element,'ID',4,32)
    tr=section('Geometry properties','all');y=0
    for key,title in [('name','Name'),('x','Position X'),('y','Position Y'),('width','Width'),('height','Height')]:_,y=input_field(key,tr,title,y,key=key)
    refs['pivotDropdown'],y=dropdown('Pivot anchors',tr,'Pivot / anchors',y);end_section(tr,y)
    tr=section('Custom pivot','custom');y=0
    for key,title in [('pivotX','Pivot X (0..1)'),('pivotY','Pivot Y (0..1)')]:_,y=input_field(key,tr,title,y,key=key)
    end_section(tr,y)
    tr=section('Text properties','text');y=0
    for key,title,h in [('text','Text',60),('textColor','Text color #RRGGBBAA',28),('fontSize','Font size',28)]:
        _,y=input_field(key,tr,title,y,h,key)
        if key=='textColor':button('Pick text color',tr,'Color…','PickTextColor',y);y+=36
    refs['fontDropdown'],y=dropdown('Font selector',tr,'Font',y)
    style_ids=[]
    for n,(title,method,key)in enumerate([('B','ToggleBold','boldButton'),('I','ToggleItalic','italicButton'),('U','ToggleUnderline','underlineButton'),('S','ToggleStrike','strikeButton')]):
        i,t=button('Style '+title,tr,title,method,y);layout(t,y,x=2,w=-4,amin=(n/4,1),amax=((n+1)/4,1));refs[key]=i;style_ids.append((title,i))
    y+=36;end_section(tr,y)
    tr=section('Fill properties','fill');y=0
    _,y=input_field('fill',tr,'Fill #RRGGBBAA',y,key='fill')
    for title,method in [('Color…','PickFillColor'),('Import image','ImportFill'),('Clear image','ClearImage')]:button(title,tr,title,method,y);y+=36
    end_section(tr,y)
    tr=section('Image OnClick','imageAction');button('Enable Image OnClick',tr,'Enable OnClick → Button','EnableImageClick');end_section(tr,36)
    tr=section('OnClick properties','actions');y=0
    text('Actions heading',tr,'OnClick: command | arguments',y);y+=24
    _,editor,_=panel('Action editor',tr,306,y);y+=314
    _,left,_=panel('Command half',editor,306);layout(left,0,306,x=0,w=-3,amax=(.5,1))
    _,right,_=panel('Arguments half',editor,306);layout(right,0,306,x=3,w=-3,amin=(.5,1))
    refs['commandDropdown'],_=dropdown('Command',left,'Command',0);refs['argumentHint']=text('Argument hint',left,'',62,180)
    for n in range(3):
        go,ar,_=panel('Argument '+str(n),right,100,n*102)
        label=text('Argument caption',ar,'',0)
        choice,_=dropdown('Argument choice',ar,'',0)
        inp,_=input_field('Argument value',ar,'',0,44)
        color,ct=button('Argument color',ar,'Color…','PickArgument'+str(n),70)
        args.append((go,label,choice,inp,color))
    for n,(title,method)in enumerate([('Add','AddAction'),('Update','UpdateAction'),('Remove','RemoveAction')]):
        _,t=button(title,tr,title,method,y);layout(t,y,x=2,w=-4,amin=(n/3,1),amax=((n+1)/3,1))
    y+=36;refs['actionRoot'],_=list_view('Action list',tr,y,132);y+=140
    _,y=input_field('onClick',tr,'Advanced Lua (optional)',y,130,key='onClick');end_section(tr,y)
    tr=section('Delete properties','all');button('Delete element',tr,'Delete element','DeleteElement');end_section(tr,36)
    # Keep the user's root-file-list rectangle; replace its contents with rows.
    ar,art=list_view('Site root file rows',site,-root_layout['m_AnchoredPosition']['y'],250);refs['assetRoot']=ar
    # Paint's exact native RGB/HEX picker, including its authored buttons/events.
    paint=Prefab(ROOT/'Assets/Resources/apps/Paint.prefab');picker=paint.mono('ColorPicker')[0][0]
    refs['colorPicker'],pt,pg=clone_subtree(paint,picker,root)
    layout(pt,0,0,x=0,w=0,amin=(0,0),amax=(1,1),pivot=(.5,.5));changes.append((pg,'m_IsActive','0'))
    # Separate row template with extension icon + name + individual delete X.
    go,at,_=panel('Site asset row template',root,32);refs['assetRowTemplate']=go;changes.append((go,'m_IsActive','0'))
    _,icon,im=ui.background('Icon',at,0,0,24,24,'1, 1, 1, 1');layout(icon,4,24,x=2,w=24,amax=(0,1))
    ti=text('Name',at,'file.ext',4,24);nt=next(int(re.search(r'&(\d+)',s)[1])for s in reversed(b.chunks)if s.startswith('--- !u!224'));layout(nt,4,24,x=32,w=-70)
    delete,dt=button('Delete',at,'×',None);layout(dt,2,28,x=-30,w=28,amin=(1,1),amax=(1,1))
    # A picker button is placed beside the existing background HEX input.
    bg=next(f['input']['fileID']for f in v['fields']if f['key']=='background');bgtr=tr_for(p.data(bg)['m_GameObject']['fileID']);bgd=p.data(bgtr)
    _,t=button('Pick background color',site,'…','PickBackgroundColor',-bgd['m_AnchoredPosition']['y']);layout(t,-bgd['m_AnchoredPosition']['y'],28,x=-40,w=32,amin=(1,1),amax=(1,1))
    p.set(bgtr,'m_SizeDelta','{x: -56, y: 28}')
    external={parent:kids for parent,kids in ui.children.items()if parent in p.blocks}
    for parent in external:del ui.children[parent]
    ui.finalize();extra=Prefab(text='\n'.join(b.chunks)+'\n');p.blocks.update(extra.blocks)
    for parent,kids in external.items():
        for kid in kids:p.child(parent,kid)
    for i,key,value in changes:p.set(i,key,value)
    for go,component_id in components:p.blocks[go]=p.blocks[go].replace('  m_Layer:',f'  - component: {{fileID: {component_id}}}\n  m_Layer:',1)
    for i,method in events:
        if method is None:continue
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
    for key,i in refs.items():p.append_field(app,key,ref(i))
    p.append_field(app,'fields','\n'+'\n'.join('  - key: '+key+'\n    input: '+ref(i)for key,i in fields))
    p.append_field(app,'sections','\n'+'\n'.join('  - capability: '+cap+'\n    panel: '+ref(go)for cap,go in sections))
    p.append_field(app,'argumentControls','\n'+'\n'.join('  - root: '+ref(go)+'\n    label: '+ref(label)+'\n    choice: '+ref(choice)+'\n    input: '+ref(inp)+'\n    color: '+ref(color)for go,label,choice,inp,color in args))
    # Fix duplicate original font GUID ambiguity: editor chrome uses the unique
    # site regular font, not Roboto-Medium (several original assets share a GUID).
    regular=read_guid(ROOT/'Assets/Resources/SiteFonts/default.ttf')
    for i,d in p.objects(114):
        if 'm_FontData'in d:
            block=p.blocks[i];block=re.sub(r'(    m_Font:) .*',r'\1 {fileID: 12800000, guid: '+regular+', type: 3}',block)
            block=re.sub(r'    m_FontStyle: \d+','    m_FontStyle: 0',block).replace('    m_RichText: 1','    m_RichText: 0');p.blocks[i]=block
    for title,button_id in style_ids:
        tr=tr_for(p.data(button_id)['m_GameObject']['fileID']);text_go=p.data(p.data(tr)['m_Children'][0]['fileID'])['m_GameObject']['fileID'];label=next(i for i,d in p.objects(114)if d.get('m_GameObject',{}).get('fileID')==text_go and 'm_FontData'in d)
        if title in ('B','I'):p.blocks[label]=p.blocks[label].replace('m_FontStyle: 0','m_FontStyle: '+('1'if title=='B'else'2'))
        else:
            effect=b.next_id();guid=read_guid(C/'Sites/SiteTextDecoration.cs')
            block=f'''--- !u!114 &{effect}
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {text_go}}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {guid}, type: 3}}
  m_Name:
  m_EditorClassIdentifier:
  underline: {1 if title=='U'else 0}
  strike: {1 if title=='S'else 0}
''';p.blocks[effect]=block;p.blocks[text_go]=p.blocks[text_go].replace('  m_Layer:',f'  - component: {{fileID: {effect}}}\n  m_Layer:',1)
    p.save(target);print('Upgraded inspector in place; user toolbar/canvas/hierarchy and site-property layout retained')
if __name__=='__main__':upgrade()
