using System;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

namespace PC.Component.Software
{
    public class Viewer : App
    {
        [SerializeField] private Text titleText;
        [SerializeField] private RawImage display;
        [SerializeField] private Slider frameSlider;
        [SerializeField] private Button playButton;
        [SerializeField] private Text playLabel;
        [SerializeField] private Text frameLabel;

        RectTransform viewRt;
        GameObject barGo;
        Texture2D[] frames;
        float interval = 0.1f;
        int index;
        bool playing;
        float lastTime;
        bool built;

        public override bool SingleInstance => false;
        protected override bool ShowMenuBar => false;

        public override bool CanOpenExtension(string ext)
        {
            if (string.IsNullOrEmpty(ext)) return false;
            return ext.Equals(".pic", StringComparison.OrdinalIgnoreCase)
                || ext.Equals(".mov", StringComparison.OrdinalIgnoreCase);
        }

        public override void Open(string content)
        {
            base.Open(content);
            rect = GetComponent<RectTransform>();
            EnsureUi();

            if (!string.IsNullOrEmpty(content))
            {
                LoadContent(content);
                return;
            }

            // Запуск с иконки/меню без файла — сразу предлагаем выбрать файл,
            // как это делает блокнот. Viewer принимает только .pic и .mov.
            LoadContent(null);
            PromptPickFile();
        }

        /// <summary>
        /// Открывает системный диалог выбора файла, отфильтрованный по .pic и .mov.
        /// </summary>
        public void PromptPickFile()
        {
            var os = system;
            if (os == null) return;

            os.SelectFile(new[] { ".pic", ".mov" }, file =>
            {
                if (file == null) return;
                LoadContent(file.content);
            });
        }

        void Update()
        {
            if (!playing || frames == null || frames.Length < 2) return;
            float step = Mathf.Max(0.02f, interval);
            if (Time.unscaledTime - lastTime < step) return;
            lastTime = Time.unscaledTime;
            ShowFrame((index + 1) % frames.Length, true);
        }

        void LoadContent(string content)
        {
            ClearFrames();
            playing = false;
            index = 0;
            interval = 0.1f;

            if (string.IsNullOrEmpty(content))
            {
                SetTitle("Viewer");
                ShowFrame(0, false);
                RefreshControls();
                return;
            }

            Texture2D[] loaded = null;
            float movInterval = 0.1f;
            byte[] bytes = null;
            try { bytes = Convert.FromBase64String(content); }
            catch { }

            if (bytes != null && IsImageBytes(bytes))
            {
                var tex = FormatConverter.BytesToTexture(bytes, false);
                if (tex != null) loaded = new[] { tex };
            }
            else if (TryLoadMov(bytes, out loaded, out movInterval))
            {
            }
            else
            {
                try
                {
                    var tex = FormatConverter.StringToTexture(content);
                    if (tex != null && tex.width > 2 && tex.height > 2)
                        loaded = new[] { tex };
                    else if (tex != null)
                        Destroy(tex);
                }
                catch { }
            }

            frames = loaded ?? new Texture2D[0];
            interval = movInterval;
            playing = frames.Length > 1;
            lastTime = Time.unscaledTime;
            ShowFrame(0, false);
            RefreshControls();
        }

        static bool IsImageBytes(byte[] bytes)
        {
            if (bytes == null || bytes.Length < 8) return false;
            if (bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47)
                return true;
            if (bytes[0] == 0xFF && bytes[1] == 0xD8)
                return true;
            return false;
        }

        static bool TryLoadMov(byte[] bytes, out Texture2D[] texs, out float interval)
        {
            texs = null;
            interval = 0.1f;
            if (bytes == null || bytes.Length < 12) return false;

            Texture2D[] list = null;
            try
            {
                using (var ms = new MemoryStream(bytes))
                using (var br = new BinaryReader(ms))
                {
                    interval = br.ReadSingle();
                    int count = br.ReadInt32();
                    if (float.IsNaN(interval) || float.IsInfinity(interval) || interval <= 0f || interval > 10f)
                        return false;
                    if (count < 1 || count > 256) return false;

                    list = new Texture2D[count];
                    for (int i = 0; i < count; i++)
                    {
                        if (ms.Position + 4 > ms.Length)
                        {
                            DestroyAll(list);
                            return false;
                        }
                        int len = br.ReadInt32();
                        if (len < 8 || len > 8000000 || ms.Position + len > ms.Length)
                        {
                            DestroyAll(list);
                            return false;
                        }
                        list[i] = FormatConverter.BytesToTexture(br.ReadBytes(len), false);
                    }
                    texs = list;
                    return true;
                }
            }
            catch
            {
                DestroyAll(list);
                return false;
            }
        }

        static void DestroyAll(Texture2D[] list)
        {
            if (list == null) return;
            for (int i = 0; i < list.Length; i++)
            {
                if (list[i] != null) Destroy(list[i]);
            }
        }

        void ShowFrame(int i, bool updateSlider)
        {
            if (frames == null || frames.Length == 0)
            {
                if (display != null) display.texture = null;
                if (frameLabel != null) frameLabel.text = "0/0";
                SetTitle("Viewer");
                return;
            }

            index = Mathf.Clamp(i, 0, frames.Length - 1);
            var tex = frames[index];
            if (display != null)
            {
                display.texture = tex;
                var fit = display.GetComponent<AspectRatioFitter>();
                if (fit != null && tex != null && tex.height > 0)
                    fit.aspectRatio = (float)tex.width / tex.height;
            }

            if (frameLabel != null)
                frameLabel.text = (index + 1) + "/" + frames.Length;
            SetTitle(frames.Length > 1 ? "Viewer  " + (index + 1) + "/" + frames.Length : "Viewer");

            if (updateSlider && frameSlider != null)
                frameSlider.SetValueWithoutNotify(index);
        }

        void RefreshControls()
        {
            int count = frames != null ? frames.Length : 0;
            bool anim = count > 1;
            if (barGo != null) barGo.SetActive(anim);
            if (viewRt != null)
                viewRt.offsetMin = new Vector2(8f, anim ? 52f : 8f);

            if (playButton != null) playButton.gameObject.SetActive(anim);
            if (frameSlider != null)
            {
                frameSlider.gameObject.SetActive(anim);
                if (anim)
                {
                    frameSlider.minValue = 0f;
                    frameSlider.maxValue = count - 1;
                    frameSlider.wholeNumbers = true;
                    frameSlider.SetValueWithoutNotify(index);
                }
            }
            if (playLabel != null) playLabel.text = playing ? "Stop" : "Play";
        }

        void TogglePlay()
        {
            if (frames == null || frames.Length < 2) return;
            playing = !playing;
            lastTime = Time.unscaledTime;
            RefreshControls();
        }

        void OnSlider(float value)
        {
            playing = false;
            if (playLabel != null) playLabel.text = "Play";
            ShowFrame(Mathf.RoundToInt(value), false);
        }

        void SetTitle(string title)
        {
            if (titleText != null) titleText.text = title ?? "Viewer";
        }

        void ClearFrames()
        {
            if (frames == null) return;
            DestroyAll(frames);
            frames = null;
        }

        public override void Close()
        {
            playing = false;
            ClearFrames();
            base.Close();
        }

        void EnsureUi()
        {
            if (titleText == null)
            {
                var t = transform.Find("Title");
                if (t != null) titleText = t.GetComponent<Text>();
            }

            var title = transform.Find("Title");
            if (title != null)
            {
                if (titleText != null) titleText.raycastTarget = true;
                if (title.GetComponent<WindowDrag>() == null)
                    title.gameObject.AddComponent<WindowDrag>();
            }

            if (built && display != null) return;
            built = true;

            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null && titleText != null) font = titleText.font;

            var stage = new GameObject("Stage", typeof(RectTransform), typeof(Image));
            stage.transform.SetParent(transform, false);
            viewRt = stage.GetComponent<RectTransform>();
            viewRt.anchorMin = Vector2.zero;
            viewRt.anchorMax = Vector2.one;
            viewRt.offsetMin = new Vector2(8f, 52f);
            viewRt.offsetMax = new Vector2(-8f, -40f);
            var stageImg = stage.GetComponent<Image>();
            stageImg.color = new Color(0.08f, 0.08f, 0.1f, 1f);
            stageImg.raycastTarget = false;

            var viewGo = new GameObject("View", typeof(RectTransform), typeof(RawImage), typeof(AspectRatioFitter));
            viewGo.transform.SetParent(stage.transform, false);
            var rawRt = viewGo.GetComponent<RectTransform>();
            rawRt.anchorMin = Vector2.zero;
            rawRt.anchorMax = Vector2.one;
            rawRt.offsetMin = Vector2.zero;
            rawRt.offsetMax = Vector2.zero;
            display = viewGo.GetComponent<RawImage>();
            display.color = Color.white;
            display.raycastTarget = false;
            var fit = viewGo.GetComponent<AspectRatioFitter>();
            fit.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            fit.aspectRatio = 1f;

            barGo = new GameObject("Bar", typeof(RectTransform), typeof(Image));
            barGo.transform.SetParent(transform, false);
            var barRt = barGo.GetComponent<RectTransform>();
            barRt.anchorMin = new Vector2(0f, 0f);
            barRt.anchorMax = new Vector2(1f, 0f);
            barRt.pivot = new Vector2(0.5f, 0f);
            barRt.anchoredPosition = Vector2.zero;
            barRt.sizeDelta = new Vector2(0f, 46f);
            barGo.GetComponent<Image>().color = new Color(0.12f, 0.16f, 0.18f, 0.95f);
            barGo.GetComponent<Image>().raycastTarget = true;

            playButton = MakeButton(barGo.transform, "Play", new Vector2(8f, 0f), new Vector2(56f, 30f), font);
            playLabel = playButton.GetComponentInChildren<Text>();
            playLabel.text = "Play";
            playButton.onClick.AddListener(TogglePlay);

            var sliderGo = new GameObject("Slider", typeof(RectTransform), typeof(Slider));
            sliderGo.transform.SetParent(barGo.transform, false);
            var sliderRt = sliderGo.GetComponent<RectTransform>();
            sliderRt.anchorMin = new Vector2(0f, 0.5f);
            sliderRt.anchorMax = new Vector2(1f, 0.5f);
            sliderRt.pivot = new Vector2(0.5f, 0.5f);
            sliderRt.anchoredPosition = new Vector2(18f, 0f);
            sliderRt.sizeDelta = new Vector2(-150f, 22f);

            var bg = MakeFill(sliderGo.transform, "Background", new Color(0.25f, 0.3f, 0.32f, 1f));
            Stretch(bg, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            var fillArea = new GameObject("Fill Area", typeof(RectTransform));
            fillArea.transform.SetParent(sliderGo.transform, false);
            Stretch(fillArea.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, new Vector2(6f, 6f), new Vector2(-6f, -6f));
            var fill = MakeFill(fillArea.transform, "Fill", new Color(0.45f, 0.75f, 0.95f, 1f));
            Stretch(fill, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            var handleArea = new GameObject("Handle Slide Area", typeof(RectTransform));
            handleArea.transform.SetParent(sliderGo.transform, false);
            Stretch(handleArea.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, new Vector2(8f, 0f), new Vector2(-8f, 0f));
            var handleGo = new GameObject("Handle", typeof(RectTransform), typeof(Image));
            handleGo.transform.SetParent(handleArea.transform, false);
            var handleRt = handleGo.GetComponent<RectTransform>();
            handleRt.sizeDelta = new Vector2(16f, 22f);
            var handleImg = handleGo.GetComponent<Image>();
            handleImg.color = Color.white;
            handleImg.raycastTarget = true;

            frameSlider = sliderGo.GetComponent<Slider>();
            frameSlider.targetGraphic = handleImg;
            frameSlider.fillRect = fill;
            frameSlider.handleRect = handleRt;
            frameSlider.direction = Slider.Direction.LeftToRight;
            frameSlider.wholeNumbers = true;
            frameSlider.minValue = 0f;
            frameSlider.maxValue = 1f;
            frameSlider.onValueChanged.AddListener(OnSlider);

            var labelGo = new GameObject("FrameLabel", typeof(RectTransform), typeof(Text));
            labelGo.transform.SetParent(barGo.transform, false);
            var labelRt = labelGo.GetComponent<RectTransform>();
            labelRt.anchorMin = labelRt.anchorMax = new Vector2(1f, 0.5f);
            labelRt.pivot = new Vector2(1f, 0.5f);
            labelRt.anchoredPosition = new Vector2(-10f, 0f);
            labelRt.sizeDelta = new Vector2(64f, 28f);
            frameLabel = labelGo.GetComponent<Text>();
            frameLabel.font = font;
            frameLabel.fontSize = 14;
            frameLabel.color = Color.white;
            frameLabel.alignment = TextAnchor.MiddleRight;
            frameLabel.raycastTarget = false;
            frameLabel.text = "0/0";
        }

        static Button MakeButton(Transform parent, string name, Vector2 pos, Vector2 size, Font font)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.anchoredPosition = new Vector2(pos.x, 0f);
            rt.sizeDelta = size;
            var img = go.GetComponent<Image>();
            img.color = new Color(0.22f, 0.28f, 0.3f, 1f);
            var btn = go.GetComponent<Button>();
            btn.targetGraphic = img;
            var textGo = new GameObject("Text", typeof(RectTransform), typeof(Text));
            textGo.transform.SetParent(go.transform, false);
            Stretch(textGo.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var text = textGo.GetComponent<Text>();
            text.font = font;
            text.fontSize = 14;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.raycastTarget = false;
            return btn;
        }

        static RectTransform MakeFill(Transform parent, string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.color = color;
            img.raycastTarget = true;
            return go.GetComponent<RectTransform>();
        }

        static void Stretch(RectTransform rt, Vector2 min, Vector2 max, Vector2 offsetMin, Vector2 offsetMax)
        {
            rt.anchorMin = min;
            rt.anchorMax = max;
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;
        }
    }
}
