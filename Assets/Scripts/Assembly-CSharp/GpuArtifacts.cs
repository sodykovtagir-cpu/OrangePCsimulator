using PC.Component;
using UnityEngine;
using UnityEngine.UI;
using Display = PC.Component.Display;

/// <summary>
/// GPU/VRAM corruption in SCREEN pixels, not colorful UI rectangles in world
/// space. The user's reference photos show sparse pixel faults, repeated glyph
/// tiles, green scanline fragments, vertical RGB bars and tiled framebuffer
/// blocks. Each symptom is synthesized on a small transparent RGBA texture.
/// </summary>
public class GpuArtifacts : MonoBehaviour
{
    private enum Pattern { Speckles, BlueGlyphs, GreenRows, ColorBars, MemoryTiles, TornBands }
    private const int Width = 320;
    private const int Height = 200;
    private static readonly Color32 Clear = new Color32(0, 0, 0, 0);
    private readonly Color32[] pixels = new Color32[Width * Height];

    [SerializeField] private Vector2 episodeLength = new Vector2(0.8f, 4f);
    [SerializeField] private Vector2 episodeGap = new Vector2(3f, 22f);
    [SerializeField] private float refreshInterval = 0.18f;

    private Display display;
    private RectTransform screen;
    private RectTransform viewport;
    private RawImage image;
    private Texture2D texture;
    private bool inEpisode;
    private bool episodeVisible;
    private float episodeUntil;
    private float nextRefresh;
    private Pattern pattern;
    private int frame;

    private void Awake()
    {
        display = GetComponent<Display>();
        // Display.Start adds this component before TexturedMonitor detaches its
        // canvas. Retain the actual canvas, not a transform under the bezel.
        var canvas = GetComponentInChildren<Canvas>();
        if (canvas != null) screen = canvas.GetComponent<RectTransform>();
    }

    private float Strength()
    {
        if (display == null || !display.Power || display.ConnectedBoard == null) return 0f;
        var cards = display.ConnectedBoard.GetHardwares(HardwareType.GPU);
        float strength = 0f;
        if (cards != null)
            foreach (var item in cards)
            {
                var gpu = item as GPU;
                if (gpu != null && !gpu.Damaged)
                    strength = Mathf.Max(strength, gpu.ArtifactStrength);
            }
        return Mathf.Clamp01(strength);
    }

    private void EnsureView()
    {
        if (viewport != null || screen == null) return;
        var root = new GameObject("GPU Artifact Viewport", typeof(RectTransform), typeof(RectMask2D));
        root.layer = screen.gameObject.layer; // render-texture cameras render UI only
        viewport = root.GetComponent<RectTransform>();
        viewport.SetParent(screen, false);
        viewport.anchorMin = Vector2.zero;
        viewport.anchorMax = Vector2.one;
        viewport.offsetMin = Vector2.zero;
        viewport.offsetMax = Vector2.zero;

        // Deliberately NO separate high-sorting Canvas. A nested Canvas with
        // overrideSorting=32000 ignored the monitor's depth and drew ON TOP of
        // its plastic frame / even objects standing in front of the glass.
        var child = new GameObject("VRAM pixel corruption", typeof(RectTransform), typeof(RawImage));
        child.layer = root.layer;
        var rect = child.GetComponent<RectTransform>();
        rect.SetParent(viewport, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        image = child.GetComponent<RawImage>();
        image.raycastTarget = false;
        image.enabled = false;
        texture = new Texture2D(Width, Height, TextureFormat.RGBA32, false);
        texture.name = "GPU VRAM artifact pixels";
        texture.filterMode = FilterMode.Point;
        texture.wrapMode = TextureWrapMode.Clamp;
        image.texture = texture;
    }

    private void LateUpdate()
    {
        float strength = Strength();
        if (strength <= 0f)
        {
            inEpisode = false;
            episodeVisible = false;
            episodeUntil = 0f;
            if (image != null) image.enabled = false;
            return;
        }

        float now = Time.unscaledTime;
        if (episodeUntil <= 0f)
            episodeUntil = now + Random.Range(episodeGap.x, episodeGap.y);
        if (now >= episodeUntil)
        {
            inEpisode = !inEpisode;
            if (inEpisode)
            {
                pattern = (Pattern)Random.Range(0, 6);
                episodeVisible = Random.value < Mathf.Lerp(.3f, 1f, strength);
                episodeUntil = now + Random.Range(episodeLength.x, episodeLength.y)
                    * Mathf.Lerp(.6f, 1.5f, strength);
                nextRefresh = 0f;
            }
            else
                episodeUntil = now + Random.Range(episodeGap.x, episodeGap.y)
                    * Mathf.Lerp(1f, .15f, strength);
        }
        if (!inEpisode || !episodeVisible)
        {
            if (image != null) image.enabled = false;
            return;
        }
        EnsureView();
        if (image == null) return;
        // Windows, desktop and popups may call SetAsLastSibling each frame.
        // Stay above OS content WITHOUT escaping the host screen's depth test.
        if (viewport.GetSiblingIndex() != screen.childCount - 1)
            viewport.SetAsLastSibling();
        if (now < nextRefresh) return;
        nextRefresh = now + Mathf.Max(.08f, refreshInterval);
        Draw(strength);
        image.enabled = true;
    }

    private void Draw(float strength)
    {
        System.Array.Clear(pixels, 0, pixels.Length);
        frame++;
        switch (pattern)
        {
            case Pattern.Speckles: Speckles(strength); break;
            case Pattern.BlueGlyphs: BlueGlyphs(strength); break;
            case Pattern.GreenRows: GreenRows(strength); break;
            case Pattern.ColorBars: ColorBars(strength); break;
            case Pattern.MemoryTiles: MemoryTiles(strength); break;
            case Pattern.TornBands: TornBands(strength); break;
        }
        texture.SetPixels32(pixels);
        texture.Apply(false, false);
    }

    private void Rect(int x, int y, int w, int h, Color32 color)
    {
        int x0 = Mathf.Clamp(x, 0, Width), x1 = Mathf.Clamp(x + w, 0, Width);
        int y0 = Mathf.Clamp(y, 0, Height), y1 = Mathf.Clamp(y + h, 0, Height);
        for (int row = y0; row < y1; row++)
            for (int column = x0; column < x1; column++) pixels[row * Width + column] = color;
    }

    // Tiny drifting RGB fragments, as on the blue Windows desktop references.
    private void Speckles(float s)
    {
        int count = Mathf.RoundToInt(Mathf.Lerp(30f, 680f, s));
        for (int i = 0; i < count; i++)
        {
            var color = Random.value < .5f
                ? new Color32(255, 196, 78, 235)
                : new Color32((byte)Random.Range(45, 255), (byte)Random.Range(65, 255),
                    (byte)Random.Range(60, 255), 240);
            Rect(Random.Range(0, Width), Random.Range(0, Height),
                Random.Range(1, 4), Random.Range(1, 3), color);
        }
    }

    // Repeating 6x8 character-shaped VRAM pages, blue/cyan/white like the
    // corrupted blue screens in the provided photographs (not a fake BSOD).
    private void BlueGlyphs(float s)
    {
        int top = Random.Range(0, Height / 4);
        int end = Mathf.Min(Height, top + Mathf.RoundToInt(Mathf.Lerp(40, Height, s)));
        Rect(0, top, Width, end - top, new Color32(4, 18, 112, 160));
        for (int y = top + 2; y < end - 6; y += 8)
            for (int x = 2; x < Width - 5; x += 6)
            {
                int glyph = (x / 6 + y / 8 + frame / 3) % 23;
                if (Random.value > .45f + .45f * s) continue;
                var color = glyph % 5 == 0 ? new Color32(253, 248, 244, 235)
                    : new Color32(60, (byte)Random.Range(145, 255), 255, 215);
                Rect(x, y, 1, 5, color);
                if (glyph % 3 != 0) Rect(x + 1, y + (glyph % 3), 3, 1, color);
                if (glyph % 2 == 0) Rect(x + 2, y + 4, 3, 1, color);
            }
        for (int i = 0; i < 4; i++)
            Rect(0, Random.Range(top, end), Width, 1, new Color32(34, 194, 252, 220));
    }

    // Repeated broken phosphor-green horizontal traces from the CRT example.
    private void GreenRows(float s)
    {
        int band = Mathf.RoundToInt(Mathf.Lerp(45f, 155f, s));
        int start = Random.Range(0, Height - band + 1);
        if (s > .65f)
            Rect(0, start, Width, band, new Color32(1, 30, 7, 55));
        for (int y = start; y < start + band; y += 4)
        {
            if (Random.value < .22f) continue;
            for (int x = 0; x < Width; x += Random.Range(7, 17))
                if (Random.value > .18f)
                    Rect(x, y + Random.Range(0, 2), Random.Range(3, 13), 1,
                        new Color32(44, (byte)Random.Range(180, 255), 56, 240));
        }
    }

    // Narrow vertical RGB memory lanes, with a few white/black dead columns.
    private void ColorBars(float s)
    {
        int count = Mathf.RoundToInt(Mathf.Lerp(8f, 85f, s));
        for (int i = 0; i < count; i++)
        {
            int x = Random.Range(0, Width), y = Random.Range(0, Height);
            int height = Random.Range(8, 75);
            Rect(x, y, Random.Range(1, 4), height,
                new Color32((byte)Random.Range(70, 255),
                    (byte)Random.Range(70, 255), (byte)Random.Range(70, 255), 230));
            if (Random.value < .6f)
                Rect(x + 3, y - 2, 1, height + 5, new Color32(245, 245, 252, 235));
        }
        for (int i = 0; i < count * 2; i++)
            Rect(Random.Range(0, Width), Random.Range(0, Height), 2, 1,
                new Color32(255, 255, 255, 180));
    }

    // Repeated multi-color glyph/mosaic tiles: failed VRAM reads usually
    // repeat in regular pitches, unlike independent confetti rectangles.
    private void MemoryTiles(float s)
    {
        int y0 = Random.Range(0, 30);
        int rows = Mathf.RoundToInt(Mathf.Lerp(45f, Height, s));
        for (int y = y0; y < Mathf.Min(Height, y0 + rows); y += 6)
            for (int x = 0; x < Width; x += 8)
            {
                if (Random.value > .55f + .35f * s) continue;
                var c = new Color32((byte)Random.Range(30, 255),
                    (byte)Random.Range(30, 255), (byte)Random.Range(30, 255), 230);
                Rect(x, y, 3, 5, c);
                if ((x / 8 + y / 6 + frame / 4) % 3 == 0)
                    Rect(x + 3, y + 1, 4, 1, new Color32(233, 241, 247, 240));
                if (Random.value < .2f)
                    Rect(x, y + 5, 8, 1, new Color32(2, 2, 22, 245));
            }
    }

    // Wide displaced horizontal framebuffer stripes with checker offsets.
    private void TornBands(float s)
    {
        int bands = Mathf.RoundToInt(Mathf.Lerp(2f, 12f, s));
        for (int i = 0; i < bands; i++)
        {
            int y = Random.Range(0, Height), h = Random.Range(2, 18);
            Color32 tint = (i % 2 == 0) ? new Color32(31, 84, 191, 145)
                : new Color32(218, 237, 249, 155);
            Rect(0, y, Width, h, tint);
            int offset = Random.Range(0, 24);
            for (int x = offset; x < Width; x += Random.Range(18, 40))
            {
                Rect(x, y + 1, Random.Range(6, 25), 1, new Color32(247, 250, 253, 230));
                if (h > 5) Rect(x + 2, y + 4, Random.Range(5, 16), 2,
                    new Color32(13, 38, 87, 210));
            }
        }
    }

    // Keep the existing window-drag API: repainting a corrupt region can
    // erase a fragment until the next GPU refresh, without moving the OS UI.
    public void WipeBlocksAt(Vector2 screenPoint, Vector2 wipeSize)
    {
        if (image == null || !image.enabled || viewport == null || texture == null) return;
        var canvas = screen != null ? screen.GetComponent<Canvas>() : null;
        Camera eventCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? canvas.worldCamera : null;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                viewport, screenPoint, eventCamera, out var local)) return;
        var area = viewport.rect;
        int x = Mathf.RoundToInt((local.x - area.xMin) / Mathf.Max(1f, area.width) * Width);
        int y = Mathf.RoundToInt((local.y - area.yMin) / Mathf.Max(1f, area.height) * Height);
        int w = Mathf.CeilToInt(wipeSize.x / Mathf.Max(1f, area.width) * Width);
        int h = Mathf.CeilToInt(wipeSize.y / Mathf.Max(1f, area.height) * Height);
        Rect(x - w / 2, y - h / 2, w, h, Clear);
        texture.SetPixels32(pixels);
        texture.Apply(false, false);
    }

    private void OnDestroy()
    {
        if (texture != null) Destroy(texture);
        // A TexturedMonitor detaches its canvas from its GameObject; destroying
        // this GPU component must not orphan its transparent overlay.
        if (viewport != null) Destroy(viewport.gameObject);
    }
}
