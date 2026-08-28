using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace PC.Component.Software
{
    public class DesktopIconDragger : MonoBehaviour, IDragHandler, IBeginDragHandler, IEndDragHandler, IEventSystemHandler
    {
        private RectTransform rectTransform;
        private RectTransform parentRect;
        private Vector2 dragOffset;
        private Camera eventCamera;
        
        [Header("Grid Settings")]
        [SerializeField] private float cellWidth = 70f;
        [SerializeField] private float cellHeight = 70f;
        [SerializeField] private float spacingX = 20f;
        [SerializeField] private float spacingY = 20f;
        [SerializeField] private float padding = 20f;
        [SerializeField] private float bottomPadding = 60f;

        private float gridStepX => cellWidth + spacingX;
        private float gridStepY => cellHeight + spacingY;

        private void Awake()
        {
            Init();
        }

        public void Init()
        {
            rectTransform = GetComponent<RectTransform>();
            var parent = transform.parent;
            if (parent != null)
                parentRect = parent.GetComponent<RectTransform>();
            
            var canvas = GetComponentInParent<Canvas>();
            if (canvas != null)
                eventCamera = canvas.worldCamera;
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (rectTransform == null || parentRect == null || eventData == null) return;
            
            Vector2 localMousePos;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                parentRect, eventData.position, eventCamera, out localMousePos))
            {
                dragOffset = localMousePos - rectTransform.anchoredPosition;
            }
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (rectTransform == null || parentRect == null || eventData == null) return;

            Vector2 localMousePos;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                parentRect, eventData.position, eventCamera, out localMousePos))
            {
                rectTransform.anchoredPosition = localMousePos - dragOffset;
            }
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (rectTransform == null || parentRect == null) return;
            
            Vector2 pos = SnapToGrid(rectTransform.anchoredPosition, true);
            rectTransform.anchoredPosition = pos;
            
            var os = GetComponentInParent<OS.OperatingSystem>();
            if (os != null)
                os.SaveIconPosition(GetIconKey(), pos);
        }
        
        public Vector2 SnapToGrid(Vector2 pos, bool avoidCollisions = false)
        {
            if (parentRect == null) return pos;
            
            float pw = parentRect.rect.width;
            float ph = parentRect.rect.height;
            
            float originX = -pw / 2f + padding + cellWidth / 2f;
            float originY = ph / 2f - padding - cellHeight / 2f;
            float maxY = -ph / 2f + bottomPadding + cellHeight / 2f;
            
            float gridX = originX + Mathf.Round((pos.x - originX) / gridStepX) * gridStepX;
            float gridY = originY + Mathf.Round((pos.y - originY) / gridStepY) * gridStepY;
            
            float maxX = pw / 2f - padding - cellWidth / 2f;
            
            gridX = Mathf.Clamp(gridX, originX, maxX);
            gridY = Mathf.Clamp(gridY, maxY, originY);
            
            if (avoidCollisions)
            {
                pos = new Vector2(gridX, gridY);
                return FindFreeCell(pos, originX, originY, maxX, maxY);
            }
            
            return new Vector2(gridX, gridY);
        }
        
        private Vector2 FindFreeCell(Vector2 targetPos, float originX, float originY, float maxX, float maxY)
        {
            var occupied = GetOccupiedCells();
            
            if (!IsCellOccupied(targetPos, occupied))
                return targetPos;
            
            int maxSearch = 100;
            for (int radius = 1; radius < maxSearch; radius++)
            {
                for (int dx = -radius; dx <= radius; dx++)
                {
                    for (int dy = -radius; dy <= radius; dy++)
                    {
                        if (Mathf.Abs(dx) != radius && Mathf.Abs(dy) != radius)
                            continue;
                        
                        float testX = targetPos.x + dx * gridStepX;
                        float testY = targetPos.y + dy * gridStepY;
                        
                        testX = Mathf.Clamp(testX, originX, maxX);
                        testY = Mathf.Clamp(testY, maxY, originY);
                        
                        var testPos = new Vector2(testX, testY);
                        if (!IsCellOccupied(testPos, occupied))
                            return testPos;
                    }
                }
            }
            
            return targetPos;
        }
        
        private bool IsCellOccupied(Vector2 pos, HashSet<Vector2Int> occupied)
        {
            var cell = GetCellCoordinates(pos);
            return occupied.Contains(cell);
        }
        
        private Vector2Int GetCellCoordinates(Vector2 pos)
        {
            float pw = parentRect.rect.width;
            float ph = parentRect.rect.height;
            float originX = -pw / 2f + padding + cellWidth / 2f;
            float originY = ph / 2f - padding - cellHeight / 2f;
            
            int col = Mathf.RoundToInt((pos.x - originX) / gridStepX);
            int row = Mathf.RoundToInt((originY - pos.y) / gridStepY);
            
            return new Vector2Int(col, row);
        }
        
        private HashSet<Vector2Int> GetOccupiedCells()
        {
            var occupied = new HashSet<Vector2Int>();
            
            if (parentRect == null) return occupied;
            
            var parent = transform.parent;
            if (parent == null) return occupied;
            
            for (int i = 0; i < parent.childCount; i++)
            {
                var child = parent.GetChild(i);
                if (child == null || child.gameObject == gameObject)
                    continue;
                
                var dragger = child.GetComponent<DesktopIconDragger>();
                if (dragger != null)
                {
                    var rt = child.GetComponent<RectTransform>();
                    if (rt != null)
                    {
                        var cell = GetCellCoordinates(rt.anchoredPosition);
                        occupied.Add(cell);
                    }
                }
            }
            
            return occupied;
        }
        
        private string GetIconKey()
        {
            var fileIcon = GetComponent<FileIcon>();
            if (fileIcon != null && fileIcon.File != null)
                return fileIcon.File.path;
            return null;
        }
    }
}
