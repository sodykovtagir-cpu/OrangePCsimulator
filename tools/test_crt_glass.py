#!/usr/bin/env python3
"""CRT glass asset/HLSL checks, not a Unity Play Mode or ShaderLab import test.

Requires Python pyyaml, plus glslangValidator and spirv-val (glslang-tools).
Run: python3 tools/test_crt_glass.py

Compiles the actual shader programs, replacing only UnityObjectToClipPos with
an identity transform for the standalone HLSL compiler. Also checks prefab
isolation, existing screen UVs, physical bounds and numerical filter behavior.
"""
from pathlib import Path
import math
import re
import shutil
import struct
import subprocess
import tempfile

import yaml

ROOT = Path(__file__).resolve().parents[1]
ASSETS = ROOT / "Assets"
SHADER = ASSETS / "Shader/CRT_Glass.shader"
MATERIAL = ASSETS / "Material/CRT_Glass.mat"
PREFAB = ASSETS / "Resources/components/CRT_Monitor.prefab"


def documents(path):
    result = {}
    for m in re.finditer(r"^--- !u!(\d+) &(-?\d+)\n(.*?)(?=^--- !u!|\Z)", path.read_text(), re.M | re.S):
        fid = int(m[2])
        assert fid not in result, (path, fid)
        result[fid] = (int(m[1]), next(iter(yaml.safe_load(m[3]).values())))
    assert result, path
    return result


def references(value):
    if isinstance(value, dict):
        if "fileID" in value:
            yield value
        for child in value.values():
            yield from references(child)
    elif isinstance(value, list):
        for child in value:
            yield from references(child)


def guid(path):
    return re.search(r"^guid: (\w+)$", Path(str(path) + ".meta").read_text(), re.M)[1]


def check_assets():
    docs = documents(PREFAB)
    shader_guid, material_guid = guid(SHADER), guid(MATERIAL)
    mat = documents(MATERIAL)[2100000][1]
    assert mat["m_Shader"] == {"fileID": 4800000, "guid": shader_guid, "type": 3}
    assert mat["m_SavedProperties"]["m_TexEnvs"] == [], "glass must not require a desktop/camera texture"
    assert mat["m_CustomRenderQueue"] == -1
    for expected_guid, expected_path in [(shader_guid, SHADER), (material_guid, MATERIAL)]:
        owners = [p for p in ASSETS.rglob("*.meta") if "guid: " + expected_guid in p.read_text(errors="replace")]
        assert owners == [Path(str(expected_path) + ".meta")], owners
    used_by = [p for p in ASSETS.rglob("*.prefab") if material_guid in p.read_text(errors="replace")]
    assert used_by == [PREFAB], used_by
    shader_users = [p for p in ASSETS.rglob("*.mat") if shader_guid in p.read_text(errors="replace")]
    assert shader_users == [MATERIAL], shader_users
    for fid, (kind, body) in docs.items():
        for ref in references(body):
            if ref["fileID"] and "guid" not in ref:
                assert ref["fileID"] in docs, (fid, ref)
        if kind == 1:
            assert docs[body["m_Component"][0]["component"]["fileID"]][0] in (4, 224)
            for component in body["m_Component"]:
                assert docs[component["component"]["fileID"]][1]["m_GameObject"]["fileID"] == fid
        if kind in (4, 224):
            for child in body["m_Children"]:
                assert docs[child["fileID"]][1]["m_Father"]["fileID"] == fid

    effect_go, effect_transform, effect_mesh, effect_renderer = range(860480000000000001, 860480000000000005)
    assert docs[effect_go][1]["m_Layer"] == 0
    assert docs[effect_go][1]["m_IsActive"] == 1
    assert [docs[x["component"]["fileID"]][0] for x in docs[effect_go][1]["m_Component"]] == [4, 33, 23]
    transform = docs[effect_transform][1]
    ancestors = []
    parent = transform["m_Father"]["fileID"]
    while parent:
        ancestors.append(parent)
        parent = docs[parent][1]["m_Father"]["fileID"]
    assert ancestors == [4564348549592250, 4085359237183865], "effect must stay with CRT hardware, not the detachable Canvas"
    assert docs[effect_mesh][1]["m_Mesh"] == docs[33258178049864700][1]["m_Mesh"]
    renderer = docs[effect_renderer][1]
    assert renderer["m_Materials"] == [{"fileID": 2100000, "guid": material_guid, "type": 2}]
    assert renderer["m_SortingOrder"] == 32767 and renderer["m_SortingLayerID"] == 0
    assert renderer["m_CastShadows"] == renderer["m_ReceiveShadows"] == 0
    # Original display component, fullscreen trigger, opaque glass and housing remain separate.
    monitor = docs[114011925485804472][1]
    assert monitor["m_Script"]["guid"] == "0a132b41b49b18a3078aa22fd0638b3e"
    assert monitor["screen"]["fileID"] == 23431920351864277
    assert monitor["screenCanvas"]["fileID"] == 224859455269634680
    assert monitor["point"]["fileID"] == 4576782421828187
    assert monitor["spawnId"] == "CRT_Monitor"
    assert docs[23431920351864277][1]["m_Materials"][0]["guid"] == "7422ee53cea897a41b16b457b2f4800d"
    assert docs[23822949864948101][1]["m_Materials"][0]["guid"] == "8e6e9217856da30489e03a9af0c7bb2e"
    assert docs[223108326910044773][1]["m_RenderMode"] == 2
    assert docs[224859455269634680][1]["m_SizeDelta"] == {"x": 840, "y": 500}
    assert sum(kind == 223 for kind, body in docs.values()) == 1, "do not add another desktop Canvas"
    print("PASS: CRT-only material reference, GUIDs, hierarchy, sorting, local refs, no collider/UI/camera additions")

    mesh = next(iter(documents(ASSETS / "Mesh/Screen_2.asset").values()))[1]
    vd = mesh["m_VertexData"]
    assert vd["m_VertexCount"] == 4
    data = bytes.fromhex(vd["_typelessdata"])
    stride = len(data) // vd["m_VertexCount"]
    uv_channel = vd["m_Channels"][4]
    assert (uv_channel["format"], uv_channel["dimension"], uv_channel["stream"]) == (0, 2, 0)
    colors = {k: v for item in mat["m_SavedProperties"]["m_Colors"] for k, v in item.items()}
    rect = colors["_ScreenUVRect"]
    corners = set()
    for i in range(vd["m_VertexCount"]):
        uv = struct.unpack_from("<2f", data, i * stride + uv_channel["offset"])
        normalized = ((uv[0] - rect["r"]) / rect["b"], (uv[1] - rect["g"]) / rect["a"])
        assert all(-1e-6 <= value <= 1 + 1e-6 for value in normalized), normalized
        assert all(abs(value - round(value)) < 1e-6 for value in normalized), normalized
        corners.add(tuple(round(value) for value in normalized))
    assert corners == {(0, 0), (1, 0), (1, 1), (0, 1)}
    assert transform["m_LocalScale"] == {"x": 1, "y": 1, "z": 1}
    assert transform["m_LocalPosition"]["x"] == transform["m_LocalPosition"]["y"] == 0
    screen_z = mesh["m_LocalAABB"]["m_Center"]["z"] + transform["m_LocalPosition"]["z"]
    canvas_z = docs[4576782421828187][1]["m_LocalPosition"]["z"]
    assert abs(screen_z - canvas_z - 0.001) < 1e-6, "glass must sit just ahead of world-space desktop, inside original screen bounds"
    print("PASS: original physical screen bounds, front depth and normalized atlas UVs")
    return {k: v for item in mat["m_SavedProperties"]["m_Floats"] for k, v in item.items()}


def check_shader():
    source = SHADER.read_text()
    clean = re.sub(r"//[^\n]*", "", source)
    for state in ['"Queue"="Overlay"', "Blend DstColor Zero", "ColorMask RGB", "ZWrite Off", "ZTest LEqual", "Cull Back"]:
        assert state in clean, state
    for forbidden in ["GrabPass", "_MainTex", "_CameraDepthTexture", "_Time", "OnRenderImage"]:
        assert forbidden not in clean, forbidden
    assert "fwidth(phase)" in clean
    display = (ASSETS / "Scripts/Assembly-CSharp/PC/Component/Display.cs").read_text()
    assert "tr.SetParent(null)" in display and "RenderMode.ScreenSpaceOverlay" in display
    assert "c.cullingMask = 0" in display
    print("PASS: hardware-only RGB multiplication, depth occlusion, no global capture, no animated flicker")
    program = re.search(r"CGPROGRAM(.*?)ENDCG", source, re.S)[1]
    program = re.sub(r"^\s*#pragma[^\n]*", "", program, flags=re.M)
    program = program.replace('#include "UnityCG.cginc"', "float4 UnityObjectToClipPos(float4 p) { return p; }")
    assert shutil.which("glslangValidator") and shutil.which("spirv-val"), "Install glslang-tools to validate shader programs"
    with tempfile.TemporaryDirectory(prefix="crt-glass-shader-") as folder:
        folder = Path(folder)
        hlsl = folder / "crt.hlsl"
        hlsl.write_text(program)
        for stage in ["vert", "frag"]:
            binary = folder / (stage + ".spv")
            subprocess.run(["glslangValidator", "-D", "-V", "--auto-map-bindings", "--auto-map-locations",
                            "-S", stage, "-e", stage, "-o", str(binary), str(hlsl)], check=True, capture_output=True, text=True)
            subprocess.run(["spirv-val", str(binary)], check=True)
    print("PASS: actual vertex and fragment HLSL compiled; SPIR-V validated (Unity transform shim)")


def smoothstep(a, b, value):
    t = max(0, min(1, (value - a) / (b - a)))
    return t * t * (3 - 2 * t)


def visibility(footprint):
    return 1 - smoothstep(0.25, 0.75, footprint)


def evaluate(uv, settings, line_footprint=0, phosphor_footprint=0):
    x, y = uv
    scanline = 1 - settings["_ScanlineStrength"] * (0.5 + 0.5 * math.cos(y * settings["_ScanlineCount"] * math.tau) * visibility(line_footprint))
    radial = ((x * 2 - 1) ** 2 + (y * 2 - 1) ** 2) * 0.5
    vignette = 1 - settings["_VignetteStrength"] * smoothstep(0.2, 1, radial)
    result = []
    for phase in [0, -math.tau / 3, math.tau / 3]:
        stripe = 0.5 - 0.5 * math.cos(x * settings["_PhosphorCount"] * math.tau + phase) * visibility(phosphor_footprint)
        result.append(scanline * (1 - settings["_PhosphorStrength"] * stripe) * vignette * settings["_Brightness"])
    return result


def check_math(settings):
    assert 0 < settings["_ScanlineStrength"] < 1 and 0 < settings["_PhosphorStrength"] < 1
    assert 0 < settings["_VignetteStrength"] < 1 and settings["_Brightness"] > 0
    minimum = (1 - settings["_ScanlineStrength"]) * (1 - settings["_PhosphorStrength"]) * (1 - settings["_VignetteStrength"]) * settings["_Brightness"]
    samples = 0
    for y in range(65):
        for x in range(65):
            for footprint in [0, 0.5, 1.5]:
                color = evaluate((x / 64, y / 64), settings, footprint, footprint)
                assert all(math.isfinite(c) and minimum - 1e-6 <= c <= settings["_Brightness"] + 1e-6 for c in color)
                assert all(c * 0 == 0 for c in color), "unpowered black screen must stay black"
                samples += 1
    neutral = dict(settings, _ScanlineStrength=0, _PhosphorStrength=0, _VignetteStrength=0, _Brightness=1)
    assert evaluate((0.13, 0.89), neutral) == [1, 1, 1]
    assert visibility(0) == 1 and visibility(0.5) == 0.5 and visibility(1) == 0
    center, edge, corner = [evaluate(uv, settings) for uv in [(0.5, 0.5), (0, 0.5), (0, 0)]]
    assert all(corner[i] < edge[i] < center[i] for i in range(3))
    flat = dict(settings, _VignetteStrength=0)
    far_a, far_b = evaluate((0.12, 0.37), flat, 2, 2), evaluate((0.88, 0.94), flat, 2, 2)
    assert far_a == far_b and far_a[0] == far_a[1] == far_a[2], "unresolved patterns should average to a neutral stable level"
    # The cosine's mean stays unchanged as detail fades, avoiding distance-dependent brightness pumping.
    for fade in [0, 0.5, 1]:
        mean = sum(0.5 + 0.5 * math.cos(i / 1024 * math.tau) * fade for i in range(1024)) / 1024
        assert abs(mean - 0.5) < 1e-10
    print("PASS:", samples, "numerical samples: finite modulation, black preservation, neutral bypass, edge falloff and stable detail fading")


def main():
    settings = check_assets()
    check_shader()
    check_math(settings)
    print("CRT glass checks passed. Unity ShaderLab import, actual scene rendering and Play Mode were not run.")


if __name__ == "__main__":
    main()
