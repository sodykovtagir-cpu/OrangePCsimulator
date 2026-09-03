using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Cropped cover image that fades to transparent (default: left opaque → right empty).
/// </summary>
public class FadedCoverImage : MaskableGraphic
{
	[SerializeField] private Texture texture;
	[SerializeField] [Range(0f, 1f)] private float fadeStart = 0.4f;
	[SerializeField] private int fadeSteps = 12;

	public Texture Texture
	{
		get => texture;
		set
		{
			if (texture == value) return;
			texture = value;
			SetAllDirty();
		}
	}

	public float FadeStart
	{
		get => fadeStart;
		set
		{
			fadeStart = Mathf.Clamp01(value);
			SetVerticesDirty();
		}
	}

	public override Texture mainTexture
	{
		get { return texture != null ? texture : s_WhiteTexture; }
	}

	protected override void OnPopulateMesh(VertexHelper vh)
	{
		vh.Clear();
		if (texture == null) return;

		var rect = GetPixelAdjustedRect();
		if (rect.width <= 0.01f || rect.height <= 0.01f) return;

		var uv = CoverUv(rect, texture.width, texture.height);
		int steps = Mathf.Clamp(fadeSteps, 2, 32);
		var c = color;

		for (int i = 0; i < steps; i++)
		{
			float t0 = i / (float)steps;
			float t1 = (i + 1) / (float)steps;
			float a0 = AlphaAt(t0);
			float a1 = AlphaAt(t1);

			float x0 = rect.x + rect.width * t0;
			float x1 = rect.x + rect.width * t1;
			float y0 = rect.y;
			float y1 = rect.y + rect.height;

			float u0 = Mathf.Lerp(uv.x, uv.z, t0);
			float u1 = Mathf.Lerp(uv.x, uv.z, t1);

			var c0 = MakeColor(c, a0);
			var c1 = MakeColor(c, a1);

			int start = vh.currentVertCount;
			vh.AddVert(new Vector3(x0, y0, 0f), c0, new Vector2(u0, uv.y));
			vh.AddVert(new Vector3(x0, y1, 0f), c0, new Vector2(u0, uv.w));
			vh.AddVert(new Vector3(x1, y1, 0f), c1, new Vector2(u1, uv.w));
			vh.AddVert(new Vector3(x1, y0, 0f), c1, new Vector2(u1, uv.y));
			vh.AddTriangle(start + 0, start + 1, start + 2);
			vh.AddTriangle(start + 2, start + 3, start + 0);
		}
	}

	private float AlphaAt(float t)
	{
		if (t <= fadeStart) return 1f;
		if (fadeStart >= 0.999f) return 1f;
		float k = (t - fadeStart) / (1f - fadeStart);
		return 1f - Mathf.SmoothStep(0f, 1f, k);
	}

	private static Color32 MakeColor(Color c, float alphaMul)
	{
		float a = Mathf.Clamp01(c.a * alphaMul);
		return new Color32(
			(byte)(c.r * 255f),
			(byte)(c.g * 255f),
			(byte)(c.b * 255f),
			(byte)(a * 255f));
	}

	private static Vector4 CoverUv(Rect rect, float texW, float texH)
	{
		if (texW <= 0f || texH <= 0f) return new Vector4(0f, 0f, 1f, 1f);

		float spriteAspect = texW / texH;
		float rectAspect = rect.width / Mathf.Max(rect.height, 0.0001f);

		if (spriteAspect > rectAspect)
		{
			float visibleWidth = rectAspect / spriteAspect;
			float x = (1f - visibleWidth) * 0.5f;
			return new Vector4(x, 0f, x + visibleWidth, 1f);
		}

		float visibleHeight = spriteAspect / rectAspect;
		float y = (1f - visibleHeight) * 0.5f;
		return new Vector4(0f, y, 1f, y + visibleHeight);
	}
}
