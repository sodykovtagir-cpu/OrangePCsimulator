#!/usr/bin/env python3
"""Render the real composite HLSL on headless Mesa/EGL via SPIR-V + GLSL.
Requires glslang-tools, spirv-cross, libegl1, Mesa DRI, moderngl and numpy.
Not a Unity/GLES device test: Unity macros are replaced by the same compile shim.
"""
from pathlib import Path
import ast,json,re,struct,subprocess,tempfile
import moderngl
import numpy as np
R=Path(__file__).resolve().parents[1]
tree=ast.parse((R/'tools/test_motion_blur_stability.py').read_text())
shim=next(n.value.value for n in ast.walk(tree)if isinstance(n,ast.Assign)and any(isinstance(t,ast.Name)and t.id=='shim'for t in n.targets))
shader=(R/'Assets/Resources/SimpleScreenFx.shader').read_text()
body=re.findall(r'CGPROGRAM(.*?)ENDCG',shader,re.S)[3]
body=re.sub(r'^\s*#pragma[^\n]*','',body,flags=re.M).replace('#include "UnityCG.cginc"',shim)
with tempfile.TemporaryDirectory()as td:
 d=Path(td);source=d/'composite.hlsl';source.write_text(body);spv=d/'composite.spv';frag=d/'composite.frag'
 subprocess.run(['glslangValidator','-D','-V','--hlsl-dx9-compatible','--auto-map-bindings','--auto-map-locations','-S','frag','-e','frag','-o',str(spv),str(source)],check=True,capture_output=True)
 subprocess.run(['spirv-cross',str(spv),'--version','330','--output',str(frag)],check=True)
 reflection=json.loads(subprocess.check_output(['spirv-cross',str(spv),'--reflect'],text=True))
 ctx=moderngl.create_standalone_context(backend='egl')
 vertex='#version 330\nout vec2 i_uv;void main(){vec2 p=vec2((gl_VertexID<<1)&2,gl_VertexID&2);i_uv=p;gl_Position=vec4(p*2.0-1.0,0,1);}'
 program=ctx.program(vertex_shader=vertex,fragment_shader=frag.read_text());vao=ctx.vertex_array(program,[])
 w,h=640,360;image=np.zeros((h,w,4),dtype='f4');image[:,:,3]=1
 for x in range(0,w,16):image[:,x:x+2,:3]=1
 texture=ctx.texture((w,h),4,image.tobytes(),dtype='f4');texture.filter=(moderngl.LINEAR,moderngl.LINEAR);texture.repeat_x=False;texture.repeat_y=False
 motion=np.zeros((h,w,2),dtype='f4');vectors=ctx.texture((w,h),2,motion.tobytes(),dtype='f4')
 depth=ctx.texture((w,h),1,np.ones((h,w),dtype='f4').tobytes(),dtype='f4');bloom=ctx.texture((w,h),4,np.zeros_like(image).tobytes(),dtype='f4')
 for name,tex in [('_MainTex',texture),('_CameraMotionVectorsTexture',vectors),('_CameraDepthTexture',depth),('_BloomTex',bloom)]:
  binding=next(t['binding']for t in reflection['textures']if t['name']==name);tex.use(binding);program[name].value=binding
 block=reflection['ubos'][0];members=reflection['types'][block['type']]['members'];uniforms=ctx.buffer(reserve=max(96,block['block_size']));uniforms.bind_to_uniform_block(block['binding'])
 output=ctx.texture((w,h),4,dtype='f4');fbo=ctx.framebuffer([output]);fbo.use()
 def render(enabled=1,use_vectors=1,fallback=(.01,0,0,0),native=(0,0),fps=60,depth_edges=False):
  depth_values=np.where(image[:,:,0]>.5,1.,.25).astype('f4') if depth_edges else np.ones((h,w),dtype='f4')
  depth.write(depth_values.tobytes())
  motion[:,:,0]=native[0];motion[:,:,1]=native[1];vectors.write(motion.tobytes())
  values={'_Motion':enabled,'_UseMotionVectors':use_vectors,'_FallbackMotion':fallback,'_Shutter':min(6.,fps/40.),'_MaxMotionPixels':24.,'_MainTex_TexelSize':(1/w,1/h,w,h)}
  data=bytearray(uniforms.size)
  for member in members:
   v=values.get(member['name'],(0,0,0,0)if member['type']=='vec4'else 0)
   args=v if isinstance(v,tuple)else(v,);struct.pack_into('<'+'f'*len(args),data,member['offset'],*args)
  uniforms.write(data);vao.render(mode=moderngl.TRIANGLES,vertices=3);return np.frombuffer(fbo.read(components=4,dtype='f4'),dtype='f4').reshape(h,w,4)
 off=render(enabled=0);assert np.max(np.abs(off-image))<.0001
 blank=render();difference=float(np.mean(np.abs(blank[:,:,:3]-off[:,:,:3])));assert difference>.05,('blank vectors removed camera blur',difference)
 fallback=render(use_vectors=0);assert np.max(np.abs(blank-fallback))<.0001,'blank GPU path differs from camera fallback'
 still=render(fallback=(0,0,0,0));assert np.max(np.abs(still-off))<.0001,'stationary view blurred'
 moving_object=render(fallback=(0,0,0,0),native=(.02,0));assert np.mean(np.abs(moving_object-off))>.05,'valid native vectors ignored'
 invalid=render(fallback=(0,0,0,0),native=(1,1));assert np.max(np.abs(invalid-off))<.0001,'invalid/default-white vector texture blurred static view'
 # A thin white line must acquire intermediate neighbouring pixels, not remain sharp.
 assert .01<blank[h//2,4,0]<.9
 assert np.isfinite(blank).all()and blank.min()>=0 and blank.max()<=1.0001
 weak=render(fallback=(.9/180,0,0,0),native=(.3/w,0),fps=180)
 camera_only=render(fallback=(.9/180,0,0,0),use_vectors=0,fps=180)
 assert np.max(np.abs(weak-camera_only))<.0001,'weak native vector replaced stronger camera blur'
 # Reproduce the reported 180-FPS case: real depth discontinuities plus
 # a weak but nonzero native vector that used to suppress camera displacement.
 at180=render(fallback=(.9/180,0,0,0),native=(.3/w,0),fps=180,depth_edges=True)
 change180=float(np.mean(np.abs(at180[:,:,:3]-off[:,:,:3])))
 assert change180>.03,('180 FPS: depth/native selection erased camera blur',change180)
 references=[]
 for fps in [60,120,144,180,240]:
  frame=render(fallback=(.9/fps,0,0,0),native=(.3/w,0),fps=fps,depth_edges=True)
  assert np.mean(np.abs(frame[:,:,:3]-off[:,:,:3]))>.03,(fps,'effect invisible')
  references.append(frame)
 for frame in references:assert np.max(np.abs(frame-at180))<.003,'different blur at same camera speed'
 still_depth=render(fallback=(0,0,0,0),fps=180,depth_edges=True)
 assert np.max(np.abs(still_depth-off))<.0001,'depth filter smeared stationary scene'
 print('PASS: 21 GPU visibility checks including 60/120/144/180/240 FPS, depth discontinuities and weak nonzero native vectors: blank native texture still blurs, GPU/camera paths, static/off sharpness, invalid vectors, thin-line spread and bounded colour; 180-FPS mean pixel change %.3f'%change180)
 ctx.release()
