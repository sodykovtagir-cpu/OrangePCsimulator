using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// «Матовое стекло» для панели (Image). Размывает то, что отрисовано канвасом
/// ПОД панелью.
///
/// Важные детали:
///  - Источник блюра рендерится ОТДЕЛЬНОЙ скрытой камерой (копией боевой),
///    которая НЕ рисует слой GlassBlur. Боевая камера монитора/экрана не
///    меняется, поэтому картинка на мониторе не пропадает.
///  - Taskbar и StartMenu переводятся на слой GlassBlur (все панели канваса
///    сразу, ДО сборки блюра), поэтому в размытие не попадают элементы самой
///    панели — никаких ореолов/следов от часов и иконок.
///  - Блюр обновляется КАЖДЫЙ кадр (180 fps => 180 раз/сек).
///  - Размытие — сепарабельный гаусс (H/V проходы), гладкий, без «квадратов».
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
    [Range(0.5f, 6f)] public float blurSpread = 2f; // шаг выборки в текселях
    [Range(1, 3)] public int blurPasses = 2;       // сколько раз применить гаусс (H+V)

    private Image image;
    private Material mat;
    private Texture2D whiteTex;

    private static Material blurMat;
    private static int glassLayer = -1;
    private static int uiLayer = -1;

    // Координатор: один источник блюра на канвас за кадр.
    private static Canvas doneCanvas;
    private static int doneFrame = -1;
    private static readonly Dictionary<Canvas, BlurSet> sets = new Dictionary<Canvas, BlurSet>();
    private static Camera blurCamera;
    private static readonly int BlurDirId = Shader.PropertyToID("_BlurDir");

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
        get { if (glassLayer < 0) glassLayer = LayerMask.NameToLayer("GlassBlur"); return glassLayer; }
    }

    private static int UiLayer
    {
        get { if (uiLayer < 0) uiLayer = LayerMask.NameToLayer("UI"); return uiLayer; }
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

        // Сначала переводим ВСЕ панели канваса на слой GlassBlur, чтобы ни одна
        // из них (таскбар, меню Пуск) не попала в источник блюра -> нет ореолов.
        int gl = GlassLayer;
        if (gl >= 0)
        {
            var glasses = canvas.GetComponentsInChildren<FrostedGlass>(true);
            for (int i = 0; i < glasses.Length; i++)
                SetLayerRecursive(glasses[i].transform, gl);
        }

        // Источник блюра для канваса строим один раз за кадр.
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

        // Боевая камера канваса (для монитора) либо main (для зума/overlay).
        Camera worldCam = canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
        Camera baseCam = worldCam != null ? worldCam : Camera.main;
        if (baseCam == null) return;

        Camera bc = GetBlurCamera();
        bc.CopyFrom(baseCam);
        bc.transform.SetPositionAndRotation(baseCam.transform.position, baseCam.transform.rotation);
        bc.enabled = false;                       // рендерим только вручную
        bc.clearFlags = CameraClearFlags.SolidColor;
        bc.backgroundColor = new Color(0.1f, 0.2f, 0.4f, 1f);

        int uiMask = (UiLayer >= 0) ? (1 << UiLayer) : (1 << 5);
        bc.cullingMask = uiMask;                 // только UI, без слоя GlassBlur -> нет панелей

        int srcW = baseCam.targetTexture != null ? baseCam.targetTexture.width : Screen.width;
        int srcH = baseCam.targetTexture != null ? baseCam.targetTexture.height : Screen.height;
        int bw = Mathf.Max(64, srcW / Mathf.Max(2, blurDownscale));
        int bh = Mathf.Max(64, srcH / Mathf.Max(2, blurDownscale));

        if (!sets.TryGetValue(canvas, out var bs) || bs == null) { bs = new BlurSet(); sets[canvas] = bs; }
        EnsureRT(ref bs.a, bw, bh);
        EnsureRT(ref bs.b, bw, bh);

        // Рендерим канвас ОТДЕЛЬНОЙ камерой (без слоя GlassBlur) в blur-RT.
        // Боевую камеру не трогаем -> картинка на мониторе остаётся.
        var savedMode = canvas.renderMode;
        var savedWorld = canvas.worldCamera;
        RenderTexture prevActive = RenderTexture.active;
        try
        {
            bc.targetTexture = bs.a;
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = bc;
            bc.Render();
        }
        finally
        {
            canvas.worldCamera = savedWorld;
            canvas.renderMode = savedMode;
            bc.targetTexture = null;
            RenderTexture.active = prevActive;
        }

        // Сепарабельный гаусс: для каждой итерации H потом V (пинг-понг A<->B).
        int passes = Mathf.Clamp(blurPasses, 1, 3);
        RenderTexture src = bs.a;
        RenderTexture dst = bs.b;
        for (int p = 0; p < passes; p++)
        {
            bm.SetVector(BlurDirId, new Vector4(blurSpread, 0f, 0f, 0f)); // H
            Graphics.Blit(src, dst, bm);
            bm.SetVector(BlurDirId, new Vector4(0f, blurSpread, 0f, 0f)); // V
            Graphics.Blit(dst, src, bm);
        }
        // Итог всегда в src == bs.a (V-проход пишет в src).
    }

    private static Camera GetBlurCamera()
    {
        if (blurCamera == null)
        {
            var go = new GameObject("GlassBlurCam");
            go.hideFlags = HideFlags.HideAndDontSave;
            blurCamera = go.AddComponent<Camera>();
            blurCamera.enabled = false;
        }
        return blurCamera;
    }

    private static void EnsureRT(ref RenderTexture rt, int w, int h)
    {
        if (rt != null && rt.width == w && rt.height == h) return;
        if (rt != null) { rt.Release(); Object.DestroyImmediate(rt); }
        rt = new RenderTexture(w, h, 0, RenderTextureFormat.ARGB32);
        rt.filterMode = FilterMode.Bilinear;
        rt.wrapMode = TextureWrapMode.Clamp;
    }

    private void OnDestroy()
    {
        if (mat != null) DestroyImmediate(mat);
    }
}
