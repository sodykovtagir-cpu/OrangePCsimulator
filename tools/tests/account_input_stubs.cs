using System;
using System.Collections.Generic;
using System.Linq;

namespace UnityEngine
{
    public class SerializeField:Attribute{}
    public class Header:Attribute{public Header(string s){}}
    public class Tooltip:Attribute{public Tooltip(string s){}}
    public class TextAreaAttribute:Attribute{public TextAreaAttribute(int a,int b){}}
    public class DefaultExecutionOrder:Attribute{public DefaultExecutionOrder(int n){}}
    public class DisallowMultipleComponent:Attribute{}
    public class RequireComponent:Attribute{public RequireComponent(Type t){}}
    public class Object
    {
        public static void Destroy(Object o){var go=o as GameObject;if(go!=null){go.activeSelf=false;go.transform.parent=null;}}
        public static implicit operator bool(Object o){return !ReferenceEquals(o,null);}
    }
    public class Component:Object
    {
        public GameObject gameObject;
        public Transform transform {get{return gameObject.transform;}}
        public T GetComponent<T>() where T:class{return gameObject.GetComponent<T>();}
        public T GetComponentInParent<T>() where T:class{return gameObject.GetComponentInParent<T>();}
        public T GetComponentInChildren<T>() where T:class{return gameObject.GetComponentInChildren<T>();}
    }
    public class MonoBehaviour:Component{public bool enabled=true;public void StopAllCoroutines(){}}
    public class GameObject:Object
    {
        private readonly List<Component> components=new List<Component>();
        public string name;public bool activeSelf=true;
        public RectTransform transform;
        public GameObject(string name="Object"){this.name=name;transform=new RectTransform{gameObject=this};components.Add(transform);}
        public void SetActive(bool value){activeSelf=value;}
        public T AddComponent<T>() where T:Component,new(){var c=new T{gameObject=this};components.Add(c);return c;}
        public T GetComponent<T>() where T:class{return components.OfType<T>().FirstOrDefault();}
        public T GetComponentInParent<T>() where T:class
        {
            for(Transform tr=transform;tr!=null;tr=tr.parent){var c=tr.gameObject.GetComponent<T>();if(c!=null)return c;}return null;
        }
        public T GetComponentInChildren<T>() where T:class
        {
            var own=GetComponent<T>();if(own!=null)return own;
            foreach(var child in transform.children){var c=child.gameObject.GetComponentInChildren<T>();if(c!=null)return c;}return null;
        }
    }
    public class Transform:Component
    {
        private Transform parentValue;
        public readonly List<Transform> children=new List<Transform>();
        public Transform parent{get{return parentValue;}set{if(parentValue!=null)parentValue.children.Remove(this);parentValue=value;if(value!=null)value.children.Add(this);}}
        public int childCount{get{return children.Count;}}
        public Transform GetChild(int i){return children[i];}
        public bool IsChildOf(Transform p){for(var t=this;t!=null;t=t.parent)if(t==p)return true;return false;}
    }
    public class RectTransform:Transform{public Vector2 anchoredPosition;}
    public class Sprite:Object{}
    public struct Color
    {
        public float r,g,b,a;public Color(float r,float g,float b,float a=1){this.r=r;this.g=g;this.b=b;this.a=a;}
        public static Color white {get{return new Color(1,1,1,1);}}
    }
    public struct Vector2
    {
        public float x,y;public Vector2(float x,float y){this.x=x;this.y=y;}
        public static Vector2 zero{get{return new Vector2();}}
        public static float Distance(Vector2 a,Vector2 b){float x=a.x-b.x,y=a.y-b.y;return (float)Math.Sqrt(x*x+y*y);}
        public static implicit operator Vector2(Vector3 v){return new Vector2(v.x,v.y);}
    }
    public struct Vector3{public float x,y,z;public Vector3(float x,float y,float z){this.x=x;this.y=y;this.z=z;}}
    public class Camera:Component{}
    public enum RenderMode{ScreenSpaceOverlay,ScreenSpaceCamera,WorldSpace}
    public class Canvas:Component{public RenderMode renderMode;public Camera worldCamera;}
    public enum KeyCode{Escape}
    public enum TouchPhase{Began,Moved,Stationary,Ended,Canceled}
    public struct Touch{public int fingerId;public Vector2 position;public TouchPhase phase;}
    public static class Input
    {
        public static bool held,leftDown,rightDown,escape;
        public static Vector3 mousePosition;
        public static Touch[] touches=new Touch[0];
        public static int touchCount{get{return touches.Length;}}
        public static Touch GetTouch(int i){return touches[i];}
        public static bool GetMouseButton(int i){return i==0&&held;}
        public static bool GetMouseButtonDown(int i){return i==0?leftDown:rightDown;}
        public static bool GetMouseButtonUp(int i){return false;}
        public static bool GetKeyDown(KeyCode k){return escape;}
    }
    public static class Time{public static float unscaledTime;}
    public static class PlayerPrefs
    {
        public static readonly Dictionary<string,string> Values=new Dictionary<string,string>();
        public static string GetString(string k,string d=""){string s;return Values.TryGetValue(k,out s)?s:d;}
        public static int GetInt(string k,int d=0){string s;int n;return Values.TryGetValue(k,out s)&&int.TryParse(s,out n)?n:d;}
        public static void SetString(string k,string v){Values[k]=v;}
        public static void SetInt(string k,int v){Values[k]=v.ToString();}
        public static void DeleteKey(string k){Values.Remove(k);}
        public static void Save(){}
    }
    public static class Debug{public static void Log(object o){} }
}
namespace UnityEngine.UI
{
    public class Selectable:UnityEngine.MonoBehaviour{}
    public class Button:Selectable{}
    public class Text:UnityEngine.MonoBehaviour{public string text;public UnityEngine.Color color=UnityEngine.Color.white;}
    public class InputField:Selectable{public string text;}
    public class Image:UnityEngine.MonoBehaviour{public UnityEngine.Sprite sprite;}
}
namespace UnityEngine.EventSystems
{
    public interface IEventSystemHandler{}
    public interface IPointerDownHandler{void OnPointerDown(PointerEventData e);}
    public interface IPointerUpHandler{void OnPointerUp(PointerEventData e);}
    public interface IPointerClickHandler{void OnPointerClick(PointerEventData e);}
    public struct RaycastResult{public UnityEngine.GameObject gameObject;}
    public class PointerEventData
    {
        public enum InputButton{Left,Right,Middle}
        public InputButton button;public int pointerId=-1;public UnityEngine.Vector2 position;public bool dragging;
        public PointerEventData(){}public PointerEventData(EventSystem e){}
    }
    public class EventSystem
    {
        public static EventSystem current=new EventSystem();
        public List<RaycastResult> hits=new List<RaycastResult>();
        public void RaycastAll(PointerEventData e,List<RaycastResult> result){result.AddRange(hits);}
    }
}
public class Joystick:UnityEngine.MonoBehaviour{}
public class ButtonHandler:UnityEngine.MonoBehaviour{}
public class MobileHotbarButton:UnityEngine.MonoBehaviour{}
public class LocalizationText:UnityEngine.MonoBehaviour{}
public class TextAnimation:UnityEngine.MonoBehaviour{}
public static class Localization{public static event Action LanguageChanged;public static string GetText(string s){return s;}public static void Change(){LanguageChanged?.Invoke();}}
public class WorkshopClient
{
    public static WorkshopClient Instance;
    public readonly List<Action<AccountMeResponse,string>> callbacks=new List<Action<AccountMeResponse,string>>();
    public void AccountMe(string token,Action<AccountMeResponse,string> cb){callbacks.Add(cb);}
    public void AccountLogout(string token,Action<string> cb){}
}
namespace PC.Component.Software
{
    public class App:UnityEngine.MonoBehaviour{}
    public class FileManager:UnityEngine.MonoBehaviour{}
    public class DesktopIconDragger{public static bool IsDragging;}
}
namespace PC.Component.Software.OS
{
    public class OperatingSystem:UnityEngine.MonoBehaviour
    {
        public UnityEngine.GameObject desktop;
        public bool IsDesktopContextTarget(UnityEngine.GameObject go){return go==desktop;}
        public bool BlocksDesktopMenu(UnityEngine.GameObject go){return false;}
    }
}
