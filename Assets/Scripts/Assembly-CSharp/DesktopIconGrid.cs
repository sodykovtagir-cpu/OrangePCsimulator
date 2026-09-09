using System.Collections.Generic;
using UnityEngine;

namespace PC.Component.Software
{
    // Desktop coordinates are offsets from the top-left corner, not the canvas centre.
    // The grid deliberately has no viewport bounds: a smaller monitor only clips it.
    internal struct DesktopIconGrid
    {
        private readonly float originX;
        private readonly float originY;
        private readonly float stepX;
        private readonly float stepY;

        public static DesktopIconGrid Default => new DesktopIconGrid(70f, 70f, 20f, 20f, 20f);

        public DesktopIconGrid(float cellWidth, float cellHeight, float spacingX, float spacingY, float padding)
        {
            originX = padding + cellWidth * 0.5f;
            originY = -padding - cellHeight * 0.5f;
            stepX = Mathf.Max(1f, cellWidth + spacingX);
            stepY = Mathf.Max(1f, cellHeight + spacingY);
        }

        public Vector2Int GetCell(Vector2 position)
        {
            return new Vector2Int(
                Mathf.Max(0, Mathf.RoundToInt((position.x - originX) / stepX)),
                Mathf.Max(0, Mathf.RoundToInt((originY - position.y) / stepY)));
        }

        public Vector2 GetPosition(Vector2Int cell)
        {
            return new Vector2(originX + Mathf.Max(0, cell.x) * stepX,
                originY - Mathf.Max(0, cell.y) * stepY);
        }

        public int ColumnsForWidth(float width)
        {
            return Mathf.Max(1, Mathf.FloorToInt((width - 2f * originX) / stepX) + 1);
        }

        public Vector2 FirstFreePosition(int columns, HashSet<Vector2Int> occupied)
        {
            columns = Mathf.Max(1, columns);
            int count = occupied != null ? occupied.Count : 0;
            // Of count + 1 different cells at least one must be free. Rows can extend
            // below the viewport; never fall back to stacking every icon in cell 0.
            for (int index = 0; index <= count; index++)
            {
                var cell = new Vector2Int(index % columns, index / columns);
                if (occupied == null || !occupied.Contains(cell))
                    return GetPosition(cell);
            }
            return GetPosition(new Vector2Int(0, count + 1));
        }

        public Vector2 Snap(Vector2 position, HashSet<Vector2Int> occupied = null)
        {
            var desired = GetCell(position);
            if (occupied == null || !occupied.Contains(desired))
                return GetPosition(desired);

            for (int radius = 1; radius <= occupied.Count + 1; radius++)
            {
                for (int dx = -radius; dx <= radius; dx++)
                {
                    for (int dy = -radius; dy <= radius; dy++)
                    {
                        if (Mathf.Abs(dx) != radius && Mathf.Abs(dy) != radius) continue;
                        var cell = new Vector2Int(desired.x + dx, desired.y + dy);
                        if (cell.x < 0 || cell.y < 0 || occupied.Contains(cell)) continue;
                        return GetPosition(cell);
                    }
                }
            }
            return FirstFreePosition(1, occupied);
        }

        public static Vector2 FromLegacyCentre(Vector2 position, Vector2 sourceSize)
        {
            return position + new Vector2(sourceSize.x * 0.5f, -sourceSize.y * 0.5f);
        }
    }
}
