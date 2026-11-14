#!/usr/bin/env python3
"""Compile real panel+manager with small TRS/UI doubles; not a Unity/device run."""
from pathlib import Path
import subprocess, tempfile, re
R=Path(__file__).resolve().parents[1]; C=R/'Assets/Scripts/Assembly-CSharp'
harness=r'''
using System;using System.Collections;using System.Collections.Generic;using System.Reflection;
namespace UnityEngine {
 public class HeaderAttribute:Attribute{public HeaderAttribute(string s){}} public class SerializeField:Attribute{} public class RangeAttribute:Attribute{public RangeAttribute(float a,float b){}}
 public struct Vector2{public float x,y;public Vector2(float a,float b){x=a;y=b;}public static Vector2 zero=>new Vector2();public static Vector2 one=>new Vector2(1,1);public static Vector2 operator+(Vector2 a,Vector2 b)=>new Vector2(a.x+b.x,a.y+b.y);public static Vector2 operator-(Vector2 a,Vector2 b)=>new Vector2(a.x-b.x,a.y-b.y);public static Vector2 operator*(Vector2 a,float b)=>new Vector2(a.x*b,a.y*b);public static bool operator==(Vector2 a,Vector2 b)=>Math.Abs(a.x-b.x)<.0001&&Math.Abs(a.y-b.y)<.0001;public static bool operator!=(Vector2 a,Vector2 b)=>!(a==b);public override bool Equals(object o)=>o is Vector2&&(Vector2)o==this;public override int GetHashCode()=>0;public static Vector2 Lerp(Vector2 a,Vector2 b,float t)=>a+(b-a)*t;public static implicit operator Vector3(Vector2 v)=>new Vector3(v.x,v.y,0);}
 public struct Vector3{public float x,y,z;public Vector3(float a,float b,float c){x=a;y=b;z=c;}public static Vector3 operator*(Vector3 a,float b)=>new Vector3(a.x*b,a.y*b,a.z*b);public static Vector3 operator+(Vector3 a,Vector3 b)=>new Vector3(a.x+b.x,a.y+b.y,a.z+b.z);public static Vector3 operator-(Vector3 a,Vector3 b)=>new Vector3(a.x-b.x,a.y-b.y,a.z-b.z);public static implicit operator Vector2(Vector3 v)=>new Vector2(v.x,v.y);}
 public struct Quaternion{public float z;public static Quaternion Euler(float a,float b,float c)=>new Quaternion{z=c};}
 public struct Rect{public float xMin,yMin,width,height;public Rect(float a,float b,float c,float d){xMin=a;yMin=b;width=c;height=d;}public float xMax=>xMin+width;public float yMax=>yMin+height;public Vector2 size=>new Vector2(width,height);public Vector2 center=>new Vector2(xMin+width/2,yMin+height/2);public static Rect MinMaxRect(float a,float b,float c,float d)=>new Rect(a,b,c-a,d-b);public static bool operator==(Rect a,Rect b)=>a.size==b.size&&a.center==b.center;public static bool operator!=(Rect a,Rect b)=>!(a==b);public override bool Equals(object o)=>o is Rect&&(Rect)o==this;public override int GetHashCode()=>0;}
 public static class Mathf{public static float Min(float a,float b)=>Math.Min(a,b);public static float Max(float a,float b)=>Math.Max(a,b);public static float Abs(float a)=>Math.Abs(a);public static float Clamp(float a,float b,float c)=>Math.Max(b,Math.Min(a,c));public static float Clamp01(float a)=>Clamp(a,0,1);public static float InverseLerp(float a,float b,float v)=>a==b?0:Clamp01((v-a)/(b-a));public static float Lerp(float a,float b,float t)=>a+(b-a)*t;}
 public class GameObject{public bool activeInHierarchy=true;public Transform transform;public void SetActive(bool b){activeInHierarchy=b;}public T AddComponent<T>()where T:new()=>new T();}
 public class Transform{public Transform parent;public GameObject gameObject;public Vector3 localScale=new Vector3(1,1,1);public Quaternion localRotation;public Vector3 localPosition;public virtual Vector3 LocalPosition=>localPosition;public Vector3 position=>TransformPoint(new Vector3());
 public Vector3 TransformVector(Vector3 p){float a=localRotation.z*(float)Math.PI/180,c=(float)Math.Cos(a),s=(float)Math.Sin(a);p=new Vector3(p.x*localScale.x,p.y*localScale.y,p.z*localScale.z);p=new Vector3(c*p.x-s*p.y,s*p.x+c*p.y,p.z);return parent==null?p:parent.TransformVector(p);}
 public Vector3 InverseTransformVector(Vector3 p){if(parent!=null)p=parent.InverseTransformVector(p);float a=-localRotation.z*(float)Math.PI/180,c=(float)Math.Cos(a),s=(float)Math.Sin(a);return new Vector3((c*p.x-s*p.y)/localScale.x,(s*p.x+c*p.y)/localScale.y,p.z/localScale.z);}
 public Vector3 TransformPoint(Vector3 p){var pos=parent==null?LocalPosition:parent.TransformPoint(LocalPosition);return pos+TransformVector(p);}public Vector3 InverseTransformPoint(Vector3 p)=>InverseTransformVector(p-position);
 public T GetComponent<T>()where T:class=>this as T;}
 public class RectTransform:Transform{public Vector2 pivot=new Vector2(.5f,.5f),anchorMin=new Vector2(.5f,.5f),anchorMax=new Vector2(.5f,.5f),anchoredPosition,sizeDelta;public Rect rect{get{var p=parent as RectTransform;float w=sizeDelta.x+(p==null?0:p.rect.width*(anchorMax.x-anchorMin.x)),h=sizeDelta.y+(p==null?0:p.rect.height*(anchorMax.y-anchorMin.y));return new Rect(-pivot.x*w,-pivot.y*h,w,h);}}public override Vector3 LocalPosition{get{var p=parent as RectTransform;if(p==null)return anchoredPosition;return new Vector2(p.rect.xMin+p.rect.width*anchorMin.x,p.rect.yMin+p.rect.height*anchorMin.y)+anchoredPosition;}}
 public void GetWorldCorners(Vector3[] c){var r=rect;c[0]=TransformPoint(new Vector2(r.xMin,r.yMin));c[1]=TransformPoint(new Vector2(r.xMin,r.yMax));c[2]=TransformPoint(new Vector2(r.xMax,r.yMax));c[3]=TransformPoint(new Vector2(r.xMax,r.yMin));}public T[] GetComponentsInChildren<T>(bool b)=>new T[0];}
 public class Coroutine{}public class WaitForEndOfFrame{} public class MonoBehaviour{public Transform transform;public GameObject gameObject;public string name="test";public T GetComponent<T>()where T:class{if(typeof(T)==typeof(RectTransform))return transform as T;return null;}public T GetComponentInParent<T>()where T:class=>null;public T[] GetComponentsInChildren<T>(bool b)=>new T[0];public Coroutine StartCoroutine(IEnumerator e)=>new Coroutine();public void StopCoroutine(Coroutine c){}}
 public class Canvas:MonoBehaviour{}public class CanvasGroup{public bool interactable,blocksRaycasts;public float alpha;}
 public static class Debug{public static void Log(object a){}public static void LogError(object a){}}
 public static class Screen{public static int width=1568,height=706;}public static class Time{public static float unscaledDeltaTime=.016f;}
 public static class PlayerPrefs{public static bool HasKey(string s)=>false;public static int GetInt(string s)=>0;public static float GetFloat(string s)=>0;public static void SetInt(string s,int v){}public static void SetFloat(string s,float v){}public static void Save(){}public static void DeleteKey(string s){}}
 public static class RectTransformUtility{public static bool ScreenPointToLocalPointInRectangle(RectTransform r,Vector2 p,object c,out Vector2 q){q=p;return true;}}
}
namespace UnityEngine.EventSystems{public interface IDragHandler{}public interface IEndDragHandler{}public class PointerEventData{public UnityEngine.Vector2 position;public object pressEventCamera;}}
namespace UnityEngine.UI{public class Button{public UnityEngine.Transform transform;}public static class LayoutRebuilder{public static int rebuilds;public static void ForceRebuildLayoutImmediate(UnityEngine.RectTransform r){rebuilds++;}}}
public class MobileFreeDraggable{public void ResetPosition(){}public void SavePosition(){}public void LoadPosition(){}}
public class DragRotateControls{public static void BuildPreview(UnityEngine.RectTransform r){}public static void SetPreviewCatcher(UnityEngine.RectTransform r,bool b){}}
class Test{
 static int n;static void Check(bool b,string s){n++;if(!b)throw new Exception(s);}
 static object Call(object o,string s,params object[] a)=>o.GetType().GetMethod(s,BindingFlags.NonPublic|BindingFlags.Instance).Invoke(o,a);
 static void Set(object o,string s,object v)=>o.GetType().GetField(s,BindingFlags.NonPublic|BindingFlags.Instance).SetValue(o,v);
 static UnityEngine.RectTransform Rect(float w,float h,UnityEngine.Transform p=null){var g=new UnityEngine.GameObject();var r=new UnityEngine.RectTransform{sizeDelta=new UnityEngine.Vector2(w,h),parent=p,gameObject=g};g.transform=r;return r;}
 static void Main(){
 foreach(var sz in new[]{new[]{1110f,500f},new[]{800f,500f},new[]{500f,1110f},new[]{320f,180f},new[]{160f,90f}})
 foreach(int e in new[]{0,1,2,3})foreach(int slot in new[]{0,1,2})foreach(float t in new[]{0f,.1f,.5f,.9f,1f})foreach(bool nested in new[]{false,true}){
 var bounds=Rect(sz[0],sz[1]);var parent=nested?Rect(sz[0]*.8f,sz[1]*.9f,bounds):bounds;
 if(nested){parent.localScale=new UnityEngine.Vector3(.8f,1.2f,1);parent.anchoredPosition=new UnityEngine.Vector2(-23,31);}
 var r=Rect(560,80,parent);var panel=new MobileAdaptiveSlidePanel{transform=r,gameObject=r.gameObject};
 Set(panel,"parentRect",bounds);Set(panel,"edge",(MobileAdaptiveSlidePanel.SnapEdge)e);Set(panel,"edgeT",t);
 var f=typeof(MobileAdaptiveSlidePanel).GetField("edgeAnchorSlot",BindingFlags.NonPublic|BindingFlags.Instance);f.SetValue(panel,Enum.ToObject(f.FieldType,slot));
 Call(panel,"ConfigureForEdge");r.anchoredPosition=(UnityEngine.Vector2)Call(panel,"GetOpenAnchoredPosition");
 var b=(UnityEngine.Rect)Call(panel,"MeasureBounds");var v=(UnityEngine.Rect)Call(panel,"PaddedViewport");
 Check(b.xMin>=v.xMin-.01&&b.xMax<=v.xMax+.01&&b.yMin>=v.yMin-.01&&b.yMax<=v.yMax+.01,"open outside viewport");
 Check(Math.Abs(e==0?b.xMin-v.xMin:e==1?b.xMax-v.xMax:e==2?b.yMax-v.yMax:b.yMin-v.yMin)<.01,"gap at dock edge");
 var scale=r.localScale;Call(panel,"ConfigureForEdge");Check(Math.Abs(r.localScale.x-scale.x)<.0001,"cumulative shrink");
 r.anchoredPosition=(UnityEngine.Vector2)Call(panel,"GetClosedAnchoredPosition");b=(UnityEngine.Rect)Call(panel,"MeasureBounds");var view=bounds.rect;
 Check(e==0?b.xMax<view.xMin:e==1?b.xMin>view.xMax:e==2?b.yMin>view.yMax:b.yMax<view.yMin,"closed panel not offscreen");
 Set(panel,"opened",true);Call(panel,"ApplyInstant");int count=UnityEngine.UI.LayoutRebuilder.rebuilds;Call(panel,"LateUpdate");Check(count==UnityEngine.UI.LayoutRebuilder.rebuilds,"idle frame rebuilt layout");
 bounds.sizeDelta=new UnityEngine.Vector2(sz[1],sz[0]);Call(panel,"LateUpdate");b=(UnityEngine.Rect)Call(panel,"MeasureBounds");v=(UnityEngine.Rect)Call(panel,"PaddedViewport");Check(b.xMin>=v.xMin-.01&&b.xMax<=v.xMax+.01&&b.yMin>=v.yMin-.01&&b.yMax<=v.yMax+.01,"resize outside viewport");
 }
 foreach(int w in new[]{706,1280,1568,2340,3200}){UnityEngine.Screen.width=w;UnityEngine.Screen.height=706;var area=Rect(840,500);var r=Rect(840,500,area);var m=new MobileCustomizeManager();Set(m,"previewPanel",r.gameObject);Call(m,"FitPreviewToScreen");Check(Math.Abs(r.rect.width/r.rect.height-(float)w/706)<.0001,"preview aspect mismatch");Check(r.rect.width*r.localScale.x<=840.01&&r.rect.height*r.localScale.y<=500.01,"preview outside shell");Check(r.rect.width>=799.99&&r.rect.height>=499.99,"HUD Expand units mismatch");}
 Console.WriteLine("PASS: "+n+" real-source toolbar fit/edge/corner/resize/idle and preview-aspect checks (UI doubles)");
 }}
'''
with tempfile.TemporaryDirectory() as td:
 p=Path(td);(p/'test.cs').write_text(harness)
 subprocess.run(['mcs','-out:'+str(p/'test.exe'),str(p/'test.cs'),str(C/'MobileAdaptiveSlidePanel.cs'),str(C/'MobileCustomizeManager.cs')],check=True)
 subprocess.run(['mono',str(p/'test.exe')],check=True)
# Preview scene has the same 8 fixed-size children as the real toolbar.
def blocks(text):
 return {m[0]:m[1] for m in re.findall(r'--- !u!\d+ &(-?\d+)\n(.*?)(?=--- !u!|\Z)',text,re.S)}
s=blocks((R/'Assets/Scenes/Menu.unity').read_text());g=blocks((R/'Assets/Scenes/Main.unity').read_text())
def sizes(d,k):
 out={}
 for c in re.findall(r'- \{fileID: (\d+)\}',d[k]):
  b=d[c];go=re.search(r'm_GameObject: \{fileID: (\d+)\}',b)[1]
  out[re.search(r'm_Name: (.*)',d[go])[1]]=re.search(r'm_SizeDelta: (.*)',b)[1]
 return out
assert sizes(s,'1543539592')==sizes(g,'505')
assert 'controlsRoot: {fileID: 1896168228}' in (R/'Assets/Scenes/Menu.unity').read_text()
assert 'Canvas.ForceUpdateCanvases()' not in (C/'MobileAdaptiveSlidePanel.cs').read_text()
print('PASS: authored preview has all 8 buttons and real HUD sizes; grid preview shares fitted viewport')
