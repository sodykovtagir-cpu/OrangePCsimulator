#!/usr/bin/env python3
"""Serialized editor hierarchy/buttons/fields + actual C# fitting behaviour.
Not a rendered Unity test; UI/PCOS declarations are doubles.
"""
from pathlib import Path
import re,subprocess,tempfile
from server_assets import ROOT,Prefab
C=ROOT/'Assets/Scripts/Assembly-CSharp/PC/Component/Software'
p=Prefab(ROOT/'Assets/Resources/apps/SiteEditor.prefab');app,v=p.mono('SiteEditor')[0]
transforms={i:d for i,d in p.objects(224)}
root=next(d for d in transforms.values() if d['m_Father']['fileID']==0)
assert root['m_SizeDelta']==dict(x=900,y=560)
assert len(v['fields'])==19 and len({f['key'] for f in v['fields']})==19
for f in v['fields']:
    field=p.data(f['input']['fileID']);assert field['m_Script']['guid']=='d199490a83bb2b844b9695cbf13b01ef'
    assert field['m_TextComponent']['fileID'] in p.blocks
    text=p.data(field['m_TextComponent']['fileID']);assert text['m_FontData']['m_RichText']==0
methods=set(re.findall(r'public void (\w+)\(', (C/'SiteEditor.cs').read_text()))|{'Close','Maximize','Minimize'}
clicks=[]
for _,d in p.objects(114):
    for event in d.get('m_OnClick',{}).get('m_PersistentCalls',{}).get('m_Calls',[]):
        assert event['m_Target']['fileID']==app
        assert event['m_MethodName'] in methods,event
        clicks.append(event['m_MethodName'])
for required in ['NewProject','OpenFile','Save','Undo','Redo','TogglePreview','Host','AddButton','AddText','AddImage','SiteSettings','ImportDownload','RemoveAsset','PivotCenter','PivotTopLeft','DeleteElement','ImportIcon','ImportFill','ClearImage','FontDefault','FontMono','FontMedium']:
    assert required in clicks,required
# Controls are authored INSIDE one body/workspace with margins for toolbar/status.
names={p.data(t['m_GameObject']['fileID'])['m_Name']:(i,t) for i,t in transforms.items()}
body=names['EditorBody'][1]
assert body['m_AnchorMin']==dict(x=0,y=0) and body['m_AnchorMax']==dict(x=1,y=1)
# offset top=-42,bottom=6 => centered position -18, height delta -48.
assert body['m_SizeDelta']==dict(x=-12,y=-48) and body['m_AnchoredPosition']==dict(x=0,y=-18)
for panel in ['Toolbar','Workspace','Status','Palette','Inspector','Inspector viewport','Site properties','Element properties','Preview properties','SiteCanvas']:
    assert panel in names,panel
assert p.data(v['elementPanel']['fileID'])['m_IsActive']==0
assert p.data(v['previewPanel']['fileID'])['m_IsActive']==0
# At least one real viewport Mask for inspector and one for document canvas.
assert sum(1 for _,d in p.objects(114) if d.get('m_Script',{}).get('guid')=='31a19414c41e5ae4aae2af33fee712f6')==2
cs=r'''
using System;using System.Reflection;using PC.Component.Software;using PC.Component.Software.Sites;using UnityEngine;
class Test {
 static int count;static void Check(bool b){count++;if(!b)throw new Exception("fit check "+count);}
 static void Invoke(SiteEditor e,string name){typeof(SiteEditor).GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(e,null);}
 static void Main(){
 foreach(float w in new[]{320f,480f,640f,800f,960f,1024f,1280f,1920f})foreach(float h in new[]{240f,360f,450f,540f,720f,1080f})foreach(float taskbar in new[]{0f,32f,48f,72f}){
  var size=SiteWindowLayout.FittedSize(w,h,taskbar);float y=SiteWindowLayout.BottomInset(h,taskbar)*.5f;
  Check(size.x>0&&size.x<=900&&size.x<=w);Check(size.y>0&&size.y<=560);
  Check(y+size.y/2<=h/2);Check(y-size.y/2>=-h/2+taskbar);
 }
 var parent=new RectTransform{rect=new Rect(0,0,640,360)};var window=new RectTransform{parent=parent};
 var go=new GameObject("editor");go.transform=window;window.gameObject=go;
 var editor=new SiteEditor{gameObject=go};Invoke(editor,"FitWindow");Check(window.sizeDelta.x==624&&window.sizeDelta.y==296);Check(window.anchoredPosition.y==24);
 window.anchoredPosition=new Vector2(12,30);Invoke(editor,"LateUpdate");Check(window.anchoredPosition.x==12&&window.anchoredPosition.y==30);
 parent.rect=new Rect(0,0,960,540);Invoke(editor,"LateUpdate");Check(window.sizeDelta.x==900&&window.sizeDelta.y==476);Check(window.anchoredPosition.y==24);
 editor.Maximize();var keep=window.sizeDelta;parent.rect=new Rect(0,0,800,450);Invoke(editor,"LateUpdate");Check(window.sizeDelta.x==keep.x);
 editor.Maximize();Check(window.sizeDelta.x==784&&window.sizeDelta.y==386);
 Console.WriteLine("PASS: "+count+" actual window-fit checks; small/large PCOS hosts, taskbar exclusion, resolution changes, no per-frame recentering, restore after maximize");
 }
}
'''
with tempfile.TemporaryDirectory() as td:
    path=Path(td);(path/'test.cs').write_text(cs)
    subprocess.run(['mcs','-r:System.Web.Extensions','-out:'+str(path/'test.exe'),str(path/'test.cs'),str(ROOT/'tools/tests/site_core_stubs.cs'),str(ROOT/'tools/tests/site_ui_stubs.cs'),str(C/'SiteEditor.cs'),str(C/'Lua/PcosLua.cs'),*[str(x) for x in (C/'Sites').glob('*.cs')]],check=True)
    subprocess.run(['mono',str(path/'test.exe')],check=True)
print('PASS: serialized inspector inputs, buttons/events, panel hierarchy/margins, clipping masks and exclusive inspector panels')
