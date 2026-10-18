using System.Collections;
using PC.Component.Software.OS;
using UnityEngine;
using UnityEngine.UI;

namespace PC.Component.Software
{
    // The entire window (including GPU/RAM controls) is editable in its prefab.
    public class Overclock : App
    {
        [SerializeField] private Slider controlBar;
        [SerializeField] private Slider tempBar;
        [SerializeField] private Image warning;
        [SerializeField] private Text frequencyText;
        [SerializeField] private Text CPU_temp;

        private CPU cpu;
        private bool auto = true;
        private float delay;
        [SerializeField] private Slider gpuSlider;
        [SerializeField] private Slider ramSlider;
        [SerializeField] private Text gpuText;
        [SerializeField] private Text ramText;
        [SerializeField] private Text statusText;
        [SerializeField] private Button testButton;
        [SerializeField] private Button saveButton;
        private Coroutine testRoutine;
        private OverclockBoot.Profile saved;
        private float startedAt;
        private bool testing;

        protected override void Start()
        {
            base.Start();
            var board = system != null ? system.Board : null;
            var list = board != null ? board.GetHardwares(HardwareType.CPU) : null;
            cpu = list != null && list.Count > 0 ? list[0] as CPU : null;
            if (board == null || cpu == null) return;
            if (OverclockBoot.Enabled(AutorunConfig.Read(system.FileManager)))
                OverclockBoot.TryRead(system.FileManager, out saved);
            else saved = null; // a recovered disk may keep its stale profile file
            if (controlBar != null) controlBar.value = cpu.frequency;
            BindControls();
            StartCoroutine(ShowInfo());
        }

        public void OnSliderChangedFrequency(float value)
        {
            if (cpu != null) cpu.frequency = value;
            if (frequencyText != null) frequencyText.text = value.ToString("0.0") + "GHz";
            delay = 0f;
        }

        public void OnToggleChangedAuto(bool value) { auto = value; }

        private void BindControls()
        {
            // Visuals, layout, labels and stock sprites live in the prefab.
            // Only the values and actions are bound at runtime. Never replace
            // artist edits by creating a second set of controls here.
            if (gpuSlider == null || ramSlider == null || gpuText == null ||
                ramText == null || statusText == null || testButton == null ||
                saveButton == null)
            {
                Debug.LogError("Overclock: missing UI references in the prefab.");
                return;
            }
            gpuSlider.SetValueWithoutNotify(saved != null ? saved.gpu : 1f);
            ramSlider.SetValueWithoutNotify(saved != null ? saved.ram : 1f);
            gpuSlider.onValueChanged.AddListener(_ => UpdateDraft());
            ramSlider.onValueChanged.AddListener(_ => UpdateDraft());
            testButton.onClick.AddListener(BeginTest);
            saveButton.onClick.AddListener(SaveProfile);
            UpdateDraft();
        }

        private void UpdateDraft()
        {
            if (gpuSlider != null && gpuText != null) gpuText.text = "x" + gpuSlider.value.ToString("0.00");
            if (ramSlider != null && ramText != null) ramText.text = "x" + ramSlider.value.ToString("0.00");
            if (statusText != null)
                statusText.text = testing
                    ? "Settings changed during test. Save or test again."
                    : "GPU/RAM profile: " + ProfileName(Draft()) + ". Test optional.";
        }

        private static string ProfileName(OverclockBoot.Profile profile)
        {
            if (OverclockBoot.Fatal(profile)) return "Extreme";
            if (OverclockBoot.Unstable(profile)) return "High";
            if (profile.gpu >= 1.15f || profile.ram >= 1.15f) return "Boosted";
            return "Normal";
        }

        private OverclockBoot.Profile Draft() => new OverclockBoot.Profile
        {
            cpuFrequency = controlBar != null ? controlBar.value : (cpu != null ? cpu.frequency : 0f),
            gpu = gpuSlider != null ? gpuSlider.value : 1f,
            ram = ramSlider != null ? ramSlider.value : 1f
        };

        private void BeginTest()
        {
            if (testing || system == null || system.Board == null) return;
            if (system.Board.GetHardwares(HardwareType.GPU).Count == 0 ||
                system.Board.GetHardwares(HardwareType.RAM).Count == 0)
            { statusText.text = "Install a GPU and RAM before testing."; return; }
            testing = true;
            testButton.interactable = false;
            testRoutine = StartCoroutine(Test(Draft()));
        }

        private IEnumerator Test(OverclockBoot.Profile p)
        {
            var board = system.Board;
            OverclockBoot.Apply(board, p); // visible GPU artifacts and score boost during test
            startedAt = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - startedAt < 60f)
            {
                if (board == null || !board.Running) yield break;
                statusText.text = "Testing GPU/RAM: " + Mathf.CeilToInt(60f -
                    (Time.realtimeSinceStartup - startedAt)) + " seconds remaining (real time).";
                yield return null;
            }
            // The test never writes a boot file. A failed test is advisory;
            // saving even untested/failed settings is deliberately allowed.
            bool changed = Mathf.Abs(p.gpu - gpuSlider.value) > .001f ||
                Mathf.Abs(p.ram - ramSlider.value) > .001f ||
                (controlBar != null && Mathf.Abs(p.cpuFrequency - controlBar.value) > .001f);
            statusText.text = changed ? "Test finished; settings changed. Retest or save."
                : "60-second test complete. GPU/RAM profile: " + ProfileName(p) + ".";
            testing = false;
            testRoutine = null;
            testButton.interactable = true;
            RestoreApplied();
        }

        private void SaveProfile()
        {
            if (system == null || system.Board == null || system.FileManager == null) return;
            if (gpuSlider == null || ramSlider == null) return;
            if (system.Board.GetHardwares(HardwareType.GPU).Count == 0 ||
                system.Board.GetHardwares(HardwareType.RAM).Count == 0)
            { statusText.text = "Install a GPU and RAM before saving."; return; }
            var profile = Draft();
            if (!OverclockBoot.Save(system.FileManager, profile))
            { statusText.text = "Cannot save: check disk space and System/Autorun.conf."; return; }
            saved = profile;
            OverclockBoot.Apply(system.Board, saved);
            statusText.text = "Saved GPU/RAM profile: " + ProfileName(profile) + ".";
        }

        private void RestoreApplied()
        {
            if (system == null || system.Board == null || !system.Board.Running) return;
            OverclockBoot.Apply(system.Board, saved);
            if (saved == null && cpu != null && !cpu.Damaged) cpu.frequency = cpu.defaultFrequency;
        }

        private void OnDisable()
        {
            if (testing && testRoutine != null) StopCoroutine(testRoutine);
            testing = false;
            RestoreApplied();
        }

        private void OnDestroy() { RestoreApplied(); }

        private IEnumerator ShowInfo()
        {
            float t = 0f;
            while (true)
            {
                if (auto && cpu != null && !testing)
                {
                    if (cpu.temperature <= 90f)
                    {
                        if (warning != null) warning.enabled = false;
                        if (cpu.temperature < 40f && controlBar != null)
                            controlBar.value += Time.deltaTime * 2f;
                    }
                    else
                    {
                        if (controlBar != null)
                            controlBar.value += Time.deltaTime * ((cpu.temperature - 90f) / -10f);
                        if (warning != null) warning.enabled = true;
                    }
                }
                if (delay >= 5f && cpu != null && cpu.frequency >= 10f)
                {
                    var ach = CloudOnceManager.Instance.GetAchievementFromId("maximum_power");
                    if (ach != null) ach.Unlock(null);
                }
                else delay += Time.deltaTime;

                if (t >= .5f)
                {
                    if (cpu != null && CPU_temp != null) CPU_temp.text = cpu.temperature.ToString("0") + "°C";
                    t = 0f;
                }
                else t += Time.deltaTime;
                if (cpu != null && tempBar != null) tempBar.value = cpu.temperature / cpu.burnTemp;
                yield return null;
            }
        }
    }
}
