using UnityEngine;
using UnityEngine.UI;

public class CoverImage : MaskableGraphic
{
	[SerializeField]
	private Sprite sprite;

	[SerializeField]
	private bool reverse;

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
		var dims = GetDrawingDimensions();
		var rect = GetPixelAdjustedRect();
		var s = sprite;
		var uv = s != null ? CalculateAspectRatio(rect, s.rect.size) : new UnityEngine.Vector4(0f, 0f, 1f, 1f);

		var c = color;
		var c32 = new UnityEngine.Color32(
			(byte)(c.r * 255f),
			(byte)(c.g * 255f),
			(byte)(c.b * 255f),
			(byte)(c.a * 255f)
		);

		vh.Clear();

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

		vh.AddTriangle(0, 1, 2);
		vh.AddTriangle(2, 3, 0);
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
