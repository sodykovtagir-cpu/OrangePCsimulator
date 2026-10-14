using System;
using System.Collections.Generic;
using PC.Component.Software.OS;
using UnityEngine;
using UnityEngine.UI;

namespace PC.Component.Software
{
    /// <summary>Prefab-driven PC/miner manager. Only device rows are instantiated at runtime.</summary>
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

        [Header("Pages — edit their controls and layout in DeviceManager.prefab")]
        [SerializeField] private RectTransform body;
        [SerializeField] private GameObject homePage;
        [SerializeField] private GameObject targetPage;
        [SerializeField] private GameObject actionPage;
        [SerializeField] private GameObject exportPage;
        [Header("Home")]
        [SerializeField] private Button homeAll;
        [SerializeField] private Button homePCs;
        [SerializeField] private Button homeMiners;
        [SerializeField] private Button buildButton;
        [SerializeField] private Button selectVisibleButton;
        [SerializeField] private Button discoverButton;
        [SerializeField] private Button removeButton;
        [SerializeField] private Button refreshButton;
        [SerializeField] private Button addIdButton;
        [SerializeField] private InputField idInput;
        [SerializeField] private ScrollRect homeScroll;
        [SerializeField] private RectTransform homeRows;
        [SerializeField] private Text homeStatus;
        [Header("Script: devices")]
        [SerializeField] private Button targetAll;
        [SerializeField] private Button targetPCs;
        [SerializeField] private Button targetMiners;
        [SerializeField] private Button targetDiscover;
        [SerializeField] private Button targetSelectVisible;
        [SerializeField] private Button targetClear;
        [SerializeField] private Button targetBack;
        [SerializeField] private Button targetNext;
        [SerializeField] private Text targetCount;
        [SerializeField] private ScrollRect targetScroll;
        [SerializeField] private RectTransform targetRows;
        [Header("Script: action")]
        [SerializeField] private Button[] actionButtons;
        [SerializeField] private Toggle worldToggle;
        [SerializeField] private Text selectedAction;
        [SerializeField] private InputField argumentInput;
        [SerializeField] private Text actionStatus;
        [SerializeField] private Button actionBack;
        [SerializeField] private Button actionNext;
        [Header("Script: export")]
        [SerializeField] private InputField scriptNameInput;
        [SerializeField] private Toggle autorun;
        [SerializeField] private Text exportStatus;
        [SerializeField] private Button exportBack;
        [SerializeField] private Button exportButton;
        [Header("List appearance — edit the row template directly in the prefab")]
        [SerializeField] private DeviceManagerRow rowTemplate;
        [SerializeField] private Color selectedRowColor = new Color(.86f, .86f, .86f, 1f);
        [SerializeField] private Color alternateRowColor = new Color(.96f, .96f, .96f, 1f);
        [SerializeField] private Color activeTabColor = new Color(.96f, .96f, .96f, 1f);
        [SerializeField] private Color inactiveTabColor = new Color(.69f, .69f, .69f, 1f);

        private static readonly string[] Actions = { "startup", "bitcoin", "cash", "video", "app", "file" };
        private readonly List<int> registered = new List<int>();
        private readonly HashSet<int> minerIds = new HashSet<int>();
        private readonly HashSet<int> pcIds = new HashSet<int>();
        private readonly HashSet<int> selection = new HashSet<int>();
        private readonly HashSet<int> targets = new HashSet<int>();
        private DeviceKind filter;
        private int stage;
        private string action = "bitcoin";
        private string status = "";
        private bool bound;

        public int SelectedCount => selection.Count;
        public Color SelectedRowColor => selectedRowColor;
        public Color AlternateRowColor => alternateRowColor;

        public override void Open(string content)
        {
            base.Open(content);
            if (system == null || !system.RequireDevices()) return;
            var window = transform as RectTransform;
            var host = transform.parent as RectTransform;
            if (window != null && host != null && host.rect.width > 400 && host.rect.height > 310)
                window.sizeDelta = new Vector2(Mathf.Min(620, host.rect.width - 16),
                                               Mathf.Min(540, host.rect.height - 16));
            // The prefab keeps the PCOS title and window controls, but its
            // content pages and editable row template belong to this app.
            if (body == null || rowTemplate == null || homeRows == null || targetRows == null)
            {
                Debug.LogError("Device Manager: assign prefab UI references in the Inspector.", this);
                return;
            }
            Bind();
            LoadRegistry();
            selection.Clear();
            targets.Clear();
            filter = DeviceKind.All;
            stage = 0;
            status = "";
            Render();
        }

        private void Bind()
        {
            if (bound) return;
            bound = true;
            homeAll.onClick.AddListener(() => SetFilter(DeviceKind.All));
            homePCs.onClick.AddListener(() => SetFilter(DeviceKind.PCs));
            homeMiners.onClick.AddListener(() => SetFilter(DeviceKind.Miners));
            targetAll.onClick.AddListener(() => SetFilter(DeviceKind.All));
            targetPCs.onClick.AddListener(() => SetFilter(DeviceKind.PCs));
            targetMiners.onClick.AddListener(() => SetFilter(DeviceKind.Miners));
            buildButton.onClick.AddListener(() => { stage = 1; Render(); });
            selectVisibleButton.onClick.AddListener(SelectVisible);
            discoverButton.onClick.AddListener(Discover);
            targetDiscover.onClick.AddListener(Discover);
            targetSelectVisible.onClick.AddListener(SelectVisible);
            targetClear.onClick.AddListener(() => { targets.Clear(); Render(); });
            addIdButton.onClick.AddListener(AddId);
            removeButton.onClick.AddListener(RemoveSelected);
            refreshButton.onClick.AddListener(Render);
            targetBack.onClick.AddListener(() => { stage = 0; Render(); });
            targetNext.onClick.AddListener(() => { stage = 2; Render(); });
            actionBack.onClick.AddListener(() => { stage = 1; Render(); });
            actionNext.onClick.AddListener(() =>
            {
                if ((action == "app" || action == "file") && string.IsNullOrWhiteSpace(argumentInput.text))
                { status = "Enter app name or file path"; Render(); return; }
                stage = 3; Render();
            });
            exportBack.onClick.AddListener(() => { stage = 2; Render(); });
            exportButton.onClick.AddListener(Export);
            if (actionButtons != null)
                for (int i = 0; i < actionButtons.Length && i < Actions.Length; i++)
                {
                    int index = i;
                    if (actionButtons[i] != null)
                        actionButtons[i].onClick.AddListener(() => { action = Actions[index]; Render(); });
                }
            // Attach to the scroll root: the viewport's Mask may hide its own
            // Graphic from raycasts, but the root background always receives
            // blank-area right-clicks and long presses.
            var pane = homeScroll.GetComponent<DeviceManagerPane>();
            if (pane == null) pane = homeScroll.gameObject.AddComponent<DeviceManagerPane>();
            pane.Init(this);
        }

        private void LoadRegistry()
        {
            registered.Clear(); minerIds.Clear(); pcIds.Clear();
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

        private void RememberKind(Motherboard board)
        {
            if (board == null) return;
            minerIds.Remove(board.Id); pcIds.Remove(board.Id);
            if (DeviceAutomation.IsMiner(board)) minerIds.Add(board.Id);
            else pcIds.Add(board.Id);
        }

        private DeviceKind Kind(int id, Motherboard board)
        {
            if (board != null) return DeviceAutomation.IsMiner(board) ? DeviceKind.Miners : DeviceKind.PCs;
            if (minerIds.Contains(id)) return DeviceKind.Miners;
            if (pcIds.Contains(id)) return DeviceKind.PCs;
            return DeviceKind.All; // Old offline entries: do not guess their type.
        }

        private bool Visible(int id, Motherboard board) => filter == DeviceKind.All || Kind(id, board) == filter;

        private void SetFilter(DeviceKind value)
        {
            filter = value;
            if (homeScroll != null) homeScroll.verticalNormalizedPosition = 1f;
            if (targetScroll != null) targetScroll.verticalNormalizedPosition = 1f;
            Render();
        }

        private void SetTab(Button button, bool active)
        {
            if (button != null && button.targetGraphic != null)
                button.targetGraphic.color = active ? activeTabColor : inactiveTabColor;
        }

        private void Render()
        {
            if (body == null) return;
            homePage.SetActive(stage == 0);
            targetPage.SetActive(stage == 1);
            actionPage.SetActive(stage == 2);
            exportPage.SetActive(stage == 3);
            if (stage == 0)
            {
                SetTab(homeAll, filter == DeviceKind.All);
                SetTab(homePCs, filter == DeviceKind.PCs);
                SetTab(homeMiners, filter == DeviceKind.Miners);
                homeStatus.text = status + (selection.Count > 0 ? "   Selected: " + selection.Count : "");
                RenderRows(homeRows, homeScroll, false);
            }
            else if (stage == 1)
            {
                SetTab(targetAll, filter == DeviceKind.All);
                SetTab(targetPCs, filter == DeviceKind.PCs);
                SetTab(targetMiners, filter == DeviceKind.Miners);
                targetCount.text = "Targets: " + targets.Count;
                RenderRows(targetRows, targetScroll, true);
            }
            else if (stage == 2)
            {
                selectedAction.text = "Selected: " + action;
                actionStatus.text = status;
                bool world = action == "bitcoin" || action == "cash" || action == "video";
                worldToggle.gameObject.SetActive(world);
                for (int i = 0; actionButtons != null && i < actionButtons.Length && i < Actions.Length; i++)
                    SetTab(actionButtons[i], action == Actions[i]);
            }
            else exportStatus.text = status;
        }

        private void RenderRows(RectTransform parent, ScrollRect scroll, bool wizard)
        {
            float position = scroll != null ? scroll.verticalNormalizedPosition : 1f;
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                var row = parent.GetChild(i);
                row.gameObject.SetActive(false);
                row.SetParent(null, false);
                Destroy(row.gameObject);
            }
            float height = Mathf.Max(28f, ((RectTransform)rowTemplate.transform).rect.height);
            int index = 0;
            foreach (int id in registered)
            {
                var board = DeviceAutomation.Find(id);
                if (!Visible(id, board)) continue;
                var kind = Kind(id, board);
                string type = kind == DeviceKind.Miners ? "MINER" : kind == DeviceKind.PCs ? "PC" : "?";
                string name = board != null ? board.name : "Offline";
                var row = Instantiate(rowTemplate, parent);
                row.gameObject.SetActive(true);
                var rt = (RectTransform)row.transform;
                rt.anchorMin = new Vector2(0, 1);
                rt.anchorMax = Vector2.one;
                rt.pivot = new Vector2(.5f, 1);
                rt.sizeDelta = new Vector2(0, height);
                rt.anchoredPosition = new Vector2(0, -height * index);
                row.Init(this, id, type + "   " + id.ToString("X8") + "   " + name +
                    "   [" + (board != null && board.Running ? "ON" : "OFF") + "]",
                    wizard ? targets.Contains(id) : selection.Contains(id), index % 2 == 0);
                index++;
            }
            parent.sizeDelta = new Vector2(parent.sizeDelta.x, height * index);
            if (scroll != null) scroll.verticalNormalizedPosition = position;
        }

        public void ToggleRow(int id)
        {
            if (stage == 1)
            {
                if (!targets.Add(id)) targets.Remove(id);
            }
            else if (stage == 0)
            {
                if (!selection.Add(id)) selection.Remove(id);
            }
            Render();
        }

        public void ShowRowMenu(int id, Vector2 screenPos)
        {
            if (stage != 0) return;
            // Right-clicking a member of a multiselection keeps the whole set.
            if (!selection.Contains(id)) { selection.Clear(); selection.Add(id); Render(); }
            ShowQuickMenu(screenPos);
        }

        public void ShowQuickMenu(Vector2 screenPos)
        {
            if (stage != 0 || !gameObject.activeInHierarchy) return;
            var menu = DesktopContextMenu.For(this);
            if (menu != null) menu.ShowDeviceManagerMenu(this, screenPos);
        }

        public void ClearSelection() { selection.Clear(); Render(); }

        private void SelectVisible()
        {
            foreach (int id in registered)
                if (Visible(id, DeviceAutomation.Find(id)))
                {
                    if (stage == 1) targets.Add(id);
                    else selection.Add(id);
                }
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
                selection.Clear(); selection.Add(id);
                SaveRegistry();
                status = "Added " + id.ToString("X8");
            }
            Render();
        }

        public void RemoveSelected()
        {
            if (selection.Count == 0) { status = "Select devices first"; Render(); return; }
            foreach (int id in selection)
            {
                registered.Remove(id);
                minerIds.Remove(id); pcIds.Remove(id); targets.Remove(id);
            }
            selection.Clear();
            SaveRegistry();
            status = "Removed selected devices";
            Render();
        }

        public void RunSelected(string what)
        {
            if (selection.Count == 0) { status = "Select devices first"; Render(); return; }
            int count = 0;
            foreach (int id in selection)
                if (registered.Contains(id) && DeviceAutomation.Run(system, DeviceAutomation.Find(id), what)) count++;
            status = "Scheduled " + count + "/" + selection.Count + " selected devices: " + what;
            Render();
        }

        public void RunGlobal(string what)
        {
            // Deliberately ignore registry, visible tab and multiselection.
            int count = DeviceAutomation.RunAll(system, what);
            status = "Scheduled " + count + " world devices: " + what;
            Render();
        }

        private void Export()
        {
            if (system == null || system.FileManager == null) return;
            string name = scriptNameInput != null ? scriptNameInput.text.Trim() : "";
            if (name.EndsWith(".tmn", StringComparison.OrdinalIgnoreCase)) name = name.Substring(0, name.Length - 4);
            if (name.Length == 0 || name.IndexOfAny(new[] { '/', '\\', '\n', '\r', '"', ':' }) >= 0)
            { status = "Invalid script name"; Render(); return; }
            bool all = worldToggle != null && worldToggle.isOn &&
                (action == "bitcoin" || action == "cash" || action == "video");
            if (!all && targets.Count == 0)
            { status = "Select at least one device in step 1"; Render(); return; }
            string fileName = name + ".tmn";
            string bodyText = "// Built with Device Manager\n";
            if (all) bodyText += "remote-all " + action + "\n";
            else
            {
                foreach (int id in targets)
                {
                    bodyText += "remote " + id.ToString("X8") + " " + action;
                    if (action == "app" || action == "file")
                        bodyText += " \"" + argumentInput.text.Trim().Replace("\"", "") + "\"";
                    bodyText += "\n";
                }
            }
            var fm = system.FileManager;
            var file = fm.Write(0, fileName, bodyText);
            if (file == null || file.content != bodyText)
            { status = "Cannot write script"; Render(); return; }
            file.hidden = false;
            if (autorun != null && autorun.isOn && !AutorunConfig.AppendCommand(fm, "run \"" + fileName + "\""))
            { status = "Exported, but could not edit Autorun.conf"; Render(); return; }
            system.RefreshDesktopIcon();
            status = "Exported " + fileName + (autorun != null && autorun.isOn ? " (autorun)" : "");
            scriptNameInput.text = "";
            autorun.isOn = false;
            worldToggle.isOn = false;
            stage = 0;
            Render();
        }
    }
}
