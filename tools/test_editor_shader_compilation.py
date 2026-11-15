#!/usr/bin/env python3
"""Static guard for the Editor-only Internal-Loading workaround.
Does not reproduce Unity rendering or assert that every invalid-pass error is fixed.
"""
from pathlib import Path
import json,re
R=Path(__file__).resolve().parents[1]
settings=(R/'ProjectSettings/EditorSettings.asset').read_text()
values=re.findall(r'^  m_AsyncShaderCompilation: (\d+)\s*$',settings,re.M)
assert values==['0'], 'Editor must compile real shader passes synchronously, not draw the loading placeholder'
manifest=json.loads((R/'Packages/manifest.json').read_text())
assert manifest['dependencies']['com.unity.postprocessing']=='3.4.0'
assert 'm_CustomRenderPipeline: {fileID: 0}' in (R/'ProjectSettings/GraphicsSettings.asset').read_text()
stock=(R/'Assets/Scripts/Assembly-CSharp/StockMotionBlur.cs').read_text()
assert 'layer.Init(resources);' in stock and 'profile.AddSettings<MotionBlur>()' in stock
assert 'resources.shaders.motionBlur' in stock
print('PASS: synchronous Editor shader compilation; stock PP 3.4.0 integration and Built-in pipeline preserved')
print('NOTE: static settings guard only; cold-cache Play Mode must be verified in Unity Editor')
