using UnityEngine;
using UnityEngine.UI;

public class RawImageAspect : RawImage
{
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        Rect rect = GetPixelAdjustedRect();
        Texture tex = mainTexture;

        if (tex == null)
            return;

        float texWidth = tex.width;
        float texHeight = tex.height;
        float rectWidth = rect.width;
        float rectHeight = rect.height;

        float texAspect = texWidth / texHeight;
        float rectAspect = rectWidth / rectHeight;

        float width, height;
        if (rectAspect > texAspect)
        {
            height = rectHeight;
            width = height * texAspect;
        }
        else
        {
            width = rectWidth;
            height = width / texAspect;
        }

        float x = (rectWidth - width) * 0.5f + rect.x;
        float y = (rectHeight - height) * 0.5f + rect.y;

        Rect uv = uvRect;
        Vector3 pos0 = new Vector3(x, y, 0);
        Vector3 pos1 = new Vector3(x + width, y, 0);
        Vector3 pos2 = new Vector3(x + width, y + height, 0);
        Vector3 pos3 = new Vector3(x, y + height, 0);

        vh.AddVert(pos0, color, new Vector2(uv.xMin, uv.yMin));
        vh.AddVert(pos1, color, new Vector2(uv.xMax, uv.yMin));
        vh.AddVert(pos2, color, new Vector2(uv.xMax, uv.yMax));
        vh.AddVert(pos3, color, new Vector2(uv.xMin, uv.yMax));

        vh.AddTriangle(0, 1, 2);
        vh.AddTriangle(2, 3, 0);
    }
}
