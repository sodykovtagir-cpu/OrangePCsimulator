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
            
            // iconParent has pivot (0.5, 0.5) and anchors stretch (0,0)-(1,1)
            // anchoredPosition (0,0) = center, (-w/2, h/2) = top-left
            float pw = parentRect.rect.width;
            float ph = parentRect.rect.height;
            
            // Grid origin (top-left corner in anchored coordinates)
            float originX = -pw / 2f + padding;
            float originY = ph / 2f - padding;
            
            // Grid bounds (bottom-right, accounting for cell size)
            float maxX = pw / 2f - padding - cellWidth;
            float maxY = -ph / 2f + padding + cellHeight;
            
            Vector2 pos = rectTransform.anchoredPosition;
            
            // Snap to grid relative to origin
            float gridX = Mathf.Round((pos.x - originX) / gridStepX) * gridStepX + originX;
            float gridY = Mathf.Round((pos.y - originY) / gridStepY) * gridStepY + originY;
            
            // Clamp to bounds
            gridX = Mathf.Clamp(gridX, originX, maxX);
            gridY = Mathf.Clamp(gridY, maxY, originY);
            
            rectTransform.anchoredPosition = new Vector2(gridX, gridY);
            
            // Save position
            var os = GetComponentInParent<OS.OperatingSystem>();
            if (os != null)
                os.SaveIconPosition(GetIconKey(), new Vector2(gridX, gridY));
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
