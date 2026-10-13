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
        [Serializable] private class Saved { public List<int> ids = new List<int>(); }
        private const string RegistryPath = "System/manager_devices";
        private readonly List<int> registered = new List<int>();
        private readonly HashSet<int> targets = new HashSet<int>();
        private int currentId;
        private int stage;
        private string action = "bitcoin";
        private string status = "";
        private string argument = "";
        private RectTransform panel;
        private RectTransform listContent;
        private InputField idInput;
        private InputField argumentInput;
        private InputField scriptNameInput;
        private Toggle autorun;
        private Font font;
        private static readonly Color Background = new Color(.13f, .15f, .19f, 1f);
        private static readonly Color Control = new Color(.22f, .28f, .34f, 1f);

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
            if (window != null)
            {
                float scale = Mathf.Min(1f, (window.rect.width - 16) / 604f,
                    (window.rect.height - 48) / 490f);
                scale = Mathf.Max(.55f, scale);
                panel.localScale = Vector3.one * scale;
                panel.anchoredPosition = new Vector2((window.rect.width - 604f * scale) / 2f, -40f);
            }
            stage = 0;
            Render();
        }

        private void LoadRegistry()
        {
            registered.Clear();
            File file;
            if (system.FileManager == null || !system.FileManager.TryGetFile(0, RegistryPath, out file) || file == null) return;
            try
            {
                var saved = JsonUtility.FromJson<Saved>(file.content);
                if (saved != null && saved.ids != null)
                    foreach (int id in saved.ids) if (!registered.Contains(id)) registered.Add(id);
            }
            catch (Exception) { /* corrupt registry must not prevent OS login */ }
        }

        private void SaveRegistry()
        {
            if (system == null || system.FileManager == null) return;
            var file = system.FileManager.Write(0, RegistryPath,
                JsonUtility.ToJson(new Saved { ids = new List<int>(registered) }));
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
            idInput = argumentInput = scriptNameInput = null;
            autorun = null;
        }

        private void Render()
        {
            if (panel == null) return;
            ClearPanel();
            if (stage == 0) RenderHome();
            else if (stage == 1) RenderTargets();
            else if (stage == 2) RenderAction();
            else RenderExport();
        }

        private void RenderHome()
        {
            Label("Managed computers and miners", 14, 8, 400, 26, 18);
            Button("Build script", 14, 40, 132, 32, () => { stage = 1; Render(); });
            Button("All: Bitcoin", 152, 40, 133, 32, () => Preset("bitcoin"));
            Button("All: Money", 291, 40, 133, 32, () => Preset("cash"));
            Button("All: Video", 430, 40, 160, 32, () => Preset("video"));
            idInput = Input("System ID (hex)", 14, 80, 176, 30);
            Button("Add ID", 196, 80, 92, 30, AddId);
            Button("Discover", 294, 80, 95, 30, Discover);
            Button("Remove", 395, 80, 95, 30, RemoveSelected);
            Button("Refresh", 496, 80, 94, 30, Render);
            MakeList(118, 242);
            foreach (int id in registered)
            {
                int selectedId = id;
                var board = DeviceAutomation.Find(id);
                string name = board != null ? board.name : "Offline";
                string state = board != null && board.Running ? "ON" : "OFF";
                Row((currentId == id ? "> " : "  ") + id.ToString("X8") + "  " + name + "  [" + state + "]",
                    () => { currentId = selectedId; status = "Selected " + selectedId.ToString("X8"); Render(); });
            }
            Label(status, 14, 366, 576, 36, 13);
            Button("Power on", 14, 415, 103, 35, () => Act("startup"));
            Button("EZ: BTC", 123, 415, 103, 35, () => Act("bitcoin"));
            Button("EZ: $", 232, 415, 103, 35, () => Act("cash"));
            Button("Video Player", 341, 415, 120, 35, () => Act("video"));
            Button("Power off", 467, 415, 123, 35, () => Act("shutdown"));
        }

        private void Preset(string choice)
        {
            int count = DeviceAutomation.RunAll(system, choice);
            status = "Scheduled " + count + " world devices: " + choice;
            Render();
        }

        private void Discover()
        {
            foreach (var board in DeviceAutomation.All())
                if (board != null && !registered.Contains(board.Id)) registered.Add(board.Id);
            SaveRegistry();
            status = "Added all discoverable PCs / miners";
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
            else
            {
                if (!registered.Contains(id)) registered.Add(id);
                currentId = id;
                SaveRegistry();
                status = "Added " + id.ToString("X8");
            }
            Render();
        }

        private void RemoveSelected()
        {
            registered.Remove(currentId);
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
            Label("Build script  1/3 — select computers / miners", 14, 10, 575, 30, 18);
            Button("Discover all", 14, 53, 130, 32, DiscoverForWizard);
            Button("Select all", 151, 53, 120, 32, () => { foreach (int id in registered) targets.Add(id); Render(); });
            Button("Clear", 278, 53, 90, 32, () => { targets.Clear(); Render(); });
            Label("Targets: " + targets.Count, 380, 53, 205, 32, 14);
            MakeList(95, 315);
            foreach (int id in registered)
            {
                int target = id;
                var board = DeviceAutomation.Find(id);
                Row((targets.Contains(id) ? "[x] " : "[ ] ") + id.ToString("X8")
                    + "  " + (board != null ? board.name : "offline"),
                    () => { if (!targets.Add(target)) targets.Remove(target); Render(); });
            }
            Button("Back", 14, 425, 130, 35, () => { stage = 0; Render(); });
            Button("Next  >", 461, 425, 129, 35, () => { stage = 2; Render(); });
        }

        private void DiscoverForWizard()
        {
            Discover();
            stage = 1;
            Render();
        }

        private void RenderAction()
        {
            Label("Build script  2/3 — what to open / switch on", 14, 10, 575, 30, 18);
            Choice("Power on", "startup", 14, 55);
            Choice("EZ: Bitcoin", "bitcoin", 210, 55);
            Choice("EZ: Money", "cash", 406, 55);
            Choice("Video Player", "video", 14, 102);
            Choice("Open app", "app", 210, 102);
            Choice("Open file / Lua", "file", 406, 102);
            Label("Selected: " + action, 14, 164, 575, 35, 16);
            Label("For app or file, enter exact name / path:", 14, 210, 560, 26, 14);
            argumentInput = Input("App name or file path", 14, 241, 575, 36);
            argumentInput.text = argument;
            Label("Presets (all suitable devices in the world):", 14, 297, 570, 27, 14);
            Choice("All: Bitcoin", "all-bitcoin", 14, 335);
            Choice("All: Money", "all-cash", 210, 335);
            Choice("All: Video", "all-video", 406, 335);
            Button("Back", 14, 425, 130, 35, () => { argument = argumentInput.text; stage = 1; Render(); });
            Button("Next  >", 461, 425, 129, 35, () =>
            {
                argument = argumentInput.text.Trim();
                if ((action == "app" || action == "file") && argument.Length == 0)
                { status = "Enter app name or file path"; Render(); return; }
                stage = 3; Render();
            });
            Label(status, 14, 390, 570, 25, 12);
        }

        private void Choice(string caption, string value, float x, float y)
        {
            Button((action == value ? "* " : "") + caption, x, y, 182, 38,
                () => { argument = argumentInput != null ? argumentInput.text : argument; action = value; Render(); });
        }

        private void RenderExport()
        {
            Label("Build script  3/3 — name and export", 14, 10, 575, 30, 18);
            Label("Script name (.tmn)", 14, 63, 575, 30, 15);
            scriptNameInput = Input("My automation", 14, 98, 575, 36);
            autorun = Toggle("Run automatically when the OS starts", 14, 164);
            Label("Export makes a real .tmn file you can open or edit.\n"
                + "Auto-start adds 'run <name>.tmn' to System/Autorun.conf.", 14, 225, 565, 95, 14);
            Label(status, 14, 340, 565, 45, 14);
            Button("Back", 14, 425, 130, 35, () => { stage = 2; Render(); });
            Button("Export", 461, 425, 129, 35, Export);
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
            var rect = NewRect("Label", parent != null ? parent : panel, x, y, w, h);
            var result = rect.gameObject.AddComponent<Text>();
            result.font = font;
            result.fontSize = size;
            result.alignment = TextAnchor.MiddleLeft;
            result.horizontalOverflow = HorizontalWrapMode.Wrap;
            result.verticalOverflow = VerticalWrapMode.Truncate;
            result.color = Color.white;
            result.text = text;
            result.raycastTarget = false;
            return result;
        }

        private Button Button(string label, float x, float y, float w, float h, UnityEngine.Events.UnityAction click)
        {
            var rect = Box(label, panel, x, y, w, h, Control);
            var button = rect.gameObject.AddComponent<Button>();
            button.onClick.AddListener(click);
            Label(label, 7, 0, w - 14, h, 14, rect).alignment = TextAnchor.MiddleCenter;
            return button;
        }

        private InputField Input(string placeholder, float x, float y, float w, float h)
        {
            var rect = Box("Input", panel, x, y, w, h, new Color(.06f, .08f, .10f, 1f));
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
            var box = Box("Checkbox", panel, x, y, 28, 28, Control);
            var check = Box("Check", box, 6, 6, 16, 16, Color.green);
            var toggle = box.gameObject.AddComponent<Toggle>();
            toggle.targetGraphic = box.GetComponent<Image>();
            toggle.graphic = check.GetComponent<Image>();
            toggle.isOn = false;
            Label(caption, x + 38, y - 1, 500, 30, 15);
            return toggle;
        }

        private void MakeList(float y, float h)
        {
            var root = Box("Scroll list", panel, 14, y, 576, h, Control);
            var viewport = Box("Viewport", root, 0, 0, 576, h, Color.clear);
            viewport.gameObject.AddComponent<RectMask2D>();
            listContent = Box("Rows", viewport, 0, 0, 576, 0, Color.clear);
            var scroll = root.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = listContent;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.scrollSensitivity = 20;
        }

        private void Row(string label, UnityEngine.Events.UnityAction click)
        {
            int row = listContent.childCount;
            var rect = Box("Row", listContent, 0, row * 43, 572, 41,
                row % 2 == 0 ? new Color(.17f, .20f, .24f, 1f) : Background);
            var button = rect.gameObject.AddComponent<Button>();
            button.onClick.AddListener(click);
            Label(label, 8, 0, 553, 40, 13, rect);
            listContent.sizeDelta = new Vector2(576, (row + 1) * 43);
        }
    }
}
