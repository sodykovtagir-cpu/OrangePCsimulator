#!/usr/bin/env python3
"""Run real ReadyBuildBox and the real builder debris method on engine doubles.
Tests routing for independent ready-PC objects, not Unity/PhysX behaviour.
"""
from pathlib import Path
import re, shutil, subprocess, tempfile
ROOT=Path(__file__).resolve().parents[1]
S=ROOT/'Assets/Scripts/Assembly-CSharp'
source=(S/'PC/ReadyBuildSpawner.cs').read_text()
def method(text,signature):
    a=text.index(signature);b=text.index('{',a);i=b+1;depth=1
    while depth:
        depth+=(text[i]=='{')-(text[i]=='}');i+=1
    return text[a:i]
assert 'IgnoreDeliveryDebris(root);' in source and 'IgnoreDeliveryDebris(spawned);' in source
# Ignore calls must happen before any post-spawn yield/physics wait.
for name,inst in [('root','var root = Instantiate('),('spawned','var spawned = Instantiate(')]:
    block=source[source.index(inst):source.index('IgnoreDeliveryDebris('+name+');')]
    assert 'yield return' not in block
save=(S/'SaveManager.cs').read_text()
legacy=method(save,'private static GameObject FindItemPrefab(')
for name in ['Entry','Storage','Compute','Ultra']:assert 'case "ReadyServer_'+name+'":' in legacy
assert legacy.index('return FindItemPrefab("Server");')<legacy.index('Resources.Load<GameObject>')
if not (shutil.which('mcs') and shutil.which('mono')):
    raise SystemExit('SKIP: Mono/mcs required for delivery doubles')
cs=r'''
using System;using System.Collections;using System.Collections.Generic;using System.Reflection;
namespace UnityEngine {
 [AttributeUsage(AttributeTargets.Field)]public class SerializeField:Attribute{}
 [AttributeUsage(AttributeTargets.Field)]public class Tooltip:Attribute{public Tooltip(string s){}}
 public class Object { public static GameObject Next; public static T Instantiate<T>(T p,Vector3 v,Quaternion q)where T:Object{return Next as T;} }
 public struct Vector3{} public struct Quaternion{}
 public class Component:Object {public GameObject gameObject;public Transform transform{get{return gameObject.transform;}}
 public T GetComponent<T>()where T:class{return gameObject.GetComponent<T>();}
 public T[] GetComponentsInChildren<T>(bool all)where T:class{return gameObject.GetComponentsInChildren<T>(all);} }
 public class MonoBehaviour:Component {public void StartCoroutine(IEnumerator e){e.MoveNext();}}
 public class Transform {public Vector3 position;public Quaternion rotation;public void Translate(Vector3 v){}public void Rotate(Vector3 v){} }
 public class GameObject:Object {public Transform transform=new Transform();public object script;public Collider[] cols=new Collider[0];
 public T GetComponent<T>()where T:class{return script as T;}public T[] GetComponentsInChildren<T>(bool all)where T:class{return Array.ConvertAll(cols,x=>x as T);} }
 public class Collider:Object{}
 public class WaitForSeconds{public WaitForSeconds(float s){}}
 public static class Physics{public static List<Tuple<Collider,Collider>> pairs=new List<Tuple<Collider,Collider>>();
 public static void IgnoreCollision(Collider a,Collider b,bool on){if(!on)throw new Exception();pairs.Add(Tuple.Create(a,b));}}
}
namespace PC {public class ReadyBuildSpawner:UnityEngine.MonoBehaviour {
public UnityEngine.Collider[] DeliveryDebris {private get;set;}
BUILDER_METHOD
public void Spawned(UnityEngine.GameObject g){IgnoreDeliveryDebris(g);}
}}
class Test {
 static void Set(object o,string n,object v){o.GetType().GetField(n,BindingFlags.NonPublic|BindingFlags.Instance).SetValue(o,v);}
 static void Start(object o){o.GetType().GetMethod("Start",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(o,null);}
 static void Check(bool v){if(!v)throw new Exception("delivery isolation failed");}
 static PC.ReadyBuildBox Box(UnityEngine.GameObject content){var b=new PC.ReadyBuildBox();b.gameObject=new UnityEngine.GameObject();b.gameObject.cols=new[]{new UnityEngine.Collider(),new UnityEngine.Collider(),null};Set(b,"prefab",new UnityEngine.GameObject());UnityEngine.Object.Next=content;return b;}
 static void Main(){
 var plan=new UnityEngine.GameObject();var builder=new PC.ReadyBuildSpawner();builder.gameObject=plan;plan.script=builder;
 var box=Box(plan);Start(box);Check(UnityEngine.Physics.pairs.Count==0);
 var root=new UnityEngine.GameObject();root.cols=new[]{new UnityEngine.Collider(),new UnityEngine.Collider()};builder.Spawned(root);
 var part=new UnityEngine.GameObject();part.cols=new[]{new UnityEngine.Collider()};builder.Spawned(part);
 Check(UnityEngine.Physics.pairs.Count==6);foreach(var p in UnityEngine.Physics.pairs)Check(p.Item2==box.gameObject.cols[0]||p.Item2==box.gameObject.cols[1]);
 UnityEngine.Physics.pairs.Clear();var bare=new PC.ReadyBuildSpawner();bare.Spawned(root);Check(UnityEngine.Physics.pairs.Count==0);
 var full=new UnityEngine.GameObject();full.cols=new[]{new UnityEngine.Collider()};var oldBox=Box(full);Start(oldBox);Check(UnityEngine.Physics.pairs.Count==2);
 UnityEngine.Physics.pairs.Clear();var serverPlan=new UnityEngine.GameObject();var serverBuilder=new PC.ReadyBuildSpawner();serverBuilder.gameObject=serverPlan;serverPlan.script=serverBuilder;
 var serverBox=Box(serverPlan);Set(serverBox,"ignoreDebrisCollisions",false);Start(serverBox);serverBuilder.Spawned(root);serverBuilder.Spawned(part);Check(UnityEngine.Physics.pairs.Count==0);
 var chassisBox=Box(full);Set(chassisBox,"ignoreDebrisCollisions",false);Start(chassisBox);Check(UnityEngine.Physics.pairs.Count==0);
 Console.WriteLine("PASS: server plans and bare chassis keep debris collisions; ordinary PC/miner protection unchanged; old server save aliases");
 }
}
'''.replace('BUILDER_METHOD',method(source,'private void IgnoreDeliveryDebris('))
cs='using UnityEngine;\n'+cs
with tempfile.TemporaryDirectory() as td:
    p=Path(td);(p/'test.cs').write_text(cs)
    subprocess.run(['mcs','-out:'+str(p/'test.exe'),str(p/'test.cs'),str(S/'PC/ReadyBuildBox.cs')],check=True)
    subprocess.run(['mono',str(p/'test.exe')],check=True)
