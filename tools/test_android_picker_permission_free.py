#!/usr/bin/env python3
"""Compile actual Android plugin/callbacks against JNI/lifecycle doubles.
Checks native AAR exposes the mode, not an Android activity/device UI test.
"""
from pathlib import Path
import re, subprocess,tempfile,zipfile
R=Path(__file__).resolve().parents[1];P=R/'Assets/Plugins/NativeFilePicker'
with zipfile.ZipFile(P/'Android/NativeFilePicker.aar') as z:
 with zipfile.ZipFile(__import__('io').BytesIO(z.read('classes.jar'))) as jar:
  main=jar.read('com/yasirkula/unity/NativeFilePicker.class')
  assert b'PermissionFreeMode' in main and b'UseDefaultFilePickerApp' in main
  fragment=jar.read('com/yasirkula/unity/NativeFilePickerPickFragment.class')
  assert b'android.intent.action.OPEN_DOCUMENT' in fragment and b'getCacheDir' in fragment
harness=r'''
using System;using System.Collections.Generic;using System.Reflection;using NativeFilePickerNamespace;
namespace UnityEngine {
 namespace Scripting{public class Preserve:Attribute{}}
 public class Object{public static void DontDestroyOnLoad(object o){}public static void Destroy(object o){((GameObject)o).dead=true;}}
 public class MonoBehaviour:Object{public GameObject gameObject;}
 public class GameObject:Object{public bool dead;public List<MonoBehaviour> components=new List<MonoBehaviour>();public static List<GameObject> all=new List<GameObject>();public GameObject(string n){all.Add(this);}public T AddComponent<T>()where T:MonoBehaviour,new(){var c=new T{gameObject=this};components.Add(c);return c;}public static void Pump(){foreach(var go in all.ToArray())if(!go.dead)foreach(var c in go.components)c.GetType().GetMethod("Update",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(c,null);}}
 public static class Application{public static string temporaryCachePath=System.IO.Path.Combine(System.IO.Path.GetTempPath(),"opc-picker-test");}
 public class AndroidJavaProxy{public AndroidJavaProxy(string n){}}
 public class AndroidJavaObject:IDisposable{public void Dispose(){}public T GetStatic<T>(string s){return (T)(object)new AndroidJavaObject();}}
 public class AndroidJavaClass:AndroidJavaObject{
  public static bool free,systemPicker;public static int picks,requests;public static string chosen="/cache/world.opc";
  public AndroidJavaClass(string n){}
  public void SetStatic<T>(string name,T v){if(name=="PermissionFreeMode")free=(bool)(object)v;if(name=="UseDefaultFilePickerApp")systemPicker=(bool)(object)v;}
  public T CallStatic<T>(string name,params object[] a){if(name=="CheckPermission")return (T)(object)(free?1:0);if(typeof(T)==typeof(bool))return (T)(object)true;return default(T);}
  public void CallStatic(string name,params object[] a){
   if(name=="RequestPermission"){requests++;((FPPermissionCallbackAndroid)a[1]).OnPermissionResult(free?1:0);}
   if(name=="PickFiles"){if(!free)throw new Exception("broad storage permission gate still active");picks++;((FPResultCallbackAndroid)a[1]).OnFilePicked(chosen);}
  }
 }
}
class Test{
 static int n;static void Check(bool b,string s){n++;if(!b)throw new Exception(s);}
 static void Pump(){for(int i=0;i<3;i++)UnityEngine.GameObject.Pump();}
 static void Main(){
  Check(NativeFilePicker.CheckPermission(true),"SAF selection blocked by storage permission");
  Check(UnityEngine.AndroidJavaClass.free&&UnityEngine.AndroidJavaClass.systemPicker,"AAR modes not initialized");
  int calls=0;string result=null;NativeFilePicker.PickFile(p=>{calls++;result=p;},"*/*");
  Check(calls==0,"JNI callback was not marshalled through main-thread helper");Pump();
  Check(calls==1&&result=="/cache/world.opc"&&UnityEngine.AndroidJavaClass.picks==1,"picker did not launch/deliver result");Pump();Check(calls==1,"duplicate callback");
  UnityEngine.AndroidJavaClass.chosen="";NativeFilePicker.PickFile(p=>{calls++;result=p;},"*/*");Pump();Check(calls==2&&result==null,"cancel not delivered");
  var task=NativeFilePicker.RequestPermissionAsync(true);Pump();Check(task.IsCompleted&&task.Result==NativeFilePicker.Permission.Granted,"Task permission callback hung");
  Console.WriteLine("PASS: "+n+" Android picker JNI/callback checks + actual AAR modes; native MIUI activity still needs device validation");
 }
}
'''
with tempfile.TemporaryDirectory() as td:
 d=Path(td);(d/'test.cs').write_text(harness)
 # mcs does not implement the package's target-typed new syntax; only expand it.
 source=(P/'NativeFilePicker.cs').read_text(encoding='utf-8-sig').replace('FPPermissionCallbackAndroid nativeCallback = new( callback );','FPPermissionCallbackAndroid nativeCallback = new FPPermissionCallbackAndroid( callback );')
 (d/'picker.cs').write_text(source)
 subprocess.run(['mcs','-define:UNITY_ANDROID','-out:'+str(d/'test.exe'),str(d/'test.cs'),str(d/'picker.cs'),*[str(P/'Android'/s) for s in ['FPPermissionCallbackAndroid.cs','FPResultCallbackAndroid.cs','FPCallbackHelper.cs']]],check=True)
 subprocess.run(['mono',str(d/'test.exe')],check=True)
