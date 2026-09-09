// Minimal headless Unity substitutes. Not part of the game's Assets/ assembly.
using System;
using System.Collections.Generic;
using System.Reflection;

namespace UnityEngine
{
    public class SerializeField : Attribute { }
    public class HeaderAttribute : Attribute { public HeaderAttribute(string text) { } }
    public struct Vector2
    {
        public float x, y;
        public Vector2(float x, float y) { this.x = x; this.y = y; }
        public static Vector2 zero => new Vector2(0, 0);
        public static Vector2 operator +(Vector2 a, Vector2 b) => new Vector2(a.x+b.x, a.y+b.y);
        public static Vector2 operator -(Vector2 a, Vector2 b) => new Vector2(a.x-b.x, a.y-b.y);
        public static Vector2 operator /(Vector2 a, float b) => new Vector2(a.x/b, a.y/b);
        public static implicit operator Vector2(Vector3 a) => new Vector2(a.x, a.y);
        public static bool operator ==(Vector2 a, Vector2 b) => Distance(a,b) < 0.00001f;
        public static bool operator !=(Vector2 a, Vector2 b) => !(a==b);
        public static float Distance(Vector2 a, Vector2 b) => (float)Math.Sqrt((a.x-b.x)*(a.x-b.x)+(a.y-b.y)*(a.y-b.y));
        public override bool Equals(object value) => value is Vector2 && this == (Vector2)value;
        public override int GetHashCode() => x.GetHashCode() ^ y.GetHashCode();
        public override string ToString() => "("+x+", "+y+")";
    }
    public struct Vector2Int : IEquatable<Vector2Int>
    {
        public int x, y;
        public Vector2Int(int x, int y) { this.x=x; this.y=y; }
        public bool Equals(Vector2Int b) => x==b.x && y==b.y;
        public override bool Equals(object b) => b is Vector2Int && Equals((Vector2Int)b);
        public override int GetHashCode() => x*397 ^ y;
    }
    public struct Vector3 { public float x,y,z; public Vector3(float x,float y,float z=0) {this.x=x;this.y=y;this.z=z;} }
    public struct Rect { public float width,height; public Rect(float w,float h) {width=w;height=h;} public Vector2 size => new Vector2(width,height); }
    public static class Mathf
    {
        public static float Max(float a,float b)=>Math.Max(a,b); public static int Max(int a,int b)=>Math.Max(a,b);
        public static float Min(float a,float b)=>Math.Min(a,b); public static int Abs(int a)=>Math.Abs(a);
        public static int RoundToInt(float a)=>(int)Math.Round(a,MidpointRounding.ToEven);
        public static int FloorToInt(float a)=>(int)Math.Floor(a);
        public static float Pow(float a,float b)=>(float)Math.Pow(a,b); public static float Log(float a,float b)=>(float)Math.Log(a,b);
        public static float Lerp(float a,float b,float t)=>a+(b-a)*Math.Max(0,Math.Min(1,t));
    }
    public class Object
    {
        public static void Destroy(Object obj)
        {
            var go=obj as GameObject;
            if (go!=null) { go.SetActive(false); go.transform.parent=null; }
        }
        public static T Instantiate<T>(T original, Transform parent) where T:Component
        {
            if (!(original is PC.Component.Software.FileIcon)) throw new Exception("Unexpected test prefab");
            var go=new GameObject(); go.transform.parent=parent;
            var icon=go.AddComponent<PC.Component.Software.FileIcon>();
            go.AddComponent<PC.Component.Software.DesktopIconDragger>();
            return (T)(Component)icon;
        }
    }
    public class Component : Object
    {
        internal GameObject owner;
        public GameObject gameObject=>owner;
        public Transform transform=>owner.transform;
        public T GetComponent<T>() where T:class=>owner.GetComponent<T>();
        public T GetComponentInParent<T>() where T:class
        {
            for (var t=transform;t!=null;t=t.parent) {var value=t.gameObject.GetComponent<T>();if(value!=null)return value;}
            return null;
        }
    }
    public class MonoBehaviour : Component { }
    public class GameObject : Object
    {
        private readonly List<Component> components=new List<Component>();
        public readonly RectTransform transform;
        public bool activeSelf=true;
        public GameObject() { transform=new RectTransform(); transform.owner=this; components.Add(transform); }
        public T AddComponent<T>() where T:Component,new()
        {
            var c=new T(); c.owner=this; components.Add(c);
            var awake=typeof(T).GetMethod("Awake",BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);
            if(awake!=null)awake.Invoke(c,null);
            return c;
        }
        public T GetComponent<T>() where T:class {foreach(var c in components)if(c is T)return c as T;return null;}
        public void SetActive(bool active) {activeSelf=active;}
    }
    public class Transform : Component
    {
        private Transform parentValue;
        private readonly List<Transform> children=new List<Transform>();
        public Transform parent {get=>parentValue;set{if(parentValue!=null)parentValue.children.Remove(this);parentValue=value;if(value!=null)value.children.Add(this);}}
        public int childCount=>children.Count;
        public Transform GetChild(int i)=>children[i];
    }
    public class RectTransform : Transform { public Rect rect=new Rect(840,500); public Vector2 anchoredPosition; }
    public class Sprite : Object { }
    public class Camera : MonoBehaviour { public static Camera main; public RenderTexture targetTexture; }
    public class RenderTexture : Object {public int width,height;}
    public enum RenderMode {ScreenSpaceOverlay,ScreenSpaceCamera,WorldSpace}
    public class Canvas : MonoBehaviour {public RenderMode renderMode;public Camera worldCamera;}
    public class CanvasGroup : MonoBehaviour { }
    public static class Screen {public static int width=1920,height=1080; public static float dpi=96;}
    public static class Input {public static Vector3 mousePosition;public static bool held;public static bool GetMouseButton(int button)=>held;}
    public static class RectTransformUtility
    {
        public static bool ScreenPointToLocalPointInRectangle(RectTransform rect,Vector2 position,Camera camera,out Vector2 local)
        {local=position;return true;}
    }
    public static class Debug {public static void Log(object value) { } }
    public static class PlayerPrefs
    {
        public static readonly Dictionary<string,string> Strings=new Dictionary<string,string>();
        public static readonly Dictionary<string,int> Ints=new Dictionary<string,int>();
        public static int Writes;
        public static void Clear(){Strings.Clear();Ints.Clear();Writes=0;}
        public static bool HasKey(string key)=>Strings.ContainsKey(key)||Ints.ContainsKey(key);
        public static string GetString(string key,string fallback="")=>Strings.ContainsKey(key)?Strings[key]:fallback;
        public static int GetInt(string key,int fallback=0)=>Ints.ContainsKey(key)?Ints[key]:fallback;
        public static void SetString(string key,string value){Strings[key]=value;Writes++;}
        public static void SetInt(string key,int value){Ints[key]=value;Writes++;}
    }
}
namespace UnityEngine.UI
{
    public class Text : UnityEngine.MonoBehaviour {public string text;}
    public class CanvasScaler : UnityEngine.MonoBehaviour
    {
        public enum ScaleMode {ConstantPixelSize,ScaleWithScreenSize,ConstantPhysicalSize}
        public enum ScreenMatchMode {MatchWidthOrHeight,Expand,Shrink}
        public enum Unit {Centimeters,Millimeters,Inches,Points,Picas}
        public ScaleMode uiScaleMode=ScaleMode.ScaleWithScreenSize;
        public ScreenMatchMode screenMatchMode=ScreenMatchMode.Expand;
        public UnityEngine.Vector2 referenceResolution=new UnityEngine.Vector2(800,500);
        public float scaleFactor=1,matchWidthOrHeight,fallbackScreenDPI=96;
        public Unit physicalUnit=Unit.Points;
    }
}
namespace UnityEngine.EventSystems
{
    public interface IEventSystemHandler { }
    public interface IPointerDownHandler {void OnPointerDown(PointerEventData data);}
    public class PointerEventData {public enum InputButton {Left,Right,Middle} public InputButton button; public UnityEngine.Vector2 position;}
}
public static class PointerInput
{
    public const float Slop=6;
    public static bool IsPrimary(UnityEngine.EventSystems.PointerEventData data)=>data.button==UnityEngine.EventSystems.PointerEventData.InputButton.Left;
}
public class File
{
    public string path;public bool hidden,isFolder;public int size;
    public File(string path,int size=0){this.path=path;this.size=size;}
    public static string Extension(string path){int i=path.LastIndexOf('.');return i<0?"":path.Substring(i);}
}
namespace PC.Component
{
    public class Display {public int Id;public UnityEngine.Vector2 UnfocusedCanvasSize=new UnityEngine.Vector2(840,500);}
    public class Motherboard {public int Id;public Display monitor;}
    public class Storage {public List<File> files=new List<File>();}
}
namespace PC.Component.Software
{
    public class FileIcon : UnityEngine.MonoBehaviour
    {
        public File File {get;private set;}
        public UnityEngine.Sprite Sprite {set{}}
        public void Init(File file,Action<File> callback){File=file;}
        public UnityEngine.Vector2 GetPosition()=>GetComponent<UnityEngine.RectTransform>().anchoredPosition;
        public void SetPosition(UnityEngine.Vector2 position){GetComponent<UnityEngine.RectTransform>().anchoredPosition=position;}
    }
}
