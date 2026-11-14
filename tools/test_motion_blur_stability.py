#!/usr/bin/env python3
"""Actual non-motion SimpleScreenFx and selected movement method with render/physics doubles;
compile actual shader passes with a UnityCG compatibility shim.
"""
from pathlib import Path
import re,subprocess,tempfile,math
R=Path(__file__).resolve().parents[1];C=R/'Assets/Scripts/Assembly-CSharp'
s=(C/'Player.cs').read_text();a=s.index('    private void MovePlayer(');b=s.index('    private IEnumerator ShakeCamera()',a);movement=s[a:b]
harness=r'''using System;using System.Collections.Generic;using System.Reflection;using UnityEngine;
namespace UnityEngine {
 public class Object{public static void Destroy(object o){}}
 public class MonoBehaviour:Object{public static Camera TestCamera;public Transform transform=new Transform();public T GetComponent<T>()where T:class{return TestCamera as T;}}
 public class RequireComponent:Attribute{public RequireComponent(Type t){}}
 public struct Vector3{public float x,y,z;public Vector3(float a,float b,float c){x=a;y=b;z=c;}public static Vector3 operator-(Vector3 a,Vector3 b){return new Vector3(a.x-b.x,a.y-b.y,a.z-b.z);}public static Vector3 operator*(Vector3 a,float b){return new Vector3(a.x*b,a.y*b,a.z*b);}public float sqrMagnitude=>x*x+y*y+z*z;public static float Distance(Vector3 a,Vector3 b){var d=a-b;return(float)Math.Sqrt(d.sqrMagnitude);}}
 public struct Vector4{public float x,y,z,w;public Vector4(float a,float b,float c,float d){x=a;y=b;z=c;w=d;}}
 public struct Quaternion{public float yaw,pitch;public float y=>(float)Math.Sin(yaw*Math.PI/360);public float x=>(float)Math.Sin(pitch*Math.PI/360);public float w=>(float)(Math.Cos(yaw*Math.PI/360)*Math.Cos(pitch*Math.PI/360));public Vector3 eulerAngles=>new Vector3(0,yaw,0);public static Quaternion Inverse(Quaternion a){return new Quaternion{yaw=-a.yaw,pitch=-a.pitch};}public static Quaternion operator*(Quaternion a,Quaternion b){return new Quaternion{yaw=a.yaw+b.yaw,pitch=a.pitch+b.pitch};}public static float Angle(Quaternion a,Quaternion b){return Math.Abs(a.yaw-b.yaw);}}
 public class Transform{public Vector3 position;public Quaternion rotation;public Vector3 InverseTransformDirection(Vector3 v){return v;}public Vector3 TransformDirection(Vector3 v){return v;}}
 public class Camera{public Transform transform=new Transform();public float fieldOfView=60,aspect=1.777f;public RenderTexture targetTexture;}
 public class Shader{public static Shader Find(string s){return new Shader();}}
 public class Material{public Dictionary<string,float> f=new Dictionary<string,float>();public Dictionary<string,Vector4> v=new Dictionary<string,Vector4>();public Material(Shader s){}public void SetFloat(string s,float x){f[s]=x;}public void SetVector(string s,Vector4 x){v[s]=x;}public void SetTexture(string s,object t){}}
 public class RenderTexture{public int width=1920,height=1080,format;public static RenderTexture GetTemporary(int w,int h,int d,int f){return new RenderTexture{width=w,height=h,format=f};}public static void ReleaseTemporary(RenderTexture r){}}
 public static class Graphics{public static void Blit(object a,object b){}public static void Blit(object a,object b,object m,int p){}}
 public static class Resources{public static T Load<T>(string s)where T:new(){return new T();}}
 public static class Debug{public static void LogWarning(string s){}}
 public static class SystemInfo{public static bool supportsMotionVectors;}
 public static class Time{public static int frameCount;public static float timeScale=1,unscaledDeltaTime=1f/60;}
 public static class Mathf{public const float Rad2Deg=57.29578f;public static float Atan2(float a,float b){return(float)Math.Atan2(a,b);}public static float Exp(float a){return(float)Math.Exp(a);}public static int Max(int a,int b){return Math.Max(a,b);}public static float Max(float a,float b){return Math.Max(a,b);}public static float Clamp(float v,float a,float b){return Math.Max(a,Math.Min(b,v));}public static float Abs(float a){return Math.Abs(a);}public static float DeltaAngle(float a,float b){float d=(b-a)%360;return d>180?d-360:d;}}
 public class AudioClip{}public class AudioSource{public void PlayOneShot(AudioClip a){}}
 public class CharacterController{public bool enabled=true,isGrounded=true;public Vector3 velocity;public float z;public void Move(Vector3 d){z+=d.z;velocity=d;}}
 public static class Random{public static int Range(int a,int b){return a;}}
}
public static class InputManager{public static bool GetButton(string s){return true;}public static float GetAxis(string s){return s=="Vertical"?1:0;}}
class Walker:MonoBehaviour{public CharacterController controller=new CharacterController();bool sit,pauseControls,isSnow;float runSpeed=10,walkSpeed=5,gravity=-9.8f,footstepTime,runstepLenghten=1.3f,footstepInterval=.5f;Vector3 playerDirection;AudioClip[] snowFootsteps,concreteFootsteps;AudioSource source;
 MOVEMENT
 public void Run(float dt){MovePlayer(dt);}}
class Test{static int n;static void Check(bool v,string why){n++;if(!v)throw new Exception(why);}static void Main(){
 foreach(int fps in new[]{30,60,90,120,144,180,240}){var walker=new Walker();for(int i=0;i<fps;i++)walker.Run(1f/fps);Check(Math.Abs(walker.controller.z-10)<.0001,"movement depends on frame rate");walker.controller.enabled=false;walker.Run(1);Check(Math.Abs(walker.controller.z-10)<.0001,"disabled controller moved");}
 Console.WriteLine("PASS: "+n+" actual movement checks at 30–240 FPS (including 180)");}}
'''.replace('MOVEMENT',movement)
with tempfile.TemporaryDirectory()as td:
 d=Path(td);test=d/'test.cs';test.write_text(harness);exe=d/'test.exe'
 subprocess.run(['mcs','-out:'+str(exe),str(test),str(C/'SimpleScreenFx.cs')],check=True);subprocess.run(['mono',str(exe)],check=True)
 # Keep all arithmetic, sampling and loops from the actual shader. Only replace
 # Unity macros/builtin declarations unavailable outside the Unity compiler.
 shader=(R/'Assets/Resources/SimpleScreenFx.shader').read_text()
 shim='''#define fixed4 float4
struct appdata_img {float4 vertex:POSITION;float2 texcoord:TEXCOORD0;};
float4 UnityObjectToClipPos(float4 p){return p;}
float4 _Time;float LinearEyeDepth(float d){return 1.0/max(d,.0001);}
#define UNITY_DECLARE_DEPTH_TEXTURE(t) sampler2D t
#define SAMPLE_DEPTH_TEXTURE(t,uv) tex2D(t,uv).r
'''
 for i,program in enumerate(re.findall(r'CGPROGRAM(.*?)ENDCG',shader,re.S)):
  program=re.sub(r'^\s*#pragma[^\n]*','',program,flags=re.M).replace('#include "UnityCG.cginc"',shim)
  f=d/'shader.hlsl';f.write_text(program)
  for stage in ['vert','frag']:
   binary=d/'shader.spv';p=subprocess.run(['glslangValidator','-D','-V','--hlsl-dx9-compatible','--auto-map-bindings','--auto-map-locations','-S',stage,'-e',stage,'-o',str(binary),str(f)],capture_output=True,text=True)
   assert p.returncode==0,(i,stage,p.stdout,p.stderr)
   subprocess.run(['spirv-val',str(binary)],check=True)
assert 'Time.deltaTime / Mathf.Max(.0001f, Time.fixedDeltaTime)'in s
assert 'MovePlayer(Time.deltaTime);'in s and 'private void FixedUpdate()'not in s
assert all(old not in shader for old in ['_PrevTex','_Motion','_FallbackMotion','_Shutter'])
print('PASS: all 4 remaining SimpleScreenFx passes compile/validate; old motion shader is absent')
subprocess.run(['python',str(R/'tools/test_stock_motion_blur.py')],check=True)
