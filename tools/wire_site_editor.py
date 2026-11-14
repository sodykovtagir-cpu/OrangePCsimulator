#!/usr/bin/env python3
"""Add a native PCOS site editor shell, keeping existing apps and authored icons.
All editor controls are serialized by the offline UI authoring helper.
"""
from pathlib import Path
import hashlib,re,shutil,sys
sys.path.insert(0,str(Path(__file__).resolve().parent))
from server_assets import ROOT,Prefab,read_guid,meta,ref
C=ROOT/'Assets/Scripts/Assembly-CSharp/PC/Component/Software'
# Existing prefabs may have been hand-edited in Unity. Never overwrite them
# implicitly; the incremental upgrader preserves the user's chrome and layout.
if (ROOT/'Assets/Resources/apps/SiteEditor.prefab').exists() and '--rebuild-shell' not in sys.argv:
    from upgrade_site_inspector import upgrade
    upgrade()
    from upgrade_site_pages import upgrade as upgrade_pages
    upgrade_pages()
    print('Preserved existing authored Site Editor shell')
    raise SystemExit(0)
for folder in [C/'Sites',ROOT/'Assets/Resources/SiteFonts']:
    folder.mkdir(parents=True,exist_ok=True);meta(folder,'DefaultImporter')
for p in [C/'SiteEditor.cs',C/'BrowserSites.cs',*(C/'Sites').glob('*.cs')]:
    path=Path(str(p)+'.meta')
    if not path.exists():
        guid=hashlib.md5(('orange-sites:'+str(p.relative_to(ROOT))).encode()).hexdigest()
        path.write_text(f'fileFormatVersion: 2\nguid: {guid}\nMonoImporter:\n  externalObjects: {{}}\n  serializedVersion: 2\n  defaultReferences: []\n  executionOrder: 0\n  icon: {{instanceID: 0}}\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n')
for name,source in [('default','Roboto-Regular.ttf'),('medium','Roboto-Medium.ttf'),('mono','windows_command_prompt.ttf')]:
    target=ROOT/'Assets/Resources/SiteFonts'/(name+'.ttf')
    shutil.copyfile(ROOT/'Assets/Font'/source,target)
    text=(ROOT/'Assets/Font'/(source+'.meta')).read_text()
    guid=hashlib.md5(('orange-site-font:'+name).encode()).hexdigest()
    Path(str(target)+'.meta').write_text(re.sub(r'^guid: .*$', 'guid: '+guid,text,flags=re.M))
p=Prefab(ROOT/'Assets/Resources/apps/TextEditor.prefab')
transforms={i:v for i,v in p.objects(224)}
root=next(i for i,v in transforms.items() if v['m_Father']['fileID']==0);rootgo=transforms[root]['m_GameObject']['fileID']
# Remove text-editor content and its titlebar Open/Save toolbar only.
remove_roots=[i for i,v in transforms.items() if p.data(v['m_GameObject']['fileID'])['m_Name'] in ('InputFieldMod_Scroll','Panel')]
remove=set()
def collect(i):
    remove.add(i)
    for child in transforms[i].get('m_Children',[]):collect(child['fileID'])
for i in remove_roots:collect(i)
gos={transforms[i]['m_GameObject']['fileID'] for i in remove}
for i in list(p.blocks):
    if i in remove or i in gos or p.data(i).get('m_GameObject',{}).get('fileID') in gos:del p.blocks[i]
for i,v in p.objects(224):
    children=[c['fileID'] for c in v.get('m_Children',[]) if c['fileID'] not in remove]
    p.set(i,'m_Children','\n'.join('  - '+ref(c) for c in children)+'\n' if children else '[]')
app=p.mono('TextEditor')[0][0]
p.set(rootgo,'m_Name','SiteEditor');p.set(root,'m_SizeDelta','{x: 900, y: 560}')
p.set(app,'m_Script','{fileID: 11500000, guid: '+read_guid(C/'SiteEditor.cs')+', type: 3}')
p.set(app,'appName','Site Editor');p.set(app,'fileName','.osite');p.set(app,'size','512')
p.blocks[app]=re.sub(r'^  input:.*\n','',p.blocks[app],flags=re.M)
for i in p.blocks:
    p.blocks[i]=p.blocks[i].replace('PC.Component.Software.TextEditor, Assembly-CSharp','PC.Component.Software.SiteEditor, Assembly-CSharp').replace('m_Text: Text Editor','m_Text: Site Editor')
from build_site_editor_ui import populate
populate(p,root,app)
target=ROOT/'Assets/Resources/apps/SiteEditor.prefab';p.save(target);guid=meta(target)
downloader=ROOT/'Assets/Resources/apps/Downloader.prefab';text=downloader.read_text()
if 'guid: '+guid not in text:
    text=text.replace('  apps:\n','  apps:\n  - {fileID: '+str(app)+', guid: '+guid+', type: 3}\n',1);downloader.write_text(text)
translate=ROOT/'Assets/Resources/Translate.txt';text=translate.read_text()
if not re.search(r'^Site Editor\t',text,re.M):
    headers=text.splitlines()[0].lstrip('\ufeff').split('\t');values=['Site Editor']*(len(headers)-1)
    if 'RU' in headers:values[headers.index('RU')-1]='Редактор сайтов'
    translate.write_text(text.rstrip('\n')+'\nSite Editor\t'+'\t'.join(values)+'\n')
from upgrade_site_inspector import upgrade
upgrade()
from upgrade_site_pages import upgrade as upgrade_pages
upgrade_pages()
print('Wired Site Editor into App Downloader; .osite association, native chrome, bundled standard fonts')
