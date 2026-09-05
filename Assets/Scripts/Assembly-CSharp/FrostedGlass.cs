using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Делает панель (Image) «матовым стеклом»: размывает то, что отрисовано её
/// канвасом под панелью, и слегка тонирует.
/// Режимы:
///  - канвас рендерится камерой в RenderTexture (монитор/проектор): берётся
///    размытая копия этого RT -> настоящее размытие фона;
///  - канвас ScreenSpaceOverlay (режим зума, камеры нет): экран захватывается
///    в конце кадра (WaitForEndOfFrame), даунсэмплится и размывается.
/// Частота обновления блюра задаётся в секундах (blurRefreshInterval) и не
/// зависит от fps.
/// </summary>
[RequireComponent(typeof(Image))]
[ExecuteAlways]
public class FrostedGlass : MonoBehaviour
{
    [Range(0f, 1f)] public float opacity = 0.62f;
    public Color tint = new Color(0.97f, 0.98f, 1f, 1f);
    [Range(0f, 0.5f)] public float grain = 0.025f;
    [Range(0.05f, 6f)] public float noiseFreq = 2f;
    [Range(0f, 1f)] public float blurMix = 0.85f;
    [Range(1, 16)] public int blurDownscale = 6;
    // Период обновления блюра в секундах (для overlay/зум). Не зависит от fps:
    // 0.05 = ~20 раз в секунду при любом фреймрейте.
    [Range(0.016f, 0.5f)] public float blurRefreshInterval = 0.05f;

    private Image image;
    private Material mat;
    private RenderTexture blurRT;
    private Texture2D whiteTex;
    private Texture2D overlayCapture;   // последний захваченный кадр (заполняется в конце кадра)
    private Coroutine captureRoutine;
    private double lastBlurTime;
    private static Material blurMat;

    private void Awake()
    {
        Apply();
    }

    private void OnEnable()
    {
        Apply();
        if (Application.isPlaying && captureRoutine == null)
            captureRoutine = StartCoroutine(CaptureLoop());
    }

    private void Reset()
    {
        Apply();
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

    /// <summary>
    /// Захват экрана строго в конце кадра: в этот момент активный рендер-таргет
    /// — экран (backbuffer), поэтому нет ошибки «region exceeds active Render
    /// Target». Делаем это по реальному времени, а не по номеру кадра.
    /// </summary>
    private IEnumerator CaptureLoop()
    {
        var wait = new WaitForEndOfFrame();
        while (true)
        {
            yield return wait;

            if (!isActiveAndEnabled || !Application.isPlaying) continue;

            // Захват только когда реально нужен overlay-режим (нет камеры с RT).
            var canvas = GetComponentInParent<Canvas>();
            Camera cam = null;
            if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
                cam = canvas.worldCamera;
            if (cam != null && cam.targetTexture != null) continue; // монитор: блюр из RT

            // Частота по времени (не зависит от fps).
            double now = Time.unscaledTime;
            if (now - lastCaptureTime < Mathf.Max(0.016f, blurRefreshInterval)) continue;
            lastCaptureTime = now;

            Texture2D oldCap = overlayCapture;
            Texture2D cap = null;
            try { cap = ScreenCapture.CaptureScreenshotAsTexture(); }
            catch { cap = null; }
            if (cap != null)
            {
                overlayCapture = cap;
                if (oldCap != null) Destroy(oldCap);
            }
        }
    }

    private double lastCaptureTime;

    private void LateUpdate()
    {
        var m = mat;
        if (m == null) { Apply(); m = mat; if (m == null) return; }

        // Синхронизируем параметры (на случай правок в инспекторе).
        m.SetColor("_Color", new Color(tint.r, tint.g, tint.b, opacity));
        m.SetFloat("_NoiseAmount", grain);
        m.SetFloat("_NoiseFreq", noiseFreq);
        m.SetFloat("_BlurAmount", blurMix);

        var canvas = GetComponentInParent<Canvas>();
        Camera cam = null;
        if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
            cam = canvas.worldCamera;

        RenderTexture screenRT = (cam != null) ? cam.targetTexture : null;

        // В редакторе вне Play-режима ничего не захватываем/не блюрим.
        if (!Application.isPlaying)
        {
            m.SetTexture("_BlurTex", GetWhite());
            return;
        }

        var bm = GetBlurMat();

        // Ветка 1: монитор/проектор — канвас рендерится камерой в RenderTexture.
        if (screenRT != null)
        {
            // Обновляем блюр тоже по времени (дешевле и плавно по нагрузке).
            double now = Time.unscaledTime;
            if (now - lastBlurTime >= Mathf.Max(0.016f, blurRefreshInterval))
            {
                lastBlurTime = now;
                int bw = Mathf.Max(64, screenRT.width / Mathf.Max(1, blurDownscale));
                int bh = Mathf.Max(64, screenRT.height / Mathf.Max(1, blurDownscale));
                EnsureBlur(bw, bh);
                if (bm != null) Graphics.Blit(screenRT, blurRT, bm);
                else Graphics.Blit(screenRT, blurRT);
            }
            m.SetTexture("_BlurTex", blurRT != null ? blurRT : (Texture)GetWhite());
            return;
        }

        // Ветка 2: зум/overlay — используем кадр, захваченный в конце кадра.
        if (overlayCapture != null)
        {
            int cw = Mathf.Max(64, overlayCapture.width / Mathf.Max(1, blurDownscale));
            int ch = Mathf.Max(64, overlayCapture.height / Mathf.Max(1, blurDownscale));
            EnsureBlur(cw, ch);
            if (bm != null) Graphics.Blit(overlayCapture, blurRT, bm);
            else Graphics.Blit(overlayCapture, blurRT);
            m.SetTexture("_BlurTex", blurRT);
        }
        else
        {
            // Захват ещё не готов — светлое стекло (без черноты).
            m.SetTexture("_BlurTex", GetWhite());
        }
    }

    private void EnsureBlur(int width, int height)
    {
        if (blurRT != null && blurRT.width == width && blurRT.height == height)
            return;
        if (blurRT != null) blurRT.Release();
        blurRT = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32);
        blurRT.filterMode = FilterMode.Bilinear;
        blurRT.wrapMode = TextureWrapMode.Clamp;
    }

    private void ReleaseBlur()
    {
        if (blurRT != null) { blurRT.Release(); DestroyImmediate(blurRT); blurRT = null; }
        if (overlayCapture != null) { DestroyImmediate(overlayCapture); overlayCapture = null; }
    }

    private void OnDisable()
    {
        if (captureRoutine != null) { StopCoroutine(captureRoutine); captureRoutine = null; }
        ReleaseBlur();
    }

    private void OnDestroy()
    {
        if (captureRoutine != null) { StopCoroutine(captureRoutine); captureRoutine = null; }
        ReleaseBlur();
        if (mat != null) DestroyImmediate(mat);
    }
}
