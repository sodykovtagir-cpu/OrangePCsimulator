#!/usr/bin/env python3
"""Real UiScale across render-resolution/DPI; authored HUD invariants across rooms.
No claim of actual Unity Canvas layout/device screenshots.
"""
from pathlib import Path
import subprocess,tempfile,re
R=Path(__file__).resolve().parents[1];C=R/'Assets/Scripts/Assembly-CSharp'
s=(C/'DragRotateControls.cs').read_text()
method=s[s.index('    public static float UiScale()'):s.index('    private void Build(Canvas canvas)')]
consts='\n'.join(re.findall(r'    private const float (?:PanelSize|BigArrowSize|BigArrowOffset|FineArrowSize|FineArrowOffset|ToggleSize) = .*;',s))
h=r'''using System;using UnityEngine;
namespace UnityEngine{public static class Screen{public static int width,height;public static float dpi;}public static class Mathf{public static float Min(float a,float b)=>Math.Min(a,b);public static float Max(float a,float b)=>Math.Max(a,b);public static float Clamp(float a,float b,float c)=>Max(b,Min(c,a));}}
class Grid{CONSTS METHOD}
class Test{static void Main(){int n=0;foreach(float dpi in new[]{0f,160f,440f,640f})foreach(var wh in new[]{new[]{1568,706},new[]{784,353},new[]{3136,1412},new[]{2340,1080},new[]{819,378},new[]{4680,2160}}){Screen.width=wh[0];Screen.height=wh[1];Screen.dpi=dpi;float scale=Grid.UiScale();if(Math.Abs(scale-.85f)>.0001f)throw new Exception("render-resolution/DPI changes arrow geometry");n++;}Console.WriteLine("PASS: "+n+" real UiScale checks, same Factory-reference 340-unit panel at 35-100% render resolutions and all DPI values");}}
'''.replace('CONSTS',consts).replace('METHOD',method)
with tempfile.TemporaryDirectory() as td:
 d=Path(td);(d/'test.cs').write_text(h)
 subprocess.run(['mcs','-out:'+str(d/'test.exe'),str(d/'test.cs')],check=True)
 subprocess.run(['mono',str(d/'test.exe')],check=True)
assert 'rig.mobile.GetComponentInParent<Canvas>()' in s
assert 'viewport.rect.width < 1f' in s and 'canvas.scaleFactor <= 0f' in s
# Medium/Large/Double Storey/Factory must all use identical reference units.
for name in ['Main','Main 2','Main 3','Main 4']:
 raw=(R/f'Assets/Scenes/{name}.unity').read_text()
 ds={m[0]:m[1]for m in re.findall(r'--- !u!\d+ &(-?\d+)\n(.*?)(?=--- !u!|\Z)',raw,re.S)}
 scaler=None
 for b in ds.values():
  if 'm_ReferenceResolution:' not in b:continue
  go=re.search(r'm_GameObject: \{fileID: (\d+)\}',b)[1]
  if 'm_Name: Main\n' in ds[go]:scaler=b;break
 assert scaler and 'm_ReferenceResolution: {x: 800, y: 500}' in scaler
 assert 'm_ScreenMatchMode: 1' in scaler and 'm_UiScaleMode: 1' in scaler
print('PASS: deterministic ControlRig HUD + ready-layout gate; all four room HUDs use identical Expand units')
