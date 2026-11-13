#!/usr/bin/env python3
"""Compile the real browser + renderer and exercise non-rendering boundary paths.
Unity/PCOS/network declarations are doubles, not a Play Mode/end-to-end test.
"""
from pathlib import Path
import subprocess,tempfile
from server_assets import ROOT,Prefab
C=ROOT/'Assets/Scripts/Assembly-CSharp/PC/Component/Software'
browser=(C/'Browser.cs').read_text();local=(C/'BrowserSites.cs').read_text();os=(C/'OS/OperatingSystem.cs').read_text()
search=browser[browser.index('public void Search()'):browser.index('private static string NormalizeUrl')]
assert search.index('OpenLocalAddress(query)')<search.index('LoadOnline(')
assert 'InternetService' not in local and 'Application.OpenURL' not in local
assert 'system.FileManager.Write' not in local and 'LaunchApp' not in local
assert 'yield return null' in local and 'generation == localGeneration' in local
assert 'siteNavigationClosed = true; ClearPage()' in local
assert 'if (!localNavigationStarted) ShowHome();' in browser
opening=os[os.index('private bool OpenFileInternal'):os.index('public bool IsElevatedExplorer')]
assert opening.index('OpenSiteFile(file)')<opening.index('LuaAppPackage.IsPackage')
assert 'siteEditor.OpenDocument(file)' in opening
helper=os[os.index('public bool OpenSiteFile'):os.index('public bool OpenFileWith')]
for s in ['!Ready','IsSystemPath','SafePath','files.Contains(file)','OpenLocalSite(file, diskIndex)','IsAppInstalled("Browser"']:assert s in helper,s
p=Prefab(ROOT/'Assets/Resources/apps/SiteEditor.prefab');_,v=p.mono('SiteEditor')[0]
assert p.data(v['addMenu']['fileID'])['m_IsActive']==0
assert p.data(p.data(v['rowTemplate']['fileID'])['m_GameObject']['fileID'])['m_IsActive']==0
assert p.data(v['commandDropdown']['fileID'])['m_Template']['fileID'] in p.blocks
names={p.data(t['m_GameObject']['fileID'])['m_Name']:t for _,t in p.objects(224)}
assert names['Command half']['m_AnchorMax']['x']==.5 and names['Arguments half']['m_AnchorMin']['x']==.5
assert names['Add menu']['m_Father']==names['Hierarchy list']['m_Father']
assert 'preserveAspect = true' not in (C/'Sites/SiteCanvas.cs').read_text()
cs=r'''
using System;using System.Text;using System.Collections;using System.Collections.Generic;using System.Reflection;using PC.Component.Software;using PC.Component.Software.Sites;using PC.Component.Software.OS;using UnityEngine;using UnityEngine.UI;
class Test {
 static int checks;static void Check(bool b){checks++;if(!b)throw new Exception("browser boundary "+checks);}
 static void Set(object b,string key,object v){var type=key=="system"?typeof(App):typeof(Browser);type.GetField(key,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(b,v);}
 static object Call(Browser b,string name,params object[] args){return typeof(Browser).GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(b,args);}
 static void Main(){
 var b=new Browser();var os=new PC.Component.Software.OS.OperatingSystem{AllStorage=new List<Storage>(),SaveDialog=new SaveDialog()};Set(b,"system",os);Set(b,"addressBar",new InputField());Set(b,"oggleHome",new GameObject("home"));Set(b,"resultsPage",new GameObject("results"));Set(b,"errorPage",new GameObject("error"));Set(b,"errorText",new Text());
 Check(!b.OpenLocalSite(null,0));Check(!b.OpenLocalSite(new File(),-1));
 var file=new File{path="System/private.osite"};os.AllStorage.Add(new Storage{Id=12,files=new List<File>{file}});Check(!b.OpenLocalSite(file,0));Check(!b.OpenLocalSite(new File{path="safe.osite"},0));
 Check(!(bool)Call(b,"OpenLocalAddress","file:///etc/passwd"));Check(!(bool)Call(b,"OpenLocalAddress","file://disk/12/System/private.osite"));Check(!(bool)Call(b,"OpenLocalAddress","file://disk/12/missing.osite"));
 var doc=new SiteDocument();var asset=new SiteAsset{name="test.txt",media="application/x-pcos-file",data=Convert.ToBase64String(Encoding.UTF8.GetBytes("bundled contents"))};doc.assets.Add(asset);Set(b,"localDocument",doc);b.IsAdministrator=true;
 Call(b,"DownloadSiteAsset",asset);Check(os.SaveDialog.calls==1);Check(!os.SaveDialog.administrator);Check(os.SaveDialog.content=="bundled contents"&&os.SaveDialog.extensions[0]==".txt");
 Call(b,"DownloadSiteAsset",new SiteAsset{name="test.txt",data=asset.data});Check(os.SaveDialog.calls==1);
 bool ran=false;int generation=(int)typeof(Browser).GetField("localGeneration",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(b);
 var pending=(IEnumerator)Call(b,"FinishLocalNavigation",generation,(Action)(()=>ran=true));Check(pending.MoveNext());Call(b,"ClearLocalContext");Check(!pending.MoveNext()&&!ran);
 generation=(int)typeof(Browser).GetField("localGeneration",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(b);
 pending=(IEnumerator)Call(b,"FinishLocalNavigation",generation,(Action)(()=>ran=true));Check(pending.MoveNext());Check(!pending.MoveNext()&&ran);
 ran=false;pending=(IEnumerator)Call(b,"FinishLocalNavigation",generation,(Action)(()=>ran=true));Check(pending.MoveNext());os.Ready=false;Check(!pending.MoveNext()&&!ran);os.Ready=true;
 Set(b,"localDocument",doc);b.Close();Call(b,"DownloadSiteAsset",asset);Check(os.SaveDialog.calls==1);Check(!b.OpenLocalSite(file,0));Check(!(bool)Call(b,"OpenLocalAddress","file://disk/12/safe.osite"));
 Console.WriteLine("PASS: "+checks+" real browser boundary checks: malformed/protected/foreign files, package-only download, normal privileges, deferred navigation/cancellation and close guards");
 }
}
'''
with tempfile.TemporaryDirectory() as td:
    path=Path(td);(path/'test.cs').write_text(cs)
    subprocess.run(['mcs','-r:System.Web.Extensions','-out:'+str(path/'test.exe'),str(path/'test.cs'),*[str(ROOT/'tools/tests'/n) for n in ['site_core_stubs.cs','site_ui_stubs.cs','site_browser_stubs.cs']],str(C/'Lua/PcosLua.cs'),str(C/'Browser.cs'),str(C/'BrowserSites.cs'),*[str(x) for x in (C/'Sites').glob('*.cs')]],check=True)
    subprocess.run(['mono',str(path/'test.exe')],check=True)
print('PASS: hierarchy/Add template, 50/50 command-argument columns, native dropdown, image stretch and local Browser/OS integration source checks')
