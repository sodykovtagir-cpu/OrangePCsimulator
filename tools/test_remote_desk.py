#!/usr/bin/env python3
"""Static RemoteDesk regression checks. Rendering and keyboard need Unity Play Mode."""
from pathlib import Path
import re
import yaml

project = Path(__file__).resolve().parents[1]
src = project / 'Assets/Scripts/Assembly-CSharp'
app = (src / 'PC/Component/Software/RemoteDesk.cs').read_text()
surface = (src / 'PC/Component/Software/RemoteDeskSurface.cs').read_text()
os = (src / 'PC/Component/Software/OS/OperatingSystem.cs').read_text()
manager = (src / 'PC/Component/Software/DeviceManager.cs').read_text()
menu = (src / 'DesktopContextMenu.cs').read_text()
dragger = (src / 'DesktopIconDragger.cs').read_text()
window_drag = (src / 'PC/Component/Software/OS/WindowDrag.cs').read_text()


def body(text, name):
    start = text.index(name)
    opening = text.index('{', start)
    depth = 0
    for i in range(opening, len(text)):
        depth += (text[i] == '{') - (text[i] == '}')
        if depth == 0:
            return text[opening + 1:i]
    raise AssertionError('unclosed method ' + name)


assert 'public class RemoteDesk : App' in app
assert 'public class RemoteDeskSurface : MonoBehaviour' in surface
assert 'public Canvas RemoteCanvas' in os
remote_canvas = body(os, 'public Canvas RemoteCanvas')
assert 'GetDesktopCanvas()' in remote_canvas
assert 'new GameObject("RemoteDesk virtual display"' in remote_canvas
assert 'desktopRect.SetParent(virtualRemoteCanvas.transform, false)' in remote_canvas
assert 'virtualRemoteCanvas.renderMode = RenderMode.WorldSpace' in remote_canvas
assert 'if (!Ready || Board == null || Board.monitor != null) return null;' in remote_canvas
assert 'EnsureRemoteDesk();' in os
assert 'appPrefabs.TryGetValue("RemoteDesk", out prefab)' in os
assert 'system.TryLaunchApp("RemoteDesk", string.Join(",", ids.ToArray()))' in manager
assert 'if (registered.Contains(id)) ids.Add' in manager
assert 'manager.OpenInRemoteDesk' in menu
assert 'FreeLimit = 5' in app and 'BitcoinManager.Bitcoin -= 5f' in app
assert 'HasLicense()' in app and 'system.FileManager.TryGetFile(0, LicensePath' in app
assert 'fm.Write(0, LicensePath, "RemoteDesk Pro v1")' in app
purchase = body(app, 'private void Purchase()')
assert purchase.index('fm.Write(0, LicensePath') < purchase.index('BitcoinManager.Bitcoin -= 5f')
assert 'SharedFramesPerSecond = 24f' in app
assert 'Mathf.Max(1, ActiveConnections) / SharedFramesPerSecond' in app
assert 'Application.targetFrameRate' not in app and 'Time.captureFramerate' not in app
assert 'system.Fault();' in body(app, 'public override void Open(string content)')
assert 'viewers[i].DisconnectSystem(system)' in app
assert 'session.os.Ready' in body(app, 'private void LateUpdate()')
assert 'captureCamera.Render()' in app and 'new RenderTexture(640, height' in app
capture = body(app, 'private void OnRemoteCanvas(Session session, Action action)')
assert 'finally' in capture and 'canvas.worldCamera = original' in capture
assert not re.search(r'canvas\.renderMode\s*=(?!=)', capture)
renderer = body(app, 'private void Capture(Session session)')
assert not re.search(r'canvas\.renderMode\s*=(?!=)', renderer)
assert 'captureCamera.Render();' in renderer and 'displayCamera.Render();' in renderer
assert 'Graphics.Blit(texture, session.frame)' in renderer
assert 'rect.GetWorldCorners(screenCorners)' in body(app, 'private void ConfigureCamera(Session session)')
assert 'new List<RaycastResult>()' in app and 'session.raycaster.Raycast(evt, hits)' in app
for signal in ('pointerDownHandler', 'pointerUpHandler', 'pointerClickHandler',
               'beginDragHandler', 'dragHandler', 'endDragHandler', 'scrollHandler'):
    assert signal in app, signal
assert 'SetSelectedGameObject(session.pressed, evt)' in app
assert 'RemoteDesk.ForwardingRaycaster.Raycast(pointer, results)' in menu
assert 'RemoteDesk.ForwardingSystem == os' in menu
assert 'GetRenderCamera()' in dragger and 'MoveRemoteDrag(Vector2 position)' in dragger
assert 'canvas.worldCamera' in body(window_drag, 'public void OnDrag(PointerEventData eventData)')
assert 'focusWatermark = Label(' in body(app, 'private void BuildUi()')
assert 'Label(' not in body(app, 'private void RefreshLayout()').split('for (int i = 0; i < sessions.Count; i++)')[0]
layout = body(app, 'private void RefreshLayout()')
assert 'focusWatermark.gameObject.SetActive(!premium && focused)' in layout
assert 'heading.gameObject.SetActive(!focused)' in layout
for name in ('idInput', 'connectButton', 'gridButton', 'statusText'):
    assert name + '.gameObject.SetActive(!focused)' in layout
assert 'backButton.gameObject.SetActive(focused)' in layout
assert '"Close", "Maximize", "Minimize"' in layout and 'button.gameObject.SetActive(!focused)' in layout
assert 'focusRect.offsetMin = Vector2.zero' in layout
assert 'focusViewport.offsetMin = Vector2.zero' in layout
assert 'Button("X", card,' in layout and '() => Disconnect(chosen)' in layout
assert 'AspectRatioFitter.AspectMode.FitInParent' in app
assert 'int cols =' in layout

prefab = project / 'Assets/Resources/apps/RemoteDesk.prefab'
raw = prefab.read_text()
raw = raw.replace('%YAML 1.1\n', '').replace('%TAG !u! tag:unity3d.com,2011:\n', '')
raw = re.sub(r'--- !u!\d+ &\d+', '---', raw)
blocks = list(yaml.safe_load_all(raw))
root = next(b['MonoBehaviour'] for b in blocks if 'MonoBehaviour' in b
            and b['MonoBehaviour'].get('appName') == 'RemoteDesk')
assert root['m_Script']['guid'] == '7667aa25a2294de8abfd1e49fae971dd'
assert root['size'] == 280
assert root['windowState']['fileID'] != 0
names = {b['GameObject']['m_Name']: b['GameObject'] for b in blocks if 'GameObject' in b}
assert names['Scroll View']['m_IsActive'] == 0 and names['Refresh']['m_IsActive'] == 0
assert names['Close']['m_IsActive'] == 1
for path in (prefab, src / 'PC/Component/Software/RemoteDesk.cs',
             src / 'PC/Component/Software/RemoteDeskSurface.cs'):
    assert path.with_name(path.name + '.meta').exists(), path
print('PASS: RemoteDesk installation, license/limits, FPS, capture/input, BSOD and prefab static checks')
print('NOTE: Unity Editor/Play Mode required for pixels, raycast alignment, drag and keyboard verification')
