using UnityEngine;

/// <summary>
/// Крошечный растровый шрифт для рисования прямо в текстуру.
/// </summary>
/// <remarks>
/// Зачем он нужен. Панель IPS -- это объект сцены с текстурой, а не элемент
/// интерфейса: канваса у неё нет, и UI-текст поверх неё не повесить. Значит
/// буквы нужно рисовать пикселями прямо в изображение экрана.
///
/// Набор символов ограничен тем, что реально показывают панели: цифры, заглавные
/// латинские буквы и немного знаков. Этого хватает на «CPU 4.2 GHZ» и «TEMP 58 C»,
/// а полноценный шрифт тянуть незачем.
///
/// Каждый символ -- 5 пикселей в ширину и 7 в высоту, закодирован пятью байтами
/// по колонкам: младший бит байта -- верхняя точка колонки.
/// </remarks>
public static class PixelFont
{
    /// <summary>Высота символа в пикселях.</summary>
    public const int Height = 7;

    /// <summary>Ширина символа в пикселях, без межбуквенного зазора.</summary>
    public const int Width = 5;

    // Каждая строка -- пять колонок по 7 значащих бит.
    private static readonly byte[] digits =
    {
        0x3E, 0x51, 0x49, 0x45, 0x3E, // 0
        0x00, 0x42, 0x7F, 0x40, 0x00, // 1
        0x42, 0x61, 0x51, 0x49, 0x46, // 2
        0x21, 0x41, 0x45, 0x4B, 0x31, // 3
        0x18, 0x14, 0x12, 0x7F, 0x10, // 4
        0x27, 0x45, 0x45, 0x45, 0x39, // 5
        0x3C, 0x4A, 0x49, 0x49, 0x30, // 6
        0x01, 0x71, 0x09, 0x05, 0x03, // 7
        0x36, 0x49, 0x49, 0x49, 0x36, // 8
        0x06, 0x49, 0x49, 0x29, 0x1E  // 9
    };

    private static readonly byte[] letters =
    {
        0x7E, 0x11, 0x11, 0x11, 0x7E, // A
        0x7F, 0x49, 0x49, 0x49, 0x36, // B
        0x3E, 0x41, 0x41, 0x41, 0x22, // C
        0x7F, 0x41, 0x41, 0x22, 0x1C, // D
        0x7F, 0x49, 0x49, 0x49, 0x41, // E
        0x7F, 0x09, 0x09, 0x09, 0x01, // F
        0x3E, 0x41, 0x49, 0x49, 0x7A, // G
        0x7F, 0x08, 0x08, 0x08, 0x7F, // H
        0x00, 0x41, 0x7F, 0x41, 0x00, // I
        0x20, 0x40, 0x41, 0x3F, 0x01, // J
        0x7F, 0x08, 0x14, 0x22, 0x41, // K
        0x7F, 0x40, 0x40, 0x40, 0x40, // L
        0x7F, 0x02, 0x0C, 0x02, 0x7F, // M
        0x7F, 0x04, 0x08, 0x10, 0x7F, // N
        0x3E, 0x41, 0x41, 0x41, 0x3E, // O
        0x7F, 0x09, 0x09, 0x09, 0x06, // P
        0x3E, 0x41, 0x51, 0x21, 0x5E, // Q
        0x7F, 0x09, 0x19, 0x29, 0x46, // R
        0x46, 0x49, 0x49, 0x49, 0x31, // S
        0x01, 0x01, 0x7F, 0x01, 0x01, // T
        0x3F, 0x40, 0x40, 0x40, 0x3F, // U
        0x1F, 0x20, 0x40, 0x20, 0x1F, // V
        0x7F, 0x20, 0x18, 0x20, 0x7F, // W
        0x63, 0x14, 0x08, 0x14, 0x63, // X
        0x03, 0x04, 0x78, 0x04, 0x03, // Y
        0x61, 0x51, 0x49, 0x45, 0x43  // Z
    };

    /// <summary>Колонки символа. Пустой массив, если символ неизвестен.</summary>
    private static bool TryGetGlyph(char c, out byte[] table, out int offset)
    {
        table = null;
        offset = 0;

        if (c >= '0' && c <= '9')
        {
            table = digits;
            offset = (c - '0') * Width;
            return true;
        }

        if (c >= 'a' && c <= 'z') c = char.ToUpperInvariant(c);

        if (c >= 'A' && c <= 'Z')
        {
            table = letters;
            offset = (c - 'A') * Width;
            return true;
        }

        return false;
    }

    /// <summary>Отдельные знаки, которые не укладываются в диапазоны.</summary>
    private static byte[] Symbol(char c)
    {
        switch (c)
        {
            case '.': return new byte[] { 0x00, 0x40, 0x60, 0x00, 0x00 };
            case ':': return new byte[] { 0x00, 0x00, 0x36, 0x00, 0x00 };
            case '-': return new byte[] { 0x08, 0x08, 0x08, 0x08, 0x08 };
            case '%': return new byte[] { 0x23, 0x13, 0x08, 0x64, 0x62 };
            case '/': return new byte[] { 0x20, 0x10, 0x08, 0x04, 0x02 };
            case '+': return new byte[] { 0x08, 0x08, 0x3E, 0x08, 0x08 };
            default: return null;
        }
    }

    /// <summary>
    /// Нарисовать строку в текстуру.
    /// </summary>
    /// <param name="target">Куда рисуем.</param>
    /// <param name="text">Что рисуем.</param>
    /// <param name="x">Левый край, в пикселях текстуры.</param>
    /// <param name="y">Нижний край строки.</param>
    /// <param name="scale">Во сколько раз увеличить символы.</param>
    /// <param name="color">Цвет текста.</param>
    /// <remarks>
    /// Рисуем прямо по пикселям без Apply: вызывающий код обновляет текстуру
    /// один раз после всех строк, иначе каждая строка стоила бы отдельной
    /// отправки на видеокарту.
    /// </remarks>
    public static void Draw(Texture2D target, string text, int x, int y,
                            int scale, Color color)
    {
        if (target == null || string.IsNullOrEmpty(text)) return;
        if (scale < 1) scale = 1;

        int cursor = x;

        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];

            if (c == ' ')
            {
                cursor += (Width + 1) * scale;
                continue;
            }

            byte[] table;
            int offset;
            byte[] custom = null;

            if (!TryGetGlyph(c, out table, out offset))
            {
                custom = Symbol(c);
                if (custom == null)
                {
                    // Неизвестный символ пропускаем, а не рисуем мусором.
                    cursor += (Width + 1) * scale;
                    continue;
                }
            }

            for (int col = 0; col < Width; col++)
            {
                byte bits = custom != null ? custom[col] : table[offset + col];

                for (int row = 0; row < Height; row++)
                {
                    if ((bits & (1 << row)) == 0) continue;

                    // Верхний бит -- верхняя точка, а текстура растёт снизу
                    // вверх, поэтому строку переворачиваем.
                    int px = cursor + col * scale;
                    int py = y + (Height - 1 - row) * scale;

                    FillBlock(target, px, py, scale, color);
                }
            }

            cursor += (Width + 1) * scale;
        }
    }

    /// <summary>Закрасить квадрат scale x scale — один «пиксель» шрифта.</summary>
    private static void FillBlock(Texture2D target, int x, int y, int scale, Color color)
    {
        for (int dy = 0; dy < scale; dy++)
        {
            int py = y + dy;
            if (py < 0 || py >= target.height) continue;

            for (int dx = 0; dx < scale; dx++)
            {
                int px = x + dx;
                if (px < 0 || px >= target.width) continue;

                target.SetPixel(px, py, color);
            }
        }
    }
}
