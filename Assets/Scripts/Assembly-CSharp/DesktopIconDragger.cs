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
        
        [SerializeField] private float gridSize = 80f;

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
            if (rectTransform == null) return;
            
            // Snap to grid
            Vector2 pos = rectTransform.anchoredPosition;
            pos.x = Mathf.Round(pos.x / gridSize) * gridSize;
            pos.y = Mathf.Round(pos.y / gridSize) * gridSize;
            rectTransform.anchoredPosition = pos;
            
            // Notify OperatingSystem to save position
            var os = GetComponentInParent<OS.OperatingSystem>();
            if (os != null)
                os.SaveIconPosition(GetIconKey(), pos);
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
