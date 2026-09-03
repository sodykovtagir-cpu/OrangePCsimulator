using UnityEngine;
using UnityEngine.UI;

public class CoverImage : MaskableGraphic
{
	public enum WallpaperFit
	{
		Fill = 0,
		Fit = 1,
		Stretch = 2,
		Center = 3,
		Tile = 4
	}

	[SerializeField]
	private Sprite sprite;

	[SerializeField]
	private bool reverse;

	[SerializeField]
	private WallpaperFit fitMode = WallpaperFit.Fill;

	public Sprite Sprite
	{
		get => sprite;
		set
        {
            var old = sprite;
			if (old == value) return;

			var oldSize = old != null ? old.rect.size : UnityEngine.Vector2.zero;
			var newSize = value != null ? value.rect.size : UnityEngine.Vector2.zero;

			sprite = value;
			SetAllDirty();
        }
	}

	public WallpaperFit FitMode
	{
		get => fitMode;
		set
		{
			if (fitMode == value) return;
			fitMode = value;
			SetAllDirty();
		}
	}

	public override Texture mainTexture {
		get
        {
            var sp = sprite;
			if (sp != null) return sp.texture;
			var mat = material;
			if (mat != null)
			{
				var tex = mat.mainTexture;
				if (tex != null) return tex;
			}
			return s_WhiteTexture;
        }
    }

	protected override void OnPopulateMesh(UnityEngine.UI.VertexHelper toFill)
	{
		if (sprite == null)
		{
			base.OnPopulateMesh(toFill);
			return;
		}
		GenerateSimpleSprite(toFill);
	}

	private void GenerateSimpleSprite(UnityEngine.UI.VertexHelper vh)
	{
		vh.Clear();

		var s = sprite;
		if (s == null) return;

		var rect = GetPixelAdjustedRect();
		var spriteSize = s.rect.size;
		if (spriteSize.x <= 0f || spriteSize.y <= 0f) return;

		var c = color;
		var c32 = new UnityEngine.Color32(
			(byte)(c.r * 255f),
			(byte)(c.g * 255f),
			(byte)(c.b * 255f),
			(byte)(c.a * 255f)
		);

		if (fitMode == WallpaperFit.Tile)
		{
			GenerateTiledSprite(vh, rect, spriteSize, c32);
			return;
		}

		UnityEngine.Vector4 dims;
		UnityEngine.Vector4 uv;

		switch (fitMode)
		{
			case WallpaperFit.Stretch:
				dims = new UnityEngine.Vector4(rect.x, rect.y, rect.x + rect.width, rect.y + rect.height);
				uv = new UnityEngine.Vector4(0f, 0f, 1f, 1f);
				break;
			case WallpaperFit.Fit:
			{
				float spriteAspect = spriteSize.x / spriteSize.y;
				float rectAspect = rect.width / Mathf.Max(rect.height, 0.0001f);
				float w, h;
				if (spriteAspect > rectAspect)
				{
					w = rect.width;
					h = w / spriteAspect;
				}
				else
				{
					h = rect.height;
					w = h * spriteAspect;
				}
				float x0 = rect.x + (rect.width - w) * 0.5f;
				float y0 = rect.y + (rect.height - h) * 0.5f;
				dims = new UnityEngine.Vector4(x0, y0, x0 + w, y0 + h);
				uv = new UnityEngine.Vector4(0f, 0f, 1f, 1f);
				break;
			}
			case WallpaperFit.Center:
			{
				float w = spriteSize.x;
				float h = spriteSize.y;
				if (w > rect.width || h > rect.height)
				{
					float scale = Mathf.Min(rect.width / w, rect.height / h);
					w *= scale;
					h *= scale;
				}
				float x0 = rect.x + (rect.width - w) * 0.5f;
				float y0 = rect.y + (rect.height - h) * 0.5f;
				dims = new UnityEngine.Vector4(x0, y0, x0 + w, y0 + h);
				uv = new UnityEngine.Vector4(0f, 0f, 1f, 1f);
				break;
			}
			default:
				dims = GetDrawingDimensions();
				uv = CalculateAspectRatio(rect, spriteSize);
				break;
		}

		AddQuad(vh, dims, uv, c32);
	}

	private void GenerateTiledSprite(UnityEngine.UI.VertexHelper vh, Rect rect, Vector2 spriteSize, Color32 c32)
	{
		float tileW = Mathf.Max(spriteSize.x, 1f);
		float tileH = Mathf.Max(spriteSize.y, 1f);
		int cols = Mathf.Max(1, Mathf.CeilToInt(rect.width / tileW));
		int rows = Mathf.Max(1, Mathf.CeilToInt(rect.height / tileH));

		for (int row = 0; row < rows; row++)
		{
			for (int col = 0; col < cols; col++)
			{
				float x0 = rect.x + col * tileW;
				float y0 = rect.y + row * tileH;
				float x1 = Mathf.Min(x0 + tileW, rect.x + rect.width);
				float y1 = Mathf.Min(y0 + tileH, rect.y + rect.height);
				float u1 = (x1 - x0) / tileW;
				float v1 = (y1 - y0) / tileH;
				var dims = new UnityEngine.Vector4(x0, y0, x1, y1);
				var uv = new UnityEngine.Vector4(0f, 0f, u1, v1);
				AddQuad(vh, dims, uv, c32);
			}
		}
	}

	private static void AddQuad(UnityEngine.UI.VertexHelper vh, UnityEngine.Vector4 dims, UnityEngine.Vector4 uv, UnityEngine.Color32 c32)
	{
		int start = vh.currentVertCount;

		var bl = new UnityEngine.Vector3(dims.x, dims.y, 0f);
		var tl = new UnityEngine.Vector3(dims.x, dims.w, 0f);
		var tr = new UnityEngine.Vector3(dims.z, dims.w, 0f);
		var br = new UnityEngine.Vector3(dims.z, dims.y, 0f);

		var uvBL = new UnityEngine.Vector2(uv.x, uv.y);
		var uvTL = new UnityEngine.Vector2(uv.x, uv.w);
		var uvTR = new UnityEngine.Vector2(uv.z, uv.w);
		var uvBR = new UnityEngine.Vector2(uv.z, uv.y);

		vh.AddVert(bl, c32, uvBL);
		vh.AddVert(tl, c32, uvTL);
		vh.AddVert(tr, c32, uvTR);
		vh.AddVert(br, c32, uvBR);

		vh.AddTriangle(start + 0, start + 1, start + 2);
		vh.AddTriangle(start + 2, start + 3, start + 0);
	}

	private Vector4 GetDrawingDimensions()
	{
		var s = sprite;
		var r = GetPixelAdjustedRect();
		if (s == null) return new UnityEngine.Vector4(r.x, r.y, r.x + r.width, r.y + r.height);

		var pad = UnityEngine.Sprites.DataUtility.GetPadding(s);
		var size = s.rect.size;
		if (size.x <= 0f || size.y <= 0f) return new UnityEngine.Vector4(r.x, r.y, r.x + r.width, r.y + r.height);

		float xMin = r.x + (pad.x / size.x) * r.width;
		float yMin = r.y + (pad.y / size.y) * r.height;
		float xMax = r.x + ((size.x - pad.z) / size.x) * r.width;
		float yMax = r.y + ((size.y - pad.w) / size.y) * r.height;

		return new UnityEngine.Vector4(xMin, yMin, xMax, yMax);
	}

    private Vector4 CalculateAspectRatio(Rect rect, Vector2 spriteSize)
    {
        float spriteAspect = spriteSize.x / spriteSize.y;
        float rectAspect = rect.width / rect.height;

        // Картинка шире экрана - обрезаем слева и справа
        if (spriteAspect > rectAspect)
        {
            float visibleWidth = rectAspect / spriteAspect;
            float x = (1f - visibleWidth) * 0.5f;

            return new Vector4(x, 0f, x + visibleWidth, 1f);
        }
        // Картинка выше экрана - обрезаем сверху и снизу
        else
        {
            float visibleHeight = spriteAspect / rectAspect;
            float y = (1f - visibleHeight) * 0.5f;

            return new Vector4(0f, y, 1f, y + visibleHeight);
        }
    }
}
