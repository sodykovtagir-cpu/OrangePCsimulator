#!/usr/bin/env python3
"""Guards for two save branches and the original no-wire monitor route.

These checks do not replace testing menu interactions and physics in Unity.
"""
from pathlib import Path
import re
from check_prefab_yaml import main as check_prefab

root=Path(__file__).resolve().parents[1]
cs=root/'Assets/Scripts/Assembly-CSharp'
read=lambda path:(cs/path).read_text()
mode=read('PC/Component/Cabling/CableMode.cs')
game=read('GameData.cs')
menu=read('CableModeMenu.cs')
main=read('MainMenu.cs')
saver=read('SaveManager.cs')
board=read('PC/Component/Motherboard.cs')
power=read('PC/Component/Cabling/CablePower.cs')
scene=(root/'Assets/Scenes/Menu.unity').read_text()
assert 'public int cableModeVersion;' in game and 'public bool cablesEnabled = true;' in game
assert 'public CableModeSnapshot otherCableMode;' in game
assert all(f'public {typ} {key};' in game for typ,key in (
    ('string','content'),('int','coin'),('float','playtime'),('float','temperature'),
    ('bool','ac'),('bool','light'),('string','icon')))
assert 'game.cableModeVersion == 0' in mode and 'game.cablesEnabled = true;' in mode
assert 'if (game.cablesEnabled == target)' in mode
assert 'Uninitialized(game, loader.Content)' in mode
assert 'var repaired = WithoutCables(game.otherCableMode);' in mode
assert 'var outgoing = Snapshot(game, loader.Content);' in mode
assert 'var incoming = game.otherCableMode;' in mode
assert 'incoming = target ? outgoing : WithoutCables(outgoing);' in mode
assert '(!target && Uninitialized(incoming) &&' in mode
assert 'game.otherCableMode = outgoing;' in mode
assert 'Restore(game, incoming);' in mode
assert 'loader.Content = incoming.content ?? string.Empty;' in mode
assert 'if (!string.IsNullOrEmpty(loader.Path)) loader.WriteToFile();' in mode
assert 'target ? loader.Content : WithoutCables(loader.Content)' not in mode
assert 'var incoming = !target ? Snapshot' not in mode
assert 'content = WithoutCables(wired.content)' in mode
assert 'coin = wired.coin, playtime = wired.playtime' in mode
assert 'light = wired.light, icon = wired.icon' in mode
assert all(f'id.StartsWith("{kind} Cable ", StringComparison.Ordinal)' in mode for kind in ('Power','VGA','HDMI'))
assert 'data.Value<int?>("cableOwner" + input)' in mode
assert 'data.Value<string>("cablePort" + input)' in mode
assert 'source.Value<int?>("hostId")' in mode
assert 'board["monitorId"] = link.Value.Contains(previous) ? previous : link.Value[0];' in mode
assert 'BoardData(board) == null' in mode
assert 'PC.Component.Cabling.CableMode.SelectForLoad(loader);' in main
assert main.index('CableMode.SelectForLoad(loader);') < main.index('SaveManager.Loader = loader;',main.index('public void LoadFile('))
assert 'game.coin = main.Money;' in saver and 'loader.Content = JsonConvert.SerializeObject' in saver
assert 'loader.WriteToFile();' in saver
assert 'if (!CableMode.Enabled) return true;' in power
assert 'if (!CableMode.Enabled) return;' in power
assert 'if (!PC.Component.Cabling.CableMode.Enabled && id != 0)' in board
assert 'ConnectMonitor(disp);' in board and 'monitor.Switch(true, false);' in board
raycast=read('Raycast.cs')
assert 'if (PC.Component.Cabling.CableMode.Enabled)' in raycast
assert 'else\n            mb.ConnectMonitor(selectedMonitor);' in raycast
# The main-menu checkbox must be an editable prefab instance in Settings,
# not a clone of ShowFps created when the menu starts.
prefab_path=root/'Assets/GameObject/CableModeToggle.prefab'
prefab=prefab_path.read_text()
prefab_guid=re.search(r'^guid: ([0-9a-f]{32})$',
    (root/'Assets/GameObject/CableModeToggle.prefab.meta').read_text(), re.M).group(1)
menu_script_guid=re.search(r'^guid: ([0-9a-f]{32})$',
    (cs/'CableModeMenu.cs.meta').read_text(), re.M).group(1)
assert check_prefab(str(prefab_path)) == 0
assert 'CableModeMenu.Install' not in main and 'Instantiate(' not in menu
assert 'toggle.SetIsOnWithoutNotify(CableMode.Selected);' in menu
assert 'toggle.onValueChanged.AddListener(CableMode.SetSelected);' in menu
assert 'Localization.LanguageChanged += UpdateLabel;' in menu
assert 'Физические провода' in menu and 'Physical cables' in menu
assert '  m_Name: Settings\n' in scene and '  m_Name: ShowFps\n' in scene
assert '  m_Name: Content\n' in scene
assert '  m_Name: CableModeToggle\n' in prefab
assert '  m_Name: Label\n' in prefab and '  m_Name: Background\n' in prefab
assert '  m_Text: Physical cables\n' in prefab
assert '  m_AnchoredPosition: ' in prefab
assert f'm_Script: {{fileID: 11500000, guid: {menu_script_guid}, type: 3}}' in prefab
assert '  toggle: {fileID: 870000000000000011}\n' in prefab
assert '  label: {fileID: 870000000000000006}\n' in prefab
assert 'm_TargetAssemblyTypeName: ShowFps' not in prefab
assert 'm_MethodName: ShowFpsDisplay' not in prefab
assert '  onValueChanged:\n    m_PersistentCalls:\n      m_Calls: []\n' in prefab
assert f'm_SourcePrefab: {{fileID: 100100000, guid: {prefab_guid}, type: 3}}' in scene
assert f'm_CorrespondingSourceObject: {{fileID: 870000000000000002, guid: {prefab_guid}, type: 3}}' in scene
assert '    m_TransformParent: {fileID: 1323815698}\n' in scene
assert '  - {fileID: 790000000000000002}\n' in scene
assert re.search(r'--- !u!224 &1323815698\nRectTransform:.*?m_Children:\n'
    r'(?:  - \{fileID: \d+\}\n)*  - \{fileID: 790000000000000002\}',scene,re.S)
print('Cable mode prefab, menu, save branches and legacy route: OK')
