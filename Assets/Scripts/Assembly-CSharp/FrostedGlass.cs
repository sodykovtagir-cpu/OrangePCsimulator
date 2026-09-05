using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Делает панель (Image) «матовым стеклом»: размывает то, что отрисовано её
/// канвасом под панелью, и слегка тонирует.
/// Работает в двух режимах:
///  - канвас рендерится камерой в RenderTexture (монитор/проектор): берётся
///    размытая копия этого RT -> настоящее размытие фона;
///  - канвас ScreenSpaceOverlay (режим зума, камеры нет): подаётся белая
///    текстура -> просто светлое полупрозрачное стекло (без черноты).
/// Вешается на тот же объект, что и фоновый Image панели.
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

    private Image image;
    private Material mat;
    private RenderTexture blurRT;
    private Texture2D whiteTex;
    private Texture2D overlayCapture;
    private static Material blurMat;

    private void Awake()
    {
        Apply();
    }

    private void OnEnable()
    {
        Apply();
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

        Texture screen = (cam != null) ? cam.targetTexture : null;

        // Зум/overlay: камеры с RT нет — пробуем захват экрана (раз в несколько
        // кадров, чтобы не грузить устройство).
        if (screen == null)
        {
            if (overlayCapture == null && Time.frameCount % 6 == 0)
            {
                try { overlayCapture = ScreenCapture.CaptureScreenshotAsTexture(); }
                catch { overlayCapture = null; }
            }

            if (overlayCapture != null)
            {
                EnsureBlur(overlayCapture.width, overlayCapture.height);
                var capMat = GetBlurMat();
                if (capMat != null) Graphics.Blit(overlayCapture, blurRT, capMat);
                else Graphics.Blit(overlayCapture, blurRT);
                m.SetTexture("_BlurTex", blurRT);
            }
            else
            {
                // Захвата ещё нет — светлое стекло (без черноты).
                m.SetTexture("_BlurTex", GetWhite());
            }
            return;
        }

        // Создаём/обновляем размытую копию (сильный даунсэмпл уже размывает).
        int bw = Mathf.Max(64, screen.width / Mathf.Max(1, blurDownscale));
        int bh = Mathf.Max(64, screen.height / Mathf.Max(1, blurDownscale));
        EnsureBlur(bw, bh);

        // Размываем текущий кадр канваса. Камера отрисована до UI-пасса,
        // поэтому в targetTexture уже есть контент. Блюр-шейдер делает box blur
        // поверх сильного даунсэмпла.
        var bm = GetBlurMat();
        if (bm != null) Graphics.Blit(screen, blurRT, bm);
        else Graphics.Blit(screen, blurRT);

        m.SetTexture("_BlurTex", blurRT);
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
        if (blurRT != null) { blurRT.Release(); Object.DestroyImmediate(blurRT); blurRT = null; }
        if (overlayCapture != null) { Object.DestroyImmediate(overlayCapture); overlayCapture = null; }
    }

    private void OnDisable()
    {
        ReleaseBlur();
    }

    private void OnDestroy()
    {
        ReleaseBlur();
        if (mat != null) Object.DestroyImmediate(mat);
        if (blurMat != null) { /* shared across instances; destroyed on scene unload */ }
    }
}
