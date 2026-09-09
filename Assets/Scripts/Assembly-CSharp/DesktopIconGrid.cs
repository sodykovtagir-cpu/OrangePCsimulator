using System.Collections.Generic;
using UnityEngine;

namespace PC.Component.Software
{
    // Desktop coordinates are offsets from the top-left corner, not the canvas centre.
    // The saved grid is unbounded; optional fullscreen fitting acts on a separate view.
    internal struct DesktopIconGrid
    {
        private readonly float originX;
        private readonly float originY;
        private readonly float stepX;
        private readonly float stepY;
        private readonly Vector2 halfSize;

        public static DesktopIconGrid Default => new DesktopIconGrid(70f, 70f, 20f, 20f, 20f);

        public DesktopIconGrid(float cellWidth, float cellHeight, float spacingX, float spacingY, float padding)
        {
            originX = padding + cellWidth * 0.5f;
            originY = -padding - cellHeight * 0.5f;
            stepX = Mathf.Max(1f, cellWidth + spacingX);
            stepY = Mathf.Max(1f, cellHeight + spacingY);
            halfSize = new Vector2(Mathf.Max(0f, cellWidth) * 0.5f, Mathf.Max(0f, cellHeight) * 0.5f);
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

        public void ReserveCells(Vector2 position, HashSet<Vector2Int> occupied)
        {
            if (occupied == null) return;
            int firstColumn = Mathf.Max(0, Mathf.FloorToInt((position.x - halfSize.x * 2f - originX) / stepX) + 1);
            int lastColumn = Mathf.CeilToInt((position.x + halfSize.x * 2f - originX) / stepX) - 1;
            int firstRow = Mathf.Max(0, Mathf.FloorToInt((originY - position.y - halfSize.y * 2f) / stepY) + 1);
            int lastRow = Mathf.CeilToInt((originY - position.y + halfSize.y * 2f) / stepY) - 1;
            for (int row = firstRow; row <= lastRow; row++)
                for (int column = firstColumn; column <= lastColumn; column++)
                    occupied.Add(new Vector2Int(column, row));
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

        public bool FitsViewport(Vector2 position, Vector2 viewport, Vector4 clippingPadding)
        {
            return position.x - halfSize.x >= clippingPadding.x &&
                position.x + halfSize.x <= viewport.x - clippingPadding.z &&
                position.y + halfSize.y <= -clippingPadding.w &&
                position.y - halfSize.y >= -viewport.y + clippingPadding.y;
        }

        public bool Overlaps(Vector2 a, Vector2 b)
        {
            return Mathf.Abs(a.x - b.x) < halfSize.x * 2f &&
                Mathf.Abs(a.y - b.y) < halfSize.y * 2f;
        }

        // Only the fullscreen VIEW is passed here, never the saved monitor layout.
        // Keep every already-visible icon exactly where it is. Move an overflowing
        // icon to the closest free visible cell, or leave it untouched if none exists.
        public void FitOverflow(Dictionary<string, Vector2> positions, Vector2 viewport, Vector4 clippingPadding)
        {
            if (positions == null || positions.Count == 0) return;
            var overflowing = new List<string>();
            foreach (var pair in positions)
                if (!FitsViewport(pair.Value, viewport, clippingPadding)) overflowing.Add(pair.Key);
            if (overflowing.Count == 0) return;
            overflowing.Sort(System.StringComparer.Ordinal);

            int firstColumn = Mathf.Max(0, Mathf.CeilToInt((clippingPadding.x + halfSize.x - originX) / stepX));
            int lastColumn = Mathf.FloorToInt((viewport.x - clippingPadding.z - halfSize.x - originX) / stepX);
            int firstRow = Mathf.Max(0, Mathf.CeilToInt((originY + clippingPadding.w + halfSize.y) / stepY));
            int lastRow = Mathf.FloorToInt((originY + viewport.y - clippingPadding.y - halfSize.y) / stepY);
            if (firstColumn > lastColumn || firstRow > lastRow) return;

            var cells = new List<Vector2>();
            for (int row = firstRow; row <= lastRow; row++)
                for (int column = firstColumn; column <= lastColumn; column++)
                    cells.Add(GetPosition(new Vector2Int(column, row)));

            // Count actual rectangle overlaps, not just rounded cell indices. This
            // also respects manually/legacy-positioned icons between grid cells and
            // icons that are partly clipped but cannot be moved because the view is full.
            var occupancy = new int[cells.Count];
            foreach (var position in positions.Values)
                for (int i = 0; i < cells.Count; i++)
                    if (Overlaps(position, cells[i])) occupancy[i]++;

            foreach (var key in overflowing)
            {
                var previous = positions[key];
                int nearest = -1;
                double nearestDistance = double.MaxValue;
                for (int i = 0; i < cells.Count; i++)
                {
                    int ownReservation = Overlaps(previous, cells[i]) ? 1 : 0;
                    if (occupancy[i] > ownReservation) continue;
                    double dx = (double)cells[i].x - previous.x;
                    double dy = (double)cells[i].y - previous.y;
                    double distance = dx * dx + dy * dy;
                    if (distance >= nearestDistance) continue;
                    nearestDistance = distance;
                    nearest = i;
                }
                if (nearest < 0) continue;

                var fitted = cells[nearest];
                positions[key] = fitted;
                for (int i = 0; i < cells.Count; i++)
                {
                    if (Overlaps(previous, cells[i])) occupancy[i]--;
                    if (Overlaps(fitted, cells[i])) occupancy[i]++;
                }
            }
        }

        public static Vector2 FromLegacyCentre(Vector2 position, Vector2 sourceSize)
        {
            return position + new Vector2(sourceSize.x * 0.5f, -sourceSize.y * 0.5f);
        }
    }
}
