// Minimal engine doubles for the real DriveTray/Case/HardwareSlot code.
// These tests exercise events/topology, not Unity physics or asset importing.
using System;
using System.Collections.Generic;
using System.Reflection;
namespace UnityEngine {
 [AttributeUsage(AttributeTargets.Field)] public class SerializeField:Attribute {}
 public class Object {
  public static implicit operator bool(Object v) { return v != null; }
  public static bool operator !(Object v) { return v == null; }
  protected static void Destroy(Object o) {}
 }
 public class Component:Object {
  public GameObject gameObject;
  public Transform transform { get { return gameObject.transform; } }
  public T GetComponent<T>() where T:class { return gameObject.GetComponent<T>(); }
  public T GetComponentInParent<T>() where T:class {
   for(var g=gameObject;g!=null;g=g.parent) { var c=g.GetComponent<T>();if(c!=null)return c; } return null;
  }
 }
 public class MonoBehaviour:Component {}
 public class GameObject:Object {
  public GameObject parent; public Transform transform; public bool active=true;
  private List<object> parts=new List<object>();
  public GameObject() { transform=new Transform();transform.gameObject=this; }
  public T Add<T>() where T:Component,new() { var c=new T();c.gameObject=this;parts.Add(c);return c; }
  public T GetComponent<T>() where T:class { return parts.Find(x=>x is T) as T; }
  public void SetActive(bool v) {active=v;}
 }
 public class Transform:Component { public void SetParent(Transform p,bool world){gameObject.parent=p==null?null:p.gameObject;} }
 public class Material:Object {}
 public class Renderer:Component { public Material material,sharedMaterial; }
 public static class Debug { public static void Log(object s){} }
 public static class Mathf { public static int Min(int a,int b){return Math.Min(a,b);} }
}
namespace UnityEngine.Events {
 public class UnityEvent { private event Action changed;
  public void AddListener(Action a){changed+=a;}public void RemoveListener(Action a){changed-=a;}public void Invoke(){if(changed!=null)changed();}
 }
}
public static class SaveManager { public static bool RestoringSave; }
public static class Localization {public static string GetText(string s){return s;}}
public class Main:UnityEngine.Object {public static Main Instance;public bool hardcore;public void FadeText(string s){} }
public class Item:UnityEngine.MonoBehaviour {
 public int Id;
 public event Action SlotConnected,SlotDisconnected;
 public void OnSlotConnected(){if(SlotConnected!=null)SlotConnected();}
 public void OnSlotDisconnected(){if(SlotDisconnected!=null)SlotDisconnected();}
}
public class Connector:UnityEngine.MonoBehaviour {public Slot slot;}
public class Slot:UnityEngine.MonoBehaviour {
 public string target;public bool IsUsing;public Item AttachedItem;
 public bool TryAttach(Item item) {IsUsing=true;SetComponent(item);return true;}
 protected virtual void SetComponent(Item item){AttachedItem=item;var c=item.GetComponent<Connector>()??item.gameObject.Add<Connector>();c.slot=this;item.gameObject.parent=gameObject;item.OnSlotConnected();}
 protected virtual void RemoveComponent(){var old=AttachedItem;IsUsing=false;AttachedItem=null;old.gameObject.parent=null;old.OnSlotDisconnected();}
 public void UnPlug(){if(IsUsing)RemoveComponent();}
}
namespace PC.Component {
 public enum HardwareType {CPU,Cooler,GPU,RAM,Drive,Supply,Output}
 public class Hardware:Item {
  public bool Power;public event Action OnDamaged;public event Action<bool> PowerChanged;
  public void Switch(bool on,bool counting){Power=on;if(PowerChanged!=null)PowerChanged(on);}
  public void Damage(){if(OnDamaged!=null)OnDamaged();}
 }
 public class Storage:Hardware {
  public bool BootDisk;
  public bool TryGetFile(string path,out PC.Component.Software.OS.TestFile file){file=BootDisk?new PC.Component.Software.OS.TestFile{content="pcos"}:null;return file!=null;}
 }
 public class TestSystem:PC.Component.Software.OS.ComputerSystem {
  public List<Storage> Disks {get{return AllStorage;}}
  public int Adds,Faults;
  public TestSystem(){Init(null,-1);}
  protected override void BootSystem(){}
  protected override void OnHardwareAdded(Hardware h){Adds++;}
  public override void Fault(){Faults++;}
  public override void PowerClicked(){}
 }
}
namespace PC.Component.Software.OS {
 public class TestFile {public string content;}
 public class FileManager {public FileManager(ComputerSystem system){}}
}
