#!/usr/bin/env python3
"""All delivery boxes keep normal contacts. Execute actual ReadyBuildBox with
small engine doubles; this does not simulate crate opening in Unity/PhysX.
"""
from pathlib import Path
import re,shutil,subprocess,tempfile
from server_assets import ROOT,Prefab,read_guid
S=ROOT/'Assets/Scripts/Assembly-CSharp'
box=(S/'PC/ReadyBuildBox.cs').read_text();builder=(S/'PC/ReadyBuildSpawner.cs').read_text()
assert 'IgnoreCollision' not in box and 'IgnoreCollision' not in builder
assert 'FreeFromDebris' not in box and 'DeliveryDebris' not in builder
assert 'ignoreDebrisCollisions' not in box
save=(S/'SaveManager.cs').read_text()
for n in ['Entry','Storage','Compute','Ultra']:assert 'case "ReadyServer_'+n+'":' in save
count=0
for p in (ROOT/'Assets').rglob('*.prefab'):
    text=p.read_text()
    assert not re.search(r'^  ignoreDebrisCollisions: [^0\n]',text,re.M),p
    if 'guid: '+read_guid(S/'PC/ReadyBuildBox.cs') not in text:continue
    data=Prefab(p);boxes=data.mono('ReadyBuildBox');assert len(boxes)==1
    go=data.data(boxes[0][1]['m_GameObject']['fileID'])
    assert go['m_IsActive']==0,(p,'contents must wait for hammer opening')
    count+=1
assert count>=20,count
if not (shutil.which('mcs') and shutil.which('mono')):raise SystemExit('SKIP: Mono/mcs required')
cs=r'''
using System;using System.Reflection;using UnityEngine;
namespace UnityEngine {
 [AttributeUsage(AttributeTargets.Field)]public class SerializeField:Attribute{}
 [AttributeUsage(AttributeTargets.Field)]public class Tooltip:Attribute{public Tooltip(string s){}}
 public class Object {public static GameObject Next;public static int Created;
 public static T Instantiate<T>(T p,Vector3 v,Quaternion q)where T:Object{Created++;return Next as T;}}
 public struct Vector3 {public float x,y,z;public Vector3(float a,float b,float c){x=a;y=b;z=c;}}
 public struct Quaternion{}
 public class MonoBehaviour:Object {public GameObject gameObject;public Transform transform{get{return gameObject.transform;}}}
 public class Transform {public Vector3 position;public Quaternion rotation;public Vector3 Move,Turn;public void Translate(Vector3 v){Move=v;}public void Rotate(Vector3 v){Turn=v;}}
 public class GameObject:Object {public Transform transform=new Transform();}
}
class Test {
 static void Set(object o,string n,object v){o.GetType().GetField(n,BindingFlags.NonPublic|BindingFlags.Instance).SetValue(o,v);}
 static void Start(object o){o.GetType().GetMethod("Start",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(o,null);}
 static void Check(bool b){if(!b)throw new Exception("delivery failed");}
 static void Main(){
 for(int kind=0;kind<4;kind++){
 var box=new PC.ReadyBuildBox();box.gameObject=new GameObject();var output=new GameObject();UnityEngine.Object.Next=output;
 Set(box,"prefab",new GameObject());Set(box,"position",new Vector3(0,1,-1.67f));Set(box,"rotation",new Vector3(0,90,0));Start(box);
 Check(output.transform.Move.y==1&&output.transform.Move.z==-1.67f&&output.transform.Turn.y==90);
 }
 var empty=new PC.ReadyBuildBox();empty.gameObject=new GameObject();int n=UnityEngine.Object.Created;Start(empty);Check(UnityEngine.Object.Created==n);
 Set(empty,"prefab",new GameObject());UnityEngine.Object.Next=null;Start(empty);
 Console.WriteLine("PASS: actual ReadyBuildBox still issues/transforms contents; no debris immunity in box or builder; null guards and legacy save aliases retained");
 }
}
'''
with tempfile.TemporaryDirectory() as td:
    p=Path(td);(p/'test.cs').write_text(cs)
    subprocess.run(['mcs','-out:'+str(p/'test.exe'),str(p/'test.cs'),str(S/'PC/ReadyBuildBox.cs')],check=True)
    subprocess.run(['mono',str(p/'test.exe')],check=True)
print('PASS:',count,'ready-build crates keep contents inactive until opening; no prefab opts into debris immunity')
