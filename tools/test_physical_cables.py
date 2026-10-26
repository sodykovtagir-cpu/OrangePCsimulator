#!/usr/bin/env python3
"""Static wiring/asset integrity guards; Unity Play Mode is still required."""
from pathlib import Path
import math
import re
from check_prefab_yaml import main as check_prefab

root=Path(__file__).resolve().parents[1]
code=root/'Assets/Scripts/Assembly-CSharp'
read=lambda path:(code/path).read_text()
ports=read('PC/Component/Cabling/CablePort.cs')
rope=read('PC/Component/Cabling/CableItem.cs')
plug=read('PC/Component/Cabling/CablePlug.cs')
power=read('PC/Component/Cabling/CablePower.cs')
outlet=read('PC/Component/Cabling/OutletStrip.cs')
assert 'PortKind.Outlet' in ports and 'PortKind.PcPower' in ports and 'PortKind.MonitorPower' in ports
assert 'PortKind.VgaInput' in ports and 'PortKind.HdmiInput' in ports
assert 'cable.Kind == CableKind.VGA' in ports and 'cable.Kind == CableKind.HDMI' in ports
assert 'plug != null' in ports and 'plug = this' in plug
assert 'cable.End(1 - candidate.End)' in ports and 'input.Cable.End(1 - input.End)' in power
assert 'cable.End(1 - input.plug.End)' in power
# The wall plug (A = end 0) and IEC/device plug (B = end 1) have different
# meshes. Reject opposite sockets even when the other cable end is still free;
# Accepts is also called by TryInsert during save restoration.
assert 'candidate.End == 0 && Powered' in ports
assert 'candidate.End == 1 && (opposite == null || opposite.kind == PortKind.Outlet)' in ports
assert 'if (Time.time < cooldown || port == null || !port.Accepts(this)) return false;' in plug
assert 'kind == CableKind.Power && end == 0 ? OutletPlugMaterial()' in rope
assert 'Physics' not in rope or 'CapsuleCollider' in rope
assert all(x in rope for x in ('CapsuleCollider','CharacterJoint','Rigidbody','ContinuousSpeculative','Physics.IgnoreCollision', 'LineRenderer', 'Length', 'JObject', 'RestorePorts()'))
assert 'Vector3.Distance(other.Connected.Snap.position, port.Snap.position) > Cable.Length * .95f' in plug
assert 'FixedJoint' in plug and 'connectedBody' in plug and 'cooldown' in plug
assert 'AddPairs(List<Pair> list, UnityEngine.Component device, Item owner)' in plug
assert 'Physics.IgnoreCollision(ours, target, true)' in plug
assert 'Physics.IgnoreCollision(pair.plug, pair.target, false)' in plug
assert 'pair.plug.bounds.Intersects(pair.target.bounds)' in plug
# Every CablePort owner (GPU, PSU, motherboard, display and wall outlet)
# ignores ONLY this plug and its adjacent capsule. The installed GPU/PSU/board
# additionally exempts their case, never unrelated devices or full cable.
assert 'WatchPort(port);' in plug and 'watchedOwner = port.owner;' in plug
assert 'AddPairs(devicePairs, watchedOwner.transform, watchedOwner);' in plug
assert 'if (!IsHeld || Connected != null || port == null || !port.Accepts(this)) return;' in plug
assert 'approachPort = port;\n            WatchPort(port);' in plug
assert 'Reinstate(devicePairs);' in plug and plug.count('DeferRestore(devicePairs);') >= 3
assert 'private static Case FindCase(GPU gpu)' in plug and 'slot.Hardware != gpu' in plug
assert 'private static Case FindCase(Supply supply)' in plug and 'slot.Hardware != supply' in plug
assert 'private static Case FindCase(Motherboard board)' in plug and 'slot.Hardware != board' in plug
assert 'if (watchedOwner is GPU gpu) next = FindCase(gpu);' in plug
assert 'else if (watchedOwner is Supply supply) next = FindCase(supply);' in plug
assert 'else if (watchedOwner is Motherboard board) next = FindCase(board);' in plug
assert 'AddPairs(casePairs, currentCase.transform, caseOwner);' in plug
assert 'gpuPairs' not in plug and 'psuPairs' not in plug
assert 'if (caseOwner != watchedOwner)' in plug
assert 'Cable.NearEndCollider(End)' in plug
assert 'target.GetComponentInParent<Item>() != owner' in plug
assert 'target == null || target.isTrigger || !target.enabled' in plug
assert 'AddPair(list, adjacent, target);' in plug
assert 'candidate.PrepareApproach(this)' in ports and 'candidate.LeaveApproach(this)' in ports
assert 'approachPort.CaptureRadius + .25f' in plug
assert 'Physics.IgnoreLayerCollision' not in plug and 'Physics.IgnoreLayerCollision' not in rope
assert 'Hooked' in outlet and 'sockets' in outlet and 'CablePort.Add' in outlet
assert all(x in power for x in ('Energized', 'BoardHasPower', 'VideoPort', 'cpu.HasIntegratedGraphics', 'display.VideoGpu', 'board.PowerOff(false)', 'ShowNoSignal'))
assert 'CablePower.BoardHasPower(this)' in read('PC/Component/Motherboard.cs')
assert 'CablePower.RefreshAll()' in read('PC/Component/Motherboard.cs')
assert 'ConnectMonitor(selectedMonitor);' not in read('Raycast.cs')
raycast=read('Raycast.cs')
# Disassembly is an explicit click action BEFORE ordinary dragging; normal
# clicks only grab, with no automatic Unplug on any cable type.
assert raycast.index('if (RemoveMode && hit.transform != null)') < raycast.index('if (dragRigidbody)', raycast.index('public void ShootRaycast'))
assert 'if (plug != null && plug.Connected != null) plug.Unplug();' in raycast
assert raycast.count('plug.Unplug();') == 1
assert 'var cable = hit.transform.GetComponentInParent<PC.Component.Cabling.CableItem>();' in raycast
assert 'Vector3.SqrMagnitude(hit.point - a.transform.position)' in raycast
assert 'plug.SetHeld(true, ReleaseHeldPlug)' in raycast and 'releasedPlug.SetHeld(false)' in raycast
assert 'if (!AutoRotation || grabbedCable != null)' in raycast
assert 'if (rb.GetComponentInParent<PC.Component.Cabling.CableItem>() != null) return;' in raycast
assert 'releaseDrag?.Invoke(this)' in plug and 'private void ReleaseHeldPlug(' in raycast
assert 'pin.breakForce = PullOutForce' in plug and 'OnJointBreak(float force)' in plug
assert 'body.layer = 0' in rope and 'HoldSection(Rigidbody section)' in rope
assert 'currentDrag.grabbedCable.HoldSection(null)' in raycast
assert 'maxPullMass = 5f' in rope and 'CanPull(Rigidbody rb, int depth)' in rope
assert 'yankedPort = old' in plug
# Heavy passive loads (e.g. a PSU hanging from an outlet) need the same
# protection as a hand-held cable. Project overstretched joints, damp light
# item pulls, and release one end at the hard limit even when nothing is held.
assert 'joint.enableProjection = true;' in rope
assert 'joint.projectionDistance = .02f;' in rope
assert 'rb.interpolation = RigidbodyInterpolation.Interpolate;' in rope
assert 'jacket.SetPosition(i + 1, bodies[i].transform.position);' in rope
assert 'path += Vector3.Distance(previous, body.position);' in rope
assert 'if (ReleaseOverstretchedEnd()) return;' in rope
assert 'other.Connected == null' in rope
assert 'a.Connected == null || b.Connected == null ||' in rope
assert 'a.IsHeld || b.IsHeld || heldSegment != null' in rope
assert 'if (sectionHeld && distance > restLength + .08f' not in rope
assert 'overstretchSteps = 0;\n                return false;' in rope
assert 'float spread = Mathf.Min(.5f, length * .15f);' in rope
assert 'count >= 24 && length > 12f ? 3' in rope
assert 'path > length + Mathf.Max(.06f, length * .02f)' in rope
assert 'span > length * .97f' in rope
assert 'Mathf.Max(0f, tension - Mathf.Max(0f, closingSpeed)' in rope
assert 'if (a.Connected.kind == PortKind.Outlet) return b;' in rope
assert 'if (b.Connected.kind == PortKind.VgaInput || b.Connected.kind == PortKind.HdmiInput) return b;' in rope
assert 'if (!hardLimit &&' in rope and 'CanPull(plugs[1].Connected.GetComponentInParent<Rigidbody>(), 0)' in rope
assert 'release.Unplug();' in rope and 'strainCooldownUntil = Time.time + .35f;' in rope
assert 'ConnectedAt = Time.time;' in plug
assert 'Time.time - plugs[0].ConnectedAt < .25f' in rope
assert 'restoringPhysics = false;\n            overstretchSteps = 0;' in rope
# The yanked-port lock must be cleared while the plug is AWAY, not only during
# TryInsert (which is never called away from a port). Otherwise reinsertion is
# blocked until the save is reloaded.
assert 'private void RefreshYankedPort()' in plug
assert 'if (yankedPort != null && !NearPort(yankedPort, -.10f)) yankedPort = null;' in plug
assert 'private void FixedUpdate()\n        {\n            RefreshYankedPort();' in plug
assert 'if (port == yankedPort) return false;' in plug
# The HDMI BoxCollider is offset from its Rigidbody origin. A port touching
# its body must accept it even when the origin lies outside the trigger.
assert 'solid.ClosestPoint(port.TriggerCenter)' in plug
assert 'if (!restoring && !NearPort(port)) return false;' in plug
assert 'Vector3.Distance(transform.position, port.TriggerCenter) > port.CaptureRadius) return false' not in plug
assert 'if (IsHeld && Connected == null) TryNearbyPort();' in plug
assert 'Physics.OverlapSphereNonAlloc' in plug and 'QueryTriggerInteraction.Collide' in plug
assert ports.count('other.GetComponentInParent<CablePlug>()') == 3
assert 'GetComponentInParent<PC.Component.Cabling.CableItem>()' in raycast
assert 'CablePorts.Install(this)' not in read('PC/Component/Case.cs')
assert 'Case AC input' not in read('PC/Component/Cabling/CablePorts.cs')
assert 'Case board video' not in read('PC/Component/Cabling/CablePorts.cs')
assert 'Case GPU video' not in read('PC/Component/Cabling/CablePorts.cs')
save = read('SaveManager.cs')
assert 'RestoreCableConnections' in save
# The cable root stays at the delivery portal. The item save position and
# below-world filter must use the actual physical cable, not that anchor.
assert 'item is PC.Component.Cabling.CableItem cable' in save
assert 'cable.SavePosition : item.transform.position' in save
assert 'if (savePos.y < -20f) continue;' in save
assert 'pos = new { x = savePos.x, y = savePos.y, z = savePos.z }' in save
assert save.index('cable.RestorePorts();') < save.index('cable.FinishRestore();')
assert 'data["cableRot" + i] = new JArray(' in rope
assert 'data["cableBodies"] = poses;' in rope
assert 'restoringPhysics = true;' in rope and 'restoringPhysics = false;' in rope
assert 'Physics.SyncTransforms();' in rope and 'rb.isKinematic = true;' in rope
assert 'rb.isKinematic = false;' in rope and 'public void FinishRestore()' in rope
assert 'if (!restoring && Cable != null && Cable.RestoringPhysics) return false;' in plug
assert 'VideoFromGpu' in read('PC/Component/Software/OS/OperatingSystem.cs')
assert 'display.VideoGpu' in read('GpuArtifacts.cs')
# CS0136 in Board() prevented Unity from compiling CableItem/OutletStrip,
# which made every new prefab appear to have a missing script.
assert 'var pcCase = connector.slot.GetComponentInParent<Case>();' not in read('PC/Component/Cabling/CablePorts.cs')
assert 'solidCase.enabled = false' in read('PC/Component/Cabling/CableCaseOpening.cs')
assert all(side in read('PC/Component/Cabling/CableCaseOpening.cs')
           for side in ('"Left"','"Right"','"Below"','"Above"'))
editor_sockets=(root/'Assets/Editor/PrepareCableSockets.cs').read_text()
assert 'Prepare sockets in selected prefab' in editor_sockets
assert 'GetComponentsInChildren<PC.Component.Display>' in editor_sockets  # UnityEngine.Display is ambiguous
assert 'Case AC input' not in editor_sockets and 'Case GPU video' not in editor_sockets

def script_guid(relative):
    meta=(code/(relative+'.meta')).read_text()
    return re.search(r'^guid: ([a-f0-9]{32})$',meta,re.M).group(1)

cable_script=script_guid('PC/Component/Cabling/CableItem.cs')
outlet_script=script_guid('PC/Component/Cabling/OutletStrip.cs')
plug_script=script_guid('PC/Component/Cabling/CablePlug.cs')
port_script=script_guid('PC/Component/Cabling/CablePort.cs')
opening_script=script_guid('PC/Component/Cabling/CableCaseOpening.cs')
hook_script=script_guid('Hook.cs')
def prefab_scripts(prefab):
    return re.findall(r'^  m_Script: \{fileID: 11500000, guid: ([a-f0-9]{32}), type: 3\}$',prefab,re.M)

shop=(root/'Assets/MonoBehaviour/Shop.asset').read_text()
assert 'pageName: Cables & Power' in shop
assert 'guid: 78348b2c142bcb88381354c04ae3fce7' in shop  # keep first ready PC
def plug_visuals(yaml, end):
    blocks={fid:body for fid,body in re.findall(
        r'(?m)^--- !u!\d+ &(-?\d+)\n([\s\S]*?)(?=^--- !u!|\Z)', yaml)}
    owner='100010' if end=='A' else '100020'
    go=blocks[owner]
    assert f'  m_Name: Plug {end}\n' in go
    components=[blocks[fid] for fid in re.findall(r'^  - component: \{fileID: (\d+)\}$',go,re.M)]
    mesh=[b for b in components if b.startswith('MeshFilter:\n')]
    renderer=[b for b in components if b.startswith('MeshRenderer:\n')]
    assert len(mesh)==len(renderer)==1
    assert all(f'  m_GameObject: {{fileID: {owner}}}' in b for b in mesh+renderer)
    return (re.search(r'^  m_Mesh: (.+)$',mesh[0],re.M).group(1),
            tuple(re.findall(r'^  - \{fileID: -?\d+, guid: [a-f0-9]{32}, type: 3\}$',
                             renderer[0],re.M)))

ids=[]
shop_ids=[]
for kind in ('Power','VGA','HDMI'):
    reference=(root/'Assets/Resources/components'/
               {'Power':'Power Cable 1.5m.prefab','VGA':'VGA Cable 1.5m.prefab',
                'HDMI':'HDMI Cable 2m.prefab'}[kind]).read_text()
    expected_visuals={end:plug_visuals(reference,end) for end in ('A','B')}
    paths=list((root/'Assets/Resources/components').glob(f'{kind} Cable *.prefab'))
    assert len(paths)==6,kind
    expected={'Power':{1,3,5,6,10,15},'VGA':{1,3,5,7,11,15},
              'HDMI':{2,4,6,8,12,16}}[kind]
    lengths=set()
    for p in paths:
        text=p.read_text()
        assert check_prefab(str(p))==0
        assert f'spawnId: {p.stem}' in text
        assert prefab_scripts(text)==[cable_script,plug_script,plug_script],p
        assert text.count('  m_Name: Plug ') == 2
        assert '  plugA: {fileID: 1140010}' in text and '  plugB: {fileID: 1140020}' in text
        # Both physical ends must render the author's connector, not the
        # runtime fallback cube. This includes the two legacy 6/10 m power
        # cables that predated the authored plug meshes.
        assert text.count('MeshFilter:') == 2 and text.count('MeshRenderer:') == 2, p
        assert all(plug_visuals(text,end)==expected_visuals[end] for end in ('A','B')),p
        assert '  jacket: {fileID: 1200000}' in text and 'LineRenderer:' in text
        # Author-adjusted HDMI jackets are 0.13 m; preserve prefab values.
        diameter = float(re.search(r'^  cableDiameter: ([0-9.]+)$',text,re.M).group(1))
        assert .08 <= diameter <= .25, p
        material=root/f'Assets/Material/Cable {kind}.mat.meta'
        mat_guid=re.search(r'^guid: ([a-f0-9]{32})$',material.read_text(),re.M).group(1)
        assert mat_guid in text,p
        cable_length=float(re.search(r'^  length: ([0-9.]+)$',text,re.M).group(1))
        lengths.add(cable_length)
        if '(' in p.stem:
            assert f'  spawnId: {p.stem}\n' in text
            assert f'  itemName: \'{{{kind} Cable}} {int(cable_length)}m\'' in (
                root/f'Assets/MonoBehaviour/{p.stem}.asset').read_text()
            assert text.count('MeshFilter:') == 2, p  # retain authored plug models
            points_block=text.split('  m_Positions:\n',1)[1].split('  m_Parameters:',1)[0]
            assert len(re.findall(r'^  - \{x: ',points_block,re.M)) == max(7,min(26,
                int(re.search(r'^  segments: (\d+)$',text,re.M).group(1))))+1,p
        # The initial loose coil must stay below the anti-stretch threshold;
        # otherwise a newly connected prefab could falsely unplug at spawn.
        count=max(7,min(26,int(re.search(r'^  segments: (\d+)$',text,re.M).group(1))))
        turns=3 if count>=24 and cable_length>12 else 2 if count>=11 else 1
        radius=(cable_length/count)/(2*math.sin(math.pi*turns/count))
        spread=min(.5,cable_length*.15)
        points=[(radius*math.cos(2*math.pi*turns*i/count)+spread*(i/count-.5),
                 radius*math.sin(2*math.pi*turns*i/count)) for i in range(count+1)]
        coil=sum(math.dist(points[i],points[i+1]) for i in range(count))
        assert coil<cable_length+max(.06,cable_length*.02),p
        prefab_guid=re.search(r'^guid: ([a-f0-9]{32})$',p.with_suffix('.prefab.meta').read_text(),re.M).group(1)
        item=root/f'Assets/MonoBehaviour/{p.stem}.asset'
        assert prefab_guid in item.read_text(),p
        if kind=='Power':
            assert 'Plug A goes to a mounted outlet; Plug B goes to the device.' in item.read_text(),p
        shop_guid=re.search(r'^guid: ([a-f0-9]{32})$',item.with_suffix('.asset.meta').read_text(),re.M).group(1)
        assert shop_guid in shop,p
        ids.append(prefab_guid)
        shop_ids.append(shop_guid)
    assert lengths==expected
for sockets in (1,2,3):
    p=root/f'Assets/Resources/components/Wall Outlet {sockets}.prefab'
    assert check_prefab(str(p))==0
    assert f'  sockets: {sockets}' in p.read_text()
    # Designer-authored Outlet 1/2/3 ports are part of these prefabs now.
    assert prefab_scripts(p.read_text())==[hook_script,outlet_script]+[port_script]*sockets,p
    prefab_guid=re.search(r'^guid: ([a-f0-9]{32})$',p.with_suffix('.prefab.meta').read_text(),re.M).group(1)
    item=root/f'Assets/MonoBehaviour/Wall Outlet {sockets}.asset'
    assert prefab_guid in item.read_text(),p
    shop_guid=re.search(r'^guid: ([a-f0-9]{32})$',item.with_suffix('.asset.meta').read_text(),re.M).group(1)
    assert shop_guid in shop,p
    ids.append(prefab_guid)
    shop_ids.append(shop_guid)
assert len(set(ids))==21
for template,script_name in (("Cable Socket TEMPLATE",port_script),("PSU Opening TEMPLATE",opening_script)):
    p=root/f"Assets/Resources/components/{template}.prefab"
    assert check_prefab(str(p))==0
    assert prefab_scripts(p.read_text())==[script_name],p
rows=(root/'Assets/Resources/Translate.txt').read_text().splitlines()
header=rows[0].split('\t')
for key in ('Cables & Power','Power Cable','VGA Cable','HDMI Cable','Wall Outlet'):
    row=next(r.split('\t') for r in rows if r.startswith(key+'\t'))
    assert row[header.index('RU')] and row[header.index('EN')]
shop_page=shop.split('  - pageName: Cables & Power\n')[1].split('  - pageName:',1)[0]
page_guids=re.findall(r'^    - \{fileID: 11400000, guid: ([a-f0-9]{32}), type: 2\}$',
                      shop_page,re.M)
assert len(page_guids)==21 and len(set(page_guids))==21
assert sorted(page_guids)==sorted(shop_ids)
for kind in ('power','vga','hdmi','outlet'):
    image=root/f'Assets/Texture2D/icon_cable_{kind}.png'
    assert image.exists() and image.with_suffix('.png.meta').exists()
    from PIL import Image
    with Image.open(image) as graphic: graphic.verify()
    sprite_guid=re.search(r'^guid: ([a-f0-9]{32})$',image.with_suffix('.png.meta').read_text(),re.M).group(1)
    assets=(root/'Assets/MonoBehaviour').glob((kind.upper() if kind in ('vga','hdmi') else 'Power' if kind=='power' else 'Wall Outlet')+'*.asset')
    assert any(sprite_guid in asset.read_text() for asset in assets)
print('Physical cables, power/video routes, persistence, 21 shop items: OK')
