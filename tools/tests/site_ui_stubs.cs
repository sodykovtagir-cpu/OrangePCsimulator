// Compile-only UGUI/PCOS API doubles. Not proof of a Unity build or rendered layout.
using System;using System.Collections.Generic;
namespace UnityEngine {
 [AttributeUsage(AttributeTargets.Field)]public class SerializeField:Attribute{}
 public class Object {public static void Destroy(Object o){} }
 public class Component:Object {public GameObject gameObject;public Transform transform{get{return gameObject.transform;}}public string name;public T GetComponent<T>()where T:Component,new(){return new T();}}
 public class MonoBehaviour:Component{}
 public class Transform:Component {public Transform parent;public int childCount;public Vector3 localScale;public void SetParent(Transform p,bool b){}public Transform GetChild(int i){return null;}public Transform Find(string n){return null;}}
 public class RectTransform:Transform {public Vector2 anchorMin,anchorMax,pivot,offsetMin,offsetMax,anchoredPosition,sizeDelta;public Rect rect;}
 public class GameObject:Object {public string name;public int layer;public Transform transform;public GameObject(string n,params Type[] types){name=n;}public void SetActive(bool b){}public T AddComponent<T>()where T:Component,new(){return new T();}public T GetComponent<T>()where T:Component,new(){return new T();}}
 public struct Vector2 {public float x,y;public Vector2(float a,float b){x=a;y=b;}public static Vector2 zero{get{return new Vector2();}}public static Vector2 one{get{return new Vector2(1,1);}}public static Vector2 operator +(Vector2 a,Vector2 b){return a;}public static Vector2 operator -(Vector2 a,Vector2 b){return a;}}
 public struct Vector3 {public float x,y,z;public Vector3(float a,float b,float c){x=a;y=b;z=c;}}
 public struct Rect {public float width,height;public Rect(float x,float y,float w,float h){width=w;height=h;}}
 public struct Color {public float r,g,b,a;public Color(float x,float y,float z,float w=1){r=x;g=y;b=z;a=w;}public static Color white{get{return new Color(1,1,1);}}public static Color black{get{return new Color();}}}
 public static class ColorUtility {public static bool TryParseHtmlString(string s,out Color c){c=new Color();return true;}}
 public static class Mathf {public static float Max(float a,float b){return Math.Max(a,b);}}
 public static class Time {public static float unscaledTime;}
 public class Font:Object{}
 public static class Resources {public static T Load<T>(string s)where T:Object{return null;}public static T GetBuiltinResource<T>(string s)where T:Object{return null;}}
 public class Texture:Object{}
 public enum TextureFormat {RGBA32}
 public class Texture2D:Texture {public int width,height;public Texture2D(int w,int h,TextureFormat f,bool b){}public bool LoadImage(byte[] data){return true;}}
 public class Sprite:Object {public Texture2D texture;public static Sprite Create(Texture2D t,Rect r,Vector2 p){return new Sprite();}}
 public enum TextAnchor {MiddleLeft,MiddleCenter,UpperLeft}
 public class Camera:Object{}
 public static class RectTransformUtility {public static bool ScreenPointToLocalPointInRectangle(RectTransform r,Vector2 p,Camera c,out Vector2 result){result=p;return true;}}
}
namespace UnityEngine.Events {public class UnityEvent {public void AddListener(Action a){}}public class UnityEvent<T>{public void AddListener(Action<T> a){}}}
namespace UnityEngine.EventSystems {
 public class PointerEventData {public enum InputButton{Left,Right}public InputButton button;public UnityEngine.Vector2 position;public UnityEngine.Camera pressEventCamera;}
 public interface IPointerDownHandler{void OnPointerDown(PointerEventData e);}public interface IPointerUpHandler{void OnPointerUp(PointerEventData e);}
 public interface IBeginDragHandler{void OnBeginDrag(PointerEventData e);}public interface IDragHandler{void OnDrag(PointerEventData e);}public interface IEndDragHandler{void OnEndDrag(PointerEventData e);}
}
namespace UnityEngine.UI {
 using UnityEngine;using UnityEngine.Events;
 public class Graphic:Component {public Color color;public bool raycastTarget;public RectTransform rectTransform{get{return (RectTransform)transform;}}}
 public class Image:Graphic {public Sprite sprite;public bool preserveAspect;}
 public class Text:Graphic {public Font font;public string text;public int fontSize;public bool supportRichText;public TextAnchor alignment;public HorizontalWrapMode horizontalOverflow;public VerticalWrapMode verticalOverflow;}
 public enum HorizontalWrapMode{Wrap}public enum VerticalWrapMode{Truncate}
 public class Button:Component {public bool interactable;public Graphic targetGraphic;public UnityEvent onClick=new UnityEvent();}
 public class InputField:Component {public enum LineType{MultiLineNewline,SingleLine}public bool interactable;public string text;public void SetTextWithoutNotify(string s){text=s;}public int characterLimit;public Text textComponent;public Graphic targetGraphic;public LineType lineType;public UnityEvent<string> onEndEdit=new UnityEvent<string>();public void ActivateInputField(){}public void MoveTextEnd(bool b){}}
 public class ScrollRect:Component {public enum MovementType{Clamped}public RectTransform viewport,content;public bool horizontal;public MovementType movementType;public float verticalNormalizedPosition;public void StopMovement(){}}
 public class Mask:Component{public bool showMaskGraphic;}
 public class RectMask2D:Component{}
 public class Outline:Component{public Color effectColor;public Vector2 effectDistance;public bool enabled;}
 public class HorizontalLayoutGroup:Component{public float spacing;public bool childForceExpandWidth,childForceExpandHeight;}
}
namespace PC.Component.Software {
 public abstract class App:UnityEngine.MonoBehaviour {protected OS.OperatingSystem system;protected bool maximized;public virtual void Maximize(){maximized=!maximized;}public bool IsAdministrator;public virtual bool SingleInstance{get{return true;}}protected virtual bool ShowMenuBar{get{return true;}}public virtual bool CanOpenExtension(string s){return false;}public virtual void Open(string s){}}
 public class File {public string path,content;public bool isFolder;}
 public class Storage{public List<File> files;}
}
namespace PC.Component.Software.OS {
 using PC.Component.Software;
 public class OperatingSystem {public float GetTaskbarInsetLocalHeight(){return 48;}public List<Storage> AllStorage;public SaveDialog SaveDialog;public void ShowMessageBox(string t,string s){}public void SelectFile(string ext,Action<File> a){}public void SelectFile(string[] ext,Action<File> a){}}
 public class SaveDialog {public void ShowDialog(string n,string c,string[] e,string p,int d,Action<File> a,bool admin){}}
}
