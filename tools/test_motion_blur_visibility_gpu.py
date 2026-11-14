#!/usr/bin/env python3
"""Render the installed Unity PP 3.4.0 MotionBlur shader passes on Mesa/EGL.
Usage: python tools/test_motion_blur_visibility_gpu.py --package /path/to/package
Needs moderngl/numpy, glslangValidator, spirv-val, spirv-cross and EGL/Mesa.
Synthetic native vectors and linear depth replace Unity's camera/depth producer;
this tests the actual stock filter, NOT Unity Player/native-vector correctness.
"""
from pathlib import Path
import argparse,json,re,struct,subprocess,tempfile
import moderngl
import numpy as np
R=Path(__file__).resolve().parents[1]
a=argparse.ArgumentParser();a.add_argument('--package',type=Path);args=a.parse_args()
package=args.package
if package is None:
 candidates=list((R/'Library/PackageCache').glob('com.unity.postprocessing@3.4.0*'))
 if (R/'Packages/com.unity.postprocessing').exists():candidates.append(R/'Packages/com.unity.postprocessing')
 if not candidates:raise SystemExit('Pass --package pointing to the extracted com.unity.postprocessing 3.4.0 package.')
 package=candidates[0]
assert json.loads((package/'package.json').read_text())['version']=='3.4.0'
# Verify every copied resource reference against the package's original asset.
original=(package/'PostProcessing/PostProcessResources.asset').read_text()
ours=(R/'Assets/Resources/OrangePCPostProcessResources.asset').read_text()
assert ours.split()==original.replace('m_Name: PostProcessResources','m_Name: OrangePCPostProcessResources').split()
guids={re.search(r'^guid: (\w+)',p.read_text(),re.M).group(1) for p in package.rglob('*.meta') if re.search(r'^guid: (\w+)',p.read_text(),re.M)}
assert set(re.findall(r'guid: (\w+)',ours))<=guids,'broken package resource reference'
source=(package/'PostProcessing/Shaders/Builtins/MotionBlur.shader').read_text()
body=re.search(r'HLSLINCLUDE(.*?)ENDHLSL',source,re.S).group(1)
# Keep all stock filter logic. Replace platform declarations, sampling macros,
# vertex varyings, and depth decode (input here is already linear01).
shim='''
#define TEXTURE2D_SAMPLER2D(t,s) sampler2D t
#define SAMPLE_TEXTURE2D(t,s,uv) tex2D(t,uv)
#define SAMPLE_TEXTURE2D_LOD(t,s,uv,lod) tex2Dlod(t,float4(uv,0,lod))
#define SAMPLE_DEPTH_TEXTURE(t,s,uv) tex2D(t,uv).r
#define UNITY_LOOP
#define TWO_PI 6.28318530718
struct VaryingsDefault {float4 vertex:SV_POSITION;float2 texcoord:TEXCOORD0;};
float4 _ScreenParams;
float Linear01Depth(float d){return d;}
float GradientNoise(float2 uv){uv=floor(uv*_ScreenParams.xy);return frac(52.9829189*frac(dot(float2(.06711056,.00583715),uv)));}
'''
body=re.sub(r'^\s*#pragma[^\n]*','',body,flags=re.M)
body=re.sub(r'^\s*#include[^\n]*',shim,body,flags=re.M)
entries=['FragVelocitySetup','FragTileMax1','FragTileMax2','FragTileMaxV','FragNeighborMax','FragReconstruction']
ctx=moderngl.create_standalone_context(backend='egl')
vertex='#version 330\nout vec2 i_texcoord;void main(){vec2 p=vec2((gl_VertexID<<1)&2,gl_VertexID&2);i_texcoord=p;gl_Position=vec4(p*2.-1.,0,1);}'
programs={}
with tempfile.TemporaryDirectory()as td:
 d=Path(td);hlsl=d/'stock.hlsl';hlsl.write_text(body)
 for entry in entries:
  spv=d/'shader.spv';frag=d/'shader.frag'
  p=subprocess.run(['glslangValidator','-D','-V','--hlsl-dx9-compatible','--auto-map-bindings','--auto-map-locations','-S','frag','-e',entry,'-o',str(spv),str(hlsl)],capture_output=True,text=True)
  assert p.returncode==0,(entry,p.stdout,p.stderr)
  subprocess.run(['spirv-val',str(spv)],check=True)
  subprocess.run(['spirv-cross',str(spv),'--version','330','--output',str(frag)],check=True)
  reflect=json.loads(subprocess.check_output(['spirv-cross',str(spv),'--reflect'],text=True))
  program=ctx.program(vertex_shader=vertex,fragment_shader=frag.read_text())
  block=reflect['ubos'][0];uniforms=ctx.buffer(reserve=block['block_size']);uniforms.bind_to_uniform_block(block['binding'])
  programs[entry]=(program,ctx.vertex_array(program,[]),reflect,uniforms)

def tex(values,linear=False):
 h,w,c=values.shape;t=ctx.texture((w,h),c,values.astype('f4').tobytes(),dtype='f4')
 t.filter=(moderngl.LINEAR,moderngl.LINEAR)if linear else(moderngl.NEAREST,moderngl.NEAREST)
 t.repeat_x=False;t.repeat_y=False;return t

def run(entry,textures,size,values):
 program,vao,reflect,uniforms=programs[entry]
 for t in reflect['textures']:
  name=t['name'];textures[name].use(t['binding']);program[name].value=t['binding']
 block=reflect['ubos'][0];data=bytearray(uniforms.size)
 for m in reflect['types'][block['type']]['members']:
  name=m['name'];v=values.get(name)
  if v is None and name.endswith('_TexelSize'):
   ts=textures[name[:-10]].size if name[:-10] in textures else (640,360);v=(1/ts[0],1/ts[1],*ts) if m['type']=='vec4' else(1/ts[0],1/ts[1])
  if v is None:v=(0.,)*int(m['type'][-1])if m['type'].startswith('vec')else 0
  v=v if isinstance(v,tuple)else(v,)
  struct.pack_into('<'+('i'if m['type']=='int'else'f')*len(v),data,m['offset'],*v)
 uniforms.write(data);uniforms.bind_to_uniform_block(block['binding'])
 output=ctx.texture(size,4,dtype='f4');fbo=ctx.framebuffer([output]);fbo.use();ctx.viewport=(0,0,*size)
 vao.render(mode=moderngl.TRIANGLES,vertices=3);fbo.release();output.repeat_x=False;output.repeat_y=False;output.filter=(moderngl.NEAREST,moderngl.NEAREST)
 return output

w,h=640,360
image=np.zeros((h,w,4),dtype='f4');image[:,:,3]=1
for x in range(0,w,16):image[:,x:x+2,:3]=1
colour=tex(image,True);depth=tex(np.full((h,w,1),.5,dtype='f4'))
checks=0

def pipeline(native,source=colour,angle=180):
 global checks
 vectors=tex(native);owned=[vectors];radius=int(h*.05);tile=((radius-1)//8+1)*8
 values={'_VelocityScale':angle/360.,'_MaxBlurRadius':radius,'_RcpMaxBlurRadius':1/radius,'_ScreenParams':(w,h,1+1/w,1+1/h),'_LoopCount':8.,'_TileMaxLoop':tile//8,'_TileMaxOffs':((tile/8-1)*-.5,)*2}
 packed=run('FragVelocitySetup',{'_CameraMotionVectorsTexture':vectors,'_CameraDepthTexture':depth},(w,h),values);owned.append(packed)
 current=run('FragTileMax1',{'_MainTex':packed},(w//2,h//2),values);owned.append(current)
 for divisor in [4,8]:
  current=run('FragTileMax2',{'_MainTex':current},(w//divisor,h//divisor),values);owned.append(current)
 current=run('FragTileMaxV',{'_MainTex':current},(w//tile,h//tile),values);owned.append(current)
 neighbor=run('FragNeighborMax',{'_MainTex':current},(w//tile,h//tile),values);owned.append(neighbor)
 output=run('FragReconstruction',{'_MainTex':source,'_VelocityTex':packed,'_NeighborMaxTex':neighbor},(w,h),values);owned.append(output)
 result=np.frombuffer(output.read(),dtype='f4').reshape(h,w,4).copy()
 velocity=np.frombuffer(packed.read(),dtype='f4').reshape(h,w,4).copy()
 for t in owned:t.release()
 return result,velocity

zero=np.zeros((h,w,2),dtype='f4');still,_=pipeline(zero)
assert np.max(np.abs(still-image))<.0001,'stationary stock filter blurred';checks+=1
for angle in [270,360]:
 stationary,_=pipeline(zero,angle=angle)
 assert np.max(np.abs(stationary-image))<.0001,'strong preset smeared stationary frame';checks+=1
# Reproduce a subtle turn falling below the stock 2px radius threshold at
# 180 degrees, then verify 270 degrees actually crosses it. No custom blur.
subtle=zero.copy();subtle[:,:,0]=6/w
subtle180,_=pipeline(subtle,angle=180)
subtle270,_=pipeline(subtle,angle=270)
assert np.max(np.abs(subtle180-image))<.0001,'fixture must be below stock threshold';checks+=1
subtle_difference=float(np.mean(np.abs(subtle270[:,:,:3]-image[:,:,:3])))
assert subtle_difference>.01,('270-degree preset still below stock threshold',subtle_difference);checks+=1
# Isolated yaw-like input, with no player translation. Native vectors are synthetic.
native=zero.copy();native[:,:,0]=16/w
blur,velocity=pipeline(native)
assert np.mean(np.abs(blur[:,:,:3]-image[:,:,:3]))>.03,'camera-turn vector did not blur';checks+=1
expected=16*.5*.5
assert np.max(np.abs((velocity[:,:,0]*2-1)*18-expected))<.0001,'exposure is not half a frame';checks+=1
assert np.isfinite(blur).all()and blur.min()>=0 and blur.max()<=1.0001;checks+=1
# A far surface's much smaller projected displacement must stay sharp, not receive
# an artificial walking-speed coefficient. Stock early-out is intentional.
far=zero.copy();far[:,:,0]=.2/w
far_image,_=pipeline(far)
assert np.max(np.abs(far_image-image))<.0001,'far/low-motion surface smeared';checks+=1
horizontal=np.zeros_like(image);horizontal[:,:,3]=1
for y in range(0,h,16):horizontal[y:y+2,:,:3]=1
htex=tex(horizontal,True);pitch=zero.copy();pitch[:,:,1]=16/h
pitch_image,_=pipeline(pitch,htex)
assert np.mean(np.abs(pitch_image[:,:,:3]-horizontal[:,:,:3]))>.03,'pitch vectors ignored';checks+=1
# At a fixed physical speed the exposure span should shrink with increasing FPS,
# unlike the deleted 1/40-second compensation. Keep below the radius clamp.
for fps in [60,120,144,180,240]:
 motion=zero.copy();motion[:,:,0]=(16*180/fps)/w
 _,v=pipeline(motion)
 radius_pixels=(v[h//2,w//2,0]*2-1)*18
 assert abs(radius_pixels-16*180/fps*.25)<.0001,(fps,radius_pixels);checks+=1
# Depth discontinuities and depth-dependent projected translation. These are
# explicit synthetic fixtures, not a claim to have reproduced the user's scene.
depth_values=np.where(image[:,:,0:1]>.5,.1,.8).astype('f4');depth.write(depth_values.tobytes())
edge_image,_=pipeline(native)
assert np.isfinite(edge_image).all()and np.mean(np.abs(edge_image[:,:,:3]-image[:,:,:3]))>.01,'depth edges erased turn blur';checks+=1
# Same lateral camera step: near half has 16px motion, far half 0.2px.
depth_values[:,:w//2]=.1;depth_values[:,w//2:]=.8;depth.write(depth_values.tobytes())
translated=zero.copy();translated[:,:w//2,0]=16/w;translated[:,w//2:,0]=.2/w
translated_image,_=pipeline(translated)
assert np.mean(np.abs(translated_image[:,:w//2-48,:3]-image[:,:w//2-48,:3]))>.03,'near translation missing';checks+=1
assert np.max(np.abs(translated_image[:,w//2+48:,:3]-image[:,w//2+48:,:3]))<.0001,'distant translation smeared';checks+=1
htex.release();colour.release();depth.release();ctx.release()
print('PASS: all 6 exact stock shader fragments compile/validate; %d stock GPU checks on synthetic vectors; package resource GUIDs resolve. NOT Unity/device validation.'%checks)
