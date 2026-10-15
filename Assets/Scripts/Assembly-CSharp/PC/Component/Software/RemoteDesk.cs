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
        private const float ProFramesPerSecond = 60f;
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
        private Button uploadButton;
        private Text statusText;
        private bool promoShownThisLaunch;
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
        private Vector2 lastViewportSize;
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
            if (!premium && !promoShownThisLaunch)
            {
                promoShownThisLaunch = true;
                ShowPromotion();
            }
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

        private static int FreeConnections
        {
            get
            {
                int count = 0;
                foreach (var viewer in viewers)
                    if (viewer != null && viewer.isActiveAndEnabled && !viewer.premium)
                        count += viewer.sessions.Count;
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
            return HasProLicense(system);
        }

        internal static bool HasProLicense(global::PC.Component.Software.OS.OperatingSystem owner)
        {
            File license;
            return owner != null && owner.FileManager != null &&
                owner.FileManager.TryGetFile(0, LicensePath, out license) &&
                license != null && license.content == "RemoteDesk Pro v1";
        }

        private void ShowPromotion()
        {
            if (premium || system == null) return;
            promoShownThisLaunch = true;
            if (!system.TryOpenRemoteDeskOffer())
            {
                status = "Could not open the RemoteDesk Pro offer.";
                RefreshLayout();
            }
        }

        internal static bool TryPurchasePro(global::PC.Component.Software.OS.OperatingSystem owner,
            out string error)
        {
            error = null;
            var fm = owner != null ? owner.FileManager : null;
            if (fm == null)
            {
                error = "Не удалось открыть диск этой ОС";
                return false;
            }
            if (!HasProLicense(owner))
            {
                if (BitcoinManager.Bitcoin < 5f)
                {
                    error = "Недостаточно BTC";
                    return false;
                }
                var file = fm.Write(0, LicensePath, "RemoteDesk Pro v1");
                if (file == null || file.content != "RemoteDesk Pro v1")
                {
                    error = "Не удалось сохранить лицензию";
                    return false;
                }
                file.hidden = true;
                BitcoinManager.Bitcoin -= 5f;
                BitcoinManager.Save();
            }
            // The offer is a separate PCOS window. Update the already-open
            // RemoteDesk on THIS OS without requiring it to be relaunched.
            foreach (var viewer in viewers)
            {
                if (viewer == null || viewer.system != owner) continue;
                viewer.premium = true;
                viewer.status = "";
                foreach (var session in viewer.sessions) session.nextFrame = 0f;
                viewer.RefreshLayout();
            }
            return true;
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

        // The focused desktop has the viewport's aspect, not the attached
        // monitor's aspect. Only that stream needs a larger RT in Pro.
        private Vector2Int FrameSize(Canvas canvas, bool focused)
        {
            float aspect = 16f / 9f;
            if (focused && focusViewport != null && focusViewport.rect.height > 1f)
                aspect = Mathf.Max(.25f, focusViewport.rect.width / focusViewport.rect.height);
            else if (canvas != null && canvas.renderMode == RenderMode.WorldSpace)
            {
                var rect = canvas.transform as RectTransform;
                if (rect != null && rect.rect.height > 1f) aspect = rect.rect.width / rect.rect.height;
            }
            else if (canvas != null && canvas.worldCamera != null && canvas.worldCamera.targetTexture != null)
            {
                var source = canvas.worldCamera.targetTexture;
                aspect = (float)source.width / source.height;
            }
            aspect = Mathf.Clamp(aspect, .25f, 5f);
            int maxWidth = premium && focused ? Mathf.Clamp(Screen.width, 1280, 1920) : 640;
            int maxHeight = premium && focused ? Mathf.Clamp(Screen.height, 720, 2048) : 960;
            int width = Mathf.Max(128, Mathf.Min(maxWidth, Mathf.RoundToInt(maxHeight * aspect)));
            int height = Mathf.Max(128, Mathf.RoundToInt(width / aspect));
            return new Vector2Int(width, height);
        }

        private RenderTexture CreateFrame(Canvas canvas, bool focused = false)
        {
            var size = FrameSize(canvas, focused);
            var frame = new RenderTexture(size.x, size.y, 16, RenderTextureFormat.ARGB32);
            frame.filterMode = FilterMode.Bilinear;
            frame.Create();
            return frame;
        }

        private void ResizeStreams()
        {
            foreach (var session in sessions)
            {
                var wanted = FrameSize(session.canvas, session.id == focusedId);
                if (session.frame != null && session.frame.width == wanted.x &&
                    session.frame.height == wanted.y) continue;
                if (session.desktopIcon != null) session.desktopIcon.EndRemoteDrag();
                session.desktopIcon = null;
                session.pointer = null;
                session.pressed = null;
                session.dragged = null;
                session.dragging = false;
                var old = session.frame;
                session.frame = CreateFrame(session.canvas, session.id == focusedId);
                if (old != null) { old.Release(); Destroy(old); }
                session.nextFrame = 0f;
            }
        }

        private Vector2 FocusedDesktopSize()
        {
            var view = focusViewport != null ? focusViewport.rect.size : Vector2.zero;
            if (view.x < 1f || view.y < 1f) return new Vector2(800f, 500f);
            // This is CanvasScaler's Expand rule for PCOS's 800x500 reference:
            // the logical area grows along the wider screen dimension.
            float scale = Mathf.Min(view.x / 800f, view.y / 500f);
            return view / Mathf.Max(.0001f, scale);
        }

        private void ConnectEntered()
        {
            if (idInput != null) Connect(idInput.text);
            RefreshLayout();
        }

        private void Upload()
        {
            var chosen = sessions.Find(s => s.id == focusedId);
            if (chosen == null || chosen.os == null || !chosen.os.Ready || system == null) return;
            int destinationId = chosen.id;
            // Use the owning PCOS's existing picker, not the host phone/PC
            // filesystem. Capture the destination so focus changes while the
            // picker is open cannot silently send a file to another machine.
            system.SelectFile("*", source =>
            {
                if (this == null || system == null || source == null) return;
                if (source.isFolder)
                {
                    system.ShowMessageBox("Upload", "Select a file, not a folder.");
                    return;
                }
                var target = sessions.Find(s => s.id == destinationId);
                if (target == null || target.os == null || !target.os.Ready ||
                    target.board == null || target.board.System != target.os ||
                    target.os.FileManager == null)
                {
                    system.ShowMessageBox("Upload", "The remote PC is no longer connected.");
                    return;
                }
                string name = source.path ?? "";
                int slash = Mathf.Max(name.LastIndexOf('/'), name.LastIndexOf('\\'));
                if (slash >= 0) name = name.Substring(slash + 1);
                if (string.IsNullOrEmpty(name) || name == "." || name == "..")
                {
                    system.ShowMessageBox("Upload", "Invalid file name.");
                    return;
                }
                string content = source.content ?? "";
                // Create is non-destructive: Storage.AddFile chooses a unique
                // (1), (2), ... name when that desktop already has the file.
                var copy = new File(name, content, false,
                    source.size > 0 ? source.size : content.Length);
                if (!target.os.FileManager.Create(0, copy))
                {
                    system.ShowMessageBox("Upload", "Not enough space on the remote PC.");
                    return;
                }
                target.os.RefreshDesktopIcon();
                status = "Uploaded " + copy.path + " to " + destinationId.ToString("X8") + ".";
                if (statusText != null) UpdateStatus();
                target.nextFrame = 0f;
            });
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
            if (Vector2.Distance(size, lastSize) > 1f ||
                Vector2.Distance(focusViewport.rect.size, lastViewportSize) > 1f)
            { lastSize = size; RefreshLayout(); }
            if (lastTotal != (premium ? ActiveConnections : FreeConnections)) UpdateStatus();
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
                // Pro runs each stream up to 60 FPS. Free viewers still share
                // their 24 FPS budget; neither path throttles the Unity game.
                session.nextFrame = now + (premium ? 1f / ProFramesPerSecond :
                    Mathf.Max(1, FreeConnections) / SharedFramesPerSecond);
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
            captureCamera.orthographicSize = height * .5f;
            captureCamera.farClipPlane = depth + Mathf.Max(width, height) + 1f;
            // Monitor prefabs use non-uniform world scaling. Fit the complete
            // UI rectangle into the RT rather than letterboxing/cropping it.
            // Input raycasts use this same projection while the layout is lent.
            captureCamera.projectionMatrix = Matrix4x4.Ortho(-width * .5f, width * .5f,
                -height * .5f, height * .5f, captureCamera.nearClipPlane,
                captureCamera.farClipPlane);
        }

        // Borrow the canvas only for this synchronous operation. The physical
        // monitor returns to its original dimensions/camera before Unity draws
        // the next game frame. The OS object itself is never cloned or moved.
        private void OnRemoteCanvas(Session session, Action action)
        {
            if (capturing || session == null || session.canvas == null) return;
            capturing = true;
            var canvas = session.canvas;
            var root = canvas.transform as RectTransform;
            bool world = canvas.renderMode == RenderMode.WorldSpace;
            bool fit = session.id == focusedId && focusViewport != null &&
                focusViewport.rect.width > 1f && focusViewport.rect.height > 1f;
            var originalSize = root != null ? root.sizeDelta : Vector2.zero;
            var originalCamera = canvas.worldCamera;
            var originalTarget = originalCamera != null ? originalCamera.targetTexture : null;
            try
            {
                if (fit && world && root != null)
                {
                    root.sizeDelta = FocusedDesktopSize();
                    Canvas.ForceUpdateCanvases();
                }
                else if (fit && !world && originalCamera != null &&
                         canvas.renderMode == RenderMode.ScreenSpaceCamera)
                {
                    originalCamera.targetTexture = session.frame;
                    Canvas.ForceUpdateCanvases();
                }
                if (world)
                {
                    ConfigureCamera(session);
                    captureCamera.targetTexture = session.frame;
                    canvas.worldCamera = captureCamera; // raycaster only
                }
                action();
            }
            finally
            {
                if (world && canvas != null) canvas.worldCamera = originalCamera;
                if (captureCamera != null) captureCamera.targetTexture = null;
                if (root != null && fit && world) root.sizeDelta = originalSize;
                if (fit && !world && originalCamera != null)
                    originalCamera.targetTexture = originalTarget;
                if (fit) Canvas.ForceUpdateCanvases();
                capturing = false;
            }
        }

        private void Capture(Session session)
        {
            var canvas = session.canvas;
            bool fit = session.id == focusedId && focusViewport != null &&
                focusViewport.rect.width > 1f && focusViewport.rect.height > 1f;
            if (canvas.renderMode == RenderMode.WorldSpace)
            {
                var root = canvas.transform as RectTransform;
                Vector2 original = root != null ? root.sizeDelta : Vector2.zero;
                try
                {
                    if (fit && root != null) root.sizeDelta = FocusedDesktopSize();
                    Canvas.ForceUpdateCanvases();
                    ConfigureCamera(session);
                    captureCamera.targetTexture = session.frame;
                    captureCamera.Render();
                }
                finally
                {
                    if (captureCamera != null) captureCamera.targetTexture = null;
                    if (fit && root != null)
                    {
                        root.sizeDelta = original;
                        Canvas.ForceUpdateCanvases();
                    }
                }
            }
            else
            {
                // Textured monitors own the camera/RT. Render the focused
                // viewport into our RT, then redraw the original monitor RT.
                var displayCamera = canvas.worldCamera;
                var original = displayCamera != null ? displayCamera.targetTexture : null;
                if (original == null || displayCamera == captureCamera) return;
                if (fit && canvas.renderMode == RenderMode.ScreenSpaceCamera)
                {
                    try
                    {
                        displayCamera.targetTexture = session.frame;
                        Canvas.ForceUpdateCanvases();
                        displayCamera.Render();
                    }
                    finally
                    {
                        displayCamera.targetTexture = original;
                        Canvas.ForceUpdateCanvases();
                        displayCamera.Render();
                    }
                }
                else
                {
                    if (canvas.renderMode == RenderMode.ScreenSpaceCamera)
                        displayCamera.Render();
                    Graphics.Blit(original, session.frame);
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
            bool fitted = session.id == focusedId;
            int eventWidth = fitted || eventTexture == null ? session.frame.width : eventTexture.width;
            int eventHeight = fitted || eventTexture == null ? session.frame.height : eventTexture.height;
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
            proButton = Button("Купить", transform, 220, 8, 120, 30, ShowPromotion);
            uploadButton = Button("Upload", transform, 348, 8, 94, 30, Upload);
            panel = Box("RemoteDesk UI", transform, 8, 40, 604, 490, Color.white);
            panel.anchorMin = Vector2.zero;
            panel.anchorMax = Vector2.one;
            panel.pivot = new Vector2(.5f, .5f);
            panel.offsetMin = new Vector2(8f, 8f);
            panel.offsetMax = new Vector2(-8f, -40f);
            idInput = InputField("System ID (hex)", panel, 14, 8, 155, 33);
            connectButton = Button("Connect", panel, 175, 8, 94, 33, ConnectEntered);
            statusText = Label("", panel, 14, 49, 700, 30, 13);
            gridPage = Box("Grid page", panel, 14, 87, 576, 280, Surface).gameObject;
            focusPage = Box("Focus page", panel, 14, 87, 576, 280, Surface).gameObject;
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
            lastTotal = premium ? ActiveConnections : FreeConnections;
            if (statusText != null)
                statusText.text = status + "   Online: " + sessions.Count +
                    "   Stream FPS/device: " + (premium ? ProFramesPerSecond :
                        lastTotal > 0 ? SharedFramesPerSecond / lastTotal : 0f).ToString("0.#");
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
            idInput.gameObject.SetActive(!focused);
            connectButton.gameObject.SetActive(!focused);
            proButton.gameObject.SetActive(!focused && !premium);
            uploadButton.gameObject.SetActive(focused);
            statusText.gameObject.SetActive(!focused);
            backButton.gameObject.SetActive(focused);
            // Keep the native title and window controls in the full view.
            foreach (string chrome in new[] { "Close", "Maximize", "Minimize" })
            {
                var button = transform.Find(chrome);
                if (button != null) button.gameObject.SetActive(true);
            }
            var window = transform as RectTransform;
            float headerWidth = window != null && window.rect.width < 440f ? 78f : 120f;
            float headerX = window != null ?
                Mathf.Min(220f, Mathf.Max(135f, window.rect.width - headerWidth - 140f)) : 220f;
            Place(backButton.GetComponent<RectTransform>(), headerX, 8, headerWidth, 30);
            Place(proButton.GetComponent<RectTransform>(), headerX, 8, headerWidth, 30);
            bool uploadBelowTitle = window != null && window.rect.width < 580f;
            Place(uploadButton.GetComponent<RectTransform>(),
                uploadBelowTitle ? 22f : headerX + headerWidth + 8f,
                uploadBelowTitle ? 50f : 8f, 94f, 30f);
            if (focused && uploadBelowTitle) uploadButton.transform.SetAsLastSibling();
            float connectWidth = compact ? Mathf.Min(94, Mathf.Max(60, W * .28f)) : 94;
            float connectX = compact ? W - connectWidth - 14 : 175;
            Place(idInput.GetComponent<RectTransform>(), 14, 8,
                compact ? Mathf.Max(45, connectX - 20) : 155, 33);
            Place(connectButton.GetComponent<RectTransform>(), connectX, 8, connectWidth, 33);
            Place(statusText.rectTransform, 14, 49, width, 30);
            UpdateStatus();
            var gridRect = (RectTransform)gridPage.transform;
            var focusRect = (RectTransform)focusPage.transform;
            gridRect.anchorMin = Vector2.zero;
            gridRect.anchorMax = Vector2.one;
            gridRect.offsetMin = new Vector2(14, 14);
            gridRect.offsetMax = new Vector2(-14, -87);
            focusRect.anchorMin = Vector2.zero;
            focusRect.anchorMax = Vector2.one;
            focusRect.offsetMin = Vector2.zero;
            focusRect.offsetMax = focused && uploadBelowTitle ? new Vector2(0f, -44f) : Vector2.zero;
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
            Canvas.ForceUpdateCanvases();
            lastViewportSize = focusViewport.rect.size;
            ResizeStreams();
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
