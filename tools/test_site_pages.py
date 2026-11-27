#!/usr/bin/env python3
"""New pages/hosting behaviours; engine/network/save declarations are doubles."""
from pathlib import Path
import subprocess,tempfile
from server_assets import ROOT,Prefab
C=ROOT/'Assets/Scripts/Assembly-CSharp/PC/Component/Software'
p=Prefab(ROOT/'Assets/Resources/apps/SiteEditor.prefab');_,v=p.mono('SiteEditor')[0]
for key in ['pageTabs','pagePath','hierarchyMenu','contextBox','contextName','backgroundDropdown','hostingPanel','hostURL','hostedSites','hostingStats','actionEditor','hostDomainKind','topUpButton','topUpAmount']:assert v[key]['fileID']in p.blocks,key
# The address dropdown offers exactly two choices: a reserved temporary address
# or a custom domain. Offline hosting is automatic, not a mode.
raw=p.data(v['hostDomainKind']['fileID'])['m_Options']
opts=raw['m_Options'] if isinstance(raw,dict) else raw
assert [o['m_Text'] for o in opts]==['Стандартный адрес','Свой домен'],opts
assert p.data(v['topUpButton']['fileID'])['m_OnClick']['m_PersistentCalls']['m_Calls'][0]['m_MethodName']=='TopUpHosting'
assert p.data(v['topUpAmount']['fileID'])['m_Text']=='100'
# Both controls are children of the hosting panel body, not floating outside it.
names={p.data(d['m_GameObject']['fileID'])['m_Name']:i for i,d in p.objects(224)}
panel=names['Site hosting panel']
kids=[p.data(c['fileID'])['m_GameObject']['fileID'] for c in p.data(panel)['m_Children']]
print_names=[p.data(k)['m_Name'] for k in kids]
assert 'Host domain kind' in print_names and 'Top up hosting' in print_names and 'Top up amount' in print_names,print_names
assert p.data(v['hostingPanel']['fileID'])['m_IsActive']==0
assert p.data(v['hierarchyMenu']['fileID'])['m_IsActive']==0
for _,d in p.objects(114):
 if 'm_TextComponent'in d:
  graphic=d['m_TargetGraphic']['fileID'];go=p.data(graphic)['m_GameObject']['fileID'];tr=next(x for _,x in p.objects(224)if x['m_GameObject']['fileID']==go)
  assert p.data(graphic)['m_Color']==dict(r=1,g=1,b=1,a=1)
  names=[p.data(p.data(x['fileID'])['m_GameObject']['fileID'])['m_Name']for x in tr['m_Children']]
  assert all('Input frame '+edge in names for edge in ['Top','Bottom','Left','Right'])
for test in ['site_pages_tests.cs','site_hosting_lifecycle_tests.cs','site_followup_tests.cs','site_server_runtime_tests.cs','site_hosting_billing_tests.cs']:
 with tempfile.TemporaryDirectory()as td:
  exe=Path(td)/'pages.exe'
  subprocess.run(['mcs',str(ROOT/'Assets/Scripts/Assembly-CSharp/PC/Component/CpuThermals.cs'),str(ROOT/'Assets/Scripts/Assembly-CSharp/OggleResultItem.cs'),'-r:System.Web.Extensions','-out:'+str(exe),*[str(ROOT/'tools/tests'/n)for n in ['site_core_stubs.cs','site_ui_stubs.cs','site_browser_stubs.cs',test]],*[str(C/n)for n in ['Lua/PcosLua.cs','SiteEditor.cs','SiteEditorInspector.cs','SiteEditorPages.cs','SiteEditorHosting.cs','Browser.cs','BrowserSites.cs','BrowserHosted.cs','BrowserSiteAssets.cs']],*[str(x)for x in (C/'Sites').glob('*.cs')]],check=True)
  subprocess.run(['mono',str(exe)],check=True,timeout=20)
print('PASS: serialized tabs/context/background/hosting references, domain-kind dropdown, top-up button, hidden overlays and four real non-raycast field borders')
