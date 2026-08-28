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

        private float gridStepX => cellWidth + spacingX;
        private float gridStepY => cellHeight + spacingY;

        private void Awake()
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
            
            Vector2 pos = SnapToGrid(rectTransform.anchoredPosition);
            rectTransform.anchoredPosition = pos;
            
            var os = GetComponentInParent<OS.OperatingSystem>();
            if (os != null)
                os.SaveIconPosition(GetIconKey(), pos);
        }
        
        public Vector2 SnapToGrid(Vector2 pos)
        {
            if (parentRect == null) return pos;
            
            float pw = parentRect.rect.width;
            float ph = parentRect.rect.height;
            
            // Grid origin (top-left cell center in anchored coordinates)
            float originX = -pw / 2f + padding + cellWidth / 2f;
            float originY = ph / 2f - padding - cellHeight / 2f;
            
            // Snap to grid relative to origin
            float gridX = originX + Mathf.Round((pos.x - originX) / gridStepX) * gridStepX;
            float gridY = originY + Mathf.Round((pos.y - originY) / gridStepY) * gridStepY;
            
            // Clamp to bounds
            float maxX = pw / 2f - padding - cellWidth / 2f;
            float maxY = -ph / 2f + padding + cellHeight / 2f;
            
            gridX = Mathf.Clamp(gridX, originX, maxX);
            gridY = Mathf.Clamp(gridY, maxY, originY);
            
            return new Vector2(gridX, gridY);
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
