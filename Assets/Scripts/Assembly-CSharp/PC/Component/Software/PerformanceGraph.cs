using UnityEngine;
using UnityEngine.UI;

namespace PC.Component.Software
{
    /// <summary>A light, allocation-free PCOS history graph: grid, tinted area
    /// and live line. The colour and size are owned by the Task Manager UI.</summary>
    public sealed class PerformanceGraph : MaskableGraphic
    {
        private const int Samples = 72;
        private readonly float[] values = new float[Samples];
        private int count;
        private int next;

        public void Sample(float value)
        {
            if (count == 0)
            {
                // Show an immediately useful flat history instead of a chart
                // that only appears after the window has been open for 25 s.
                for (int i = 0; i < Samples; i++) values[i] = Mathf.Clamp01(value);
                count = Samples;
            }
            values[next] = Mathf.Clamp01(value);
            next = (next + 1) % Samples;
            SetVerticesDirty();
        }

        private static void Quad(VertexHelper mesh, Vector2 a, Vector2 b,
            Vector2 c, Vector2 d, Color ca, Color cb, Color cc, Color cd)
        {
            int start = mesh.currentVertCount;
            mesh.AddVert(a, ca, Vector2.zero);
            mesh.AddVert(b, cb, Vector2.zero);
            mesh.AddVert(c, cc, Vector2.zero);
            mesh.AddVert(d, cd, Vector2.zero);
            mesh.AddTriangle(start, start + 1, start + 2);
            mesh.AddTriangle(start, start + 2, start + 3);
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            Rect rect = GetPixelAdjustedRect();
            if (rect.width < 10 || rect.height < 10 || count < 2) return;
            float left = rect.xMin + 2f;
            float right = rect.xMax - 2f;
            float bottom = rect.yMin + 3f;
            float top = rect.yMax - 3f;
            float height = top - bottom;
            float step = (right - left) / (Samples - 1f);
            Color grid = new Color(.42f, .42f, .42f, .12f);
            for (int i = 0; i <= 4; i++)
            {
                float y = bottom + i * height / 4f;
                Quad(mesh, new Vector2(left, y), new Vector2(right, y),
                    new Vector2(right, y + 1f), new Vector2(left, y + 1f),
                    grid, grid, grid, grid);
            }
            for (int i = 0; i <= 6; i++)
            {
                float x = left + i * (right - left) / 6f;
                Quad(mesh, new Vector2(x, bottom), new Vector2(x + 1f, bottom),
                    new Vector2(x + 1f, top), new Vector2(x, top),
                    grid, grid, grid, grid);
            }

            Color lower = new Color(color.r, color.g, color.b, .025f);
            Color upper = new Color(color.r, color.g, color.b, .22f);
            Color line = new Color(color.r, color.g, color.b, .98f);
            for (int i = 0; i < Samples - 1; i++)
            {
                // next points to the oldest value once the circular buffer is full.
                float y1 = bottom + values[(next + i) % Samples] * height;
                float y2 = bottom + values[(next + i + 1) % Samples] * height;
                float x1 = left + i * step;
                float x2 = x1 + step;
                Quad(mesh, new Vector2(x1, bottom), new Vector2(x2, bottom),
                    new Vector2(x2, y2), new Vector2(x1, y1),
                    lower, lower, upper, upper);
                Vector2 start = new Vector2(x1, y1);
                Vector2 end = new Vector2(x2, y2);
                Vector2 tangent = (end - start).normalized;
                Vector2 normal = new Vector2(-tangent.y, tangent.x) * 1.35f;
                Quad(mesh, start - normal, end - normal, end + normal,
                    start + normal, line, line, line, line);
            }
        }
    }
}
