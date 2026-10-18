using System.Collections;
using PC.Component.Software.OS;
using UnityEngine;
using UnityEngine.UI;

namespace PC.Component.Software
{
    // The existing CPU/temperature controls remain on the prefab. The added
    // GPU/RAM controls are built in its window, without replacing its CPU UI.
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
        private Slider gpuSlider, ramSlider;
        private Text gpuText, ramText, statusText;
        private Button testButton;
        private Coroutine testRoutine;
        private OverclockBoot.Profile saved;
        private float startedAt;
        private bool testing;
        private static readonly Color Ink = new Color(.14f, .19f, .22f, 1f);

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
            BuildControls();
            StartCoroutine(ShowInfo());
        }

        public void OnSliderChangedFrequency(float value)
        {
            if (cpu != null) cpu.frequency = value;
            if (frequencyText != null) frequencyText.text = value.ToString("0.0") + "GHz";
            delay = 0f;
        }

        public void OnToggleChangedAuto(bool value) { auto = value; }

        private static RectTransform Box(Transform parent, string name, Vector2 position, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = 5;
            var r = go.GetComponent<RectTransform>();
            r.SetParent(parent, false);
            r.anchorMin = r.anchorMax = new Vector2(0f, 1f);
            r.pivot = new Vector2(0f, 1f);
            r.anchoredPosition = position;
            r.sizeDelta = size;
            return r;
        }

        private static Text Label(Transform parent, string name, string value, Vector2 pos, Vector2 size, int fontSize = 16)
        {
            var r = Box(parent, name, pos, size);
            var text = r.gameObject.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.color = Ink;
            text.alignment = TextAnchor.MiddleLeft;
            text.text = value;
            return text;
        }

        private static Button MakeButton(Transform parent, string title, Vector2 pos, Vector2 size)
        {
            var r = Box(parent, title, pos, size);
            var bg = r.gameObject.AddComponent<Image>();
            bg.color = new Color(.84f, .88f, .89f, 1f);
            var button = r.gameObject.AddComponent<Button>();
            button.targetGraphic = bg;
            var caption = Label(r, "Caption", title, Vector2.zero, size, 14);
            caption.alignment = TextAnchor.MiddleCenter;
            return button;
        }

        private static Slider MakeSlider(Transform parent, string name, Vector2 pos)
        {
            var r = Box(parent, name, pos, new Vector2(300f, 22f));
            var bg = r.gameObject.AddComponent<Image>();
            bg.color = new Color(.73f, .76f, .77f, 1f);
            var slider = r.gameObject.AddComponent<Slider>();
            slider.targetGraphic = bg;
            var fill = Box(r, "Fill", new Vector2(0f, -4f), new Vector2(300f, 14f));
            fill.anchorMin = new Vector2(0f, 0f);
            fill.anchorMax = new Vector2(1f, 1f);
            fill.sizeDelta = new Vector2(0f, -8f);
            var fillImage = fill.gameObject.AddComponent<Image>();
            fillImage.color = new Color(.96f, .48f, .16f, 1f);
            fillImage.raycastTarget = false;
            var handle = Box(r, "Handle", new Vector2(0f, 0f), new Vector2(18f, 22f));
            handle.anchorMin = new Vector2(0f, 0f);
            handle.anchorMax = new Vector2(0f, 1f);
            handle.pivot = new Vector2(.5f, .5f);
            handle.anchoredPosition = Vector2.zero;
            handle.sizeDelta = new Vector2(18f, 0f);
            handle.gameObject.AddComponent<Image>().color = Color.white;
            slider.fillRect = fill;
            slider.handleRect = handle;
            slider.minValue = 1f;
            slider.maxValue = 2f;
            slider.wholeNumbers = false;
            return slider;
        }

        private void BuildControls()
        {
            var root = transform as RectTransform;
            if (root == null) return;
            // Keep the stock title, CPU slider, thermal bar and auto toggle.
            SetDefaultSize(new Vector2(Mathf.Max(defaultSize.x, 700f), 530f));
            Label(root, "GPU label", "GPU clock", new Vector2(48f, -299f), new Vector2(112f, 26f));
            gpuSlider = MakeSlider(root, "GPU ratio", new Vector2(160f, -302f));
            gpuText = Label(root, "GPU value", "", new Vector2(470f, -299f), new Vector2(82f, 26f));
            Label(root, "RAM label", "RAM clock", new Vector2(48f, -346f), new Vector2(112f, 26f));
            ramSlider = MakeSlider(root, "RAM ratio", new Vector2(160f, -349f));
            ramText = Label(root, "RAM value", "", new Vector2(470f, -346f), new Vector2(82f, 26f));
            gpuSlider.value = saved != null ? saved.gpu : 1f;
            ramSlider.value = saved != null ? saved.ram : 1f;
            gpuSlider.onValueChanged.AddListener(_ => UpdateDraft());
            ramSlider.onValueChanged.AddListener(_ => UpdateDraft());
            testButton = MakeButton(root, "Test 60 seconds", new Vector2(45f, -402f), new Vector2(150f, 34f));
            testButton.onClick.AddListener(BeginTest);
            var save = MakeButton(root, "Save without test", new Vector2(207f, -402f), new Vector2(161f, 34f));
            save.onClick.AddListener(SaveProfile);
            statusText = Label(root, "Status", "Test optional. Choose a GPU/RAM profile.",
                new Vector2(45f, -449f), new Vector2(615f, 52f), 14);
            statusText.verticalOverflow = VerticalWrapMode.Overflow;
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
