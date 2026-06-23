using UnityEngine;

public struct ColorYmck
{
    public float y;
    public float m;
    public float c;
    public float k;

    public ColorYmck(float y, float m, float c, float k)
    {
        this.y = y;
        this.m = m;
        this.c = c;
        this.k = k;
    }

    public static ColorYmck FromRgb(float r, float g, float b)
    {
        float max = Mathf.Max(r, g, b);
        if (max == 0f) return new ColorYmck(0f, 0f, 0f, 1f);
        float k = 1f - max;
        float c = (max - r) / max;
        float m = (max - g) / max;
        float y = (max - b) / max;
        return new ColorYmck(y, m, c, k);
    }

    public static ColorYmck operator +(ColorYmck a, ColorYmck b)
    {
        return new ColorYmck(a.y + b.y, a.m + b.m, a.c + b.c, a.k + b.k);
    }

    public static ColorYmck operator -(ColorYmck a, ColorYmck b)
    {
        return new ColorYmck(a.y - b.y, a.m - b.m, a.c - b.c, a.k - b.k);
    }

    public Color ToRgb()
    {
        float r = (1f - c) * (1f - k);
        float g = (1f - m) * (1f - k);
        float b = (1f - y) * (1f - k);
        return new Color(r, g, b, 1f);
    }
}
