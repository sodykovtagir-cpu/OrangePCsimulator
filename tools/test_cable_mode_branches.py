#!/usr/bin/env python3
"""Guards for two save branches and the original no-wire monitor route.

These checks do not replace testing menu interactions and physics in Unity.
"""
from pathlib import Path
import re

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
assert 'if (game.cablesEnabled == target) return;' in mode
assert 'var outgoing = Snapshot(game, loader.Content);' in mode
assert 'game.otherCableMode = outgoing;' in mode
assert 'Restore(game, incoming);' in mode
assert 'loader.Content = incoming.content ?? string.Empty;' in mode
assert 'if (!string.IsNullOrEmpty(loader.Path)) loader.WriteToFile();' in mode
assert 'target ? loader.Content : WithoutCables(loader.Content)' in mode
assert all(f'id.StartsWith("{kind} Cable ", StringComparison.Ordinal)' in mode for kind in ('Power','VGA','HDMI'))
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
assert 'CableModeMenu.Install();' in main
assert 'Scroll View/Viewport/Content' in menu and 'Physical Cables' in menu
assert 'toggle.onValueChanged = new Toggle.ToggleEvent();' in menu
assert 'toggle.SetIsOnWithoutNotify(CableMode.Selected);' in menu
assert 'toggle.onValueChanged.AddListener(CableMode.SetSelected);' in menu
assert '  m_Name: Settings\n' in scene and '  m_Name: ShowFps\n' in scene
assert '  m_Name: Content\n' in scene
print('Cable mode menu, save branches and legacy route: OK')
