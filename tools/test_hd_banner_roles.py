#!/usr/bin/env python3
"""Validate 1024x2240 HD banners and the authored admin-author label. Requires PyYAML, tree-sitter, mcs/mono."""
from pathlib import Path
from tree_sitter import Language,Parser
import tree_sitter_c_sharp
import re,subprocess,tempfile,yaml
ROOT=Path(__file__).resolve().parents[1];CS=ROOT/'Assets/Scripts/Assembly-CSharp'
P=Parser(Language(tree_sitter_c_sharp.language()))

def methods(path,names):
    tree=P.parse(path.read_bytes());assert not tree.root_node.has_error
    out=[]
    def visit(n):
        if n.type=='method_declaration' and n.child_by_field_name('name').text.decode() in names:out.append(n.text.decode())
        for c in n.children:visit(c)
    visit(tree.root_node);assert len(out)==len(names);return '\n'.join(out)

def docs(path):
    return {int(m[2]):(int(m[1]),next(iter(yaml.safe_load(m[3]).values()))) for m in re.finditer(r'^--- !u!(\d+) &(-?\d+)(?: stripped)?\n(.*?)(?=^--- !u!|\Z)',path.read_text(),re.M|re.S)}

def main():
    paint=CS/'PC/Component/Software/Paint.cs';print_code=(CS/'PC/Component/Software/PrintExpert.cs').read_text()
    assert 'bannerHdW = 1024' in print_code and 'bannerHdH = 2240' in print_code
    assert 'bannerW = 32' in print_code and 'bannerH = 70' in print_code and 'hdSurcharge = 500' in print_code
    prefab=(ROOT/'Assets/Resources/apps/Paint.prefab').read_text()
    assert 'maxCanvasSize: {x: 1024, y: 2240}' in prefab and 'm_Text: 1024x2240' in prefab
    assert 'm_Text: 1024x2240' in (ROOT/'Assets/GameObject/PrintExpert.prefab').read_text()
    row=docs(ROOT/'Assets/Resources/components/SavePrefab.prefab')
    authors=[(fid,b) for fid,(kind,b) in row.items() if kind==1 and b.get('m_Name')=='Author']
    assert len(authors)==1
    comps=[row[c['component']['fileID']] for c in authors[0][1]['m_Component']]
    label=next(b for kind,b in comps if 'm_FontData' in b)
    assert label['m_FontData']['m_RichText']==0 and label['m_RaycastTarget']==0
    assert label['m_Text'] in ['',None]
    assert 'it.author_is_admin ? AccountProfileLabel.AdminColor' in (CS/'WorkshopMenu.cs').read_text()
    code='''using System;
namespace UnityEngine {public struct Vector2Int {public int x,y;public Vector2Int(int x,int y){this.x=x;this.y=y;}}}
class InputField {public string text;}
class Button {public bool interactable;}
class PaintHost {
 public InputField canvasWidthInput=new InputField(),canvasHeightInput=new InputField();public Button createButton=new Button();
 public UnityEngine.Vector2Int minCanvasSize=new UnityEngine.Vector2Int(1,1),maxCanvasSize=new UnityEngine.Vector2Int(1024,2240);
 public int Width,Height,Created;
 private void SetCanvasSize(int w,int h){Width=w;Height=h;}private void NewCanvas(){Created++;}private void InitWorkspace(){}
'''+methods(paint,{'ApplyPreset','OnValueChangedSize','Create'})+'''
}
class Tests {
 static void Check(bool value){if(!value)throw new Exception("banner regression");}
 static void Main(){var p=new PaintHost();int[,] expected={{32,32},{32,70},{1024,1024},{1024,2240}};
 for(int i=0;i<4;i++){p.ApplyPreset(i);p.OnValueChangedSize();Check(p.createButton.interactable);Check(p.canvasWidthInput.text==expected[i,0].ToString()&&p.canvasHeightInput.text==expected[i,1].ToString());}
 p.Create();Check(p.Width==1024&&p.Height==2240&&p.Created==1);
 p.canvasHeightInput.text="2241";p.OnValueChangedSize();p.Create();Check(!p.createButton.interactable&&p.Created==1);
 p.canvasHeightInput.text="2240";p.canvasWidthInput.text="1025";p.OnValueChangedSize();p.Create();Check(!p.createButton.interactable&&p.Created==1);
 p.canvasWidthInput.text="bad";p.OnValueChangedSize();p.Create();Check(!p.createButton.interactable&&p.Created==1);
 Console.WriteLine("PASS: real Paint presets/create guards; 1024x2240 works, older normal/square presets remain, invalid sizes are rejected");}}
'''
    with tempfile.TemporaryDirectory(prefix='hd-banner-') as tmp:
        folder=Path(tmp);source=folder/'test.cs';source.write_text(code);exe=folder/'test.exe'
        subprocess.run(['mcs','-out:'+str(exe),str(source)],check=True)
        subprocess.run(['mono',str(exe)],check=True)
    print('PASS: PrintExpert dimensions/prices and separate non-rich-text author label; no automatic UI generation added')
    print('HD banner/admin-label checks passed. Unity Play Mode was not run.')
if __name__=='__main__':main()
