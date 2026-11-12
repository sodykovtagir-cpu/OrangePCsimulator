#!/usr/bin/env python3
"""Downsample real-model Blender renders, add sprite metadata and render hashes."""
from pathlib import Path
import hashlib,json,math,re
from PIL import Image
from server_assets import ROOT,guid

def main():
    template=(ROOT/'Assets/Texture2D/GTX1070Ti.png.meta').read_text()
    manifest=json.loads((ROOT/'tools/server_catalog_manifest.json').read_text())
    scenes=json.loads((ROOT/'tools/server_cover_scenes.json').read_text())
    result=dict(method='Blender Cycles, actual prefab meshes/UVs/materials, orthographic studio view; no replacement geometry',size=256,sources=scenes['sources'],covers={})
    for name,entry in manifest['covers'].items():
        p=ROOT/'Assets/Texture2D/Servers'/(name+'.png');im=Image.open(p).convert('RGBA')
        if im.size!=(256,256):
            box=im.getchannel('A').getbbox();assert box,name
            side=math.ceil(max(box[2]-box[0],box[3]-box[1])/.90);left=round((box[0]+box[2]-side)/2);top=round((box[1]+box[3]-side)/2)
            im=im.crop((left,top,left+side,top+side))
            bg=Image.new('RGBA',im.size,'white');bg.alpha_composite(im);im=bg.resize((256,256),Image.Resampling.LANCZOS);im.save(p,optimize=True)
        text=re.sub(r'^guid: \w+','guid: '+entry['guid'],template,count=1,flags=re.M)
        text=re.sub(r'(spriteID:) \w+',r'\1 '+guid('sprite:'+name),text)
        text=text.replace('maxTextureSize: 2048','maxTextureSize: 256').replace('spriteGenerateFallbackPhysicsShape: 1','spriteGenerateFallbackPhysicsShape: 0').replace('textureCompression: 2','textureCompression: 0')
        Path(str(p)+'.meta').write_text(text)
        result['covers'][name]=dict(file=str(p.relative_to(ROOT)),source=entry['source'],sha256=hashlib.sha256(p.read_bytes()).hexdigest())
    (ROOT/'tools/server_cover_render_manifest.json').write_text(json.dumps(result,indent=2)+'\n')
    print('Finalized',len(result['covers']),'256px opaque-white shop sprites')
if __name__=='__main__':main()
