using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace PC.Component.Software
{
    public static class WindowChrome
    {
        const string BorderName = "Win7Border";
        const string GlowName = "Win7Glow";
        const float OpenTime = 0.18f;
        const float CloseTime = 0.14f;

        static Sprite roundedFill;
        static Sprite roundedGlow;

        public static void Apply(RectTransform window)
        {
            if (window == null) return;
            if (window.Find(BorderName) != null) return;

            EnsureSprites();

            var image = window.GetComponent<Image>();
            if (image == null)
            {
                image = window.gameObject.AddComponent<Image>();
                image.color = new Color(0.94f, 0.96f, 0.98f, 0.97f);
            }

            image.sprite = roundedFill;
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = 1.35f;
            image.raycastTarget = true;

            var outline = window.GetComponent<Outline>();
            if (outline == null) outline = window.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.72f, 0.88f, 1f, 0.42f);
            outline.effectDistance = new Vector2(3.5f, -3.5f);
            outline.useGraphicAlpha = true;

            CreateLayer(window, GlowName, 10f, new Color(0.70f, 0.86f, 1f, 0.16f), 0);
            CreateLayer(window, BorderName, 5f, new Color(1f, 1f, 1f, 0.28f), 1);
        }

        public static IEnumerator PlayOpen(RectTransform window)
        {
            if (window == null) yield break;
            var cg = GetGroup(window.gameObject);
            cg.alpha = 0f;
            window.localScale = Vector3.one * 0.88f;

            float t = 0f;
            while (t < OpenTime && window != null)
            {
                // Не больше 25% длительности за кадр: фриз/лаг не должен
                // «телепортировать» анимацию в конец (иначе окно появляется скачком).
                t += Mathf.Min(Time.unscaledDeltaTime, OpenTime * 0.25f);
                float k = Mathf.Clamp01(t / OpenTime);
                k = 1f - (1f - k) * (1f - k);
                cg.alpha = k;
                float s = Mathf.Lerp(0.88f, 1f, k);
                window.localScale = new Vector3(s, s, 1f);
                yield return null;
            }

            if (window == null) yield break;
            cg.alpha = 1f;
            window.localScale = Vector3.one;
        }

        public static IEnumerator PlayClose(RectTransform window)
        {
            if (window == null) yield break;
            var cg = GetGroup(window.gameObject);
            Vector3 from = window.localScale;
            float fromAlpha = cg.alpha;
            float t = 0f;
            while (t < CloseTime && window != null)
            {
                t += Mathf.Min(Time.unscaledDeltaTime, CloseTime * 0.25f);
                float k = Mathf.Clamp01(t / CloseTime);
                k = k * k;
                cg.alpha = Mathf.Lerp(fromAlpha, 0f, k);
                float s = Mathf.Lerp(from.x, 0.90f, k);
                window.localScale = new Vector3(s, s, 1f);
                yield return null;
            }
        }

        public static IEnumerator PlayStartMenu(RectTransform menu, bool open)
        {
            if (menu == null) yield break;
            var cg = GetGroup(menu.gameObject);
            float time = open ? 0.20f : 0.14f;
            float fromA = open ? 0f : cg.alpha;
            float toA = open ? 1f : 0f;
            float fromS = open ? 0.86f : menu.localScale.x;
            float toS = open ? 1f : 0.90f;

            if (open)
            {
                cg.alpha = 0f;
                menu.localScale = Vector3.one * fromS;
            }

            float t = 0f;
            while (t < time && menu != null)
            {
                t += Mathf.Min(Time.unscaledDeltaTime, time * 0.25f);
                float k = Mathf.Clamp01(t / time);
                k = open ? (1f - (1f - k) * (1f - k)) : (k * k);
                cg.alpha = Mathf.Lerp(fromA, toA, k);
                float s = Mathf.Lerp(fromS, toS, k);
                menu.localScale = new Vector3(s, s, 1f);
                yield return null;
            }

            if (menu == null) yield break;
            cg.alpha = toA;
            menu.localScale = Vector3.one * toS;
            if (!open) menu.gameObject.SetActive(false);
        }

        static void CreateLayer(RectTransform window, string name, float outset, Color color, int sibling)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(window, false);
            go.transform.SetSiblingIndex(Mathf.Min(sibling, window.childCount - 1));
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(-outset, -outset);
            rt.offsetMax = new Vector2(outset, outset);
            var img = go.GetComponent<Image>();
            img.sprite = roundedGlow;
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = 1.1f;
            img.color = color;
            img.raycastTarget = false;
        }

        static CanvasGroup GetGroup(GameObject go)
        {
            var cg = go.GetComponent<CanvasGroup>();
            if (cg == null) cg = go.AddComponent<CanvasGroup>();
            return cg;
        }

        static void EnsureSprites()
        {
            if (roundedFill == null)
                roundedFill = MakeRounded(64, 14, 16, false);
            if (roundedGlow == null)
                roundedGlow = MakeRounded(64, 16, 18, true);
        }

        static Sprite MakeRounded(int size, int radius, int border, bool softEdge)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            var pixels = new Color32[size * size];
            float r = radius;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = SignedRoundDist(x + 0.5f, y + 0.5f, size, r);
                    float a;
                    if (softEdge)
                    {
                        a = Mathf.Clamp01(1f - Mathf.Abs(d) / 6f);
                        if (d < -2f) a = Mathf.Clamp01(0.35f + (-d) / (size * 0.35f));
                    }
                    else
                    {
                        a = Mathf.Clamp01(0.6f - d);
                    }

                    byte b = (byte)Mathf.RoundToInt(a * 255f);
                    pixels[y * size + x] = new Color32(255, 255, 255, b);
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply(false, false);
            var sprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(border, border, border, border));
            sprite.name = softEdge ? "Win7GlowSlice" : "Win7FillSlice";
            return sprite;
        }

        static float SignedRoundDist(float x, float y, int size, float r)
        {
            float min = r;
            float max = size - r;
            float cx = Mathf.Clamp(x, min, max);
            float cy = Mathf.Clamp(y, min, max);
            if (x >= min && x <= max && y >= min && y <= max)
                return -Mathf.Min(x, y, size - x, size - y);
            float dx = x - cx;
            float dy = y - cy;
            return Mathf.Sqrt(dx * dx + dy * dy) - r;
        }
    }
}
