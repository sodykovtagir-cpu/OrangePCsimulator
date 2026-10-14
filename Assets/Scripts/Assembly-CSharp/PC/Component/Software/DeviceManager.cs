using System;
using System.Collections.Generic;
using PC.Component.Software.OS;
using UnityEngine;
using UnityEngine.UI;

namespace PC.Component.Software
{
    /// <summary>Separate PC/miner manager. Existing My Devices remains wireless-only.</summary>
    public class DeviceManager : App
    {
        [Serializable] private class Saved
        {
            public List<int> ids = new List<int>();
            public List<int> minerIds = new List<int>();
            public List<int> pcIds = new List<int>();
        }
        private enum DeviceKind { All, PCs, Miners }
        private const string RegistryPath = "System/manager_devices";
        private readonly List<int> registered = new List<int>();
        private readonly HashSet<int> minerIds = new HashSet<int>();
        private readonly HashSet<int> pcIds = new HashSet<int>();
        private DeviceKind filter;
        private string idText = "";
        private string scriptName = "";
        private bool autorunEnabled;
        private Vector2 lastPanelSize;
        private readonly HashSet<int> targets = new HashSet<int>();
        private int currentId;
        private int stage;
        private string action = "bitcoin";
        private string status = "";
        private string argument = "";
        private RectTransform panel;
        private RectTransform controlHost;
        private RectTransform listContent;
        private InputField idInput;
        private InputField argumentInput;
        private InputField scriptNameInput;
        private Toggle autorun;
        private Font font;
        // Match the white PCOS window and its grey controls / blue highlight.
        private static readonly Color Background = new Color(.96f, .97f, .98f, 1f);
        private static readonly Color Control = new Color(.89f, .91f, .93f, 1f);
        private static readonly Color Ink = new Color(.15f, .19f, .23f, 1f);
        private static readonly Color Accent = new Color(.4f, .74f, 1f, 1f);
        private float W => panel.rect.width;
        private float H => panel.rect.height;

        private void LateUpdate()
        {
            if (panel == null || !panel.gameObject.activeInHierarchy) return;
            Vector2 size = panel.rect.size;
            if (Mathf.Abs(size.x - lastPanelSize.x) < 1f && Mathf.Abs(size.y - lastPanelSize.y) < 1f) return;
            lastPanelSize = size;
            Render(); // Maximize/restore and host resize keep the list and actions usable.
        }

        public override void Open(string content)
        {
            base.Open(content);
            if (system == null || !system.RequireDevices()) return;
            var window = transform as RectTransform;
            var host = transform.parent as RectTransform;
            if (window != null && host != null && host.rect.width > 400 && host.rect.height > 310)
                window.sizeDelta = new Vector2(Mathf.Min(620, host.rect.width - 16),
                                               Mathf.Min(540, host.rect.height - 16));
            LoadRegistry();
            // This prefab borrows only the window chrome from My Devices.
            // Its old wireless content is deliberately hidden, not modified.
            var old = transform.Find("Scroll View");
            if (old != null) old.gameObject.SetActive(false);
            old = transform.Find("Refresh");
            if (old != null) old.gameObject.SetActive(false);
            if (panel == null)
            {
                var title = transform.Find("Title");
                var text = title != null ? title.GetComponent<Text>() : null;
                font = text != null ? text.font : Resources.GetBuiltinResource<Font>("Arial.ttf");
                panel = Box("Manager UI", transform, 8, 40, 604, 490, Background);
            }
            // Stretch under the prefab's title bar, not a scaled 604 x 490 island.
            // Anchors track the existing PCOS Maximize/restore implementation.
            panel.anchorMin = Vector2.zero;
            panel.anchorMax = Vector2.one;
            panel.pivot = new Vector2(.5f, .5f);
            panel.offsetMin = new Vector2(8f, 8f);
            panel.offsetMax = new Vector2(-8f, -40f);
            panel.localScale = Vector3.one;
            lastPanelSize = panel.rect.size;
            filter = DeviceKind.All;
            stage = 0;
            Render();
        }

        private void LoadRegistry()
        {
            registered.Clear();
            minerIds.Clear();
            pcIds.Clear();
            File file;
            if (system.FileManager == null || !system.FileManager.TryGetFile(0, RegistryPath, out file) || file == null) return;
            try
            {
                var saved = JsonUtility.FromJson<Saved>(file.content);
                if (saved != null && saved.ids != null)
                    foreach (int id in saved.ids) if (!registered.Contains(id)) registered.Add(id);
                if (saved != null && saved.minerIds != null)
                    foreach (int id in saved.minerIds) if (registered.Contains(id)) minerIds.Add(id);
                if (saved != null && saved.pcIds != null)
                    foreach (int id in saved.pcIds) if (registered.Contains(id)) pcIds.Add(id);
            }
            catch (Exception) { /* corrupt registry must not prevent OS login */ }
        }

        private void SaveRegistry()
        {
            if (system == null || system.FileManager == null) return;
            var file = system.FileManager.Write(0, RegistryPath,
                JsonUtility.ToJson(new Saved { ids = new List<int>(registered),
                    minerIds = new List<int>(minerIds), pcIds = new List<int>(pcIds) }));
            if (file != null) file.hidden = true;
        }

        private void ClearPanel()
        {
            for (int i = panel.childCount - 1; i >= 0; --i)
            {
                var child = panel.GetChild(i);
                child.gameObject.SetActive(false);
                child.SetParent(null, false); // Destroy is deferred; rows must start at 0 now.
                Destroy(child.gameObject);
            }
            listContent = null;
            controlHost = panel;
            idInput = argumentInput = scriptNameInput = null;
            autorun = null;
        }

        private void Render()
        {
            if (panel == null) return;
            if (idInput != null) idText = idInput.text;
            if (argumentInput != null) argument = argumentInput.text;
            if (scriptNameInput != null) scriptName = scriptNameInput.text;
            if (autorun != null) autorunEnabled = autorun.isOn;
            ClearPanel();
            if (stage == 0) RenderHome();
            else if (stage == 1) RenderTargets();
            else if (stage == 2) RenderAction();
            else RenderExport();
        }

        private void RenderFilters(float y)
        {
            float tab = (W - 40f) / 3f;
            Button("All devices", 14, y, tab, 30, () => SetFilter(DeviceKind.All), filter == DeviceKind.All);
            Button("PCs", 20 + tab, y, tab, 30, () => SetFilter(DeviceKind.PCs), filter == DeviceKind.PCs);
            Button("Miners", 26 + 2 * tab, y, tab, 30, () => SetFilter(DeviceKind.Miners), filter == DeviceKind.Miners);
        }

        private void SetFilter(DeviceKind value)
        {
            filter = value;
            if (!Visible(currentId, DeviceAutomation.Find(currentId))) currentId = 0;
            Render();
        }

        private void RememberKind(Motherboard board)
        {
            if (board == null) return;
            minerIds.Remove(board.Id);
            pcIds.Remove(board.Id);
            if (DeviceAutomation.IsMiner(board)) minerIds.Add(board.Id);
            else pcIds.Add(board.Id);
        }

        private DeviceKind Kind(int id, Motherboard board)
        {
            if (board != null) return DeviceAutomation.IsMiner(board) ? DeviceKind.Miners : DeviceKind.PCs;
            if (minerIds.Contains(id)) return DeviceKind.Miners;
            if (pcIds.Contains(id)) return DeviceKind.PCs;
            return DeviceKind.All; // Old offline entries: unknown, do not guess.
        }

        private bool Visible(int id, Motherboard board)
        {
            return filter == DeviceKind.All || Kind(id, board) == filter;
        }

        private void RenderHome()
        {
            Label("Device Manager", 14, 5, W - 28, 28, 18);
            RenderFilters(37);
            float four = (W - 46f) / 4f;
            Button("Build script", 14, 74, four, 31, () => { stage = 1; Render(); }, true);
            Button("All: Bitcoin", 20 + four, 74, four, 31, () => Preset("bitcoin"));
            Button("All: Money", 26 + four * 2, 74, four, 31, () => Preset("cash"));
            Button("All: Video", 32 + four * 3, 74, four, 31, () => Preset("video"));
            float inputWidth = (W - 28) * .30f;
            float small = (W - 28 - inputWidth - 24) / 4f;
            idInput = Input("System ID (hex)", 14, 113, inputWidth, 31);
            idInput.text = idText;
            Button("Add ID", 20 + inputWidth, 113, small, 31, AddId);
            Button("Discover", 26 + inputWidth + small, 113, small, 31, Discover);
            Button("Remove", 32 + inputWidth + small * 2, 113, small, 31, RemoveSelected);
            Button("Refresh", 38 + inputWidth + small * 3, 113, small, 31, Render);
            MakeList(152, Mathf.Max(45f, H - 250f), W - 28f);
            foreach (int id in registered)
            {
                int selectedId = id;
                var board = DeviceAutomation.Find(id);
                if (!Visible(id, board)) continue;
                string type = Kind(id, board) == DeviceKind.Miners ? "MINER" :
                    Kind(id, board) == DeviceKind.PCs ? "PC" : "?";
                string name = board != null ? board.name : "Offline";
                string state = board != null && board.Running ? "ON" : "OFF";
                Row(type + "   " + id.ToString("X8") + "   " + name + "   [" + state + "]",
                    () => { currentId = selectedId; status = "Selected " + selectedId.ToString("X8"); Render(); },
                    currentId == id);
            }
            Label(status, 14, H - 91, W - 28, 28, 13);
            float five = (W - 52f) / 5f;
            Button("Power on", 14, H - 49, five, 35, () => Act("startup"));
            Button("EZ: BTC", 20 + five, H - 49, five, 35, () => Act("bitcoin"));
            Button("EZ: $", 26 + five * 2, H - 49, five, 35, () => Act("cash"));
            Button("Video Player", 32 + five * 3, H - 49, five, 35, () => Act("video"));
            Button("Power off", 38 + five * 4, H - 49, five, 35, () => Act("shutdown"));
        }

        private void Preset(string choice)
        {
            int count = DeviceAutomation.RunAll(system, choice);
            status = "Scheduled " + count + " world devices: " + choice;
            Render();
        }

        private void Discover()
        {
            int added = 0;
            foreach (var board in DeviceAutomation.All())
            {
                if (board == null || !Visible(board.Id, board)) continue;
                if (!registered.Contains(board.Id)) { registered.Add(board.Id); added++; }
                RememberKind(board);
            }
            SaveRegistry();
            status = "Added " + added + " " + filter + " from this world";
            Render();
        }

        private void AddId()
        {
            string value = idInput != null ? idInput.text.Trim() : "";
            int id;
            if (!int.TryParse(value, System.Globalization.NumberStyles.HexNumber,
                    System.Globalization.CultureInfo.InvariantCulture, out id)
                || DeviceAutomation.Find(id) == null)
                status = "System ID not found in this world";
            else if (!Visible(id, DeviceAutomation.Find(id)))
                status = "This ID belongs to another device category";
            else
            {
                if (!registered.Contains(id)) registered.Add(id);
                RememberKind(DeviceAutomation.Find(id));
                currentId = id;
                SaveRegistry();
                status = "Added " + id.ToString("X8");
            }
            Render();
        }

        private void RemoveSelected()
        {
            registered.Remove(currentId);
            minerIds.Remove(currentId);
            pcIds.Remove(currentId);
            targets.Remove(currentId);
            currentId = 0;
            SaveRegistry();
            Render();
        }

        private void Act(string what)
        {
            bool ok = DeviceAutomation.Run(system, DeviceAutomation.Find(currentId), what);
            status = ok ? "Scheduled " + what + " on " + currentId.ToString("X8") : "Select an available device";
            Render();
        }

        private void RenderTargets()
        {
            Label("Build script  1/3 — select computers / miners", 14, 5, W - 28, 30, 18);
            RenderFilters(42);
            float control = (W - 46f) / 3.6f;
            Button("Discover", 14, 83, control, 31, DiscoverForWizard);
            Button("Select visible", 20 + control, 83, control, 31, () =>
            {
                foreach (int id in registered)
                    if (Visible(id, DeviceAutomation.Find(id))) targets.Add(id);
                Render();
            });
            Button("Clear", 26 + 2 * control, 83, control * .6f, 31, () => { targets.Clear(); Render(); });
            Label("Targets: " + targets.Count, 32 + control * 2.6f, 83,
                W - 46 - control * 2.6f, 31, 14);
            MakeList(123, Mathf.Max(50f, H - 181f), W - 28f);
            foreach (int id in registered)
            {
                int target = id;
                var board = DeviceAutomation.Find(id);
                if (!Visible(id, board)) continue;
                string type = Kind(id, board) == DeviceKind.Miners ? "MINER" :
                    Kind(id, board) == DeviceKind.PCs ? "PC" : "?";
                Row((targets.Contains(id) ? "[x] " : "[ ] ") + type + "   " + id.ToString("X8")
                    + "   " + (board != null ? board.name : "offline"),
                    () => { if (!targets.Add(target)) targets.Remove(target); Render(); }, targets.Contains(id));
            }
            Button("Back", 14, H - 47, 130, 35, () => { stage = 0; Render(); });
            Button("Next  >", W - 144, H - 47, 130, 35, () => { stage = 2; Render(); }, true);
        }

        private void DiscoverForWizard()
        {
            Discover(); // Discovery respects the same PC/miner tab; never limits global presets.
        }

        private void RenderAction()
        {
            Label("Build script  2/3 — what to open / switch on", 14, 5, W - 28, 30, 18);
            MakeFormScroll(39, 322);
            float third = (W - 40f) / 3f;
            Choice("Power on", "startup", 14, 8, third);
            Choice("EZ: Bitcoin", "bitcoin", 20 + third, 8, third);
            Choice("EZ: Money", "cash", 26 + 2 * third, 8, third);
            Choice("Video Player", "video", 14, 49, third);
            Choice("Open app", "app", 20 + third, 49, third);
            Choice("Open file / Lua", "file", 26 + 2 * third, 49, third);
            Label("Selected: " + action, 14, 94, W - 28, 27, 16);
            Label("For app or file, enter exact name / path:", 14, 128, W - 28, 26, 14);
            argumentInput = Input("App name or file path", 14, 159, W - 28, 34);
            argumentInput.text = argument;
            Label("Presets (all suitable devices in the world):", 14, 202, W - 28, 26, 14);
            Choice("All: Bitcoin", "all-bitcoin", 14, 233, third);
            Choice("All: Money", "all-cash", 20 + third, 233, third);
            Choice("All: Video", "all-video", 26 + 2 * third, 233, third);
            controlHost = panel;
            Label(status, 14, H - 89, W - 28, 26, 12);
            Button("Back", 14, H - 47, 130, 35, () => { stage = 1; Render(); });
            Button("Next  >", W - 144, H - 47, 130, 35, () =>
            {
                argument = argumentInput.text.Trim();
                if ((action == "app" || action == "file") && argument.Length == 0)
                { status = "Enter app name or file path"; Render(); return; }
                stage = 3; Render();
            }, true);
        }

        private void Choice(string caption, string value, float x, float y, float width)
        {
            Button(caption, x, y, width, 34,
                () => { argument = argumentInput != null ? argumentInput.text : argument; action = value; Render(); },
                action == value);
        }

        private void RenderExport()
        {
            Label("Build script  3/3 — name and export", 14, 5, W - 28, 30, 18);
            MakeFormScroll(39, 290);
            Label("Script name (.tmn)", 14, 20, W - 28, 30, 15);
            scriptNameInput = Input("My automation", 14, 55, W - 28, 36);
            scriptNameInput.text = scriptName;
            autorun = Toggle("Run automatically when the OS starts", 14, 116);
            autorun.isOn = autorunEnabled;
            Label("Export makes a real .tmn file you can open or edit.\n"
                + "Auto-start adds 'run <name>.tmn' to System/Autorun.conf.", 14, 171, W - 28, 72, 14);
            controlHost = panel;
            Label(status, 14, H - 92, W - 28, 30, 14);
            Button("Back", 14, H - 47, 130, 35, () => { stage = 2; Render(); });
            Button("Export", W - 144, H - 47, 130, 35, Export, true);
        }

        private void Export()
        {
            if (system == null || system.FileManager == null) return;
            string name = scriptNameInput != null ? scriptNameInput.text.Trim() : "";
            if (name.EndsWith(".tmn", StringComparison.OrdinalIgnoreCase)) name = name.Substring(0, name.Length - 4);
            if (name.Length == 0 || name.IndexOfAny(new[] { '/', '\\', '\n', '\r', '"', ':' }) >= 0)
            { status = "Invalid script name"; Render(); return; }
            if (!action.StartsWith("all-") && targets.Count == 0)
            { status = "Select at least one device in step 1"; Render(); return; }
            string fileName = name + ".tmn";
            string body = "// Built with Device Manager\n";
            if (action.StartsWith("all-")) body += "remote-all " + action.Substring(4) + "\n";
            else
            {
                foreach (int id in targets)
                {
                    body += "remote " + id.ToString("X8") + " " + action;
                    if (action == "app" || action == "file")
                        body += " \"" + argument.Replace("\"", "") + "\"";
                    body += "\n";
                }
            }
            var fm = system.FileManager;
            var file = fm.Write(0, fileName, body);
            if (file == null || file.content != body)
            { status = "Cannot write script"; Render(); return; }
            file.hidden = false;
            if (autorun != null && autorun.isOn && !AutorunConfig.AppendCommand(fm, "run \"" + fileName + "\""))
            { status = "Exported, but could not edit Autorun.conf"; Render(); return; }
            system.RefreshDesktopIcon();
            status = "Exported " + fileName + (autorun != null && autorun.isOn ? " (autorun)" : "");
            stage = 0;
            Render();
            scriptName = "";
            autorunEnabled = false;
        }

        // Small Unity UI primitives: prefab carries window chrome; all controls
        // are runtime-generated so there are no missing serialized callbacks.
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

        private Text Label(string text, float x, float y, float w, float h, int size, Transform parent = null)
        {
            // UI Text and Image are both Graphic components: they must be on
            // separate GameObjects. A label needs only a RectTransform + Text.
            var rect = NewRect("Label", parent != null ? parent : controlHost, x, y, w, h);
            var result = rect.gameObject.AddComponent<Text>();
            result.font = font;
            result.fontSize = size;
            result.alignment = TextAnchor.MiddleLeft;
            result.horizontalOverflow = HorizontalWrapMode.Wrap;
            result.verticalOverflow = VerticalWrapMode.Truncate;
            result.color = Ink;
            result.text = text;
            result.raycastTarget = false;
            return result;
        }

        private Button Button(string label, float x, float y, float w, float h,
            UnityEngine.Events.UnityAction click, bool highlighted = false)
        {
            var rect = Box(label, controlHost, x, y, w, h, highlighted ? Accent : Control);
            var button = rect.gameObject.AddComponent<Button>();
            button.onClick.AddListener(click);
            Label(label, 4, 0, w - 8, h, w < 75 ? 12 : 14, rect).alignment = TextAnchor.MiddleCenter;
            return button;
        }

        private InputField Input(string placeholder, float x, float y, float w, float h)
        {
            var rect = Box("Input", controlHost, x, y, w, h, Color.white);
            var input = rect.gameObject.AddComponent<InputField>();
            var text = Label("", 7, 0, w - 14, h, 15, rect);
            var hint = Label(placeholder, 7, 0, w - 14, h, 14, rect);
            hint.color = new Color(.55f, .55f, .55f);
            input.textComponent = text;
            input.placeholder = hint;
            return input;
        }

        private Toggle Toggle(string caption, float x, float y)
        {
            var box = Box("Checkbox", controlHost, x, y, 28, 28, Control);
            var check = Box("Check", box, 6, 6, 16, 16, Color.green);
            var toggle = box.gameObject.AddComponent<Toggle>();
            toggle.targetGraphic = box.GetComponent<Image>();
            toggle.graphic = check.GetComponent<Image>();
            toggle.isOn = false;
            Label(caption, x + 38, y - 1, W - x - 52, 30, 15);
            return toggle;
        }

        // Steps 2/3 stay scrollable when the PCOS window is restored on a
        // short display; their navigation buttons remain pinned to the bottom.
        private void MakeFormScroll(float y, float contentHeight)
        {
            float height = Mathf.Max(40f, H - y - 103f);
            var root = Box("Form scroll", panel, 0, y, W, height, Color.white);
            var viewport = Box("Form viewport", root, 0, 0, W, height, Color.clear);
            viewport.gameObject.AddComponent<RectMask2D>();
            controlHost = NewRect("Form content", viewport, 0, 0, W, contentHeight);
            var scroll = root.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = controlHost;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.scrollSensitivity = 20;
        }

        private float listWidth;
        private void MakeList(float y, float h, float width)
        {
            listWidth = width;
            var root = Box("Scroll list", panel, 14, y, width, h, Color.white);
            var viewport = Box("Viewport", root, 0, 0, width, h, Color.clear);
            viewport.gameObject.AddComponent<RectMask2D>();
            listContent = Box("Rows", viewport, 0, 0, width, 0, Color.clear);
            var scroll = root.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = listContent;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.scrollSensitivity = 20;
        }

        private void Row(string label, UnityEngine.Events.UnityAction click, bool selected = false)
        {
            int row = listContent.childCount;
            var rect = Box("Row", listContent, 0, row * 43, listWidth - 4, 41,
                selected ? new Color(.79f, .89f, .98f, 1f) :
                row % 2 == 0 ? new Color(.94f, .95f, .97f, 1f) : Color.white);
            var button = rect.gameObject.AddComponent<Button>();
            button.onClick.AddListener(click);
            Label(label, 8, 0, listWidth - 24, 40, 13, rect);
            listContent.sizeDelta = new Vector2(listWidth, (row + 1) * 43);
        }
    }
}
