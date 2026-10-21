using System;
using System.Collections.Generic;
using PC.Component;
using UnityEngine;
using UnityEngine.UI;

namespace PC.Component.Software
{
    /// <summary>PCOS process list and telemetry. All visible controls, pages,
    /// chart colors, fonts, masks and row layout live in TaskManager.prefab.
    /// Only live process rows are instantiated from its editable row template.</summary>
    public class TaskManager : App
    {
        private static readonly string[] Services = { "PCOS.exe", "Drivers.dll", "sys.dll", "shell.exe" };

        [Header("Navigation — edit layout and button masks in the prefab")]
        [SerializeField] private Button processesTab;
        [SerializeField] private Button performanceTab;
        [SerializeField] private GameObject processesIndicator;
        [SerializeField] private GameObject performanceIndicator;
        [SerializeField] private Text processesTabLabel;
        [SerializeField] private Text performanceTabLabel;
        [SerializeField] private GameObject processesPage;
        [SerializeField] private GameObject performancePage;

        [Header("Processes — list and row template are prefab children")]
        [SerializeField] private RectTransform rows;
        [SerializeField] private TaskManagerRow rowTemplate;
        [SerializeField] private Button endButton;
        [SerializeField] private Text endButtonLabel;
        [SerializeField] private Text processHeader;
        [SerializeField] private Text processesHeading;
        [SerializeField] private Text status;

        [Header("Performance — CPU, GPU, RAM (in this order)")]
        [SerializeField] private Text[] stats;
        [SerializeField] private Image[] bars;
        [SerializeField] private PerformanceGraph[] graphs;
        [SerializeField] private RectTransform gpuCard;
        [SerializeField] private RectTransform ramCard;
        private Vector2 ramDefaultMax;
        private Vector2 ramDefaultOffsetMax;

        private readonly List<TaskManagerRow> liveRows = new List<TaskManagerRow>();
        private readonly List<string> names = new List<string>();
        private readonly List<App> apps = new List<App>();
        private string selected;
        private bool ru;
        private float nextRefresh;

        protected override bool ShowMenuBar => false;
        public override float Workload => .10f;

        protected override void Start()
        {
            base.Start();
            if (processesTab == null || performanceTab == null || endButton == null ||
                processesPage == null || performancePage == null || rows == null ||
                rowTemplate == null || stats == null || stats.Length != 3 ||
                bars == null || bars.Length != 3 || graphs == null || graphs.Length != 3 ||
                gpuCard == null || ramCard == null)
            {
                Debug.LogError("Task Manager: missing UI references in TaskManager.prefab");
                enabled = false;
                return;
            }

            ru = string.Equals(Localization.GetLanguage(), "RU", StringComparison.OrdinalIgnoreCase);
            if (ru)
            {
                // Preserve custom Prefab labels; translate only the stock copy.
                if (processesTabLabel != null && processesTabLabel.text == "Processes")
                    processesTabLabel.text = "Процессы";
                if (performanceTabLabel != null && performanceTabLabel.text == "Performance")
                    performanceTabLabel.text = "Производительность";
                if (endButtonLabel != null && endButtonLabel.text == "End task")
                    endButtonLabel.text = "Снять задачу";
                if (processHeader != null && processHeader.text == "Process")
                    processHeader.text = "Процесс";
                if (processesHeading != null && processesHeading.text == "Processes")
                    processesHeading.text = "Процессы";
            }
            for (int i = 0; i < 3; i++)
                if (stats[i] == null || bars[i] == null || graphs[i] == null)
                {
                    Debug.LogError("Task Manager: incomplete performance card in TaskManager.prefab");
                    enabled = false;
                    return;
                }
            ramDefaultMax = ramCard.anchorMax;
            ramDefaultOffsetMax = ramCard.offsetMax;
            processesTab.onClick.AddListener(() => ShowPage(true));
            performanceTab.onClick.AddListener(() => ShowPage(false));
            endButton.onClick.AddListener(EndSelected);
            rowTemplate.gameObject.SetActive(false);
            ShowPage(true);
        }

        private string L(string english, string russian) { return ru ? russian : english; }

        private void ShowPage(bool processes)
        {
            processesPage.SetActive(processes);
            performancePage.SetActive(!processes);
            if (processesIndicator != null) processesIndicator.SetActive(processes);
            if (performanceIndicator != null) performanceIndicator.SetActive(!processes);
            Refresh();
        }

        private void EndSelected()
        {
            if (system == null || string.IsNullOrEmpty(selected)) return;
            system.EndProcess(selected);
            selected = null;
            Refresh();
        }

        private void RebuildRows(List<App> current)
        {
            foreach (var item in liveRows) if (item != null) Destroy(item.gameObject);
            liveRows.Clear();
            names.Clear();
            apps.Clear();
            foreach (var service in Services)
                if (service != "shell.exe" || system.ShellRunning)
                { names.Add(service); apps.Add(null); }
            foreach (var app in current)
            { names.Add(app.AppName + ".exe"); apps.Add(app); }

            rows.sizeDelta = new Vector2(rows.sizeDelta.x, names.Count * 36f);
            for (int i = 0; i < names.Count; i++)
            {
                int index = i;
                var row = Instantiate(rowTemplate, rows);
                row.name = names[i];
                row.gameObject.SetActive(true);
                var tr = (RectTransform)row.transform;
                tr.anchorMin = new Vector2(0f, 1f);
                tr.anchorMax = new Vector2(1f, 1f);
                tr.pivot = new Vector2(.5f, 1f);
                tr.anchoredPosition = new Vector2(0f, -i * 36f);
                tr.sizeDelta = new Vector2(0f, 34f);
                row.Bind(names[i], i, () => { selected = names[index]; UpdateRows(); });
                liveRows.Add(row);
            }
        }

        private void UpdateRows()
        {
            for (int i = 0; i < liveRows.Count; i++)
            {
                var row = liveRows[i];
                if (row == null) continue;
                row.SetSelected(selected == names[i]);
                var app = apps[i];
                if (app == null)
                    row.SetMetrics(i == 0 ? (system.CpuLoad * 5f).ToString("0") + "%" : "1%",
                        names[i] == "shell.exe" ? (system.GpuLoad * 3f).ToString("0") + "%" : "0%",
                        i == 0 ? "180 MB" : "28 MB");
                else
                    row.SetMetrics((system.ProcessCpuLoad(app) * 100f).ToString("0") + "%",
                        (system.ProcessGpuLoad(app) * 100f).ToString("0") + "%",
                        Mathf.Max(40, app.size) + " MB");
            }
        }

        private static string HardwareModel(string name)
        {
            if (string.IsNullOrEmpty(name)) return "?";
            string result = name.Replace("(Clone)", "").Trim();
            if (result.StartsWith("CPU ", StringComparison.OrdinalIgnoreCase) ||
                result.StartsWith("GPU ", StringComparison.OrdinalIgnoreCase))
                result = result.Substring(4);
            return result;
        }

        private static string Models(List<string> devices)
        {
            var labels = new List<string>();
            var counts = new List<int>();
            foreach (var model in devices)
            {
                int at = labels.IndexOf(model);
                if (at < 0) { labels.Add(model); counts.Add(1); }
                else counts[at]++;
            }
            for (int i = 0; i < labels.Count; i++)
                labels[i] += " (" + counts[i] + ")";
            return string.Join(", ", labels.ToArray());
        }

        private void UpdatePerformance()
        {
            if (system == null || system.Board == null) return;
            float cpuTemp = 0f, clock = 0f, gpuTemp = 0f;
            int cpuCount = 0, gpuCount = 0;
            var cpuModels = new List<string>();
            var gpuModels = new List<string>();
            var cpus = system.Board.GetHardwares(HardwareType.CPU);
            if (cpus != null) foreach (var part in cpus)
                if (part is CPU cpu && cpu.Power && !cpu.Damaged)
                {
                    cpuTemp = Mathf.Max(cpuTemp, cpu.temperature);
                    clock += cpu.EffectiveFrequency;
                    cpuModels.Add(HardwareModel(cpu.gameObject.name));
                    cpuCount++;
                }
            var gpus = system.Board.GetHardwares(HardwareType.GPU);
            if (gpus != null) foreach (var part in gpus)
                if (part is GPU gpu && gpu.Power && !gpu.Damaged)
                {
                    gpuTemp = Mathf.Max(gpuTemp, gpu.Temperature);
                    gpuModels.Add(HardwareModel(gpu.gameObject.name));
                    gpuCount++;
                }
            bool hasGpu = gpuCount > 0;
            if (gpuCard.gameObject.activeSelf != hasGpu)
            {
                gpuCard.gameObject.SetActive(hasGpu);
                // Stretch the authored RAM card into the GPU card's space.
                // Restore its Inspector layout when a card is installed again.
                ramCard.anchorMax = hasGpu ? ramDefaultMax :
                    new Vector2(ramDefaultMax.x, gpuCard.anchorMax.y);
                ramCard.offsetMax = hasGpu ? ramDefaultOffsetMax : gpuCard.offsetMax;
            }
            stats[0].text = Models(cpuModels) + "\n" +
                (system.CpuLoad * 100f).ToString("0") + "%    " +
                cpuTemp.ToString("0") + "°C    " +
                (cpuCount > 0 ? clock / cpuCount : 0f).ToString("0.00") + " GHz";
            if (hasGpu)
                stats[1].text = Models(gpuModels) + "\n" +
                    (system.GpuLoad * 100f).ToString("0") + "%    " +
                    gpuTemp.ToString("0") + "°C";
            stats[2].text = (system.RamLoad * 100f).ToString("0") + "%    " +
                system.RamUsedMb + "/" + system.RamTotalMb + " MB";
            float[] usage = { system.CpuLoad, gpuCount > 0 ? system.GpuLoad : 0f, system.RamLoad };
            for (int i = 0; i < 3; i++)
            {
                if (stats[i] == null || bars[i] == null || graphs[i] == null) continue;
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
            bool changed = names.Count != current.Count + (system.ShellRunning ? 4 : 3);
            if (!changed)
                for (int i = 0; i < current.Count; i++)
                    if (apps[i + (system.ShellRunning ? 4 : 3)] != current[i])
                    { changed = true; break; }
            if (changed) RebuildRows(current);
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
