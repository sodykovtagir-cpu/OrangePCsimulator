using System;
using System.Collections.Generic;
using PC.Component;
using UnityEngine;
using UnityEngine.UI;

namespace PC.Component.Software
{
    /// <summary>PCOS processes and live hardware telemetry. Window chrome is authored
    /// in TaskManager.prefab, using the same title/drag/close controls as Viewer.</summary>
    public class TaskManager : App
    {
        private static readonly Color Ink = new Color(.12f, .18f, .19f);
        private static readonly Color Paper = new Color(.95f, .97f, .96f);
        private static readonly Color Mint = new Color(.63f, .83f, .82f);
        private static readonly Color Orange = new Color(.94f, .50f, .18f);
        private static readonly string[] Services = { "PCOS.exe", "Drivers.dll", "sys.dll", "shell.exe" };
        private bool ru;
        private GameObject processesPage;
        private GameObject performancePage;
        private RectTransform rows;
        private Text status;
        private Image processTab;
        private Image performanceTab;
        private Text[] stats;
        private Image[] bars;
        private PerformanceGraph[] graphs;
        private readonly List<Button> rowButtons = new List<Button>();
        private readonly List<string> names = new List<string>();
        private readonly List<App> apps = new List<App>();
        private string selected;
        private float nextRefresh;

        protected override bool ShowMenuBar => false;
        public override float Workload => .10f;

        protected override void Start()
        {
            base.Start();
            ru = string.Equals(Localization.GetLanguage(), "RU", StringComparison.OrdinalIgnoreCase);
            BuildWindow();
            Refresh();
        }

        private string L(string english, string russian) { return ru ? russian : english; }

        private static RectTransform Panel(Transform parent, string name, Vector2 min, Vector2 max,
            Vector2 low, Vector2 high, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.layer = 5;
            var tr = go.GetComponent<RectTransform>();
            tr.SetParent(parent, false);
            tr.anchorMin = min;
            tr.anchorMax = max;
            tr.offsetMin = low;
            tr.offsetMax = high;
            go.GetComponent<Image>().color = color;
            return tr;
        }

        private static Text Label(Transform parent, string value, int size, TextAnchor anchor,
            Vector2 min, Vector2 max, Vector2 low, Vector2 high, Color color)
        {
            var go = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            go.layer = 5;
            var tr = go.GetComponent<RectTransform>();
            tr.SetParent(parent, false);
            tr.anchorMin = min; tr.anchorMax = max;
            tr.offsetMin = low; tr.offsetMax = high;
            var text = go.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = size;
            text.color = color;
            text.alignment = anchor;
            text.text = value;
            text.raycastTarget = false;
            text.resizeTextForBestFit = false;
            return text;
        }

        private Button Action(Transform parent, string title, Vector2 min, Vector2 max,
            Vector2 low, Vector2 high, Action clicked)
        {
            var tr = Panel(parent, title, min, max, low, high, Mint);
            var btn = tr.gameObject.AddComponent<Button>();
            btn.targetGraphic = tr.GetComponent<Image>();
            btn.onClick.AddListener(() => clicked());
            Label(tr, title, 14, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one,
                Vector2.zero, Vector2.zero, Ink);
            return btn;
        }

        private void BuildWindow()
        {
            var frame = transform as RectTransform;
            var body = Panel(frame, "Task Manager body", Vector2.zero, Vector2.one,
                new Vector2(12, 12), new Vector2(-12, -48), Paper);
            var title = Label(body, L("SYSTEM MONITOR", "МОНИТОР СИСТЕМЫ"), 12,
                TextAnchor.MiddleLeft, new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(16, -30), new Vector2(-16, -4), Ink);
            title.fontStyle = FontStyle.Bold;
            var tabs = Panel(body, "Pages", new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(14, -67), new Vector2(-14, -34), Paper);
            processTab = Action(tabs, L("Processes", "Процессы"), new Vector2(0, 0), new Vector2(.48f, 1),
                Vector2.zero, new Vector2(-4, 0), () => ShowPage(true)).GetComponent<Image>();
            performanceTab = Action(tabs, L("Performance", "Производительность"), new Vector2(.48f, 0), Vector2.one,
                new Vector2(4, 0), Vector2.zero, () => ShowPage(false)).GetComponent<Image>();
            processesPage = Panel(body, "Processes page", Vector2.zero, Vector2.one,
                new Vector2(14, 14), new Vector2(-14, -77), Paper).gameObject;
            performancePage = Panel(body, "Performance page", Vector2.zero, Vector2.one,
                new Vector2(14, 14), new Vector2(-14, -77), Paper).gameObject;

            var proc = processesPage.transform;
            var heading = Panel(proc, "Process columns", new Vector2(0, 1), Vector2.one,
                new Vector2(0, -35), Vector2.zero, Mint);
            Columns(heading, L("Process", "Процесс"), "CPU", "GPU", "RAM", 13);
            var viewport = Panel(proc, "Process list", Vector2.zero, Vector2.one,
                new Vector2(0, 64), new Vector2(0, -38), Color.white);
            viewport.gameObject.AddComponent<RectMask2D>();
            rows = Panel(viewport, "Rows", new Vector2(0, 1), new Vector2(1, 1),
                Vector2.zero, Vector2.zero, Color.clear);
            rows.pivot = new Vector2(.5f, 1f);
            var scroller = viewport.gameObject.AddComponent<ScrollRect>();
            scroller.viewport = viewport;
            scroller.content = rows;
            scroller.horizontal = false;
            scroller.vertical = true;
            scroller.movementType = ScrollRect.MovementType.Clamped;
            scroller.scrollSensitivity = 22f;
            status = Label(proc, "", 12, TextAnchor.MiddleLeft, Vector2.zero,
                new Vector2(.70f, 0), new Vector2(6, 12), new Vector2(0, 58), Ink);
            var end = Action(proc, L("End task", "Снять задачу"),
                new Vector2(.72f, 0), new Vector2(1, 0),
                new Vector2(0, 13), new Vector2(-2, 52), () =>
                {
                    if (system == null || string.IsNullOrEmpty(selected)) return;
                    system.EndProcess(selected);
                    selected = null;
                    Refresh();
                });
            end.gameObject.name = "End task";

            var perf = performancePage.transform;
            stats = new Text[3];
            bars = new Image[3];
            graphs = new PerformanceGraph[3];
            var names = new[] { "CPU", "GPU", "RAM" };
            for (int i = 0; i < 3; i++)
            {
                float top = -i *  .327f;
                var card = Panel(perf, names[i] + " metrics",
                    new Vector2(0, 1 + top - .30f), new Vector2(1, 1 + top),
                    new Vector2(0, 1), new Vector2(0, -4), Color.white);
                var caption = Label(card, names[i], 18, TextAnchor.MiddleLeft,
                    new Vector2(0, .66f), new Vector2(.34f, 1),
                    new Vector2(14, 0), Vector2.zero, Ink);
                caption.fontStyle = FontStyle.Bold;
                stats[i] = Label(card, "", 13, TextAnchor.MiddleLeft,
                    new Vector2(.34f, .62f), Vector2.one, Vector2.zero,
                    new Vector2(-12, 0), Ink);
                var barBg = Panel(card, names[i] + " bar", new Vector2(0, .58f),
                    new Vector2(.31f, .64f), new Vector2(14, 0),
                    new Vector2(-8, 0), Paper);
                var bar = Panel(barBg, "Usage", Vector2.zero,
                    new Vector2(0, 1), Vector2.zero, Vector2.zero,
                    i == 0 ? Orange : Mint);
                bars[i] = bar.GetComponent<Image>();
                var chart = new GameObject(names[i] + " history graph", typeof(RectTransform),
                    typeof(CanvasRenderer), typeof(PerformanceGraph));
                chart.layer = 5;
                var chartRect = chart.GetComponent<RectTransform>();
                chartRect.SetParent(card, false);
                chartRect.anchorMin = new Vector2(0, .06f);
                chartRect.anchorMax = new Vector2(1, .55f);
                chartRect.offsetMin = new Vector2(14, 0);
                chartRect.offsetMax = new Vector2(-14, 0);
                graphs[i] = chart.GetComponent<PerformanceGraph>();
                graphs[i].raycastTarget = false;
                graphs[i].color = i == 0 ? Orange : i == 1
                    ? new Color(.19f, .61f, .58f) : new Color(.34f, .48f, .78f);
            }
            ShowPage(true);
        }

        private void ShowPage(bool processes)
        {
            processTab.color = processes ? Orange : Mint;
            performanceTab.color = processes ? Mint : Orange;
            processesPage.SetActive(processes);
            performancePage.SetActive(!processes);
            Refresh();
        }

        private static void Columns(Transform parent, string process, string cpu, string gpu, string ram, int font)
        {
            var titles = new[] { process, cpu, gpu, ram };
            float[] x = { 0f, .49f, .66f, .82f, 1f };
            for (int i = 0; i < 4; i++)
                Label(parent, titles[i], font, TextAnchor.MiddleLeft,
                    new Vector2(x[i], 0), new Vector2(x[i + 1], 1),
                    new Vector2(10, 0), Vector2.zero, Ink);
        }

        private void RebuildRows(List<App> current)
        {
            names.Clear();
            apps.Clear();
            foreach (var service in Services)
                if (service != "shell.exe" || system.ShellRunning) { names.Add(service); apps.Add(null); }
            foreach (var app in current) { names.Add(app.AppName + ".exe"); apps.Add(app); }
            foreach (Transform child in rows) Destroy(child.gameObject);
            rowButtons.Clear();
            rows.sizeDelta = new Vector2(0f, names.Count * 34f);
            for (int i = 0; i < names.Count; i++)
            {
                int index = i;
                var row = Panel(rows, names[i], new Vector2(0, 1), new Vector2(1, 1),
                    new Vector2(0, -34 * (i + 1)), new Vector2(0, -34 * i - 2),
                    i % 2 == 0 ? Paper : Color.white);
                var button = row.gameObject.AddComponent<Button>();
                button.targetGraphic = row.GetComponent<Image>();
                button.onClick.AddListener(() =>
                {
                    selected = names[index];
                    UpdateRows();
                });
                Columns(row, names[i], "", "", "", 13);
                rowButtons.Add(button);
            }
        }

        private void UpdateRows()
        {
            for (int i = 0; i < rowButtons.Count; i++)
            {
                var row = rowButtons[i];
                if (row == null) continue;
                var img = row.GetComponent<Image>();
                img.color = selected == names[i] ? new Color(1f, .85f, .70f) :
                    i % 2 == 0 ? Paper : Color.white;
                var labels = row.GetComponentsInChildren<Text>();
                if (labels.Length < 4) continue;
                var app = apps[i];
                if (app == null)
                {
                    labels[1].text = i == 0 ? (system.CpuLoad * 5f).ToString("0") + "%" : "1%";
                    labels[2].text = names[i] == "shell.exe" ? (system.GpuLoad * 3f).ToString("0") + "%" : "0%";
                    labels[3].text = i == 0 ? "180 MB" : "28 MB";
                }
                else
                {
                    labels[1].text = (system.ProcessCpuLoad(app) * 100f).ToString("0") + "%";
                    labels[2].text = (system.ProcessGpuLoad(app) * 100f).ToString("0") + "%";
                    labels[3].text = Mathf.Max(40, app.size) + " MB";
                }
            }
        }

        private void UpdatePerformance()
        {
            if (system == null || system.Board == null) return;
            float cpuTemp = 0f, clock = 0f, gpuTemp = 0f;
            int cpuCount = 0, gpuCount = 0;
            var cpus = system.Board.GetHardwares(HardwareType.CPU);
            if (cpus != null) foreach (var part in cpus)
                if (part is CPU cpu && cpu.Power && !cpu.Damaged)
                { cpuTemp = Mathf.Max(cpuTemp, cpu.temperature); clock += cpu.EffectiveFrequency; cpuCount++; }
            var gpus = system.Board.GetHardwares(HardwareType.GPU);
            if (gpus != null) foreach (var part in gpus)
                if (part is GPU gpu && gpu.Power && !gpu.Damaged)
                { gpuTemp = Mathf.Max(gpuTemp, gpu.Temperature); gpuCount++; }
            stats[0].text = (system.CpuLoad * 100f).ToString("0") + "%    " +
                cpuTemp.ToString("0") + "°C    " +
                (cpuCount > 0 ? clock / cpuCount : 0f).ToString("0.00") + " GHz";
            stats[1].text = gpuCount > 0 ? (system.GpuLoad * 100f).ToString("0") +
                "%    " + gpuTemp.ToString("0") + "°C" :
                L("Integrated / no discrete GPU", "Встроенная / без видеокарты");
            // RAM has no physical sensor component; display an explicitly
            // estimated case temperature, not an invented hardware reading.
            float ramTemp = system.RamTemperature;
            stats[2].text = (system.RamLoad * 100f).ToString("0") + "%    " +
                system.RamUsedMb + "/" + system.RamTotalMb + " MB    " +
                ramTemp.ToString("0") + "°C " + L("est.", "оценка");
            float[] usage = { system.CpuLoad, gpuCount > 0 ? system.GpuLoad : 0f, system.RamLoad };
            for (int i = 0; i < bars.Length; i++)
            {
                var bar = bars[i].rectTransform;
                bar.anchorMax = new Vector2(Mathf.Clamp01(usage[i]), 1f);
                bar.offsetMax = Vector2.zero;
                graphs[i].Sample(usage[i]);
            }
        }

        private void Refresh()
        {
            if (system == null || rows == null) return;
            var current = system.RunningProcesses();
            bool different = names.Count != current.Count + (system.ShellRunning ? 4 : 3);
            if (!different)
                for (int i = 0; i < current.Count; i++)
                    if (apps[i + (system.ShellRunning ? 4 : 3)] != current[i]) { different = true; break; }
            if (different) RebuildRows(current);
            if (status != null) status.text = L("Running: ", "Запущено: ") + names.Count +
                (selected != null ? "  •  " + selected : "");
            UpdateRows();
            UpdatePerformance();
        }

        private void Update()
        {
            if (Time.unscaledTime < nextRefresh) return;
            nextRefresh = Time.unscaledTime + .35f;
            Refresh();
        }
    }
}
