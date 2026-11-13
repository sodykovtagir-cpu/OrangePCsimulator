#!/usr/bin/env python3
"""Execute the actual CaptureScreenshot method against small engine doubles.
Covers ownership/restoration on success and every allocating failure point.
Not a Unity render/Android reproduction of the reported 1.8.41 artefact.
"""
from pathlib import Path
import shutil, subprocess, tempfile
ROOT=Path(__file__).resolve().parents[1]
text=(ROOT/'Assets/Scripts/Assembly-CSharp/SaveManager.cs').read_text()
a=text.index('private string CaptureScreenshot()');b=text.index('{',a);i=b+1;depth=1
while depth:
    depth+=(text[i]=='{')-(text[i]=='}');i+=1
method=text[a:i]
assert 'var previousActive = RenderTexture.active;' in method
assert 'RenderTexture.active = previousActive;' in method
assert 'RenderTexture.active = null;' not in method
if not (shutil.which('mcs') and shutil.which('mono')):
    raise SystemExit('SKIP: Mono/mcs required for screenshot runtime doubles')
cs=r'''
using System;using System.Collections.Generic;using UnityEngine;
namespace UnityEngine {
 public class Object {
  public bool destroyed;
  public static void Destroy(Object value){value.destroyed=true;}
 }
 public static class Fault {
  public static string Stage;
  public static void At(string stage){if(Stage==stage)throw new Exception(stage);}
 }
 public class Camera:Object {
  public static Camera main;
  public RenderTexture targetTexture;
  public bool enabled=true;public int cullingMask=123;public float fieldOfView=63;
  public void Render(){RenderTexture.active=targetTexture;Fault.At("render");}
 }
 public class RenderTexture:Object {
  public static RenderTexture active;
  public static List<RenderTexture> made=new List<RenderTexture>();
  public bool released;
  public RenderTexture(int w,int h,int d){Fault.At("rt");made.Add(this);}
  public void Release(){
   if(this==active || (Camera.main!=null&&Camera.main.targetTexture==this))throw new Exception("freeing attached RT");
   released=true;
  }
 }
 public enum TextureFormat {RGB24}
 public struct Rect{public Rect(int x,int y,int w,int h){}}
 public class Texture2D:Object {
  public static List<Texture2D> made=new List<Texture2D>();
  public Texture2D(int w,int h,TextureFormat f,bool mip){Fault.At("texture");made.Add(this);}
  public void ReadPixels(Rect r,int x,int y){Fault.At("read");}
  public void Apply(){Fault.At("apply");}
  public byte[] EncodeToPNG(){Fault.At("encode");return new byte[]{137,80,78,71};}
 }
}
class CaptureHarness:UnityEngine.Object {
 CAPTURE_METHOD
 public string Capture(){return CaptureScreenshot();}
}
class Tests {
 static void Check(bool b,string why){if(!b)throw new Exception(why);}
 static void Run(string stage,bool nonNullActive,bool nonNullTarget){
  Fault.Stage=null;
  var previousActive=nonNullActive?new RenderTexture(640,480,24):null;
  var previousTarget=nonNullTarget?new RenderTexture(1280,720,24):null;
  Camera.main=new Camera{targetTexture=previousTarget};RenderTexture.active=previousActive;
  int start=RenderTexture.made.Count,texStart=Texture2D.made.Count;Fault.Stage=stage;
  string result=new CaptureHarness().Capture();Fault.Stage=null;
  Check((result!=null)==(stage==null),"capture result "+stage);
  Check(Camera.main.targetTexture==previousTarget,"camera target lost "+stage);
  Check(RenderTexture.active==previousActive,"active target lost "+stage);
  Check(Camera.main.enabled&&Camera.main.cullingMask==123&&Camera.main.fieldOfView==63,"camera state changed");
  Check(previousActive==null||(!previousActive.destroyed&&!previousActive.released),"caller active target freed");
  Check(previousTarget==null||(!previousTarget.destroyed&&!previousTarget.released),"caller camera target freed");
  for(int i=start;i<RenderTexture.made.Count;i++)Check(RenderTexture.made[i].released&&RenderTexture.made[i].destroyed,"owned GPU buffer leak");
  for(int i=texStart;i<Texture2D.made.Count;i++)Check(Texture2D.made[i].destroyed,"owned texture leak");
 }
 static void Main(){
  string[] stages={null,"rt","render","texture","read","apply","encode"};
  foreach(var stage in stages)foreach(bool active in new[]{false,true})foreach(bool target in new[]{false,true})Run(stage,active,target);
  Camera.main=null;var old=new RenderTexture(32,32,0);RenderTexture.active=old;
  Check(new CaptureHarness().Capture()==null,"missing camera must fail softly");Check(RenderTexture.active==old,"missing camera changed render state");
  Console.WriteLine("PASS: actual CaptureScreenshot; 28 success/failure/state combinations + missing camera; prior RTs restored and not freed; owned textures released");
 }
}
'''.replace('CAPTURE_METHOD',method)
with tempfile.TemporaryDirectory() as td:
    p=Path(td);(p/'test.cs').write_text(cs)
    subprocess.run(['mcs','-out:'+str(p/'test.exe'),str(p/'test.cs')],check=True)
    subprocess.run(['mono',str(p/'test.exe')],check=True)
