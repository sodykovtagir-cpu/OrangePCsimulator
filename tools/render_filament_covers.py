"""Render shop covers from the actual Filament.fbx, not replacement geometry.

Blender 4.x:
  blender -b -t 4 -P tools/render_filament_covers.py -- --root .
Optional --only RedFilamentMatte for one preview. Output is 512 px; run
  python3 tools/wire_filament_covers.py
to downsample to the game's 256 px icon format and wire TextureImporter sprites.
The original FBX, prefab transforms and runtime materials are never modified.
"""
import argparse
import hashlib
import json
import math
from pathlib import Path
import re
import sys

import bpy
from mathutils import Vector


def linear(c):
    return c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4


def material(name, color, roughness=0.5, emission=None):
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes.get('Principled BSDF')
    bsdf.inputs['Base Color'].default_value = (*color, 1)
    bsdf.inputs['Roughness'].default_value = roughness
    bsdf.inputs['Metallic'].default_value = 0
    if emission:
        bsdf.inputs['Emission Color'].default_value = (*emission, 1)
        bsdf.inputs['Emission Strength'].default_value = 1.0
    return mat


def light(name, location, energy, size, target):
    data = bpy.data.lights.new(name, 'AREA')
    data.energy = energy
    data.shape = 'DISK'
    data.size = size
    obj = bpy.data.objects.new(name, data)
    bpy.context.collection.objects.link(obj)
    obj.location = location
    obj.rotation_euler = (target - obj.location).to_track_quat('-Z', 'Y').to_euler()


def main():
    argv = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
    parser = argparse.ArgumentParser()
    parser.add_argument('--root', default='.')
    parser.add_argument('--only')
    args = parser.parse_args(argv)
    root = Path(args.root).resolve()
    source = root / 'Assets/Models/Filament.fbx'
    output = root / 'Assets/Texture2D/Filaments'
    output.mkdir(parents=True, exist_ok=True)

    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.object.delete(use_global=False)
    bpy.ops.import_scene.fbx(filepath=str(source))
    bpy.context.view_layer.update()
    meshes = [obj for obj in bpy.context.scene.objects if obj.type == 'MESH']
    assert {o.name for o in meshes} == {'Filament', 'Reel'}, 'Review changed FBX before rendering'
    plastic = bpy.data.objects['Filament']
    reel = bpy.data.objects['Reel']

    # Use FBX geometry and orientation as authored: reel axle along X, no
    # synthetic torus, wire texture, label, badge, plane or spool replacement.
    corners = [o.matrix_world @ Vector(c) for o in meshes for c in o.bound_box]
    lo = Vector(tuple(min(v[i] for v in corners) for i in range(3)))
    hi = Vector(tuple(max(v[i] for v in corners) for i in range(3)))
    center = (lo + hi) / 2
    extent = max(hi - lo)

    # Reproduce the references: orthographic product view from above, clean
    # white background, no text/floor/drop shadow, consistent crop for all SKUs.
    scene = bpy.context.scene
    camera_data = bpy.data.cameras.new('Shop camera')
    camera = bpy.data.objects.new('Shop camera', camera_data)
    bpy.context.collection.objects.link(camera)
    camera.location = center + Vector((5.8, -7.6, 5.6)).normalized() * extent * 5
    camera.rotation_euler = (center - camera.location).to_track_quat('-Z', 'Y').to_euler()
    camera_data.type = 'ORTHO'
    scene.camera = camera
    camera_inv = camera.matrix_world.inverted()
    # Update camera transform before calculating the screen-space silhouette.
    bpy.context.view_layer.update()
    camera_inv = camera.matrix_world.inverted()
    framed = [camera_inv @ v for v in corners]
    span_x = max(v.x for v in framed) - min(v.x for v in framed)
    span_y = max(v.y for v in framed) - min(v.y for v in framed)
    camera_data.ortho_scale = max(span_x, span_y) / 0.88

    scene.render.engine = 'CYCLES'
    scene.cycles.samples = 96
    scene.cycles.use_denoising = False
    scene.cycles.max_bounces = 5
    scene.render.resolution_x = 512
    scene.render.resolution_y = 512
    scene.render.resolution_percentage = 100
    scene.render.film_transparent = True
    scene.render.image_settings.file_format = 'PNG'
    scene.render.image_settings.color_mode = 'RGBA'
    scene.view_settings.view_transform = 'Standard'
    scene.view_settings.look = "None"
    scene.view_settings.exposure = -0.3
    scene.view_settings.gamma = 1
    world = bpy.data.worlds.new('Soft studio ambient')
    world.use_nodes = True
    world.node_tree.nodes['Background'].inputs['Color'].default_value = (1, 1, 1, 1)
    world.node_tree.nodes['Background'].inputs['Strength'].default_value = 0.65
    scene.world = world
    light('Key', center + Vector((1.2, -1.8, 2.8)) * extent, 95 * extent * extent, extent * 2.5, center)
    light('Fill', center + Vector((-2, -0.4, 1)) * extent, 28 * extent * extent, extent * 2, center)

    # Preserve the imported reel's dark housing; only the Filament child is
    # tinted in the game's FilamentSpool component.
    for mat in reel.data.materials:
        if mat.use_nodes:
            node = mat.node_tree.nodes.get('Principled BSDF')
            if node:
                node.inputs['Roughness'].default_value = 0.62
                node.inputs['Metallic'].default_value = 0

    manifest = []
    for path in sorted((root / 'Assets/MonoBehaviour').glob('*Filament*.asset')):
        name = path.stem
        if args.only and name != args.only:
            continue
        prefab = root / ('Assets/Resources/components/' + name + '.prefab')
        text = prefab.read_text()
        match = re.search(r'filamentColor: \{r: ([^,]+), g: ([^,]+), b: ([^,]+), a: ([^}]+)\}', text)
        rgb = tuple(float(x) for x in match.groups()[:3])
        finish = int(re.search(r'^  finish: (\d+)', text, re.M).group(1))
        color = tuple(linear(c) for c in rgb)
        # Unity smoothness .12/.85 maps to a visibly matte/glossy surface.
        mat = material(name, color, 0.88 if finish == 0 else 0.15,
                       tuple(c * 0.8 for c in color) if finish == 2 else None)
        plastic.data.materials.clear()
        plastic.data.materials.append(mat)
        scene.render.filepath = str(output / (name + '.png'))
        bpy.ops.render.render(write_still=True)
        manifest.append({'name': name, 'color_srgb': rgb, 'finish': finish})
        print('COVER_RENDERED', name, flush=True)
    (root / 'tools/filament_cover_render_manifest.json').write_text(json.dumps({
        'source': 'Assets/Models/Filament.fbx',
        'source_sha256': hashlib.sha256(source.read_bytes()).hexdigest(),
        'geometry': {o.name: {'vertices': len(o.data.vertices), 'faces': len(o.data.polygons)} for o in meshes},
        'camera_direction': [5.8, -7.6, 5.6],
        'projection': 'orthographic', 'background': 'white', 'render_size': 512,
        'covers': manifest,
    }, indent=2) + '\n')


if __name__ == '__main__':
    main()
