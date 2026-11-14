#!/usr/bin/env python3
"""Compile real integration C# with lifecycle/API doubles, not a Unity Player test."""
from pathlib import Path
import argparse, json, re, subprocess, tempfile
parser=argparse.ArgumentParser();parser.add_argument("--export-exposure",type=Path);args=parser.parse_args()
R=Path(__file__).resolve().parents[1]
C=R/'Assets/Scripts/Assembly-CSharp'
bootstrap=(C/'GraphicsBootstrap.cs').read_text()
setup=bootstrap[bootstrap.index('\tprivate static void SetupCamera('):bootstrap.index('\n\tpublic static void ApplyPostProcess()')]
harness=r'''
using System;using System.Collections.Generic;using System.Reflection;
using UnityEngine;using UnityEngine.Rendering.PostProcessing;
namespace UnityEngine {
 public class Object {public HideFlags hideFlags;public static void Destroy(object o){}public static void DontDestroyOnLoad(object o){}}
 public enum HideFlags{HideAndDontSave}
 public class RequireComponent:Attribute{public RequireComponent(Type t){}}
 public class DisallowMultipleComponent:Attribute{}
 public class TooltipAttribute:Attribute{public TooltipAttribute(string s){}}
 public class RangeAttribute:Attribute{public RangeAttribute(float a,float b){}}
 public class ContextMenu:Attribute{public ContextMenu(string s){}}
 public class DefaultExecutionOrder:Attribute{public DefaultExecutionOrder(int n){}}
 public static class Events{public static void Call(object o,string s){var m=o.GetType().GetMethod(s,BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public);if(m!=null)m.Invoke(o,null);}}
 public class Component:Object{public GameObject gameObject;public Transform transform=>gameObject.transform;public T GetComponent<T>()where T:class{return gameObject.GetComponent<T>();}}
 public class MonoBehaviour:Component{bool state=true;public bool enabled{get{return state;}set{if(state==value)return;state=value;Events.Call(this,value?"OnEnable":"OnDisable");}}}
 public class Transform{public Vector3 position;public Quaternion rotation;}
 public class GameObject:Object{public string name;public int layer;public Transform transform=new Transform();readonly List<Component> components=new List<Component>();public static int volumes;public GameObject(string n="test"){name=n;}public T GetComponent<T>()where T:class{foreach(var c in components)if(c is T)return c as T;return null;}public T AddComponent<T>()where T:Component,new(){var c=new T();c.gameObject=this;components.Add(c);if(c is PostProcessVolume)volumes++;Events.Call(c,"OnEnable");return c;}}
 public class ScriptableObject:Object{public static T CreateInstance<T>()where T:new(){return new T();}}
 public struct Vector3{public float x,y,z;public Vector3(float a,float b,float c){x=a;y=b;z=c;}public static float Distance(Vector3 a,Vector3 b){return(float)Math.Sqrt((a.x-b.x)*(a.x-b.x)+(a.y-b.y)*(a.y-b.y)+(a.z-b.z)*(a.z-b.z));}}
 public struct Quaternion{public float yaw;public static float Angle(Quaternion a,Quaternion b){return Math.Abs(a.yaw-b.yaw);}}
 public static class Mathf{public static float Clamp(float v,float a,float b){return Math.Max(a,Math.Min(b,v));}public static int Clamp(int v,int a,int b){return Math.Max(a,Math.Min(b,v));}public static float Max(float a,float b){return Math.Max(a,b);}public static float Abs(float v){return Math.Abs(v);}public static int Max(int a,int b){return Math.Max(a,b);}}
 [Flags]public enum DepthTextureMode{None=0,Depth=1,MotionVectors=4}
 public enum RenderTextureFormat{Default,RGHalf}
 public class Camera:MonoBehaviour{public float fieldOfView=60;public int pixelWidth=1920,pixelHeight=1080;public RenderTexture targetTexture;public bool allowHDR,allowMSAA;public DepthTextureMode depthTextureMode;}
 public class RenderTexture{public int width=1920,height=1080,format;public static RenderTexture GetTemporary(int a,int b,int c,int d){return new RenderTexture{width=a,height=b};}public static void ReleaseTemporary(RenderTexture r){}}
 public class Shader{public bool isSupported=true;public static Shader Find(string s){return new Shader();}}
 public class Material{public Material(Shader s){}public void SetFloat(string s,float v){}public void SetTexture(string s,object o){}}
 public static class Graphics{public static void Blit(object a,object b){}public static void Blit(object a,object b,object c,int d){}}
 public static class Resources{public static bool available=true;public static T Load<T>(string s)where T:class,new(){return available?new T():null;}}
 public static class SystemInfo{public static bool supportsMotionVectors=true,half=true;public static bool SupportsRenderTextureFormat(RenderTextureFormat f){return half;}}
 public static class Debug{public static int warnings;public static void LogWarning(string s){warnings++;}public static void Log(string s){}}
 public static class Time{public static float timeScale=1,unscaledDeltaTime=1f/180;public static int frameCount;}
 public static class PlayerPrefs{public static Dictionary<string,int> p=new Dictionary<string,int>();public static int GetInt(string s,int d){return p.ContainsKey(s)?p[s]:d;}}
}
namespace UnityEngine.Rendering.PostProcessing {
 public class Parameter<T>{public T value;public bool overrideState;public void Override(T v){value=v;overrideState=true;}}
 public class MotionBlur:ScriptableObject{public Parameter<bool> enabled=new Parameter<bool>();public Parameter<float> shutterAngle=new Parameter<float>();public Parameter<int> sampleCount=new Parameter<int>();}
 public class PostProcessProfile:ScriptableObject{public MotionBlur blur;public T AddSettings<T>()where T:new(){var v=new T();blur=v as MotionBlur;return v;}}
 public class PostProcessVolume:MonoBehaviour{public bool isGlobal;public float priority,weight;public PostProcessProfile sharedProfile;}
 public class ShaderResources{public Shader motionBlur=new Shader();}
 public class PostProcessResources:ScriptableObject{public ShaderResources shaders=new ShaderResources();}
 public class Fog{public bool enabled=true;}
 public class PostProcessLayer:MonoBehaviour{public enum Antialiasing{None,TemporalAntialiasing}public int volumeLayer;public Transform volumeTrigger;public Antialiasing antialiasingMode;public bool finalBlitToCameraTarget=true;public Fog fog=new Fog();public PostProcessResources resources;public int resets;public void Init(PostProcessResources r){resources=r;}public void ResetHistory(){resets++;}public T GetSettings<T>()where T:new(){return new T();}}
}
static class Bootstrap { SETUP public static void Run(Camera c){SetupCamera(c);}}
class Test {
 static int n;static void Check(bool b,string s){n++;if(!b)throw new Exception(s);}
 static Camera NewCamera(){return new GameObject().AddComponent<Camera>();}
 static MotionBlur Settings(){return (MotionBlur)typeof(StockMotionBlur).GetField("settings",BindingFlags.NonPublic|BindingFlags.Static).GetValue(null);}
 static bool Frame(Camera c){Time.frameCount++;Events.Call(c.GetComponent<StockMotionBlur>(),"OnPreCull");return Settings().enabled.value;}
 static void Main(){
  var c=NewCamera();Bootstrap.Run(c);
  Check(c.GetComponent<StockMotionBlur>()==null&&c.GetComponent<PostProcessLayer>()==null&&c.GetComponent<SimpleScreenFx>()==null,"off allocated post-processing");
  PlayerPrefs.p["PP_MotionBlur"]=1;Bootstrap.Run(c);var layer=c.GetComponent<PostProcessLayer>();
  Check(layer!=null&&layer.enabled,"stock layer missing");Check(c.GetComponent<SimpleScreenFx>()==null,"second blur/image pass created");
  Check(layer.resources!=null&&!layer.finalBlitToCameraTarget,"resources/chain wrong");
  Check(layer.antialiasingMode==PostProcessLayer.Antialiasing.None&&!layer.fog.enabled,"unrequested AA/fog");
  Check(Math.Abs(Settings().shutterAngle.value-1620)<.001&&Settings().sampleCount.value==16,"wrong baseline");
  Check(Settings().shutterAngle.overrideState&&Settings().sampleCount.overrideState,"settings not overridden");
  Check(c.depthTextureMode==(DepthTextureMode.Depth|DepthTextureMode.MotionVectors),"native camera motion/depth not requested");
  Check(!Frame(c)&&Frame(c),"initial reset/stationary native pipeline");
  c.transform.rotation=new Quaternion{yaw=.1f};Check(Frame(c),"rotation without walking disabled blur");
  c.transform.position=new Vector3(0,0,.1f);Check(Frame(c),"walking disabled blur");
  foreach(int fps in new[]{30,60,90,120,144,180,240}){
   Time.unscaledDeltaTime=1f/fps;c.transform.rotation=new Quaternion{yaw=c.transform.rotation.yaw+.1f};Check(Frame(c),"normal turn considered cut");
   Check(Math.Abs(Settings().shutterAngle.value-270f*fps/30f)<.001,"blur exposure changes with FPS");
   Console.WriteLine("EXPOSURE_CASE "+fps+" "+Settings().shutterAngle.value.ToString(System.Globalization.CultureInfo.InvariantCulture));
  }
  Time.unscaledDeltaTime=1f/30;
  var controls=c.GetComponent<StockMotionBlur>();
  controls.shutterAngle=360;controls.sampleCount=24;Check(Frame(c)&&Math.Abs(Settings().shutterAngle.value-360)<.001&&Settings().sampleCount.value==24,"live Inspector controls not applied");
  controls.shutterAngle=999;controls.sampleCount=100;Frame(c);Check(Math.Abs(Settings().shutterAngle.value-360)<.001&&Settings().sampleCount.value==32,"Inspector upper limits ignored");
  controls.shutterAngle=-100;controls.sampleCount=-5;Frame(c);Check(Settings().shutterAngle.value==0&&Settings().sampleCount.value==4,"Inspector lower limits ignored");
  controls.shutterAngle=270;controls.sampleCount=16;Frame(c);Events.Call(controls,"PrintDiagnostics");
  Time.timeScale=0;Check(!Frame(c),"pause blur");Time.timeScale=1;Check(!Frame(c)&&Frame(c),"resume not reset");
  c.transform.position=new Vector3(0,0,20);Check(!Frame(c)&&Frame(c),"teleport not reset");
  c.transform.rotation=new Quaternion{yaw=90};Check(!Frame(c)&&Frame(c),"camera cut not reset");
  c.fieldOfView=90;Check(!Frame(c)&&Frame(c),"FOV not reset");
  c.pixelWidth=1280;Check(!Frame(c)&&Frame(c),"resize not reset");
  Time.frameCount+=4;Check(!Frame(c)&&Frame(c),"missing frame not reset");
  Time.unscaledDeltaTime=.2f;Check(!Frame(c),"stall not reset");Time.unscaledDeltaTime=1f/180;Check(Frame(c),"stall recovery disabled");
  c.targetTexture=new RenderTexture();Check(!Frame(c),"thumbnail blurred");c.targetTexture=null;Check(!Frame(c)&&Frame(c),"thumbnail contaminated normal camera");
  c.pixelHeight=16;Check(!Frame(c),"tiny target allowed invalid stock tile size");c.pixelHeight=1080;Check(!Frame(c)&&Frame(c),"tiny target recovery");
  int volumes=GameObject.volumes;Bootstrap.Run(c);Check(GameObject.volumes==volumes,"ApplyAll allocated duplicate volume");
  var second=NewCamera();Bootstrap.Run(second);Check(GameObject.volumes==volumes,"per-camera duplicate global profiles");
  Check(!Frame(second)&&Frame(second),"second camera not initialized");
  second.targetTexture=new RenderTexture();Check(!Frame(second),"second camera RT blur");Check(!Frame(c)&&Frame(c),"second camera profile contaminated main camera");
  PlayerPrefs.p["PP_MotionBlur"]=0;Bootstrap.Run(c);Check(!layer.enabled&&!c.allowHDR&&c.depthTextureMode==DepthTextureMode.None,"off retained costs");
  PlayerPrefs.p["PP_Bloom"]=1;Bootstrap.Run(c);var simple=c.GetComponent<SimpleScreenFx>();Check(simple!=null&&simple.enabled&&simple.bloom&&!layer.enabled,"bloom affected by blur off");
  PlayerPrefs.p["PP_MotionBlur"]=1;Bootstrap.Run(c);Check(layer.enabled&&simple.enabled,"combined stack missing effect");
  Check(!Frame(c)&&Frame(c),"reenable did not reset");
  PlayerPrefs.p["PP_Bloom"]=0;Bootstrap.Run(c);Check(!simple.enabled&&layer.enabled,"blur-only kept simple image pass");
  var monitor=NewCamera();monitor.targetTexture=new RenderTexture();Bootstrap.Run(monitor);Check(monitor.GetComponent<StockMotionBlur>()==null,"monitor modified");
  SystemInfo.supportsMotionVectors=false;var unsupported=NewCamera();Bootstrap.Run(unsupported);int warnings=Debug.warnings;Bootstrap.Run(unsupported);
  Check(unsupported.GetComponent<PostProcessLayer>()==null&&!unsupported.allowHDR,"fake fallback on unsupported GPU");Check(Debug.warnings==warnings,"warning spam");
  SystemInfo.supportsMotionVectors=true;SystemInfo.half=false;var noHalf=NewCamera();Bootstrap.Run(noHalf);Check(noHalf.GetComponent<PostProcessLayer>()==null,"unsupported RGHalf");SystemInfo.half=true;
  var f=typeof(StockMotionBlur).GetField("resources",BindingFlags.Static|BindingFlags.NonPublic);f.SetValue(null,null);Resources.available=false;
  var noResources=NewCamera();Bootstrap.Run(noResources);Check(noResources.GetComponent<PostProcessLayer>()==null,"missing build resources ignored");Resources.available=true;
  f.SetValue(null,new PostProcessResources());((PostProcessResources)f.GetValue(null)).shaders.motionBlur.isSupported=false;
  var noShader=NewCamera();Bootstrap.Run(noShader);Check(noShader.GetComponent<PostProcessLayer>()==null,"unsupported shader ignored");
  Events.Call(c.GetComponent<StockMotionBlur>(),"OnDisable");Events.Call(c.GetComponent<StockMotionBlur>(),"OnDestroy");
  Check(Settings()!=null,"destroying one camera removed shared profile of another");
  Events.Call(second.GetComponent<StockMotionBlur>(),"OnDisable");Events.Call(second.GetComponent<StockMotionBlur>(),"OnDestroy");
  Check(Settings()==null,"last camera leaked runtime profile");
  f.SetValue(null,new PostProcessResources());var afterScene=NewCamera();Bootstrap.Run(afterScene);
  Check(afterScene.GetComponent<PostProcessLayer>()!=null&&Math.Abs(Settings().shutterAngle.value-1620)<.001,"new scene did not recreate stock profile");
  Console.WriteLine("PASS: "+n+" real integration C# checks with lifecycle/API doubles (not Unity rendering)");
 }
}
'''.replace('SETUP',setup)
with tempfile.TemporaryDirectory() as td:
 d=Path(td); (d/'test.cs').write_text(harness)
 subprocess.run(['mcs','-out:'+str(d/'test.exe'),str(d/'test.cs'),str(C/'StockMotionBlur.cs'),str(C/'SimpleScreenFx.cs')],check=True)
 result=subprocess.run(['mono',str(d/'test.exe')],check=True,text=True,capture_output=True)
 exposure={int(fps):float(angle) for fps,angle in re.findall(r'^EXPOSURE_CASE (\d+) ([0-9.eE+-]+)$',result.stdout,re.M)}
 print('\n'.join(line for line in result.stdout.splitlines() if not line.startswith('EXPOSURE_CASE ')))
 if args.export_exposure:args.export_exposure.write_text(json.dumps(exposure))
# Runtime resource references must resolve into the exact installed package.
asset=R/'Assets/Resources/OrangePCPostProcessResources.asset'
assert asset.exists() and asset.with_suffix('.asset.meta').exists()
assert '"com.unity.postprocessing": "3.4.0"' in (R/'Packages/manifest.json').read_text()
assert 'StockMotionBlur.Configure(cam, motionBlur)' in bootstrap
simple=(C/'SimpleScreenFx.cs').read_text();shader=(R/'Assets/Resources/SimpleScreenFx.shader').read_text()
for old in ['_FallbackMotion','_Shutter','_Motion','_CameraMotionVectorsTexture','ConfigureMotion','velocityRate']:
 assert old not in simple+shader,('custom blur survived',old)
assert 'DefaultExecutionOrder(-10000)' in (C/'StockMotionBlur.cs').read_text()
print('PASS: package pin, Resources inclusion, callback ordering and removal of old blur')
