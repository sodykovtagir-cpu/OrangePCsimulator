#!/usr/bin/env python3
"""Compile actual resolution UI + bootstrap resolution code using UI/platform doubles."""
from pathlib import Path
import subprocess,tempfile
R=Path(__file__).resolve().parents[1];C=R/'Assets/Scripts/Assembly-CSharp'
b=(C/'GraphicsBootstrap.cs').read_text()
property_code=b[b.index('    public static Resolution NativeResolution'):b.index('\n\t[RuntimeInitializeOnLoadMethod')]
apply_code=b[b.index('\tpublic static void ApplyResolution()'):b.index('\n\tpublic static void ApplyRTX()')]
code=r'''
using System;using System.Collections.Generic;using System.Reflection;using UnityEngine;using UnityEngine.UI;
namespace UnityEngine {
 public class SerializeField:Attribute{}public class Header:Attribute{public Header(string s){}}
 public class Object{}
 public class Component:Object{public GameObject gameObject;public string name=>gameObject.name;public Transform transform=>gameObject.transform;public T GetComponentInParent<T>()where T:class{return null;}public T GetComponent<T>()where T:class{return gameObject.GetComponent<T>();}}
 public class MonoBehaviour:Component{}
 public class GameObject:Object{public string name;public Transform transform;public List<Component> components=new List<Component>();public bool activeSelf=true;public GameObject(string n){name=n;transform=new Transform{gameObject=this};components.Add(transform);}public void SetActive(bool b){activeSelf=b;}public T Add<T>()where T:Component,new(){var c=new T{gameObject=this};components.Add(c);return c;}public T GetComponent<T>()where T:class{foreach(var c in components)if(c is T)return c as T;return null;}}
 public class Transform:Component{public Transform parent;public List<Transform> children=new List<Transform>();public int childCount=>children.Count;public Transform root=>parent==null?this:parent.root;public void Add(Transform c){children.Add(c);c.parent=this;}public T GetComponentInChildren<T>(bool b)where T:class{var list=GetComponentsInChildren<T>(b);return list.Length>0?list[0]:null;}public T[] GetComponentsInChildren<T>(bool b)where T:class{var list=new List<T>();foreach(var c in gameObject.components)if(c is T)list.Add(c as T);foreach(var child in children)list.AddRange(child.GetComponentsInChildren<T>(b));return list.ToArray();}}
 public class Canvas:Component{}public class ToggleEvent:MonoBehaviour{}
 public struct Resolution{public int width,height,refreshRate;}
 public enum FullScreenMode{FullScreenWindow,Windowed}
 public static class Screen{public static Resolution currentResolution=new Resolution{width=2400,height=1080,refreshRate=120};public static Resolution[] resolutions=new[]{currentResolution};public static int width=2400,height=1080,calls;public static void SetResolution(int w,int h,bool b){calls++;width=w;height=h;currentResolution=new Resolution{width=w,height=h,refreshRate=120};}public static void SetResolution(int w,int h,FullScreenMode mode,int hz){SetResolution(w,h,true);}}
 public static class Application{public static bool isMobilePlatform=true;public static int targetFrameRate;}
 public static class QualitySettings{public static float resolutionScalingFixedDPIFactor;public static int vSyncCount;}
 public static class ScalableBufferManager{public static void ResizeBuffers(float a,float b){}}
 public static class Debug{public static void Log(object s){}}
 public static class Mathf{public static int Max(int a,int b){return Math.Max(a,b);}public static float Clamp(float v,float a,float b){return Math.Max(a,Math.Min(b,v));}public static int Clamp(int v,int a,int b){return Math.Max(a,Math.Min(b,v));}public static int RoundToInt(float v){return(int)Math.Round(v);}}
 public static class Time{public static float unscaledTime;}
 public static class PlayerPrefs{public static Dictionary<string,int> p=new Dictionary<string,int>();public static int saves;public static int GetInt(string s,int d){return p.ContainsKey(s)?p[s]:d;}public static void SetInt(string s,int v){p[s]=v;}public static void Save(){saves++;}}
}
namespace UnityEngine.UI {
 public class Event<T>{public List<Action<T>> handlers=new List<Action<T>>();public void AddListener(Action<T> a){handlers.Add(a);}public void Invoke(T v){foreach(var a in handlers.ToArray())a(v);}}
 public class Toggle:Component{public bool isOn;public Event<bool> onValueChanged=new Event<bool>();public void SetIsOnWithoutNotify(bool v){isOn=v;}}
 public class Text:Component{public string text="";}
 public class Slider:Component{public float minValue,maxValue;public bool wholeNumbers;public float value;public Event<float> onValueChanged=new Event<float>();public void SetValueWithoutNotify(float v){value=v;}public T[] GetComponentsInChildren<T>(bool b)where T:class{return transform.GetComponentsInChildren<T>(b);}}
 public class Dropdown:Component{public Event<int> onValueChanged=new Event<int>();public void ClearOptions(){}public void AddOptions(List<string> s){}public void SetValueWithoutNotify(int v){}}
}
public static class GraphicsBootstrap{private static Resolution nativeResolution;PROPERTY APPLY public static int pp;public static int ReflectionsDefault=>0;public static int TargetFpsDefault=>120;public static int TargetFps=>120;public static void ApplyRTX(){}public static void ApplyReflectionsQuality(){}public static void ApplyReflectionsToScene(){}public static void AttachLayersToCameras(){}public static void ApplyPostProcess(){pp++;}}
class Test{
 static int n;static void Check(bool v,string s){n++;if(!v)throw new Exception(s);}static object Call(object o,string name,params object[] a){return o.GetType().GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(o,a);}static void Field(object o,string name,object v){o.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(o,v);}
 static ResolutionSetting Menu(out Slider slider){var go=new GameObject("Settings");var ui=go.Add<ResolutionSetting>();var row=new GameObject("Resolution");go.transform.Add(row.transform);var sl=new GameObject("Slider");row.transform.Add(sl.transform);slider=sl.Add<Slider>();Field(ui,"mobileScaleSlider",slider);var label=new GameObject("label");row.transform.Add(label.transform);Field(ui,"mobileScaleLabel",label.Add<Text>());return ui;}
 static void Main(){
  var native=GraphicsBootstrap.NativeResolution;Check(native.width==2400,"initial native");
  Slider slider;var menu=Menu(out slider);Call(menu,"WireAll");Call(menu,"WireAll");Check(slider.onValueChanged.handlers.Count==1,"duplicate bindings");
  for(int i=0;i<100;i++){Time.unscaledTime=i*.01f;slider.onValueChanged.Invoke(1f-i*.0065f);Call(menu,"Update");}
  Check(Screen.calls==0&&PlayerPrefs.saves==0,"resolution changed/saved during drag");
  Time.unscaledTime=1.5f;Call(menu,"Update");Check(Screen.calls==1&&Screen.width>=840,"final drag did not coalesce");
  slider.onValueChanged.Invoke(.35f);Call(menu,"OnDisable");Check(Screen.width==840&&Screen.height==378,"minimum/aspect");
  Check(GraphicsBootstrap.NativeResolution.width==2400,"native shrank with render target");
  for(int i=0;i<8;i++){
   Slider next;var other=Menu(out next);Call(other,"WireAll");Check(Math.Abs(next.value-.35f)<.001,"saved scale not based on physical reference");
   next.onValueChanged.Invoke(.35f);Time.unscaledTime+=1;Call(other,"Update");Check(Screen.width==840&&Screen.height==378,"cumulative scene shrink");
  }
  int before=Screen.calls;GraphicsBootstrap.ApplyResolution();GraphicsBootstrap.ApplyResolution();Check(Screen.calls==before,"identical requests not deduped");
  slider.onValueChanged.Invoke(.5f);Call(menu,"OnDisable");Check(Screen.width==1200&&Screen.height==540,"resolution based on reduced currentResolution");
  PlayerPrefs.SetInt("ResWidth",320);PlayerPrefs.SetInt("ResHeight",240);GraphicsBootstrap.ApplyResolution();Check(Screen.width==840&&Screen.height==378,"bad saved aspect/resolution not recovered");
  PlayerPrefs.SetInt("PP_MotionBlur",1);Field(menu,"initializing",true);menu.SetMotionBlur(false);Check(PlayerPrefs.GetInt("PP_MotionBlur",0)==1,"UI initialization disabled blur");Field(menu,"initializing",false);
  menu.SetMotionBlur(false);Check(PlayerPrefs.GetInt("PP_MotionBlur",1)==0,"user cannot disable blur");
  var group=new GameObject("Postprocessing");var a=new GameObject("MotionBlur");var z=new GameObject("Bloom");group.transform.Add(a.transform);group.transform.Add(z.transform);var ta=a.Add<Toggle>();var tz=z.Add<Toggle>();a.Add<Text>().text="Motion Blur";z.Add<Text>().text="Bloom";
  var identify=typeof(ResolutionSetting).GetMethod("Identify",BindingFlags.NonPublic|BindingFlags.Static);
  Check(!((string)identify.Invoke(null,new object[]{tz})).Contains("motion"),"sibling motion label contaminated bloom");
  Check(!((string)identify.Invoke(null,new object[]{ta})).Contains("bloom"),"sibling bloom label contaminated motion");
  Console.WriteLine("PASS: "+n+" real resolution/UI checks: native reference, 100-event coalescing, scene reopen, damaged prefs, control isolation and init suppression");
 }
}
'''.replace('PROPERTY',property_code).replace('APPLY',apply_code)
with tempfile.TemporaryDirectory() as td:
 d=Path(td);(d/'test.cs').write_text(code)
 subprocess.run(['mcs','-define:UNITY_ANDROID','-out:'+str(d/'test.exe'),str(d/'test.cs'),str(C/'ResolutionSetting.cs')],check=True)
 subprocess.run(['mono',str(d/'test.exe')],check=True)

 # Also compile the desktop/fullscreen request path, without applying Android
 # aspect expectations to the simulated Windows build.
 subprocess.run(['mcs','-define:UNITY_STANDALONE_WIN','-out:'+str(d/'desktop.exe'),str(d/'test.cs'),str(C/'ResolutionSetting.cs')],check=True)
