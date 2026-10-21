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
assert 'EnsureRemoteDesk();' not in os
assert '{fileID: 114706950818030795, guid: 30880d3feb9c466494d51cfad949dd4f, type: 3}' in (project / 'Assets/Resources/apps/Downloader.prefab').read_text()
assert 'system.TryLaunchApp("RemoteDesk", string.Join(",", ids.ToArray()))' in manager
assert 'if (registered.Contains(id)) ids.Add' in manager
assert 'manager.OpenInRemoteDesk' in menu
assert 'FreeLimit = 5' in app and 'BitcoinManager.Bitcoin -= 5f' in app
assert 'HasLicense()' in app and 'owner.FileManager.TryGetFile(0, LicensePath' in app
assert 'fm.Write(0, LicensePath, "RemoteDesk Pro v1")' in app
purchase = body(app, 'internal static bool TryPurchasePro(')
assert purchase.index('fm.Write(0, LicensePath') < purchase.index('BitcoinManager.Bitcoin -= 5f')
assert 'SharedFramesPerSecond = 24f' in app
assert 'ProFramesPerSecond = 60f' in app
assert 'premium ? 1f / ProFramesPerSecond' in app
assert 'Mathf.Max(1, FreeConnections) / SharedFramesPerSecond' in app
assert '!viewer.premium' in body(app, 'private static int FreeConnections')
assert 'Application.targetFrameRate' not in app and 'Time.captureFramerate' not in app
assert 'system.Fault();' in body(app, 'public override void Open(string content)')
assert 'viewers[i].DisconnectSystem(system)' in app
assert 'session.os.Ready' in body(app, 'private void LateUpdate()')
assert 'captureCamera.Render()' in app and 'new RenderTexture(size.x, size.y' in app
capture = body(app, 'private void OnRemoteCanvas(Session session, Action action)')
assert 'finally' in capture and 'canvas.worldCamera = originalCamera' in capture
assert not re.search(r'canvas\.renderMode\s*=(?!=)', capture)
renderer = body(app, 'private void Capture(Session session)')
assert not re.search(r'canvas\.renderMode\s*=(?!=)', renderer)
assert 'captureCamera.Render();' in renderer and 'displayCamera.Render();' in renderer
assert 'Graphics.Blit(original, session.frame)' in renderer
camera_setup = body(app, 'private void ConfigureCamera(Session session)')
assert 'rect.GetWorldCorners(screenCorners)' in camera_setup
assert ' - normal * depth' in camera_setup and 'Quaternion.LookRotation(normal, up.normalized)' in camera_setup
assert 'Matrix4x4.Ortho(-width * .5f, width * .5f' in camera_setup
assert 'Mathf.Clamp(Screen.width, 1280, 1920)' in body(app, 'private Vector2Int FrameSize(')
assert 'view.x / 800f, view.y / 500f' in body(app, 'private Vector2 FocusedDesktopSize()')
assert 'root.sizeDelta = FocusedDesktopSize()' in capture and 'root.sizeDelta = originalSize' in capture
assert 'root.sizeDelta = original;' in renderer
assert 'originalCamera.targetTexture = session.frame' in capture
assert 'displayCamera.targetTexture = session.frame' in renderer
assert 'ConnectionsPath = "System/RemoteDesk.connections"' in app
assert 'JsonUtility.FromJson<SavedConnections>' in body(app, 'private void LoadSaved()')
assert 'fm.Write(0, ConnectionsPath' in body(app, 'private void SaveConnections()')
assert 'savedIds.Remove(id)' in body(app, 'private void RemoveSaved(int id)')
assert 'string result = board.Boot();' in body(app, 'private void Wake(int id)')
assert 'ReconnectSaved();' in body(app, 'private void LateUpdate()')
assert 'new List<RaycastResult>()' in app and 'session.raycaster.Raycast(evt, hits)' in app
for signal in ('pointerDownHandler', 'pointerUpHandler', 'pointerClickHandler',
               'beginDragHandler', 'dragHandler', 'endDragHandler', 'scrollHandler'):
    assert signal in app, signal
assert 'SetSelectedGameObject(session.pressed, evt)' in app
assert 'RemoteDesk.ForwardingRaycaster.Raycast(pointer, results)' in menu
assert 'RemoteDesk.ForwardingSystem == os' in menu
assert 'GetRenderCamera()' in dragger and 'MoveRemoteDrag(Vector2 position)' in dragger
assert 'canvas.worldCamera' in body(window_drag, 'public void OnDrag(PointerEventData eventData)')
build_ui = body(app, 'private void BuildUi()')
assert 'focusWatermark = Label(' in build_ui
assert 'proButton = Button("Купить", transform, 220, 8, 120, 30, ShowPromotion)' in build_ui
assert 'idInput = InputField("System ID (hex)", panel, 14, 8' in build_ui
assert 'connectButton = Button("Connect", panel, 175, 8' in build_ui
assert 'Label(' not in body(app, 'private void RefreshLayout()').split('for (int i = 0; i < savedIds.Count; i++)')[0]
layout = body(app, 'private void RefreshLayout()')
assert 'focusWatermark.gameObject.SetActive(!premium && focused)' in layout
assert 'windowTitle.text = premium ? "RemoteDesk- Pro" : "RemoteDesk"' in layout
assert 'gridRect.offsetMax = new Vector2(-14, -87)' in layout
promo = body(app, 'private void ShowPromotion()')
assert 'system.TryOpenRemoteDeskOffer()' in promo
assert 'Box(' not in promo and 'RawImage' not in promo
assert 'promoShownThisLaunch' in body(app, 'public override void Open(string content)')
assert 'LaunchApp(prefab);' in body(os, 'public bool TryOpenRemoteDeskOffer()')
assert 'Resources.Load<GameObject>("RemoteDesk/RemoteDeskProOffer")' in os
assert 'IsAppInstalled("RemoteDesk")' in body(os, 'public bool TryOpenRemoteDeskOffer()')
offer = (src / 'PC/Component/Software/RemoteDeskProOffer.cs').read_text()
assert 'public class RemoteDeskProOffer : App' in offer
assert 'RemoteDesk.TryPurchasePro(system, out error)' in offer
assert 'banner != null ? banner : Resources.Load<Sprite>("RemoteDesk/pro_banner")' in offer
assert 'artwork == null' in offer and 'Debug.LogError(' in offer
assert 'buy.gameObject.AddComponent<Button>().onClick.AddListener(Purchase)' in offer
assert 'titleText.text = "Buy pro!"' in offer
assert 'canMaximize = false' in offer and 'button.gameObject.SetActive(false)' in offer
assert 'public override void Maximize() { }' in offer
assert 'public override void Minimize() { }' in offer
assert 'posterAspect / ScreenStretch(host)' in offer
assert 'Stretch(poster);' in offer and 'AspectRatioFitter' not in offer
assert 'RectTransformUtility.WorldToScreenPoint' in offer
for stretch in (.5, 1., 1.8):
    assert abs((1718. / 2400. / stretch) * stretch - 1718. / 2400.) < 1e-9
assert 'proButton = Button("Buy Pro' not in app
assert 'heading' not in layout
assert 'proButton.gameObject.SetActive(!focused && !premium)' in layout
assert 'uploadButton.gameObject.SetActive(focused)' in layout
assert 'Button("Upload", transform,' in build_ui
assert 'uploadBelowTitle ? 50f : 8f' in layout
upload = body(app, 'private void Upload()')
assert 'system.SelectFile("*", source =>' in upload
assert 'int destinationId = chosen.id' in upload
assert 'source.isFolder' in upload and 'source.content ?? ""' in upload
assert 'new File(name, content, false,' in upload
assert 'StartCoroutine(UploadProgress(destinationId, copy))' in upload
progress = body(app, 'private IEnumerator UploadProgress(')
assert 'bar.CallProgressBar(' in progress and 'bar.SetProgress(' in progress
assert 'uploadCancelled' in progress and 'CompleteUpload(id, copy)' in progress
complete = body(app, 'private void CompleteUpload(')
assert 'target.os.FileManager.Create(0, copy)' in complete
assert 'target.os.RefreshDesktopIcon()' in complete
assert 'target.os.Ready' in complete
for name in ('idInput', 'connectButton', 'statusText'):
    assert name + '.gameObject.SetActive(!focused)' in layout
assert 'gridButton' not in app
assert 'backButton.gameObject.SetActive(focused)' in layout
assert '"Close", "Maximize", "Minimize"' in layout and 'button.gameObject.SetActive(true)' in layout
assert 'focusRect.offsetMin = Vector2.zero' in layout
assert 'focusViewport.offsetMin = Vector2.zero' in layout
assert 'Button("X", card,' in layout and '() => RemoveSaved(chosen)' in layout
assert 'Button("Wake", card,' in layout and '() => Wake(chosen)' in layout
assert 'for (int i = 0; i < savedIds.Count; i++)' in layout
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
assert root['icon']['guid'] == '5225909a642e461eba4c04954de2747c'
names = {b['GameObject']['m_Name']: b['GameObject'] for b in blocks if 'GameObject' in b}
assert names['Scroll View']['m_IsActive'] == 0 and names['Refresh']['m_IsActive'] == 0
assert names['Close']['m_IsActive'] == 1
for path in (prefab, src / 'PC/Component/Software/RemoteDesk.cs',
             src / 'PC/Component/Software/RemoteDeskSurface.cs'):
    assert path.with_name(path.name + '.meta').exists(), path
poster = project / 'Assets/Resources/RemoteDesk/pro_banner.png'
assert poster.exists() and poster.with_name(poster.name + '.meta').exists()
assert poster.read_bytes()[:8] == b'\x89PNG\r\n\x1a\n'
poster_meta = poster.with_name(poster.name + '.meta').read_text()
assert 'nPOTScale: 0' in poster_meta
assert 'textureShape: 1' in poster_meta and 'textureType: 8' in poster_meta
assert 'spriteID: 979c1648735b4489943f73574502406d' in poster_meta
assert (src / 'PC/Component/Software/RemoteDeskProOffer.cs.meta').exists()
offer_prefab = project / 'Assets/Resources/RemoteDesk/RemoteDeskProOffer.prefab'
assert offer_prefab.with_name(offer_prefab.name + '.meta').exists()
raw_offer = offer_prefab.read_text().replace('%YAML 1.1\n','').replace('%TAG !u! tag:unity3d.com,2011:\n','')
raw_offer = re.sub(r'--- !u!\d+ &\d+', '---', raw_offer)
offer_blocks = list(yaml.safe_load_all(raw_offer))
offer_root = next(b['MonoBehaviour'] for b in offer_blocks if 'MonoBehaviour' in b
                  and b['MonoBehaviour'].get('appName') == 'RemoteDesk Pro offer')
assert offer_root['m_Script']['guid'] == '557f8184d90d4dff9ae9a31297a679b2'
assert offer_root['banner']['guid'] == '979c1648735b4489943f73574502406d'
assert offer_root['windowState']['fileID'] != 0
assert offer_root['icon']['guid'] == root['icon']['guid']
assert 'm_Text: Buy pro!' in offer_prefab.read_text()
assert 'm_SizeDelta: {x: 320, y: 720}' in offer_prefab.read_text()
offer_names = {b['GameObject']['m_Name']: b['GameObject'] for b in offer_blocks if 'GameObject' in b}
assert offer_names['Maximize']['m_IsActive'] == 0 and offer_names['Minimize']['m_IsActive'] == 0
assert offer_names['Close']['m_IsActive'] == 1
icon = project / 'Assets/Resources/RemoteDesk/remote_icon.png'
assert icon.exists() and icon.read_bytes()[:8] == b'\x89PNG\r\n\x1a\n'
icon_meta = icon.with_name(icon.name + '.meta').read_text()
assert 'textureType: 8' in icon_meta and root['icon']['guid'] in icon_meta
print('PASS: RemoteDesk installation, Pro banner/purchase, FPS, capture/input and prefab checks')
print('NOTE: Unity Editor/Play Mode required for pixels, raycast alignment, drag and keyboard verification')
