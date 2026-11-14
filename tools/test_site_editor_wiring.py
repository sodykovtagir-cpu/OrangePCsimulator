#!/usr/bin/env python3
"""Native editor shell/GUID/data wiring checks + compile against API doubles.
Does not launch Unity, assert rendered layout, or publish a website.
"""
from pathlib import Path
import hashlib,re,subprocess,tempfile
from server_assets import ROOT,Prefab,read_guid
C=ROOT/'Assets/Scripts/Assembly-CSharp/PC/Component/Software'
p=Prefab(ROOT/'Assets/Resources/apps/SiteEditor.prefab')
apps=p.mono('SiteEditor');assert len(apps)==1
app,v=apps[0];assert v['appName']=='Site Editor' and v['fileName']=='.osite'
for m in re.finditer(r'\{fileID: (-?\d+)\}',p.render()):assert int(m[1])==0 or int(m[1]) in p.blocks,m[0]
assert not p.mono('TextEditor')
assert len(v['fields'])==18
for key in ['view','inspector','sitePanel','elementPanel','previewPanel','status','assetRoot','assetRowTemplate','colorPicker','pivotDropdown','fontDropdown','undoButton','redoButton','hierarchyRoot','actionRoot','rowTemplate','addMenu','commandDropdown','argumentHint']:
    assert v[key]['fileID'] in p.blocks,key
guid=read_guid(ROOT/'Assets/Resources/apps/SiteEditor.prefab')
downloader=(ROOT/'Assets/Resources/apps/Downloader.prefab').read_text()
assert downloader.count('guid: '+guid)==1
assert 'fileID: '+str(app)+', guid: '+guid in downloader
for name in ['default','medium','mono']:
    assert (ROOT/'Assets/Resources/SiteFonts'/(name+'.ttf')).exists()
source=(C/'SiteEditor.cs').read_text()
for required in ['ShowDialog(', '".osite"', 'SiteHistory', 'SiteDocument.Parse', 'ImportImage', 'BindUI', 'SetTextWithoutNotify', 'GetTaskbarInsetLocalHeight']:
    assert required in source,required
assert 'SiteWidgets.' not in source and 'BuildUI' not in source
assert 'new GameObject' not in source
assert 'UnityWebRequest' not in source
assert 'OpenHostingPanel' in source
# Re-run generation and ensure it is idempotent and does not modify old apps/icons.
files=[ROOT/'Assets/Resources/apps/SiteEditor.prefab',ROOT/'Assets/Resources/apps/Downloader.prefab',ROOT/'Assets/Resources/Translate.txt',ROOT/'Assets/Resources/apps/TextEditor.prefab']
before={x:hashlib.sha256(x.read_bytes()).digest() for x in files}
subprocess.run(['python',str(ROOT/'tools/wire_site_editor.py')],check=True)
assert all(hashlib.sha256(x.read_bytes()).digest()==h for x,h in before.items())
# Compile complete new editor/renderer and the actual interpreter, with fake
# Unity/PCOS declarations solely to catch type/signature/syntax mismatches.
with tempfile.TemporaryDirectory() as td:
    subprocess.run(['mcs','-target:library','-r:System.Web.Extensions','-out:'+str(Path(td)/'site-ui.dll'),str(ROOT/'tools/tests/site_core_stubs.cs'),str(ROOT/'tools/tests/site_ui_stubs.cs'),str(C/'Lua/PcosLua.cs'),str(C/'SiteEditor.cs'),str(C/'SiteEditorInspector.cs'),str(C/'SiteEditorPages.cs'),str(C/'SiteEditorHosting.cs'),str(C/'Browser.cs'),str(C/'BrowserSites.cs'),str(C/'BrowserHosted.cs'),str(ROOT/'tools/tests/site_browser_stubs.cs'),*[str(x) for x in (C/'Sites').glob('*.cs')]],check=True)
print('PASS: native Site Editor shell, all local refs, .osite, App Downloader, fonts, idempotent wiring, UI C# compile with API doubles; no false publication')
