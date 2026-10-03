import os

CUBE_MESH = "10202"
CYLINDER_MESH = "10206"
QUAD_MESH = "10210"

# Materials
MAT_METAL = "ec065bf9dbee0f5488ef8418c5180982"
MAT_WALL = "3aeb13925290c2c4280ee2fa35130faf"
MAT_GROUND = "b84a6ff4f86c5bd4ca18ecef0cb390db"
MAT_BLACK = "9a7daad72ecda3f4abc0e90021cd55f8"
MAT_LIGHTON = "09395d106c433fb4d95c650708ac6fea"
MAT_CASE = "b0f6cca6ffa03504cb91941481e0b19c"
MAT_PEDESTAL = "0c81a9a892a43df4a85c9dac4bf54555"

FLYBY_WP_SCRIPT_GUID = "b7bc5000aa504a879c260db91b195a87"

class PrefabBuilder:
    def __init__(self):
        self._cur_id = 6000000000000000
        self.chunks = []

    def next_id(self):
        self._cur_id += 1
        return str(self._cur_id)

    def add(self, chunk):
        self.chunks.append(chunk.strip())

b = PrefabBuilder()

# IDs
id_root_go = b.next_id()
id_root_tr = b.next_id()

id_target_go = b.next_id()
id_target_tr = b.next_id()

id_struct_go = b.next_id()
id_struct_tr = b.next_id()

id_center_go = b.next_id()
id_center_tr = b.next_id()

id_lights_go = b.next_id()
id_lights_tr = b.next_id()

id_wps_go = b.next_id()
id_wps_tr = b.next_id()

# Helper for mesh objects
def add_mesh_obj(name, parent_tr, pos, rot, scale, mesh_id, mat_guid):
    go_id = b.next_id()
    tr_id = b.next_id()
    mf_id = b.next_id()
    mr_id = b.next_id()

    chunk = f"""
--- !u!1 &{go_id}
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  serializedVersion: 6
  m_Component:
  - component: {{fileID: {tr_id}}}
  - component: {{fileID: {mf_id}}}
  - component: {{fileID: {mr_id}}}
  m_Layer: 0
  m_Name: {name}
  m_TagString: Untagged
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!4 &{tr_id}
Transform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {go_id}}}
  serializedVersion: 2
  m_LocalRotation: {{x: {rot[0]}, y: {rot[1]}, z: {rot[2]}, w: {rot[3]}}}
  m_LocalPosition: {{x: {pos[0]}, y: {pos[1]}, z: {pos[2]}}}
  m_LocalScale: {{x: {scale[0]}, y: {scale[1]}, z: {scale[2]}}}
  m_ConstrainProportionsScale: 0
  m_Children: []
  m_Father: {{fileID: {parent_tr}}}
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
--- !u!33 &{mf_id}
MeshFilter:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {go_id}}}
  m_Mesh: {{fileID: {mesh_id}, guid: 0000000000000000e000000000000000, type: 0}}
--- !u!23 &{mr_id}
MeshRenderer:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {go_id}}}
  m_Enabled: 1
  m_CastShadows: 1
  m_ReceiveShadows: 1
  m_DynamicOccludee: 1
  m_Materials:
  - {{fileID: 2100000, guid: {mat_guid}, type: 2}}
  m_Lightmapping: 4
"""
    b.add(chunk)
    return tr_id

# Light objects
def add_light_obj(name, parent_tr, pos, rot, l_type, color, intensity, range_val, spot_angle=30):
    go_id = b.next_id()
    tr_id = b.next_id()
    lt_id = b.next_id()

    chunk = f"""
--- !u!1 &{go_id}
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  serializedVersion: 6
  m_Component:
  - component: {{fileID: {tr_id}}}
  - component: {{fileID: {lt_id}}}
  m_Layer: 0
  m_Name: {name}
  m_TagString: Untagged
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!4 &{tr_id}
Transform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {go_id}}}
  serializedVersion: 2
  m_LocalRotation: {{x: {rot[0]}, y: {rot[1]}, z: {rot[2]}, w: {rot[3]}}}
  m_LocalPosition: {{x: {pos[0]}, y: {pos[1]}, z: {pos[2]}}}
  m_LocalScale: {{x: 1, y: 1, z: 1}}
  m_ConstrainProportionsScale: 0
  m_Children: []
  m_Father: {{fileID: {parent_tr}}}
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
--- !u!108 &{lt_id}
Light:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {go_id}}}
  m_Enabled: 1
  serializedVersion: 10
  m_Type: {l_type}
  m_Color: {{r: {color[0]}, g: {color[1]}, b: {color[2]}, a: 1}}
  m_Intensity: {intensity}
  m_Range: {range_val}
  m_SpotAngle: {spot_angle}
  m_Lightmapping: 4
"""
    b.add(chunk)
    return tr_id

# Waypoint objects
def add_waypoint_obj(name, parent_tr, pos, rot, phase_name, duration, load_mult, look_at_target_tr):
    go_id = b.next_id()
    tr_id = b.next_id()
    wp_id = b.next_id()

    chunk = f"""
--- !u!1 &{go_id}
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  serializedVersion: 6
  m_Component:
  - component: {{fileID: {tr_id}}}
  - component: {{fileID: {wp_id}}}
  m_Layer: 0
  m_Name: {name}
  m_TagString: Untagged
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!4 &{tr_id}
Transform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {go_id}}}
  serializedVersion: 2
  m_LocalRotation: {{x: {rot[0]}, y: {rot[1]}, z: {rot[2]}, w: {rot[3]}}}
  m_LocalPosition: {{x: {pos[0]}, y: {pos[1]}, z: {pos[2]}}}
  m_LocalScale: {{x: 1, y: 1, z: 1}}
  m_ConstrainProportionsScale: 0
  m_Children: []
  m_Father: {{fileID: {parent_tr}}}
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
--- !u!114 &{wp_id}
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {go_id}}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {FLYBY_WP_SCRIPT_GUID}, type: 3}}
  m_Name: 
  m_EditorClassIdentifier: 
  phaseName: {phase_name}
  duration: {duration}
  loadMultiplier: {load_mult}
  lookAtTarget: {{fileID: {look_at_target_tr}}}
  useTransformRotation: 1
"""
    b.add(chunk)
    return tr_id

# 1. Structure meshes
struct_children = []
struct_children.append(add_mesh_obj("Floor", id_struct_tr, (0, -0.1, 0), (0, 0, 0, 1), (18, 0.2, 18), CUBE_MESH, MAT_GROUND))
struct_children.append(add_mesh_obj("Ceiling", id_struct_tr, (0, 6.1, 0), (0, 0, 0, 1), (18, 0.2, 18), CUBE_MESH, MAT_BLACK))
struct_children.append(add_mesh_obj("Wall_North", id_struct_tr, (0, 3, 9), (0, 0, 0, 1), (18, 6, 0.2), CUBE_MESH, MAT_WALL))
struct_children.append(add_mesh_obj("Wall_South", id_struct_tr, (0, 3, -9), (0, 0, 0, 1), (18, 6, 0.2), CUBE_MESH, MAT_WALL))
struct_children.append(add_mesh_obj("Wall_East", id_struct_tr, (9, 3, 0), (0, 0, 0, 1), (0.2, 6, 18), CUBE_MESH, MAT_WALL))
struct_children.append(add_mesh_obj("Wall_West", id_struct_tr, (-9, 3, 0), (0, 0, 0, 1), (0.2, 6, 18), CUBE_MESH, MAT_WALL))

# 4 Corner Pillars with Neon Strips
struct_children.append(add_mesh_obj("Pillar_NW", id_struct_tr, (-6, 3, 6), (0, 0, 0, 1), (1, 6, 1), CUBE_MESH, MAT_METAL))
struct_children.append(add_mesh_obj("Neon_NW", id_struct_tr, (-5.9, 3, 5.4), (0, 0, 0, 1), (0.1, 4.5, 0.1), CUBE_MESH, MAT_LIGHTON))

struct_children.append(add_mesh_obj("Pillar_NE", id_struct_tr, (6, 3, 6), (0, 0, 0, 1), (1, 6, 1), CUBE_MESH, MAT_METAL))
struct_children.append(add_mesh_obj("Neon_NE", id_struct_tr, (5.9, 3, 5.4), (0, 0, 0, 1), (0.1, 4.5, 0.1), CUBE_MESH, MAT_LIGHTON))

struct_children.append(add_mesh_obj("Pillar_SW", id_struct_tr, (-6, 3, -6), (0, 0, 0, 1), (1, 6, 1), CUBE_MESH, MAT_METAL))
struct_children.append(add_mesh_obj("Neon_SW", id_struct_tr, (-5.9, 3, -5.4), (0, 0, 0, 1), (0.1, 4.5, 0.1), CUBE_MESH, MAT_LIGHTON))

struct_children.append(add_mesh_obj("Pillar_SE", id_struct_tr, (6, 3, -6), (0, 0, 0, 1), (1, 6, 1), CUBE_MESH, MAT_METAL))
struct_children.append(add_mesh_obj("Neon_SE", id_struct_tr, (5.9, 3, -5.4), (0, 0, 0, 1), (0.1, 4.5, 0.1), CUBE_MESH, MAT_LIGHTON))

# 2. Centerpiece
center_children = []
center_children.append(add_mesh_obj("Pedestal_Base", id_center_tr, (0, 0.35, 0), (0, 0, 0, 1), (3.2, 0.7, 3.2), CYLINDER_MESH, MAT_PEDESTAL))
center_children.append(add_mesh_obj("Pedestal_Ring", id_center_tr, (0, 0.72, 0), (0, 0, 0, 1), (3.3, 0.06, 3.3), CYLINDER_MESH, MAT_LIGHTON))
center_children.append(add_mesh_obj("Showcase_PC_Case", id_center_tr, (0, 1.4, 0), (0, 0, 0, 1), (0.9, 1.3, 1.3), CUBE_MESH, MAT_CASE))
center_children.append(add_mesh_obj("Hardware_Core_Glow", id_center_tr, (0, 1.4, 0), (0, 0, 0, 1), (0.92, 0.15, 0.95), CUBE_MESH, MAT_LIGHTON))

# 3. Lights
lights_children = []
# Directional key light (type 1)
lights_children.append(add_light_obj("Key_Sun_Light", id_lights_tr, (0, 5.5, 0), (0.42, -0.25, 0.12, 0.86), 1, (0.85, 0.9, 1.0), 0.8, 20))
# Orange Point Light (type 2)
lights_children.append(add_light_obj("Neon_Orange_Point", id_lights_tr, (3.8, 2.5, -3.8), (0, 0, 0, 1), 2, (1.0, 0.55, 0.08), 2.5, 14))
# Cyan Point Light (type 2)
lights_children.append(add_light_obj("Neon_Cyan_Point", id_lights_tr, (-3.8, 2.5, 3.8), (0, 0, 0, 1), 2, (0.1, 0.75, 1.0), 2.5, 14))
# Spotlight pointing at pedestal (type 0)
lights_children.append(add_light_obj("Pedestal_Spot", id_lights_tr, (0, 5.8, 0), (0.7071, 0, 0, 0.7071), 0, (1.0, 0.98, 0.9), 3.2, 10, spot_angle=58))

# 4. Waypoints
wp_children = []
wp_children.append(add_waypoint_obj("Point_1_Entrance", id_wps_tr, (-6.2, 4.4, -6.2), (0.18, 0.38, -0.07, 0.90), "Scene 1: Tech Hall Flyby", 4.2, 0.92, id_target_tr))
wp_children.append(add_waypoint_obj("Point_2_Overview", id_wps_tr, (5.2, 3.2, -4.2), (0.14, -0.42, 0.06, 0.89), "Scene 2: Hardware Showcase Test", 4.0, 0.85, id_target_tr))
wp_children.append(add_waypoint_obj("Point_3_CloseUp", id_wps_tr, (2.4, 1.4, 2.4), (0.06, -0.85, 0.10, 0.51), "Scene 3: Dynamic Lighting & Shadows", 4.5, 1.18, id_target_tr))
wp_children.append(add_waypoint_obj("Point_4_Pass", id_wps_tr, (-3.4, 1.2, 2.2), (0.05, 0.78, -0.07, 0.62), "Scene 4: Stress Test Sweep", 3.8, 1.25, id_target_tr))
wp_children.append(add_waypoint_obj("Point_5_HighAngle", id_wps_tr, (-6.2, 4.4, -6.2), (0.18, 0.38, -0.07, 0.90), "Scene 1: Tech Hall Flyby", 4.2, 0.92, id_target_tr))

# Root & Category Containers
def format_children(child_ids):
    lines = []
    for cid in child_ids:
        lines.append(f"  - {{fileID: {cid}}}")
    return "\n".join(lines)

b.add(f"""
--- !u!1 &{id_root_go}
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  serializedVersion: 6
  m_Component:
  - component: {{fileID: {id_root_tr}}}
  m_Layer: 0
  m_Name: 3DMork_Stage
  m_TagString: Untagged
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!4 &{id_root_tr}
Transform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {id_root_go}}}
  serializedVersion: 2
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalPosition: {{x: 0, y: 0, z: 0}}
  m_LocalScale: {{x: 1, y: 1, z: 1}}
  m_ConstrainProportionsScale: 0
  m_Children:
  - {{fileID: {id_target_tr}}}
  - {{fileID: {id_struct_tr}}}
  - {{fileID: {id_center_tr}}}
  - {{fileID: {id_lights_tr}}}
  - {{fileID: {id_wps_tr}}}
  m_Father: {{fileID: 0}}
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
--- !u!1 &{id_target_go}
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  serializedVersion: 6
  m_Component:
  - component: {{fileID: {id_target_tr}}}
  m_Layer: 0
  m_Name: ShowcaseTarget
  m_TagString: Untagged
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!4 &{id_target_tr}
Transform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {id_target_go}}}
  serializedVersion: 2
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalPosition: {{x: 0, y: 1.4, z: 0}}
  m_LocalScale: {{x: 1, y: 1, z: 1}}
  m_ConstrainProportionsScale: 0
  m_Children: []
  m_Father: {{fileID: {id_root_tr}}}
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
--- !u!1 &{id_struct_go}
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  serializedVersion: 6
  m_Component:
  - component: {{fileID: {id_struct_tr}}}
  m_Layer: 0
  m_Name: Structure
  m_TagString: Untagged
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!4 &{id_struct_tr}
Transform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {id_struct_go}}}
  serializedVersion: 2
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalPosition: {{x: 0, y: 0, z: 0}}
  m_LocalScale: {{x: 1, y: 1, z: 1}}
  m_ConstrainProportionsScale: 0
  m_Children:
{format_children(struct_children)}
  m_Father: {{fileID: {id_root_tr}}}
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
--- !u!1 &{id_center_go}
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  serializedVersion: 6
  m_Component:
  - component: {{fileID: {id_center_tr}}}
  m_Layer: 0
  m_Name: Centerpiece
  m_TagString: Untagged
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!4 &{id_center_tr}
Transform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {id_center_go}}}
  serializedVersion: 2
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalPosition: {{x: 0, y: 0, z: 0}}
  m_LocalScale: {{x: 1, y: 1, z: 1}}
  m_ConstrainProportionsScale: 0
  m_Children:
{format_children(center_children)}
  m_Father: {{fileID: {id_root_tr}}}
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
--- !u!1 &{id_lights_go}
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  serializedVersion: 6
  m_Component:
  - component: {{fileID: {id_lights_tr}}}
  m_Layer: 0
  m_Name: Lighting
  m_TagString: Untagged
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!4 &{id_lights_tr}
Transform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {id_lights_go}}}
  serializedVersion: 2
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalPosition: {{x: 0, y: 0, z: 0}}
  m_LocalScale: {{x: 1, y: 1, z: 1}}
  m_ConstrainProportionsScale: 0
  m_Children:
{format_children(lights_children)}
  m_Father: {{fileID: {id_root_tr}}}
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
--- !u!1 &{id_wps_go}
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  serializedVersion: 6
  m_Component:
  - component: {{fileID: {id_wps_tr}}}
  m_Layer: 0
  m_Name: Waypoints
  m_TagString: Untagged
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!4 &{id_wps_tr}
Transform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {id_wps_go}}}
  serializedVersion: 2
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalPosition: {{x: 0, y: 0, z: 0}}
  m_LocalScale: {{x: 1, y: 1, z: 1}}
  m_ConstrainProportionsScale: 0
  m_Children:
{format_children(wp_children)}
  m_Father: {{fileID: {id_root_tr}}}
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
""")

full_yaml = "%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n" + "\n".join(b.chunks) + "\n"
out_path = "Assets/Resources/3DMork_Stage.prefab"
with open(out_path, "w", encoding="utf-8") as f:
    f.write(full_yaml)

print(f"Successfully wrote {out_path} ({len(full_yaml)} bytes, {len(b.chunks)} blocks)")
