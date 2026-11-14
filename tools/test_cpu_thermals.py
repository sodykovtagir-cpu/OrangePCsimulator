#!/usr/bin/env python3
"""Compile and step the actual CPU.FixedUpdate. Engine/physics declarations are doubles."""
from pathlib import Path
import subprocess,tempfile
R=Path(__file__).resolve().parents[1]
code=r'''
using System;using System.Reflection;using System.Collections;
namespace UnityEngine {
 public class Object {public static GameObject Instantiate(GameObject g,Transform t){return g;}public static void Destroy(Object g,float t=0){}}
 public class Component:Object {public Transform transform=new Transform();public T GetComponent<T>()where T:class{return null;}}
 public class MonoBehaviour:Component{public void StartCoroutine(string s){}}
 public class GameObject:Object{}
 public class Transform {public Vector3 position,up;public bool IsChildOf(Transform t){return false;}public bool TryGetComponent<T>(out T v){v=default(T);return false;}public T GetComponentInParent<T>(){return default(T);}public T GetComponentInChildren<T>(){return default(T);}public T GetComponent<T>(){return default(T);}}
 public struct Vector3{}
 public struct LayerMask{public int value;}
 public struct Color {public float r,g,b,a;public Color(float r,float g,float b,float a){this.r=r;this.g=g;this.b=b;this.a=a;}}
 public class Renderer:Object{public Material material=new Material();}public class Material{public Color color;}
 public class Collider{public Transform transform;}
 public struct Ray{public Ray(Vector3 p,Vector3 u){}}
 public struct RaycastHit{public Transform transform;}public enum QueryTriggerInteraction{Collide}
 public static class Physics {public const int DefaultRaycastLayers=0;public static bool Raycast(Ray r,out RaycastHit h,float n,int m,QueryTriggerInteraction q){h=new RaycastHit();return false;}public static Collider[] OverlapSphere(Vector3 p,float r,int m,QueryTriggerInteraction q){return new Collider[0];}}
 public static class Time{public static float fixedDeltaTime=.02f,deltaTime=.02f;}
 public static class Mathf {public static float Max(float a,float b){return Math.Max(a,b);}public static float Min(float a,float b){return Math.Min(a,b);}public static float Clamp(float v,float a,float b){return Math.Max(a,Math.Min(b,v));}public static float Clamp01(float v){return Clamp(v,0,1);}public static float Lerp(float a,float b,float k){return a+(b-a)*Clamp01(k);}public static float InverseLerp(float a,float b,float v){return Clamp01((v-a)/(b-a));}public static float MoveTowards(float v,float t,float d){return v<t?Math.Min(t,v+d):Math.Max(t,v-d);}}
 [AttributeUsage(AttributeTargets.Field)]public class SerializeField:Attribute{}
}
public static class AirConditioner{public static float temperature=20;}
public class Achievement {public void Unlock(object o){}}public class CloudOnceManager {public static CloudOnceManager Instance=new CloudOnceManager();public Achievement GetAchievementFromId(string s){return null;}}
namespace PC.Component {public class Hardware:UnityEngine.MonoBehaviour{public bool Power,Damaged;public float Wattage=65;public virtual void Damage(){Damaged=true;}public virtual string GetInfo(){return "";}}}
namespace PC.Component.Cabling{public static class CablePower{public static void RefreshAll(){}}}
class Cooler:PC.Component.ICooler{public float Temperature{get;set;}}
class Test {
 static int n;static void Check(bool b){n++;if(!b)throw new Exception("CPU thermal "+n);}
 static void Step(PC.Component.CPU c){typeof(PC.Component.CPU).GetMethod("FixedUpdate",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(c,null);}
 static PC.Component.CPU Cpu(float load){return new PC.Component.CPU{Power=true,frequency=3,defaultFrequency=3,Workload=load};}
 static void Main(){
 var c=Cpu(1);bool throttled=false;int ticks=0;for(;ticks<60*50&&!c.Damaged;ticks++){Step(c);if(c.ThermalThrottleRatio<.9f)throttled=true;}Check(throttled&&c.Damaged&&c.temperature>=c.burnTemp);Check(ticks>100&&ticks<3000);
 c=Cpu(0);for(int i=0;i<180*50&&!c.Damaged;i++)Step(c);Check(c.temperature>80);
 c=Cpu(1);var cooler=new Cooler{Temperature=20};typeof(PC.Component.CPU).GetField("cooling",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(c,true);typeof(PC.Component.CPU).GetField("cooler",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(c,cooler);for(int i=0;i<60*50;i++){cooler.Temperature=20;Step(c);}Check(!c.Damaged&&c.temperature<40&&c.ThermalThrottleRatio>.99f);
 c=Cpu(0);c.Power=false;c.temperature=130;for(int i=0;i<60*50;i++)Step(c);Check(c.temperature<21&&c.Workload==0&&c.ThermalThrottleRatio==1);
 Console.WriteLine("PASS: "+n+" real CPU.FixedUpdate checks: bare full-load overheating/damage despite throttling, bare idle heat, working cooler and powered-off cooling");
 }
}
'''
with tempfile.TemporaryDirectory()as td:
 t=Path(td);(t/'test.cs').write_text(code);cpu=R/'Assets/Scripts/Assembly-CSharp/PC/Component'
 subprocess.run(['mcs','-out:'+str(t/'test.exe'),str(t/'test.cs'),str(cpu/'CPU.cs'),str(cpu/'ICooler.cs'),str(cpu/'CpuThermals.cs')],check=True)
 subprocess.run(['mono',str(t/'test.exe')],check=True)
