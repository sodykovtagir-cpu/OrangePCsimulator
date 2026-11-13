#!/usr/bin/env python3
"""Execute actual shared filament material helper with Mono engine doubles.
Checks material state, NOT Unity-rendered pixels or postprocessing.
"""
from pathlib import Path
import shutil,subprocess,tempfile
ROOT=Path(__file__).resolve().parents[1]
COMP=ROOT/'Assets/Scripts/Assembly-CSharp/PC/Component'
look=(COMP/'FilamentLook.cs').read_text()
assert 'AddComponent<Light>' not in look and 'RealtimeEmissive' not in look
for name in ['FilamentSpool.cs','PrintableModel.cs']:
    text=(COMP/name).read_text()
    assert 'FilamentLook.Apply(' in text and 'FilamentLook.RemoveGlowLight(' in text
    assert 'EnsureGlowLight' not in text
assert 'FilamentLook.Apply(' in (COMP/'Software/ThreeDPrinterApp.cs').read_text()
for p in (ROOT/'Assets/Resources/components').glob('*Filament*.prefab'):
    assert 'glowLight:' not in p.read_text(),p
if not(shutil.which('mcs') and shutil.which('mono')):raise SystemExit('SKIP: Mono/mcs required')
cs=r'''
using System;using System.Collections.Generic;using PC.Component;using UnityEngine;
namespace UnityEngine {
 public class Object {public bool destroyed;public static void Destroy(Object o){o.destroyed=true;}}
 public struct Color {public float r,g,b,a;public Color(float x,float y,float z,float w=1){r=x;g=y;b=z;a=w;}public float maxColorComponent{get{return Math.Max(r,Math.Max(g,b));}}public static Color black{get{return new Color(0,0,0);}}}
 public static class Mathf {public static float Max(float a,float b){return Math.Max(a,b);}public static float Min(float a,float b){return Math.Min(a,b);}public static float Clamp01(float f){return Math.Max(0,Math.Min(1,f));}}
 public enum MaterialGlobalIlluminationFlags {None,RealtimeEmissive,BakedEmissive}
 public class Shader {public string name;public static Shader Find(string n){return new Shader{name=n};}}
 public class Material {public Shader shader=new Shader{name="ImportedUnsupported"};public bool supports,keyword;public MaterialGlobalIlluminationFlags globalIlluminationFlags=MaterialGlobalIlluminationFlags.RealtimeEmissive;public Dictionary<string,Color> colors=new Dictionary<string,Color>();public bool HasProperty(string n){return supports||shader.name=="Standard";}public void SetColor(string n,Color c){colors[n]=c;}public void EnableKeyword(string k){keyword=true;}public void DisableKeyword(string k){keyword=false;}}
 public class MaterialPropertyBlock {public Dictionary<string,Color> colors=new Dictionary<string,Color>();public Dictionary<string,float> floats=new Dictionary<string,float>();public void SetColor(string n,Color c){colors[n]=c;}public void SetFloat(string n,float v){floats[n]=v;}}
 public class Renderer {public Material[] materials;public MaterialPropertyBlock block;public void SetPropertyBlock(MaterialPropertyBlock b){block=b;}}
 public class GameObject:Object {public Transform transform=new Transform();}
 public class Transform {public Transform child;public Light light;public GameObject gameObject;public Transform Find(string n){return child;}public T GetComponent<T>()where T:class{return light as T;}}
 public class Light {public bool enabled=true;}
}
class Test {
 static int checks;static void Check(bool b){checks++;if(!b)throw new Exception("check "+checks);}
 static bool Near(float a,float b){return Math.Abs(a-b)<0.00001f;}
 static void Main(){
 var green=new Color(.721569f,1,.34902f);var old=new Color(green.r*2.4f,2.4f,green.b*2.4f);
 var a=new Material();var b=new Material{supports=true};var renderer=new Renderer{materials=new[]{a,null,b}};
 FilamentLook.Apply(new[]{renderer,null},green,.85f,old);
 foreach(var m in new[]{a,b}){Check(m.keyword);Check(m.globalIlluminationFlags==MaterialGlobalIlluminationFlags.None);var e=m.colors["_EmissionColor"];Check(Near(e.g,.65f));Check(Near(e.r/e.g,green.r));Check(Near(e.b/e.g,green.b));Check(Near(m.colors["_Color"].r,green.r));}
 Check(a.shader.name=="Standard");Check(b.shader.name=="ImportedUnsupported");Check(Near(renderer.block.floats["_Smoothness"],.25f));Check(Near(renderer.block.colors["_EmissionColor"].g,.65f));
 // Reapplying does not replace working materials; all slots lose glow on recolor.
 FilamentLook.Apply(new[]{renderer},green,.12f,Color.black);
 Check(object.ReferenceEquals(a,renderer.materials[0]));Check(!a.keyword&&!b.keyword);Check(a.colors["_EmissionColor"].maxColorComponent==0);Check(Near(renderer.block.floats["_Smoothness"],.12f));
 foreach(float intensity in new[]{0f,.2f,.65f,2.4f,100f}){var e=FilamentLook.SurfaceEmission(new Color(green.r*intensity,intensity,green.b*intensity));Check(e.maxColorComponent<=.650001f);if(intensity>0)Check(Near(e.r/e.g,green.r));}
 FilamentLook.Apply(null,green,1,old);FilamentLook.RemoveGlowLight(null);
 var root=new GameObject();var legacy=new Transform{light=new Light(),gameObject=new GameObject()};root.transform.child=legacy;
 FilamentLook.RemoveGlowLight(root);Check(!legacy.light.enabled);Check(legacy.gameObject.destroyed);
 root.transform.child=null;FilamentLook.RemoveGlowLight(root);
 Console.WriteLine("PASS: actual FilamentLook, "+checks+" checks: colored bounded emission, all material slots/fallback, no GI, non-glow reset, immediate legacy-light shutdown");
 }
}
'''
with tempfile.TemporaryDirectory() as td:
    p=Path(td);(p/'test.cs').write_text(cs)
    subprocess.run(['mcs','-out:'+str(p/'test.exe'),str(p/'test.cs'),str(COMP/'FilamentLook.cs')],check=True)
    subprocess.run(['mono',str(p/'test.exe')],check=True)

# Exercise the real generator in a scratch folder, never rewriting authored assets.
import generate_filament_variants as generator
from server_assets import read_guid
source=generator.COMPONENTS
template,variants=generator.build_variants()
try:
    with tempfile.TemporaryDirectory() as td:
        generator.COMPONENTS=Path(td)
        for variant in variants:
            name=variant['prefab']+'.prefab'
            shutil.copy2(source/name,Path(td)/name)
            shutil.copy2(source/(name+'.meta'),Path(td)/(name+'.meta'))
            guid=read_guid(source/name)
            assert generator.write_prefab(template,variant)==guid
            text=(Path(td)/name).read_text()
            assert 'glowLight:' not in text and 'glowLightIntensity:' not in text
            if variant['finish']==2:assert 'glowIntensity: 0.65' in text
finally:
    generator.COMPONENTS=source
print('PASS: actual 25-variant generator retains prefab GUIDs and never restores scene-light settings')
