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

        private const string LicensePath = "System/RemoteDesk.license";
        private const int FreeLimit = 5;
        private const float SharedFramesPerSecond = 24f;
        private static readonly List<RemoteDesk> viewers = new List<RemoteDesk>();
        // Only set while dispatching a synthetic event. Existing PCOS hit
        // tests must see the remote canvas, not the local game's UI overlay.
        internal static global::PC.Component.Software.OS.OperatingSystem ForwardingSystem { get; private set; }
        internal static GraphicRaycaster ForwardingRaycaster { get; private set; }
        private readonly List<Session> sessions = new List<Session>();
        private RectTransform panel;
        private RectTransform gridContent;
        private RectTransform gridViewport;
        private InputField idInput;
        private Button connectButton;
        private Button gridButton;
        private Button proButton;
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
            if (!viewers.Contains(this)) viewers.Add(this);
            if (!string.IsNullOrEmpty(content))
                foreach (string id in content.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries))
                    Connect(id);
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
            status = "RemoteDesk Pro activated on this PC.";
            RefreshLayout();
        }

        private void Connect(string input)
        {
            if (string.IsNullOrEmpty(input)) return;
            int id;
            if (!int.TryParse(input.Trim(), System.Globalization.NumberStyles.HexNumber,
                System.Globalization.CultureInfo.InvariantCulture, out id))
            { status = "Invalid hexadecimal system ID."; return; }
            if (id == 0) { status = "System ID 00000000 is not valid."; return; }
            if (sessions.Exists(s => s.id == id)) return;
            if (!premium && sessions.Count >= FreeLimit)
            { status = "Free edition: at most 5 connected devices. Upgrade for 5 BTC."; return; }
            var board = DeviceAutomation.Find(id);
            if (board == null || board == system.Board)
            { status = "Device not found or cannot connect to this PC itself."; return; }
            var remote = board.System as global::PC.Component.Software.OS.OperatingSystem;
            if (remote == null || !remote.Ready)
            { status = "Remote PCOS must be powered on and ready."; return; }
            var canvas = remote.RemoteCanvas;
            if (canvas == null)
            { status = "Remote device has no connected display."; return; }
            var raycaster = canvas.GetComponent<GraphicRaycaster>();
            if (raycaster == null)
            { status = "Remote display does not accept pointer input."; return; }
            var session = new Session {
                id = id, board = board, os = remote, canvas = canvas,
                raycaster = raycaster,
                frame = new RenderTexture(640, 360, 16, RenderTextureFormat.ARGB32)
            };
            session.frame.Create();
            sessions.Add(session);
            status = "Connected to " + id.ToString("X8") + ".";
            RefreshLayout();
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

        private void RemoveFocused()
        {
            var session = sessions.Find(s => s.id == focusedId);
            if (session != null) RemoveSession(session);
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
            if (sessions.Count == 0) return;
            float now = Time.unscaledTime;
            for (int i = sessions.Count - 1; i >= 0; i--)
            {
                var session = sessions[i];
                if (session.board == null || session.os == null || !session.os.Ready ||
                    session.canvas == null || session.board.System != session.os ||
                    session.os.RemoteCanvas != session.canvas)
                { RemoveSession(session); RefreshLayout(); continue; }
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
            go.transform.position = new Vector3(10000, 10000, 10000);
            go.transform.rotation = Quaternion.identity;
            captureCamera = go.AddComponent<Camera>();
            captureCamera.enabled = false;
            captureCamera.clearFlags = CameraClearFlags.SolidColor;
            captureCamera.backgroundColor = Color.black;
            captureCamera.cullingMask = 1 << 5; // Unity UI layer only.
            captureCamera.nearClipPlane = .05f;
            captureCamera.farClipPlane = 10f;
        }

        private void OnRemoteCanvas(Session session, Action action)
        {
            if (capturing || session == null || session.canvas == null) return;
            EnsureCamera();
            capturing = true;
            var canvas = session.canvas;
            var mode = canvas.renderMode;
            var camera = canvas.worldCamera;
            float distance = canvas.planeDistance;
            var root = canvas.transform as RectTransform;
            Vector2 rootSize = root != null ? root.sizeDelta : Vector2.zero;
            Vector2 rootPosition = root != null ? root.anchoredPosition : Vector2.zero;
            Vector2 rootAnchorMin = root != null ? root.anchorMin : Vector2.zero;
            Vector2 rootAnchorMax = root != null ? root.anchorMax : Vector2.zero;
            Vector2 rootPivot = root != null ? root.pivot : Vector2.zero;
            var texture = captureCamera.targetTexture;
            try
            {
                captureCamera.targetTexture = session.frame;
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = captureCamera;
                canvas.planeDistance = 1f;
                Canvas.ForceUpdateCanvases();
                action();
            }
            finally
            {
                canvas.renderMode = mode;
                canvas.worldCamera = camera;
                canvas.planeDistance = distance;
                if (root != null)
                {
                    root.anchorMin = rootAnchorMin;
                    root.anchorMax = rootAnchorMax;
                    root.pivot = rootPivot;
                    root.sizeDelta = rootSize;
                    root.anchoredPosition = rootPosition;
                }
                captureCamera.targetTexture = texture;
                Canvas.ForceUpdateCanvases();
                capturing = false;
            }
        }

        private void Capture(Session session)
        {
            OnRemoteCanvas(session, () => captureCamera.Render());
        }

        // Called by RemoteDeskSurface. Forward actual UGUI click/drag/scroll
        // events to the remote OS while its canvas is mapped to the preview RT.
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
            Vector2 pos = new Vector2(u * session.frame.width, v * session.frame.height);
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
            var t = title != null ? title.GetComponent<Text>() : null;
            font = t != null ? t.font : Resources.GetBuiltinResource<Font>("Arial.ttf");
            panel = Box("RemoteDesk UI", transform, 8, 40, 604, 490, Color.white);
            panel.anchorMin = Vector2.zero;
            panel.anchorMax = Vector2.one;
            panel.pivot = new Vector2(.5f, .5f);
            panel.offsetMin = new Vector2(8f, 8f);
            panel.offsetMax = new Vector2(-8f, -40f);
            heading = Label("Freeware edition", panel, 14, 4, 570, 32, 16);
            idInput = InputField("System ID (hex)", panel, 14, 43, 155, 33);
            connectButton = Button("Connect", panel, 175, 43, 94, 33, ConnectEntered);
            gridButton = Button("Grid", panel, 275, 43, 74, 33, () => { focusedId = 0; RefreshLayout(); });
            proButton = Button("Buy Pro - 5 BTC", panel, 355, 43, 160, 33, Purchase);
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
            var back = Button("Back to grid", focusRect, 0, 287, 136, 32,
                () => { focusedId = 0; RefreshLayout(); });
            var disconnect = Button("Disconnect", focusRect, 145, 287, 120, 32, RemoveFocused);
            foreach (var item in new[] { back, disconnect })
            {
                var rt = item.GetComponent<RectTransform>();
                rt.anchorMin = rt.anchorMax = Vector2.zero;
                rt.pivot = Vector2.zero;
                rt.anchoredPosition = new Vector2(item == back ? 0 : 145, 4);
            }
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
            float width = W - 28f;
            bool compact = W < 535f;
            heading.text = premium ? "Pro - unlimited, no watermark" :
                "Free - watermark and 5 connections";
            heading.rectTransform.sizeDelta = new Vector2(width, 32);
            proButton.gameObject.SetActive(!premium);
            float connectWidth = compact ? Mathf.Min(94, Mathf.Max(60, W * .28f)) : 94;
            float connectX = compact ? W - connectWidth - 14 : 175;
            Place(idInput.GetComponent<RectTransform>(), 14, 43,
                compact ? Mathf.Max(45, connectX - 20) : 155, 33);
            Place(connectButton.GetComponent<RectTransform>(), connectX, 43, connectWidth, 33);
            Place(gridButton.GetComponent<RectTransform>(), compact ? 14 : 275, compact ? 83 : 43, 74, 33);
            Place(proButton.GetComponent<RectTransform>(), compact ? 94 : 355, compact ? 83 : 43,
                compact ? Mathf.Max(70, W - 108) : 160, 33);
            Place(statusText.rectTransform, 14, compact ? 121 : 81, width, compact ? 38 : 28);
            UpdateStatus();
            var gridRect = (RectTransform)gridPage.transform;
            var focusRect = (RectTransform)focusPage.transform;
            foreach (var rt in new[] { gridRect, focusRect })
            {
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = new Vector2(14, 52);
                rt.offsetMax = new Vector2(-14, compact ? -166 : -115);
            }
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
            focusViewport.offsetMin = new Vector2(0, 42);
            focusViewport.offsetMax = Vector2.zero;
            focusPage.SetActive(focusedId != 0);
            gridPage.SetActive(focusedId == 0);
            var selected = sessions.Find(s => s.id == focusedId);
            focusImage.texture = selected != null ? selected.frame : null;
            var surface = focusImage.GetComponent<RemoteDeskSurface>();
            if (surface != null) surface.Init(this, focusedId);
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
            for (int i = 0; i < sessions.Count; i++)
            {
                var s = sessions[i];
                int chosen = s.id;
                var card = Box("Remote " + chosen.ToString("X8"), gridContent,
                    (i % cols) * (tileW + 8), (i / cols) * (tileH + 8), tileW, tileH, Grey);
                var image = NewRect("Live view", card, 4, 4, tileW - 8, tileH - 41).gameObject.AddComponent<RawImage>();
                image.texture = s.frame;
                image.raycastTarget = false;
                var hit = card.gameObject.AddComponent<Button>();
                hit.onClick.AddListener(() => { focusedId = chosen; RefreshLayout(); });
                Label(chosen.ToString("X8") + "   " + (DeviceAutomation.IsMiner(s.board) ? "MINER" : "PC"),
                    card, 5, tileH - 35, tileW - 10, 30, 14);
                if (!premium) Label("RemoteDesk FREE", card, 8, tileH / 2 - 20, tileW - 16, 40, 16)
                    .alignment = TextAnchor.MiddleCenter;
            }
            gridContent.sizeDelta = new Vector2(0, Mathf.Ceil(sessions.Count / (float)cols) * (tileH + 8));
            // Keep one non-interactive watermark instead of creating another
            // Text every time a tile or window size changes.
            focusWatermark.gameObject.SetActive(!premium && focusedId != 0);
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
