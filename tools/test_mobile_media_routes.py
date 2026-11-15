#!/usr/bin/env python3
"""Compile actual Paint import/texture methods + Animator video picker with UI doubles.
Native video codecs/Android document providers must still be tested on device.
"""
from pathlib import Path
import subprocess,tempfile
R=Path(__file__).resolve().parents[1];C=R/'Assets/Scripts/Assembly-CSharp/PC/Component/Software'
def method(source, signature):
 a=source.index(signature);start=source.index('{',a);depth=1;i=start+1
 while depth:
  depth+=(source[i]=='{')-(source[i]=='}');i+=1
 return source[a:i]
p=(C/'Paint.cs').read_text();a=(C/'Animator.cs').read_text()
paint='\n'.join(method(p,s) for s in ['public void OpenLocalFile()','private void LoadTexture(byte[] bytes)','private Texture2D DecodeTexture(byte[] bytes)','private void SetTexture(Texture2D tex)','private void Resize(Texture2D texture2D, int targetX, int targetY)'])
video=method(a,'private void PickVideoFromDevice(Vector2Int resolution)')
assert 'NativeGallery' not in video and 'NativeGallery.GetImageFromGallery' not in paint
assert 'new System.Uri(System.IO.Path.GetFullPath(path)).AbsoluteUri' in a
h=r'''using System;using System.IO;using System.Collections;using UnityEngine;using Paint=PC.Component.Software.Paint;using Animator=PC.Component.Software.Animator;
namespace UnityEngine{
 public class TextAreaAttribute:Attribute{public TextAreaAttribute(int a,int b){}}
 public class GameObject{public bool activeInHierarchy=true;}
 public class MonoBehaviour{public GameObject gameObject=new GameObject();public IEnumerator started;public object StartCoroutine(IEnumerator c){started=c;return c;}public static void Destroy(Texture2D t){t.dead=true;}}
 public struct Vector2Int{public int x,y;public Vector2Int(int w,int h){x=w;y=h;}}
 public struct Rect{public Rect(float a,float b,float c,float d){}}
 public enum TextureFormat{RGBA32}public enum FilterMode{Point}public enum TextureWrapMode{Clamp}
 public class Texture2D{public int width,height;public bool dead;public FilterMode filterMode;public TextureWrapMode wrapMode;public static bool failRead;public Texture2D(int w,int h,TextureFormat f,bool m){width=w;height=h;}public void Reinitialize(int w,int h){width=w;height=h;}public void ReadPixels(Rect r,int a,int b){if(failRead)throw new Exception();}public void Apply(){}}
 public class RenderTexture{public static RenderTexture active;public static int gets,releases;public static RenderTexture GetTemporary(int w,int h,int d){gets++;return new RenderTexture();}public static void ReleaseTemporary(RenderTexture r){releases++;}}
 public static class Graphics{public static void Blit(Texture2D t,RenderTexture r){}}
 public static class ImageConversion{public static bool valid=true;public static bool LoadImage(Texture2D t,byte[] b){t.width=64;t.height=32;return valid;}}
 public static class Debug{public static void LogWarning(string s){}}
}
class OS{public int errors;public void ShowMessageBox(string a,string b){errors++;}}
class Display{public Texture2D texture;}
static class NativeFilePicker{public static Action<string> callback;public static string[] filters;public static bool fail;public static void PickFile(Action<string> c,string[] f){if(fail)throw new Exception();callback=c;filters=f;}}
namespace PC.Component.Software{
class Paint:MonoBehaviour{public OS system=new OS();public Texture2D texture;public Display display=new Display();public Vector2Int canvasSize=new Vector2Int(128,128);PAINT}
class Animator:MonoBehaviour{public OS system=new OS();public bool mediaAvailable=true;public object mediaImport;public string imported;public Vector2Int resolution;void ImportFailure(string s){system.errors++;}public void Pick(Vector2Int r){PickVideoFromDevice(r);}IEnumerator ImportVideoFrames(string p,Vector2Int r){imported=p;resolution=r;yield return null;}VIDEO}
}
class Test{static int n;static void Check(bool b,string s){n++;if(!b)throw new Exception(s);}static void Main(string[] args){
 string file=Path.Combine(args[0],"Юникод # image.png");File.WriteAllBytes(file,new byte[]{1,2,3});
 var p=new Paint();p.OpenLocalFile();Check(NativeFilePicker.filters.Length==2&&NativeFilePicker.filters[0]=="image/png","image filters");NativeFilePicker.callback(file);Check(p.texture.width==128&&p.texture.height==128,"paint import/resize not reached");Check(p.display.texture==p.texture,"paint display not updated");Check(RenderTexture.gets==RenderTexture.releases,"paint RT leak");
 var before=p.texture;p.OpenLocalFile();NativeFilePicker.callback(null);Check(before==p.texture,"cancel replaced image");
 ImageConversion.valid=false;p.OpenLocalFile();NativeFilePicker.callback(file);Check(before==p.texture&&!before.dead&&p.system.errors==1,"bad image destroyed old painting");ImageConversion.valid=true;
 var previous=new RenderTexture();RenderTexture.active=previous;Texture2D.failRead=true;p.OpenLocalFile();NativeFilePicker.callback(file);Check(RenderTexture.active==previous&&RenderTexture.gets==RenderTexture.releases,"read failure leaked RT state");Check(p.texture==before&&!before.dead,"resize error replaced old painting");Texture2D.failRead=false;
 p.gameObject.activeInHierarchy=false;before=p.texture;p.OpenLocalFile();NativeFilePicker.callback(file);Check(before==p.texture,"late callback imported into closed Paint");
 NativeFilePicker.fail=true;p.OpenLocalFile();Check(p.system.errors==3,"picker exception not reported");NativeFilePicker.fail=false;
 var a=new Animator();a.Pick(new Vector2Int(120,80));Check(NativeFilePicker.filters[0]=="video/*","video filter");NativeFilePicker.callback(file);Check(a.started!=null,"video callback did not start decoder");a.started.MoveNext();Check(a.imported==file&&a.resolution.x==120,"video path/resolution not forwarded");
 a=new Animator();a.Pick(new Vector2Int(120,80));NativeFilePicker.callback(null);Check(a.started==null,"cancel started decoder");NativeFilePicker.callback(file+".missing");Check(a.started==null&&a.system.errors==1,"missing video accepted");
 string uri=new Uri(Path.GetFullPath(file)).AbsoluteUri;Check(uri.StartsWith("file:")&&uri.Contains("%23")&&!uri.Contains(" "),"local URL escaping");
 Console.WriteLine("PASS: "+n+" real Paint/video-picker route checks: success, filters, cancel, lifecycle, invalid file, RT cleanup and escaped URL");}}
'''.replace('PAINT',paint).replace('VIDEO',video)
with tempfile.TemporaryDirectory() as td:
 d=Path(td);(d/'test.cs').write_text(h)
 subprocess.run(['mcs','-out:'+str(d/'test.exe'),str(d/'test.cs'),str(C/'File.cs')],check=True)
 subprocess.run(['mono',str(d/'test.exe'),td],check=True)
