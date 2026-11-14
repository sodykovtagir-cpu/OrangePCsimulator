#!/usr/bin/env python3
"""Inspector data-binding tests with API doubles and prefab preservation checks.
Not a rendered Unity or Play Mode test.
"""
from pathlib import Path
import hashlib,importlib.util,subprocess,tempfile
from server_assets import ROOT,Prefab,read_guid
C=ROOT/'Assets/Scripts/Assembly-CSharp/PC/Component/Software'
p=Prefab(ROOT/'Assets/Resources/apps/SiteEditor.prefab');app,v=p.mono('SiteEditor')[0]
assert len(v['argumentControls'])==3
assert {s['capability']for s in v['sections']}=={'all','custom','text','fill','actions','imageAction'}
for row in v['argumentControls']:
    for key in ['root','label','choice','input','color']:assert row[key]['fileID'] in p.blocks
    assert p.data(row['choice']['fileID'])['m_Template']['fileID'] in p.blocks
for key in ['pivotDropdown','fontDropdown','boldButton','italicButton','underlineButton','strikeButton','assetRoot','assetRowTemplate','colorPicker']:assert v[key]['fileID'] in p.blocks
# All ordinary editor text uses one unambiguous regular-font GUID. Bold/Italic
# are intentional ONLY on their own style-button glyph samples.
regular=read_guid(ROOT/'Assets/Resources/SiteFonts/default.ttf')
style_gos=set()
for key in ['boldButton','italicButton']:
    go=p.data(v[key]['fileID'])['m_GameObject']['fileID'];tr=next(d for _,d in p.objects(224)if d['m_GameObject']['fileID']==go)
    style_gos.add(p.data(tr['m_Children'][0]['fileID'])['m_GameObject']['fileID'])
for _,d in p.objects(114):
    if 'm_FontData' in d:
        assert d['m_FontData']['m_Font']['guid']==regular
        if d['m_GameObject']['fileID']not in style_gos:assert d['m_FontData']['m_FontStyle']==0
        assert d['m_FontData']['m_RichText']==0
picker=p.data(v['colorPicker']['fileID']);assert picker['m_Script']['guid']==read_guid(ROOT/'Assets/Scripts/Assembly-CSharp/ColorPicker.cs')
for key in ['redSlider','greenSlider','blueSlider','hexInput']:assert picker[key]['fileID'] in p.blocks
# Re-run the INCREMENTAL upgrader against the user's latest edited revision (ba5ecb4) in an
# isolated temp folder. This verifies determinism and preserved surviving rects.
original=subprocess.run(['git','show','ba5ecb4:Assets/Resources/apps/SiteEditor.prefab'],cwd=ROOT,text=True,capture_output=True)
if original.returncode==0:
    import upgrade_site_inspector as upgrade
    with tempfile.TemporaryDirectory() as td:
        base=Path(td);file=base/'Assets/Resources/apps/SiteEditor.prefab';file.parent.mkdir(parents=True);file.write_text(original.stdout)
        for rel in ['Assets/Resources/apps/Paint.prefab','Assets/Resources/SiteFonts/default.ttf.meta']:
            dest=base/rel;dest.parent.mkdir(parents=True,exist_ok=True);dest.write_bytes((ROOT/rel).read_bytes())
        old_root=upgrade.ROOT;upgrade.ROOT=base
        try:upgrade.upgrade()
        finally:upgrade.ROOT=old_root
        import upgrade_site_pages as pages_upgrade
        old_root=pages_upgrade.ROOT;pages_upgrade.ROOT=base
        try:pages_upgrade.upgrade()
        finally:pages_upgrade.ROOT=old_root
        generated=Prefab(file);assert generated.render()==p.render(),'incremental output differs from checked-in prefab'
    old=Prefab(text=original.stdout);count=0
    background=next(f['input']['fileID']for f in old.mono('SiteEditor')[0][1]['fields']if f['key']=='background')
    bg_go=old.data(background)['m_GameObject']['fileID']
    for i,d in old.objects(224):
        if i not in p.blocks:continue
        new=p.data(i)
        for key in ['m_AnchorMin','m_AnchorMax','m_AnchoredPosition','m_Pivot','m_LocalScale','m_LocalPosition','m_LocalRotation','m_SizeDelta']:
            name=old.data(d['m_GameObject']['fileID'])['m_Name']
            if name=='Workspace'and key in ['m_AnchoredPosition','m_SizeDelta']:continue # room for tabs
            if name=='Site properties'and key=='m_SizeDelta':continue # new background controls
            if d['m_GameObject']['fileID']==bg_go and key=='m_SizeDelta':continue
            assert new[key]==d[key],(i,key);count+=1
    print('PASS:',count,'surviving user-authored rect properties retained; incremental upgrade reproduces prefab exactly')
else:print('NOTE: user revision absent from shallow git history; preservation baseline comparison skipped')
cs=r'''
using System;using System.Collections.Generic;using System.Reflection;using UnityEngine;using UnityEngine.UI;using PC.Component.Software;using PC.Component.Software.Sites;
class Test {
 static int count;static void Check(bool b){count++;if(!b)throw new Exception("inspector binding "+count);}
 static void Set(SiteEditor e,string k,object v){typeof(SiteEditor).GetField(k,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(e,v);}
 static object Call(SiteEditor e,string n,params object[] a){return typeof(SiteEditor).GetMethod(n,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(e,a);}
 static GameObject Go(){var g=new GameObject("test");var t=new RectTransform{gameObject=g,sizeDelta=new Vector2(100,100)};g.transform=t;return g;}
 static void Main(){
 var e=new SiteEditor();var d=new SiteDocument();d.elements.Add(new SiteElement{id="image",kind="image",name="Background"});d.elements.Add(new SiteElement{id="button",kind="button",name="Download"});d.elements.Add(new SiteElement{id="text",kind="text",name="Title"});d.assets.Add(SiteEditing.ImportFile(d,"Program name.exe",""));var history=new SiteHistory(d);Set(e,"project",history);Set(e,"selected","button");
 var command=new Dropdown();Set(e,"commandDropdown",command);var controls=new SiteEditor.ArgumentControl[3];
 for(int i=0;i<3;i++)controls[i]=new SiteEditor.ArgumentControl{root=Go(),label=new Text(),choice=new Dropdown{gameObject=Go()},input=new InputField{gameObject=Go()},color=new Button{gameObject=Go()}};
 Set(e,"argumentControls",controls);
 command.value=Array.FindIndex(SiteCommands.All,c=>c.Name=="SetText");Call(e,"ConfigureArguments",new List<string>{"button","New text"});
 Check(controls[0].choice.gameObject.activeSelf&&!controls[0].input.gameObject.activeSelf);Check(controls[1].input.text=="New text"&&!controls[1].choice.gameObject.activeSelf);Check(!controls[2].root.activeSelf);
 var action=(SiteAction)Call(e,"ReadAction");Check(action.command=="SetText"&&action.arguments[0]=="button"&&action.arguments[1]=="New text");
 controls[1].input.text="Edited text";action=(SiteAction)Call(e,"ReadAction");Check(action.arguments[1]=="Edited text");
 Call(e,"ConfigureArguments",new List<string>{"image","invalid target"});bool rejected=false;try{Call(e,"ReadAction");}catch(TargetInvocationException ex){rejected=ex.InnerException is ArgumentException;}Check(rejected);
 command.value=Array.FindIndex(SiteCommands.All,c=>c.Name=="StartDownload");Call(e,"ConfigureArguments",new List<string>{"Program name.exe"});action=(SiteAction)Call(e,"ReadAction");Check(action.arguments[0]=="Program name.exe"&&controls[0].choice.gameObject.activeSelf);
 history.Change(doc=>doc.assets.Clear());Call(e,"ConfigureArguments",new List<string>{"Program name.exe"});rejected=false;try{Call(e,"ReadAction");}catch(TargetInvocationException ex){rejected=ex.InnerException is ArgumentException;}Check(rejected);
 command.value=Array.FindIndex(SiteCommands.All,c=>c.Name=="SetFillColor");Call(e,"ConfigureArguments",new List<string>{"image","#AABBCC"});Check(controls[1].color.gameObject.activeSelf);Check(((SiteAction)Call(e,"ReadAction")).arguments[0]=="image");
 command.value=Array.FindIndex(SiteCommands.All,c=>c.Name=="GetTitle");Call(e,"ConfigureArguments",(object)null);Check(!controls[0].root.activeSelf&&!controls[1].root.activeSelf&&!controls[2].root.activeSelf);Check(((SiteAction)Call(e,"ReadAction")).arguments.Count==0);
 Set(e,"selected","image");e.EnableImageClick();Check(history.Document.Find("image").kind=="button"&&history.Document.Find("image").text=="");e.ToggleBold();e.ToggleItalic();e.ToggleUnderline();e.ToggleStrike();var image=history.Document.Find("image");Check(image.bold&&image.italic&&image.underline&&image.strike);
 // Reflow hides entire unsupported sections instead of disabling individual
 // fields while leaving their labels/buttons visible.
 var sections=new List<SiteEditor.InspectorSection>();foreach(string cap in new[]{"all","custom","text","fill","imageAction","actions"})sections.Add(new SiteEditor.InspectorSection{capability=cap,panel=Go()});
 Set(e,"sections",sections.ToArray());Set(e,"elementPanel",Go());Set(e,"pivotDropdown",new Dropdown());Set(e,"fontDropdown",new Dropdown());
 var plain=new SiteElement{kind="image"};Call(e,"RefreshInspector",plain);Check(!sections[2].panel.activeSelf&&sections[3].panel.activeSelf&&sections[4].panel.activeSelf&&!sections[5].panel.activeSelf);
 plain.kind="text";Call(e,"RefreshInspector",plain);Check(sections[2].panel.activeSelf&&!sections[3].panel.activeSelf&&!sections[4].panel.activeSelf&&!sections[5].panel.activeSelf);
 plain.kind="button";plain.layout=5;Call(e,"RefreshInspector",plain);Check(sections[2].panel.activeSelf&&sections[3].panel.activeSelf&&!sections[4].panel.activeSelf&&sections[5].panel.activeSelf&&!sections[1].panel.activeSelf);
 Console.WriteLine("PASS: "+count+" actual inspector binding checks: typed targets/files/colors, free text, stale-reference rejection, zero arguments, image conversion, style toggles and conditional section reflow");
 }
}
'''
with tempfile.TemporaryDirectory() as td:
    t=Path(td);(t/'test.cs').write_text(cs)
    subprocess.run(['mcs','-r:System.Web.Extensions','-out:'+str(t/'test.exe'),str(t/'test.cs'),str(ROOT/'tools/tests/site_core_stubs.cs'),str(ROOT/'tools/tests/site_ui_stubs.cs'),str(ROOT/'tools/tests/site_browser_stubs.cs'),str(C/'Lua/PcosLua.cs'),str(C/'SiteEditor.cs'),str(C/'SiteEditorInspector.cs'),str(C/'SiteEditorPages.cs'),str(C/'SiteEditorHosting.cs'),str(C/'Browser.cs'),str(C/'BrowserSites.cs'),str(C/'BrowserHosted.cs'),*[str(x)for x in (C/'Sites').glob('*.cs')]],check=True)
    subprocess.run(['mono',str(t/'test.exe')],check=True)
print('PASS: serialized conditional sections, typed argument controls, regular editor font and native Paint ColorPicker')
