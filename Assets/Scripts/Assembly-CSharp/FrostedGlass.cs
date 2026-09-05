using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// «Матовое стекло» для панели (Image). Размывает то, что отрисовано канвасом
/// ПОД панелью.
///
/// Чтобы в размытии не оставалось следов самой панели (часы, иконки, кнопка
/// Пуск «растекаются» ореолом за край), источник блюра рендерится отдельным
/// проходом камеры, которая НЕ рисует слой GlassBlur — на нём лежат Taskbar и
/// StartMenu. Поэтому в блюр попадает только рабочий стол/окна.
///
/// Обновление — КАЖДЫЙ кадр (синхронно с r/vsync): 180 fps => блюр 180 раз/сек.
/// Работает и для монитора (камера в RenderTexture), и для зума (overlay).
/// </summary>
[RequireComponent(typeof(Image))]
[ExecuteAlways]
public class FrostedGlass : MonoBehaviour
{
    [Range(0f, 1f)] public float opacity = 0.62f;
    public Color tint = new Color(0.97f, 0.98f, 1f, 1f);
    [Range(0f, 0.5f)] public float grain = 0.025f;
    [Range(0.05f, 6f)] public float noiseFreq = 2f;
    [Range(0f, 1f)] public float blurMix = 0.9f;
    [Range(2, 16)] public int blurDownscale = 8;   // сильнее даунсэмпл = сильнее блюр
    [Range(1f, 6f)] public float blurSpread = 3f;  // ширина выборки одного прохода
    [Range(1, 4)] public int blurPasses = 2;       // кол-во проходов размытия

    private Image image;
    private Material mat;
    private Texture2D whiteTex;

    private static Material blurMat;
    private static int glassLayer = -1;
    private static int uiLayer = 5;

    // --- Глобальный координатор: один источник блюра на канвас за кадр. ---
    private static Canvas doneCanvas;
    private static int doneFrame = -1;
    private static readonly Dictionary<Canvas, BlurSet> sets = new Dictionary<Canvas, BlurSet>();
    private static Camera overlayBlurCam;

    private class BlurSet
    {
        public RenderTexture a;
        public RenderTexture b;
    }

    private void Awake() { Apply(); }
    private void OnEnable() { Apply(); }
    private void Reset() { Apply(); }

    private static int GlassLayer
    {
        get
        {
            if (glassLayer < 0) glassLayer = LayerMask.NameToLayer("GlassBlur");
            return glassLayer;
        }
    }

    private Material GetMaterial()
    {
        if (mat != null) return mat;
        var shader = Shader.Find("UI/FrostedGlass");
        if (shader == null) return null;
        mat = new Material(shader);
        mat.name = "FrostedGlass_" + name;
        return mat;
    }

    private static Material GetBlurMat()
    {
        if (blurMat == null)
        {
            var sh = Shader.Find("Hidden/UiScreenBlur");
            if (sh != null) blurMat = new Material(sh);
        }
        return blurMat;
    }

    public void Apply()
    {
        image = GetComponent<Image>();
        if (image == null) return;
        var m = GetMaterial();
        if (m != null)
        {
            image.material = m;
            image.color = Color.white;
            m.SetColor("_Color", new Color(tint.r, tint.g, tint.b, opacity));
            m.SetFloat("_NoiseAmount", grain);
            m.SetFloat("_NoiseFreq", noiseFreq);
            m.SetFloat("_BlurAmount", blurMix);
        }
        else
        {
            image.color = new Color(tint.r, tint.g, tint.b, opacity);
        }
    }

    private Texture2D GetWhite()
    {
        if (whiteTex == null)
        {
            whiteTex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            whiteTex.SetPixel(0, 0, Color.white);
            whiteTex.Apply();
        }
        return whiteTex;
    }

    private static void SetLayerRecursive(Transform t, int layer)
    {
        if (t.gameObject.layer != layer) t.gameObject.layer = layer;
        for (int i = 0; i < t.childCount; i++)
            SetLayerRecursive(t.GetChild(i), layer);
    }

    private void LateUpdate()
    {
        var m = mat;
        if (m == null) { Apply(); m = mat; if (m == null) return; }

        m.SetColor("_Color", new Color(tint.r, tint.g, tint.b, opacity));
        m.SetFloat("_NoiseAmount", grain);
        m.SetFloat("_NoiseFreq", noiseFreq);
        m.SetFloat("_BlurAmount", blurMix);

        var canvas = GetComponentInParent<Canvas>();
        if (canvas == null || !Application.isPlaying)
        {
            m.SetTexture("_BlurTex", GetWhite());
            return;
        }

        // Панель и всё её дерево — на слой GlassBlur, чтобы не попадать в блюр.
        int gl = GlassLayer;
        if (gl >= 0) SetLayerRecursive(transform, gl);

        // Источник блюра для этого канваса строим один раз за кадр.
        if (!(doneCanvas == canvas && doneFrame == Time.frameCount))
        {
            doneCanvas = canvas;
            doneFrame = Time.frameCount;
            BuildBlur(canvas);
        }

        if (sets.TryGetValue(canvas, out var bs) && bs != null && bs.a != null)
            m.SetTexture("_BlurTex", bs.a);
        else
            m.SetTexture("_BlurTex", GetWhite());
    }

    private void BuildBlur(Canvas canvas)
    {
        var bm = GetBlurMat();
        if (bm == null) return;

        Camera cam = canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
        RenderTexture displayRT = (cam != null) ? cam.targetTexture : null;

        int srcW, srcH;
        if (displayRT != null) { srcW = displayRT.width; srcH = displayRT.height; }
        else { srcW = Screen.width; srcH = Screen.height; }

        int bw = Mathf.Max(64, srcW / Mathf.Max(2, blurDownscale));
        int bh = Mathf.Max(64, srcH / Mathf.Max(2, blurDownscale));

        if (!sets.TryGetValue(canvas, out var bs) || bs == null) { bs = new BlurSet(); sets[canvas] = bs; }
        EnsureRT(ref bs.a, bw, bh);
        EnsureRT(ref bs.b, bw, bh);

        bm.SetFloat("_Spread", blurSpread);

        int uiMask = 1 << uiLayer;
        int glassMask = (GlassLayer >= 0) ? (1 << GlassLayer) : 0;

        RenderTexture prevActive = RenderTexture.active;

        if (displayRT != null && cam != null)
        {
            // --- Монитор/проектор: рисуем UI камерой канваса, но без слоя панелей.
            // Камера должна показывать панели в норме -> маска UI|Glass; на время
            // блюр-захвата временно оставляем только UI.
            int savedMask = cam.cullingMask;
            cam.cullingMask = uiMask;          // без GlassBlur => нет ореолов панели
            var savedTarget = cam.targetTexture;
            cam.targetTexture = bs.a;          // сразу в низкое разрешение (дёшево)
            cam.Render();
            cam.targetTexture = savedTarget;
            // после восстановления гарантируем, что панели рисуются на экране:
            cam.cullingMask = uiMask | glassMask;
            // savedMask тоже уже включает UI|Glass после первого кадра — не теряем.
        }
        else
        {
            // --- Зум (overlay): отдельная камера, временно переключаем канвас на
            // ScreenSpaceCamera, рендерим UI без слоя панелей, возвращаем overlay.
            Camera bc = GetOverlayBlurCam();
            Camera mainCam = Camera.main;
            if (bc == null || mainCam == null)
            {
                RenderTexture.active = prevActive;
                return;
            }

            bc.CopyFrom(mainCam);
            bc.transform.SetPositionAndRotation(mainCam.transform.position, mainCam.transform.rotation);
            bc.cullingMask = uiMask;           // только UI, без GlassBlur
            bc.clearFlags = CameraClearFlags.SolidColor;
            bc.backgroundColor = new Color(0.1f, 0.2f, 0.4f, 1f);
            bc.targetTexture = bs.a;
            bc.enabled = false;

            var savedMode = canvas.renderMode;
            Camera savedWorld = canvas.worldCamera;
            try
            {
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = bc;
                bc.Render();
            }
            finally
            {
                canvas.worldCamera = savedWorld;
                canvas.renderMode = savedMode;
                bc.targetTexture = null;
            }
        }

        RenderTexture.active = prevActive;

        // Многопроходное размытие (пинг-понг A<->B), результат остаётся в A.
        RenderTexture src = bs.a, dst = bs.b;
        for (int p = 0; p < Mathf.Max(1, blurPasses); p++)
        {
            Graphics.Blit(src, dst, bm);
            var t = src; src = dst; dst = t;
        }
        // Если итог оказался в B — копируем в A (на неё ссылается материал).
        if (src == bs.b) Graphics.Blit(bs.b, bs.a);
    }

    private static Camera GetOverlayBlurCam()
    {
        if (overlayBlurCam == null)
        {
            var go = new GameObject("GlassBlurCam");
            go.hideFlags = HideFlags.HideAndDontSave;
            overlayBlurCam = go.AddComponent<Camera>();
            overlayBlurCam.enabled = false;
        }
        return overlayBlurCam;
    }

    private static void EnsureRT(ref RenderTexture rt, int w, int h)
    {
        if (rt != null && rt.width == w && rt.height == h) return;
        if (rt != null) { rt.Release(); DestroyImmediate(rt); }
        rt = new RenderTexture(w, h, 0, RenderTextureFormat.ARGB32);
        rt.filterMode = FilterMode.Bilinear;
        rt.wrapMode = TextureWrapMode.Clamp;
    }

    private void OnDisable()
    {
        // Не чистим общие RT — за канвасом следит координатор; здесь ничего не нужно.
    }

    private void OnDestroy()
    {
        if (mat != null) DestroyImmediate(mat);
    }
}
