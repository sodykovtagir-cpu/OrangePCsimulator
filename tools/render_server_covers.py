"""Blender 4.x: render the exported actual prefab geometry on white.
python3 tools/export_server_cover_scenes.py
blender -b -t 4 --python-exit-code 1 -P tools/render_server_covers.py -- --root .
python3 tools/finalize_server_covers.py
"""
from pathlib import Path
import argparse,json,sys,math
import bpy
from mathutils import Matrix,Vector

def mat(spec,root):
    m=bpy.data.materials.new(spec['name']);m.use_nodes=True;bs=m.node_tree.nodes.get('Principled BSDF')
    def linear(v):return v/12.92 if v<=.04045 else ((v+.055)/1.055)**2.4
    bs.inputs['Base Color'].default_value=tuple(linear(v) for v in spec['color'][:3])+(1,)
    bs.inputs['Roughness'].default_value=spec['roughness'];bs.inputs['Metallic'].default_value=spec['metallic']
    if spec.get('texture'):
        tex=m.node_tree.nodes.new('ShaderNodeTexImage');tex.image=bpy.data.images.load(str(root/spec['texture']),check_existing=True)
        m.node_tree.links.new(tex.outputs['Color'],bs.inputs['Base Color'])
        m.node_tree.links.new(tex.outputs['Alpha'],bs.inputs['Alpha'])
    return m

def light(name,point,target,energy,size):
    data=bpy.data.lights.new(name,'AREA');data.energy=energy;data.shape='DISK';data.size=size
    o=bpy.data.objects.new(name,data);bpy.context.collection.objects.link(o);o.location=point;o.rotation_euler=(target-o.location).to_track_quat('-Z','Y').to_euler()

def main():
    argv=sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else []
    pa=argparse.ArgumentParser();pa.add_argument('--root',default='.');pa.add_argument('--only');args=pa.parse_args(argv)
    root=Path(args.root).resolve();defs=json.loads((root/'tools/server_cover_scenes.json').read_text());output=root/'Assets/Texture2D/Servers';output.mkdir(exist_ok=True)
    conv=Matrix(((1,0,0,0),(0,0,-1,0),(0,1,0,0),(0,0,0,1)))
    for name,definition in defs['scenes'].items():
        if args.only and name!=args.only:continue
        bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
        for d in definition['draws']:
            mesh=bpy.data.meshes.new(d['name']);md=d['mesh'];mesh.from_pydata(md['vertices'],[],md['faces']);mesh.update()
            if md['uv']:
                layer=mesh.uv_layers.new(name='UVMap')
                for poly,coords in zip(mesh.polygons,md['uv']):
                    for loop,uv in zip(poly.loop_indices,coords):layer.data[loop].uv=uv
            for spec in d['materials']:mesh.materials.append(mat(spec,root))
            for poly,idx in zip(mesh.polygons,md['material_indices']):poly.material_index=min(idx,max(0,len(d['materials'])-1))
            obj=bpy.data.objects.new(d['name'],mesh);bpy.context.collection.objects.link(obj);obj.matrix_world=conv@Matrix(d['matrix'])
        bpy.context.view_layer.update();objects=[o for o in bpy.context.scene.objects if o.type=='MESH'];assert objects,name
        for obj in objects:
            for material in obj.data.materials:
                if material and material.use_nodes:
                    for node in material.node_tree.nodes:
                        if node.type=='TEX_IMAGE' and node.image:
                            assert Path(bpy.path.abspath(node.image.filepath)).is_file(),(name,'missing texture',node.image.filepath)
        points=[o.matrix_world@Vector(c) for o in objects for c in o.bound_box];lo=Vector(tuple(min(v[i] for v in points) for i in range(3)));hi=Vector(tuple(max(v[i] for v in points) for i in range(3)));center=(lo+hi)/2;extent=max(hi-lo)
        scene=bpy.context.scene;scene.render.engine='CYCLES';scene.cycles.samples=64;scene.cycles.use_denoising=False;scene.cycles.max_bounces=5
        scene.render.resolution_x=512;scene.render.resolution_y=512;scene.render.resolution_percentage=100;scene.render.film_transparent=True
        scene.render.image_settings.file_format='PNG';scene.render.image_settings.color_mode='RGBA'
        scene.view_settings.view_transform='Standard';scene.view_settings.look='None';scene.view_settings.exposure=-.25
        camera=bpy.data.objects.new('Shop camera',bpy.data.cameras.new('Shop camera'));bpy.context.collection.objects.link(camera)
        camera.location=center+Vector((6,8,5)).normalized()*extent*5;camera.rotation_euler=(center-camera.location).to_track_quat('-Z','Y').to_euler();camera.data.type='ORTHO';scene.camera=camera
        bpy.context.view_layer.update();iv=camera.matrix_world.inverted();screen=[iv@v for v in points];camera.data.ortho_scale=max(max(v.x for v in screen)-min(v.x for v in screen),max(v.y for v in screen)-min(v.y for v in screen))/.88
        world=bpy.data.worlds.new('Studio');world.use_nodes=True;world.node_tree.nodes['Background'].inputs['Color'].default_value=(1,1,1,1);world.node_tree.nodes['Background'].inputs['Strength'].default_value=.6;scene.world=world
        light('Key',center+Vector((1.2,1.8,2.8))*extent,center,95*extent*extent,extent*2.5)
        light('Fill',center+Vector((-2,.4,1))*extent,center,28*extent*extent,extent*2)
        scene.render.filepath=str(output/(name+'.png'));bpy.ops.render.render(write_still=True)
        print('RENDERED',name,len(objects),'actual mesh objects',flush=True)
if __name__=='__main__':main()
