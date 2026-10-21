using System;
using System.Collections.Generic;
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
        float[] frameIntervals;
        float interval = 0.1f;
        int index;
        bool playing;
        float frameAccumulator;
        bool built;
        private File currentFile;
        private readonly List<File> siblings = new List<File>();
        private Button previousButton;
        private Button nextButton;

        public override bool SingleInstance => false;
        protected override bool ShowMenuBar => false;

        public override bool CanOpenExtension(string ext)
        {
            if (string.IsNullOrEmpty(ext)) return false;
            return ext.Equals(".pic", StringComparison.OrdinalIgnoreCase)
                || ext.Equals(".mov", StringComparison.OrdinalIgnoreCase)
                || ext.Equals(".png", StringComparison.OrdinalIgnoreCase)
                || ext.Equals(".jpg", StringComparison.OrdinalIgnoreCase)
                || ext.Equals(".jpeg", StringComparison.OrdinalIgnoreCase)
                || ext.Equals(".gif", StringComparison.OrdinalIgnoreCase);
        }

        public override void Open(string content)
        {
            base.Open(content);
            rect = GetComponent<RectTransform>();
            currentFile = null;
            siblings.Clear();
            EnsureUi();

            if (!string.IsNullOrEmpty(content))
            {
                LoadContent(content);
                return;
            }

            // Запуск без файла открывает диалог выбора фото или анимации.
            LoadContent(null);
            PromptPickFile();
        }

        /// <summary>
        /// Открывает системный диалог выбора изображения или анимации.
        /// </summary>
        public void PromptPickFile()
        {
            var os = system;
            if (os == null) return;

            os.SelectFile(new[] { ".pic", ".mov", ".png", ".jpg", ".jpeg", ".gif" }, file =>
            {
                if (file == null) return;
                LoadContent(file.content);
                OpenDocument(file);
            });
        }

        public void OpenDocument(File file)
        {
            if (file == null) return;
            currentFile = file;
            siblings.Clear();
            if (system != null && system.AllStorage != null)
                foreach (var disk in system.AllStorage)
                {
                    if (disk == null || disk.files == null || !disk.files.Contains(file)) continue;
                    string folder = FolderOf(file.path);
                    foreach (var item in disk.files)
                        if (item != null && !item.isFolder &&
                            FolderOf(item.path) == folder && CanOpenExtension(item.Extension()))
                            siblings.Add(item);
                    break;
                }
            siblings.Sort((a, b) => string.Compare(a.path, b.path, StringComparison.OrdinalIgnoreCase));
            ShowFrame(index, false);
            RefreshControls();
        }

        private static string FolderOf(string path)
        {
            if (string.IsNullOrEmpty(path)) return "";
            int slash = path.LastIndexOf('/');
            return slash < 0 ? "" : path.Substring(0, slash);
        }

        public void PreviousFile() { SwitchFile(-1); }
        public void NextFile() { SwitchFile(1); }

        private void SwitchFile(int direction)
        {
            if (siblings.Count < 2 || currentFile == null) return;
            int i = siblings.IndexOf(currentFile);
            if (i < 0) return;
            currentFile = siblings[(i + direction + siblings.Count) % siblings.Count];
            LoadContent(currentFile.content);
        }

        void Update()
        {
            if (!playing || frames == null || frames.Length < 2) return;
            float step = Mathf.Max(0.02f, frameIntervals != null && index < frameIntervals.Length
                ? frameIntervals[index] : interval);
            frameAccumulator += Time.unscaledDeltaTime *
                (system != null ? system.UiResponsiveness : 1f);
            if (frameAccumulator < step) return;
            frameAccumulator = 0f;
            ShowFrame((index + 1) % frames.Length, true);
        }

        void LoadContent(string content)
        {
            ClearFrames();
            playing = false;
            index = 0;
            interval = 0.1f;
            frameIntervals = null;

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
            else if (bytes != null && GifFrames.TryDecode(bytes, out loaded, out frameIntervals))
            {
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
            frameAccumulator = 0f;
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
                SetTitle(currentFile != null ? System.IO.Path.GetFileName(currentFile.path) : "Viewer");
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
            string name = currentFile != null ? System.IO.Path.GetFileName(currentFile.path) : "Viewer";
            SetTitle(frames.Length > 1 ? name + "  " + (index + 1) + "/" + frames.Length : name);

            if (updateSlider && frameSlider != null)
                frameSlider.SetValueWithoutNotify(index);
        }

        void RefreshControls()
        {
            int count = frames != null ? frames.Length : 0;
            bool anim = count > 1;
            bool navigation = siblings.Count > 1;
            // Photo navigation floats on the vertical edges. The bottom bar is
            // reserved for animation controls and never steals photo space.
            if (barGo != null) barGo.SetActive(anim);
            if (viewRt != null)
                viewRt.offsetMin = new Vector2(8f, anim ? 52f : 8f);
            if (previousButton != null) previousButton.gameObject.SetActive(navigation);
            if (nextButton != null) nextButton.gameObject.SetActive(navigation);

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
            frameAccumulator = 0f;
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
            frameIntervals = null;
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

            previousButton = MakeButton(viewRt, "Previous", Vector2.zero, new Vector2(58f, 74f), font);
            PlaceNavigationArrow(previousButton, false);
            previousButton.GetComponentInChildren<Text>().text = "‹";
            previousButton.GetComponentInChildren<Text>().fontSize = 32;
            previousButton.onClick.AddListener(PreviousFile);
            nextButton = MakeButton(viewRt, "Next", Vector2.zero, new Vector2(58f, 74f), font);
            PlaceNavigationArrow(nextButton, true);
            nextButton.GetComponentInChildren<Text>().text = "›";
            nextButton.GetComponentInChildren<Text>().fontSize = 32;
            nextButton.onClick.AddListener(NextFile);
            playButton = MakeButton(barGo.transform, "Play", new Vector2(84f, 0f), new Vector2(56f, 30f), font);
            playLabel = playButton.GetComponentInChildren<Text>();
            playLabel.text = "Play";
            playButton.onClick.AddListener(TogglePlay);

            var sliderGo = new GameObject("Slider", typeof(RectTransform), typeof(Slider));
            sliderGo.transform.SetParent(barGo.transform, false);
            var sliderRt = sliderGo.GetComponent<RectTransform>();
            sliderRt.anchorMin = new Vector2(0f, 0.5f);
            sliderRt.anchorMax = new Vector2(1f, 0.5f);
            sliderRt.pivot = new Vector2(0.5f, 0.5f);
            sliderRt.anchoredPosition = new Vector2(85f, 0f);
            sliderRt.sizeDelta = new Vector2(-284f, 22f);

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

        private static void PlaceNavigationArrow(Button button, bool right)
        {
            var rt = button.transform as RectTransform;
            if (rt == null) return;
            rt.anchorMin = rt.anchorMax = new Vector2(right ? 1f : 0f, .5f);
            rt.pivot = new Vector2(right ? 1f : 0f, .5f);
            rt.anchoredPosition = new Vector2(right ? -12f : 12f, 0f);
            rt.SetAsLastSibling();
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
