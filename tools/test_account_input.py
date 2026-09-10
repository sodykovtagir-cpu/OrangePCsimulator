#!/usr/bin/env python3
"""Headless account/pointer regressions using real C# state, labels, pointer and gesture code.
Requires PyYAML, tree-sitter, tree-sitter-c-sharp, mcs and mono. Not Unity Play Mode.
"""
from pathlib import Path
import re,subprocess,tempfile
import yaml
from tree_sitter import Language,Parser
import tree_sitter_c_sharp
ROOT=Path(__file__).resolve().parents[1]
CS=ROOT/'Assets/Scripts/Assembly-CSharp'
PARSER=Parser(Language(tree_sitter_c_sharp.language()))

def node_class(path,name):
    data=path.read_bytes();tree=PARSER.parse(data)
    assert not tree.root_node.has_error,path
    def visit(node):
        if node.type=='class_declaration' and node.child_by_field_name('name').text.decode()==name:return node
        for child in node.children:
            result=visit(child)
            if result:return result
    found=visit(tree.root_node);assert found,name
    return found

def members(path,name,methods):
    result=[];found=set()
    for node in node_class(path,name).child_by_field_name('body').named_children:
        if node.type in ['field_declaration','enum_declaration','property_declaration']:
            result.append(node.text.decode())
        elif node.type=='method_declaration':
            n=node.child_by_field_name('name').text.decode()
            if n in methods:result.append(node.text.decode());found.add(n)
    assert found==methods,(name,methods-found)
    return '\n'.join(result)

def generated():
    account=members(CS/'AccountPage.cs','AccountPage',set('Tr RefreshChip Refresh RebuildSaves ShowSavesState IsSaveTemplate LoadMe SetMode DoLogout OnStateChanged OnLanguageChanged Subscribe'.split()))
    desktop=members(CS/'DesktopContextMenu.cs','DesktopContextMenu',set('GetEventCamera RaycastAt IsMenuElement IsPointerOverMenu CanReceivePointer PointerHitsThisOs IsForeignOs CanOpenMenuAt OnPointerDown OnPointerUp Update OnDisable'.split()))
    dtos='\n'.join(node_class(CS/'WorkshopClient.cs',n).text.decode() for n in ['AccountSaveItem','AccountMeResponse'])
    explorer='\n'.join(node_class(CS/'PC/Component/Software/FileManager.cs',n).text.decode() for n in ['ExplorerPane','ExplorerFileItem'])
    return '''using System;using System.Collections.Generic;using UnityEngine;using UnityEngine.UI;using UnityEngine.EventSystems;
'''+dtos+'''
public class AccountPage:MonoBehaviour {
public int Spawned;public string LastStatus;
private WorkshopClient Net(){return WorkshopClient.Instance;}
private void SetStatus(string value){LastStatus=value;}
private void SpawnSaveCard(AccountSaveItem value){var go=new GameObject("row_"+value.id);go.transform.parent=savesList;Spawned++;}
public void BeginProfile(){opened=true;mode=Mode.Home;Subscribe();Refresh();LoadMe();}
public void DisposeTest(){ServerAccounts.StateChanged-=OnStateChanged;Localization.LanguageChanged-=OnLanguageChanged;}
'''+account+'''
}
namespace PC.Component.Software {
using OperatingSystem = PC.Component.Software.OS.OperatingSystem;
public class DesktopContextMenu:MonoBehaviour {
public static DesktopContextMenu Current;public int Opened;
public static DesktopContextMenu For(UnityEngine.Component c){return Current;}
private void EnsureRefs(){}
private void OpenContextAt(Vector2 p){Opened++;}
private void CloseMenu(){menuPanel=null;renamePanel=null;}
public void ShowDesktopMenu(Vector2 p,bool skip=false){if(CanReceivePointer(p))Opened++;}
public void ShowFileMenu(File file,Vector2 p){if(CanReceivePointer(p))Opened++;}
public void ShowExplorerMenu(FileManager f,Vector2 p){if(CanReceivePointer(p))Opened++;}
'''+desktop+'''\n}\n'''+explorer+'\n}\n'

def docs(path):
    result={}
    for m in re.finditer(r'^--- !u!(\d+) &(-?\d+)(?: stripped)?\n(.*?)(?=^--- !u!|\Z)',path.read_text(),re.M|re.S):
        result[int(m[2])]=(int(m[1]),next(iter(yaml.safe_load(m[3]).values())))
    return result

def check_scene():
    d=docs(ROOT/'Assets/Scenes/Menu.unity')
    profile_guid=re.search(r'^guid: (\w+)',(CS/'AccountProfileLabel.cs.meta').read_text(),re.M)[1]
    excluded=set()
    for name in ['LocalizationText','LocalizationTextBracket','TextAnimation']:
        excluded.add(re.search(r'^guid: (\w+)',(CS/(name+'.cs.meta')).read_text(),re.M)[1])
    for text_id,email in [(79074060,False),(2136509710,False),(1571509672,True)]:
        text=d[text_id][1];go=d[text['m_GameObject']['fileID']][1]
        components=[d[c['component']['fileID']][1] for c in go['m_Component']]
        assert not any(c.get('m_Script',{}).get('guid') in excluded for c in components)
        labels=[c for c in components if c.get('m_Script',{}).get('guid')==profile_guid]
        assert len(labels)==1 and bool(labels[0]['showEmail'])==email
        assert not text['m_Text'] and text['m_FontData']['m_RichText']==0
    page=d[1704076433][1]
    state=d[page['savesStateText']['fileID']][1]
    go=d[state['m_GameObject']['fileID']][1]
    rt=d[go['m_Component'][0]['component']['fileID']][1]
    assert rt['m_Father']['fileID']==671177226 and rt['m_SizeDelta']['y']>=80
    assert state['m_FontData']['m_FontSize']>=18 and state['m_Color']['a']==1 and state['m_RaycastTarget']==0
    assert d[781788932][1]['m_SizeDelta']['x']==0
    assert d[781788933][1]['m_HorizontalFit']==0 and d[781788933][1]['m_VerticalFit']==2
    assert d[781788934][1]['m_ChildControlWidth']==1
    text=(CS/'AccountPage.cs').read_text()
    assert 'new GameObject("Empty"' not in text and 'MakeText(' not in text
    print('PASS: cached identity owns its labels; authored empty-state text is inside the viewport; scroll content width and vertical sizing are valid',flush=True)

def main():
    check_scene()
    with tempfile.TemporaryDirectory(prefix='account-input-tests-') as tmp:
        folder=Path(tmp);host=folder/'hosts.cs';host.write_text(generated());exe=folder/'tests.exe'
        subprocess.run(['mcs','-langversion:latest','-nowarn:0169,0414,0649','-out:'+str(exe),str(host),str(CS/'ServerAccounts.cs'),str(CS/'AccountProfileLabel.cs'),str(CS/'PointerInput.cs'),str(CS/'PC/Component/Software/File.cs'),str(CS/'PC/Component/Software/FileIcon.cs'),str(ROOT/'tools/tests/account_input_stubs.cs'),str(ROOT/'tools/tests/account_input_tests.cs')],check=True)
        subprocess.run(['mono',str(exe)],check=True)
    print('Account/input regressions passed. Unity scene rendering, actual mobile touches and network requests were not run.')
if __name__=='__main__':main()
