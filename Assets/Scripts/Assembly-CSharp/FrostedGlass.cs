using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// «Матовое стекло» для панели (Image). Размывает то, что на экране ПОД панелью.
///
/// Источник блюра:
///  - монитор/проектор: берётся прямо готовый кадр камеры канваса (её
///    RenderTexture) — без ручных Render() и смены масок, поэтому картинка
///    монитора не ломается. Кадр на 1 кадр «старый» (незаметно);
///  - зум (ScreenSpaceOverlay): экран снимается в конце кадра (WaitForEndOfFrame).
/// Кадр сильно уменьшается (даунсэмпл) и прогоняется через сепарабельный
/// гауссов блюр (H/V). Обновление — каждый кадр.
/// </summary>
public class FrostedGlass : MonoBehaviour
{
    [Range(0f, 1f)] public float opacity = 0.92f;
    public Color tint = new Color(0.96f, 0.97f, 1f, 1f);
    [Range(0f, 0.5f)] public float grain = 0.02f;
    [Range(0.05f, 6f)] public float noiseFreq = 2f;
    [Range(0f, 1f)] public float blurMix = 0.9f;
    [Range(0f, 1f)] public float frost = 0.3f;   // молочное вымывание: больше -> текст за стеклом не читается
    [Range(2, 16)] public int blurDownscale = 10;
    [Range(0.5f, 6f)] public float blurSpread = 2.5f;
    [Range(1, 4)] public int blurPasses = 3;

    private Image image;
    private Material mat;
    private Texture2D whiteTex;

    private static Material blurMat;

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

        // ИСТОЧНИК:
        //  - монитор/проектор: берём прямо готовый кадр камеры (displayRT). В нём
        //    уже всё отрисовано (рабочий стол, окна, панели). Никаких ручных
        //    Render() и смены масок — это надёжно и не ломает картинку монитора.
        //    Кадр — с прошлого прохода камеры (лаг 1 кадр, незаметно).
        //  - зум/overlay: кадр, снятый в конце кадра (WaitForEndOfFrame).
        if (displayRT != null)
        {
            Graphics.Blit(displayRT, bs.a);   // даунсэмпл -> сразу сильное размытие
        }
        else if (screenCap != null)
        {
            Graphics.Blit(screenCap, bs.a);
        }
        else
        {
            RenderTexture.active = prevActive;
            return;
        }

        RenderTexture.active = prevActive;

        // Сепарабельный гаусс (H потом V), пинг-понг A<->B; итог в A.
        int passes = Mathf.Clamp(blurPasses, 1, 4);
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
