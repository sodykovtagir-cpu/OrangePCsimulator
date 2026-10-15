using System;
using System.Collections.Generic;
using PC.Component.Software.OS;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PC.Component.Software
{
    /// <summary>Live remote PCOS desktop, using each motherboard's own UI canvas.</summary>
    public class RemoteDesk : App
    {
        private sealed class Session
        {
            public int id;
            public Motherboard board;
            public global::PC.Component.Software.OS.OperatingSystem os;
            public Canvas canvas;
            public GraphicRaycaster raycaster;
            public RenderTexture frame;
            public float nextFrame;
            public PointerEventData pointer;
            public GameObject pressed;
            public GameObject dragged;
            public bool dragging;
            public DesktopIconDragger desktopIcon;
        }

        [Serializable]
        private sealed class SavedConnections
        {
            public List<int> ids = new List<int>();
        }

        private const string LicensePath = "System/RemoteDesk.license";
        private const string ConnectionsPath = "System/RemoteDesk.connections";
        private const int FreeLimit = 5;
        private const float SharedFramesPerSecond = 24f;
        private static readonly List<RemoteDesk> viewers = new List<RemoteDesk>();
        // Only set while dispatching a synthetic event. Existing PCOS hit
        // tests must see the remote canvas, not the local game's UI overlay.
        internal static global::PC.Component.Software.OS.OperatingSystem ForwardingSystem { get; private set; }
        internal static GraphicRaycaster ForwardingRaycaster { get; private set; }
        private readonly List<Session> sessions = new List<Session>();
        private readonly List<int> savedIds = new List<int>();
        private bool savedLoaded;
        private float nextReconnect;
        private Text windowTitle;
        private RectTransform panel;
        private RectTransform gridContent;
        private RectTransform gridViewport;
        private InputField idInput;
        private Button connectButton;
        private Button proButton;
        private Button backButton;
        private Text statusText;
        private Text heading;
        private GameObject gridPage;
        private GameObject focusPage;
        private RectTransform focusViewport;
        private RawImage focusImage;
        private Text focusWatermark;
        private Camera captureCamera;
        private Font font;
        private int focusedId;
        private bool premium;
        private bool built;
        private bool capturing;
        private readonly Vector3[] screenCorners = new Vector3[4];
        private Vector2 lastSize;
        private int lastTotal = -1;
        private string status = "Enter a system ID or import the Device Manager selection.";

        private static readonly Color Ink = new Color(.12f, .14f, .17f, 1f);
        private static readonly Color Grey = new Color(.72f, .74f, .77f, 1f);
        private static readonly Color Surface = new Color(.96f, .97f, .98f, 1f);
        private float W => panel.rect.width;
        private float H => panel.rect.height;

        public override void Open(string content)
        {
            base.Open(content);
            if (system == null) return;
            // A nested session is a real OS fault on the remotely controlled PC.
            if (IsControlled(system))
            {
                system.Fault();
                for (int i = viewers.Count - 1; i >= 0; --i)
                    if (viewers[i] != null) viewers[i].DisconnectSystem(system);
                Close();
                return;
            }
            var window = transform as RectTransform;
            var host = transform.parent as RectTransform;
            if (window != null && host != null && host.rect.width > 220 && host.rect.height > 220)
                window.sizeDelta = new Vector2(Mathf.Min(900, host.rect.width - 16),
                                               Mathf.Min(650, host.rect.height - 16));
            var old = transform.Find("Scroll View");
            if (old != null) old.gameObject.SetActive(false);
            old = transform.Find("Refresh");
            if (old != null) old.gameObject.SetActive(false);
            if (!built) BuildUi();
            premium = HasLicense();
            if (!savedLoaded) LoadSaved();
            if (!viewers.Contains(this)) viewers.Add(this);
            if (!string.IsNullOrEmpty(content))
                foreach (string id in content.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries))
                    Connect(id);
            ReconnectSaved();
            RefreshLayout();
        }

        private static int ActiveConnections
        {
            get
            {
                int count = 0;
                foreach (var viewer in viewers)
                    if (viewer != null && viewer.isActiveAndEnabled) count += viewer.sessions.Count;
                return count;
            }
        }

        private static bool IsControlled(global::PC.Component.Software.OS.OperatingSystem os)
        {
            foreach (var viewer in viewers)
                if (viewer != null)
                    foreach (var session in viewer.sessions)
                        if (session.os == os) return true;
            return false;
        }

        private bool HasLicense()
        {
            File license;
            return system != null && system.FileManager != null &&
                system.FileManager.TryGetFile(0, LicensePath, out license) &&
                license != null && license.content == "RemoteDesk Pro v1";
        }

        private void Purchase()
        {
            if (premium) return;
            if (BitcoinManager.Bitcoin < 5f)
            { status = "Pro costs 5 BTC. Insufficient balance."; RefreshLayout(); return; }
            var fm = system != null ? system.FileManager : null;
            var file = fm != null ? fm.Write(0, LicensePath, "RemoteDesk Pro v1") : null;
            if (file == null || file.content != "RemoteDesk Pro v1")
            { status = "Cannot save the license to this PC."; RefreshLayout(); return; }
            file.hidden = true;
            BitcoinManager.Bitcoin -= 5f;
            BitcoinManager.Save();
            premium = true;
            status = "";
            RefreshLayout();
        }

        private void LoadSaved()
        {
            savedLoaded = true;
            File file;
            if (system == null || system.FileManager == null ||
                !system.FileManager.TryGetFile(0, ConnectionsPath, out file) || file == null) return;
            try
            {
                var saved = JsonUtility.FromJson<SavedConnections>(file.content);
                if (saved == null || saved.ids == null) return;
                foreach (int id in saved.ids)
                    if (id != 0 && id != system.SystemId && !savedIds.Contains(id)) savedIds.Add(id);
            }
            catch (Exception) { status = "Saved connections could not be read."; }
        }

        private void SaveConnections()
        {
            var fm = system != null ? system.FileManager : null;
            if (fm == null) return;
            var file = fm.Write(0, ConnectionsPath,
                JsonUtility.ToJson(new SavedConnections { ids = new List<int>(savedIds) }));
            if (file != null) file.hidden = true;
        }

        private bool Attach(int id, Motherboard board)
        {
            if (sessions.Exists(s => s.id == id)) return true;
            if (!premium && sessions.Count >= FreeLimit) return false;
            var remote = board != null ? board.System as global::PC.Component.Software.OS.OperatingSystem : null;
            if (remote == null || !remote.Ready) return false;
            var canvas = remote.RemoteCanvas;
            var raycaster = canvas != null ? canvas.GetComponent<GraphicRaycaster>() : null;
            if (raycaster == null) return false;
            sessions.Add(new Session {
                id = id, board = board, os = remote, canvas = canvas,
                raycaster = raycaster, frame = CreateFrame(canvas)
            });
            return true;
        }

        private void ReconnectSaved()
        {
            bool changed = false;
            foreach (int id in savedIds)
                if (!sessions.Exists(s => s.id == id))
                    changed |= Attach(id, DeviceAutomation.Find(id));
            if (changed) RefreshLayout();
        }

        private void Connect(string input)
        {
            if (string.IsNullOrEmpty(input)) return;
            int id;
            if (!int.TryParse(input.Trim(), System.Globalization.NumberStyles.HexNumber,
                System.Globalization.CultureInfo.InvariantCulture, out id) || id == 0)
            { status = "Invalid hexadecimal system ID."; return; }
            var board = DeviceAutomation.Find(id);
            if (board == null || board == system.Board)
            { status = "Device not found or cannot connect to this PC itself."; return; }
            if (!savedIds.Contains(id))
            {
                savedIds.Add(id);
                SaveConnections();
            }
            if (sessions.Exists(s => s.id == id))
                status = "Already connected to " + id.ToString("X8") + ".";
            else if (!board.Running)
                status = "Saved " + id.ToString("X8") + ". Device is offline; use Wake.";
            else if (Attach(id, board))
                status = "Connected to " + id.ToString("X8") + ".";
            else if (!premium && sessions.Count >= FreeLimit)
                status = "Saved device. Free edition allows 5 active connections.";
            else
                status = "Saved device. Waiting for PCOS to finish booting.";
            RefreshLayout();
        }

        private void Wake(int id)
        {
            var board = DeviceAutomation.Find(id);
            if (board == null)
                status = "Saved device not found in this world.";
            else if (board.Running)
                status = "Device is running; waiting for PCOS to boot.";
            else
            {
                string result = board.Boot();
                status = board.Running ? "Wake sent; waiting for PCOS." :
                    "Cannot wake this device: " + result;
            }
            nextReconnect = 0f;
            RefreshLayout();
        }

        private static RenderTexture CreateFrame(Canvas canvas)
        {
            int height = 360;
            // Textured monitors already render to their own RT; preserve their
            // true aspect ratio rather than stretching a 2:1 screen to 16:9.
            if (canvas != null && canvas.renderMode == RenderMode.ScreenSpaceCamera &&
                canvas.worldCamera != null && canvas.worldCamera.targetTexture != null)
            {
                var source = canvas.worldCamera.targetTexture;
                height = Mathf.Clamp(Mathf.RoundToInt(640f * source.height / source.width), 180, 720);
            }
            var frame = new RenderTexture(640, height, 16, RenderTextureFormat.ARGB32);
            frame.Create();
            return frame;
        }

        private void ConnectEntered()
        {
            if (idInput != null) Connect(idInput.text);
            RefreshLayout();
        }

        private void DisconnectSystem(global::PC.Component.Software.OS.OperatingSystem remote)
        {
            for (int i = sessions.Count - 1; i >= 0; i--)
                if (sessions[i].os == remote) RemoveSession(sessions[i]);
            status = "Remote system stopped. Session disconnected.";
            RefreshLayout();
        }

        private void RemoveSession(Session session)
        {
            if (session == null) return;
            if (session.desktopIcon != null) session.desktopIcon.EndRemoteDrag();
            sessions.Remove(session);
            if (session.frame != null) { session.frame.Release(); Destroy(session.frame); }
            if (focusedId == session.id) focusedId = 0;
        }

        private void RemoveSaved(int id)
        {
            var session = sessions.Find(s => s.id == id);
            if (session != null) RemoveSession(session);
            if (savedIds.Remove(id)) SaveConnections();
            status = "Removed " + id.ToString("X8") + ".";
            RefreshLayout();
        }

        private void OnDestroy()
        {
            viewers.Remove(this);
            for (int i = sessions.Count - 1; i >= 0; i--) RemoveSession(sessions[i]);
            if (captureCamera != null) Destroy(captureCamera.gameObject);
        }

        public override void OnSystemStop()
        {
            viewers.Remove(this);
            for (int i = sessions.Count - 1; i >= 0; i--) RemoveSession(sessions[i]);
            base.OnSystemStop();
        }

        private void LateUpdate()
        {
            if (!built || panel == null || !gameObject.activeInHierarchy) return;
            var size = panel.rect.size;
            if (Vector2.Distance(size, lastSize) > 1f) { lastSize = size; RefreshLayout(); }
            if (lastTotal != ActiveConnections) UpdateStatus();
            float now = Time.unscaledTime;
            if (now >= nextReconnect)
            {
                nextReconnect = now + 1f;
                ReconnectSaved();
            }
            for (int i = sessions.Count - 1; i >= 0; i--)
            {
                var session = sessions[i];
                if (session.board == null || session.os == null || !session.os.Ready ||
                    session.board.System != session.os)
                { RemoveSession(session); RefreshLayout(); continue; }
                var liveCanvas = session.os.RemoteCanvas;
                if (liveCanvas == null)
                { RemoveSession(session); RefreshLayout(); continue; }
                if (liveCanvas != session.canvas)
                {
                    if (session.desktopIcon != null) session.desktopIcon.EndRemoteDrag();
                    session.canvas = liveCanvas;
                    session.raycaster = liveCanvas.GetComponent<GraphicRaycaster>();
                    if (session.raycaster == null)
                    { RemoveSession(session); RefreshLayout(); continue; }
                    var previousFrame = session.frame;
                    session.frame = CreateFrame(liveCanvas);
                    if (previousFrame != null) { previousFrame.Release(); Destroy(previousFrame); }
                    session.pointer = null;
                    session.desktopIcon = null;
                    session.pressed = null;
                    session.dragged = null;
                    RefreshLayout();
                }
                if (now < session.nextFrame) continue;
                // One shared refresh budget across all attached PCs/miners.
                session.nextFrame = now + Mathf.Max(1, ActiveConnections) / SharedFramesPerSecond;
                Capture(session);
            }
        }

        private void EnsureCamera()
        {
            if (captureCamera != null) return;
            var go = new GameObject("RemoteDesk offscreen camera");
            captureCamera = go.AddComponent<Camera>();
            captureCamera.enabled = false;
            captureCamera.orthographic = true;
            captureCamera.clearFlags = CameraClearFlags.SolidColor;
            captureCamera.backgroundColor = Color.black;
            captureCamera.cullingMask = 1 << 5; // UI layer only.
            captureCamera.nearClipPlane = .01f;
        }

        // Render the EXISTING world-space UI from in front of its rectangle.
        // Changing a monitor's Canvas.renderMode each frame broke both its own
        // display and our texture; never change that mode for capture/input.
        private void ConfigureCamera(Session session)
        {
            EnsureCamera();
            var rect = session.canvas.transform as RectTransform;
            if (rect == null) return;
            rect.GetWorldCorners(screenCorners);
            Vector3 right = screenCorners[3] - screenCorners[0];
            Vector3 up = screenCorners[1] - screenCorners[0];
            Vector3 normal = Vector3.Cross(right, up).normalized;
            if (normal.sqrMagnitude < .5f) normal = rect.forward;
            float width = right.magnitude;
            float height = up.magnitude;
            float depth = Mathf.Max(width, height) * 2f + .5f;
            // The physical UI faces the opposite side of its world-space
            // rectangle. Capturing from +normal mirrored text and caused the
            // GraphicRaycaster to discard clicks as back-facing graphics.
            captureCamera.transform.position = (screenCorners[0] + screenCorners[2]) * .5f - normal * depth;
            captureCamera.transform.rotation = Quaternion.LookRotation(normal, up.normalized);
            captureCamera.orthographicSize = Mathf.Max(height * .5f,
                width * session.frame.height / (2f * session.frame.width)) * 1.01f;
            captureCamera.farClipPlane = depth + Mathf.Max(width, height) + 1f;
        }

        private void OnRemoteCanvas(Session session, Action action)
        {
            if (capturing || session == null || session.canvas == null) return;
            capturing = true;
            var canvas = session.canvas;
            var original = canvas.worldCamera;
            try
            {
                if (canvas.renderMode == RenderMode.WorldSpace)
                {
                    ConfigureCamera(session);
                    captureCamera.targetTexture = session.frame;
                    canvas.worldCamera = captureCamera; // raycaster only; no mode change
                }
                action();
            }
            finally
            {
                if (canvas != null && canvas.renderMode == RenderMode.WorldSpace)
                    canvas.worldCamera = original;
                if (captureCamera != null) captureCamera.targetTexture = null;
                capturing = false;
            }
        }

        private void Capture(Session session)
        {
            var canvas = session.canvas;
            Canvas.ForceUpdateCanvases();
            if (canvas.renderMode == RenderMode.WorldSpace)
            {
                ConfigureCamera(session);
                try
                {
                    captureCamera.targetTexture = session.frame;
                    captureCamera.Render();
                }
                finally { captureCamera.targetTexture = null; }
            }
            else
            {
                // TexturedMonitor/Projector own a camera and an RT. Re-render
                // that camera and copy its pixels; taking it over makes the
                // physical monitor show a blank frame.
                var displayCamera = canvas.worldCamera;
                var texture = displayCamera != null ? displayCamera.targetTexture : null;
                if (texture != null && displayCamera != captureCamera)
                {
                    if (canvas.renderMode == RenderMode.ScreenSpaceCamera)
                        displayCamera.Render();
                    Graphics.Blit(texture, session.frame);
                }
            }
        }

        // Called by RemoteDeskSurface. Forward actual UGUI click/drag/scroll
        // to the remote OS in the camera's pixel coordinate space.
        public void Forward(int id, PointerEventData source, RectTransform surface, string phase)
        {
            var session = sessions.Find(s => s.id == id);
            if (source == null || surface == null || session == null ||
                session.raycaster == null || session.frame == null || EventSystem.current == null) return;
            Vector2 local;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(surface,
                source.position, source.pressEventCamera, out local)) return;
            float u = Mathf.Clamp01(Mathf.InverseLerp(surface.rect.xMin, surface.rect.xMax, local.x));
            float v = Mathf.Clamp01(Mathf.InverseLerp(surface.rect.yMin, surface.rect.yMax, local.y));
            // Textured monitors raycast in THEIR camera's RT pixel space.
            var eventTexture = session.canvas.renderMode == RenderMode.ScreenSpaceCamera &&
                session.canvas.worldCamera != null ? session.canvas.worldCamera.targetTexture : null;
            int eventWidth = eventTexture != null ? eventTexture.width : session.frame.width;
            int eventHeight = eventTexture != null ? eventTexture.height : session.frame.height;
            Vector2 pos = new Vector2(u * eventWidth, v * eventHeight);
            OnRemoteCanvas(session, () =>
            {
                var previousSystem = ForwardingSystem;
                var previousRaycaster = ForwardingRaycaster;
                ForwardingSystem = session.os;
                ForwardingRaycaster = session.raycaster;
                try
                {
                    var evt = session.pointer ?? new PointerEventData(EventSystem.current);
                    Vector2 previous = evt.position;
                    evt.position = pos;
                    evt.delta = pos - previous;
                    evt.pointerId = source.pointerId;
                    evt.button = source.button;
                    evt.clickCount = source.clickCount;
                    evt.scrollDelta = source.scrollDelta;
                    session.pointer = evt;
                    var hits = new List<RaycastResult>();
                    session.raycaster.Raycast(evt, hits);
                    GameObject hit = hits.Count > 0 ? hits[0].gameObject : null;
                    evt.pointerCurrentRaycast = hits.Count > 0 ? hits[0] : new RaycastResult();
                    evt.pointerEnter = hit;
                    if (phase == "down")
                    {
                        evt.eligibleForClick = true;
                        evt.dragging = false;
                        evt.pressPosition = pos;
                        evt.pointerPressRaycast = evt.pointerCurrentRaycast;
                        session.dragging = false;
                        session.pressed = hit != null
                            ? ExecuteEvents.ExecuteHierarchy(hit, evt, ExecuteEvents.pointerDownHandler) : null;
                        if (session.pressed == null && hit != null)
                            session.pressed = ExecuteEvents.GetEventHandler<IPointerClickHandler>(hit);
                        evt.pointerPress = session.pressed;
                        evt.rawPointerPress = hit;
                        session.dragged = hit != null ? ExecuteEvents.GetEventHandler<IDragHandler>(hit) : null;
                        evt.pointerDrag = session.dragged;
                        if (session.dragged != null)
                            ExecuteEvents.Execute(session.dragged, evt, ExecuteEvents.initializePotentialDrag);
                        session.desktopIcon = hit != null ? hit.GetComponentInParent<DesktopIconDragger>() : null;
                        if (session.desktopIcon != null) session.desktopIcon.BeginRemoteDrag();
                        if (session.pressed != null && source.button == PointerEventData.InputButton.Left)
                            EventSystem.current.SetSelectedGameObject(session.pressed, evt);
                    }
                    else if (phase == "drag")
                    {
                        if (session.desktopIcon != null) session.desktopIcon.MoveRemoteDrag(pos);
                        if (session.dragged != null)
                        {
                            if (!session.dragging && Vector2.Distance(pos, evt.pressPosition) > 5f)
                            {
                                ExecuteEvents.Execute(session.dragged, evt, ExecuteEvents.beginDragHandler);
                                session.dragging = true;
                            }
                            if (session.dragging)
                            {
                                evt.dragging = true;
                                ExecuteEvents.Execute(session.dragged, evt, ExecuteEvents.dragHandler);
                            }
                        }
                        if (session.desktopIcon != null &&
                            Vector2.Distance(pos, evt.pressPosition) > PointerInput.Slop)
                        {
                            session.dragging = true;
                            evt.dragging = true;
                        }
                    }
                    else if (phase == "up")
                    {
                        evt.dragging = session.dragging;
                        if (session.pressed != null)
                            ExecuteEvents.Execute(session.pressed, evt, ExecuteEvents.pointerUpHandler);
                        if (session.dragged != null && session.dragging)
                        {
                            if (hit != null) ExecuteEvents.ExecuteHierarchy(hit, evt, ExecuteEvents.dropHandler);
                            ExecuteEvents.Execute(session.dragged, evt, ExecuteEvents.endDragHandler);
                        }
                        if (!session.dragging && session.pressed != null && hit != null &&
                            ExecuteEvents.GetEventHandler<IPointerClickHandler>(hit) == session.pressed)
                            ExecuteEvents.Execute(session.pressed, evt, ExecuteEvents.pointerClickHandler);
                        if (session.desktopIcon != null) session.desktopIcon.EndRemoteDrag();
                        session.desktopIcon = null;
                        session.pressed = null;
                        session.dragged = null;
                        session.dragging = false;
                        evt.pointerPress = null;
                        evt.pointerDrag = null;
                        evt.dragging = false;
                    }
                    else if (phase == "scroll" && hit != null)
                        ExecuteEvents.ExecuteHierarchy(hit, evt, ExecuteEvents.scrollHandler);

                    // Desktop context menus normally require physical monitor zoom.
                    // A right click in the remote image is an intentional desktop
                    // interaction, but only when the raycast actually hit this OS.
                    if (phase == "down" && source.button == PointerEventData.InputButton.Right && hit != null)
                    {
                        var menu = DesktopContextMenu.For(session.os);
                        if (menu != null && hit.GetComponentInParent<FileIcon>() == null &&
                            session.os.IsDesktopContextTarget(hit) && menu.CanReceiveDesktopPointer(pos))
                            menu.ShowDesktopMenu(pos, true);
                    }
                }
                finally
                {
                    ForwardingSystem = previousSystem;
                    ForwardingRaycaster = previousRaycaster;
                }
            });
        }

        private void BuildUi()
        {
            built = true;
            var title = transform.Find("Title");
            windowTitle = title != null ? title.GetComponent<Text>() : null;
            font = windowTitle != null ? windowTitle.font : Resources.GetBuiltinResource<Font>("Arial.ttf");
            backButton = Button("Back", transform, 220, 8, 100, 30,
                () => { focusedId = 0; RefreshLayout(); });
            backButton.gameObject.SetActive(false);
            panel = Box("RemoteDesk UI", transform, 8, 40, 604, 490, Color.white);
            panel.anchorMin = Vector2.zero;
            panel.anchorMax = Vector2.one;
            panel.pivot = new Vector2(.5f, .5f);
            panel.offsetMin = new Vector2(8f, 8f);
            panel.offsetMax = new Vector2(-8f, -40f);
            heading = Label("Freeware edition", panel, 14, 4, 570, 32, 16);
            idInput = InputField("System ID (hex)", panel, 14, 43, 155, 33);
            connectButton = Button("Connect", panel, 175, 43, 94, 33, ConnectEntered);
            proButton = Button("Buy Pro - 5 BTC", panel, 275, 43, 160, 33, Purchase);
            statusText = Label("", panel, 14, 81, 700, 28, 13);
            gridPage = Box("Grid page", panel, 14, 115, 576, 280, Surface).gameObject;
            focusPage = Box("Focus page", panel, 14, 115, 576, 280, Surface).gameObject;
            var focusRect = focusPage.GetComponent<RectTransform>();
            focusViewport = Box("Screen area", focusRect, 0, 0, 576, 238, Color.black);
            focusImage = NewRect("Remote desktop", focusViewport, 0, 0, 576, 238).gameObject.AddComponent<RawImage>();
            var fitter = focusImage.gameObject.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            fitter.aspectRatio = 16f / 9f;
            var view = focusImage.gameObject.AddComponent<RemoteDeskSurface>();
            view.Init(this, 0);
            focusWatermark = Label("RemoteDesk FREE", focusImage.transform, 10, 10, 440, 38, 24);
            focusWatermark.alignment = TextAnchor.MiddleCenter;
            focusWatermark.color = new Color(1f, 1f, 1f, .5f);
            gridViewport = Box("Grid viewport", gridPage.transform, 0, 0, 576, 280, Color.white);
            gridViewport.gameObject.AddComponent<RectMask2D>();
            gridContent = NewRect("Connected devices", gridViewport, 0, 0, 576, 0);
            var scroller = gridPage.AddComponent<ScrollRect>();
            scroller.viewport = gridViewport;
            scroller.content = gridContent;
            scroller.horizontal = false;
            scroller.scrollSensitivity = 20;
            lastSize = panel.rect.size;
        }

        private void UpdateStatus()
        {
            lastTotal = ActiveConnections;
            if (statusText != null)
                statusText.text = status + "   Online: " + sessions.Count +
                    "   Stream FPS/device: " + (lastTotal > 0 ? SharedFramesPerSecond / lastTotal : 0f).ToString("0.#");
        }

        private void RefreshLayout()
        {
            if (!built || panel == null) return;
            var selected = sessions.Find(s => s.id == focusedId);
            if (focusedId != 0 && selected == null) focusedId = 0;
            bool focused = selected != null;
            float width = W - 28f;
            bool compact = W < 535f;
            if (windowTitle != null) windowTitle.text = premium ? "RemoteDesk- Pro" : "RemoteDesk";
            heading.text = "Free - watermark and 5 connections";
            heading.rectTransform.sizeDelta = new Vector2(width, 32);
            heading.gameObject.SetActive(!focused && !premium);
            idInput.gameObject.SetActive(!focused);
            connectButton.gameObject.SetActive(!focused);
            proButton.gameObject.SetActive(!focused && !premium);
            statusText.gameObject.SetActive(!focused);
            backButton.gameObject.SetActive(focused);
            // Keep the native title and window controls in the full view.
            foreach (string chrome in new[] { "Close", "Maximize", "Minimize" })
            {
                var button = transform.Find(chrome);
                if (button != null) button.gameObject.SetActive(true);
            }
            if (focused)
            {
                var window = transform as RectTransform;
                Place(backButton.GetComponent<RectTransform>(),
                    window != null ? Mathf.Min(220f, Mathf.Max(160f, window.rect.width - 230f)) : 220f,
                    8, 100, 30);
            }
            float connectWidth = compact ? Mathf.Min(94, Mathf.Max(60, W * .28f)) : 94;
            float connectX = compact ? W - connectWidth - 14 : 175;
            Place(idInput.GetComponent<RectTransform>(), 14, 43,
                compact ? Mathf.Max(45, connectX - 20) : 155, 33);
            Place(connectButton.GetComponent<RectTransform>(), connectX, 43, connectWidth, 33);
            Place(proButton.GetComponent<RectTransform>(), compact ? 14 : 275, compact ? 83 : 43,
                compact ? Mathf.Max(70, W - 28) : 160, 33);
            Place(statusText.rectTransform, 14, compact ? 121 : 81, width, compact ? 38 : 28);
            UpdateStatus();
            var gridRect = (RectTransform)gridPage.transform;
            var focusRect = (RectTransform)focusPage.transform;
            gridRect.anchorMin = Vector2.zero;
            gridRect.anchorMax = Vector2.one;
            gridRect.offsetMin = new Vector2(14, 52);
            gridRect.offsetMax = new Vector2(-14, compact ? -166 : -115);
            focusRect.anchorMin = Vector2.zero;
            focusRect.anchorMax = Vector2.one;
            focusRect.offsetMin = Vector2.zero;
            focusRect.offsetMax = Vector2.zero;
            gridViewport.anchorMin = Vector2.zero;
            gridViewport.anchorMax = Vector2.one;
            gridViewport.offsetMin = Vector2.zero;
            gridViewport.offsetMax = Vector2.zero;
            gridContent.anchorMin = new Vector2(0, 1);
            gridContent.anchorMax = new Vector2(1, 1);
            gridContent.pivot = new Vector2(.5f, 1);
            gridContent.anchoredPosition = Vector2.zero;
            focusViewport.anchorMin = Vector2.zero;
            focusViewport.anchorMax = Vector2.one;
            focusViewport.offsetMin = Vector2.zero;
            focusViewport.offsetMax = Vector2.zero;
            focusPage.SetActive(focused);
            gridPage.SetActive(!focused);
            focusImage.texture = focused ? selected.frame : null;
            if (focused)
                focusImage.GetComponent<AspectRatioFitter>().aspectRatio =
                    (float)selected.frame.width / selected.frame.height;
            var surface = focusImage.GetComponent<RemoteDeskSurface>();
            if (surface != null) surface.Init(this, focused ? focusedId : 0);
            for (int i = gridContent.childCount - 1; i >= 0; --i)
            {
                var row = gridContent.GetChild(i);
                row.gameObject.SetActive(false);
                row.SetParent(null, false);
                Destroy(row.gameObject);
            }
            float totalWidth = Mathf.Max(160f, W - 28f);
            int cols = totalWidth > 780 ? 4 : totalWidth > 510 ? 3 : totalWidth > 270 ? 2 : 1;
            float tileW = (totalWidth - (cols - 1) * 8) / cols;
            float tileH = tileW * 9f / 16f + 38f;
            for (int i = 0; i < savedIds.Count; i++)
            {
                int chosen = savedIds[i];
                var session = sessions.Find(item => item.id == chosen);
                var board = session != null ? session.board : DeviceAutomation.Find(chosen);
                var card = Box("Remote " + chosen.ToString("X8"), gridContent,
                    (i % cols) * (tileW + 8), (i / cols) * (tileH + 8), tileW, tileH, Grey);
                var previewArea = Box("Live area", card, 4, 4, tileW - 8, tileH - 41, Color.black);
                previewArea.GetComponent<Image>().raycastTarget = false;
                if (session != null)
                {
                    var image = NewRect("Live view", previewArea, 0, 0, tileW - 8, tileH - 41)
                        .gameObject.AddComponent<RawImage>();
                    image.texture = session.frame;
                    image.raycastTarget = false;
                    var previewFit = image.gameObject.AddComponent<AspectRatioFitter>();
                    previewFit.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
                    previewFit.aspectRatio = (float)session.frame.width / session.frame.height;
                    var hit = card.gameObject.AddComponent<Button>();
                    hit.onClick.AddListener(() => { focusedId = chosen; RefreshLayout(); });
                    if (!premium)
                    {
                        var tileMark = Label("RemoteDesk FREE", card, 8, tileH / 2 - 20, tileW - 16, 40, 16);
                        tileMark.alignment = TextAnchor.MiddleCenter;
                        tileMark.color = new Color(1f, 1f, 1f, .5f);
                    }
                }
                else
                {
                    var os = board != null ? board.System as global::PC.Component.Software.OS.OperatingSystem : null;
                    string state = board == null ? "NOT FOUND" : !board.Running ? "OFFLINE" :
                        os != null && os.Ready && !premium && sessions.Count >= FreeLimit ? "FREE LIMIT" : "BOOTING";
                    var label = Label(state, previewArea, 8, 5, tileW - 24, 33, 15);
                    label.color = Color.white;
                    if (board != null && !board.Running)
                        Button("Wake", card, 8, tileH - 77, 68, 29, () => Wake(chosen));
                }
                Label(chosen.ToString("X8") + "   " +
                    (board == null ? "DEVICE" : DeviceAutomation.IsMiner(board) ? "MINER" : "PC"),
                    card, 5, tileH - 35, tileW - 46, 30, 14);
                Button("X", card, tileW - 35, tileH - 35, 30, 30, () => RemoveSaved(chosen));
            }
            gridContent.sizeDelta = new Vector2(0, Mathf.Ceil(savedIds.Count / (float)cols) * (tileH + 8));
            // Keep one non-interactive watermark instead of creating another
            // Text every time a tile or window size changes.
            focusWatermark.gameObject.SetActive(!premium && focused);
            var markRect = focusWatermark.rectTransform;
            markRect.anchorMin = new Vector2(0, 1);
            markRect.anchorMax = Vector2.one;
            markRect.sizeDelta = new Vector2(-20, 38);
            markRect.anchoredPosition = new Vector2(0, -10);
        }

        private static void Place(RectTransform rect, float x, float y, float w, float h)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(w, h);
        }

        private RectTransform NewRect(string name, Transform parent, float x, float y, float w, float h)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer));
            go.layer = 5;
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(w, h);
            return rect;
        }

        private RectTransform Box(string name, Transform parent, float x, float y, float w, float h, Color color)
        {
            var rect = NewRect(name, parent, x, y, w, h);
            rect.gameObject.AddComponent<Image>().color = color;
            return rect;
        }

        private Text Label(string value, Transform parent, float x, float y, float w, float h, int size)
        {
            var text = NewRect("Label", parent, x, y, w, h).gameObject.AddComponent<Text>();
            text.font = font;
            text.fontSize = size;
            text.color = Ink;
            text.text = value;
            text.alignment = TextAnchor.MiddleLeft;
            text.raycastTarget = false;
            return text;
        }

        private Button Button(string caption, Transform parent, float x, float y, float w, float h, UnityEngine.Events.UnityAction click)
        {
            var rect = Box(caption, parent, x, y, w, h, Grey);
            var button = rect.gameObject.AddComponent<Button>();
            button.onClick.AddListener(click);
            Label(caption, rect, 4, 0, w - 8, h, 13).alignment = TextAnchor.MiddleCenter;
            return button;
        }

        private InputField InputField(string hint, Transform parent, float x, float y, float w, float h)
        {
            var rect = Box("System ID", parent, x, y, w, h, Surface);
            var field = rect.gameObject.AddComponent<InputField>();
            field.textComponent = Label("", rect, 6, 0, w - 12, h, 14);
            field.placeholder = Label(hint, rect, 6, 0, w - 12, h, 13);
            field.onEndEdit.AddListener(value => { if (Input.GetKeyDown(KeyCode.Return)) ConnectEntered(); });
            return field;
        }
    }
}
