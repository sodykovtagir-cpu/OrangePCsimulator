#!/usr/bin/env python3
"""Headless Aquarium catalogue and startup-FOV checks (PyYAML, mcs, mono). No Unity Play Mode."""
from pathlib import Path
import re,struct,subprocess,tempfile
import yaml
ROOT=Path(__file__).resolve().parents[1]
ASSETS=ROOT/'Assets'

def docs(path):
    return {int(m[2]):(int(m[1]),next(iter(yaml.safe_load(m[3]).values()))) for m in re.finditer(r'^--- !u!(\d+) &(-?\d+)(?: stripped)?\n(.*?)(?=^--- !u!|\Z)',path.read_text(),re.M|re.S)}

def refs(value):
    if isinstance(value,dict):
        if 'fileID' in value:yield value
        for child in value.values():yield from refs(child)
    elif isinstance(value,list):
        for child in value:yield from refs(child)

def main():
    menu=docs(ASSETS/'GameObject/ModForge.prefab');catalog=menu[114711914853252429][1]
    assert len(catalog['coverPrefabs'])==len(catalog['texts'])==5
    assert struct.unpack('<5i',bytes.fromhex(catalog['prices']))==(120,100,80,60,120)
    assert catalog['hdSurcharge']==500
    cover_path=ASSETS/'Resources/components/Aquarium_Cover_ATX(customGlass).prefab'
    cover=docs(cover_path)
    guid=re.search(r'^guid: (\w+)',Path(str(cover_path)+'.meta').read_text(),re.M)[1]
    assert catalog['coverPrefabs'][4]=={'fileID':7478285624938629712,'guid':guid,'type':3}
    assert catalog['texts'][4]['fileID']==3202623659170792719
    assert menu[1261032253561010680][1]['m_OnClick']['m_PersistentCalls']['m_Calls'][0]['m_Arguments']['m_IntArgument']==4
    assert '{Glass}' in menu[3202623659170792719][1]['m_Text']
    paint=cover[7478285624938629712][1]
    assert paint['m_Script']['guid']=='496255149b263314289917e9997fe255'
    assert paint['rend']['fileID']==5099699094043531772 and cover[5099699094043531772][0]==23
    assert cover[114919461863270154][1]['spawnId']==cover_path.stem
    for parsed in [menu,cover]:
        for fid,(kind,body) in parsed.items():
            for ref in refs(body):
                if ref['fileID'] and 'guid' not in ref:assert ref['fileID'] in parsed,(fid,ref)
            if kind==1:assert parsed[body['m_Component'][0]['component']['fileID']][0] in (4,224)
    code=(ASSETS/'Scripts/Assembly-CSharp/PC/Component/Software/ModForge.cs').read_text()
    assert 'if (!HasProduct(index)) return;' in code
    assert 'if (selectedFile == null || !HasProduct(selectedProduct)) return;' in code
    assert 'if (!Hd && (tex.width > 512 || tex.height > 512))' in code
    assert 'Spawn(coverPrefabs[selectedProduct], tex)' in code
    print('PASS: Aquarium button, fifth slot, normal/HD prices, localized label, paint renderer, saved ID and all local prefab refs')
    player=(ASSETS/'Scripts/Assembly-CSharp/Player.cs').read_text()
    awake=player[player.index('private void Awake()'):player.index('private void Start()')]
    assert 'FieldOfViewSettings.ApplySaved(viewCamera)' in awake
    pause=(ASSETS/'Scripts/Assembly-CSharp/PauseMenu.cs').read_text()
    destroy=pause[pause.index('private void OnDestroy()'):]
    assert 'PlayerPrefs.SetFloat("FOV"' not in destroy
    assert 'fovSlider.SetValueWithoutNotify(fov)' in pause
    assert 'FieldOfViewSettings.ReadSaved()' in (ASSETS/'Scripts/Assembly-CSharp/Functions.cs').read_text()
    harness=r'''
using System;
namespace UnityEngine {
 public class Camera {public float fieldOfView;public bool orthographic;public object targetTexture;}
 public static class Mathf {public static float Clamp(float v,float min,float max){return Math.Max(min,Math.Min(max,v));}}
 public static class PlayerPrefs {public static float? Saved;public static float GetFloat(string key,float fallback){return Saved.HasValue?Saved.Value:fallback;}}
}
class Tests {
 static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
 static void Main(){
  var c=new UnityEngine.Camera{fieldOfView=72};UnityEngine.PlayerPrefs.Saved=95;
  FieldOfViewSettings.ApplySaved(c);Check(c.fieldOfView==95,"saved FOV not applied");
  UnityEngine.PlayerPrefs.Saved=null;c.fieldOfView=72;FieldOfViewSettings.ApplySaved(c);Check(c.fieldOfView==72,"default camera changed");
  UnityEngine.PlayerPrefs.Saved=110;c.targetTexture=new object();c.fieldOfView=35;FieldOfViewSettings.ApplySaved(c);Check(c.fieldOfView==35,"preview camera changed");
  c.targetTexture=null;c.orthographic=true;FieldOfViewSettings.ApplySaved(c);Check(c.fieldOfView==35,"orthographic camera changed");
  c.orthographic=false;UnityEngine.PlayerPrefs.Saved=float.NaN;FieldOfViewSettings.ApplySaved(c);Check(c.fieldOfView==35,"NaN accepted");
  Check(FieldOfViewSettings.Sanitize(float.PositiveInfinity,float.NaN)==60,"invalid fallback");
  Check(FieldOfViewSettings.Sanitize(-10)==1&&FieldOfViewSettings.Sanitize(200)==179,"unsafe perspective range");
  UnityEngine.PlayerPrefs.Saved=95;c.fieldOfView=20;Check(FieldOfViewSettings.ReadSaved()==95,"zoom did not restore preferred FOV");
  FieldOfViewSettings.ApplySaved(null);
  Console.WriteLine("PASS: saved/default FOV, preview exclusion, orthographic exclusion, invalid values, valid bounds and zoom restoration");
 }
}'''
    with tempfile.TemporaryDirectory(prefix='aquarium-fov-') as tmp:
        folder=Path(tmp);source=folder/'tests.cs';source.write_text(harness);exe=folder/'tests.exe'
        subprocess.run(['mcs','-out:'+str(exe),str(ASSETS/'Scripts/Assembly-CSharp/FieldOfViewSettings.cs'),str(source)],check=True)
        subprocess.run(['mono',str(exe)],check=True)
    print('PASS: startup runs in Player.Awake and inactive pause menus cannot overwrite the saved FOV')
    print('Aquarium/FOV checks passed. Unity Editor/rendering/Play Mode were not run.')
if __name__=='__main__':main()
