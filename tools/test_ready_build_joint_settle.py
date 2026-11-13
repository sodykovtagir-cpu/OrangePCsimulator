#!/usr/bin/env python3
"""Run actual Slot.SetComponent + ReadyBuildSpawner.RestoreMasses with Mono.
Doubles model deferred transform sync, not real PhysX contacts/solver/damage.
"""
from pathlib import Path
import re,shutil,subprocess,tempfile
ROOT=Path(__file__).resolve().parents[1]
S=ROOT/'Assets/Scripts/Assembly-CSharp'
def method(text,signature):
    a=text.index(signature);b=text.index('{',a);i=b+1;depth=1
    while depth:
        depth+=(text[i]=='{')-(text[i]=='}');i+=1
    return text[a:i]
slot=(S/'Slot.cs').read_text();spawner=(S/'PC/ReadyBuildSpawner.cs').read_text()
set_component=method(slot,'protected virtual void SetComponent(Item item)')
restore=method(spawner,'private void RestoreMasses()')
backup=method(spawner,'private struct MassBackup')
assert 'Physics.SyncTransforms()' in set_component
assert set_component.index('Physics.SyncTransforms()')<set_component.index('AddComponent<FixedJoint>')
assert '.Sleep()' not in restore and '.WakeUp()' in restore
assert 'IgnoreCollision' not in spawner
build=method(spawner,'private IEnumerator Build()')
release=build.index('baseBody.isKinematic = baseWasKinematic;')
assert build.index('RestoreMasses();')<release<build.index('yield return new WaitForFixedUpdate();',release)
if not(shutil.which('mcs') and shutil.which('mono')):raise SystemExit('SKIP: Mono/mcs required')
cs=r'''
using System;using System.Collections.Generic;using UnityEngine;
namespace UnityEngine {
 public struct Vector3 {public float x,y,z;public Vector3(float a,float b,float c){x=a;y=b;z=c;}public static Vector3 zero{get{return new Vector3();}}public static Vector3 operator +(Vector3 a,Vector3 b){return new Vector3(a.x+b.x,a.y+b.y,a.z+b.z);}}
 public struct Quaternion{public float w;}
 public class Transform {public GameObject gameObject;public Vector3 position;public Quaternion rotation;public Transform parent;public Vector3 TransformVector(Vector3 v){return v;}public void SetParent(Transform t,bool keep){parent=t;}}
 public class AudioClip {public static implicit operator bool(AudioClip a){return a!=null;}}
 public class AudioSource {public static void PlayClipAtPoint(AudioClip a,Vector3 p){}}
 public class MonoBehaviour {public GameObject gameObject;public Transform transform{get{return gameObject.transform;}}}
 public class GameObject {public GameObject(){transform.gameObject=this;}public Transform transform=new Transform();public Rigidbody body;public FixedJoint joint;public Connector connector;public int layer;
 public T AddComponent<T>()where T:new(){var o=new T();if(o is FixedJoint){joint=o as FixedJoint;joint.atCreation=body.pose;joint.rotationAtCreation=body.poseRotation;}if(o is Connector)connector=o as Connector;return o;}
 public T GetComponentInParent<T>()where T:class{return body as T;}}
 public enum CollisionDetectionMode{Discrete,ContinuousDynamic}
 public class Rigidbody {public Vector3 velocity,angularVelocity,pose;public Quaternion poseRotation;public Transform transform;public float mass;public int solverIterations,solverVelocityIterations;public CollisionDetectionMode collisionDetectionMode;public bool sleeping;public int wakes;public Action BeforeWake;
 public void Sleep(){sleeping=true;}public void WakeUp(){if(BeforeWake!=null)BeforeWake();sleeping=false;wakes++;}}
 public class FixedJoint {public Vector3 atCreation;public Quaternion rotationAtCreation;public float breakForce=float.PositiveInfinity;public Rigidbody connectedBody;}
 public static class Physics {public static List<Rigidbody> Bodies=new List<Rigidbody>();public static int Syncs;public static void SyncTransforms(){Syncs++;foreach(var b in Bodies){b.pose=b.transform.position;b.poseRotation=b.transform.rotation;}}}
}
public class Item {public GameObject gameObject=new GameObject();public Transform transform{get{return gameObject.transform;}}public Vector3 SlotOffset;public bool glue;public int connected;public void OnSlotConnected(){connected++;}}
public class Connector {public SlotHarness slot;}
public class SlotHarness:MonoBehaviour {
 public Transform insertPos=new Transform();public bool setParent;public float breakForce=800;public bool preparing=true;public AudioClip plug;public Transform attachedTransform;public Item attachedItem;
 SLOT_METHOD
 public void Attach(Item item){SetComponent(item);}
}
class SpawnerHarness {
 BACKUP
 private List<MassBackup> restoreMass=new List<MassBackup>();
 RESTORE
 public void Track(Rigidbody body){restoreMass.Add(new MassBackup{body=body,mass=2.5f,solver=6,solverVelocity=1,collision=CollisionDetectionMode.Discrete});}
 public void Restore(){RestoreMasses();}public int Count{get{return restoreMass.Count;}}
}
class Test {
 static int checks;static void Check(bool b){checks++;if(!b)throw new Exception("check "+checks);}
 static bool Same(Vector3 a,Vector3 b){return a.x==b.x&&a.y==b.y&&a.z==b.z;}
 static Rigidbody Body(GameObject go){var b=new Rigidbody{transform=go.transform,pose=go.transform.position};go.body=b;Physics.Bodies.Add(b);return b;}
 static void Main(){
 // Exercise fresh boards at different crate heights, orientations and offsets,
 // both parent modes, and normal/glued joints. Physics pose initially stale.
 for(int i=0;i<8;i++){
  Physics.Bodies.Clear();var host=new GameObject();Body(host);host.transform.position=new Vector3(3,i,2);
  var slot=new SlotHarness{gameObject=host,setParent=(i%2==0)};slot.insertPos.position=new Vector3(4,i+2,7);slot.insertPos.rotation=new Quaternion{w=i};
  var item=new Item{SlotOffset=new Vector3(.1f,.2f,.3f),glue=(i%3==0)};var body=Body(item.gameObject);slot.Attach(item);
  Check(Same(item.transform.position,slot.insertPos.position+item.SlotOffset));Check(Same(item.gameObject.joint.atCreation,item.transform.position));Check(item.gameObject.joint.rotationAtCreation.w==i);
  Check(Same(host.body.pose,host.transform.position));Check(item.gameObject.joint.connectedBody==host.body);Check(item.gameObject.connector.slot==slot);Check(slot.attachedItem==item&&item.connected==1&&item.gameObject.layer==8);
  Check(item.transform.parent==(slot.setParent?slot.insertPos:null));Check(item.glue?float.IsPositiveInfinity(item.gameObject.joint.breakForce):item.gameObject.joint.breakForce==800);Check(!body.sleeping);
 }
 // Restore every parameter before waking either body. Do not freeze the
 // displaced board; its normal physics must be allowed to settle naturally.
 var root=new Rigidbody{mass=108,solverIterations=40,solverVelocityIterations=40,collisionDetectionMode=CollisionDetectionMode.ContinuousDynamic,velocity=new Vector3(1,2,3),angularVelocity=new Vector3(2,3,4),sleeping=true};
 var board=new Rigidbody{mass=1,solverIterations=40,solverVelocityIterations=40,sleeping=true};var sp=new SpawnerHarness();sp.Track(root);sp.Track(board);sp.Track(null);
 Action ready=()=>{Check(root.mass==2.5f&&board.mass==2.5f);Check(root.solverIterations==6&&board.solverIterations==6);Check(root.solverVelocityIterations==1&&root.collisionDetectionMode==CollisionDetectionMode.Discrete);};root.BeforeWake=ready;board.BeforeWake=ready;
 sp.Restore();Check(!root.sleeping&&!board.sleeping);Check(root.wakes==1&&board.wakes==1);Check(Same(root.velocity,Vector3.zero)&&Same(root.angularVelocity,Vector3.zero));Check(sp.Count==0);sp.Restore();Check(root.wakes==1&&board.wakes==1);
 Console.WriteLine("PASS: actual slot snap/rest-pose and mass restoration; "+checks+" checks; synced joint anchors, all parameters restored before wake, no forced sleep; normal debris contacts retained");
 }
}
'''.replace('SLOT_METHOD',set_component).replace('BACKUP',backup).replace('RESTORE',restore)
with tempfile.TemporaryDirectory() as td:
    p=Path(td);(p/'test.cs').write_text(cs)
    subprocess.run(['mcs','-out:'+str(p/'test.exe'),str(p/'test.cs')],check=True)
    subprocess.run(['mono',str(p/'test.exe')],check=True)
