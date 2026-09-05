using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// «Матовое стекло» для панели (Image). Размывает то, что на экране ПОД панелью.
///
/// Рендер в URP: канвас в режиме ScreenSpaceCamera рисуется ТОЛЬКО своей
/// worldCamera, поэтому отдельная камера/переключение worldCamera не работают
/// (и ломали картинку на мониторе). Поэтому:
///  - панели (Taskbar/StartMenu) переводятся на слой GlassBlur;
///  - для монитора раз в кадр боевой камере канваса временно сужаем
///    cullingMask до слоя UI (без GlassBlur) и рендерим её прямо в маленькую
///    blur-RT, затем полностью возвращаем маску и targetTexture. Боевой
///    рендер на экран монитора идёт после LateUpdate уже с восстановленным
///    состоянием — картинка не пропадает, а в blur-RT нет элементов панели;
///  - в зуме (ScreenSpaceOverlay) экран снимается в конце кадра (WaitForEndOfFrame).
/// Размытие — сепарабельный гаусс (H/V), гладкое, без квадратов. Обновление
/// каждый кадр (180 fps => 180 раз/сек).
/// </summary>
[RequireComponent(typeof(Image))]
[ExecuteAlways]
public class FrostedGlass : MonoBehaviour
{
    [Range(0f, 1f)] public float opacity = 0.92f;
    public Color tint = new Color(0.96f, 0.97f, 1f, 1f);
    [Range(0f, 0.5f)] public float grain = 0.02f;
    [Range(0.05f, 6f)] public float noiseFreq = 2f;
    [Range(0f, 1f)] public float blurMix = 0.9f;
    [Range(0f, 1f)] public float frost = 0.75f;   // молочное вымывание: больше -> текст за стеклом не читается
    [Range(2, 16)] public int blurDownscale = 10;
    [Range(0.5f, 6f)] public float blurSpread = 2.5f;
    [Range(1, 3)] public int blurPasses = 3;

    private Image image;
    private Material mat;
    private Texture2D whiteTex;

    private static Material blurMat;
    private static int glassLayer = -1;
    private static int uiLayer = -1;

    private static Canvas doneCanvas;
    private static int doneFrame = -1;
    private static readonly Dictionary<Canvas, BlurSet> sets = new Dictionary<Canvas, BlurSet>();
    private static readonly int BlurDirId = Shader.PropertyToID("_BlurDir");
    private static Texture2D screenCap;
    private static bool captureCoordinatorStarted;

    private class BlurSet
    {
        public RenderTexture a;   // итог размытия (её сэмплит шейдер)
        public RenderTexture b;   // пинг-понг
    }

    private void Awake() { Apply(); }
    private void OnEnable()
    {
        Apply();
        if (Application.isPlaying && !captureCoordinatorStarted)
        {
            captureCoordinatorStarted = true;
            StartCoroutine(CaptureCoordinator());
        }
    }
    private void Reset() { Apply(); }

    private static int GlassLayer { get { if (glassLayer < 0) glassLayer = LayerMask.NameToLayer("GlassBlur"); return glassLayer; } }
    private static int UiLayer { get { if (uiLayer < 0) uiLayer = LayerMask.NameToLayer("UI"); return uiLayer; } }

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
            m.SetFloat("_Frost", frost);
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
        m.SetFloat("_Frost", frost);

        var canvas = GetComponentInParent<Canvas>();
        if (canvas == null || !Application.isPlaying)
        {
            m.SetTexture("_BlurTex", GetWhite());
            return;
        }

        // ВСЕ панели канваса -> слой GlassBlur, чтобы не попадать в источник блюра.
        int gl = GlassLayer;
        if (gl >= 0)
        {
            var glasses = canvas.GetComponentsInChildren<FrostedGlass>(true);
            for (int i = 0; i < glasses.Length; i++)
                SetLayerRecursive(glasses[i].transform, gl);
        }

        if (!sets.ContainsKey(canvas)) sets[canvas] = new BlurSet();

        // Источник блюра для канваса строим один раз за кадр.
        if (!(doneCanvas == canvas && doneFrame == Time.frameCount))
        {
            doneCanvas = canvas;
            doneFrame = Time.frameCount;
            BuildBlur(canvas, sets[canvas]);
        }

        var bs = sets[canvas];
        m.SetTexture("_BlurTex", (bs != null && bs.a != null) ? (Texture)bs.a : (Texture)GetWhite());
    }

    private void BuildBlur(Canvas canvas, BlurSet bs)
    {
        var bm = GetBlurMat();
        if (bm == null) return;

        Camera cam = canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
        RenderTexture displayRT = cam != null ? cam.targetTexture : null;

        int srcW = displayRT != null ? displayRT.width : Screen.width;
        int srcH = displayRT != null ? displayRT.height : Screen.height;
        int bw = Mathf.Max(64, srcW / Mathf.Max(2, blurDownscale));
        int bh = Mathf.Max(64, srcH / Mathf.Max(2, blurDownscale));
        EnsureRT(ref bs.a, bw, bh);
        EnsureRT(ref bs.b, bw, bh);

        RenderTexture prevActive = RenderTexture.active;

        if (cam != null && displayRT != null)
        {
            // --- Монитор/проектор. НЕ меняем targetTexture камеры (в URP это
            // ломает её конвейер в кадре -> чёрный экран). Вместо этого рендерим
            // камеру в её ОБЫЧНЫЙ displayRT с временно убранным слоем GlassBlur
            // (получаем кадр без панелей), сразу копируем его в маленькую
            // blur-RT и возвращаем маску. Дальше в этом же кадре URP сам
            // перерисует камеру нормально (с панелями) — на мониторе всё видно.
            int uiMask = (UiLayer >= 0) ? (1 << UiLayer) : (1 << 5);
            int glassMask = (GlassLayer >= 0) ? (1 << GlassLayer) : 0;
            int savedMask = cam.cullingMask;
            // Если камера сейчас не рендерится сама (монитор не виден) — не трогаем
            // её RT, чтобы не оставлять кадр без панелей.
            if (cam.isActiveAndEnabled)
            {
                try
                {
                    cam.cullingMask = uiMask;          // только UI -> панели (GlassBlur) не рисуются
                    cam.Render();                      // в displayRT, targetTexture не трогаем
                    Graphics.Blit(displayRT, bs.a);    // кадр без панелей -> маленькая blur-RT
                }
                finally
                {
                    // гарантируем, что панели (GlassBlur) рисуются на мониторе
                    cam.cullingMask = savedMask | glassMask;
                }
            }
        }
        else
        {
            // --- Зум/overlay: используем кадр, снятый в конце кадра.
            if (screenCap != null) Graphics.Blit(screenCap, bs.a); // даунсэмпл
            else { RenderTexture.active = prevActive; return; }
        }

        RenderTexture.active = prevActive;

        // Сепарабельный гаусс (H потом V), пинг-понг A<->B; итог в A.
        int passes = Mathf.Clamp(blurPasses, 1, 3);
        for (int p = 0; p < passes; p++)
        {
            bm.SetVector(BlurDirId, new Vector4(blurSpread, 0f, 0f, 0f)); // H
            Graphics.Blit(bs.a, bs.b, bm);
            bm.SetVector(BlurDirId, new Vector4(0f, blurSpread, 0f, 0f)); // V
            Graphics.Blit(bs.b, bs.a, bm);
        }
    }

    /// <summary>
    /// Снимает экран в конце кадра (активен реальный backbuffer — без ошибки RT),
    /// только когда есть overlay-канвас (режим зума).
    /// </summary>
    private static IEnumerator CaptureCoordinator()
    {
        var wait = new WaitForEndOfFrame();
        while (true)
        {
            yield return wait;
            if (!Application.isPlaying) continue;

            bool anyOverlay = false;
            foreach (var kv in sets)
                if (kv.Key != null && kv.Key.renderMode == RenderMode.ScreenSpaceOverlay) { anyOverlay = true; break; }

            if (anyOverlay)
            {
                Texture2D cap = null;
                try { cap = ScreenCapture.CaptureScreenshotAsTexture(); }
                catch { cap = null; }
                if (cap != null)
                {
                    if (screenCap != null) Destroy(screenCap);
                    screenCap = cap;
                }
            }
            else if (screenCap != null)
            {
                Destroy(screenCap);
                screenCap = null;
            }
        }
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
