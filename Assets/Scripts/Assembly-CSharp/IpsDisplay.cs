using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using PC.Component;
using UnityEngine;

/// <summary>
/// Тонкая IPS-панель: полноцветный экран для корпуса и стола.
/// </summary>
/// <remarks>
/// Отличие от LedDisplay принципиальное. Матричный дисплей растягивает
/// крошечную текстуру (32x16) на всю поверхность, поэтому между пикселями
/// зияет сетка -- получается светодиодное табло. Здесь картинка живёт в полном
/// разрешении, а структура матрицы рисуется шейдером OrangePC/IPS Panel: пиксель
/// делится на три субполосы R/G/B, как в настоящей IPS. Вблизи видна честная
/// матрица, издали -- ровное изображение.
///
/// Панель умеет три вещи:
///   * показывать картинку или анимацию, загруженную из приложения;
///   * показывать показатели компьютера (частота, температура, память);
///   * и то и другое сразу -- показатели поверх обоев рабочего стола.
///
/// Последний режим стоит по умолчанию: свежекупленная панель сразу показывает
/// что-то осмысленное, а не чёрный прямоугольник.
/// </remarks>
public class IpsDisplay : Device
{
    /// <summary>Что показывает панель.</summary>
    public enum Mode
    {
        /// <summary>Показатели компьютера поверх обоев рабочего стола.</summary>
        StatsOnWallpaper = 0,

        /// <summary>Только загруженная картинка или анимация.</summary>
        Media = 1,

        /// <summary>Показатели поверх загруженной картинки.</summary>
        StatsOnMedia = 2,

        /// <summary>Только показатели на чёрном фоне.</summary>
        StatsOnly = 3
    }

    [SerializeField]
    [Tooltip("Поверхность экрана панели.")]
    private Renderer screen;

    [SerializeField]
    [Tooltip("Разрешение панели в пикселях.")]
    private Vector2Int resolution = new Vector2Int(480, 320);

    [SerializeField]
    [Tooltip("Что показывать. По умолчанию — показатели поверх обоев PCOS.")]
    private Mode mode = Mode.StatsOnWallpaper;

    [SerializeField]
    [Tooltip("Как часто обновлять показатели, в секундах.")]
    private float refreshInterval = 0.5f;

    [SerializeField]
    [Tooltip("Цвет текста показателей.")]
    private Color statsColor = new Color(0.65f, 1f, 0.95f, 1f);

    /// <summary>Кадры загруженной анимации. Один кадр — обычная картинка.</summary>
    private Texture2D[] frames;

    private int frameIndex;
    private float frameInterval = 0.1f;
    private float nextFrame;

    /// <summary>Холст, на котором собирается итоговое изображение.</summary>
    private Texture2D canvas;

    private Material mat;
    private string savedMedia;

    /// <summary>Плата, к которой подключена панель.</summary>
    private Motherboard board;

    /// <summary>Обои рабочего стола, снятые с системы.</summary>
    private Texture2D wallpaper;

    public Mode CurrentMode => mode;

    public Vector2Int Resolution => resolution;

    protected override void Awake()
    {
        base.Awake();

        if (screen != null) mat = screen.material;

        canvas = new Texture2D(
            Mathf.Max(8, resolution.x), Mathf.Max(8, resolution.y),
            TextureFormat.RGBA32, false);

        // Билинейная фильтрация, а не Point: пиксельную структуру рисует
        // шейдер, и дублировать её ступеньками текстуры не нужно.
        canvas.filterMode = FilterMode.Bilinear;

        if (mat != null) mat.mainTexture = canvas;
    }

    private void Start()
    {
        InvokeRepeating(nameof(Refresh), 0f, Mathf.Max(0.05f, refreshInterval));
    }

    /// <summary>Подключить панель к компьютеру.</summary>
    /// <remarks>
    /// Без платы панель не знает, чьи показатели рисовать. Вызывается из
    /// приложения управления, когда игрок выбирает панель в списке устройств.
    /// </remarks>
    public void Attach(Motherboard motherboard)
    {
        board = motherboard;
    }

    /// <summary>Сменить режим показа.</summary>
    public void SetMode(Mode value)
    {
        mode = value;
        Refresh();
    }

    /// <summary>
    /// Загрузить картинку или анимацию.
    /// </summary>
    /// <remarks>
    /// Кадры клонируются: исходные текстуры принадлежат приложению и могут
    /// быть уничтожены вместе с его окном.
    /// </remarks>
    public void UploadMedia(IEnumerable<Texture2D> source, float interval)
    {
        if (source == null) return;

        var list = source.Where(t => t != null)
                         .Select(FormatConverter.CloneTexture)
                         .ToArray();
        if (list.Length == 0) return;

        frames = list;
        frameIndex = 0;
        frameInterval = Mathf.Max(0.02f, interval);
        nextFrame = 0f;

        // Сохраняем в том же формате, что и матричные дисплеи: сейв
        // переживает перезапуск игры.
        var mov = new FormatConverter.Mov { texs = frames, interval = frameInterval };
        savedMedia = FormatConverter.MovToString(mov);

        // Одна картинка без показателей -- это просто фото на стене.
        if (mode == Mode.StatsOnWallpaper) mode = Mode.StatsOnMedia;

        Refresh();
    }

    /// <summary>Запомнить обои рабочего стола для фона.</summary>
    public void SetWallpaper(Texture2D texture)
    {
        wallpaper = texture;
    }

    public void Refresh()
    {
        if (canvas == null) return;

        AdvanceAnimation();
        DrawBackground();

        if (mode != Mode.Media) DrawStats();

        canvas.Apply(false);
    }

    /// <summary>Перелистнуть кадр анимации, если пришло время.</summary>
    private void AdvanceAnimation()
    {
        if (frames == null || frames.Length < 2) return;

        float now = Time.unscaledTime;
        if (now < nextFrame) return;

        nextFrame = now + frameInterval;
        frameIndex = (frameIndex + 1) % frames.Length;
    }

    private void DrawBackground()
    {
        Texture2D source = null;

        if (mode == Mode.Media || mode == Mode.StatsOnMedia)
            source = CurrentFrame();
        else if (mode == Mode.StatsOnWallpaper)
            source = wallpaper;

        if (source == null)
        {
            Fill(Color.black);
            return;
        }

        Blit(source);
    }

    private Texture2D CurrentFrame()
    {
        if (frames == null || frames.Length == 0) return null;
        return frames[Mathf.Clamp(frameIndex, 0, frames.Length - 1)];
    }

    private void Fill(Color color)
    {
        var pixels = canvas.GetPixels();
        for (int i = 0; i < pixels.Length; i++) pixels[i] = color;
        canvas.SetPixels(pixels);
    }

    /// <summary>
    /// Растянуть картинку на холст, сохраняя пропорции.
    /// </summary>
    /// <remarks>
    /// Простое растяжение по обеим осям плющит фото: обои экрана и кадр из
    /// видео почти всегда другой формы, чем панель. Вписываем по меньшей
    /// стороне, остальное заливаем чёрным.
    /// </remarks>
    private void Blit(Texture2D source)
    {
        int cw = canvas.width;
        int ch = canvas.height;

        var pixels = new Color[cw * ch];

        float scale = Mathf.Min((float)cw / source.width, (float)ch / source.height);
        int dw = Mathf.Max(1, Mathf.RoundToInt(source.width * scale));
        int dh = Mathf.Max(1, Mathf.RoundToInt(source.height * scale));
        int ox = (cw - dw) / 2;
        int oy = (ch - dh) / 2;

        for (int y = 0; y < ch; y++)
        {
            for (int x = 0; x < cw; x++)
            {
                int lx = x - ox;
                int ly = y - oy;

                if (lx < 0 || ly < 0 || lx >= dw || ly >= dh)
                {
                    pixels[y * cw + x] = Color.black;
                    continue;
                }

                float u = (lx + 0.5f) / dw;
                float v = (ly + 0.5f) / dh;
                pixels[y * cw + x] = source.GetPixelBilinear(u, v);
            }
        }

        canvas.SetPixels(pixels);
    }

    // ================= показатели =================

    /// <summary>
    /// Нарисовать показатели компьютера поверх фона.
    /// </summary>
    /// <remarks>
    /// Рисуем встроенным растровым шрифтом, а не UI-текстом: панель -- это
    /// объект сцены с текстурой, канваса у неё нет. Шрифт мелкий, но на
    /// разрешении 480x320 читается.
    /// </remarks>
    private void DrawStats()
    {
        var lines = CollectStats();
        if (lines.Count == 0) return;

        int scale = Mathf.Max(1, canvas.height / 90);
        int x = 4 * scale;
        int y = canvas.height - (PixelFont.Height + 2) * scale;

        foreach (var line in lines)
        {
            PixelFont.Draw(canvas, line, x, y, scale, statsColor);
            y -= (PixelFont.Height + 2) * scale;
            if (y < 0) break;
        }
    }

    /// <summary>Собрать строки показателей с подключённого компьютера.</summary>
    private List<string> CollectStats()
    {
        var lines = new List<string>();

        if (board == null)
        {
            lines.Add("NO SIGNAL");
            return lines;
        }

        var cpus = board.GetHardwares(HardwareType.CPU);
        if (cpus != null && cpus.Count > 0)
        {
            var cpu = cpus[0] as CPU;
            if (cpu != null)
            {
                lines.Add("CPU " + cpu.frequency.ToString("0.0") + " GHZ");

                var cooler = cpu.GetComponentInParent<ICooler>();
                if (cooler != null)
                    lines.Add("TEMP " + Mathf.RoundToInt(cooler.Temperature) + " C");
            }
        }

        var rams = board.GetHardwares(HardwareType.RAM);
        if (rams != null && rams.Count > 0)
        {
            // Именно Capacity: Score -- это баллы производительности,
            // а на панели нужен объём, как в приложении Info.
            int total = 0;
            for (int i = 0; i < rams.Count; i++)
                if (rams[i] != null) total += rams[i].Capacity;

            // Conversion.Size сам переводит мегабайты в ГБ/ТБ -- тот же
            // формат, что показывает приложение Info.
            lines.Add("RAM " + Conversion.Size(total).ToUpperInvariant());
        }

        var gpus = board.GetHardwares(HardwareType.GPU);
        if (gpus != null && gpus.Count > 0)
            lines.Add("GPU X" + gpus.Count);

        lines.Add(GameClock.Hour.ToString("00") + ":" + GameClock.Minute.ToString("00"));

        return lines;
    }

    // ================= сохранение =================

    public override void ToData(JObject jObject)
    {
        base.ToData(jObject);
        jObject["ipsMode"] = (int)mode;
        if (!string.IsNullOrEmpty(savedMedia)) jObject["ipsMedia"] = savedMedia;
    }

    public override void FromData(JObject jObject)
    {
        base.FromData(jObject);
        if (jObject == null) return;

        if (jObject.TryGetValue("ipsMode", out var modeToken) && modeToken != null)
            mode = (Mode)modeToken.ToObject<int>();

        if (jObject.TryGetValue("ipsMedia", out var mediaToken) && mediaToken != null)
        {
            savedMedia = mediaToken.ToString();
            var mov = FormatConverter.StringToMov(savedMedia, true);
            if (mov != null && mov.texs != null && mov.texs.Length > 0)
            {
                frames = mov.texs;
                frameInterval = Mathf.Max(0.02f, mov.interval);
            }
        }

        Refresh();
    }
}
