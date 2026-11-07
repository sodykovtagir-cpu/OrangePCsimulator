#!/usr/bin/env python3
"""Dakimakura impact FX: sound needs an AudioSource; feathers only on impact.

User wired Impact + feather ParticleSystem + drop AudioClip, but the prefab had
no AudioSource (Impact.PlayOneShot had nothing to play through) and the
ParticleSystem had playOnAwake=1, so feathers burst on load.
"""
from pathlib import Path
import re

root = Path(__file__).resolve().parents[1]
prefab = (root / 'Assets/Resources/components/Dakimakura.prefab').read_text()
impact = (root / 'Assets/Scripts/Assembly-CSharp/Impact.cs').read_text()
clip_meta = root / 'Assets/AudioClip/bambo_drop_carpet_001_33211.mp3.meta'

ROOT_GO = '1172905954065532'
IMPACT_ID = '2794564472544760219'
PS_ID = '513312876930122676'
AUDIO_ID = '855660000000000005'
CLIP_GUID = re.search(r'^guid: ([0-9a-f]{32})$', clip_meta.read_text(), re.M).group(1)
# Короткий impact-клип должен быть готов к первому же удару.
assert 'preloadAudioData: 1' in clip_meta.read_text()


def block(fid):
    m = re.search(r'^--- !u!\d+ &' + fid + r'\n(.*?)(?=^--- |\Z)', prefab, re.M | re.S)
    assert m, f'missing prefab block {fid}'
    return m.group(1)


# Root must carry the AudioSource component, and it must not auto-play.
assert f'component: {{fileID: {AUDIO_ID}}}' in block(ROOT_GO)
audio = block(AUDIO_ID)
assert re.search(r'^AudioSource:', audio, re.M)
assert f'm_GameObject: {{fileID: {ROOT_GO}}}' in audio
assert 'm_PlayOnAwake: 0' in audio
assert f'm_audioClip: {{fileID: 8300000, guid: {CLIP_GUID}, type: 3}}' in audio
# 3D positional audio (panLevel curve = 1), not 2D.
pan = re.search(r'panLevelCustomCurve:\n(.*?)(?=^  \w|\Z)', audio, re.M | re.S).group(1)
assert re.search(r'value: 1', pan)

# Impact must reference the clip and the feather system.
imp = block(IMPACT_ID)
assert f'impactSound: {{fileID: 8300000, guid: {CLIP_GUID}, type: 3}}' in imp
assert f'particle: {{fileID: {PS_ID}}}' in imp

# Feathers must not emit on load; a burst at time 0 fires when Impact.Play() runs.
ps = block(PS_ID)
assert re.search(r'^ParticleSystem:', ps, re.M)
assert 'playOnAwake: 0' in ps
assert 'looping: 0' in ps
assert 'EmissionModule:' in ps and 'm_Bursts:' in ps

# Runtime guard: even if an instance overrides Play On Awake, Impact clears it.
assert 'private void Awake()' in impact
assert 'main.playOnAwake = false;' in impact
assert 'ParticleSystemStopBehavior.StopEmittingAndClear' in impact
assert 'source = GetComponent<AudioSource>();' in impact

# Do not regress the accepted prefab/model scale while touching FX.
root_tf = re.search(r'^--- !u!4 &4996549057227249\n(.*?)(?=^--- |\Z)', prefab, re.M | re.S).group(1)
assert 'm_LocalScale: {x: 1, y: 1, z: 1}' in root_tf
meta = (root / 'Assets/Models/dakimakura.fbx.meta').read_text()
assert 'globalScale: 6' in meta

print('PASS: dakimakura has impact AudioSource, feathers only on collision, scale preserved')
