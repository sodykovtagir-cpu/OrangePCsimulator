#!/usr/bin/env python3
"""Compile real menu/startup UI classes; execute batching/cancellation/async open.
UGUI, catalog and scene loader are test doubles, not a Unity rendering test.
"""
from pathlib import Path
import re,tempfile,subprocess
R=Path(__file__).resolve().parents[1];C=R/'Assets/Scripts/Assembly-CSharp'
stubs=r'''using System;using System.Collections;using System.Collections.Generic;using System.Threading;using UnityEngine;
namespace UnityEngine {
 public class Object {public string name;public static implicit operator bool(Object o){return o!=null;}public static void Destroy(Object o){}public static void DontDestroyOnLoad(Object o){}public static Transform Instantiate(Transform t,Transform p,bool w){var g=new GameObject("row");g.transform.SetParent(p,false);return g.transform;}}
 public class Component:Object {public GameObject gameObject;public Transform transform=>gameObject.transform;public T GetComponent<T>()where T:Component,new(){return gameObject.GetComponent<T>();}}
 public class MonoBehaviour:Component {public bool isActiveAndEnabled=true;public IEnumerator Pending;public object StartCoroutine(IEnumerator e){Pending=e;return null;}public void InvokeRepeating(string s,float a,float b){}}
 public class Transform:Component {List<Transform> children=new List<Transform>();Dictionary<string,Transform> named=new Dictionary<string,Transform>();public int childCount=>children.Count;public Transform GetChild(int n){return children[n];}public void SetParent(Transform t,bool w){t.children.Add(this);}public Transform Find(string s){Transform t;if(!named.TryGetValue(s,out t)){var g=new GameObject(s);t=g.transform;named[s]=t;}return t;}public void SetAsFirstSibling(){}}
 public class RectTransform:Transform{public Vector2 anchorMin,anchorMax,pivot,sizeDelta,anchoredPosition;}
 public class GameObject:Object{public Transform transform;Dictionary<Type,Component> components=new Dictionary<Type,Component>();public bool activeSelf;public GameObject(string s,params Type[] types){name=s;transform=new RectTransform{gameObject=this};components[typeof(RectTransform)]=transform;}public void SetActive(bool v){activeSelf=v;}public T GetComponent<T>()where T:Component,new(){Component c;if(!components.TryGetValue(typeof(T),out c)){c=new T{gameObject=this};components[typeof(T)]=c;}return(T)c;}public T AddComponent<T>()where T:Component,new(){return GetComponent<T>();}}
 public class SerializeField:Attribute{}public class RequireComponent:Attribute{public RequireComponent(Type t){}}public enum RuntimeInitializeLoadType{BeforeSplashScreen,BeforeSceneLoad}public class RuntimeInitializeOnLoadMethodAttribute:Attribute{public RuntimeInitializeOnLoadMethodAttribute(RuntimeInitializeLoadType t){}}
 public struct Vector2 {public Vector2(float a,float b){}}public struct Rect{public Rect(float a,float b,float c,float d){}}public struct Color32{public Color32(byte a,byte b,byte c,byte d){}}public struct Color{public static Color white=>new Color();}
 public class Texture:Object{}public enum TextureFormat{RGBA32}public enum TextureWrapMode{Clamp}public enum FilterMode{Bilinear}public class Texture2D:Texture{public int width=128,height=128;public TextureWrapMode wrapMode;public FilterMode filterMode;public Texture2D(int w,int h,TextureFormat f,bool m){}public bool LoadImage(byte[] d,bool n){return true;}public void SetPixels32(Color32[] p){}public void Apply(bool a,bool b){}}
 public class Sprite:Object {public static Sprite Create(Texture2D t,Rect r,Vector2 v,float f){return new Sprite();}}public class CanvasRenderer:Component{}public class AudioSource:Component{}
 public static class Debug{public static void Log(object s){}public static void LogException(Exception e){}public static void LogWarning(string s){}public static void LogError(string s){}}
 public class AsyncOperation{public bool allowSceneActivation,isDone=true;public float progress=1;}public class WaitForSeconds{public WaitForSeconds(float t){}}
 public enum SystemLanguage{LANGUAGES}public static class Application{public static string persistentDataPath="/tmp",streamingAssetsPath="/tmp";public static SystemLanguage systemLanguage;public static event Action lowMemory;public static void Quit(){}}
 public static class PlayerPrefs{public static string GetString(string k,string d){return d;}public static int GetInt(string k,int d){return d;}public static float GetFloat(string k,float d){return d;}public static void SetInt(string k,int d){}public static void SetString(string k,string d){}}
 public static class AudioListener{public static float volume;}public static class Mathf{public static float Clamp01(float f){return Math.Max(0,Math.Min(1,f));}public static int RoundToInt(float f){return(int)Math.Round(f);}}
}
namespace UnityEngine.UI{public class Text:Component{public string text;}public class Button:Component{public Event onClick=new Event();}public class Event{public void AddListener(Action a){}}public class RawImage:Component{public bool enabled;}public class Image:Component{public bool enabled,raycastTarget,preserveAspect;public Sprite sprite;public Color color;}}
namespace UnityEngine.SceneManagement{public static class SceneManager{public static AsyncOperation LoadSceneAsync(int n){return new AsyncOperation();}public static AsyncOperation LoadSceneAsync(string n){return new AsyncOperation();}}}
namespace UnityEngine.Networking{public class UnityWebRequest:IDisposable{public enum Result{Success}public Result result;public Handler downloadHandler=new Handler();public static UnityWebRequest Get(string s){return new UnityWebRequest();}public object SendWebRequest(){return null;}public void Dispose(){}}public class Handler{public string text;}}
public class FadedCoverImage:Component{public bool raycastTarget,enabled;public float FadeStart;public Color color;public Texture Texture;}
public class FileInformation{public void Show(FileMenu.Load l){}}public class MessageBox{public void Show(string s){}}public class MenuManager{public string last;public void ShowMenu(string s){last=s;}public void Back(){}}
public static class Localization{public static event Action LanguageChanged;public static string GetText(string s){return s;}public static string GetLanguage(){return"EN";}public static void CreateContent(){}public static void SetLanguage(string s){}}
public static class NativeFilePicker{public static void PickFile(Action<string> cb,string[] s){}}public static class FpsSetting{public static void RestoreSetting(){}}public static class AccountPage{public static void EnsureOnScene(){}}
public class GameData{public string roomName="Save",icon;public int room,workshopId;public bool hardcore;}
namespace SaveManagement{
 public class DataLoader{public string Path;public GameData GameData=new GameData();public static int WorkerThread;public DataLoader(){}public DataLoader(string p){Path=p;}public void LoadFromString(string s){}public void LoadFromPath(){WorkerThread=Thread.CurrentThread.ManagedThreadId;}}
 public static class SaveUtility{public static string GetFolderPath(){return"/tmp";}}
 public static class SaveStorage{public static void Delete(string s){}}
 public static class SaveFileImport{public static string Import(string s,string n){return s;}}
 public static class SaveCatalog{public class Entry{public DataLoader Loader;public string Path,Error;}public static Entry[] Rows=new Entry[0];public static bool IsScanning;public static int Revision;public static string Error;public static void Initialize(string s){}public static void Refresh(){}public static Entry[] Snapshot(){return Rows;}}
}
public static class SaveManager{public static SaveManagement.DataLoader Loader;}
namespace PC.Component.Cabling{public static class CableMode{public static void SelectForLoad(SaveManagement.DataLoader l){}}}
'''
languages=re.findall(r'case SystemLanguage\.(\w+)',(C/'MainMenu.cs').read_text());stubs=stubs.replace('LANGUAGES',','.join(languages))
tests=r'''class Test{
 static int n;static void Check(bool v,string why){n++;if(!v)throw new Exception(why);}static object Call(object o,string m,params object[] a){return o.GetType().GetMethod(m,System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).Invoke(o,a);}static void Set(object o,string key,object v){o.GetType().GetField(key,System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).SetValue(o,v);}
 static void Main(){var menu=new FileMenu();Set(menu,"slotParent",new GameObject("rows").transform);Set(menu,"slotPrefab",new GameObject("template").transform);Set(menu,"empty",new GameObject("empty"));var rows=new SaveManagement.SaveCatalog.Entry[17];for(int i=0;i<rows.Length;i++)rows[i]=new SaveManagement.SaveCatalog.Entry{Path="world"+i,Loader=new SaveManagement.DataLoader()};SaveManagement.SaveCatalog.Rows=rows;
 menu.RebuildList();var build=menu.Pending;Check(build.MoveNext(),"no first-frame yield");int frames=1;while(build.MoveNext())frames++;Check(frames>=3,"all rows built in one frame");var loads=(System.Collections.IList)typeof(FileMenu).GetField("loads",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).GetValue(menu);Check(loads.Count==17,"lost rows");
 menu.RebuildList();build=menu.Pending;Check(build.MoveNext(),"missing rebuild yield");Call(menu,"OnDisable");Check(!build.MoveNext()&&loads.Count==17,"cancelled build cleared new list");
 var main=new MainMenu();Set(main,"menuManager",new MenuManager());Set(main,"loadingText",new UnityEngine.UI.Text());int thread=Thread.CurrentThread.ManagedThreadId;main.LoadSavedFile("saved.opc");var open=main.Pending;Check(open.MoveNext()&&SaveManager.Loader==null,"no loading-screen yield");int waits=0;while(open.MoveNext()){Thread.Sleep(1);if(++waits>1000)throw new Exception("load stuck");}Check(SaveManager.Loader!=null&&SaveManager.Loader.Path=="saved.opc"&&SaveManagement.DataLoader.WorkerThread!=thread,"world decoded on main thread");
 Console.WriteLine("PASS: "+n+" actual menu coroutine checks: first-frame responsiveness, 17-row batching, cancelled rebuild and worker-thread world open; real startup/menu classes compile");}}
'''
with tempfile.TemporaryDirectory()as td:
 d=Path(td);f=d/'test.cs';f.write_text(stubs+tests);exe=d/'test.exe'
 subprocess.run(['mcs','-out:'+str(exe),str(f),*[str(C/s)for s in ['FileMenu.cs','MainMenu.cs','SaveStartup.cs']]],check=True);subprocess.run(['mono',str(exe)],check=True,timeout=20)
