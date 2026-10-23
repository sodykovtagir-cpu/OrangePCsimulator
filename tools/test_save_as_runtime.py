#!/usr/bin/env python3
"""Compile real SaveDialog.cs and exercise save logic with small Unity UI stubs.
This cannot replace Unity Play Mode for actual prefab layout / input handling.
"""
from pathlib import Path
import shutil
import subprocess
import tempfile

root = Path(__file__).resolve().parents[1]
src = root / 'Assets/Scripts/Assembly-CSharp/PC/Component/Software'

STUBS = r'''
using System;
using System.Collections.Generic;
using System.Reflection;
namespace UnityEngine {
 public class SerializeField : Attribute {}
 public class TextAreaAttribute : Attribute {public TextAreaAttribute(int a,int b){}}
 public class Object {
  public static T Instantiate<T>(T original, Transform parent) where T : Object { return original; }
  public static void Destroy(Object obj) {}
 }
 public class Component : Object {
  public GameObject gameObject = new GameObject(false);
  public Transform transform { get { return gameObject.transform; } }
  public T GetComponent<T>() where T:class {return gameObject.GetComponent<T>();}
  public T GetComponentInChildren<T>() where T:class {return gameObject.GetComponent<T>();}
 }
 public class MonoBehaviour : Component {}
 public class GameObject : Object {
  public string name; public int layer; public bool activeSelf;
  public RectTransform transform;
  public GameObject(bool withTransform=true) { if (withTransform) transform = new RectTransform(); }
  public GameObject(string name, params Type[] types) : this() {this.name=name;}
  public T GetComponent<T>() where T:class { return null; }
  public void SetActive(bool value) {activeSelf=value;}
 }
 public class Transform : Component {
  public Transform parent;
  public Transform Find(string name) {return null;}
  public void SetAsLastSibling() {}
 }
 public class RectTransform : Transform {
  public Vector2 anchorMin,anchorMax,offsetMin,offsetMax,anchoredPosition,sizeDelta,pivot;
  public void SetParent(Transform parent, bool worldPositionStays) {this.parent=parent;}
 }
 public struct Vector2 {public float x,y; public Vector2(float x,float y){this.x=x;this.y=y;}
  public static Vector2 zero {get{return new Vector2(0,0);}}
  public static Vector2 one {get{return new Vector2(1,1);}}
 }
 public struct Color {public float r,g,b,a;public Color(float r,float g,float b,float a=1){this.r=r;this.g=g;this.b=b;this.a=a;}
  public static Color black {get{return new Color();}}
 }
 public class CanvasRenderer : Component {}
 public class Font : Object {}
 public static class Resources {public static T GetBuiltinResource<T>(string name) where T:new(){return new T();}}
 public enum TextAnchor {MiddleLeft,MiddleCenter}
 public enum HorizontalWrapMode {Overflow}
 public static class Time {public static float unscaledTime;}
}
namespace UnityEngine.UI {
 using UnityEngine;
 public class UnityEvent {public void AddListener(Action a){} public void AddListener(Action<string> a){} }
 public class InputField : Component {public string text;public Text textComponent;public UnityEvent onValueChanged=new UnityEvent();}
 public class Text : Component {public string text;public Font font;public int fontSize;public Color color;public TextAnchor alignment;public bool raycastTarget;public HorizontalWrapMode horizontalOverflow;public RectTransform rectTransform = new RectTransform();}
 public class Button : Component {public Image targetGraphic;public UnityEvent onClick=new UnityEvent();}
 public class Image : Component {public Sprite sprite;public Type type;public Color color;}
 public class Sprite : Object {}
}
public class ListView : UnityEngine.Component {
 public int SelectedIndex;public event Action<int> SelectedIndexChanged;
 public void SetSelectionTint(UnityEngine.Color c){} public void Clear(){} public void Add(ListViewItem i){}
}
public class ListViewItem {public ListViewItem(string name,UnityEngine.UI.Sprite sprite){}}
namespace PC.Component.Software.OS {
 public class OpenDialog : UnityEngine.Component {}
 public class Storage {public string storageName="Disk"; public List<PC.Component.Software.File> files=new List<PC.Component.Software.File>();}
 public class FileManager {
  public readonly List<Storage> disks;
  public int writes, creates;public bool fail;
  public FileManager(List<Storage> disks){this.disks=disks;}
  public bool TryGetFile(int d,string path,out PC.Component.Software.File result){result=null;
   if(d<0||d>=disks.Count||disks[d]==null) return false;
   result=disks[d].files.Find(f=>f.path==path);return result!=null;
  }
  public bool Create(int d, PC.Component.Software.File file){creates++;
   if(fail||d<0||d>=disks.Count||disks[d]==null||disks[d].files.Exists(f=>f.path==file.path))return false;
   disks[d].files.Add(file);return true;}
  public PC.Component.Software.File Write(int d,string path,string content){writes++;
   if(fail)return null;
   PC.Component.Software.File file;if(!TryGetFile(d,path,out file))return null;
   file.content=content;file.size=content.Length;return file;}
 }
 public class OperatingSystem : UnityEngine.Component {
  public OpenDialog OpenPickerTemplate;public FileManager FileManager;public int ActiveStorage;
  public List<Storage> AllStorage;public UnityEngine.UI.Sprite GetFileSprite(string path){return null;}
  public static bool IsSystemPath(string path){return path.StartsWith("System/")||path=="System";}
 }
}
class Test {
 static void Set(object o,string key,object val){typeof(PC.Component.Software.OS.SaveDialog).GetField(key,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(o,val);}
 static void Check(bool value,string why){if(!value)throw new Exception(why);}
 static void Main(){
  var d0=new PC.Component.Software.OS.Storage();var d1=new PC.Component.Software.OS.Storage();
  d1.files.Add(PC.Component.Software.File.MakeFolder("Art"));
  var disks=new List<PC.Component.Software.OS.Storage>{d0,d1};
  var os=new PC.Component.Software.OS.OperatingSystem();os.AllStorage=disks;os.FileManager=new PC.Component.Software.OS.FileManager(disks);
  var dialog=new PC.Component.Software.OS.SaveDialog();
  var input=new UnityEngine.UI.InputField();var message=new UnityEngine.UI.Text();
  var button=new UnityEngine.UI.Text();
  Set(dialog,"system",os);Set(dialog,"fileNameInput",input);Set(dialog,"messageText",message);
  Set(dialog,"saveButtonText",button);Set(dialog,"extensions",new[]{".pic"});Set(dialog,"index",0);
  Set(dialog,"content","pixels");Set(dialog,"currentDisk",1);Set(dialog,"currentFolder","Art");
  PC.Component.Software.File result=null;Set(dialog,"saved",new Action<PC.Component.Software.File>(f=>result=f));
  input.text="image.pic";dialog.Save();
  Check(result!=null&&result.path=="Art/image.pic"&&d1.files.Contains(result),"nested path/disk");
  Check(d0.files.Count==0&&os.FileManager.creates==1&&os.FileManager.writes==0,"correct disk / no accidental write");
  Set(dialog,"saved",new Action<PC.Component.Software.File>(f=>result=f));Set(dialog,"content","new pixels");
  input.text="image";dialog.Save();
  Check(result.content=="pixels"&&button.text=="Replace"&&os.FileManager.writes==0,"confirm overwrite");
  dialog.Save();Check(result.content=="new pixels"&&os.FileManager.writes==1,"replace after second click");
  int count=d1.files.Count;input.text="../System/user";dialog.Save();
  Check(d1.files.Count==count&&message.text.Contains("valid"),"traversal blocked");
  Set(dialog,"currentDisk",0);Set(dialog,"currentFolder","System");input.text="boot";dialog.Save();
  Check(d0.files.Count==0&&message.text.Contains("Administrator"),"system folder gated");
  Set(dialog,"currentFolder","");input.text="Readme";os.FileManager.fail=true;
  dialog.gameObject.SetActive(true);dialog.Save();
  Check(dialog.gameObject.activeSelf&&message.text.Contains("disk space"),"failed save stays open");
  Console.WriteLine("PASS: SaveDialog compiles; nested disk/path, overwrite confirmation, traversal/admin gates, failed writes");
 }
}
'''

if shutil.which('mcs') is None or shutil.which('mono') is None:
    print('SKIP: mcs/mono unavailable; run this behavioral harness where Mono is installed')
    raise SystemExit(0)

with tempfile.TemporaryDirectory(prefix='save-as-picker-test-') as tmp:
    fixture = Path(tmp) / 'fixture.cs'
    fixture.write_text(STUBS)
    binary = Path(tmp) / 'test.exe'
    subprocess.run(['mcs', '-langversion:latest', '-out:' + str(binary),
                    str(src / 'File.cs'), str(src / 'OS/SaveDialog.cs'), str(fixture)], check=True)
    subprocess.run(['mono', str(binary)], check=True)
print('NOTE: Unity prefab rendering and pointer interactions were not tested')
