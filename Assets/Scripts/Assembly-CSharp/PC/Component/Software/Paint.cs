using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using System.Collections.Generic;
using System.IO;

namespace PC.Component.Software
{
    public class Paint : App
    {
        [Header("UI References")]
        [SerializeField] private InputField canvasWidthInput;
        [SerializeField] private InputField canvasHeightInput;
        [SerializeField] private GameObject newDocument;
        [SerializeField] private GameObject workspace;
        [SerializeField] private Button createButton;
        [SerializeField] private RawImage display;
        [SerializeField] private RectTransform canvasTransform;
        [SerializeField] private Button colorPrefab;
        [SerializeField] private Transform colorParent;
        [SerializeField] private ColorPicker colorPicker;

        [Header("Settings")]
        [SerializeField] private Color[] allColors;
        [SerializeField] private Vector2Int minCanvasSize = new Vector2Int(1, 1);
        [SerializeField] private Vector2Int maxCanvasSize = new Vector2Int(1024, 1024);

        private Vector2Int canvasSize;
        private Texture2D texture;
        private Vector2 lastPoint;
        private bool first;
        private bool isDrawing; // Флаг рисования
        private Color drawColor = new Color(0, 0, 0, 1);
        private Image[] colorImages;

        protected override bool ShowMenuBar => false;

        public override void Open(string content)
        {
            base.Open(content);
            if (string.IsNullOrEmpty(content)) return;

            var bytes = System.Convert.FromBase64String(content);
            LoadTexture(bytes);
            var t = texture;
            if (t != null) SetCanvasSize(t.width, t.height);
            InitWorkspace();
        }

        private void Update()
        {
            // Логика рисования мышью/тачем
            if (texture == null || canvasTransform == null || workspace == null || !workspace.activeSelf) return;

            if (Input.GetMouseButtonDown(0))
            {
                if (IsOverCanvas())
                {
                    first = true;
                    isDrawing = true;
                    Draw(Input.mousePosition);
                }
            }
            else if (Input.GetMouseButton(0) && isDrawing)
            {
                Draw(Input.mousePosition);
            }
            else if (Input.GetMouseButtonUp(0))
            {
                isDrawing = false;
            }
        }

        public void ApplyPreset(int i)
        {
            var w = canvasWidthInput;
            var h = canvasHeightInput;

            if (i == 0)
            {
                if (w != null) w.text = "32";
                if (h != null) h.text = "32";
                return;
            }

            if (i == 1)
            {
                if (w != null) w.text = "32";
                if (h != null) h.text = "70";
            }
        }

        public void OnValueChangedSize()
        {
            var btn = createButton;
            var wField = canvasWidthInput;
            var hField = canvasHeightInput;
            if (btn == null || wField == null || hField == null) return;

            int w, h;
            bool okW = int.TryParse(wField.text, out w);
            bool okH = int.TryParse(hField.text, out h);

            bool valid = okW && okH
                && w >= minCanvasSize.x && h >= minCanvasSize.y
                && w <= maxCanvasSize.x && h <= maxCanvasSize.y;

            btn.interactable = valid;
        }

        public void Create()
        {
            if (canvasWidthInput == null || canvasHeightInput == null) return;

            int w, h;
            if (!int.TryParse(canvasWidthInput.text, out w)) return;
            if (!int.TryParse(canvasHeightInput.text, out h)) return;

            SetCanvasSize(w, h);
            NewCanvas();
            InitWorkspace();
        }

        private void SetCanvasSize(int width, int height)
        {
            canvasSize = new Vector2Int(width, height);
            var rt = canvasTransform;
            if (rt != null)
            {
                var arf = rt.GetComponent<AspectRatioFitter>();
                if (arf != null) arf.aspectRatio = (float)width / (float)height;
            }
        }

        private void InitWorkspace()
        {
            if (newDocument != null) newDocument.SetActive(false);
            if (workspace != null) workspace.SetActive(true);

            // Создаем кнопки цветов (один раз при инициализации)
            if (allColors != null && (colorImages == null || colorImages.Length == 0))
            {
                colorImages = new Image[allColors.Length];
                for (int i = 0; i < allColors.Length; i++)
                {
                    var btn = Instantiate(colorPrefab, colorParent);
                    var img = btn != null ? btn.GetComponent<Image>() : null;
                    if (img != null) colorImages[i] = img;
                    int idx = i;
                    if (btn != null) btn.onClick.AddListener(() => SelectColor(idx));
                }
            }
            RefreshColors();

            var os = system;
            if (os != null) os.ShowMenuBar(this);
        }

        private void NewCanvas()
        {
            int w = canvasSize.x;
            int h = canvasSize.y;

            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var pixels = new Color[w * h];
            var white = Color.white;
            for (int i = 0; i < pixels.Length; i++) pixels[i] = white;
            tex.SetPixels(pixels);
            tex.Apply();

            SetTexture(tex);
        }

        private void RefreshColors()
        {
            var colors = allColors;
            var imgs = colorImages;
            if (colors == null || imgs == null) return;
            int n = System.Math.Min(colors.Length, imgs.Length);
            for (int i = 0; i < n; i++)
            {
                var img = imgs[i];
                if (img != null) img.color = colors[i];
            }
        }

        // ПРОВЕРКА: Можно ли рисовать в этой точке?
        private bool IsOverCanvas()
        {
            if (canvasTransform == null) return false;

            Camera cam = GetCanvasCamera();

            // Не попали в прямоугольник холста
            if (!RectTransformUtility.RectangleContainsScreenPoint(canvasTransform, Input.mousePosition, cam))
                return false;

            // Попали в другой UI элемент (кнопка, панель с инструментами)
            if (EventSystem.current != null)
            {
                var ped = new PointerEventData(EventSystem.current) { position = Input.mousePosition };
                var results = new List<RaycastResult>();
                EventSystem.current.RaycastAll(ped, results);

                foreach (var r in results)
                {
                    // Если элемент является кнопкой, инпутом и т.д. — блокируем рисование
                    if (r.gameObject.GetComponent<Selectable>() || r.gameObject.GetComponentInParent<Selectable>())
                        return false;
                }
            }

            return true;
        }

        private void Draw(Vector2 screenPosition)
        {
            if (canvasTransform == null || texture == null) return;

            Camera cam = GetCanvasCamera();

            Vector2 local;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasTransform, screenPosition, cam, out local))
                return;

            var rect = canvasTransform.rect;
            var size = rect.size;
            if (size.x <= 0f || size.y <= 0f) return;

            // Правильная конвертация независимо от Pivot
            var pos = local - rect.min;

            float px = pos.x / size.x * canvasSize.x;
            float py = pos.y / size.y * canvasSize.y;

            if (first)
            {
                lastPoint = new Vector2(px, py);
                SetPixelSafe(Mathf.RoundToInt(px), Mathf.RoundToInt(py));
                first = false;
            }
            else
            {
                var current = new Vector2(px, py);
                DrawLine(lastPoint, current);
                lastPoint = current;
            }

            texture.Apply();
        }

        private void DrawLine(Vector2 p1, Vector2 p2)
        {
            float dx = p2.x - p1.x;
            float dy = p2.y - p1.y;
            int steps = Mathf.Max(1, Mathf.CeilToInt(Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy))));

            for (int i = 0; i <= steps; i++)
            {
                float t = i / (float)steps;
                int x = Mathf.RoundToInt(p1.x + dx * t);
                int y = Mathf.RoundToInt(p1.y + dy * t);
                SetPixelSafe(x, y);
            }
        }

        private void SetPixelSafe(int x, int y)
        {
            if (texture != null && x >= 0 && x < canvasSize.x && y >= 0 && y < canvasSize.y)
                texture.SetPixel(x, y, drawColor);
        }

        private void SelectColor(int index)
        {
            if (allColors == null || index < 0 || index >= allColors.Length) return;
            drawColor = allColors[index];
        }

        public void PickColor()
        {
            var current = drawColor;
            var picker = colorPicker;
            if (picker == null) return;

            picker.PickColor(current, c =>
            {
                drawColor = c;
                var colors = allColors;
                if (colors == null || colors.Length == 0) return;

                for (int i = colors.Length - 1; i >= 1; i--) colors[i] = colors[i - 1];
                colors[0] = c;
                RefreshColors();

                if (picker != null) picker.gameObject.SetActive(false);
            });
        }

        public void Save()
        {
            var png = ImageConversion.EncodeToPNG(texture);
            var content = System.Convert.ToBase64String(png);
            var os = system;
            if (os == null) return;
            var dlg = os.SaveDialog;
            var exts = new string[1];
            exts[0] = FileName;
            if (dlg != null) dlg.ShowDialog("Untitled", content, exts);
        }

        public void OpenLocalFile()
        {
            NativeGallery.GetImageFromGallery(path =>
            {
                if (string.IsNullOrEmpty(path)) return;
                var bytes = System.IO.File.ReadAllBytes(path);
                LoadTexture(bytes);
                var tex = texture;
                if (tex == null) return;
                int targetX = canvasSize.x;
                int targetY = canvasSize.y;
                if (tex.width != targetX || tex.height != targetY) Resize(tex, targetX, targetY);
            }, "Select an image", "image/*");
        }

        public void PrintPicture()
        {
            var os = system;
            if (os != null) os.PrintPicture(texture);
        }

        private void LoadTexture(byte[] bytes)
        {
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            ImageConversion.LoadImage(tex, bytes);
            SetTexture(tex);
        }

        private void SetTexture(Texture2D tex)
        {
            var old = texture;
            if (old != null) Destroy(old);
            if (tex == null) return;
            tex.filterMode = FilterMode.Point;
            tex.wrapMode = TextureWrapMode.Clamp;
            texture = tex;
            var img = display;
            if (img != null) img.texture = tex;
        }

        private void Resize(Texture2D texture2D, int targetX, int targetY)
        {
            var rt = new RenderTexture(targetX, targetY, 24);
            RenderTexture.active = rt;
            Graphics.Blit(texture2D, rt);
            if (texture2D == null) return;
            texture2D.Reinitialize(targetX, targetY);
            var source = new Rect(0f, 0f, targetX, targetY);
            texture2D.ReadPixels(source, 0, 0);
            texture2D.Apply();
            RenderTexture.active = null; // Очистка во избежание утечек памяти
            rt.Release();
        }

        public override void Close()
        {
            if (colorPicker != null && colorPicker.gameObject.activeSelf)
                colorPicker.gameObject.SetActive(false);

            if (display != null) display.texture = null;
            var tex = texture;
            if (tex != null) Destroy(tex);

            base.Close();
        }

        // Вспомогательный метод для определения камеры Canvas
        private Camera GetCanvasCamera()
        {
            var c = canvasTransform != null ? canvasTransform.GetComponentInParent<Canvas>() : null;
            if (c != null && c.renderMode != RenderMode.ScreenSpaceOverlay)
                return c.worldCamera;
            return null;
        }
    }
}