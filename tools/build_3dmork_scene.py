import os
from build_3dmork_stage import b, id_root_tr

# Create scene by taking all chunks and appending SceneRoots
scene_yaml = "%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n" + "\n".join(b.chunks) + f"""
--- !u!1660057539 &9223372036854775807
SceneRoots:
  m_ObjectHideFlags: 0
  m_Roots:
  - {{fileID: {id_root_tr}}}
"""

out_scene = "Assets/Scenes/3DMork_Room.unity"
with open(out_scene, "w", encoding="utf-8") as f:
    f.write(scene_yaml)

print(f"Successfully wrote {out_scene}")
